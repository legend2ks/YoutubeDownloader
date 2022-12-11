using System;

namespace YoutubeApp.Exceptions;

[Serializable]
internal class PlaylistNotAvailableException : Exception
{
    public string? Reason { get; }

    public PlaylistNotAvailableException()
    {
    }

    public PlaylistNotAvailableException(string reason) : base($"Playlist is Unavailable: {reason}")
    {
        Reason = reason;
    }
}