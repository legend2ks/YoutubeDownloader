namespace YoutubeApp.Models;

public class ChannelDTO
{
    public required int Id { get; set; }
    public required string UniqueId { get; set; }
    public required string ListId { get; set; }
    public required string Title { get; set; }
    public required string Path { get; set; }
    public required int CategoryId { get; set; }
    public required string LastUpdate { get; set; }
    public required int VideoCount { get; set; }
    public required int IncompleteCount { get; set; }
    public required int AddedVideoCount { get; set; }
}