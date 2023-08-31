using CommunityToolkit.Mvvm.Messaging.Messages;
using YoutubeApp.Models;

namespace YoutubeApp.Messages;

public class ShowMoveChannelWindowMessage : AsyncRequestMessage<bool>
{
    public required Channel Channel { get; set; }
    public required string DestPath { get; set; }
}