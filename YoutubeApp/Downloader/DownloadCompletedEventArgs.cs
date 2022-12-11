namespace YoutubeApp.Downloader;

public class DownloadCompletedEventArgs
{
    public required int Id { get; set; }
    public required long BytesLoaded { get; set; }
}