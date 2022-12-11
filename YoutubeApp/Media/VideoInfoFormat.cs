// ReSharper disable InconsistentNaming

namespace YoutubeApp.Media;

public class VideoInfoFormat
{
    public required string format_id { get; set; }
    public string? format_note { get; set; }
    public int? width { get; set; }
    public int? height { get; set; }
    public string? vcodec { get; set; }
    public string? acodec { get; set; }
    public float? abr { get; set; }
    public float? vbr { get; set; }
    public float? fps { get; set; }
    public long? filesize { get; set; }
    public long? filesize_approx { get; set; }
    public required string url { get; set; }
    public string? fragment_base_url { get; set; }
    public required string protocol { get; set; }
    public VideoInfoFormatFragment[]? fragments { get; set; }
}