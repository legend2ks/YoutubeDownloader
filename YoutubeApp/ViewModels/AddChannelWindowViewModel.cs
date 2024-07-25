using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DynamicData;
using MsBox.Avalonia.Enums;
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

    public string PrimaryButtonText => Success ? "Add" : "Continue";

    public bool LinkBoxIsEnabled => !Loading && !Success;

    public bool DetailsIsVisible => Success || Exists;

    private PlaylistInfo _playlistInfo;

    private CancellationTokenSource _cancellationTokenSource;

    [RelayCommand]
    private async Task AddButtonPressedAsync()
    {
        if (!Success)
        {
            Exists = false;
            Error = false;

            var trimmedLink = Link.Trim();
            if (trimmedLink.Length == 0) return;

            var handlePattern = @"^(?:https?:\/\/)?(?:www\.|m\.)?youtube\.com\/(@[-a-zA-Z0-9_\.]{3,})\/?";
            var idPattern = @"^(?:https?:\/\/)?(?:www\.|m\.)?youtube\.com\/channel\/(UC[-a-zA-Z0-9_\.]+)\/?";

            var handleMatch = Regex.Match(trimmedLink, handlePattern);
            var idMatch = Regex.Match(trimmedLink, idPattern);
            if (!handleMatch.Success && !idMatch.Success)
            {
                await _messenger.Send(new ShowMessageBoxMessage
                {
                    Title = "Error", Message = "Invalid channel link.", Icon = Icon.Error,
                    ButtonDefinitions = ButtonEnum.Ok
                }, (int)MessengerChannel.AddChannelWindow);
                return;
            }

            string channelId;

            if (idMatch.Success)
            {
                channelId = idMatch.Groups[1].Value;
            }
            else
            {
                // Get Channel ID
                var handle = handleMatch.Groups[1].Value;
                Loading = true;
                _cancellationTokenSource = new CancellationTokenSource();
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

                channelId = channelInfo.channel_id;
            }

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
                var dirInfo = Directory.CreateDirectory(SaveTo);
                SaveTo = Path.TrimEndingDirectorySeparator(dirInfo.FullName);
            }
            catch (Exception e)
            {
                await _messenger.Send(new ShowMessageBoxMessage
                {
                    Title = "Error", Message = e.Message, Icon = Icon.Error, ButtonDefinitions = ButtonEnum.Ok
                }, (int)MessengerChannel.AddChannelWindow);
                return;
            }

            var channels = ChannelCategories.SelectMany(cat => cat.Channels);
            foreach (var ch in channels)
            {
                if (!Utils.IsSamePath(SaveTo, ch.Path)) continue;
                await _messenger.Send(new ShowMessageBoxMessage
                {
                    Title = "Move", Message = $"The selected save path belongs to another channel:\n\"{ch.Title}\"",
                    Icon = Icon.Error, ButtonDefinitions = ButtonEnum.Ok
                }, (int)MessengerChannel.AddChannelWindow);
                return;
            }

            var updateDateTime = DateTime.UtcNow.ToString(CultureInfo.InvariantCulture);

            var channel
                = new Channel
                {
                    UniqueId = _playlistInfo.channel_id,
                    ListId = _playlistInfo.id,
                    Title = _playlistInfo.channel,
                    Path = SaveTo,
                    VideoCount = VideoCount,
                    IncompleteCount = VideoCount,
                    LastUpdate = updateDateTime,
                    LocalLastUpdate = DateTime.Parse(updateDateTime, CultureInfo.InvariantCulture).ToLocalTime()
                        .ToString(Settings.ChannelDateFormat),
                };

            var idx = ChannelCategories[0].Channels
                .BinarySearch(channel.Title,
                    (s, ch) => string.Compare(s, ch.Title, StringComparison.OrdinalIgnoreCase));
            if (idx < 0) idx = ~idx;
            _channelData.AddChannel(channel, _playlistInfo.entries, updateDateTime);
            ChannelCategories[0].Channels.Insert(idx, channel);

            _messenger.Send(new ChannelAddedMessage { Channel = channel });
            _messenger.Send(new CloseWindowMessage<AddChannelWindowResult>(new AddChannelWindowResult
                { Channel = channel, PlaylistInfo = _playlistInfo }), (int)MessengerChannel.AddChannelWindow);
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

    [RelayCommand]
    private void AddChannelTitle()
    {
        var savePath = Path.GetFullPath(SaveTo);
        savePath = Path.TrimEndingDirectorySeparator(savePath);
        var channelName = Youtube.SanitizeFilename(_playlistInfo.channel);
        SaveTo = !savePath.EndsWith(channelName) ? Path.Combine(savePath, channelName) : savePath;
    }
}

public class AddChannelWindowResult
{
    public required Channel Channel { get; init; }
    public required PlaylistInfo PlaylistInfo { get; init; }
}