using System.Collections.ObjectModel;

namespace YoutubeApp.Models;

public class ChannelCategory
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public int Parent { get; set; }
    public ObservableCollection<Channel> Channels { get; init; } = new();
}