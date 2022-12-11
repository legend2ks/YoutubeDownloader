using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Messaging.Messages;
using YoutubeApp.Models;
using YoutubeApp.ViewModels;

namespace YoutubeApp.Messages;

public class ShowAddChannelWindowMessage : AsyncRequestMessage<AddChannelWindowResult?>
{
    public required ObservableCollection<ChannelCategory> ChannelCategories { get; init; }
}