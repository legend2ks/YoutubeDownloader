using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace YoutubeApp.Models;

public partial class Channel : ObservableObject
{
    public int Id { get; set; }
    public string ListId { get; set; }

    public string Path { get; set; }
    public int CategoryId { get; set; }

    [ObservableProperty] private string _lastUpdate;

    [ObservableProperty] private string _title;

    [ObservableProperty] private int _videoCount;

    [ObservableProperty] private bool _updating;

    [ObservableProperty] private string _statusText;

    public CancellationTokenSource? CancellationTokenSource { get; set; }
}