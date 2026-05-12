namespace RemoteViewerApp.DTOs;

/// <summary>
/// Gửi sự kiện bàn phím tới Host qua server — phải khớp chính xác với server DTO
/// QUAN TRỌNG: server dùng Action/Key/Code/CtrlKey/ShiftKey/AltKey
/// (khác với HostApp dùng EventType/KeyCode/IsCtrl/IsShift/IsAlt)
/// </summary>
public class KeyboardEventDto
{
    public string SessionId { get; set; } = string.Empty;

    /// <summary>"KeyDown" | "KeyUp"</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Tên key (e.g. "A", "Enter", "F5", "ArrowLeft")</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Mã vật lý của phím (e.g. "KeyA", "Enter", "F5")</summary>
    public string Code { get; set; } = string.Empty;

    public bool CtrlKey { get; set; }
    public bool ShiftKey { get; set; }
    public bool AltKey { get; set; }

    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}
