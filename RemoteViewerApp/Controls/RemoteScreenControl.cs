using RemoteViewerApp.DTOs;
using RemoteViewerApp.Helpers;
using RemoteViewerApp.Services;

namespace RemoteViewerApp.Controls;

/// <summary>
/// PictureBox tuỳ chỉnh hiển thị màn hình remote.
/// - Bắt sự kiện chuột và tính lại tọa độ theo tỉ lệ màn hình Host
/// - Bắt sự kiện bàn phím và gửi lên server
/// - Hiển thị cursor vị trí chuột Host
/// </summary>
public sealed class RemoteScreenControl : PictureBox
{
    private SignalRViewerService? _signalR;
    private string _sessionId = string.Empty;
    private int _hostScreenWidth = 1920;
    private int _hostScreenHeight = 1080;

    // Vị trí chuột Host (pixel thực) để vẽ cursor chồng lên ảnh
    private int _hostMouseX;
    private int _hostMouseY;
    private bool _showHostCursor = true;

    // Throttle mouse move để không spam server
    private DateTime _lastMoveSent = DateTime.MinValue;
    private const int MoveThrottleMs = 33; // ~30fps

    public RemoteScreenControl()
    {
        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;
        SizeMode = PictureBoxSizeMode.Zoom;
        BackColor = Color.Black;
        BorderStyle = BorderStyle.None;

        // Chuột
        MouseDown   += OnMouseDown;
        MouseUp     += OnMouseUp;
        MouseMove   += OnMouseMove;
        MouseWheel  += OnMouseWheel;
        MouseClick  += OnMouseClick;
        MouseDoubleClick += OnMouseDoubleClick;

        // Bàn phím
        KeyDown += OnKeyDown;
        KeyUp   += OnKeyUp;

        // Nhận focus khi click
        MouseDown += (_, _) => Focus();
    }

    // ─── Public API ───────────────────────────────────────────────────────────

    public void Initialize(SignalRViewerService signalR, string sessionId)
    {
        _signalR  = signalR;
        _sessionId = sessionId;
    }

    public void Detach()
    {
        _signalR  = null;
        _sessionId = string.Empty;
    }

    public void UpdateFrame(System.Drawing.Image image, int screenW, int screenH, int mouseX, int mouseY)
    {
        _hostScreenWidth  = screenW;
        _hostScreenHeight = screenH;
        _hostMouseX = mouseX;
        _hostMouseY = mouseY;

        if (InvokeRequired)
            BeginInvoke(() => UpdateFrameUi(image));
        else
            UpdateFrameUi(image);
    }

    private void UpdateFrameUi(System.Drawing.Image image)
    {
        var old = Image;
        Image = image;
        old?.Dispose();
        Invalidate();
    }

    // ─── Vẽ cursor Host ───────────────────────────────────────────────────────

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        if (!_showHostCursor || Image == null) return;

        // Tính vị trí cursor Host trên control (theo tỉ lệ Zoom)
        var (cx, cy) = HostToControl(_hostMouseX, _hostMouseY);

