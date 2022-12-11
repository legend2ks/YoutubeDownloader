using CommunityToolkit.Mvvm.Messaging.Messages;

namespace YoutubeApp.Messages;

public class ShowLogWindowMessage : AsyncRequestMessage<bool>
{
    public required string Title { get; init; }
    public required string[] Items { get; init; }
}