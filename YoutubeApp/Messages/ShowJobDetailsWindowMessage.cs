using CommunityToolkit.Mvvm.Messaging.Messages;
using YoutubeApp.Models;

namespace YoutubeApp.Messages;

public class ShowJobDetailsWindowMessage : AsyncRequestMessage<bool>
{
    public required GrabberJob Job { get; init; }
}