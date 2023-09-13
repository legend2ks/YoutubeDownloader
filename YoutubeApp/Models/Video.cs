using System.IO;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace YoutubeApp.Models;

public partial class Video : ObservableObject
{
    public int Id { get; set; }
    public int ChannelId { get; set; }
    public Channel Channel { get; set; }
    public string VideoId { get; set; }
    public string Title { get; set; }
    public string Duration { get; set; }
    public int Timestamp { get; set; }
    public string PublishedAt { get; set; }

    private string? _fileName;

    public string? FileName
    {
        get => _fileName;
        set
        {
            _fileName = value;
            OnPropertyChanged(nameof(IsDownloaded));
        }
    }

    public bool IsDownloaded => FileName is not null;

    [ObservableProperty] private bool _isChecked;
    [ObservableProperty] private bool _isCursor;

    [ObservableProperty] private bool _watched;

    private Bitmap? _thumbnail;

    private bool _thumbnailLoaded;

    public Bitmap? Thumbnail
    {
        get
        {
            if (_thumbnailLoaded) return _thumbnail;
            _thumbnail = LoadThumbnailBitmap();
            _thumbnailLoaded = true;
            return _thumbnail;
        }
    }


    private Bitmap? LoadThumbnailBitmap()
    {
        var thumbFilePath = Path.Combine(Channel.Path, ".thumbs", $"{VideoId}.jpg");
        if (!File.Exists(thumbFilePath)) return null;

        using var imageStream = File.OpenRead(thumbFilePath);
        var thumbnail = Bitmap.DecodeToWidth(imageStream, 200);
        return thumbnail;
    }
}