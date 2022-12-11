namespace YoutubeApp.Media;

public class SelectedVariant
{
    public required int Id { get; set; }
    public required string VFormatId { get; set; }
    public required string AFormatId { get; set; }
    public required string VFormatItagNoDash { get; set; }
    public required string AFormatItagNoDash { get; set; }
    public required string VFormatProtocol { get; set; }
    public required string AFormatProtocol { get; set; }
    public required bool VFormatThrottled { get; set; }
    public required bool AFormatThrottled { get; set; }
    public required string VideoLmt { get; set; }
    public required string AudioLmt { get; set; }
    public required string Description { get; set; }
    public required bool IsApproxFilesize { get; set; }
}