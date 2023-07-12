// ReSharper disable InconsistentNaming

namespace YoutubeApp.Media;

public class PlaylistInfoEntry
{
    public required string id { get; set; }
    public required string title { get; set; }
    public required int? duration { get; set; }
    public required long? timestamp { get; set; }
    public required PlaylistInfoEntryThumbnail[] thumbnails { get; set; }
}