using YoutubeApp.Models;

namespace YoutubeApp.Messages;

public class VideoDownloadCompletedMessage
{
    public required Download DownloadItem { get; init; }
}