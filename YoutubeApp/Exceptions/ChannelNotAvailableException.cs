using System;

namespace YoutubeApp.Exceptions;

[Serializable]
internal class ChannelNotAvailableException : Exception
{
    public string? Reason { get; }
    public string? ErrorMessage { get; }

    public ChannelNotAvailableException()
    {
    }

    public ChannelNotAvailableException(string? reason, string? errorMsg) : base($"Channel is Unavailable: {reason}")
    {
        Reason = reason;
        ErrorMessage = errorMsg;
    }
}