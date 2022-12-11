using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace YoutubeApp.Downloader;

public interface IAria2
{
    bool Run();

    Task<bool> ConnectAsync(
        Action<AriaNotificationArgs> onDownloadStart,
        Action<AriaNotificationArgs> onDownloadStop,
        Func<AriaNotificationArgs, Task> onDownloadComplete,
        Func<AriaNotificationArgs, Task> onDownloadError);

    Task<string> AddUriAsync(string uri, string saveTo, string filename, string gid, bool singleConnection);
    Task<string> RemoveAsync(string gid);
    Task<AriaTellStatusResponse> TellStatusAsync(string gid);
    Task<AriaTellActiveResponse[]> TellActiveAsync();
    Task<JObject> GetVersionAsync();
}