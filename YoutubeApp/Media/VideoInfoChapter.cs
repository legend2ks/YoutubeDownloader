// ReSharper disable InconsistentNaming

namespace YoutubeApp.Media;

public class VideoInfoChapter
{
    public required string title { get; set; }
    public required float start_time { get; set; }
    public required float end_time { get; set; }
}