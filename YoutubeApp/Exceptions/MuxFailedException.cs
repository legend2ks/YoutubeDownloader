using System;

namespace YoutubeApp.Exceptions;

internal class MuxFailedException : Exception
{
    public string? Reason { get; }

    public MuxFailedException()
    {
    }

    public MuxFailedException(string reason)
    {
        Reason = reason;
    }
}