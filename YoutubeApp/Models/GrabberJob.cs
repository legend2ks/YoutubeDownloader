using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YoutubeApp.Media;

namespace YoutubeApp.Models;

public partial class GrabberJob : ObservableObject
{
    private readonly Youtube _youtube;
    public int Id { get; private set; }
    public List<string> VideoIds { get; private set; }
    public List<string> PlaylistIds { get; private set; }

    [ObservableProperty] private Dictionary<string, GrabberJobPlaylist>? _playlists;

    public string Title { get; private set; }
    public Channel? Channel { get; }
    public string SavePath { get; private set; }
    public string DateAdded { get; private set; }

    private readonly CancellationTokenSource _cancellationTokenSource = new();
    public CancellationToken CancellationToken { get; }


    [ObservableProperty] [NotifyPropertyChangedFor(nameof(StatusText))]
    private int _doneCount;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(ErrorCountText))]
    private int _errorCount;

    public int TotalCount { get; set; }

    [ObservableProperty] private int _duplicateCount;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(RetryIsEnabled))]
    private bool _isFailed;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(RetryIsEnabled))]
    private bool _isFinished;

    public string InputCountText
    {
        get
        {
            if (PlaylistIds.Count == 0)
            {
                return $"{VideoIds.Count} Video(s)";
            }

            if (VideoIds.Count == 0)
            {
                return $"{PlaylistIds.Count} List(s)";
            }

            return $"{VideoIds.Count} Video(s)  &  {PlaylistIds.Count} List(s)";
        }
    }

    public string StatusText =>
        IsFailed
            ? "Failed"
            : $"{(IsFinished ? "Finished" : "Fetching...")} {(IsFinished || TotalCount == 0 ? DoneCount : DoneCount + 1)}/{(TotalCount == 0 ? "-" : TotalCount)}";

    [ObservableProperty] private string _statusTextColor = "White";

    private readonly List<GrabberJobVideo> _failedVideos = [];

    public string ErrorCountText => $"{ErrorCount} Error(s)";

    public bool RetryIsEnabled => IsFailed || (IsFinished && ErrorCount > 0);

    public static event EventHandler<CancelledEventArgs>? Cancelled;
    public static event EventHandler? Error;
    public static event EventHandler? Failed;
    public static event EventHandler? Finished;
    public static event EventHandler? Retry;


    public GrabberJob(int id, List<string> videoIds, List<string> playlistIds, string savePath, string title,
        Channel? channel, Youtube youtube)
    {
        _youtube = youtube;
        Id = id;
        VideoIds = videoIds;
        PlaylistIds = playlistIds;
        SavePath = savePath;
        Title = title;
        Channel = channel;
        DateAdded = DateTime.Now.ToString("HH:mm");
        CancellationToken = _cancellationTokenSource.Token;
    }

    public void SetTotalVideoCount(int totalVideoCount, int duplicateCount)
    {
        TotalCount = totalVideoCount;
        DuplicateCount = duplicateCount;
        OnPropertyChanged(nameof(StatusText));
    }

    [RelayCommand]
    private void CancelButtonPressed()
    {
        var isActive = false;
        if (!IsFinished && !IsFailed)
        {
            isActive = true;
            _cancellationTokenSource.Cancel();
        }

        Cancelled?.Invoke(this, new CancelledEventArgs { IsActive = isActive });
    }

    public void SetFailed()
    {
        IsFailed = true;
        StatusTextColor = "#d27570";
        OnPropertyChanged(nameof(StatusText));
        Error?.Invoke(this, EventArgs.Empty);
        Failed?.Invoke(this, EventArgs.Empty);
    }

    public void SetJobFinished()
    {
        IsFinished = true;
        OnPropertyChanged(nameof(StatusText));
        Finished?.Invoke(this, EventArgs.Empty);
    }

    public void SetVideoStatus(GrabberJobVideo video, VideoStatus status, string? desc = null, string? errorMsg = null)
    {
        switch (status)
        {
            case VideoStatus.Waiting:
                video.StatusText = "Waiting  ◽ ";
                break;
            case VideoStatus.Fetching:
                video.StatusText = "Fetching  ◻ ";
                break;
            case VideoStatus.Duplicate:
                video.StatusText = "Duplicate ✖";
                break;
            case VideoStatus.Done:
                video.StatusText = "Added ✔";
                DoneCount++;
                break;
            case VideoStatus.Error:
                video.StatusText = $"Error{(desc is not null ? " (" + desc + ")" : "")} ❌";
                ErrorCount++;
                DoneCount++;
                _failedVideos.Add(video);
                Error?.Invoke(this, EventArgs.Empty);
                break;
            default:
                throw new UnreachableException();
        }

        video.Status = status;
        video.ErrorMessage = errorMsg;
    }

    public void TryAgain()
    {
        if (IsFailed)
        {
            IsFailed = false;
            StatusTextColor = "White";
            OnPropertyChanged(nameof(StatusText));

            _ = _youtube.GetVideosAsync(this);
        }
        else
        {
            foreach (var video in _failedVideos)
            {
                SetVideoStatus(video, VideoStatus.Waiting);
            }

            IsFinished = false;
            ErrorCount -= _failedVideos.Count;
            DoneCount -= _failedVideos.Count;
            _failedVideos.Clear();

            _ = _youtube.GetVideosAsync(this, false);
        }

        Retry?.Invoke(this, EventArgs.Empty);
    }
}

public class GrabberJobPlaylist
{
    public required string Title { get; set; }
    public List<GrabberJobVideo> Videos { get; set; } = new();
}

public partial class GrabberJobVideo : ObservableObject
{
    public string VideoId { get; init; }

    [ObservableProperty] private string _title;

    [ObservableProperty] private VideoStatus _status;

    [ObservableProperty] private string _statusText;

    [ObservableProperty] private string? _errorMessage;
}

public enum VideoStatus
{
    Waiting,
    Fetching,
    Duplicate,
    Done,
    Error,
}

public class CancelledEventArgs
{
    public required bool IsActive { get; init; }
}