// ReSharper disable InconsistentNaming

namespace YoutubeApp.Downloader;

public class AriaTellStatusResponse
{
    public required string errorCode { get; set; }
    public required string errorMessage { get; set; }
    public required long completedLength { get; set; }
}