using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace YoutubeApp.Models;

public partial class Channel : ObservableObject
{
    public int Id { get; set; }
    public string UniqueId { get; set; }
    public string ListId { get; set; }

    public string Path { get; set; }
    public int CategoryId { get; set; }
    public int IncompleteCount { get; set; }

    [ObservableProperty] private int _addedVideoCount;

    [ObservableProperty] private string _lastUpdate;
    [ObservableProperty] private string _localLastUpdate;

    [ObservableProperty] private string _title;

    [ObservableProperty] private int _videoCount;

    [ObservableProperty] private bool _updating;

    [ObservableProperty] private string _statusText;

    public CancellationTokenSource? CancellationTokenSource { get; set; }
}