using System;
using System.Collections.Generic;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Humanizer;
using Humanizer.Localisation;
using YoutubeApp.Media;

namespace YoutubeApp.Models;

public partial class Download : ObservableObject
{
    public int Id { get; set; }
    public string VideoId { get; set; }
    public string Uuid { get; set; }

    [ObservableProperty] private string _title;
    public string Container { get; set; }
    public List<Variant> Variants { get; set; }
    public Dictionary<string, Format> Formats { get; set; }
    public List<Chapter>? Chapters { get; set; }
    public string Duration { get; set; }
    [ObservableProperty] private bool _missingFormats;

    private bool _refreshing;

    public bool Refreshing
    {
        get => _refreshing;
        private set
        {
            SetProperty(ref _refreshing, value);
            OnPropertyChanged(nameof(EnabledSwitchEnabled));
            OnPropertyChanged(nameof(VariantButtonEnabled));
        }
    }

    public CancellationTokenSource RefreshCancellationTokenSource { get; set; }

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _error;

    public bool HasError => _error is not null;

    [ObservableProperty] private string _filename;

    [ObservableProperty] private string _saveTo;
    [ObservableProperty] private string _channelTitle;
    public string UploadDate { get; set; }

    private long _filesize;

    public long Filesize
    {
        get => _filesize;
        set => SetProperty(ref _filesize, value);
    }

    private long _bytesLoaded;

    public long BytesLoaded
    {
        get => _bytesLoaded;
        set
        {
            SetProperty(ref _bytesLoaded, value);
            OnPropertyChanged(nameof(Progress));
            OnPropertyChanged(nameof(ProgressText));
            OnPropertyChanged(nameof(Eta));
        }
    }

    public bool EnabledSwitchEnabled
    {
        get => !Downloading && !Completed && !Refreshing && SelectedVariant.Id != -1;
    }

    [ObservableProperty] private int _speed;

    [ObservableProperty] private int _connections;

    public string Eta => Speed == 0
        ? "· · ·"
        : TimeSpan.FromSeconds((Filesize - BytesLoaded) / Speed).Humanize(2, minUnit: TimeUnit.Second);

    public SelectedVariant SelectedVariant { get; set; }

    public void ChangeSelectedVariant(SelectedVariant selectedVariant)
    {
        SelectedVariant = selectedVariant;
        OnPropertyChanged(nameof(NoVariant));
        OnPropertyChanged(nameof(EnabledSwitchEnabled));
        OnPropertyChanged(nameof(SelectedVariant));
        OnPropertyChanged(nameof(SelectedVariantDescription));
    }

    private bool _enabled;

    public bool Enabled
    {
        get => _enabled;
        set
        {
            SetProperty(ref _enabled, value);
            OnPropertyChanged(nameof(VariantButtonEnabled));
            if (Completed || (Refreshing && !Downloading)) return;
            EnableStateChanged?.Invoke(this, new EnableStateChangedEventArgs { Download = this });
        }
    }

    public float Progress => Completed ? 100 : (float)BytesLoaded / Filesize * 100;
    public string ProgressText => Message ?? Error ?? (Completed ? "Complete" : $"{Progress:0.#}%");

    [ObservableProperty] private bool _progressIndeterminate;

    public string? Message { get; set; }

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(EnabledSwitchEnabled))]
    private bool _downloading;

    [ObservableProperty] private bool _completed;

    public bool VariantButtonEnabled => !Enabled && !Completed && !Refreshing;

    public bool NoVariant => SelectedVariant.Id == -1;

    public string SelectedVariantDescription => SelectedVariant.Description;

    public int Priority { get; set; }
    public string ChannelId { get; set; }
    public Channel? Channel { get; set; }

    public static event EventHandler<EnableStateChangedEventArgs>? EnableStateChanged;

    public void SetStarted()
    {
        Error = null;
        Downloading = true;
        OnPropertyChanged(nameof(ProgressText));
    }

    public void SetCompleted()
    {
        Completed = true;
        Enabled = false;
        SetStopped();
    }

    public void SetFailed(string errorMsg)
    {
        Error = errorMsg;
        SetStopped();
    }

    public void SetStopped()
    {
        Downloading = false;
        Speed = 0;
        Connections = 0;
        Message = null;
        ProgressIndeterminate = false;
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(Eta));
    }

    public void SetRefreshing(bool isRefreshing)
    {
        if (isRefreshing)
        {
            Error = null;
        }

        Refreshing = isRefreshing;
        Message = isRefreshing ? "Refreshing..." : null;
        ProgressIndeterminate = isRefreshing;
        Speed = 0;
        Connections = 0;
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(Eta));
    }

    public void SetMuxing()
    {
        Message = "Muxing...";
        Speed = 0;
        Connections = 0;
        ProgressIndeterminate = true;
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(Eta));
    }
}

public class EnableStateChangedEventArgs
{
    public required Download Download { get; init; }
}