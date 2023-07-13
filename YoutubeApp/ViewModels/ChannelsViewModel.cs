using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MessageBox.Avalonia.Enums;
using Microsoft.Extensions.Logging;
using YoutubeApp.Database;
using YoutubeApp.Media;
using YoutubeApp.Messages;
using YoutubeApp.Models;
using YoutubeApp.Views;

namespace YoutubeApp.ViewModels;

public partial class ChannelsViewModel : ViewModelBase
{
    public ChannelsViewModel(ILogger<ChannelsViewModel> logger, Grabber grabber, ChannelData channelData,
        IYoutubeCommunicator youtubeCommunicator,
        IMessenger messenger)
    {
        _logger = logger;
        _grabber = grabber;
        _channelData = channelData;
        _youtubeCommunicator = youtubeCommunicator;
        _messenger = messenger;

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

    private readonly ILogger<ChannelsViewModel> _logger;
    private readonly Grabber _grabber;
    private readonly ChannelData _channelData;
    private readonly IYoutubeCommunicator _youtubeCommunicator;
    private readonly IMessenger _messenger;
    [ObservableProperty] private int _currentPage;

    public ObservableCollection<ChannelCategory> ChannelCategories { get; set; }

    [ObservableProperty] private Channel? _selectedChannel;

    private List<Video> _allVideos;
    [ObservableProperty] private List<Video> _videos;
    [ObservableProperty] private string _searchText = string.Empty;

    [ObservableProperty] private Vector _videosScrollOffset;

    private IDisposable _searchObservable;

    public ObservableCollection<Video> SelectedVideos { get; } = new();


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

        SelectedChannel = channel;
        var videos = _channelData.GetVideos(channel.Id);
        _allVideos = videos.Reverse().ToList();

        // Check available files
        if (Directory.Exists(channel.Path))
        {
            var dirFiles = Directory.GetFiles(channel.Path).Select(Path.GetFileName).ToArray();
            foreach (var video in _allVideos)
            {
                video.Channel = channel;
                foreach (var file in dirFiles)
                {
                    if (!file.Contains($"[{video.VideoId}]")) continue;
                    video.FileName = file;
                    break;
                }
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
        var filepath = Path.Combine(SelectedChannel!.Path, video.FileName);
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

        result.Channel.Updating = true;
        var cts = new CancellationTokenSource();
        result.Channel.CancellationTokenSource = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                await DownloadThumbnailsAsync(result.Channel, result.PlaylistInfo, cts.Token);
            }
            catch (Exception)
            {
                result.Channel.StatusText = "Getting thumbnails failed.";
                return;
            }
            finally
            {
                result.Channel.Updating = false;
                result.Channel.CancellationTokenSource.Dispose();
            }

            result.Channel.StatusText = string.Empty;
        }, cts.Token);
    }

    [RelayCommand]
    private void UpdateChannelPressed(Channel channel)
    {
        UpdateChannel(channel);
    }

    [RelayCommand]
    private void FullChannelUpdatePressed(Channel channel)
    {
        UpdateChannel(channel, true);
    }

    private void UpdateChannel(Channel channel, bool fullUpdate = false)
    {
        if (channel.Updating)
        {
            channel.CancellationTokenSource!.Cancel();
            channel.Updating = false;
            channel.StatusText = string.Empty;
            return;
        }

        channel.Updating = true;
        channel.StatusText = "Updating...";
        var cts = new CancellationTokenSource();
        channel.CancellationTokenSource = cts;

        _ = Task.Run(async () =>
        {
            var lastUpdate = DateTime.Parse(channel.LastUpdate);
            var daysSinceLastUpdate = (DateTime.Now - lastUpdate).Days;
            var count = Math.Max((int)(daysSinceLastUpdate * 1.5), 10);

            while (true)
            {
                PlaylistInfo playlistInfo;
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
                    channel.Updating = false;
                    channel.StatusText = "Update failed.";
                    channel.CancellationTokenSource.Dispose();
                    return;
                }

                var prevVideos = _channelData.GetVideos(channel.Id).ToArray();
                var prevVideoIds = prevVideos.ToDictionary(x => x.VideoId, x => x.Id);

                var prevLastVideo = prevVideos[^1];
                if (!fullUpdate && playlistInfo.entries.All(x => x.id != prevLastVideo.VideoId))
                {
                    _logger.LogDebug("Previous last video ID not found. Switching to full update. Channel: {Channel}",
                        channel.Title);
                    fullUpdate = true;
                    continue;
                }

                var updateDateTime = DateTime.UtcNow.ToString();

                int totalVideoCount;
                try
                {
                    totalVideoCount = _channelData.UpdateChannel(channel, playlistInfo, prevVideoIds, updateDateTime);
                }
                catch (Exception)
                {
                    channel.Updating = false;
                    channel.StatusText = "Update failed.";
                    channel.CancellationTokenSource.Dispose();
                    return;
                }

                var addedCount = totalVideoCount - channel.VideoCount;

                channel.Title = playlistInfo.channel;
                channel.VideoCount = totalVideoCount;
                channel.LastUpdate = DateTime.Parse(updateDateTime).ToLocalTime().ToString();

                try
                {
                    await DownloadThumbnailsAsync(channel, playlistInfo, cts.Token);
                }
                catch (Exception)
                {
                    channel.Updating = false;
                    channel.StatusText = "Getting thumbnails failed.";
                    channel.CancellationTokenSource.Dispose();
                    return;
                }

                channel.Updating = false;
                channel.StatusText = $"Updated successfully (+{(addedCount < 0 ? 0 : addedCount)})";
                channel.CancellationTokenSource.Dispose();
                break;
            }
        }, cts.Token);
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
            var imageBytes = await httpClient.GetByteArrayAsync(thumbUrl, token);
            await File.WriteAllBytesAsync(filePath, imageBytes, token);
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

        try
        {
            Directory.Delete(Path.Combine(channel.Path, ".thumbs"), true);
        }
        catch (Exception)
        {
            // ignored
        }

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
    private void CopyLink()
    {
        var links = string.Join("\n", SelectedVideos.Select(x => $"https://youtube.com/watch?v={x.VideoId}"));
        _messenger.Send(new SetClipboardTextMessage { Text = links });
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
}