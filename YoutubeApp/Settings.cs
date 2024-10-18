using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using YoutubeApp.Database;

namespace YoutubeApp;

public partial class ColumnConfig : ObservableObject
{
    [ObservableProperty] private int _order;

    private int _minWidth;

    private GridLength _width;

    public GridLength Width
    {
        get => _width;
        set
        {
            if (value.Value < _minWidth) return;
            SetProperty(ref _width, value);
        }
    }

    public bool Movable { get; set; }

    public ColumnConfig(int order, string width, int minWidth, bool movable)
    {
        _order = order;
        _width = GridLength.Parse(width);
        _minWidth = minWidth;
        Movable = movable;
    }
}

internal class ColumnOrderAndSize
{
    public required string Key { get; set; }
    public required int Order { get; set; }
    public required double Width { get; set; }
}

public class Settings
{
    private readonly SettingsData _settingsData;

    public const string DefaultFilenameTemplate =
        "[*UPYEAR*.*UPMONTH*.*UPDAY*][*CHANNEL*][*VIDEO_ID*] *TITLE* (*H*p*FPS*fps_*VIDEO_CODEC*_*AUDIO_CODEC*@*AUDIO_BITRATE*)";

    public Settings(SettingsData settingsData)
    {
        _settingsData = settingsData;
        var settings = _settingsData.GetSettings();

        // Filename Template
        var filenameTemplate = settings.FirstOrDefault(x => x.Key == "FilenameTemplate")?.Value;
        FilenameTemplate = filenameTemplate ?? DefaultFilenameTemplate;

        // Last Save Path
        var lastSavePath = settings.FirstOrDefault(x => x.Key == "LastSavePath")?.Value;
        LastSavePath = lastSavePath ?? Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);

        // Max Connections
        var maxConnectionsString = settings.FirstOrDefault(x => x.Key == "MaxConnections")?.Value;
        if (maxConnectionsString is not null)
        {
            var maxConnections = int.Parse(maxConnectionsString);
            if (new[] { 1, 2, 4, 8, 16 }.Contains(maxConnections))
            {
                MaxConnections = maxConnections;
            }
        }

        // Max Concurrent channel updates
        var maxConcurrentChannelUpdatesString =
            settings.FirstOrDefault(x => x.Key == "MaxConcurrentChannelUpdates")?.Value;
        if (maxConcurrentChannelUpdatesString is not null)
        {
            var maxConcurrentChannelUpdates = int.Parse(maxConcurrentChannelUpdatesString);
            if (new[] { 1, 2, 4 }.Contains(maxConcurrentChannelUpdates))
            {
                MaxConcurrentChannelUpdates = maxConcurrentChannelUpdates;
            }
        }

        // Cookies Browser Name
        var cookiesBrowserName = settings.FirstOrDefault(x => x.Key == "CookiesBrowserName")?.Value;
        CookiesBrowserName = cookiesBrowserName ?? "";

        // Window State
        var windowWidthString = settings.FirstOrDefault(x => x.Key == "WindowWidth")?.Value;
        var windowHeightString = settings.FirstOrDefault(x => x.Key == "WindowHeight")?.Value;
        var windowStateString = settings.FirstOrDefault(x => x.Key == "WindowState")?.Value;
        if (windowWidthString is not null && windowHeightString is not null)
        {
            WindowWidth = int.Parse(windowWidthString);
            WindowHeight = int.Parse(windowHeightString);
        }

        if (windowStateString is not null)
        {
            WindowState = Enum.Parse<WindowState>(windowStateString);
        }

        // Columns
        var columnsConfigString = settings.FirstOrDefault(x => x.Key == "ColumnsConfig")?.Value;
        if (columnsConfigString is null) return;
        var columnsConfig = JsonSerializer.Deserialize<List<ColumnOrderAndSize>>(columnsConfigString);
        foreach (var item in columnsConfig)
        {
            if (!ColumnsConfig.ContainsKey(item.Key)) continue;
            ColumnsConfig[item.Key].Order = item.Order;
            ColumnsConfig[item.Key].Width = GridLength.Parse(item.Width.ToString());
        }
    }

    public string FilenameTemplate { get; private set; }
    public static string LastSavePath { get; set; }
    public static int MaxConnections { get; private set; } = 4;
    public static int MaxConcurrentChannelUpdates { get; set; } = 2;
    public string CookiesBrowserName { get; set; }
    public static int WindowWidth { get; private set; } = 900;
    public static int WindowHeight { get; private set; } = 640;
    public static WindowState WindowState { get; private set; } = WindowState.Normal;
    public static WindowState WindowStateBeforeHide { get; set; }

    public static Dictionary<string, ColumnConfig> ColumnsConfig { get; private set; } = new()
    {
        { "en", new(0, "56", 56, false) },
        { "tl", new(1, "200", 100, true) },
        { "vr", new(2, "190", 190, true) },
        { "fs", new(3, "90", 90, true) },
        { "pr", new(4, "110", 110, true) },
        { "sp", new(5, "130", 120, true) },
        { "eta", new(6, "160", 90, true) },
        { "ld", new(7, "100", 100, true) },
        { "dr", new(8, "90", 90, true) },
        { "ch", new(9, "100", 100, true) },
        { "fn", new(10, "150", 100, true) },
        { "sv", new(11, "100", 100, true) },
    };

    public static string ChannelDateFormat => "yy/M/d h:mm tt";

    public void SaveColumnConfig(KeyValuePair<string, ColumnConfig>[] columnConfigs)
    {
        var config = new List<ColumnOrderAndSize>();
        foreach (var item in columnConfigs)
        {
            config.Add(new() { Key = item.Key, Order = item.Value.Order, Width = item.Value.Width.Value });
        }

        var serialized = JsonSerializer.Serialize(config);

        _settingsData.SaveSetting("ColumnsConfig", serialized);
    }

    public void SaveFilenameTemplate(string filenameTemplate)
    {
        FilenameTemplate = filenameTemplate;
        _settingsData.SaveSetting("FilenameTemplate", filenameTemplate);
    }

    public void SaveMaxConnections(int maxConnections)
    {
        MaxConnections = maxConnections;
        _settingsData.SaveSetting("MaxConnections", maxConnections.ToString());
    }

    public void SaveMaxConcurrentChannelUpdates(int maxConcurrentChannelUpdates)
    {
        MaxConcurrentChannelUpdates = maxConcurrentChannelUpdates;
        _settingsData.SaveSetting("MaxConcurrentChannelUpdates", maxConcurrentChannelUpdates.ToString());
    }

    public void SaveCookiesBrowserName(string cookiesBrowserName)
    {
        CookiesBrowserName = cookiesBrowserName;
        _settingsData.SaveSetting("CookiesBrowserName", cookiesBrowserName);
    }

    public void SaveWindowState(int windowWidth, int windowHeight)
    {
        _settingsData.SaveSetting("WindowWidth", windowWidth.ToString());
        _settingsData.SaveSetting("WindowHeight", windowHeight.ToString());
        _settingsData.SaveSetting("WindowState", WindowStateBeforeHide.ToString());
    }

    public void SaveLastSavePath(string saveTo)
    {
        LastSavePath = saveTo;
        _settingsData.SaveSetting("LastSavePath", saveTo);
    }
}