namespace YoutubeApp.Models;

public class DownloadDTO
{
    public int Id { get; set; }
    public string VideoId { get; set; }
    public string Uuid { get; set; }
    public string Title { get; set; }
    public string Container { get; set; }
    public string? SelectedVariant { get; set; }
    public string Variants { get; set; }
    public string Formats { get; set; }
    public string? Chapters { get; set; }
    public string Duration { get; set; }
    public string Filename { get; set; }
    public string SaveTo { get; set; }
    public string ChannelTitle { get; set; }
    public string ChannelId { get; set; }
    public string UploadDate { get; set; }
    public long Filesize { get; set; }
    public long BytesLoaded { get; set; }
    public bool Enabled { get; set; }
    public bool Completed { get; set; }
    public bool MissingFormats { get; set; }
    public int Priority { get; set; }
}