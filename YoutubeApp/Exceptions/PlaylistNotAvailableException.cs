using System;

namespace YoutubeApp.Exceptions;

[Serializable]
internal class PlaylistNotAvailableException : Exception
{
    public string? Reason { get; }
    public string? ErrorMessage { get; }

    public PlaylistNotAvailableException()
    {
    }

    public PlaylistNotAvailableException(string? reason, string? errorMsg) : base($"Playlist is Unavailable: {reason}")
    {
        Reason = reason;
        ErrorMessage = errorMsg;
    }
}