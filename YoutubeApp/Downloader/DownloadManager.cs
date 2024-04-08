using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using YoutubeApp.Database;
using YoutubeApp.Exceptions;
using YoutubeApp.Media;
using YoutubeApp.Models;

namespace YoutubeApp.Downloader;

public class DownloadManager
{
    private readonly ILogger<DownloadManager> _logger;
    private readonly IAria2 _aria2;
    private readonly DownloadData _downloadData;
    private readonly Youtube _youtube;
    private readonly DownloaderUtils _downloaderUtils;
    private readonly Ffmpeg _ffmpeg;

    public ObservableCollection<Download> ActiveDownloads { get; } = new();
    private readonly Dictionary<string, ActiveFileDownload> _activeFileDownloads = new();
    private readonly Dictionary<int, DownloadPackage> _downloadPackages = new();

    private CancellationTokenSource? _progressUpdateCancellationTokenSource;
    private int _saveDownloadProgressSkipped;

    public DownloadManager(ILogger<DownloadManager> logger, IAria2 aria2, DownloadData downloadData, Youtube youtube,
        DownloaderUtils downloaderUtils, Ffmpeg ffmpeg)
    {
        _logger = logger;
        _aria2 = aria2;
        _downloadData = downloadData;
        _youtube = youtube;
        _downloaderUtils = downloaderUtils;
        _ffmpeg = ffmpeg;
    }

    // Events
    public static event EventHandler<DownloadCompletedEventArgs>? DownloadCompleted;
    public static event EventHandler<DownloadStoppedEventArgs>? DownloadStopped;
    public static event EventHandler<DownloadErrorEventArgs>? DownloadError;
    public static event EventHandler<ProgressUpdateEventArgs>? ProgressUpdate;


    public async Task<bool> InitializeAsync()
    {
        var running = _aria2.Run();
        if (running == false) return false;

        var connected = await _aria2.ConnectAsync(OnDownloadStart, OnDownloadStop, OnDownloadCompleteAsync,
            OnDownloadErrorAsync);
        return connected;
    }

    public async Task StartDownloadAsync(Download downloadInfo, DownloadPackage? downloadPackage = null)
    {
        if (downloadPackage is null)
        {
            var fileStats = _downloadData.GetDownloadFiles(downloadInfo.Id);
            downloadPackage = new DownloadPackage
            {
                DownloadInfo = downloadInfo,
                FileStats = fileStats
            };
            _downloadPackages[downloadInfo.Id] = downloadPackage;

            if (_progressUpdateCancellationTokenSource is null)
            {
                _progressUpdateCancellationTokenSource = new CancellationTokenSource();
                _ = UpdateProgressAsync(_progressUpdateCancellationTokenSource.Token);
            }
        }

        downloadPackage.BytesLoaded = 0;

        // Download video stream
        var vFormatId = downloadInfo.SelectedVariant.VFormatItagNoDash;
        var skip = await DownloadPartAsync(downloadPackage, vFormatId, "v");
        if (!skip) return;

        // Download audio stream
        var aFormatId = downloadInfo.SelectedVariant.AFormatItagNoDash;
        skip = await DownloadPartAsync(downloadPackage, aFormatId, "a");
        if (!skip) return;

        // Mux video and audio
        var cts = new CancellationTokenSource();
        downloadPackage.CancellationTokenSources.Add(cts);
        try
        {
            await MuxStreamsAsync(downloadInfo, vFormatId, aFormatId, cts.Token);
        }
        catch (OperationCanceledException)
        {
            _downloadPackages.Remove(downloadInfo.Id);
            return;
        }
        catch (MuxFailedException e)
        {
            _logger.LogError(e, "Mux failed {DownloadId}", downloadInfo.Id);
            _downloadPackages.Remove(downloadInfo.Id);
            downloadInfo.Enabled = false;
            _downloadData.UpdateDownloadEnabledState(new[] { downloadInfo.Id }, false);
            DownloadError?.Invoke(null,
                new DownloadErrorEventArgs
                {
                    Id = downloadInfo.Id,
                    Error = $"Mux Failed{(e.Reason is not null ? " (" + e.Reason + ")" : "")}"
                });
            return;
        }

        // Completed ✔
        _downloadPackages.Remove(downloadInfo.Id);
        _downloadData.SaveDownloadCompleted(downloadInfo.Id, downloadPackage.BytesLoaded);
        DownloadCompleted?.Invoke(null,
            new DownloadCompletedEventArgs { Id = downloadInfo.Id, BytesLoaded = downloadPackage.BytesLoaded });
    }

