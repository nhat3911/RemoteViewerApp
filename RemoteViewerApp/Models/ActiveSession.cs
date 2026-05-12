namespace RemoteViewerApp.Models;

/// <summary>
/// Trạng thái phiên điều khiển hiện tại của Viewer
/// </summary>
public class ActiveSession
{
    public string SessionId { get; set; } = string.Empty;
    public string HostId { get; set; } = string.Empty;
    public string ViewerId { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? AcceptedAt { get; set; }
}

public class HostInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ConnectionId { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public DateTime LastSeenAt { get; set; }

    public override string ToString() => $"{Name}  [{Id}]";
}
