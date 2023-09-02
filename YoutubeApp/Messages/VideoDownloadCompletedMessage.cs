using CommunityToolkit.Mvvm.Messaging.Messages;
using YoutubeApp.Models;

namespace YoutubeApp.Messages;

public class VideoDownloadCompletedMessage : ValueChangedMessage<Download>
{
    public VideoDownloadCompletedMessage(Download value) : base(value)
    {
    }
}