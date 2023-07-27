using YoutubeApp.Models;

namespace YoutubeApp.Messages;

public class ChannelAddedMessage
{
    public required Channel Channel { get; init; }
}