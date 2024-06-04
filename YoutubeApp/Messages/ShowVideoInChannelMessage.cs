using YoutubeApp.Models;

namespace YoutubeApp.Messages;

public class ShowVideoInChannelMessage(Channel channel, string videoId)
{
    public Channel Channel { get; } = channel;
    public string VideoId { get; } = videoId;
}