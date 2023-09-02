using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reactive;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MessageBox.Avalonia.Enums;
using Microsoft.Extensions.Logging;
using YoutubeApp.Database;
using YoutubeApp.Extensions;
using YoutubeApp.Media;
using YoutubeApp.Messages;
using YoutubeApp.Models;
using YoutubeApp.Views;

namespace YoutubeApp.ViewModels;

public partial class ChannelsViewModel : ViewModelBase, IRecipient<VideoDownloadCompletedMessage>
{
    private const string VideoIdPattern = @"^\[.*]\[.*]\[(.*)]";
    private readonly ChannelData _channelData;
    private readonly Grabber _grabber;

    private readonly ILogger<ChannelsViewModel> _logger;
    private readonly IMessenger _messenger;
    private readonly IYoutubeCommunicator _youtubeCommunicator;

    private List<Video> _allVideos;
    [ObservableProperty] private int _currentPage;

    private IDisposable _searchObservable;
    [ObservableProperty] private string _searchText = string.Empty;

    [ObservableProperty] private Channel? _selectedChannel;
    [ObservableProperty] private List<Video> _videos;

    [ObservableProperty] private Vector _videosScrollOffset;

    public ChannelsViewModel(ILogger<ChannelsViewModel> logger, Grabber grabber, ChannelData channelData,
        IYoutubeCommunicator youtubeCommunicator, IMessenger messenger)
    {
        _logger = logger;
        _grabber = grabber;
        _channelData = channelData;
        _youtubeCommunicator = youtubeCommunicator;
        _messenger = messenger;

        messenger.RegisterAll(this);

        var channels = _channelData.GetChannels();
        var categories = _channelData.GetChannelCategories().ToList();

        categories.Insert(0, new ChannelCategory { Id = 0, Title = "Uncategorized" });
        foreach (var pl in channels)
        {
            categories.First(x => x.Id == pl.CategoryId).Channels.Add(pl);
        }

        ChannelCategories = new ObservableCollection<ChannelCategory>(categories);
    }

    protected ChannelsViewModel()
    {
    }

    public ObservableCollection<ChannelCategory> ChannelCategories { get; set; }

    public ObservableCollection<Video> SelectedVideos { get; } = new();

    private readonly List<UpdateChannelJob> _updateQueue = new();
    private int _activeUpdateJobCount;
    [ObservableProperty] private int _totalUpdateJobCount;

    private void SearchVideos(EventPattern<PropertyChangedEventArgs> e)
    {
        Debug.WriteLine($"Search for -> {SearchText}");
        var query = SearchText.Trim();
        if (query == "")
        {
            Videos = _allVideos;
            return;
        }

        Videos = _allVideos.FindAll(x => x.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
    }

    [RelayCommand]
    private void ChannelPressed(Channel channel)
    {
        if (SelectedChannel is not null && SelectedChannel.Id != channel.Id)
        {
            // Reset scroll position
            VideosScrollOffset = new Vector();
        }

        _channelData.SetAddedVideoCount(channel.Id, 0);
        channel.AddedVideoCount = 0;

        SelectedChannel = channel;
        var videos = _channelData.GetVideos(channel.Id);
        _allVideos = videos.Reverse().ToList();

        // Check available files
        if (Directory.Exists(channel.Path))
        {
            var fileNames = new Dictionary<string, string>();
            var dirFiles = Directory.GetFiles(channel.Path).Select(Path.GetFileName);
            foreach (var filename in dirFiles)
            {
                var videoIdMatch = Regex.Match(filename, VideoIdPattern);
                if (!videoIdMatch.Success) continue;
                var videoId = videoIdMatch.Groups[1].Value;
                fileNames.TryAdd(videoId, filename);
            }

            foreach (var video in _allVideos)
            {
                video.Channel = channel;
                fileNames.TryGetValue(video.VideoId, out var filename);
                video.FileName = filename;
            }
        }

        Videos = _allVideos;

        _searchObservable = Observable.FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                x => PropertyChanged += x,
                x => PropertyChanged -= x)
            .Where(e => e.EventArgs.PropertyName == "SearchText")
            .Throttle(TimeSpan.FromMilliseconds(600))
            .Subscribe(SearchVideos);

        CurrentPage = 1;
    }

    [RelayCommand]
    private void BackToChannelsPressed()
    {
        SelectedVideos.Clear();
        _searchObservable.Dispose();
        SearchText = "";
        CurrentPage = 0;
    }

