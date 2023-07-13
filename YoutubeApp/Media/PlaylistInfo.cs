// ReSharper disable InconsistentNaming

namespace YoutubeApp.Media;

public class PlaylistInfo
{
    public required string id { get; set; }
    public required string title { get; set; }
    public required string channel_id { get; set; }
    public required string channel { get; set; }
    public required string uploader { get; set; }
    public required string availability { get; set; } //public
    public required int playlist_count { get; set; } //including_hidden
    public required PlaylistInfoEntry[] entries { get; set; } //including_hidden
}