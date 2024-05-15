using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MsBox.Avalonia.Enums;
using YoutubeApp.Enums;
using YoutubeApp.Messages;

namespace YoutubeApp.ViewModels;

public partial class VideoWithPlaylist : ObservableObject
{
    public required string Link { get; init; } = "";
    public required List<string> VideoIds { get; init; }
    public required string PlaylistId { get; init; } = "";

    [ObservableProperty] private bool _videoIsSelected;

    [ObservableProperty] private bool _playlistIsSelected;
}

public partial class AddLinkWindowViewModel : ViewModelBase
{
    public AddLinkWindowViewModel(Settings settings, IMessenger messenger)
    {
        _settings = settings;
        _messenger = messenger;
        SaveTo = Settings.LastSavePath;
    }

    private readonly Settings _settings;
    private readonly IMessenger _messenger;

    private class ParseError
    {
        public required string Text { get; init; }
        public required int LineNumber { get; init; }
    }

    private const string VideoPattern =
        @"^(?:https?:\/\/)?(?:www\.|m\.)?youtu(?:\.be\/|be\.com\/\S*(?:watch|embed|shorts)(?:(?:(?=\/[-a-zA-Z0-9_]{11,}(?!\S))\/)|(?:\S*v=|v\/)))([-a-zA-Z0-9_]{11,})";

    private const string PlaylistPattern =
        @"^(?:https?:\/\/)?(?:www\.|m\.)?youtube\.com\/\S*(?:\?|&)list=([-a-zA-Z0-9_]{24,})";

    [ObservableProperty] private string _links = "";

    [ObservableProperty] private string _saveTo;

    [ObservableProperty] private int _currentPage = 0;

    private List<string> _videos = new();
    private List<string> _playlists = new();

    [ObservableProperty] private List<VideoWithPlaylist> _videosWithPlaylist = new();

    [ObservableProperty] private bool _continueButtonEnabled = true;

    private int _addedVideoCount;
    private int _addedPlaylistCount;

    public string Stats =>
        $"{_videos.Count + _addedVideoCount} Video(s) + {_playlists.Count + _addedPlaylistCount} Playlist(s)";

