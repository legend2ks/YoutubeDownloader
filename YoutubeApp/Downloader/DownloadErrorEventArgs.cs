namespace YoutubeApp.Downloader;

public class DownloadErrorEventArgs
{
    public required int Id { get; set; }
    public required string Error { get; set; }
}