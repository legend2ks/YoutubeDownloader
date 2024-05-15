using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia.Models;
using YoutubeApp.Database;
using YoutubeApp.Downloader;
using YoutubeApp.Enums;
using YoutubeApp.Messages;
using YoutubeApp.Models;

namespace YoutubeApp.ViewModels;

public partial class MoveChannelWindowViewModel : ViewModelBase
{
    private const string VideoIdPattern = @"^\[.*]\[.*]\[(.*)]";

    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly ChannelData _channelData;
    private readonly DownloadData _downloadData;
    private readonly DownloaderUtils _downloaderUtils;
    private readonly Channel _channel;
    private readonly IMessenger _messenger;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(CancelButtonEnabled))]
    private bool _isCancelled;


    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(CancelButtonEnabled))]
    private bool _isFinished;

    public bool CancelButtonEnabled => !IsCancelled && !IsFinished;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(StatusText))]
    private int _totalCount;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(StatusText))]
    private int _doneCount;

    public MoveChannelWindowViewModel(IMessenger messenger, ChannelData channelData, DownloadData downloadData,
        DownloaderUtils downloaderUtils, Channel channel, string destPath)
    {
        _messenger = messenger;
        _channelData = channelData;
        _downloadData = downloadData;
        _downloaderUtils = downloaderUtils;
        _channel = channel;
        SourcePath = channel.Path;
        DestPath = destPath;
    }

    protected MoveChannelWindowViewModel()
    {
    }

    public ObservableCollection<FileItem> FileItems { get; } = new();
    public string SourcePath { get; init; }
    public string DestPath { get; init; }

    public string StatusText =>
        $"{(IsFinished ? "Finished." : IsCancelled ? "Cancelled." : "Moving...")} ({DoneCount}/{TotalCount})";

    public async Task MoveFilesAsync()
    {
        try
        {
            var channelVideoIds = _channelData.GetVideos(_channel.Id).Reverse().Select(x => x.VideoId).ToHashSet();
            var downloads =
                _downloadData.Downloads.Where(x =>
                    x.Channel == _channel && Utils.IsSamePath(x.SaveTo, _channel.Path) && x.Completed).ToArray();
            var filesToMove = new List<(FileInfo info, string dest, Download? dl)>();
            var totalFileSize = 0L;

            // Check for available video files
            var srcFolderInfo = new DirectoryInfo(_channel.Path);
            var dirFiles = srcFolderInfo.GetFiles();
            foreach (var fileInfo in dirFiles)
            {
                var videoIdMatch = Regex.Match(fileInfo.Name, VideoIdPattern);
                if (!videoIdMatch.Success) continue;
                var videoId = videoIdMatch.Groups[1].Value;
                if (!channelVideoIds.Contains(videoId)) continue;

                var dl = downloads.FirstOrDefault(x => x.VideoId == videoId);
                filesToMove.Add((fileInfo, "", dl));
                totalFileSize += fileInfo.Length;
            }

            // Check for available thumbnails files
            var srcThumbsDirPath = Path.Combine(_channel.Path, ".thumbs");
            var srcDirFolderInfo = new DirectoryInfo(srcThumbsDirPath);
            if (srcDirFolderInfo.Exists)
            {
                var thumbFiles = srcDirFolderInfo.GetFiles();
                foreach (var fileInfo in thumbFiles)
                {
                    var videoId = fileInfo.Name[..^fileInfo.Extension.Length];
                    if (!channelVideoIds.Contains(videoId)) continue;

                    filesToMove.Add((fileInfo, ".thumbs", null));
                    totalFileSize += fileInfo.Length;
                }
            }

            TotalCount = filesToMove.Count;

            // Check dest. available free space
            var sameDrive = new DriveInfo(_channel.Path).Name == new DriveInfo(DestPath).Name;
            if (!sameDrive)
            {
                var hasEnoughSpace = _downloaderUtils.CheckAvailableFreeSpace(DestPath, totalFileSize + 1024 * 1024);
                if (!hasEnoughSpace)
                {
                    await _messenger.Send(
                        new ShowMessageBoxMessage
                        {
                            Title = "Error", Message = "There is not enough free space.", Icon = Icon.Error,
                            ButtonDefinitions = ButtonEnum.Ok
                        },
                        (int)MessengerChannel.MoveChannelWindow);
                    _cancellationTokenSource.Dispose();
                    IsCancelled = true;
                    return;
                }
            }

            try
            {
                Directory.CreateDirectory(Path.Combine(DestPath, ".thumbs"));
            }
            catch (Exception e)
            {
                await _messenger.Send(
                    new ShowMessageBoxMessage
                        { Title = "Error", Message = e.Message, Icon = Icon.Error, ButtonDefinitions = ButtonEnum.Ok },
                    (int)MessengerChannel.MoveChannelWindow);
                _cancellationTokenSource.Dispose();
                IsCancelled = true;
                return;
            }

            // Move files
            var skipErrors = false;
            var skipExists = false;
            var overwriteAll = false;

            foreach (var file in filesToMove)
            {
                DoneCount++;
                var localFilePath = Path.Combine(file.dest, file.info.Name);
                var fileItem = new FileItem { Filename = localFilePath, Status = "Moving..." };
                FileItems.Add(fileItem);
                var destFileName = Path.Combine(DestPath, localFilePath);
                if (!overwriteAll && File.Exists(destFileName))
                {
                    if (skipExists)
                    {
                        fileItem.Status = "Skipped";
                        fileItem.Details = "Destination file already exists.";
                        continue;
                    }

                    var result = await _messenger.Send(new ShowMessageBoxCustomMessage
                    {
                        Title = "Move", Message = $"\"{localFilePath}\"\nDestination file already exists. Overwrite?",
                        Icon = Icon.Question,
                        ButtonDefinitions = new[]
                        {
                            new ButtonDefinition { Name = "Overwrite", IsDefault = true },
                            new ButtonDefinition { Name = "Overwrite All" },
                            new ButtonDefinition { Name = "Skip" },
                            new ButtonDefinition { Name = "Skip All" },
                            new ButtonDefinition { Name = "Cancel", IsCancel = true }
                        },
                    }, (int)MessengerChannel.MoveChannelWindow);
                    switch (result)
                    {
                        case "Overwrite All":
                            overwriteAll = true;
                            goto case "Overwrite";
                        case "Overwrite":
                            break;
                        case "Skip All":
                            skipExists = true;
                            goto case "Skip";
                        case "Skip":
                            fileItem.Status = "Skipped";
                            fileItem.Details = "Destination file already exists.";
                            continue;
                        default:
                            Cancel();
                            fileItem.Status = "Cancelled";
                            return;
                    }
                }

                while (true)
                {
                    try
                    {
                        var fileOp = new MoveOp(_cancellationTokenSource.Token);
                        await fileOp.MoveFileAsync(file.info.FullName, destFileName, true);
                        fileItem.Status = "Moved";
                    }
                    catch (OperationCanceledException)
                    {
                        fileItem.Status = "Cancelled";
                    }
                    catch (Exception e)
                    {
                        if (skipErrors)
                        {
                            fileItem.Status = "Skipped";
                            fileItem.Details = e.Message;
                            break;
                        }

                        var result = await _messenger.Send(new ShowMessageBoxCustomMessage
                        {
                            Title = "Move", Message = $"Unable to move:\n\"{file.info.Name}\"\n{e.Message}",
                            Icon = Icon.Error,
                            ButtonDefinitions = new[]
                            {
                                new ButtonDefinition { Name = "Retry", IsDefault = true },
                                new ButtonDefinition { Name = "Skip" },
                                new ButtonDefinition { Name = "Skip All" },
                                new ButtonDefinition { Name = "Cancel", IsCancel = true }
                            },
                        }, (int)MessengerChannel.MoveChannelWindow);
                        switch (result)
                        {
                            case "Retry":
                                continue;
                            case "Skip All":
                                skipErrors = true;
                                goto case "Skip";
                            case "Skip":
                                fileItem.Status = "Skipped";
                                fileItem.Details = e.Message;
                                break;
                            default:
                                Cancel();
                                fileItem.Status = "Cancelled";
                                return;
                        }
                    }

                    break; // Goto next file
                }

                if (file.dl is not null)
                {
                    _downloadData.UpdateSaveTo(file.dl.Id, DestPath);
                    file.dl.SaveTo = DestPath;
                }

                if (_cancellationTokenSource.IsCancellationRequested)
                {
                    return;
                }
            }

            DeleteSourceDirs();

            _channelData.SetChannelPath(_channel, DestPath);
            _channel.Path = DestPath;
            IsFinished = true;
        }
        catch (Exception e)
        {
            Cancel();
            await _messenger.Send(new ShowMessageBoxMessage
                { Title = "Error", Message = e.Message, Icon = Icon.Error, ButtonDefinitions = ButtonEnum.Ok });
        }
    }

    private void DeleteSourceDirs()
    {
        try
        {
            Directory.Delete(Path.Combine(_channel.Path, ".thumbs"), false);
        }
        catch (Exception)
        {
            // ignored
        }

        try
        {
            Directory.Delete(_channel.Path, false);
        }
        catch (Exception)
        {
            // ignored
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        if (IsCancelled) return;
        IsCancelled = true;
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }

    public partial class FileItem : ObservableObject

    {
        [ObservableProperty] private string _status = "-";
        public required string Filename { get; set; }
        public string? Details { get; set; }
    }
}