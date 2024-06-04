using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Selection;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using MsBox.Avalonia.Enums;
using YoutubeApp.Database;
using YoutubeApp.Downloader;
using YoutubeApp.Enums;
using YoutubeApp.Extensions;
using YoutubeApp.Media;
using YoutubeApp.Messages;
using YoutubeApp.Models;

namespace YoutubeApp.ViewModels;

public partial class DownloadsViewModel : ViewModelBase, IRecipient<ChannelDeletedMessage>,
    IRecipient<ChannelAddedMessage>
{
    public DownloadsViewModel(ILogger<DownloadsViewModel> logger, DownloadData downloadData, ChannelData channelData,
        Settings settings, DownloadManager downloadManager, Youtube youtube, DownloaderUtils downloaderUtils,
        IMessenger messenger)
    {
        _logger = logger;
        _downloadData = downloadData;
        _settings = settings;
        _youtube = youtube;
        _downloaderUtils = downloaderUtils;
        _messenger = messenger;
        DownloadManager = downloadManager;

        DownloadManager.ActiveDownloads.CollectionChanged += ActiveDownloads_CollectionChanged;

        Selection = new SelectionModel<Download>
        {
            SingleSelect = false
        };
        Selection.SelectionChanged += SelectionChanged;

        var downloads = _downloadData.GetDownloadList();
        var channels = channelData.GetChannels();
        foreach (var download in downloads)
        {
            download.Channel = channels.FirstOrDefault(x => x.UniqueId == download.ChannelId);
            Downloads.Add(download);
        }

        downloadData.Downloads = Downloads;

        GridConfig = Settings.ColumnsConfig;
        var columnsSorted = GridConfig.OrderBy(x => x.Value.Order).Select(x => x.Value);
        ColumnOrdered = new ObservableCollection<ColumnConfig>(columnsSorted);

        var appLifetime = (IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
        appLifetime.ShutdownRequested += Application_ShutdownRequested;

        DownloadManager.DownloadCompleted += DownloadManager_DownloadCompleted;
        DownloadManager.DownloadStopped += DownloadManager_DownloadStopped;
        DownloadManager.DownloadError += DownloadManager_DownloadError;
        Youtube.VideoFound += Youtube_VideoFound;
        Youtube.RefreshFinished += Youtube_RefreshFinished;
        Download.EnableStateChanged += Download_EnableStateChanged;

        _messenger.RegisterAll(this);
    }

    protected DownloadsViewModel()
    {
    }

    private readonly ILogger<DownloadsViewModel> _logger;
    private readonly DownloadData _downloadData;
    private readonly Settings _settings;
    private readonly Youtube _youtube;
    private readonly DownloaderUtils _downloaderUtils;
    private readonly IMessenger _messenger;
    public DownloadManager DownloadManager { get; }

    public Dictionary<string, ColumnConfig> GridConfig { get; set; }

    [ObservableProperty] private ObservableCollection<ColumnConfig> _columnOrdered;

    [ObservableProperty] private ObservableCollection<Download> _selectedDownloads = new();

    public SelectionModel<Download> Selection { get; init; }

    [ObservableProperty] private ObservableCollection<Download> _downloads = new();

    private int _removeBlockerCount;
    private int _refreshBlockerCount;
    private int _chooseFormatBlockerCount;
    private bool _movingItems;
    private bool _isShuttingDown;

    public bool RemoveButtonEnabled => Selection.Count > 0 && _removeBlockerCount == 0;

    public bool RefreshButtonEnabled => Selection.Count > 0 && _refreshBlockerCount == 0;

    public bool ChooseFormatButtonEnabled => Selection.Count > 0 && _chooseFormatBlockerCount == 0;

    public bool OpenIsEnabled => Selection.Count == 1 && Selection.SelectedItems[0]!.Completed;

    public bool OpenFolderIsEnabled => Selection.Count == 1;

    public bool ChangeDirectoryIsEnabled => Selection.SelectedItems.All(x => x!.Enabled == false);

    public bool StartButtonEnabled => DownloadManager.ActiveDownloads.Count == 0;

    private void ActiveDownloads_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(StartButtonEnabled));
    }

    private void Application_ShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        _logger.LogDebug("DownloadsVM - Application shutdown");
        if (DownloadManager.ActiveDownloads.Count > 0 && !_isShuttingDown)
        {
            _isShuttingDown = true;
            StopButtonClicked();
            return;
        }

        _logger.LogDebug("DownloadsVM - Saving settings");
        _settings.SaveColumnConfig(GridConfig.ToArray());
    }

    [RelayCommand]
    private void StartButtonClicked()
    {
        foreach (var dl in Downloads)
        {
            if (dl.Enabled != true) continue;

            dl.SetStarted();
            DownloadManager.ActiveDownloads.Add(dl);
            if (Selection.SelectedItems.Contains(dl))
            {
                _removeBlockerCount++;
            }

            _ = DownloadManager.StartDownloadAsync(dl);
            break;
        }

        OnPropertyChanged(nameof(RemoveButtonEnabled));
    }

    [RelayCommand]
    private void StopButtonClicked()
    {
        foreach (var dl in DownloadManager.ActiveDownloads.ToArray())
        {
            if (dl.Refreshing)
            {
                dl.RefreshCancellationTokenSource.Cancel();
                continue;
            }

            DownloadManager.StopDownload(dl.Id);
        }
    }

    [RelayCommand]
    private async Task ChooseFormatButtonPressedAsync()
    {
        var selectedItems =
            Selection.SelectedItems.Where(x => x is { Enabled: false, Completed: false, Refreshing: false }).ToList();
        if (selectedItems.Count == 0) return;
        await _messenger.Send(new ShowChooseFormatWindowMessage { SelectedItems = selectedItems! });
    }

    [RelayCommand]
    private void RefreshButtonClicked()
    {
        _removeBlockerCount += Selection.Count;
        _chooseFormatBlockerCount += Selection.Count;
        _refreshBlockerCount += Selection.Count;
        OnPropertyChanged(nameof(RemoveButtonEnabled));
        OnPropertyChanged(nameof(RefreshButtonEnabled));
        OnPropertyChanged(nameof(ChooseFormatButtonEnabled));

        _ = _youtube.RefreshVideoInfoAsync(Selection.SelectedItems.ToArray()!);
    }


    [RelayCommand]
    private async Task RemoveAsync()
    {
        var activeTabIndex = _messenger.Send(new GetActiveTabIndexMessage()).Response;
        if (activeTabIndex != 0) return;

        var selectedIndexes = Selection.SelectedIndexes.ToArray();
        var selectedItems = Selection.SelectedItems.ToArray();

        var temporaryDisabledDownloads = new List<Download>();

        foreach (var dl in selectedItems)
        {
            if (!dl!.Enabled) continue;
            dl.Enabled = false;
            temporaryDisabledDownloads.Add(dl);
        }

        var result = await _messenger.Send(new ShowMessageBoxCheckboxMessage
        {
            Title = "Remove download(s)",
            Message = $"Are you sure you want to remove the {selectedIndexes.Length} selected download(s)?",
            CheckboxText = "Also permanently delete the files",
            ButtonDefinitions = ButtonEnum.YesNo,
            Icon = Icon.Question,
        });

        if (result.Button != ButtonResult.Yes)
        {
            foreach (var dl in temporaryDisabledDownloads)
            {
                dl.Enabled = true;
            }

            return;
        }

        _downloaderUtils.DeleteTempFiles(selectedItems!);
        if (result.IsCheckboxChecked) _downloaderUtils.DeleteCompletedFiles(selectedItems);
        _downloadData.RemoveDownloads(selectedItems!);

        var deletedCount = 0;
        foreach (var index in selectedIndexes)
        {
            Downloads.RemoveAt(index - deletedCount);
            deletedCount++;
        }

        var i = 1;
        foreach (var dl in Downloads)
        {
            dl.Priority = i;
            i++;
        }
    }

    [RelayCommand]
    private async Task RemoveCompletedAsync()
    {
        var completedIndexes = new List<int>();
        var completedItems = new List<Download>();

        var idx = 0;
        foreach (var dl in Downloads)
        {
            if (dl.Completed)
            {
                completedIndexes.Add(idx);
                completedItems.Add(dl);
            }

            idx++;
        }

        if (completedItems.Count == 0) return;

        var result = await _messenger.Send(new ShowMessageBoxCheckboxMessage
        {
            Title = "Remove completed download(s)",
            Message = $"Are you sure you want to remove the {completedItems.Count} completed download(s)?",
            CheckboxText = "Also permanently delete the files",
            ButtonDefinitions = ButtonEnum.YesNo,
            Icon = Icon.Question,
        });

        if (result.Button != ButtonResult.Yes) return;

        if (result.IsCheckboxChecked) _downloaderUtils.DeleteCompletedFiles(completedItems);
        _downloadData.RemoveDownloads(completedItems);

        var deletedCount = 0;
        foreach (var index in completedIndexes)
        {
            Downloads.RemoveAt(index - deletedCount);
            deletedCount++;
        }

        var i = 1;
        foreach (var dl in Downloads)
        {
            dl.Priority = i;
            i++;
        }
    }

    [RelayCommand]
    private async Task RemoveCompletedFilelessAsync()
    {
        var completedIndexes = new List<int>();
        var completedItems = new List<Download>();

        var idx = 0;
        foreach (var dl in Downloads)
        {
            if (dl.Completed)
            {
                var filepath = Path.Combine(dl.SaveTo, dl.Filename);
                if (!File.Exists(filepath))
                {
                    completedIndexes.Add(idx);
                    completedItems.Add(dl);
                }
            }

            idx++;
        }

        if (completedItems.Count == 0) return;

        var result = await _messenger.Send(new ShowMessageBoxMessage
        {
            Title = "Remove completed fileless download(s)",
            Message = $"Are you sure you want to remove the {completedItems.Count} completed fileless download(s)?",
            ButtonDefinitions = ButtonEnum.YesNo,
            Icon = Icon.Question,
        });

        if (result != ButtonResult.Yes) return;

        _downloadData.RemoveDownloads(completedItems);

        var deletedCount = 0;
        foreach (var index in completedIndexes)
        {
            Downloads.RemoveAt(index - deletedCount);
            deletedCount++;
        }

        var i = 1;
        foreach (var dl in Downloads)
        {
            dl.Priority = i;
            i++;
        }
    }

    [RelayCommand]
    private void MoveUpButtonClicked() // ⬆
    {
        var selectedDownloadsIndexes = Selection.SelectedIndexes.ToList();
        if (selectedDownloadsIndexes.Count == 0) return;

        if (selectedDownloadsIndexes[0] == 0) return;

        List<(int, int)> changes = new();
        Download? temp = null;
        var tempIndex = 0;

        _movingItems = true;
        for (var i = 0; i < selectedDownloadsIndexes.Count; i++)
        {
            if (temp is null)
            {
                temp = Downloads[selectedDownloadsIndexes[i] - 1];
                tempIndex = selectedDownloadsIndexes[i] - 1;
            }

            Downloads[selectedDownloadsIndexes[i] - 1] = Downloads[selectedDownloadsIndexes[i]];
            Downloads[selectedDownloadsIndexes[i] - 1].Priority -= 1;
            changes.Add((Downloads[selectedDownloadsIndexes[i] - 1].Id, -1));

            if (i != selectedDownloadsIndexes.Count - 1
                && selectedDownloadsIndexes[i + 1] - selectedDownloadsIndexes[i] <= 1) continue;
            Downloads[selectedDownloadsIndexes[i]] = temp;
            var priorityChange = selectedDownloadsIndexes[i] - tempIndex;
            temp.Priority += priorityChange;
            changes.Add((temp.Id, priorityChange));
            temp = null;
        }

        foreach (var i in selectedDownloadsIndexes)
        {
            Selection.Select(i - 1);
        }

        _movingItems = false;
        _downloadData.UpdateDownloadPriorities(changes);
    }

    [RelayCommand]
    private void MoveDownButtonClicked() // ⬇
    {
        var selectedDownloadsIndexes = Selection.SelectedIndexes.ToList();
        if (selectedDownloadsIndexes.Count == 0) return;

        if (selectedDownloadsIndexes[^1] == Downloads.Count - 1) return;

        List<(int, int)> changes = new();
        Download? temp = null;
        var tempIndex = 0;

        _movingItems = true;
        for (var i = selectedDownloadsIndexes.Count - 1; i >= 0; i--)
        {
            if (temp is null)
            {
                temp = Downloads[selectedDownloadsIndexes[i] + 1];
                tempIndex = selectedDownloadsIndexes[i] + 1;
            }

            Downloads[selectedDownloadsIndexes[i] + 1] = Downloads[selectedDownloadsIndexes[i]];
            Downloads[selectedDownloadsIndexes[i] + 1].Priority += 1;
            changes.Add((Downloads[selectedDownloadsIndexes[i] + 1].Id, 1));

            if (i != 0
                && selectedDownloadsIndexes[i] - selectedDownloadsIndexes[i - 1] <= 1) continue;
            Downloads[selectedDownloadsIndexes[i]] = temp;
            var priorityChange = selectedDownloadsIndexes[i] - tempIndex;
            temp.Priority += priorityChange;
            changes.Add((temp.Id, priorityChange));
            temp = null;
        }

        foreach (var i in selectedDownloadsIndexes)
        {
            Selection.Select(i + 1);
        }

        _movingItems = false;
        _downloadData.UpdateDownloadPriorities(changes);
    }

    [RelayCommand]
    private async Task ChooseSingleFormatButtonPressedAsync(Download dl)
    {
        await _messenger.Send(new ShowChooseSingleFormatWindowMessage { Download = dl });
    }

    [RelayCommand]
    private async Task ColumnSettingsButtonPressedAsync()
    {
        var columnConfigs = await _messenger.Send(new ShowColumnsWindowMessage());
        if (columnConfigs is null) return;
        ColumnOrdered = new ObservableCollection<ColumnConfig>(columnConfigs);
    }


    private void Download_EnableStateChanged(object? sender, EnableStateChangedEventArgs e)
    {
        if (!Selection.SelectedItems.Contains(e.Download)) return;

        if (e.Download.Enabled)
        {
            _chooseFormatBlockerCount++;
            _refreshBlockerCount++;
        }
        else
        {
            _chooseFormatBlockerCount--;
            _refreshBlockerCount--;
        }

        OnPropertyChanged(nameof(RefreshButtonEnabled));
        OnPropertyChanged(nameof(ChooseFormatButtonEnabled));
    }

    private void AddToDownloadList(Download download)
    {
        Downloads.Add(download);
    }

    [RelayCommand]
    private void ChangeEnabledState(Download dl)
    {
        _downloadData.UpdateDownloadEnabledState(new[] { dl.Id }, dl.Enabled);
    }


    private void SelectionChanged(object? sender, SelectionModelSelectionChangedEventArgs<Download> e)
    {
        if (_movingItems) return;

        // Selected
        foreach (var item in e.SelectedItems)
        {
            if (item!.Enabled || item.Completed || item.Refreshing)
            {
                _chooseFormatBlockerCount++;
                _refreshBlockerCount++;
            }

            if (item.Downloading || item.Refreshing)
            {
                _removeBlockerCount++;
            }
        }

        // De-selected
        foreach (var item in e.DeselectedItems)
        {
            if (item!.Enabled || item.Completed || item.Refreshing)
            {
                _chooseFormatBlockerCount--;
                _refreshBlockerCount--;
            }

            if (item.Downloading || item.Refreshing)
            {
                _removeBlockerCount--;
            }
        }

        OnPropertyChanged(nameof(RemoveButtonEnabled));
        OnPropertyChanged(nameof(RefreshButtonEnabled));
        OnPropertyChanged(nameof(ChooseFormatButtonEnabled));
    }

    private void DownloadManager_DownloadStopped(object? sender, DownloadStoppedEventArgs e)
    {
        var dl = Downloads.First(x => x.Id == e.Id);
        dl.SetStopped();

        if (Selection.SelectedItems.Contains(dl))
        {
            _removeBlockerCount--;
            OnPropertyChanged(nameof(RemoveButtonEnabled));
        }

        DownloadManager.ActiveDownloads.Remove(dl);
    }

    private void DownloadManager_DownloadCompleted(object? sender, DownloadCompletedEventArgs e)
    {
        var dl = Downloads.First(x => x.Id == e.Id);
        dl.SetCompleted();
        dl.BytesLoaded = e.BytesLoaded;

        if (Selection.SelectedItems.Contains(dl))
        {
            _removeBlockerCount--;
            OnPropertyChanged(nameof(RemoveButtonEnabled));
        }

        DownloadManager.ActiveDownloads.Remove(dl);

        _messenger.Send(new VideoDownloadCompletedMessage(dl));

        StartButtonClicked();
    }

    private void DownloadManager_DownloadError(object? sender, DownloadErrorEventArgs e)
    {
        var dl = Downloads.First(x => x.Id == e.Id);
        var isDownloading = dl.Downloading;
        dl.SetFailed(e.Error);

        if (Selection.SelectedItems.Contains(dl))
        {
            _removeBlockerCount--;
            OnPropertyChanged(nameof(RemoveButtonEnabled));
        }

        DownloadManager.ActiveDownloads.Remove(dl);

        if (isDownloading)
            StartButtonClicked();
    }

    private void Youtube_VideoFound(object? sender, VideoFoundEventArgs e)
    {
        if (e.DownloadItem is null)
        {
            return;
        }

        AddToDownloadList(e.DownloadItem);
    }

    private void Youtube_RefreshFinished(object? sender, RefreshFinishedEventArgs e)
    {
        if (!Selection.SelectedItems.Contains(e.Download)) return;

        _removeBlockerCount--;
        _chooseFormatBlockerCount--;
        _refreshBlockerCount--;
        OnPropertyChanged(nameof(RemoveButtonEnabled));
        OnPropertyChanged(nameof(RefreshButtonEnabled));
        OnPropertyChanged(nameof(ChooseFormatButtonEnabled));
    }


    // Context Menu
    //==============

    [RelayCommand]
    private void MenuOpenPressed()
    {
        var item = Selection.SelectedItem;
        var filepath = Path.Combine(item!.SaveTo, item.Filename);
        if (!File.Exists(filepath)) return;

        Process.Start("explorer", $"\"{filepath}\"");
    }

    [RelayCommand]
    private void MenuOpenFolderPressed()
    {
        var item = Selection.SelectedItem;
        var filepath = Path.Combine(item!.SaveTo, item.Filename);
        string args;
        if (File.Exists(filepath))
            args = $"/select, \"{filepath}\"";
        else
        {
            if (!Directory.Exists(item.SaveTo)) return;
            args = $"\"{item.SaveTo}\"";
        }

        using var fileopener = new Process();
        Process.Start("explorer", args);
    }

    [RelayCommand]
    private void MenuCopyLinkPressed()
    {
        var items = Selection.SelectedItems;
        var links = string.Join("\n", items.Select(x => $"https://youtube.com/watch?v={x!.VideoId}"));
        _messenger.Send(new SetClipboardTextMessage { Text = links });
    }

    [RelayCommand]
    private void MenuEnablePressed()
    {
        var items = Selection.SelectedItems.Where(x => x!.EnabledSwitchEnabled && x.Enabled == false).ToArray();
        _downloadData.UpdateDownloadEnabledState(items.Select(x => x!.Id).ToArray(), true);
        foreach (var item in items)
        {
            item!.Enabled = true;
        }
    }

    [RelayCommand]
    private void MenuDisablePressed()
    {
        var items = Selection.SelectedItems.Where(x => x!.EnabledSwitchEnabled && x.Enabled).ToArray();
        _downloadData.UpdateDownloadEnabledState(items.Select(x => x!.Id).ToArray(), false);
        foreach (var item in items)
        {
            item!.Enabled = false;
        }
    }

    [RelayCommand]
    private async Task RefreshFilenameAsync()
    {
        var downloadItems = Selection.SelectedItems.ToArray();
        var result = await _messenger.Send(new ShowMessageBoxMessage
        {
            Title = "Refresh filename(s)",
            Message =
                $"Refresh filename for {downloadItems.Length} download(s) based on current template?\n{_settings.FilenameTemplate}",
            ButtonDefinitions = ButtonEnum.YesNo,
            Icon = Icon.Question,
        });

        if (result != ButtonResult.Yes) return;

        var errors = new List<string>();
        foreach (var dl in downloadItems)
        {
            var isChannelVideo = Utils.IsSamePath(dl.Channel?.Path, dl.SaveTo);
            var filenameTemplate = isChannelVideo ? Settings.DefaultFilenameTemplate : _settings.FilenameTemplate;
            var newFilename = Youtube.GenerateFilename(filenameTemplate, dl.VideoId, dl.Title, dl.Container,
                dl.SelectedVariant.Fps, dl.ChannelTitle, dl.UploadDate, dl.SelectedVariant.Width,
                dl.SelectedVariant.Height, dl.SelectedVariant.VCodec, dl.SelectedVariant.ACodec,
                dl.SelectedVariant.Abr);
            if (dl.Filename == newFilename) continue;

            var srcPath = Path.Combine(dl.SaveTo, dl.Filename);

            if (dl.Completed && File.Exists(srcPath))
            {
                var destPath = Path.Combine(dl.SaveTo, newFilename);
                if (File.Exists(destPath))
                {
                    errors.Add($"Filename already exists: {destPath}");
                    continue;
                }

                try
                {
                    File.Move(srcPath, destPath);
                }
                catch (Exception e)
                {
                    errors.Add($"{e.Message}: {destPath}");
                    continue;
                }
            }

            _downloadData.UpdateFilename(dl.Id, newFilename);
            dl.Filename = newFilename;
        }

        if (errors.Count > 0)
        {
            await _messenger.Send(new ShowLogWindowMessage { Title = "Rename Error(s)", Items = errors.ToArray() });
        }
    }

    [RelayCommand]
    private async Task MenuChangeDirectoryPressedAsync()
    {
        string? suggestedStartLocation = null;
        var paths = Selection.SelectedItems.Select(x => x!.SaveTo).ToArray();
        var commonPath = Utils.FindCommonPath(paths);
        if (commonPath.Length > 0)
        {
            var saveTo = Path.GetFullPath(commonPath);
            if (Directory.Exists(saveTo))
                suggestedStartLocation = saveTo;
        }

        var selectedFolders = await _messenger.Send(new OpenFolderPickerMessage
        {
            Title = "Move To...",
            SuggestedStartLocation = suggestedStartLocation
        });
        if (selectedFolders.Count != 1) return;

        var destPath = new DirectoryInfo(selectedFolders[0]!.Path.LocalPath).GetActualPath();

        List<string> errors;
        try
        {
            errors = _downloaderUtils.MoveFiles(Selection.SelectedItems.ToArray()!, destPath);
        }
        catch (Exception e)
        {
            await _messenger.Send(new ShowMessageBoxMessage
            {
                Title = "Change Directory Error",
                Message = e.Message,
                ButtonDefinitions = ButtonEnum.Ok,
                Icon = Icon.Error,
            });
            return;
        }

        if (errors.Count > 0)
        {
            await _messenger.Send(new ShowLogWindowMessage
            {
                Title = "Move File Error(s)",
                Items = errors.ToArray(),
            });
        }
    }

    [RelayCommand]
    private void GoToChannel(Download dl)
    {
        var channel = dl.Channel;
        if (channel is null || channel.Updating)
            return;
        _messenger.Send(new ShowVideoInChannelMessage(channel, dl.VideoId));
        _ = Task.Run(() =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                _messenger.Send(new ShowVideoInChannelMessage(channel, dl.VideoId),
                    (int)MessengerChannel.VideosView);
            });
        });
    }

    public void Receive(ChannelDeletedMessage message)
    {
        foreach (var dl in Downloads)
        {
            if (dl.Channel == message.Channel) dl.Channel = null;
        }
    }

    public void Receive(ChannelAddedMessage message)
    {
        foreach (var dl in Downloads)
        {
            if (dl.ChannelId == message.Channel.UniqueId) dl.Channel = message.Channel;
        }
    }
}