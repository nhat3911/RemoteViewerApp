namespace RemoteViewerApp.Models;

/// <summary>
/// Cấu hình ứng dụng từ appsettings.json
/// </summary>
public class AppSettings
{
    public string ServerUrl { get; set; } = "http://localhost:5271";
    public string HubEndpoint { get; set; } = "/remoteHub";
    public int PingIntervalSeconds { get; set; } = 10;
    public int JpegQuality { get; set; } = 75;

    public string FullHubUrl => $"{ServerUrl.TrimEnd('/')}{HubEndpoint}";
}
