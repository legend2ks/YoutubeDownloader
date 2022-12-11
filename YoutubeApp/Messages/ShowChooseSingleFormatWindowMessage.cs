using CommunityToolkit.Mvvm.Messaging.Messages;
using YoutubeApp.Models;

namespace YoutubeApp.Messages;

public class ShowChooseSingleFormatWindowMessage : AsyncRequestMessage<bool>
{
    public required Download Download { get; init; }
}