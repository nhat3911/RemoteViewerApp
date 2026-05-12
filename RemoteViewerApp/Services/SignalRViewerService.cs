using Microsoft.AspNetCore.SignalR.Client;
using RemoteViewerApp.DTOs;
using RemoteViewerApp.Helpers;
using RemoteViewerApp.Models;

namespace RemoteViewerApp.Services;

/// <summary>
/// Quản lý toàn bộ kết nối SignalR của Viewer với server.
/// Xử lý: đăng ký, ping, yêu cầu điều khiển, nhận frame màn hình, gửi input.
/// </summary>
public class SignalRViewerService : IAsyncDisposable
{
    private HubConnection? _connection;
    private readonly AppSettings _settings;
    private System.Threading.Timer? _pingTimer;
    private ViewerRegisterDto? _lastRegisterDto;

    // ─── Events ra ngoài (MainForm lắng nghe) ─────────────────────────────────
    public event Action<string>? OnConnectionStatusChanged;       // Connected / Disconnected / Reconnecting
    public event Action<ScreenFrameDto>? OnScreenFrameReceived;   // Nhận frame màn hình
    public event Action<string, string, string>? OnControlAccepted;   // sessionId, hostId, viewerId
    public event Action<string, string?>? OnControlRejected;      // sessionId, reason
    public event Action<string>? OnControlEnded;                  // sessionId
    public event Action<string>? OnControlRequestSent;            // sessionId (xác nhận server đã nhận)
    public event Action<string>? OnControlRequestFailed;          // error message
    public event Action<string>? OnRegisterSuccess;               // viewerId
    public event Action<string>? OnRegisterFailed;                // error

    public string? ViewerId { get; private set; }

    public bool IsConnected =>
        _connection?.State == HubConnectionState.Connected;

    public SignalRViewerService(AppSettings settings)
    {
        _settings = settings;
    }

    // ─── Kết nối ──────────────────────────────────────────────────────────────

    public async Task ConnectAsync()
    {
        if (_connection != null)
            await DisconnectAsync();

        LoggingHelper.Info($"Đang kết nối tới {_settings.FullHubUrl} ...");

        _connection = new HubConnectionBuilder()
            .WithUrl(_settings.FullHubUrl)
            .WithAutomaticReconnect(new[]
            {
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(30)
            })
            .Build();

        // ── Lifecycle events ──────────────────────────────────────────────────
        _connection.Reconnecting += ex =>
        {
            LoggingHelper.Warning($"SignalR đang reconnect: {ex?.Message}");
            OnConnectionStatusChanged?.Invoke("Reconnecting");
            return Task.CompletedTask;
        };

        _connection.Reconnected += async connId =>
        {
            LoggingHelper.Info($"SignalR reconnected – connId: {connId}");
            OnConnectionStatusChanged?.Invoke("Connected");

            // Tự động re-register viewer sau reconnect
            if (_lastRegisterDto != null)
            {
                try
                {
                    await _connection!.InvokeAsync("RegisterViewer", _lastRegisterDto);
                    LoggingHelper.Info($"Auto re-register viewer thành công – ID: {_lastRegisterDto.ViewerId}");
                }
                catch (Exception ex)
                {
                    LoggingHelper.Error($"Auto re-register thất bại: {ex.Message}");
                }
            }
        };

        _connection.Closed += ex =>
        {
            LoggingHelper.Warning($"SignalR đóng kết nối: {ex?.Message}");
            OnConnectionStatusChanged?.Invoke("Disconnected");
            StopPing();
            return Task.CompletedTask;
        };

        // Đăng ký nhận sự kiện từ server
        RegisterServerEvents();

        await _connection.StartAsync();
        LoggingHelper.Info("Kết nối SignalR thành công!");
        OnConnectionStatusChanged?.Invoke("Connected");

        StartPing();
    }

    public async Task DisconnectAsync()
    {
        StopPing();

        if (_connection != null)
        {
            await _connection.StopAsync();
            await _connection.DisposeAsync();
            _connection = null;
        }

        LoggingHelper.Info("Đã ngắt kết nối SignalR.");
        OnConnectionStatusChanged?.Invoke("Disconnected");
    }

    // ─── Nhận sự kiện từ server ───────────────────────────────────────────────

