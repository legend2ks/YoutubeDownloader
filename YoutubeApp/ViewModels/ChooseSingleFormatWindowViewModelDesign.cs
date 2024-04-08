using System.Collections.Generic;
using System.Linq;
using YoutubeApp.Media;
using YoutubeApp.Models;

namespace YoutubeApp.ViewModels;

internal class ChooseSingleFormatWindowViewModelDesign : ChooseSingleFormatWindowViewModel
{
    public ChooseSingleFormatWindowViewModelDesign() : base(new Download
    {
        Variants = Enumerable.Range(1, 6).Select(x => new Variant
        {
            Id = x,
            VCodec = "av01",
            ACodec = "mp4a",
            Width = 1280,
            Height = 720,
            Abr = 64,
            Vbr = 256,
            VFormatId = "301",
            AFormatId = "289",
            Filesize = x * 4096,
            Fps = 60,
            IsApproxFilesize = x % 3 == 0,
        }).ToList(),
        Formats = new Dictionary<string, Format>
        {
            { "301", new Format { Protocol = Protocol.Https } },
            { "289", new Format { Protocol = Protocol.Https } },
        },
        Chapters = null,
    }, null!, null!, null!)
    {
    }
}