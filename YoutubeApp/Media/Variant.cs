namespace YoutubeApp.Media;

public class Variant
{
    public int Id { get; set; }
    public required string VFormatId { get; set; }
    public required string VCodec { get; set; }
    public required int Width { get; set; }
    public required int Height { get; set; }
    public required float Fps { get; set; }
    public required float Vbr { get; set; }
    public required string AFormatId { get; set; }
    public required string ACodec { get; set; }
    public required float Abr { get; set; }
    public required long Filesize { get; set; }
    public required bool IsApproxFilesize { get; set; }
}