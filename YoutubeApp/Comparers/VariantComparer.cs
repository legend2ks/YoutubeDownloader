using System;
using System.Collections.Generic;
using DynamicData;
using YoutubeApp.Media;

namespace YoutubeApp.Comparers;

internal class VariantComparer : IComparer<Variant>
{
    private static readonly string[] Vcodecs = { "av01", "vp9", "avc1" };
    private static readonly string[] Acodecs = { "opus", "mp4a" };

    public int Compare(Variant? x, Variant? y)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);

        float result = y.Height - x.Height;
        if (result == 0) result = y.Fps - x.Fps;
        if (result == 0) result = y.Abr - x.Abr;
        if (result == 0) result = Vcodecs.IndexOf(x.VCodec) - Vcodecs.IndexOf(y.VCodec);
        if (result == 0) result = Acodecs.IndexOf(x.ACodec) - Acodecs.IndexOf(y.ACodec);

        return result switch
        {
            > 0 => 1,
            < 0 => -1,
            _ => 0
        };
    }
}