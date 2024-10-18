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
    private readonly Settings _settings;
    private const string YtdlpBinaryPath = @"./utils/yt-dlp.exe";

    public Ytdlp(ILogger<Ytdlp> logger, Settings settings)
    {
        _logger = logger;
        _settings = settings;
    }

    public async Task<(VideoInfo, bool)> GetVideoInfoAsync(string videoId, int retries, bool useTimeout,
        CancellationToken cancellationToken)
    {
        var retriesLeft = retries;
        var infiniteRetries = retries == -1;
        BufferedCommandResult? result = null;

        while (true)
        {
            var innerCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (useTimeout)
            {
                innerCancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(20));
            }

            try
            {
                result = await Cli.Wrap(YtdlpBinaryPath)
                    .WithArguments(args =>
                    {
                        args.Add("-J")
                            .Add("--compat-options").Add("manifest-filesize-approx");
                        if (_settings.CookiesBrowserName != "")
                            args.Add("--cookies-from-browser").Add(_settings.CookiesBrowserName);
                        args.Add($"https://youtube.com/watch?v={videoId}");
                    })
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteBufferedAsync(innerCancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }

                _logger.LogError("GetVideoInfo timeout VideoId:{VideoId}", videoId);
            }
            finally
            {
                innerCancellationTokenSource.Dispose();
            }

            if (result is not null)
            {
                if (result.ExitCode == 0)
                    break;

                var errorMsg = result.StandardError.Trim();
                _logger.LogError("GetVideoInfo: {Error}", errorMsg);

                if (result.StandardError.Contains("This live event will begin in a few moments.")
                    || result.StandardError.Contains("Video unavailable"))
                {
                    throw new VideoNotAvailableException("Unavailable", errorMsg);
                }

                if (result.StandardError.Contains("Private video."))
                {
                    throw new VideoNotAvailableException("Private", errorMsg);
                }

                if (result.StandardError.Contains("This video is available to this channel's members") ||
                    result.StandardError.Contains("members-only"))
                {
                    throw new VideoNotAvailableException("Members only", errorMsg);
                }

                if (result.StandardError.Contains("This live event will begin in ") ||
                    result.StandardError.Contains("Premieres in "))
                {
                    throw new VideoNotAvailableException("Upcoming", errorMsg);
                }

                if (result.StandardError.Contains("confirm you’re not a bot"))
                {
                    throw new VideoNotAvailableException("Bot?", errorMsg);
                }
            }

            if (infiniteRetries == false)
            {
                retriesLeft--;
                if (retriesLeft == 0)
                {
                    if (result is not null)
                        throw new VideoNotAvailableException(null, result.StandardError.Trim());
                    throw new VideoNotAvailableException();
                }
            }

            _logger.LogError("GetVideoInfo Error, Retrying... ({RetriesLeft} left) VideoId:{VideoId}",
                infiniteRetries ? "∞" : retriesLeft, videoId);
            await Task.Delay(3000, cancellationToken);
        }

        var missingFormats = false;
        if (result.StandardError.Contains("Some formats may be missing", StringComparison.OrdinalIgnoreCase))
        {
            missingFormats = true;
            _logger.LogWarning("GetVideoInfo: Some formats may be missing. VideoId: {VideoId}", videoId);
        }

        var videoInfo = JsonSerializer.Deserialize<VideoInfo>(result.StandardOutput);
        if (videoInfo is null) throw new JsonException("Deserialize result is null.");
        await new VideoInfoValidator().ValidateAndThrowAsync(videoInfo, cancellationToken);

        return (videoInfo, missingFormats);
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
                .WithArguments(args =>
                {
                    args.Add("-J")
                        .Add("--flat-playlist")
                        .Add("--extractor-args").Add("youtubetab:approximate_date")
                        .Add("-I").Add($":{stop}");
                    if (_settings.CookiesBrowserName != "")
                        args.Add("--cookies-from-browser").Add(_settings.CookiesBrowserName);
                    args.Add($"https://youtube.com/playlist?list={playlistId}");
                })
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync(cancellationToken);

            // Retry if failed
            if (result.ExitCode != 0)
            {
                var errorMsg = result.StandardError.Trim();
                _logger.LogError("GetPlaylistInfo: {Error}", errorMsg);

                if (result.StandardError.Contains("does not exist"))
                {
                    throw new PlaylistNotAvailableException("Unavailable", errorMsg);
                }

                retriesLeft--;
                if (retriesLeft == 0)
                    throw new PlaylistNotAvailableException(null, errorMsg);
                _logger.LogError("GetPlaylistInfo Error, Retrying... ({RetriesLeft} left) PlaylistId:{PlaylistId}",
                    retriesLeft, playlistId);
                await Task.Delay(3000, cancellationToken);
                continue;
            }

            break;
        }

        var playlistInfo = JsonSerializer.Deserialize<PlaylistInfo>(result.StandardOutput);
        if (playlistInfo is null) throw new JsonException("Deserialize result is null.");
        await new PlaylistInfoValidator().ValidateAndThrowAsync(playlistInfo, cancellationToken);

        return playlistInfo;
    }

    public async Task<ChannelInfo> GetChannelInfoAsync(string handle, CancellationToken cancellationToken)
    {
        var retriesLeft = 3;
        BufferedCommandResult result;

        while (true)
        {
            result = await Cli.Wrap(YtdlpBinaryPath)
                .WithArguments(args =>
                    {
                        args.Add("-J")
                            .Add("--flat-playlist")
                            .Add("-I").Add("0:0");
                        if (_settings.CookiesBrowserName != "")
                            args.Add("--cookies-from-browser").Add(_settings.CookiesBrowserName);
                        args.Add($"https://youtube.com/{handle}/featured");
                    }
                )
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync(cancellationToken);

            // Retry if failed
            if (result.ExitCode != 0)
            {
                var errorMsg = result.StandardError.Trim();
                _logger.LogError("GetChannelInfo: {Error}", errorMsg);

                if (result.StandardError.Contains("Not Found"))
                {
                    throw new ChannelNotAvailableException("Unavailable", errorMsg);
                }

                retriesLeft--;
                if (retriesLeft == 0)
                    throw new ChannelNotAvailableException(null, errorMsg);
                _logger.LogError("GetChannelInfo Error, Retrying... ({RetriesLeft} left) Channel:{ChannelHandle}",
                    retriesLeft, handle);
                await Task.Delay(3000, cancellationToken);
                continue;
            }

            break;
        }

        var channelInfo = JsonSerializer.Deserialize<ChannelInfo>(result.StandardOutput);
        if (channelInfo is null) throw new JsonException("Deserialize result is null.");
        await new ChannelInfoValidator().ValidateAndThrowAsync(channelInfo, cancellationToken);

        return channelInfo;
    }
}