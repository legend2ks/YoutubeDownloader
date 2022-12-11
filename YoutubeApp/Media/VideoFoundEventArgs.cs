using YoutubeApp.Models;

namespace YoutubeApp.Media;

public class VideoFoundEventArgs
{
    public int JobId { get; set; }
    public Download? DownloadItem { get; set; }
}