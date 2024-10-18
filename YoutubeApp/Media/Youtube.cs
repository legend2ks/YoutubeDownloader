using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NanoidDotNet;
using YoutubeApp.Comparers;
using YoutubeApp.Database;
using YoutubeApp.Downloader;
using YoutubeApp.Exceptions;
using YoutubeApp.Models;

namespace YoutubeApp.Media;

public class Youtube
{
    private readonly ILogger<Youtube> _logger;
    private readonly IYoutubeCommunicator _youtubeCommunicator;
    private readonly DownloadData _downloadData;
    private readonly ChannelData _channelData;
    private readonly DownloaderUtils _downloaderUtils;
    private readonly Settings _settings;

    public Youtube(ILogger<Youtube> logger, IYoutubeCommunicator youtubeCommunicator, DownloadData downloadData,
        ChannelData channelData, DownloaderUtils downloaderUtils, Settings settings)
    {
        _logger = logger;
        _youtubeCommunicator = youtubeCommunicator;
        _downloadData = downloadData;
        _channelData = channelData;
        _downloaderUtils = downloaderUtils;
        _settings = settings;
    }

    public static event EventHandler<VideoFoundEventArgs>? VideoFound;
    public static event EventHandler<RefreshFinishedEventArgs>? RefreshFinished;

    /// <summary>
    /// Get videos asynchronously
    /// </summary>
    /// <param name="job"></param>
    /// <param name="fetchPlaylists"></param>
    public async Task GetVideosAsync(GrabberJob job, bool fetchPlaylists = true)
    {
        _logger.LogDebug("Getting videos JobId: {JobId}", job.Id);

        if (fetchPlaylists)
        {
            if (!await GetPlaylistInfoAsync(job)) return;
        }

        // Get Video Information
        foreach (var (_, playlist) in job.Playlists)
        {
            foreach (var video in playlist.Videos)
            {
                if (video.Status != VideoStatus.Waiting)
                    continue;

                job.SetVideoStatus(video, VideoStatus.Fetching);

                try
                {
                    var (videoInfo, missingFormats) =
                        await _youtubeCommunicator.GetVideoInfoAsync(video.VideoId, 3, false, job.CancellationToken);

                    // Live status
                    if (videoInfo.live_status != "not_live" && videoInfo.live_status != "was_live")
                    {
                        _logger.LogError("Live status is not acceptable: {Status} JobId:{JobId}",
                            videoInfo.live_status, job.Id);
                        video.Title = videoInfo.title;
                        job.SetVideoStatus(video, VideoStatus.Error, "Is Live");
                        continue;
                    }

                    var formats = ProcessFormats(videoInfo.formats);
                    var variants = GenerateVariants(videoInfo.formats);
                    var chapters = ProcessChapters(videoInfo.chapters);

                    var bestVariant = variants[0];
                    var vformat = formats[bestVariant.VFormatId];
                    var aformat = formats[bestVariant.AFormatId];

                    var selectedVariant = new SelectedVariant
                    {
                        Id = 0,
                        VFormatId = bestVariant.VFormatId,
                        AFormatId = bestVariant.AFormatId,
                        VFormatItagNoDash = bestVariant.VFormatId.Replace("-dash", ""),
                        AFormatItagNoDash = bestVariant.AFormatId.Replace("-dash", ""),
                        VFormatProtocol = vformat.Protocol,
                        AFormatProtocol = aformat.Protocol,
                        VFormatThrottled = vformat.Throttled,
                        AFormatThrottled = aformat.Throttled,
                        VideoLmt = Utils.ExtractLmt(vformat.Url),
                        AudioLmt = Utils.ExtractLmt(aformat.Url),
                        Description = Utils.GenerateVariantDescription(bestVariant, vformat.Protocol, aformat.Protocol,
                            vformat.Throttled, aformat.Throttled),
                        IsApproxFilesize = bestVariant.IsApproxFilesize,
                        VCodec = bestVariant.VCodec,
                        ACodec = bestVariant.ACodec,
                        Width = bestVariant.Width,
                        Height = bestVariant.Height,
                        Fps = bestVariant.Fps,
                        Abr = bestVariant.Abr,
                    };

                    var container = GetContainerOptions(bestVariant.VCodec, bestVariant.ACodec)[0];

                    var uploadDate = videoInfo.upload_date.Insert(4, "-").Insert(7, "-");

                    var channel = job.Channel ?? _channelData.GetChannels()
                        .FirstOrDefault(x => x.UniqueId == videoInfo.channel_id);

                    var isChannelVideo = Utils.IsSamePath(channel?.Path, job.SavePath);
                    var filenameTemplate =
                        isChannelVideo ? Settings.DefaultFilenameTemplate : _settings.FilenameTemplate;
                    var filename = GenerateFilename(filenameTemplate, video.VideoId, videoInfo.title,
                        container,
                        bestVariant.Fps, videoInfo.channel, uploadDate, bestVariant.Width, bestVariant.Height,
                        bestVariant.VCodec, bestVariant.ACodec, bestVariant.Abr);

                    var uuid = await Nanoid.GenerateAsync();

                    // Save to Database
                    var download = new Download
                    {
                        VideoId = video.VideoId,
                        Uuid = uuid,
                        ChannelId = videoInfo.channel_id,
                        Channel = channel,
                        Title = videoInfo.title,
                        SelectedVariant = selectedVariant,
                        Container = container,
                        Variants = variants,
                        Formats = formats,
                        Chapters = chapters,
                        Duration = videoInfo.duration_string,
                        Filename = filename,
                        SaveTo = job.SavePath,
                        ChannelTitle = videoInfo.channel,
                        UploadDate = uploadDate,
                        Filesize = bestVariant.Filesize,
                        BytesLoaded = 0,
                        MissingFormats = missingFormats,
                    };

                    if (job.CancellationToken.IsCancellationRequested) return;
                    _downloadData.AddDownload(download);

                    // Update UI
                    video.Title = videoInfo.title;
                    job.SetVideoStatus(video, VideoStatus.Done);
                    VideoFound?.Invoke(null, new VideoFoundEventArgs { JobId = job.Id, DownloadItem = download });
                }
                catch (VideoNotAvailableException e)
                {
                    job.SetVideoStatus(video, VideoStatus.Error, e.Reason, e.ErrorMessage);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Failed to get video info. JodId:{JobId} VideoId:{VideoId}",
                        job.Id, video.VideoId);
                    job.SetVideoStatus(video, VideoStatus.Error);
                }
            }
        }

