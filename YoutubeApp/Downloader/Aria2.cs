using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;

namespace YoutubeApp.Downloader;

public class Aria2 : IAria2
{
    private readonly ILogger<Aria2> _logger;
    private const string Aria2BinaryPath = "./utils/aria2c.exe";
    private const string Aria2RpcPort = "6888";

    private Process? _ariaProcess;
    private JsonRpc? _jsonRpc;

    public Aria2(ILogger<Aria2> logger)
    {
        _logger = logger;
    }

    public bool Run()
    {
        _ariaProcess = new ProcessRunner().StartProcess(Aria2BinaryPath,
            $"-k 1M -j 1 --allow-overwrite=true --disable-ipv6 --allow-piece-length-change=true --enable-rpc --rpc-listen-port={Aria2RpcPort}");
        return _ariaProcess is not null;
    }

    public async Task<bool> ConnectAsync(
        Action<AriaNotificationArgs> onDownloadStart,
        Action<AriaNotificationArgs> onDownloadStop,
        Func<AriaNotificationArgs, Task> onDownloadComplete,
        Func<AriaNotificationArgs, Task> onDownloadError)
    {
        ClientWebSocket socket;
        var retriesLeft = 5;

        while (true)
        {
            socket = new ClientWebSocket();
            socket.Options.KeepAliveInterval = TimeSpan.Zero;
            try
            {
                await socket.ConnectAsync(new Uri($"ws://localhost:{Aria2RpcPort}/jsonrpc"), CancellationToken.None);
                break;
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "WebSocket connection to Aria2 failed");
                if (retriesLeft == 0)
                    return false;
                retriesLeft--;
                _logger.LogInformation("Retrying... {RetriesLeft}", retriesLeft);
                await Task.Delay(1000);
            }
        }

        _logger.LogInformation("Aria2 WebSocket connected.");

        _jsonRpc = new JsonRpc(new WebSocketMessageHandler(socket));
        _jsonRpc.AddLocalRpcMethod("aria2.onDownloadStart",
            (AriaNotificationArgs args) => { Dispatcher.UIThread.Post(() => onDownloadStart(args)); });
        _jsonRpc.AddLocalRpcMethod("aria2.onDownloadStop",
            (AriaNotificationArgs args) => { Dispatcher.UIThread.Post(() => onDownloadStop(args)); });
        _jsonRpc.AddLocalRpcMethod("aria2.onDownloadComplete",
            (AriaNotificationArgs args) => { Dispatcher.UIThread.Post(() => onDownloadComplete(args)); });
        _jsonRpc.AddLocalRpcMethod("aria2.onDownloadError",
            (AriaNotificationArgs args) => { Dispatcher.UIThread.Post(() => onDownloadError(args)); });
        _jsonRpc.AddLocalRpcMethod("aria2.onDownloadPause",
            (AriaNotificationArgs args) => { _logger.LogCritical("DownloadPause event occured -> {Args}", args); });

        _jsonRpc.Disconnected += JsonRpc_Disconnected;
        _jsonRpc.StartListening();

        return true;
    }

    private static void JsonRpc_Disconnected(object? sender, JsonRpcDisconnectedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (Application.Current!.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.TryShutdown(-1);
            }
        });
    }

    public async Task<string> AddUriAsync(string uri, string saveTo, string filename, string gid, bool singleConnection)
    {
        var returnedGid = await _jsonRpc!.InvokeAsync<string>("aria2.addUri",
            new[] { uri },
            new Dictionary<string, string>()
            {
                { "gid", gid },
                { "dir", saveTo },
                { "out", filename },
                { "file-allocation", "none" },
                {
                    "split",
                    singleConnection || Settings.MaxConnections == 1 ? "1" : (Settings.MaxConnections * 2).ToString()
                },
                { "max-connection-per-server", singleConnection ? "1" : Settings.MaxConnections.ToString() }
            });
        return returnedGid;
    }

    public async Task<string> RemoveAsync(string gid)
    {
        var removedGid = await _jsonRpc!.InvokeAsync<string>("aria2.remove", gid);
        return removedGid;
    }

    public async Task<AriaTellStatusResponse> TellStatusAsync(string gid)
    {
        var response = await _jsonRpc!.InvokeAsync<AriaTellStatusResponse>("aria2.tellStatus", gid);
        return response;
    }

    public async Task<AriaTellActiveResponse[]> TellActiveAsync()
    {
        var response = await _jsonRpc!.InvokeAsync<AriaTellActiveResponse[]>("aria2.tellActive",
            argument: new[] { "completedLength", "connections", "downloadSpeed", "gid" });
        return response;
    }

    public async Task<JObject> GetVersionAsync()
    {
        var response = await _jsonRpc!.InvokeAsync<JObject>("aria2.getVersion");
        return response;
    }
}