using System;

namespace YoutubeApp.Exceptions;

[Serializable]
internal class VideoNotAvailableException : Exception
{
    public string? Reason { get; }
    public string? ErrorMessage { get; }

    public VideoNotAvailableException()
    {
    }

    public VideoNotAvailableException(string? reason, string? errorMsg) : base($"Video is Unavailable: {reason}")
    {
        Reason = reason;
        ErrorMessage = errorMsg;
    }
}