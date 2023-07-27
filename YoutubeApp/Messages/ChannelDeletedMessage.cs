using YoutubeApp.Models;

namespace YoutubeApp.Messages;

public class ChannelDeletedMessage
{
    public required Channel Channel { get; init; }
}