    private async Task<bool> DownloadPartAsync(DownloadPackage downloadPackage, string formatId, string fileIdPrefix)
    {
        var downloadInfo = downloadPackage.DownloadInfo;
        var fileStats = downloadPackage.FileStats;
        var part = downloadInfo.Formats[formatId];
        var partFilename = $"{downloadInfo.VideoId}_{downloadInfo.Uuid}_{formatId}.part";
        var partFilepath = Path.Combine(downloadInfo.SaveTo, partFilename);
        var filesize = fileStats.GetValueOrDefault(fileIdPrefix)?.Filesize;
        var fileInfo = new FileInfo(partFilepath);
        if (filesize is not null && fileInfo.Exists && fileInfo.Length == filesize)
        {
            downloadPackage.BytesLoaded += (long)filesize;
            return true;
        }

        // PartFile doesn't Exist
        downloadPackage.CurrentPart = part;
        downloadPackage.CurrentPartFilepath = partFilepath;
        downloadPackage.CurrentPartFormatId = formatId;
        downloadPackage.CurrentPartFileIdPrefix = fileIdPrefix;

        switch (part.Protocol)
        {
            case Protocol.Https:
                _logger.LogDebug("Starting part download {DownloadId} {Filename} {Protocol}", downloadInfo.Id,
                    partFilename, Protocol.Https);
                var gid = Utils.GenerateGid();
                _activeFileDownloads.Add(gid,
                    new ActiveFileDownload
                        { Package = downloadPackage, FileId = fileIdPrefix, Url = part.Url, Filename = partFilename });
                downloadPackage.Gids.Add(gid);
                await AddToDownloaderAsync(part.Url, downloadInfo.SaveTo, partFilename, gid);
                break;
            case Protocol.HttpDashSegments:
                // Check fragments
                downloadPackage.RemainingFragments = new();
                var i = 0;
                var fragmentsDirectory = Path.Combine(downloadInfo.SaveTo,
                    $"{downloadInfo.VideoId}_{downloadInfo.Uuid}_{formatId}");

                foreach (var fmt in part.Fragments)
                {
                    var fmtFilename = $"{downloadInfo.VideoId}_{downloadInfo.Uuid}_{formatId}_{i}.part";
                    var fmtFilepath = Path.Combine(fragmentsDirectory, fmtFilename);
                    var fmtFilesize = fileStats.GetValueOrDefault(fileIdPrefix + i)?.Filesize;
                    var fmtFileInfo = new FileInfo(fmtFilepath);

                    if (fmtFilesize is not null && fmtFileInfo.Exists && fmtFileInfo.Length == fmtFilesize)
                    {
                        downloadPackage.BytesLoaded += (long)fmtFilesize;
                    }
                    else
                    {
                        downloadPackage.RemainingFragments.Enqueue((i, fmtFilename, fmtFilepath));
                    }

                    i++;
                }

                _ = DownloadSegmentedPartAsync(downloadPackage);
                break;
            default:
                throw new Exception("Unknown protocol");
        }

        return false;
    }

    private async Task DownloadSegmentedPartAsync(DownloadPackage downloadPackage)
    {
        var part = downloadPackage.CurrentPart;
        while (downloadPackage.ActiveFragmentCount < Settings.MaxConnections
               && downloadPackage.RemainingFragments?.Count > 0)
        {
            downloadPackage.ActiveFragmentCount++;
            var (index, filename, _) = downloadPackage.RemainingFragments.Dequeue();
            var fmtPath = downloadPackage.CurrentPart!.Fragments![index].path;
            var url = $"{part.Url}{fmtPath}";
            _logger.LogDebug("Starting fragment download {DownloadId} {Filename} {Index}",
                downloadPackage.DownloadInfo.Id, filename, index);

            var gid = Utils.GenerateGid();
            _activeFileDownloads.Add(gid,
                new ActiveFileDownload
                {
                    Package = downloadPackage, FileId = downloadPackage.CurrentPartFileIdPrefix + index, Url = url,
                    Filename = filename
                });
            downloadPackage.Gids.Add(gid);
            var fragmentsDirectory = Path.Combine(downloadPackage.DownloadInfo.SaveTo,
                $"{downloadPackage.DownloadInfo.VideoId}_{downloadPackage.DownloadInfo.Uuid}_{downloadPackage.CurrentPartFormatId}");
            await AddToDownloaderAsync(url, fragmentsDirectory, filename, gid, true);
        }

        // Done
        if (downloadPackage.ActiveFragmentCount == 0
            && downloadPackage.RemainingFragments!.Count == 0)
        {
            _logger.LogDebug("All fragments have been downloaded {DownloadId} {Prefix}",
                downloadPackage.DownloadInfo.Id, downloadPackage.CurrentPartFileIdPrefix);
            var merged = MergeFragments(downloadPackage);
            if (!merged)
            {
                if (downloadPackage.IsStopped) return;
                _downloadPackages.Remove(downloadPackage.DownloadInfo.Id);
                DownloadError?.Invoke(null,
                    new DownloadErrorEventArgs { Id = downloadPackage.DownloadInfo.Id, Error = "Merge Failed" });
                return;
            }

            _ = StartDownloadAsync(downloadPackage.DownloadInfo, downloadPackage);
        }
    }