    private void RegisterServerEvents()
    {
        if (_connection == null) return;

        // ── Đăng ký thành công / thất bại ────────────────────────────────────
        _connection.On<string>("RegisterViewerSuccess", viewerId =>
        {
            LoggingHelper.Info($"[RegisterViewer] Thành công – ID: {viewerId}");
            OnRegisterSuccess?.Invoke(viewerId);
        });

        _connection.On<string>("RegisterViewerFailed", error =>
        {
            LoggingHelper.Error($"[RegisterViewer] Thất bại: {error}");
            OnRegisterFailed?.Invoke(error);
        });

        // ── Xác nhận yêu cầu điều khiển đã gửi ──────────────────────────────
        // Server gửi object: { SessionId, HostId, ViewerId, Status, CreatedAt }
        _connection.On<Newtonsoft.Json.Linq.JObject>("ControlRequestSent", obj =>
        {
            var sessionId = obj["sessionId"]?.ToString() ?? obj["SessionId"]?.ToString() ?? "";
            LoggingHelper.Info($"[ControlRequest] Server xác nhận – sessionId: {sessionId}");
            OnControlRequestSent?.Invoke(sessionId);
        });

        _connection.On<string>("ControlRequestFailed", error =>
        {
            LoggingHelper.Error($"[ControlRequest] Thất bại: {error}");
            OnControlRequestFailed?.Invoke(error);
        });

        // ── Host đồng ý điều khiển ────────────────────────────────────────────
        // Server gửi object: { SessionId, HostId, ViewerId, Status, AcceptedAt }
        _connection.On<Newtonsoft.Json.Linq.JObject>("ControlAccepted", obj =>
        {
            var sessionId = obj["sessionId"]?.ToString() ?? obj["SessionId"]?.ToString() ?? "";
            var hostId    = obj["hostId"]?.ToString()    ?? obj["HostId"]?.ToString()    ?? "";
            var viewerId  = obj["viewerId"]?.ToString()  ?? obj["ViewerId"]?.ToString()  ?? "";
            LoggingHelper.Info($"[ControlAccepted] Host đồng ý – session: {sessionId}");
            OnControlAccepted?.Invoke(sessionId, hostId, viewerId);
        });

        // ── Host từ chối điều khiển ───────────────────────────────────────────
        // Server gửi object: { SessionId, HostId, ViewerId, Status, Reason, RejectedAt }
        _connection.On<Newtonsoft.Json.Linq.JObject>("ControlRejected", obj =>
        {
            var sessionId = obj["sessionId"]?.ToString() ?? obj["SessionId"]?.ToString() ?? "";
            var reason    = obj["reason"]?.ToString()    ?? obj["Reason"]?.ToString();
            LoggingHelper.Info($"[ControlRejected] Host từ chối – session: {sessionId}, lý do: {reason}");
            OnControlRejected?.Invoke(sessionId, reason);
        });

        // ── Phiên kết thúc (từ Host hoặc từ viewer khác) ─────────────────────
        _connection.On<Newtonsoft.Json.Linq.JObject>("ControlEnded", obj =>
        {
            var sessionId = obj["sessionId"]?.ToString() ?? obj["SessionId"]?.ToString() ?? "";
            LoggingHelper.Info($"[ControlEnded] Phiên kết thúc – session: {sessionId}");
            OnControlEnded?.Invoke(sessionId);
        });

        // ── Nhận frame màn hình từ Host ───────────────────────────────────────
        // Server relay: { SessionId, HostId, ViewerId, ImageBase64,
        //                 ScreenWidth, ScreenHeight, FrameWidth, FrameHeight,
        //                 MouseX, MouseY, SentAt, ReceivedAt }
        _connection.On<Newtonsoft.Json.Linq.JObject>("ReceiveScreenFrame", obj =>
        {
            try
            {
                var frame = new ScreenFrameDto
                {
                    SessionId   = obj["sessionId"]?.ToString()   ?? obj["SessionId"]?.ToString()   ?? "",
                    HostId      = obj["hostId"]?.ToString()      ?? obj["HostId"]?.ToString()      ?? "",
                    ViewerId    = obj["viewerId"]?.ToString()    ?? obj["ViewerId"]?.ToString()    ?? "",
                    ImageBase64 = obj["imageBase64"]?.ToString() ?? obj["ImageBase64"]?.ToString() ?? "",
                    ScreenWidth = (int)(obj["screenWidth"]  ?? obj["ScreenWidth"]  ?? 1920),
                    ScreenHeight= (int)(obj["screenHeight"] ?? obj["ScreenHeight"] ?? 1080),
                    FrameWidth  = (int)(obj["frameWidth"]   ?? obj["FrameWidth"]   ?? 1280),
                    FrameHeight = (int)(obj["frameHeight"]  ?? obj["FrameHeight"]  ?? 720),
                    MouseX      = (int)(obj["mouseX"]       ?? obj["MouseX"]       ?? 0),
                    MouseY      = (int)(obj["mouseY"]       ?? obj["MouseY"]       ?? 0),
                };
                OnScreenFrameReceived?.Invoke(frame);
            }
            catch (Exception ex)
            {
                LoggingHelper.Error($"Parse ScreenFrame lỗi: {ex.Message}");
            }
        });

        // ── Các lỗi gửi event ─────────────────────────────────────────────────
        _connection.On<string>("SendMouseEventFailed", err =>
            LoggingHelper.Warning($"SendMouseEvent thất bại: {err}"));

        _connection.On<string>("SendKeyboardEventFailed", err =>
            LoggingHelper.Warning($"SendKeyboardEvent thất bại: {err}"));
    }

