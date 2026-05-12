using RemoteViewerApp.Controls;
using RemoteViewerApp.DTOs;
using RemoteViewerApp.Helpers;
using RemoteViewerApp.Models;
using RemoteViewerApp.Services;
using System.Drawing.Imaging;
using System.IO;

namespace RemoteViewerApp.Forms;

/// <summary>
/// Form chính của Viewer App.
/// Quản lý kết nối, chọn Host, nhận frame màn hình và gửi input.
/// </summary>
public partial class MainForm : Form
{
    // ─── Services ─────────────────────────────────────────────────────────────
    private readonly SignalRViewerService _signalR;
    private readonly AppSettings _settings;

    // ─── State ────────────────────────────────────────────────────────────────
    private ActiveSession? _currentSession;
    private bool _isClosing;

    // Thống kê frame
    private int _frameCount;
    private DateTime _lastFpsCalc = DateTime.Now;
    private int _lastFrameCount;

    // ─── Controls ─────────────────────────────────────────────────────────────
    private TextBox txtServerUrl = null!;
    private TextBox txtViewerId = null!;
    private TextBox txtViewerName = null!;
    private TextBox txtUserId = null!;
    private Button btnConnect = null!;
    private Button btnRegister = null!;
    private Button btnRefreshHosts = null!;
    private Button btnRequestControl = null!;
    private Button btnEndControl = null!;
    private ComboBox cmbHosts = null!;
    private Label lblStatus = null!;
    private Label lblFps = null!;
    private Label lblResolution = null!;
    private RichTextBox rtbLog = null!;
    private RemoteScreenControl screenControl = null!;
    private SplitContainer splitMain = null!;
    private Panel pnlSidebar = null!;
    private TabControl tabMain = null!;

    private Label lblHint = null!;

    public MainForm(AppSettings settings, SignalRViewerService signalR)
    {
        _settings = settings;
        _signalR = signalR;

        BuildUi();
        SetupSignalREvents();
        SetupLogging();

        // Giá trị mặc định
        txtServerUrl.Text = settings.ServerUrl;
        txtViewerId.Text = $"VIEWER-{Environment.MachineName}-{Guid.NewGuid():N}".Substring(0, 28);
        txtViewerName.Text = $"Viewer@{Environment.MachineName}";
        txtUserId.Text = $"USER-{Environment.MachineName}";

        UpdateUI();
    }

    // ─── UI Setup ─────────────────────────────────────────────────────────────

    private void BuildUi()
    {
        Text = "RemoteViewerApp – Remote Controller Viewer";
        Size = new Size(1200, 750);
        MinimumSize = new Size(900, 600);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9);
        FormClosing += OnFormClosing;

        // ── Split: sidebar bên trái | màn hình bên phải ──────────────────────
        splitMain = new SplitContainer
        {
            Dock = DockStyle.Fill,
            //SplitterDistance = 300,
            //Panel1MinSize = 260,
            //Panel2MinSize = 400,
            FixedPanel = FixedPanel.Panel1
        };

