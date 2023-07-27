using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MessageBox.Avalonia;
using YoutubeApp.Database;
using YoutubeApp.Enums;
using YoutubeApp.Media;
using YoutubeApp.Messages;
using YoutubeApp.Models;

namespace YoutubeApp.ViewModels;

public partial class AddChannelWindowViewModel : ObservableObject
{
    public AddChannelWindowViewModel(ChannelData channelData, IYoutubeCommunicator youtubeCommunicator,
        IMessenger messenger)
    {
        _channelData = channelData;
        _youtubeCommunicator = youtubeCommunicator;
        _messenger = messenger;
    }

    private readonly ChannelData _channelData;
    private readonly IYoutubeCommunicator _youtubeCommunicator;
    private readonly IMessenger _messenger;
    public required ObservableCollection<ChannelCategory> ChannelCategories { get; init; }

    [ObservableProperty] private string _link = "";

    [ObservableProperty] private string _saveTo = "";

    [ObservableProperty] private string _listId;

    [ObservableProperty] private string _title;

    [ObservableProperty] private int _videoCount;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(LinkBoxIsEnabled))]
    private bool _loading;

    [ObservableProperty] private bool _error;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(DetailsIsVisible))]
    private bool _exists;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LinkBoxIsEnabled))]
    [NotifyPropertyChangedFor(nameof(PrimaryButtonText))]
    [NotifyPropertyChangedFor(nameof(DetailsIsVisible))]
    private bool _success;

    public string PrimaryButtonText
    {
        get => Success ? "Add" : "Continue";
    }

    public bool LinkBoxIsEnabled
    {
        get => !Loading && !Success;
    }

    public bool DetailsIsVisible
    {
        get => Success || Exists;
    }

    private PlaylistInfo _playlistInfo;

    private CancellationTokenSource _cancellationTokenSource;

    [RelayCommand]
    private async Task AddButtonPressedAsync(Window window)
    {
        if (!Success)
        {
            Exists = false;
            Error = false;

            var trimmedLink = Link.Trim();
            if (trimmedLink.Length == 0) return;

            var handlePattern = @"^(?:https?:\/\/)?(?:www\.|m\.)?youtube\.com\/(@[-a-zA-Z0-9_\.]{3,})\/?";

            var handleMatch = Regex.Match(trimmedLink, handlePattern);
            if (!handleMatch.Success)
            {
                await MessageBoxManager.GetMessageBoxStandardWindow("Error", $"Invalid link.")
                    .ShowDialog(window);
                return;
            }

            var handle = handleMatch.Groups[1].Value;

            // Get Playlist ID
            Loading = true;
            _cancellationTokenSource = new();
            ChannelInfo channelInfo;
            try
            {
                channelInfo =
                    await _youtubeCommunicator.GetChannelInfoAsync(handle, _cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception)
            {
                Error = true;
                Loading = false;
                return;
            }
            finally
            {
                _cancellationTokenSource.Dispose();
            }

            var channelId = channelInfo.channel_id;
            if (!channelId.StartsWith("UC", StringComparison.OrdinalIgnoreCase))
            {
                Error = true;
                Loading = false;
                return;
            }

            ListId = $"UU{channelId[2..]}";

            var channels = ChannelCategories.SelectMany(x => x.Channels);
            var ch = channels.FirstOrDefault(x => x.ListId == ListId);
            if (ch != null)
            {
                Title = ch.Title;
                VideoCount = ch.VideoCount;
                Exists = true;
                Loading = false;
                return;
            }

            Loading = true;

            _cancellationTokenSource = new CancellationTokenSource();
            try
            {
                _playlistInfo = await _youtubeCommunicator.GetPlaylistInfoAsync(ListId, _cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception)
            {
                Error = true;
                return;
            }
            finally
            {
                Loading = false;
                _cancellationTokenSource.Dispose();
            }

            Title = _playlistInfo.channel;
            VideoCount = _playlistInfo.entries.Length;
            SaveTo = Path.Combine(Settings.LastSavePath, Youtube.SanitizeFilename(_playlistInfo.channel));
            Success = true;
        }
        else
        {
            try
            {
                Directory.CreateDirectory(SaveTo);
            }
            catch (Exception)
            {
                return;
            }

            var channel
                = new Channel
                {
                    UniqueId = _playlistInfo.channel_id,
                    ListId = _playlistInfo.id,
                    Title = _playlistInfo.channel,
                    Path = SaveTo,
                    VideoCount = VideoCount,
                    IncompleteCount = VideoCount,
                    LastUpdate = DateTime.UtcNow.ToString(),
                };

            _channelData.AddChannel(channel, _playlistInfo.entries);
            ChannelCategories[0].Channels.Add(channel);
            _messenger.Send(new ChannelAddedMessage { Channel = channel });

            window.Close(new AddChannelWindowResult { Channel = channel, PlaylistInfo = _playlistInfo });
        }
    }

    [RelayCommand]
    private async Task BrowseButtonPressedAsync()
    {
        var selectedFolders = await _messenger.Send(new OpenFolderPickerMessage
        {
            Title = "Save To...",
            SuggestedStartLocation = SaveTo
        }, (int)MessengerChannel.AddChannelWindow);
        if (selectedFolders.Count == 0) return;

        SaveTo = selectedFolders[0]!.Path.LocalPath;
    }
}

public class AddChannelWindowResult
{
    public required Channel Channel { get; init; }
    public required PlaylistInfo PlaylistInfo { get; init; }
}