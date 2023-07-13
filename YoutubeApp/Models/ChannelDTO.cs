namespace YoutubeApp.Models;

public class ChannelDTO
{
    public int Id { get; set; }
    public string UniqueId { get; set; }
    public string ListId { get; set; }
    public string Title { get; set; }
    public string Path { get; set; }
    public int CategoryId { get; set; }
    public string LastUpdate { get; set; }
    public int VideoCount { get; set; }
}