        // ── SIDEBAR ──────────────────────────────────────────────────────────
        pnlSidebar = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };

        tabMain = new TabControl { Dock = DockStyle.Fill };

        var tabConn = new TabPage("🔌 Kết nối");
        BuildConnectionTab(tabConn);

        var tabLog = new TabPage("📋 Log");
        BuildLogTab(tabLog);

        tabMain.TabPages.Add(tabConn);
        tabMain.TabPages.Add(tabLog);

        pnlSidebar.Controls.Add(tabMain);
        splitMain.Panel1.Controls.Add(pnlSidebar);

        // ── SCREEN PANEL ──────────────────────────────────────────────────────
        var pnlScreen = new Panel { Dock = DockStyle.Fill, BackColor = Color.Black };

        // Status bar phía trên màn hình
        var pnlScreenTop = new Panel
        {
            Dock = DockStyle.Top,
            Height = 36,
            BackColor = Color.FromArgb(30, 30, 30),
            Padding = new Padding(8, 0, 8, 0)
        };

        lblStatus = new Label
        {
            Text = "● Offline",
            ForeColor = Color.Gray,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            AutoSize = false,
            Dock = DockStyle.Left,
            Width = 200,
            TextAlign = ContentAlignment.MiddleLeft
        };

        lblFps = new Label
        {
            Text = "FPS: –",
            ForeColor = Color.LightGray,
            Font = new Font("Consolas", 8.5f),
            AutoSize = false,
            Dock = DockStyle.Left,
            Width = 90,
            TextAlign = ContentAlignment.MiddleLeft
        };

        lblResolution = new Label
        {
            Text = "–",
            ForeColor = Color.LightGray,
            Font = new Font("Consolas", 8.5f),
            AutoSize = false,
            Dock = DockStyle.Left,
            Width = 160,
            TextAlign = ContentAlignment.MiddleLeft
        };

        // Nút End Control ở góc phải
        btnEndControl = new Button
        {
            Text = "🛑 End Control",
            Dock = DockStyle.Right,
            Width = 130,
            BackColor = Color.FromArgb(200, 40, 40),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Enabled = false,
            Font = new Font("Segoe UI", 9, FontStyle.Bold)
        };
        btnEndControl.Click += BtnEndControl_Click;

        pnlScreenTop.Controls.AddRange(new Control[] { lblStatus, lblFps, lblResolution, btnEndControl });

        // Screen control chiếm phần còn lại
        screenControl = new RemoteScreenControl { Dock = DockStyle.Fill };

        // Gợi ý khi chưa có phiên
        lblHint = new Label
        {
            Text = "Chọn Host và nhấn \"Yêu cầu điều khiển\" để bắt đầu",
            ForeColor = Color.Gray,
            Font = new Font("Segoe UI", 12),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Name = "lblHint"
        };

        pnlScreen.Controls.Add(screenControl);
        pnlScreen.Controls.Add(lblHint);
        pnlScreen.Controls.Add(pnlScreenTop);

        splitMain.Panel2.Controls.Add(pnlScreen);

        Controls.Add(splitMain);

        splitMain.Panel1MinSize = 260;
        splitMain.Panel2MinSize = 400;
        splitMain.SplitterDistance = 300;

        // FPS timer
        var fpsTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        fpsTimer.Tick += (_, _) => UpdateFps();
        fpsTimer.Start();
    }

    private void BuildConnectionTab(TabPage tab)
    {
        tab.Padding = new Padding(8);
        var pnl = new Panel { Dock = DockStyle.Fill, AutoScroll = true };

        int y = 8;
        int lw = 100, tw = 160;

        // ── Server URL ────────────────────────────────────────────────────────
        pnl.Controls.Add(MakeLabel("Server URL:", 8, y));
        txtServerUrl = new TextBox { Bounds = new Rectangle(lw, y, tw, 24) };
        pnl.Controls.Add(txtServerUrl);
        y += 32;

        btnConnect = new Button
        {
            Text = "🔌 Kết nối",
            Bounds = new Rectangle(8, y, 120, 32),
            BackColor = Color.FromArgb(0, 120, 212),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnConnect.Click += BtnConnect_Click;
        pnl.Controls.Add(btnConnect);
        y += 44;

        // ── Divider ───────────────────────────────────────────────────────────
        pnl.Controls.Add(MakeDivider(y)); y += 16;

        // ── Viewer info ───────────────────────────────────────────────────────
        pnl.Controls.Add(MakeLabel("Viewer ID:", 8, y));
        txtViewerId = new TextBox { Bounds = new Rectangle(lw, y, tw, 24) };
        pnl.Controls.Add(txtViewerId);
        y += 32;

        pnl.Controls.Add(MakeLabel("Tên Viewer:", 8, y));
        txtViewerName = new TextBox { Bounds = new Rectangle(lw, y, tw, 24) };
        pnl.Controls.Add(txtViewerName);
        y += 32;

        pnl.Controls.Add(MakeLabel("User ID:", 8, y));
        txtUserId = new TextBox { Bounds = new Rectangle(lw, y, tw, 24) };
        pnl.Controls.Add(txtUserId);
        y += 32;

        btnRegister = new Button
        {
            Text = "📋 Đăng ký Viewer",
            Bounds = new Rectangle(8, y, 160, 32),
            BackColor = Color.FromArgb(16, 124, 16),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Enabled = false
        };
        btnRegister.Click += BtnRegister_Click;
        pnl.Controls.Add(btnRegister);
        y += 44;

        // ── Divider ───────────────────────────────────────────────────────────
        pnl.Controls.Add(MakeDivider(y)); y += 16;

        // ── Chọn Host ─────────────────────────────────────────────────────────
        pnl.Controls.Add(MakeLabel("Danh sách Host:", 8, y, bold: true));
        y += 22;

        btnRefreshHosts = new Button
        {
            Text = "🔄 Làm mới",
            Bounds = new Rectangle(8, y, 110, 28),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(60, 60, 60),
            ForeColor = Color.White,
            Enabled = false
        };
        btnRefreshHosts.Click += BtnRefreshHosts_Click;
        pnl.Controls.Add(btnRefreshHosts);
        y += 36;

        cmbHosts = new ComboBox
        {
            Bounds = new Rectangle(8, y, tw + lw - 8, 24),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        pnl.Controls.Add(cmbHosts);
        y += 32;

        btnRequestControl = new Button
        {
            Text = "🖥 Yêu cầu điều khiển",
            Bounds = new Rectangle(8, y, 200, 36),
            BackColor = Color.FromArgb(100, 60, 180),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Enabled = false
        };
        btnRequestControl.Click += BtnRequestControl_Click;
        pnl.Controls.Add(btnRequestControl);

        tab.Controls.Add(pnl);
    }

    private void BuildLogTab(TabPage tab)
    {
        tab.Padding = new Padding(4);

        rtbLog = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = Color.FromArgb(15, 15, 15),
            ForeColor = Color.LightGreen,
            Font = new Font("Consolas", 8f),
            ScrollBars = RichTextBoxScrollBars.Vertical
        };

        var btnClear = new Button
        {
            Text = "Xoá log",
            Dock = DockStyle.Bottom,
            Height = 26
        };
        btnClear.Click += (_, _) => rtbLog.Clear();

        tab.Controls.Add(rtbLog);
        tab.Controls.Add(btnClear);
    }

    // ─── Helper UI builders ───────────────────────────────────────────────────

    private static Label MakeLabel(string text, int x, int y, bool bold = false) => new Label
    {
        Text = text,
        Bounds = new Rectangle(x, y + 3, 110, 18),
        Font = bold ? new Font("Segoe UI", 9, FontStyle.Bold) : null
    };

    private static Panel MakeDivider(int y) => new Panel
    {
        Bounds = new Rectangle(0, y, 280, 1),
        BackColor = Color.FromArgb(80, 80, 80)
    };

    // ─── Logging ──────────────────────────────────────────────────────────────

    private void SetupLogging()
    {
        LoggingHelper.OnLog = (msg, level) =>
        {
            if (rtbLog == null || rtbLog.IsDisposed) return;
            if (rtbLog.InvokeRequired)
                rtbLog.BeginInvoke(() => AppendLog(msg, level));
            else
                AppendLog(msg, level);
        };
    }

    private void AppendLog(string msg, Helpers.LogLevel level)
    {
        var color = level switch
        {
            Helpers.LogLevel.Error => Color.OrangeRed,
            Helpers.LogLevel.Warning => Color.Yellow,
            Helpers.LogLevel.Debug => Color.DimGray,
            _ => Color.LightGreen
        };
        rtbLog.SelectionColor = color;
        rtbLog.AppendText(msg + Environment.NewLine);
        rtbLog.ScrollToCaret();

        if (rtbLog.Lines.Length > 2000)
            rtbLog.Lines = rtbLog.Lines.TakeLast(1500).ToArray();
    }

    // ─── SignalR events ───────────────────────────────────────────────────────

    private void SetupSignalREvents()
    {
        _signalR.OnConnectionStatusChanged += status =>
            SafeInvoke(() => UpdateStatusLabel(status));

        _signalR.OnRegisterSuccess += viewerId =>
            SafeInvoke(() =>
            {
                UpdateStatusLabel("Registered");
                UpdateUI();
            });

        _signalR.OnRegisterFailed += err =>
            SafeInvoke(() =>
                MessageBox.Show($"Đăng ký Viewer thất bại:\n{err}", "Lỗi",
                    MessageBoxButtons.OK, MessageBoxIcon.Error));

        _signalR.OnControlRequestSent += sessionId =>
            SafeInvoke(() =>
            {
                _currentSession = new ActiveSession { SessionId = sessionId };
                UpdateStatusLabel("Waiting");
                LoggingHelper.Info($"Đang chờ Host phản hồi – session: {sessionId}");
            });

        _signalR.OnControlRequestFailed += err =>
            SafeInvoke(() =>
            {
                MessageBox.Show($"Yêu cầu điều khiển thất bại:\n{err}", "Lỗi",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                _currentSession = null;
                UpdateUI();
            });

        _signalR.OnControlAccepted += (sessionId, hostId, viewerId) =>
            SafeInvoke(() =>
            {
                if (_currentSession != null)
                {
                    _currentSession.SessionId = sessionId;
                    _currentSession.HostId = hostId;
                    _currentSession.ViewerId = viewerId;
                    _currentSession.Status = "Accepted";
                    _currentSession.AcceptedAt = DateTime.Now;
                }

                // Kích hoạt screen control nhận input
                screenControl.Initialize(_signalR, sessionId);
                screenControl.Focus();

                // Ẩn hint label
                HideHint();

                UpdateStatusLabel("In Session");
                UpdateUI();
                LoggingHelper.Info($"Host đồng ý! Đang nhận màn hình – session: {sessionId}");
            });

        _signalR.OnControlRejected += (sessionId, reason) =>
            SafeInvoke(() =>
            {
                _currentSession = null;
                screenControl.Detach();
                UpdateStatusLabel("Registered");
                UpdateUI();
                ShowHint();
                MessageBox.Show(
                    $"Host đã từ chối yêu cầu điều khiển.\nLý do: {reason ?? "(không có)"}",
                    "Từ chối", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            });

        _signalR.OnControlEnded += sessionId =>
            SafeInvoke(() =>
            {
                if (_currentSession?.SessionId != sessionId) return;
                EndSessionUi();
                LoggingHelper.Info($"Phiên điều khiển đã kết thúc: {sessionId}");
            });

        _signalR.OnScreenFrameReceived += frame =>
            HandleScreenFrame(frame);
    }

    // ─── Frame handling ───────────────────────────────────────────────────────

    private void HandleScreenFrame(DTOs.ScreenFrameDto frame)
    {
        if (string.IsNullOrEmpty(frame.ImageBase64)) return;

        try
        {
            var bytes = Convert.FromBase64String(frame.ImageBase64);
            using var ms = new MemoryStream(bytes);
            var img = System.Drawing.Image.FromStream(ms);

            _frameCount++;
            screenControl.UpdateFrame(img, frame.ScreenWidth, frame.ScreenHeight,
                frame.MouseX, frame.MouseY);

            // Cập nhật resolution label (thread-safe)
            SafeInvoke(() =>
            {
                lblResolution.Text = $"{frame.ScreenWidth}×{frame.ScreenHeight}  [{frame.FrameWidth}×{frame.FrameHeight}]";
            });
        }
        catch (Exception ex)
        {
            LoggingHelper.Error($"Render frame lỗi: {ex.Message}");
        }
    }

    private void UpdateFps()
    {
        var now = DateTime.Now;
        var elapsed = (now - _lastFpsCalc).TotalSeconds;
        if (elapsed >= 1.0)
        {
            var fps = (_frameCount - _lastFrameCount) / elapsed;
            lblFps.Text = $"FPS: {fps:F0}";
            _lastFrameCount = _frameCount;
            _lastFpsCalc = now;
        }
    }

    // ─── Button handlers ──────────────────────────────────────────────────────

    private async void BtnConnect_Click(object? sender, EventArgs e)
    {
        btnConnect.Enabled = false;
        try
        {
            if (_signalR.IsConnected)
            {
                await _signalR.DisconnectAsync();
                btnConnect.Text = "🔌 Kết nối";
            }
            else
            {
                _settings.ServerUrl = txtServerUrl.Text.Trim();
                btnConnect.Text = "⏳ Đang kết nối...";
                await _signalR.ConnectAsync();
                btnConnect.Text = "⏏ Ngắt kết nối";
            }
        }
        catch (Exception ex)
        {
            LoggingHelper.Error($"Kết nối thất bại: {ex.Message}");
            MessageBox.Show($"Không thể kết nối:\n{ex.Message}", "Lỗi",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            btnConnect.Text = "🔌 Kết nối";
        }
        finally
        {
            btnConnect.Enabled = true;
            UpdateUI();
        }
    }

    private async void BtnRegister_Click(object? sender, EventArgs e)
    {
        btnRegister.Enabled = false;
        try
        {
            var viewerId = txtViewerId.Text.Trim();
            var viewerName = txtViewerName.Text.Trim();
            var userId = txtUserId.Text.Trim();

            if (string.IsNullOrWhiteSpace(viewerId))
            {
                MessageBox.Show("Viewer ID không được trống!", "Cảnh báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(userId))
            {
                MessageBox.Show("User ID không được trống! (server bắt buộc)", "Cảnh báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            await _signalR.RegisterViewer(new ViewerRegisterDto
            {
                ViewerId = viewerId,
                ViewerName = viewerName,
                UserId = userId
            });
        }
        catch (Exception ex)
        {
            LoggingHelper.Error($"Đăng ký thất bại: {ex.Message}");
            MessageBox.Show($"Đăng ký thất bại:\n{ex.Message}", "Lỗi",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            btnRegister.Enabled = _signalR.IsConnected;
        }
    }

    private async void BtnRefreshHosts_Click(object? sender, EventArgs e)
    {
        btnRefreshHosts.Enabled = false;
        try
        {
            var hosts = await _signalR.GetOnlineHostsAsync();
            cmbHosts.Items.Clear();
            foreach (var h in hosts)
                cmbHosts.Items.Add(h);

            if (cmbHosts.Items.Count > 0)
                cmbHosts.SelectedIndex = 0;

            LoggingHelper.Info($"Tìm thấy {hosts.Count} Host đang online.");
        }
        catch (Exception ex)
        {
            LoggingHelper.Error($"Lấy danh sách Host thất bại: {ex.Message}");
        }
        finally
        {
            btnRefreshHosts.Enabled = _signalR.IsConnected;
        }
    }

    private async void BtnRequestControl_Click(object? sender, EventArgs e)
    {
        if (cmbHosts.SelectedItem is not HostInfo host)
        {
            MessageBox.Show("Vui lòng chọn Host!", "Cảnh báo",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        btnRequestControl.Enabled = false;
        try
        {
            var dto = new ControlRequestDto
            {
                HostId = host.Id,
                ViewerId = txtViewerId.Text.Trim(),
                ViewerName = txtViewerName.Text.Trim(),
                UserId = txtUserId.Text.Trim()
            };

            if (string.IsNullOrWhiteSpace(dto.UserId))
            {
                MessageBox.Show("User ID không được trống!", "Cảnh báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            await _signalR.RequestControl(dto);
        }
        catch (Exception ex)
        {
            LoggingHelper.Error($"RequestControl thất bại: {ex.Message}");
            MessageBox.Show($"Gửi yêu cầu thất bại:\n{ex.Message}", "Lỗi",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            btnRequestControl.Enabled = true;
        }
    }

    private async void BtnEndControl_Click(object? sender, EventArgs e)
    {
        if (_currentSession == null) return;
        try
        {
            await _signalR.EndControl(_currentSession.SessionId);
            EndSessionUi();
        }
        catch (Exception ex)
        {
            LoggingHelper.Error($"EndControl lỗi: {ex.Message}");
        }
    }

    // ─── UI helpers ───────────────────────────────────────────────────────────

    private void UpdateStatusLabel(string status)
    {
        (lblStatus.Text, lblStatus.ForeColor) = status switch
        {
            "Connected" => ("● Đã kết nối", Color.DodgerBlue),
            "Registered" => ("● Đã đăng ký", Color.LimeGreen),
            "Waiting" => ("⏳ Đang chờ Host...", Color.Orange),
            "In Session" => ("● Đang điều khiển", Color.Cyan),
            "Reconnecting" => ("⟳ Đang reconnect...", Color.Yellow),
            "Disconnected" => ("● Offline", Color.Gray),
            _ => ("● " + status, Color.Gray)
        };
    }

    private void UpdateUI()
    {
        bool connected = _signalR.IsConnected;
        bool hasSession = _currentSession != null;

        btnConnect.Text = connected ? "⏏ Ngắt kết nối" : "🔌 Kết nối";
        btnRegister.Enabled = connected && !hasSession;
        btnRefreshHosts.Enabled = connected && !hasSession;
        btnRequestControl.Enabled = connected && !hasSession && cmbHosts.Items.Count > 0;
        btnEndControl.Enabled = hasSession;

        txtViewerId.ReadOnly = connected;
        txtViewerName.ReadOnly = connected;
        txtUserId.ReadOnly = connected;
        txtServerUrl.ReadOnly = connected;
    }

    private void EndSessionUi()
    {
        _currentSession = null;
        screenControl.Detach();
        screenControl.Image = null;
        ShowHint();
        UpdateStatusLabel("Registered");
        UpdateUI();
        lblFps.Text = "FPS: –";
        lblResolution.Text = "–";
    }

    private void HideHint() => lblHint.Visible = false;
    private void ShowHint() => lblHint.Visible = true;

    private void SafeInvoke(Action action)
    {
        if (IsDisposed) return;
        if (InvokeRequired) BeginInvoke(action);
        else action();
    }

    // ─── Form closing ─────────────────────────────────────────────────────────

    private async void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_isClosing) return;
        e.Cancel = true;
        _isClosing = true;

        if (_currentSession != null)
        {
            try { await _signalR.EndControl(_currentSession.SessionId); } catch { }
        }
        await _signalR.DisconnectAsync();
        Close();
    }
}