    [RelayCommand]
    private void ClearSearchText()
    {
        SearchText = "";
    }

    [RelayCommand]
    private void PlayPressed(Video video)
    {
        var filepath = Path.Combine(SelectedChannel!.Path, video.FileName!);
        if (!File.Exists(filepath))
        {
            video.FileName = null;
            return;
        }

        Process.Start("explorer", $"\"{filepath}\"");
    }

    [RelayCommand]
    private async Task AddChannelPressedAsync()
    {
        var result = await _messenger.Send(new ShowAddChannelWindowMessage
            { ChannelCategories = ChannelCategories });
        if (result is null) return;

        AddChannelToUpdateQueue(result.Channel, false, result.PlaylistInfo);
    }

    [RelayCommand]
    private void UpdateAll()
    {
        var channels = ChannelCategories.SelectMany(x => x.Channels);

        foreach (var channel in channels)
        {
            if (channel.Updating) continue;

            channel.Updating = true;
            channel.StatusText = "Updating...";
            TotalUpdateJobCount++;
            _updateQueue.Add(new UpdateChannelJob { Channel = channel });
        }

        ProcessUpdateQueue();
    }

    [RelayCommand]
    private void StopUpdateAll()
    {
        var channels = ChannelCategories.SelectMany(x => x.Channels);
        foreach (var channel in channels)
        {
            if (!channel.Updating) continue;

            if (channel.CancellationTokenSource is not null)
            {
                channel.CancellationTokenSource.Cancel();
                channel.CancellationTokenSource.Dispose();
                channel.CancellationTokenSource = null;
            }
            else
            {
                var jobIdx = _updateQueue.FindIndex(x => x.Channel == channel);
                _updateQueue.RemoveAt(jobIdx);
            }

            channel.Updating = false;
            channel.StatusText = string.Empty;
        }

        _activeUpdateJobCount = 0;
        TotalUpdateJobCount = 0;
    }

    [RelayCommand]
    private void UpdateChannelPressed(Channel channel)
    {
        if (channel.Updating)
        {
            if (channel.CancellationTokenSource is not null)
            {
                channel.CancellationTokenSource.Cancel();
                channel.CancellationTokenSource.Dispose();
                channel.CancellationTokenSource = null;
                _activeUpdateJobCount--;
                ProcessUpdateQueue();
            }
            else
            {
                var jobIdx = _updateQueue.FindIndex(x => x.Channel == channel);
                _updateQueue.RemoveAt(jobIdx);
            }

            TotalUpdateJobCount--;
            channel.Updating = false;
            channel.StatusText = string.Empty;
            return;
        }

        AddChannelToUpdateQueue(channel);
    }

    [RelayCommand]
    private void FullChannelUpdatePressed(Channel channel)
    {
        AddChannelToUpdateQueue(channel, true);
    }

    private void AddChannelToUpdateQueue(Channel channel, bool fullUpdate = false,
        PlaylistInfo? resultPlaylistInfo = null)
    {
        channel.Updating = true;
        channel.StatusText = "Updating...";
        TotalUpdateJobCount++;
        _updateQueue.Add(new UpdateChannelJob
            { Channel = channel, FullUpdate = fullUpdate, PlaylistInfo = resultPlaylistInfo });
        ProcessUpdateQueue();
    }

    private void ProcessUpdateQueue()
    {
        while (_activeUpdateJobCount < Settings.MaxConcurrentChannelUpdates && _updateQueue.Count > 0)
        {
            var updateJob = _updateQueue[0];
            _updateQueue.RemoveAt(0);
            _activeUpdateJobCount++;
            UpdateChannel(updateJob);
        }
    }