        job.SetJobFinished();
    }

    /// <summary>
    /// Get playlist information for grabber job asynchronously
    /// </summary>
    /// <param name="job"></param>
    /// <returns></returns>
    private async Task<bool> GetPlaylistInfoAsync(GrabberJob job)
    {
        var videosIds = new Dictionary<string, GrabberJobVideo>();
        var playlists = new Dictionary<string, GrabberJobPlaylist>();
        var duplicateCount = 0;
        foreach (var playlistId in job.PlaylistIds)
        {
            PlaylistInfo playlistInfo;
            try
            {
                playlistInfo = await _youtubeCommunicator.GetPlaylistInfoAsync(playlistId, job.CancellationToken);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to get playlist info. JodId:{JobId} PlaylistId:{PlaylistId}",
                    job.Id, playlistId);
                job.SetFailed();
                return false;
            }

            var playlist = new GrabberJobPlaylist { Title = playlistInfo.title };

            foreach (var videoInfo in playlistInfo.entries)
            {
                var video = new GrabberJobVideo
                {
                    VideoId = videoInfo.id,
                    Title = videoInfo.title,
                };
                var alreadyExists = !videosIds.TryAdd(videoInfo.id, video);
                if (alreadyExists)
                {
                    job.SetVideoStatus(video, VideoStatus.Duplicate);
                    duplicateCount++;
                }
                else
                {
                    job.SetVideoStatus(video, VideoStatus.Waiting);
                }

                playlist.Videos.Add(video);
            }

            playlists.Add(playlistId, playlist);
        }

        // Add separate videos
        var withoutPlaylist = new GrabberJobPlaylist { Title = "- No Playlist -" };

        foreach (var videoId in job.VideoIds)
        {
            var video = new GrabberJobVideo
            {
                VideoId = videoId,
            };
            var alreadyExists = !videosIds.TryAdd(videoId, video);
            if (alreadyExists)
            {
                video.Title = videosIds[videoId].Title;
                job.SetVideoStatus(video, VideoStatus.Duplicate);
                duplicateCount++;
            }
            else
            {
                video.Title = $"[{videoId}]";
                job.SetVideoStatus(video, VideoStatus.Waiting);
            }

            withoutPlaylist.Videos.Add(video);
        }

        if (withoutPlaylist.Videos.Count > 0)
            playlists.Add("-", withoutPlaylist);

        job.SetTotalVideoCount(videosIds.Count, duplicateCount);
        job.Playlists = playlists;
        return true;
    }


    /// <summary>
    /// Refresh video information asynchronously
    /// </summary>
    /// <param name="downloads"></param>
    public async Task RefreshVideoInfoAsync(Download[] downloads)
    {
        foreach (var dl in downloads)
        {
            dl.SetRefreshing(true);
        }

        foreach (var dl in downloads)
        {
            var success = await RefreshVideoInfoAsync(dl);
            RefreshFinished?.Invoke(null, new RefreshFinishedEventArgs { Download = dl });
            if (!success)
            {
                dl.SetFailed("Refresh Failed");
            }

            dl.SetRefreshing(false);
        }
    }

    public async Task<bool> RefreshVideoInfoAsync(Download dl)
    {
        dl.RefreshCancellationTokenSource = new CancellationTokenSource();
        Dictionary<string, Format> formats;
        List<Variant> variants;
        List<Chapter>? chapters;
        SelectedVariant? selectedVariant = null;

        var retriesLeft = 3;
        VideoInfo? videoInfo;
        bool missingFormats;

        while (true)
        {
            try
            {
                (videoInfo, missingFormats) = await _youtubeCommunicator.GetVideoInfoAsync(dl.VideoId,
                    dl.Downloading ? -1 : 3, !dl.Downloading, dl.RefreshCancellationTokenSource.Token);
            }
            catch (VideoNotAvailableException e)
            {
                _logger.LogError("Failed to get video info: {Error} {DownloadId}", e.Reason, dl.Id);
                return false;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to get video info. DownloadId:{DownloadId} VideoId:{VideoId}",
                    dl.Id, dl.VideoId);
                return false;
            }

            formats = ProcessFormats(videoInfo.formats);
            variants = GenerateVariants(videoInfo.formats);
            chapters = ProcessChapters(videoInfo.chapters);

            // Find currently selected variant
            var prevVideoFormatId = dl.SelectedVariant.VFormatItagNoDash;
            var prevAudioFormatId = dl.SelectedVariant.AFormatItagNoDash;

            foreach (var variant in variants)
            {
                if (variant.VFormatId != prevVideoFormatId
                    || variant.AFormatId != prevAudioFormatId) continue;

                var videoFormatId = variant.VFormatId;
                var audioFormatId = variant.AFormatId;
                var sameVideoProtocol = formats[videoFormatId].Protocol == dl.SelectedVariant.VFormatProtocol;
                var sameAudioProtocol = formats[audioFormatId].Protocol == dl.SelectedVariant.AFormatProtocol;

                // Change itag to -dash if protocol has changed
                if (!sameVideoProtocol)
                {
                    if (dl.SelectedVariant.VFormatProtocol != Protocol.HttpDashSegments)
                        break;
                    if (!formats.ContainsKey($"{dl.SelectedVariant.VFormatId}-dash"))
                        break;
                    videoFormatId = $"{dl.SelectedVariant.VFormatId}-dash";
                }

                if (!sameAudioProtocol)
                {
                    if (dl.SelectedVariant.AFormatProtocol != Protocol.HttpDashSegments)
                        break;
                    if (!formats.ContainsKey($"{dl.SelectedVariant.AFormatId}-dash"))
                        break;
                    audioFormatId = $"{dl.SelectedVariant.AFormatId}-dash";
                }

                var vformat = formats[videoFormatId];
                var aformat = formats[audioFormatId];

                selectedVariant = new SelectedVariant
                {
                    Id = variant.Id,
                    VFormatId = videoFormatId,
                    AFormatId = audioFormatId,
                    VFormatItagNoDash = dl.SelectedVariant.VFormatId,
                    AFormatItagNoDash = dl.SelectedVariant.AFormatId,
                    VFormatProtocol = vformat.Protocol,
                    AFormatProtocol = aformat.Protocol,
                    VFormatThrottled = vformat.Throttled,
                    AFormatThrottled = aformat.Throttled,
                    VideoLmt = Utils.ExtractLmt(vformat.Url),
                    AudioLmt = Utils.ExtractLmt(aformat.Url),
                    Description = Utils.GenerateVariantDescription(variant, vformat.Protocol, aformat.Protocol,
                        vformat.Throttled, aformat.Throttled),
                    IsApproxFilesize = variant.IsApproxFilesize,
                    VCodec = variant.VCodec,
                    ACodec = variant.ACodec,
                    Width = variant.Width,
                    Height = variant.Height,
                    Fps = variant.Fps,
                    Abr = variant.Abr,
                };
                break;
            }

            if (selectedVariant is null)
            {
                retriesLeft--;
                if (retriesLeft == 0)
                    break;
                _logger.LogDebug("Previous variant not found. Retrying... ({RetriesLeft} left) {DownloadId}",
                    retriesLeft, dl.Id);
                try
                {
                    await Task.Delay(1000, dl.RefreshCancellationTokenSource.Token);
                }
                catch (Exception)
                {
                    return false;
                }

                continue;
            }

            break;
        }

        if (selectedVariant is null)
        {
            _logger.LogDebug("Previous variant is not available {DownloadId}", dl.Id);

            selectedVariant = dl.SelectedVariant;
            selectedVariant.Id = -1;
            dl.Enabled = false;
        }

        // Compare the last modification time
        if (selectedVariant.Id != -1)
        {
            var videoHasSameLmt = dl.SelectedVariant.VideoLmt == selectedVariant.VideoLmt;
            if (videoHasSameLmt == false)
            {
                _downloaderUtils.DeleteTempFiles(dl, "v");
            }

            var audioHasSameLmt = dl.SelectedVariant.AudioLmt == selectedVariant.AudioLmt;
            if (audioHasSameLmt == false)
            {
                _downloaderUtils.DeleteTempFiles(dl, "a");
            }
        }

        var uploadDate = videoInfo.upload_date.Insert(4, "-").Insert(7, "-");

        // Save to Database
        _downloadData.UpdateDownload(dl.Id, videoInfo.title, selectedVariant, variants, formats, chapters,
            videoInfo.duration_string, missingFormats,
            selectedVariant.Id != -1 ? variants[selectedVariant.Id].Filesize : dl.Filesize, videoInfo.channel_id,
            videoInfo.channel, uploadDate, dl.Enabled);

        // Update UI
        dl.Title = videoInfo.title;
        dl.Variants = variants;
        dl.Formats = formats;
        dl.Chapters = chapters;
        _downloaderUtils.DeleteUselessFiles(dl, selectedVariant);
        dl.ChangeSelectedVariant(selectedVariant);
        dl.Duration = videoInfo.duration_string;
        dl.Filesize = selectedVariant.Id != -1 ? variants[selectedVariant.Id].Filesize : dl.Filesize;
        dl.ChannelId = videoInfo.channel_id;
        dl.ChannelTitle = videoInfo.channel;
        dl.UploadDate = uploadDate;
        dl.MissingFormats = missingFormats;

        return true;
    }


    /// <summary>
    /// Generate variants from formats
    /// </summary>
    /// <param name="formats"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    private List<Variant> GenerateVariants(IEnumerable<VideoInfoFormat> formats)
    {
        var variants = new List<Variant>();
        var videoOnly = new List<VideoInfoFormat>();
        var audioOnly = new List<VideoInfoFormat>();

        foreach (var format in formats)
        {
            if (format.protocol != Protocol.Https && format.protocol != Protocol.HttpDashSegments)
            {
                _logger.LogDebug("Ignoring unsupported protocol. Protocol: {Protocol}, FormatId: {FormatId}",
                    format.protocol, format.format_id);
                continue;
            }

            if (format.format_id.Contains("-dash"))
            {
                _logger.LogDebug("Ignoring format (-DASH): {FormatId}", format.format_id);
                continue;
            }

            if (format.format_id.Contains("-drc"))
            {
                _logger.LogDebug("Ignoring format (-DRC)  {FormatId}", format.format_id);
                continue;
            }

            if (format.format_id.Contains('-') && format.format_note is not null &&
                !format.format_note.Contains("(default)"))
            {
                _logger.LogDebug("Ignoring format (Non-default language): {FormatId}", format.format_id);
                continue;
            }

            if ((format.filesize ?? format.filesize_approx) is null)
            {
                _logger.LogWarning("Filesize is not available! FormatId: {FormatId}", format.format_id);
                continue;
            }

            var hasVideo = format.vcodec is not null && format.vcodec != "none";
            var hasAudio = format.acodec is not null && format.acodec != "none";

            if (hasVideo && !hasAudio)
            {
                // Video format
                format.vcodec = format.vcodec!.Split(".")[0];
                videoOnly.Add(format);
            }
            else if (!hasVideo && hasAudio)
            {
                // Audio format
                format.acodec = format.acodec!.Split(".")[0];
                audioOnly.Add(format);
            }
            else if (hasVideo && hasAudio)
            {
                // TODO Video + Audio
            }
        }

        foreach (var videoFormat in videoOnly)
        {
            foreach (var audioFormat in audioOnly)
            {
                if (videoFormat.vcodec == "vp9" && audioFormat.acodec is "opus" or "mp4a" ||
                    videoFormat.vcodec == "avc1" && audioFormat.acodec is "opus" or "mp4a" ||
                    videoFormat.vcodec == "av01" && audioFormat.acodec is "opus" or "mp4a")
                {
                    var videoFileSize = videoFormat.filesize ?? videoFormat.filesize_approx;
                    var audioFileSize = audioFormat.filesize ?? audioFormat.filesize_approx;
                    var filesize = videoFileSize + audioFileSize;

                    variants.Add(new Variant
                    {
                        VFormatId = videoFormat.format_id,
                        VCodec = videoFormat.vcodec,
                        Width = (int)videoFormat.width,
                        Height = (int)videoFormat.height,
                        Fps = (float)videoFormat.fps,
                        Vbr = (float)videoFormat.vbr,
                        AFormatId = audioFormat.format_id,
                        ACodec = audioFormat.acodec,
                        Abr = (float)audioFormat.abr,
                        Filesize = (long)filesize,
                        IsApproxFilesize = videoFormat.filesize is null || audioFormat.filesize is null,
                    });
                }
                else
                {
                    _logger.LogDebug("Unsupported Variant: {VideoCodec}({VideoFormatId})+{AudioCodec}({AudioFormatId})",
                        videoFormat.vcodec, videoFormat.format_id, audioFormat.acodec, audioFormat.format_id);
                }
            }
        }

        variants.Sort(new VariantComparer());

        var i = 0;
        foreach (var v in variants)
        {
            v.Id = i;
            i++;
        }

        return variants;
    }

    /// <summary>
    /// Process formats
    /// </summary>
    /// <param name="infoFormats"></param>
    /// <returns></returns>
    private Dictionary<string, Format> ProcessFormats(IEnumerable<VideoInfoFormat> infoFormats)
    {
        var formats = new Dictionary<string, Format>();
        foreach (var format in infoFormats)
        {
            // Ignore non default audios
            if (format.format_id
                    .Replace("-dash", "")
                    .Replace("-drc", "")
                    .Contains('-') && format.format_note is not null && !format.format_note.Contains("(default)"))
            {
                _logger.LogDebug("Ignoring format (Non-default language): {FormatId}", format.format_id);
                continue;
            }

            var throttled = format.format_note is not null && format.format_note.Contains("THROTTLED");
            if (throttled)
            {
                _logger.LogDebug("Throttled format: {FormatId}", format.format_id);
            }

            // Incorrect protocol workaround
            if (format.protocol == Protocol.HttpDashSegments && format.fragment_base_url is null)
            {
                format.protocol = Protocol.Https;
                _logger.LogWarning("Incorrect Protocol. Changing http_dash_segments to https.");
            }

            // 
            if (format.vcodec is not null && format.vcodec.StartsWith("vp09"))
                format.vcodec = "vp9";

            formats.Add(format.format_id, format.protocol switch
            {
                Protocol.Https => new Format { Protocol = format.protocol, Url = format.url, Throttled = throttled },
                Protocol.HttpDashSegments => new Format
                {
                    Protocol = format.protocol, Url = format.fragment_base_url, Fragments = format.fragments,
                    Throttled = throttled
                },
                _ => new Format { Protocol = format.protocol, Throttled = throttled },
            });
        }

        return formats;
    }

    private static List<Chapter>? ProcessChapters(VideoInfoChapter[]? infoChapters)
    {
        if (infoChapters is null) return null;

        var chapters = new List<Chapter>();
        foreach (var chapter in infoChapters)
        {
            if (string.IsNullOrEmpty(chapter.title) || chapter.start_time < 0 || chapter.end_time < 0)
                continue;
            chapters.Add(new Chapter
            {
                Title = chapter.title,
                StartTime = chapter.start_time,
                EndTime = chapter.end_time,
            });
        }

        return chapters.Count == 0 ? null : chapters;
    }

    /// <summary>
    /// Get supported container formats for codecs
    /// </summary>
    /// <param name="vcodec"></param>
    /// <param name="acodec"></param>
    /// <returns></returns>
    public static string[] GetContainerOptions(string vcodec, string acodec)
    {
        var containers = new[]
        {
            new
            {
                name = "mp4",
                vcodecs = new[] { "vp9", "avc1", "av01" },
                acodecs = new[] { "mp4a", "opus" }
            },
            new
            {
                name = "mkv",
                vcodecs = new[] { "vp9", "avc1" },
                acodecs = new[] { "mp4a", "opus" }
            },
            new
            {
                name = "webm",
                vcodecs = new[] { "vp9" },
                acodecs = new[] { "opus" }
            }
        };

        var options = new List<string>();
        foreach (var container in containers)
        {
            if (!container.vcodecs.Contains(vcodec) || !container.acodecs.Contains(acodec)) continue;
            options.Add(container.name);
        }

        return options.ToArray();
    }

    public static string GenerateFilename(string template, string videoId, string title, string container, float fps,
        string channelTitle, string uploadDate, int width, int height, string videoCodec, string audioCodec,
        float audioBitrate)
    {
        return SanitizeFilename(
            template.Replace("*TITLE*", title)
                .Replace("*VIDEO_ID*", videoId)
                .Replace("*CHANNEL*", channelTitle)
                .Replace("*UPYEAR*", uploadDate.Split("-")[0])
                .Replace("*UPMONTH*", uploadDate.Split("-")[1])
                .Replace("*UPDAY*", uploadDate.Split("-")[2])
                .Replace("*W*", width.ToString())
                .Replace("*H*", height.ToString())
                .Replace("*FPS*", fps.ToString(CultureInfo.InvariantCulture))
                .Replace("*VIDEO_CODEC*", videoCodec)
                .Replace("*AUDIO_CODEC*", audioCodec)
                .Replace("*AUDIO_BITRATE*", audioBitrate.ToString(CultureInfo.InvariantCulture))
            + $".{container}", '_');
    }

    public static string SanitizeFilename(string filename, char replaceChar = ' ')
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        foreach (var c in invalidChars)
        {
            filename = filename.Replace(c, replaceChar);
        }

        return RemoveExtraWhiteSpaces(filename);
    }

    private static string RemoveExtraWhiteSpaces(string text)
    {
        var inspaces = false;
        var builder = new StringBuilder(text.Length);

        foreach (var c in text)
        {
            if (inspaces)
            {
                if (c == ' ') continue;
                inspaces = false;
                builder.Append(c);
            }
            else if (c == ' ')
            {
                inspaces = true;
                builder.Append(' ');
            }
            else
                builder.Append(c);
        }

        return builder.ToString();
    }
}