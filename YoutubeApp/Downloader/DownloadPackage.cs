using System.Collections.Generic;
using System.Threading;
using YoutubeApp.Media;
using YoutubeApp.Models;

namespace YoutubeApp.Downloader;

public class DownloadPackage
{
    public Download DownloadInfo { get; set; }
    public Format? CurrentPart { get; set; }
    public string CurrentPartFilepath { get; set; }
    public string CurrentPartFormatId { get; set; }
    public string CurrentPartFileIdPrefix { get; set; }
    public Dictionary<string, DownloadFile> FileStats { get; set; }
    public int ActiveFragmentCount { get; set; }
    public Queue<(int index, string filename, string filepath)>? RemainingFragments { get; set; }
    public long BytesLoaded { get; set; }
    public List<string> Gids { get; set; } = new();
    public int DownloadSpeed { get; set; }
    public int Connections { get; set; }
    public long PartCompletedLength { get; set; }
    public List<CancellationTokenSource> CancellationTokenSources { get; set; } = new();
    public bool IsStopped { get; set; }
}