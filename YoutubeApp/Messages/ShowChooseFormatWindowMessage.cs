using System.Collections.Generic;
using CommunityToolkit.Mvvm.Messaging.Messages;
using YoutubeApp.Models;

namespace YoutubeApp.Messages;

public class ShowChooseFormatWindowMessage : AsyncRequestMessage<bool>
{
    public required List<Download> SelectedItems { get; set; }
}