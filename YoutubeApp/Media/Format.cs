namespace YoutubeApp.Media;

public class Format
{
    public required string Protocol { get; set; }
    public string? Url { get; set; }
    public bool Throttled { get; set; }
    public VideoInfoFormatFragment[]? Fragments { get; set; } //?
}