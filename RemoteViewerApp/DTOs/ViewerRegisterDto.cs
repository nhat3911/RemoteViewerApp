namespace RemoteViewerApp.DTOs;

/// <summary>
/// Đăng ký Viewer với server — phải khớp chính xác với server DTO
/// </summary>
public class ViewerRegisterDto
{
    public string ViewerId { get; set; } = string.Empty;
    public string ViewerName { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
}
