using YoutubeApp.Models;

namespace YoutubeApp.Media;

public class RefreshFinishedEventArgs
{
    public required Download Download { get; set; }
}