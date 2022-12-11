using System;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;
using Microsoft.Extensions.Logging;
using YoutubeApp.Exceptions;

namespace YoutubeApp.Media;

public class Ffmpeg
{
    private readonly ILogger<Ffmpeg> _logger;
    private const string FfmpegBinaryPath = @"./utils/ffmpeg.exe";

    public Ffmpeg(ILogger<Ffmpeg> logger)
    {
        _logger = logger;
    }

    public async Task MuxAsync(string videoFilepath, string audioFilepath, string outputFilepath,
        CancellationToken cancellationToken)
    {
        CommandResult result;
        try
        {
            result = await Cli.Wrap(FfmpegBinaryPath)
                .WithArguments(
                    $"-y -i \"{videoFilepath}\" -i \"{audioFilepath}\" -c:v copy -c:a copy \"{outputFilepath}\"")
                .WithValidation(CommandResultValidation.None).ExecuteAsync(cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Mux failed");
            throw new MuxFailedException();
        }

        if (result.ExitCode == -28)
            throw new MuxFailedException("No Space");
        if (result.ExitCode != 0)
            throw new MuxFailedException();
    }
}