        // Vẽ hình chữ thập nhỏ màu đỏ
        using var pen = new Pen(Color.Red, 2f);
        const int r = 8;
        e.Graphics.DrawLine(pen, cx - r, cy, cx + r, cy);
        e.Graphics.DrawLine(pen, cx, cy - r, cx, cy + r);
        using var brush = new SolidBrush(Color.FromArgb(180, Color.Red));
        e.Graphics.FillEllipse(brush, cx - 3, cy - 3, 6, 6);
    }

    // ─── Tính tọa độ ─────────────────────────────────────────────────────────

    /// <summary>
    /// Chuyển tọa độ chuột trên control sang tọa độ pixel thực trên màn hình Host.
    /// Tính đến PictureBoxSizeMode.Zoom (có thể có letterbox).
    /// </summary>
    private (int hostX, int hostY) ControlToHost(int controlX, int controlY)
    {
        if (Image == null || _hostScreenWidth == 0 || _hostScreenHeight == 0)
            return (controlX, controlY);

        var imgRect = GetImageRect();
        if (imgRect.Width == 0 || imgRect.Height == 0) return (0, 0);

        var relX = (controlX - imgRect.X) / (double)imgRect.Width;
        var relY = (controlY - imgRect.Y) / (double)imgRect.Height;

        relX = Math.Clamp(relX, 0, 1);
        relY = Math.Clamp(relY, 0, 1);

        return ((int)(relX * _hostScreenWidth), (int)(relY * _hostScreenHeight));
    }

    private (int cx, int cy) HostToControl(int hostX, int hostY)
    {
        var imgRect = GetImageRect();
        if (imgRect.Width == 0 || imgRect.Height == 0) return (0, 0);

        var cx = imgRect.X + (int)(hostX / (double)_hostScreenWidth  * imgRect.Width);
        var cy = imgRect.Y + (int)(hostY / (double)_hostScreenHeight * imgRect.Height);
        return (cx, cy);
    }

    /// <summary>
    /// Tính vùng ảnh thực tế trong PictureBox với SizeMode=Zoom (có letterbox).
    /// </summary>
    private Rectangle GetImageRect()
    {
        if (Image == null) return ClientRectangle;

        var ctrlAspect  = (double)Width  / Height;
        var imgAspect   = (double)Image.Width / Image.Height;

        int w, h, x, y;
        if (ctrlAspect > imgAspect)
        {
            // Letterbox ngang
            h = Height;
            w = (int)(h * imgAspect);
            x = (Width - w) / 2;
            y = 0;
        }
        else
        {
            // Letterbox dọc
            w = Width;
            h = (int)(w / imgAspect);
            x = 0;
            y = (Height - h) / 2;
        }

        return new Rectangle(x, y, w, h);
    }

    // ─── Mouse events ─────────────────────────────────────────────────────────

    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        // Xử lý trong MouseClick / DoubleClick để tránh gửi 2 lần
    }

    private void OnMouseUp(object? sender, MouseEventArgs e)
    {
        // Không cần gửi riêng MouseUp vì Host App bắt LeftClick/RightClick (Down+Up liền)
    }

    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
        if (_signalR == null || string.IsNullOrEmpty(_sessionId)) return;

        // Throttle
        if ((DateTime.Now - _lastMoveSent).TotalMilliseconds < MoveThrottleMs) return;
        _lastMoveSent = DateTime.Now;

        var (hx, hy) = ControlToHost(e.X, e.Y);
        _ = _signalR.SendMouseEvent(new MouseEventDto
        {
            SessionId    = _sessionId,
            Action       = "Move",
            X            = hx,
            Y            = hy,
            ScreenWidth  = _hostScreenWidth,
            ScreenHeight = _hostScreenHeight,
            Button       = 0,
            Delta        = 0,
            SentAt       = DateTime.UtcNow
        });
    }

    private void OnMouseClick(object? sender, MouseEventArgs e)
    {
        if (_signalR == null || string.IsNullOrEmpty(_sessionId)) return;

        var (hx, hy) = ControlToHost(e.X, e.Y);
        var action = e.Button switch
        {
            MouseButtons.Left   => "LeftClick",
            MouseButtons.Right  => "RightClick",
            MouseButtons.Middle => "LeftClick", // fallback
            _ => null
        };
        if (action == null) return;

        var btn = e.Button switch
        {
            MouseButtons.Right  => 2,
            MouseButtons.Middle => 1,
            _                   => 0
        };

        _ = _signalR.SendMouseEvent(new MouseEventDto
        {
            SessionId    = _sessionId,
            Action       = action,
            X            = hx,
            Y            = hy,
            ScreenWidth  = _hostScreenWidth,
            ScreenHeight = _hostScreenHeight,
            Button       = btn,
            Delta        = 0,
            SentAt       = DateTime.UtcNow
        });
        LoggingHelper.Debug($"Mouse {action} @ host({hx},{hy})");
    }

    private void OnMouseDoubleClick(object? sender, MouseEventArgs e)
    {
        if (_signalR == null || string.IsNullOrEmpty(_sessionId)) return;
        if (e.Button != MouseButtons.Left) return;

        var (hx, hy) = ControlToHost(e.X, e.Y);
        _ = _signalR.SendMouseEvent(new MouseEventDto
        {
            SessionId    = _sessionId,
            Action       = "DoubleClick",
            X            = hx,
            Y            = hy,
            ScreenWidth  = _hostScreenWidth,
            ScreenHeight = _hostScreenHeight,
            Button       = 0,
            Delta        = 0,
            SentAt       = DateTime.UtcNow
        });
        LoggingHelper.Debug($"Mouse DoubleClick @ host({hx},{hy})");
    }

    private void OnMouseWheel(object? sender, MouseEventArgs e)
    {
        if (_signalR == null || string.IsNullOrEmpty(_sessionId)) return;

        var (hx, hy) = ControlToHost(e.X, e.Y);
        _ = _signalR.SendMouseEvent(new MouseEventDto
        {
            SessionId    = _sessionId,
            Action       = "Scroll",
            X            = hx,
            Y            = hy,
            ScreenWidth  = _hostScreenWidth,
            ScreenHeight = _hostScreenHeight,
            Button       = 0,
            Delta        = e.Delta / 120, // chuẩn hoá về đơn vị 1 notch
            SentAt       = DateTime.UtcNow
        });
    }

    // ─── Keyboard events ──────────────────────────────────────────────────────

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_signalR == null || string.IsNullOrEmpty(_sessionId)) return;

        // Không để WinForms xử lý các phím điều hướng (arrow, tab, etc.)
        e.Handled = true;
        e.SuppressKeyPress = true;

        var (keyName, keyCode) = KeyMapper.Map(e.KeyCode);
        _ = _signalR.SendKeyboardEvent(new KeyboardEventDto
        {
            SessionId = _sessionId,
            Action    = "KeyDown",
            Key       = keyName,
            Code      = keyCode,
            CtrlKey   = e.Control,
            ShiftKey  = e.Shift,
            AltKey    = e.Alt,
            SentAt    = DateTime.UtcNow
        });
        LoggingHelper.Debug($"KeyDown: {keyName} (Ctrl={e.Control} Shift={e.Shift} Alt={e.Alt})");
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        if (_signalR == null || string.IsNullOrEmpty(_sessionId)) return;

        e.Handled = true;

        var (keyName, keyCode) = KeyMapper.Map(e.KeyCode);
        _ = _signalR.SendKeyboardEvent(new KeyboardEventDto
        {
            SessionId = _sessionId,
            Action    = "KeyUp",
            Key       = keyName,
            Code      = keyCode,
            CtrlKey   = e.Control,
            ShiftKey  = e.Shift,
            AltKey    = e.Alt,
            SentAt    = DateTime.UtcNow
        });
    }

    // Cho phép bắt Tab key
    protected override bool IsInputKey(Keys keyData) => true;

    // Cho phép bắt toàn bộ phím kể cả phím hệ thống
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (_signalR != null && !string.IsNullOrEmpty(_sessionId))
        {
            // Để OnKeyDown xử lý
            return false;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}
