namespace RemoteViewerApp.DTOs;

/// <summary>
/// Gửi yêu cầu điều khiển Host — phải khớp chính xác với server DTO
/// NOTE: Server yêu cầu UserId bắt buộc (không được rỗng)
/// </summary>
public class ControlRequestDto
{
    public string HostId { get; set; } = string.Empty;
    public string ViewerId { get; set; } = string.Empty;
    public string ViewerName { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
}
