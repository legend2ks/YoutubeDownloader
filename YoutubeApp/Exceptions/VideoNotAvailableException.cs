using System;

namespace YoutubeApp.Exceptions;

[Serializable]
internal class VideoNotAvailableException : Exception
{
    public string? Reason { get; }

    public VideoNotAvailableException()
    {
    }

    public VideoNotAvailableException(string reason) : base($"Video is Unavailable: {reason}")
    {
        Reason = reason;
    }
}