    private bool MergeFragments(DownloadPackage pkg)
    {
        _logger.LogDebug("Merging fragments {DownloadId} {Prefix}", pkg.DownloadInfo.Id, pkg.CurrentPartFileIdPrefix);
        var mergedFilepath = pkg.CurrentPartFilepath;
        try
        {
            File.Delete(mergedFilepath);
        }
        catch (Exception)
        {
            // ignored
        }

        // Check free space
        long requiredSpace = 0;
        foreach (var (id, fs) in pkg.FileStats)
        {
            if (id.StartsWith(pkg.CurrentPartFileIdPrefix))
            {
                requiredSpace += fs.Filesize;
            }
        }

        var hasEnoughSpace =
            _downloaderUtils.CheckAvailableFreeSpace(pkg.DownloadInfo.SaveTo, requiredSpace + 1024 * 1024);
        if (!hasEnoughSpace)
        {
            _logger.LogError("Merge: Not enough space {RequiredSpace}", Utils.FormatBytes(requiredSpace + 1024 * 1024));
            return false;
        }

        var fmtFiles = new List<string>();
        var fragmentsDirectory = Path.Combine(pkg.DownloadInfo.SaveTo,
            $"{pkg.DownloadInfo.VideoId}_{pkg.DownloadInfo.Uuid}_{pkg.CurrentPartFormatId}");

        using (var outStream = File.Create(mergedFilepath))
        {
            for (var i = 0; i < pkg.CurrentPart!.Fragments!.Length; i++)
            {
                var fmtFilename =
                    $"{pkg.DownloadInfo.VideoId}_{pkg.DownloadInfo.Uuid}_{pkg.CurrentPartFormatId}_{i}.part";
                var fmtFilepath = Path.Combine(fragmentsDirectory, fmtFilename);
                fmtFiles.Add(fmtFilepath);
                using var inStream = File.OpenRead(fmtFilepath);
                if (pkg.IsStopped) return false; // Cancelled
                inStream.CopyTo(outStream);
            }

            _downloadData.RemoveDownloadFiles(pkg.DownloadInfo.Id, pkg.CurrentPartFileIdPrefix);
            SaveFileStats(pkg, outStream.Length, pkg.CurrentPartFileIdPrefix);
        }

        // Delete fragment files
        foreach (var filepath in fmtFiles)
        {
            try
            {
                File.Delete(filepath);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Deleting fragment file failed {DownloadId} {Filepath}",
                    pkg.DownloadInfo.Id, filepath);
            }
        }

