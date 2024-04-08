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
        string? metadataFilepath, CancellationToken cancellationToken)
    {
        var cmd = metadataFilepath is not null
            ? $"""
               -y -i "{videoFilepath}" -i "{audioFilepath}" -i "{metadataFilepath}" -map_metadata 2 -c:v copy -c:a copy "{outputFilepath}"
               """
            : $"""
               -y -i "{videoFilepath}" -i "{audioFilepath}" -c:v copy -c:a copy "{outputFilepath}"
               """;

        CommandResult result;
        try
        {
            result = await Cli.Wrap(FfmpegBinaryPath).WithArguments(cmd).WithValidation(CommandResultValidation.None)
                .ExecuteAsync(cancellationToken);
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