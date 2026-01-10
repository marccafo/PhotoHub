namespace PhotoHub.Blazor.Shared.Models;

public class ScanProgressUpdate
{
    public string Message { get; set; } = string.Empty;
    public double Percentage { get; set; }
    public ScanStatistics? Statistics { get; set; }
    public bool IsCompleted { get; set; }
}