    private void UpdateChannel(UpdateChannelJob updateJob)
    {
        var channel = updateJob.Channel;
        var fullUpdate = updateJob.FullUpdate;

        var cts = new CancellationTokenSource();
        channel.CancellationTokenSource = cts;

        _ = Task.Run(async () =>
        {
            var lastUpdate = DateTime.Parse(channel.LastUpdate, CultureInfo.InvariantCulture);
            var daysSinceLastUpdate = (DateTime.Now - lastUpdate).Days;
            var count = Math.Max((int)(daysSinceLastUpdate * 1.5), 10) + channel.IncompleteCount;

            while (true)
            {
                var playlistInfo = updateJob.PlaylistInfo;
                if (playlistInfo is null)
                {
                    try
                    {
                        playlistInfo =
                            await _youtubeCommunicator.GetPlaylistInfoAsync(channel.ListId, cts.Token,
                                fullUpdate ? null : count);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    catch (Exception)
                    {
                        SetUpdateDone(channel, "Update failed.");
                        return;
                    }

                    var prevVideos = _channelData.GetVideos(channel.Id).ToArray();
                    var prevVideoIds = prevVideos.ToDictionary(x => x.VideoId, x => x.Id);

                    var prevLastVideo = prevVideos[^(channel.IncompleteCount > 0 ? channel.IncompleteCount : 1)];
                    if (!fullUpdate && playlistInfo.entries.All(x => x.id != prevLastVideo.VideoId))
                    {
                        _logger.LogDebug(
                            "Previous last video ID not found. Switching to full update. Channel: {Channel}",
                            channel.Title);
                        fullUpdate = true;
                        continue;
                    }

                    var updateDateTime = DateTime.UtcNow.ToString(CultureInfo.InvariantCulture);

                    int addedVideoCount;
                    try
                    {
                        addedVideoCount =
                            _channelData.UpdateChannel(channel, playlistInfo, prevVideoIds, updateDateTime);
                    }
                    catch (Exception)
                    {
                        SetUpdateDone(channel, "Update failed.");
                        return;
                    }

                    channel.Title = playlistInfo.channel;
                    channel.VideoCount = prevVideoIds.Count + addedVideoCount;
                    channel.IncompleteCount += addedVideoCount;
                    channel.AddedVideoCount += addedVideoCount;
                    channel.LastUpdate =
                        DateTime.Parse(updateDateTime).ToLocalTime().ToString(CultureInfo.InvariantCulture);
                }

                try
                {
                    await DownloadThumbnailsAsync(channel, playlistInfo, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception)
                {
                    SetUpdateDone(channel, "Getting thumbnails failed.");
                    return;
                }

                _channelData.SetIncompleteCount(channel.Id, 0);
                channel.IncompleteCount = 0;

                SetUpdateDone(channel, "✔ Updated successfully");
                break;
            }
        }, cts.Token);
    }

    private void SetUpdateDone(Channel channel, string statusText)
    {
        Dispatcher.UIThread.Post(() =>
        {
            channel.Updating = false;
            channel.StatusText = statusText;
            channel.CancellationTokenSource!.Dispose();
            channel.CancellationTokenSource = null;
            _activeUpdateJobCount--;
            TotalUpdateJobCount--;
            ProcessUpdateQueue();
        });
    }

    private static async Task DownloadThumbnailsAsync(Channel channel, PlaylistInfo playlistInfo,
        CancellationToken token)
    {
        using var httpClient = new HttpClient();

        var thumbsPath = Path.Combine(channel.Path, ".thumbs");
        Directory.CreateDirectory(thumbsPath);

        var i = 0;
        foreach (var video in playlistInfo.entries.Reverse())
        {
            i++;
            var filePath = Path.Combine(thumbsPath, $"{video.id}.jpg");
            if (File.Exists(filePath)) continue;

            Debug.WriteLine($"DL Thumb -> {i}: {video.id}");
            channel.StatusText = $"Getting Thumbnails... {i}/{playlistInfo.entries.Length}";
            var thumbUrl = video.thumbnails[0].url;
            try
            {
                var imageBytes = await httpClient.GetByteArrayAsync(thumbUrl, token);
                await File.WriteAllBytesAsync(filePath, imageBytes, token);
            }
            catch (HttpRequestException e)
            {
                if (e.StatusCode != HttpStatusCode.NotFound)
                    throw;
            }
        }
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task RemoveChannelPressedAsync(Channel channel)
    {
        var result = await _messenger.Send(new ShowMessageBoxMessage
        {
            Title = "Confirm deletion of channel",
            Message = $"Are you sure you want to delete this channel?\n\"{channel.Title}\"",
            ButtonDefinitions = ButtonEnum.YesNo, Icon = Icon.Question
        });
        if (result != ButtonResult.Yes) return;

        _channelData.RemoveChannel(channel.Id);
        ChannelCategories.First(x => x.Id == channel.CategoryId).Channels.Remove(channel);

        _messenger.Send(new ChannelDeletedMessage { Channel = channel });

        try
        {
            Directory.Delete(Path.Combine(channel.Path, ".thumbs"), true);
        }
        catch (Exception)
        {
            // ignored
        }

        // Delete the channel folder if it is empty
        try
        {
            Directory.Delete(channel.Path);
        }
        catch (Exception)
        {
            // ignored
        }
    }

    [RelayCommand]
    private async Task MoveChannelFolderAsync(Channel channel)
    {
        string? suggestedStartLocation = null;
        if (Directory.Exists(channel.Path))
            suggestedStartLocation = channel.Path;

        var selectedFolders = await _messenger.Send(new OpenFolderPickerMessage
        {
            Title = "Move channel folder to...",
            SuggestedStartLocation = suggestedStartLocation
        });
        if (selectedFolders.Count != 1) return;
        var destPath = new DirectoryInfo(selectedFolders[0]!.Path.LocalPath).GetActualPath();

        var isSamePath = Utils.IsSamePath(channel.Path, destPath);
        if (isSamePath)
        {
            return;
        }

        // Check for path duplication
        var channels = ChannelCategories.SelectMany(cat => cat.Channels);
        foreach (var ch in channels)
        {
            if (!Utils.IsSamePath(destPath, ch.Path)) continue;
            await _messenger.Send(new ShowMessageBoxMessage
            {
                Title = "Move", Message = $"The selected destination path belongs to another channel:\n\"{ch.Title}\"",
                Icon = Icon.Error, ButtonDefinitions = ButtonEnum.Ok
            });
            return;
        }

        await _messenger.Send(new ShowMoveChannelWindowMessage { Channel = channel, DestPath = destPath });
    }

    [RelayCommand]
    private void WatchButtonPressed(Video video)
    {
        _channelData.UpdateWatchedState(video.Id, !video.Watched);
        video.Watched = !video.Watched;
    }

    [RelayCommand]
    private void Deselect()
    {
        foreach (var video in SelectedVideos)
        {
            video.IsChecked = false;
        }

        SelectedVideos.Clear();
    }

    [RelayCommand]
    private void OpenVideoInYoutube(Video video)
    {
        var link = $"https://youtube.com/watch?v={video.VideoId}";
        Process.Start(new ProcessStartInfo(link) { UseShellExecute = true });
    }

    [RelayCommand]
    private void CopyLinks()
    {
        var links = string.Join("\n", SelectedVideos.Select(x => $"https://youtube.com/watch?v={x.VideoId}"));
        _messenger.Send(new SetClipboardTextMessage { Text = links });
    }

    [RelayCommand]
    private void CopyLink(Video video)
    {
        _messenger.Send(new SetClipboardTextMessage { Text = $"https://youtube.com/watch?v={video.VideoId}" });
    }

    [RelayCommand]
    private void SelectionToggle(Video video)
    {
        if (video.IsChecked)
        {
            SelectedVideos.Add(video);
        }
        else
        {
            SelectedVideos.Remove(video);
        }
    }

    [RelayCommand]
    private void DownloadVideoPressed()
    {
        if (SelectedChannel is null) return;
        var videoIds = SelectedVideos.Select(x => x.VideoId).ToList();
        _grabber.AddJob(videoIds, new List<string>(), SelectedChannel.Path, SelectedChannel);
    }

    [RelayCommand]
    private async Task ChangeCategoryAsync(Channel channel)
    {
        var channelCategorySettingsWindow = new ChannelCategorySettingsWindow
        {
            DataContext = new ChannelCategorySettingsWindowViewModel(ChannelCategories, channel, _channelData)
        };
        var window = ((IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!).MainWindow!;
        var result = await channelCategorySettingsWindow.ShowDialog<IEnumerable<ChannelCategory>?>(window);
        if (result is null) return;

        ChannelCategories = new ObservableCollection<ChannelCategory>(result);
        OnPropertyChanged(nameof(ChannelCategories));
    }

    [RelayCommand]
    private static void OpenChannelFolder(Channel channel)
    {
        var folderPath = channel.Path;
        if (!Directory.Exists(folderPath)) return;

        using var fileopener = new Process();
        Process.Start("explorer", $"\"{folderPath}\"");
    }

    public class UpdateChannelJob
    {
        public required Channel Channel { get; set; }
        public bool FullUpdate { get; set; }
        public PlaylistInfo? PlaylistInfo { get; set; }
    }

    public void Receive(VideoDownloadCompletedMessage message)
    {
        if (CurrentPage != 1) return;
        var dl = message.Value;
        if (!Utils.IsSamePath(dl.SaveTo, SelectedChannel?.Path)) return;
        var video = _allVideos.FirstOrDefault(x => x.VideoId == dl.VideoId);
        if (video is null) return;
        video.FileName = dl.Filename;
    }
}