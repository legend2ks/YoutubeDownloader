// ReSharper disable InconsistentNaming

namespace YoutubeApp.Downloader;

public class AriaTellActiveResponse
{
    public required string gid { get; set; }
    public required string completedLength { get; set; }
    public required string connections { get; set; }
    public required string downloadSpeed { get; set; }
}