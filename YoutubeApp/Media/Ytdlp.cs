using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.Buffered;
using FluentValidation;
using Microsoft.Extensions.Logging;
using YoutubeApp.Exceptions;
using YoutubeApp.Validators;

namespace YoutubeApp.Media;

internal class Ytdlp : IYoutubeCommunicator
{
    private readonly ILogger<Ytdlp> _logger;
    private const string YtdlpBinaryPath = @"./utils/yt-dlp.exe";

    public Ytdlp(ILogger<Ytdlp> logger)
    {
        _logger = logger;
    }

    public async Task<VideoInfo> GetVideoInfoAsync(string videoId, int retries, bool useTimeout,
        CancellationToken cancellationToken)
    {
        var retriesLeft = retries;
        var infiniteRetries = retries == -1;
        BufferedCommandResult? result = null;

        while (true)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (useTimeout)
            {
                cts.CancelAfter(TimeSpan.FromSeconds(20));
            }

            try
            {
                result = await Cli.Wrap(YtdlpBinaryPath)
                    .WithArguments(
                        $"-J --compat-options manifest-filesize-approx https://youtube.com/watch?v={videoId}")
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteBufferedAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                if (!useTimeout)
                {
                    throw;
                }

                _logger.LogError("GetVideoInfo timeout VideoId:{VideoId}", videoId);
            }
            finally
            {
                cts.Dispose();
            }

            if (result is not null)
            {
                if (result.ExitCode == 0)
                    break;

                if (result.StandardError.Contains("This live event will begin in a few moments.")
                    || result.StandardError.Contains("Video unavailable"))
                {
                    throw new VideoNotAvailableException("Unavailable");
                }

                if (result.StandardError.Contains("Private video."))
                {
                    throw new VideoNotAvailableException("Private");
                }

                if (result.StandardError.Contains("This video is available to this channel's members"))
                {
                    throw new VideoNotAvailableException("Members only");
                }

                if (result.StandardError.Contains("This live event will begin in ") ||
                    result.StandardError.Contains("Premieres in "))
                {
                    throw new VideoNotAvailableException("Upcoming");
                }
            }

            if (infiniteRetries == false)
            {
                retriesLeft--;
                if (retriesLeft == 0)
                    throw new VideoNotAvailableException();
            }

            _logger.LogWarning("GetVideoInfo Error, Retrying... ({RetriesLeft} left) VideoId:{VideoId}",
                infiniteRetries ? "∞" : retriesLeft, videoId);
            await Task.Delay(3000, cancellationToken);
        }

        if (result.StandardError.Contains("Some formats may be missing"))
        {
            _logger.LogWarning("GetVideoInfo: Some formats may be missing VideoId:{VideoId}", videoId);
        }

        var videoInfo = JsonSerializer.Deserialize<VideoInfo>(result.StandardOutput);
        if (videoInfo is null) throw new JsonException("Deserialize result is null.");
        new VideoInfoValidator().ValidateAndThrow(videoInfo);

        return videoInfo;
    }

    public async Task<PlaylistInfo> GetPlaylistInfoAsync(string playlistId, CancellationToken cancellationToken,
        int? count = null)
    {
        var retriesLeft = 3;
        BufferedCommandResult result;
        var stop = count?.ToString();

        while (true)
        {
            result = await Cli.Wrap(YtdlpBinaryPath)
                .WithArguments(
                    $"-J --flat-playlist --extractor-args youtubetab:approximate_date -I :{stop} https://youtube.com/playlist?list={playlistId}")
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync(cancellationToken);

            // Retry if failed
            if (result.ExitCode != 0)
            {
                if (result.StandardError.Contains("does not exist"))
                {
                    throw new PlaylistNotAvailableException("Unavailable");
                }

                retriesLeft--;
                if (retriesLeft == 0)
                    throw new PlaylistNotAvailableException();
                _logger.LogWarning("GetPlaylistInfo Error, Retrying... ({RetriesLeft} left) PlaylistId:{PlaylistId}",
                    retriesLeft, playlistId);
                await Task.Delay(3000, cancellationToken);
                continue;
            }

            break;
        }

        var playlistInfo = JsonSerializer.Deserialize<PlaylistInfo>(result.StandardOutput);
        if (playlistInfo is null) throw new JsonException("Deserialize result is null.");
        new PlaylistInfoValidator().ValidateAndThrow(playlistInfo);

        return playlistInfo;
    }

    public async Task<ChannelInfo> GetChannelInfoAsync(string handle, CancellationToken cancellationToken)
    {
        var retriesLeft = 3;
        BufferedCommandResult result;

        while (true)
        {
            result = await Cli.Wrap(YtdlpBinaryPath)
                .WithArguments($"-J --flat-playlist -I 0:0 https://youtube.com/{handle}/featured")
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync(cancellationToken);

            // Retry if failed
            if (result.ExitCode != 0)
            {
                if (result.StandardError.Contains("Not Found"))
                {
                    throw new ChannelNotAvailableException("Unavailable");
                }

                retriesLeft--;
                if (retriesLeft == 0)
                    throw new ChannelNotAvailableException();
                _logger.LogWarning("GetChannelInfo Error, Retrying... ({RetriesLeft} left) Channel:{ChannelHandle}",
                    retriesLeft, handle);
                await Task.Delay(3000, cancellationToken);
                continue;
            }

            break;
        }

        var channelInfo = JsonSerializer.Deserialize<ChannelInfo>(result.StandardOutput);
        if (channelInfo is null) throw new JsonException("Deserialize result is null.");
        new ChannelInfoValidator().ValidateAndThrow(channelInfo);

        return channelInfo;
    }
}