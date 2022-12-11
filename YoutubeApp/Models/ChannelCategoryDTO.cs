namespace YoutubeApp.Models;

public class ChannelCategoryDTO
{
    public required int Id { get; set; }
    public required string Title { get; set; }
    public required int Parent { get; set; }
}