namespace RemoteViewerApp.DTOs;

/// <summary>
/// Frame màn hình nhận từ server relay (Host → Server → Viewer)
/// Server gửi thêm SessionId, HostId, ViewerId, ReceivedAt so với DTO gốc
/// </summary>
public class ScreenFrameDto
{
    public string SessionId { get; set; } = string.Empty;
    public string HostId { get; set; } = string.Empty;
    public string ViewerId { get; set; } = string.Empty;

    /// <summary>Ảnh JPEG đã nén, encode Base64</summary>
    public string ImageBase64 { get; set; } = string.Empty;

    public int ScreenWidth { get; set; }
    public int ScreenHeight { get; set; }
    public int FrameWidth { get; set; }
    public int FrameHeight { get; set; }

    /// <summary>Vị trí chuột trên màn hình Host (pixel)</summary>
    public int MouseX { get; set; }
    public int MouseY { get; set; }

    public DateTime SentAt { get; set; }
    public DateTime ReceivedAt { get; set; }
}
