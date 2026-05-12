namespace RemoteViewerApp.DTOs;

/// <summary>
/// DTO phản hồi yêu cầu điều khiển (Accept/Reject)
/// </summary>
public class ControlResponseDto
{
    public string SessionId { get; set; } = string.Empty;
    public string HostId { get; set; } = string.Empty;
    public bool IsAccepted { get; set; }
    public string? RejectReason { get; set; }
}