        try
        {
            Directory.Delete(fragmentsDirectory);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Deleting fragments directory failed {DownloadId} {Path}",
                pkg.DownloadInfo.Id, fragmentsDirectory);
        }

        return true;
    }

    private async Task MuxStreamsAsync(Download downloadInfo, string vFormatId, string aFormatId,
        CancellationToken cancellationToken)
    {
        downloadInfo.SetMuxing();

        var videoFilepath = Path.Combine(downloadInfo.SaveTo,
            $"{downloadInfo.VideoId}_{downloadInfo.Uuid}_{vFormatId}.part");
        var audioFilepath = Path.Combine(downloadInfo.SaveTo,
            $"{downloadInfo.VideoId}_{downloadInfo.Uuid}_{aFormatId}.part");
        var outputFilepath = Path.Combine(downloadInfo.SaveTo, downloadInfo.Filename);

        var streamsFilesize = new FileInfo(videoFilepath).Length + new FileInfo(audioFilepath).Length;
        var hasEnoughSpace = _downloaderUtils.CheckAvailableFreeSpace(outputFilepath, (long)(streamsFilesize * 1.05));
        if (!hasEnoughSpace)
            throw new MuxFailedException("No Space");

        string? metadataFilepath = null;
        if (downloadInfo.Chapters is not null)
        {
            var chapters = Utils.GenerateChapters(downloadInfo.Chapters);
            var metadataFilename = $"{downloadInfo.VideoId}_{downloadInfo.Uuid}_metadata";
            metadataFilepath = Path.Combine(downloadInfo.SaveTo, metadataFilename);
            await File.WriteAllTextAsync(metadataFilepath, chapters, cancellationToken);
        }

        await _ffmpeg.MuxAsync(videoFilepath, audioFilepath, outputFilepath, metadataFilepath, cancellationToken);

        // Compare sizes
        var muxedFilesize = new FileInfo(outputFilepath).Length;
        var muxedToStreamsPercent = (float)muxedFilesize / streamsFilesize * 100;
        _logger.LogInformation("MuxFileSizeRatio {MuxedToStreamsPercent}", muxedToStreamsPercent);

        // Delete stream files
        var filepaths = new[] { videoFilepath, audioFilepath, metadataFilepath };
        foreach (var filepath in filepaths)
        {
            try
            {
                if (filepath is not null) File.Delete(filepath);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Deleting stream file failed {DownloadId} {Filepath}", downloadInfo.Id, filepath);
            }
        }
    }

    private void SaveFileStats(DownloadPackage pkg, long filesize, string fileId)
    {
        var fileStat = pkg.FileStats.GetValueOrDefault(fileId);
        _downloadData.SaveDownloadFile(pkg.DownloadInfo.Id, fileId, filesize, fileStat is not null);
        if (fileStat is null)
        {
            pkg.FileStats.Add(fileId, new DownloadFile { Filesize = filesize });
        }
        else
        {
            pkg.FileStats[fileId].Filesize = filesize;
        }
    }

    private async Task<string> AddToDownloaderAsync(string url, string saveTo, string filename, string gid,
        bool singleConnection = false)
    {
        var returnedGid = await _aria2.AddUriAsync(url, saveTo, filename, gid, singleConnection);
        return returnedGid;
    }

    public void StopDownload(int downloadId)
    {
        var downloadPackage = _downloadPackages[downloadId];
        downloadPackage.IsStopped = true;

        foreach (var cts in downloadPackage.CancellationTokenSources)
        {
            cts.Cancel();
            cts.Dispose();
        }

        downloadPackage.CancellationTokenSources.Clear();

        if (downloadPackage.Gids.Count == 0)
        {
            DownloadStopped?.Invoke(null, new() { Id = downloadId });
            _downloadPackages.Remove(downloadId);
        }

        foreach (var gid in downloadPackage.Gids.ToList())
        {
            _ = _aria2.RemoveAsync(gid);
        }

        _downloadData.SaveDownloadProgress(downloadId, downloadPackage.DownloadInfo.BytesLoaded);
    }


    // Update Progress
    //=================
    private async Task UpdateProgressAsync(CancellationToken cancellationToken)
    {
        while (_downloadPackages.Count > 0)
        {
            var ariaActiveDownloads = await _aria2.TellActiveAsync();
            _logger.LogTrace("AriaActiveDownload:{AriaActiveDownloadCount}", ariaActiveDownloads.Length);

            foreach (var adl in ariaActiveDownloads)
            {
                var downloadPackage = _activeFileDownloads[adl.gid].Package;
                switch (downloadPackage.CurrentPart.Protocol)
                {
                    case Protocol.Https:
                        downloadPackage.DownloadSpeed = int.Parse(adl.downloadSpeed);
                        downloadPackage.PartCompletedLength = long.Parse(adl.completedLength);
                        downloadPackage.Connections = int.Parse(adl.connections);
                        break;
                    case Protocol.HttpDashSegments:
                        downloadPackage.DownloadSpeed += int.Parse(adl.downloadSpeed);
                        downloadPackage.Connections += int.Parse(adl.connections);
                        break;
                    default:
                        throw new Exception("Unknown protocol");
                }
            }

            var totalSpeed = 0;
            foreach (var (_, pkg) in _downloadPackages)
            {
                if (pkg.PartCompletedLength == 0 && pkg.CurrentPart?.Protocol != Protocol.HttpDashSegments)
                    continue;

                pkg.DownloadInfo.Speed = pkg.DownloadSpeed;
                pkg.DownloadInfo.BytesLoaded = pkg.BytesLoaded + pkg.PartCompletedLength;
                pkg.DownloadInfo.Connections = pkg.Connections;
                totalSpeed += pkg.DownloadSpeed;

                pkg.Connections = 0;
                pkg.DownloadSpeed = 0;

                _logger.LogTrace(
                    "Progress update, ID:{DownloadId} Loaded:{BytesLoaded} Connections:{Connections} Speed:{Speed}",
                    pkg.DownloadInfo.Id, pkg.DownloadInfo.BytesLoaded, pkg.DownloadInfo.Connections,
                    pkg.DownloadInfo.Speed);

                // Save download progress every 60sec
                if (_saveDownloadProgressSkipped == 30)
                {
                    _downloadData.SaveDownloadProgress(pkg.DownloadInfo.Id, pkg.DownloadInfo.BytesLoaded);
                    _saveDownloadProgressSkipped = 0;
                    _logger.LogTrace("Download progress saved");
                }
                else
                {
                    _saveDownloadProgressSkipped++;
                }
            }

            ProgressUpdate?.Invoke(this, new ProgressUpdateEventArgs { Speed = totalSpeed });

            await Task.Delay(2000, cancellationToken);
        }

        _progressUpdateCancellationTokenSource = null;
    }


    // Event Handlers
    //===================

    // Download Start
    private void OnDownloadStart(AriaNotificationArgs eventArgs)
    {
        var gid = eventArgs.gid;
        _logger.LogDebug("Aria2 event: Start {Gid}", gid);
    }

    // Download Stop
    private void OnDownloadStop(AriaNotificationArgs eventArgs)
    {
        var gid = eventArgs.gid;
        _logger.LogDebug("Aria2 event: Stop {Gid}", gid);

        var gidInfo = _activeFileDownloads[gid];
        var downloadPackage = gidInfo.Package;

        _activeFileDownloads.Remove(gid);
        downloadPackage.Gids.Remove(gid);

        switch (downloadPackage.CurrentPart.Protocol)
        {
            case Protocol.Https:
                _downloadPackages.Remove(downloadPackage.DownloadInfo.Id);
                DownloadStopped?.Invoke(null, new() { Id = downloadPackage.DownloadInfo.Id });
                break;
            case Protocol.HttpDashSegments:
                downloadPackage.ActiveFragmentCount--;
                if (downloadPackage.Gids.Count == 0)
                {
                    _downloadPackages.Remove(downloadPackage.DownloadInfo.Id);
                    DownloadStopped?.Invoke(null, new() { Id = downloadPackage.DownloadInfo.Id });
                }

                break;
            default:
                throw new Exception("Unknown protocol");
        }
    }

    // Download Complete
    private async Task OnDownloadCompleteAsync(AriaNotificationArgs eventArgs)
    {
        var gid = eventArgs.gid;

        var gidInfo = _activeFileDownloads[gid];
        var downloadPackage = gidInfo.Package;
        var fileId = gidInfo.FileId;
        var status = await _aria2.TellStatusAsync(gid);
        downloadPackage.BytesLoaded += status.completedLength;
        downloadPackage.PartCompletedLength = 0;

        SaveFileStats(downloadPackage, status.completedLength, fileId);

        switch (downloadPackage.CurrentPart!.Protocol)
        {
            case Protocol.Https:
                _logger.LogDebug("Aria2 event: Complete {Gid} {FileId} {Protocol}", gid, fileId, Protocol.Https);
                if (downloadPackage.IsStopped) return;
                _ = StartDownloadAsync(downloadPackage.DownloadInfo, downloadPackage);
                break;
            case Protocol.HttpDashSegments:
                _logger.LogDebug("Aria2 event: Complete {Gid} {FileId} {Protocol}", gid, fileId,
                    Protocol.HttpDashSegments);
                downloadPackage.ActiveFragmentCount--;
                if (downloadPackage.IsStopped) return;
                _ = DownloadSegmentedPartAsync(downloadPackage);
                break;
            default:
                throw new Exception("Unknown protocol");
        }

        _activeFileDownloads.Remove(gid);
        downloadPackage.Gids.Remove(gid);
    }

    // Download Error
    private async Task OnDownloadErrorAsync(AriaNotificationArgs eventArgs)
    {
        var gid = eventArgs.gid;

        var gidInfo = _activeFileDownloads[gid];
        var downloadPackage = gidInfo.Package;
        var status = await _aria2.TellStatusAsync(gid);

        _logger.LogError("Aria2 event: Error {Gid} {FileId} {ErrorCode} {ErrorMessage}",
            gid, gidInfo.FileId, status.errorCode, status.errorMessage);

        _activeFileDownloads.Remove(gid);
        downloadPackage.Gids.Remove(gid);

        if (downloadPackage.IsStopped) return;

        var downloadInfo = downloadPackage.DownloadInfo;

        if (status.errorCode == "22" && status.errorMessage.Contains("status=403")
            || status.errorCode == "3")
        {
            // 🚫 Expired
            downloadPackage.IsStopped = true;

            _logger.LogDebug("Link has expired {DownloadId} {Gid} {FileId}", downloadInfo.Id, gid, gidInfo.FileId);

            _downloadPackages.Remove(downloadInfo.Id);

            downloadInfo.SetRefreshing(true);
            var success = await _youtube.RefreshVideoInfoAsync(downloadInfo);
            downloadInfo.SetRefreshing(false);
            if (downloadInfo.RefreshCancellationTokenSource.IsCancellationRequested)
            {
                DownloadStopped?.Invoke(null, new DownloadStoppedEventArgs { Id = downloadInfo.Id });
                return;
            }

            if (success && downloadInfo.Enabled)
            {
                _ = StartDownloadAsync(downloadInfo);
            }
            else
            {
                DownloadError?.Invoke(null,
                    new DownloadErrorEventArgs { Id = downloadInfo.Id, Error = "Refresh Failed" });
            }

            return;
        }

        switch (status.errorCode)
        {
            case "1":
                if (status.errorMessage == "Download aborted.")
                {
                    var controlFilePath = Path.Combine(downloadInfo.SaveTo, gidInfo.Filename + ".aria2");
                    var controlFileInfo = new FileInfo(controlFilePath);
                    if (controlFileInfo is { Exists: true, Length: 0 })
                    {
                        try
                        {
                            controlFileInfo.Delete();
                            goto case "2";
                        }
                        catch (Exception)
                        {
                            // ignored
                        }
                    }

                    goto default;
                }

                goto case "2";
            case "2":
            case "6":
            case "19":
            case "22":
            case "23":
            case "24":
            case "29":
                // ⏳ Retry
                _logger.LogDebug("Retry in 10 seconds {DownloadId} {Gid} {FileId}",
                    downloadInfo.Id, gid, gidInfo.FileId);
                var cts = new CancellationTokenSource();
                downloadPackage.CancellationTokenSources.Add(cts);

                _ = Task.Run(async () =>
                {
                    await Task.Delay(10000, cts.Token);
                    downloadPackage.CancellationTokenSources.Remove(cts);
                    cts.Dispose();

                    if (downloadPackage.IsStopped) return;

                    _logger.LogDebug("Retrying {DownloadId} {Gid} {FileId}",
                        downloadInfo.Id, gid, gidInfo.FileId);
                    var newGid = Utils.GenerateGid();
                    _activeFileDownloads.Add(newGid, gidInfo);
                    downloadPackage.Gids.Add(newGid);
                    await AddToDownloaderAsync(gidInfo.Url, downloadInfo.SaveTo,
                        gidInfo.Filename, newGid,
                        gidInfo.Package.CurrentPart!.Protocol == Protocol.HttpDashSegments);
                }, cts.Token);
                break;
            case "9":
                // Not enough space
                downloadPackage.IsStopped = true;
                _downloadPackages.Remove(downloadInfo.Id);
                downloadInfo.Enabled = false;
                _downloadData.UpdateDownloadEnabledState(new[] { downloadInfo.Id }, false);
                DownloadError?.Invoke(null,
                    new DownloadErrorEventArgs { Id = downloadPackage.DownloadInfo.Id, Error = "No Space" });
                break;
            default:
                // Unexpected
                downloadPackage.IsStopped = true;
                _downloadPackages.Remove(downloadInfo.Id);
                downloadInfo.Enabled = false;
                _downloadData.UpdateDownloadEnabledState(new[] { downloadInfo.Id }, false);
                DownloadError?.Invoke(null,
                    new DownloadErrorEventArgs { Id = downloadPackage.DownloadInfo.Id, Error = "Unknown Error" });
                break;
        }
    }
}