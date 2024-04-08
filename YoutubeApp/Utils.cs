using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using DynamicData;
using YoutubeApp.Media;
using YoutubeApp.Models;

namespace YoutubeApp;

public static class Utils
{
    public static string FormatBytes(long bytes)
    {
        // Determine the suffix and readable value
        string suffix;
        double readable;
        switch (bytes)
        {
            // Exabyte
            case >= 0x1000000000000000:
                suffix = "EB";
                readable = (bytes >> 50);
                break;
            // Petabyte
            case >= 0x4000000000000:
                suffix = "PB";
                readable = (bytes >> 40);
                break;
            // Terabyte
            case >= 0x10000000000:
                suffix = "TB";
                readable = (bytes >> 30);
                break;
            // Gigabyte
            case >= 0x40000000:
                suffix = "GB";
                readable = (bytes >> 20);
                break;
            // Megabyte
            case >= 0x100000:
                suffix = "MB";
                readable = (bytes >> 10);
                break;
            // Kilobyte
            case >= 0x400:
                suffix = "KB";
                readable = bytes;
                break;
            // Byte
            case > 0:
                return bytes.ToString("0 B");
            default:
                return "0";
        }

        // Divide by 1024 to get fractional value
        readable = (readable / 1024);
        // Return formatted number with suffix
        return readable.ToString("0.## ") + suffix;
    }

    public static string ExtractLmt(string url)
    {
        var urlSegments = url.Split("/");
        var lmtIndex = urlSegments.IndexOf("lmt");
        if (lmtIndex != -1)
        {
            return urlSegments[lmtIndex + 1];
        }

        var splittedByQuestionMark = url.Split("?");
        if (splittedByQuestionMark.Length == 1)
            throw new Exception("Lmt not found");
        var videoFormatUrlQueryString = splittedByQuestionMark[1];
        var videoFormatUrlQueryParams = HttpUtility.ParseQueryString(videoFormatUrlQueryString);
        if (videoFormatUrlQueryParams == null)
            throw new Exception("Lmt not found");
        var lmt = videoFormatUrlQueryParams.Get("lmt");
        if (lmt == null)
            throw new Exception("Lmt not found");
        return lmt;
    }

    public static string GenerateGid(int digits = 16)
    {
        var random = new Random();
        var buffer = new byte[digits / 2];
        random.NextBytes(buffer);
        var result = string.Concat(buffer.Select(x => x.ToString("X2")).ToArray());
        return digits % 2 == 0 ? result.ToLower() : (result + random.Next(16).ToString("X")).ToLower();
    }

    public static string GenerateVariantDescription(Variant variant, string vformatProtocol, string aformatProtocol,
        bool vformatThrottled, bool aformatThrottled)
    {
        return
            $"{(vformatThrottled ? "!" : "")}{variant.VCodec}  {variant.Height}p{variant.Fps}  {(aformatThrottled ? "!" : "")}{variant.ACodec}@{Math.Round(variant.Abr)}";
    }

    public static string FindCommonPath(string[] paths)
    {
        var firstPath = paths[0];
        var commonPathLength = firstPath.Length;

        for (var i = 1; i < paths.Length; i++)
        {
            var otherPath = paths[i];
            var pos = -1;
            var checkpoint = -1;

            while (true)
            {
                pos++;

                if (pos == commonPathLength)
                {
                    if (pos == otherPath.Length
                        || (pos < otherPath.Length
                            && (otherPath[pos] == '/' || otherPath[pos] == '\\')))
                    {
                        checkpoint = pos;
                    }

                    break;
                }

                if (pos == otherPath.Length)
                {
                    if (pos == commonPathLength
                        || (pos < commonPathLength
                            && (firstPath[pos] == '/' || firstPath[pos] == '\\')))
                    {
                        checkpoint = pos;
                    }

                    break;
                }

                if ((firstPath[pos] == '/' || firstPath[pos] == '\\')
                    && (otherPath[pos] == '/' || otherPath[pos] == '\\'))
                {
                    checkpoint = pos;
                    continue;
                }

                var a = char.ToLowerInvariant(firstPath[pos]);
                var b = char.ToLowerInvariant(otherPath[pos]);

                if (a != b)
                    break;
            }

            if (checkpoint == 0 && (firstPath[0] == '/' || firstPath[0] == '\\'))
                commonPathLength = 1;
            else commonPathLength = checkpoint;

            if (commonPathLength is -1 or 0)
                return "";
        }

        return firstPath[..commonPathLength];
    }

    public static string DurationStringFromSeconds(int seconds)
    {
        if (seconds == 0) return "0s";
        var durationString = TimeSpan.FromSeconds(seconds).ToString().TrimStart('0', ':');
        if (durationString.Length < 3)
            durationString += "s";
        return durationString;
    }

    public static bool IsSamePath(string? path1, string? path2)
    {
        if (path1 is null || path2 is null)
            return false;
        return string.Equals(Path.TrimEndingDirectorySeparator(path1), Path.TrimEndingDirectorySeparator(path2),
            StringComparison.OrdinalIgnoreCase);
    }

    public static string GenerateChapters(List<Chapter> chapters)
    {
        const string template = """
                                [CHAPTER]
                                TIMEBASE=1/1000
                                START={0}
                                END={1}
                                title={2}
                                """;
        var metadata = new StringBuilder();
        metadata.AppendLine(";FFMETADATA1");
        foreach (var chapter in chapters)
        {
            var start = (int)Math.Round(chapter.StartTime * 1000);
            var end = (int)Math.Round(chapter.EndTime * 1000);
            metadata.AppendLine(string.Format(template, start, end, chapter.Title));
        }

        return metadata.ToString();
    }
}