    // ─── Gọi method lên server ────────────────────────────────────────────────

    /// <summary>Đăng ký viewer với server</summary>
    public async Task RegisterViewer(ViewerRegisterDto dto)
    {
        EnsureConnected();
        ViewerId = dto.ViewerId;
        _lastRegisterDto = dto;
        await _connection!.InvokeAsync("RegisterViewer", dto);
        LoggingHelper.Info($"Đã gửi RegisterViewer – ID: {dto.ViewerId}");
    }

    /// <summary>Ping định kỳ để server biết viewer còn online</summary>
    public async Task PingViewer()
    {
        if (!IsConnected || ViewerId == null) return;
        try
        {
            await _connection!.InvokeAsync("PingViewer", ViewerId);
            LoggingHelper.Debug("Ping viewer OK");
        }
        catch (Exception ex)
        {
            LoggingHelper.Warning($"PingViewer lỗi: {ex.Message}");
        }
    }

    /// <summary>Gửi yêu cầu điều khiển Host</summary>
    public async Task RequestControl(ControlRequestDto dto)
    {
        EnsureConnected();
        await _connection!.InvokeAsync("RequestControl", dto);
        LoggingHelper.Info($"Đã gửi RequestControl → Host: {dto.HostId}");
    }

    /// <summary>Kết thúc phiên điều khiển</summary>
    public async Task EndControl(string sessionId)
    {
        EnsureConnected();
        await _connection!.InvokeAsync("EndControl", sessionId);
        LoggingHelper.Info($"Đã gửi EndControl – session: {sessionId}");
    }

    /// <summary>Gửi sự kiện chuột tới Host</summary>
    public async Task SendMouseEvent(MouseEventDto dto)
    {
        if (!IsConnected) return;
        try
        {
            await _connection!.SendAsync("SendMouseEvent", dto);
        }
        catch (Exception ex)
        {
            LoggingHelper.Error($"SendMouseEvent lỗi: {ex.Message}");
        }
    }

    /// <summary>Gửi sự kiện bàn phím tới Host</summary>
    public async Task SendKeyboardEvent(KeyboardEventDto dto)
    {
        if (!IsConnected) return;
        try
        {
            await _connection!.SendAsync("SendKeyboardEvent", dto);
        }
        catch (Exception ex)
        {
            LoggingHelper.Error($"SendKeyboardEvent lỗi: {ex.Message}");
        }
    }

    // ─── HTTP REST helper ──────────────────────────────────────────────────────

    /// <summary>Lấy danh sách Host đang online qua REST API</summary>
    public async Task<List<Models.HostInfo>> GetOnlineHostsAsync()
    {
        using var http = new HttpClient();
        var url = $"{_settings.ServerUrl.TrimEnd('/')}/api/Connection/hosts";
        try
        {
            var json = await http.GetStringAsync(url);
            var list = Newtonsoft.Json.JsonConvert.DeserializeObject<List<Models.HostInfo>>(json);
            return list ?? new List<Models.HostInfo>();
        }
        catch (Exception ex)
        {
            LoggingHelper.Error($"GetOnlineHosts thất bại: {ex.Message}");
            return new List<Models.HostInfo>();
        }
    }

    // ─── Ping timer ───────────────────────────────────────────────────────────

    private void StartPing()
    {
        var interval = TimeSpan.FromSeconds(_settings.PingIntervalSeconds);
        _pingTimer = new System.Threading.Timer(
            async _ => await PingViewer(),
            null, interval, interval);
        LoggingHelper.Debug($"Ping timer bắt đầu – mỗi {_settings.PingIntervalSeconds}s");
    }

    private void StopPing()
    {
        _pingTimer?.Dispose();
        _pingTimer = null;
    }

    // ─── Helper ───────────────────────────────────────────────────────────────

    private void EnsureConnected()
    {
        if (!IsConnected)
            throw new InvalidOperationException("Chưa kết nối tới server SignalR!");
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }
}
