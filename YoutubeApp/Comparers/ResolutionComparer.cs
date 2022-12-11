using System.Collections.Generic;

namespace YoutubeApp.Comparers;

internal class ResolutionComparer : IComparer<string>
{
    public int Compare(string x, string y)
    {
        var xsplit = x.Split("x");
        var ysplit = y.Split("x");
        return (int.Parse(ysplit[0]) * int.Parse(ysplit[1]))
            .CompareTo(int.Parse(xsplit[0]) * int.Parse(xsplit[1]));
    }
}