    [RelayCommand]
    private async Task ContinueButtonClickedAsync()
    {
        if (CurrentPage == 0)
        {
            var videos = new Dictionary<string, bool>();
            var playlists = new Dictionary<string, bool>();
            var videosWithPlaylist = new Dictionary<string, VideoWithPlaylist>();

            ParseError? error = null;

            var idx = 0;
            foreach (var link in Links.Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.None))
            {
                var trimmedLink = link.Trim();
                if (trimmedLink.Length == 0) continue;

                var videoMatch = Regex.Match(trimmedLink, VideoPattern);
                var playlistMatch = Regex.Match(trimmedLink, PlaylistPattern);

                if (videoMatch.Success)
                {
                    var videoId = videoMatch.Groups[1].Value;
                    if (playlistMatch.Success)
                    {
                        // Video + Playlist
                        var playlistId = playlistMatch.Groups[1].Value;

                        var videoIdExists = false;
                        if (videos.ContainsKey(videoId))
                        {
                            videos[videoId] = false;
                            videoIdExists = true;
                        }
                        else
                            videos.Add(videoId, false);

                        if (playlists.TryGetValue(playlistId, out var value))
                        {
                            if (value == true || videoIdExists)
                                continue;
                            var videoWithPlaylist = videosWithPlaylist[playlistId];
                            videoWithPlaylist.VideoIds.Add(videoId);
                        }
                        else
                        {
                            playlists.Add(playlistId, false);
                            videosWithPlaylist.Add(playlistId, new VideoWithPlaylist
                            {
                                Link = link,
                                PlaylistId = playlistId,
                                VideoIds = new List<string> { videoId }
                            });
                        }
                    }
                    else
                    {
                        // Video only
                        if (videos.ContainsKey(videoId))
                            continue;
                        videos.Add(videoId, true);
                    }
                }
                else if (playlistMatch.Success)
                {
                    // Playlist only
                    var playlistId = playlistMatch.Groups[1].Value;

                    if (playlists.ContainsKey(playlistId))
                    {
                        if (playlists[playlistId] == true)
                            continue;
                        playlists[playlistId] = true;
                        videosWithPlaylist.Remove(playlistId);
                    }
                    else
                    {
                        playlists.Add(playlistId, true);
                    }
                }
                else
                {
                    // Nothing
                    error = new ParseError { Text = link, LineNumber = idx + 1 };
                    break;
                }

                idx++;
            }

            if (error is not null)
            {
                await _messenger.Send(new ShowMessageBoxMessage
                {
                    Title = "Error", Message = $"Invalid link at line {error.LineNumber}:\n{error.Text}",
                    Icon = Icon.Error, ButtonDefinitions = ButtonEnum.Ok
                }, (int)MessengerChannel.AddLinkWindow);
                Reset();
                return;
            }

            _videos = videos.Where(x => x.Value == true).Select(x => x.Key).ToList();
            _playlists = playlists.Where(x => x.Value == true).Select(x => x.Key).ToList();
            VideosWithPlaylist = videosWithPlaylist.Select(x => x.Value).ToList();

            if (_videos.Count == 0 && _playlists.Count == 0 && VideosWithPlaylist.Count == 0)
            {
                return;
            }

            // Check save path
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
                }, (int)MessengerChannel.AddLinkWindow);
                Reset();
                return;
            }

            OnPropertyChanged(nameof(Stats));

            if (VideosWithPlaylist.Count > 0)
            {
                ContinueButtonEnabled = false;
            }
            else
            {
                StartJob();
                return;
            }

            CurrentPage = 1;
        }
        else if (CurrentPage == 1) // ✔ Confirm
        {
            StartJob();
        }
    }

    [RelayCommand]
    private void RadioButtonClicked()
    {
        _addedVideoCount = 0;
        _addedPlaylistCount = 0;
        var allDone = true;

        foreach (var vp in VideosWithPlaylist)
        {
            if (vp.VideoIsSelected)
            {
                _addedVideoCount += vp.VideoIds.Count;
                continue;
            }

            if (vp.PlaylistIsSelected)
            {
                _addedPlaylistCount++;
                continue;
            }

            allDone = false;
        }

        if (allDone)
        {
            ContinueButtonEnabled = true;
        }

        OnPropertyChanged(nameof(Stats));
    }

    [RelayCommand]
    private async Task BrowseButtonPressedAsync()
    {
        string? suggestedStartLocation = null;
        if (Directory.Exists(SaveTo))
            suggestedStartLocation = SaveTo;

        var selectedFolders = await _messenger.Send(new OpenFolderPickerMessage
        {
            Title = "Save To...",
            SuggestedStartLocation = suggestedStartLocation
        }, (int)MessengerChannel.AddLinkWindow);
        if (selectedFolders.Count == 0) return;

        SaveTo = selectedFolders[0]!.Path.LocalPath;
    }

    private void StartJob()
    {
        foreach (var vp in VideosWithPlaylist)
        {
            if (vp.VideoIsSelected)
            {
                _videos.AddRange(vp.VideoIds);
            }
            else if (vp.PlaylistIsSelected)
            {
                _playlists.Add(vp.PlaylistId);
            }
        }

        _settings.SaveLastSavePath(SaveTo);

        _messenger.Send(new CloseWindowMessage<AddLinkWindowResult>(new AddLinkWindowResult
            { Videos = _videos, Playlists = _playlists, SavePath = SaveTo }), (int)MessengerChannel.AddLinkWindow);
    }

    private void Reset()
    {
        _videos.Clear();
        _playlists.Clear();
        VideosWithPlaylist.Clear();
    }
}

public class AddLinkWindowResult
{
    public required List<string> Videos { get; init; }
    public required List<string> Playlists { get; init; }
    public required string SavePath { get; init; }
}