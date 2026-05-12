namespace RemoteViewerApp.DTOs;

/// <summary>
/// Gửi sự kiện chuột tới Host qua server — phải khớp chính xác với server DTO
/// QUAN TRỌNG: server dùng field "Action" (không phải "EventType")
/// Action values: "Move", "LeftClick", "RightClick", "DoubleClick", "Scroll"
/// Button: 0=Left, 1=Middle, 2=Right
/// </summary>
public class MouseEventDto
{
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// "Move" | "LeftClick" | "RightClick" | "DoubleClick" | "Scroll"
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Tọa độ X trên màn hình Host (pixel thật)</summary>
    public int X { get; set; }

    /// <summary>Tọa độ Y trên màn hình Host (pixel thật)</summary>
    public int Y { get; set; }

    /// <summary>Chiều rộng màn hình Host (dùng để scale tọa độ)</summary>
    public int ScreenWidth { get; set; }

    /// <summary>Chiều cao màn hình Host</summary>
    public int ScreenHeight { get; set; }

    /// <summary>0=Left, 1=Middle, 2=Right</summary>
    public int Button { get; set; }

    /// <summary>Giá trị cuộn (dương = lên, âm = xuống)</summary>
    public int Delta { get; set; }

    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}
