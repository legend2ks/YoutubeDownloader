using System;

namespace YoutubeApp.Exceptions;

[Serializable]
internal class ChannelNotAvailableException : Exception
{
    public string? Reason { get; }

    public ChannelNotAvailableException()
    {
    }

    public ChannelNotAvailableException(string reason) : base($"Channel is Unavailable: {reason}")
    {
        Reason = reason;
    }
}