using YoutubeApp.Media;

namespace YoutubeApp.Models;

public class VariantItem
{
    public required Variant Variant { get; init; }
    public required string Color { get; set; }
    public required string Description { get; set; }
}