namespace PhotoHub.Client.Shared.Models;

public class MetadataProgressUpdate
{
    public string Message { get; set; } = string.Empty;
    public double Percentage { get; set; }
    public MetadataJobStatistics? Statistics { get; set; }
    public bool IsCompleted { get; set; }
}

public class MetadataJobStatistics
{
    public int TotalAssets { get; set; }
    public int Processed { get; set; }
    public int Extracted { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
}
