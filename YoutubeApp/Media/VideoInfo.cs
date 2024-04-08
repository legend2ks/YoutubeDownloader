// ReSharper disable InconsistentNaming

namespace YoutubeApp.Media;

public class VideoInfo
{
    public required string title { get; set; }
    public required string duration_string { get; set; }
    public required string channel { get; set; }
    public required string upload_date { get; set; }
    public required string live_status { get; set; }
    public required string channel_id { get; set; }
    public required VideoInfoFormat[] formats { get; set; }
    public VideoInfoChapter[]? chapters { get; set; }
}