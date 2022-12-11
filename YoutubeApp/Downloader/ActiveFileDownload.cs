namespace YoutubeApp.Downloader;

internal class ActiveFileDownload
{
    public required DownloadPackage Package { get; set; }
    public required string FileId { get; set; }
    public required string Url { get; set; }
    public required string Filename { get; set; }
}