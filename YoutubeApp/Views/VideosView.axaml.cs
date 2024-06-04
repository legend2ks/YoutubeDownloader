using Avalonia.Controls;
using CommunityToolkit.Mvvm.Messaging;
using YoutubeApp.Enums;
using YoutubeApp.Messages;
using YoutubeApp.ViewModels;

namespace YoutubeApp.Views;

public partial class VideosView : UserControl, IRecipient<ShowVideoInChannelMessage>
{
    public VideosView()
    {
        InitializeComponent();
        WeakReferenceMessenger.Default.RegisterAll(this, (int)MessengerChannel.VideosView);
    }

    public void Receive(ShowVideoInChannelMessage message)
    {
        var vm = (ChannelsViewModel)DataContext!;
        var videoIndex = vm.Videos.FindIndex(v => v.VideoId == message.VideoId);
        if (videoIndex < 0)
            return;
        var el = Repeater.GetOrCreateElement(videoIndex);
        el.UpdateLayout();
        el.BringIntoView();
    }
}