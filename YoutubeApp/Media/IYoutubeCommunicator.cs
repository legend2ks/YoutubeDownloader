using System.Threading;
using System.Threading.Tasks;

namespace YoutubeApp.Media;

public interface IYoutubeCommunicator
{
    Task<(VideoInfo, bool)> GetVideoInfoAsync(string videoId, int retries, bool useTimeout,
        CancellationToken cancellationToken);

    Task<PlaylistInfo> GetPlaylistInfoAsync(string playlistId, CancellationToken cancellationToken, int? count = null);
    Task<ChannelInfo> GetChannelInfoAsync(string handle, CancellationToken cancellationToken);
}