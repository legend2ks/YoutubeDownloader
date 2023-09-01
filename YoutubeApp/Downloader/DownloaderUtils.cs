using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using YoutubeApp.Database;
using YoutubeApp.Extensions;
using YoutubeApp.Media;
using YoutubeApp.Models;

namespace YoutubeApp.Downloader;

public class DownloaderUtils
{
    private readonly ILogger<DownloaderUtils> _logger;
    private readonly DownloadData _downloadData;

    public DownloaderUtils(ILogger<DownloaderUtils> logger, DownloadData downloadData)
    {
        _logger = logger;
        _downloadData = downloadData;
    }

    public void DeleteUselessFiles(Download dl, SelectedVariant selectedVariant)
    {
        if (dl.SelectedVariant.VFormatItagNoDash != selectedVariant.VFormatItagNoDash
            || dl.SelectedVariant.VFormatProtocol != selectedVariant.VFormatProtocol)
        {
            DeleteTempFiles(dl, "v");
        }

        if (dl.SelectedVariant.AFormatItagNoDash != selectedVariant.AFormatItagNoDash
            || dl.SelectedVariant.AFormatProtocol != selectedVariant.AFormatProtocol)
        {
            DeleteTempFiles(dl, "a");
        }
    }

    public void DeleteTempFiles(IEnumerable<Download> downloads)
    {
        foreach (var dl in downloads)
        {
            DeleteTempFiles(dl);
        }
    }

    public void DeleteTempFiles(Download dl, string? fileIdPrefix = null)
    {
        var pattern = fileIdPrefix switch
        {
            "v" => @$"^{dl.VideoId}_{dl.Uuid}_{dl.SelectedVariant.VFormatItagNoDash}(?:_\d+)?\.part(?:\.aria2)?$",
            "a" => @$"^{dl.VideoId}_{dl.Uuid}_{dl.SelectedVariant.AFormatItagNoDash}(?:_\d+)?\.part(?:\.aria2)?$",
            null => @$"^{dl.VideoId}_{dl.Uuid}_\d+(?:_\d+)?\.part(?:\.aria2)?$",
            _ => throw new ArgumentOutOfRangeException(nameof(fileIdPrefix), fileIdPrefix, null)
        };

        var filenameRegex = new Regex(pattern);

        if (!Directory.Exists(dl.SaveTo))
            return;
        var dirFiles = Directory.GetFiles(dl.SaveTo).ToList();
        var directoriesToDelete = new List<string>();

        if (fileIdPrefix is "v" or null && dl.SelectedVariant.VFormatProtocol == Protocol.HttpDashSegments)
        {
            var fragmentsDirectory =
                Path.Combine(dl.SaveTo, $"{dl.VideoId}_{dl.Uuid}_{dl.SelectedVariant.VFormatItagNoDash}");
            directoriesToDelete.Add(fragmentsDirectory);
            if (Directory.Exists(fragmentsDirectory))
                dirFiles.AddRange(Directory.GetFiles(fragmentsDirectory));
        }

        if (fileIdPrefix is "a" or null && dl.SelectedVariant.AFormatProtocol == Protocol.HttpDashSegments)
        {
            var fragmentsDirectory =
                Path.Combine(dl.SaveTo, $"{dl.VideoId}_{dl.Uuid}_{dl.SelectedVariant.AFormatItagNoDash}");
            directoriesToDelete.Add(fragmentsDirectory);
            if (Directory.Exists(fragmentsDirectory))
                dirFiles.AddRange(Directory.GetFiles(fragmentsDirectory));
        }

        foreach (var file in dirFiles)
        {
            var filename = Path.GetFileName(file);
            var match = filenameRegex.IsMatch(filename);
            if (!match) continue;
            try
            {
                File.Delete(file);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to delete temp file {Filename} of DownloadId {DownloadId}",
                    filename, dl.Id);
            }
        }

        foreach (var dir in directoriesToDelete)
        {
            try
            {
                Directory.Delete(dir);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Deleting fragments directory failed {DownloadId} {Path}", dl.Id, dir);
            }
        }

        // Remove file stats
        _downloadData.RemoveDownloadFiles(dl.Id, fileIdPrefix);
    }

    public void DeleteCompletedFiles(IEnumerable<Download?> downloads)
    {
        foreach (var dl in downloads)
        {
            if (!dl.Completed) continue;
            var filepath = Path.Combine(dl.SaveTo, dl.Filename);
            if (!File.Exists(filepath)) continue;

            try
            {
                File.Delete(filepath);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to delete downloaded file {Filename} of DownloadId {DownloadId}",
                    dl.Filename, dl.Id);
            }
        }
    }

    public List<string> MoveFiles(IEnumerable<Download> downloadItems, string destPath)
    {
        // Get destination path free space
        var destDriveInfo = new DriveInfo(destPath);
        var destFreeSpace = destDriveInfo.AvailableFreeSpace;

        var errors = new List<string>();

        foreach (var dl in downloadItems)
        {
            if (Utils.IsSamePath(dl.SaveTo, destPath))
            {
                continue;
            }

            _logger.LogDebug("Move Files: DownloadItem {DownloadId}", dl.Id);

            var srcDriveInfo = new DriveInfo(dl.SaveTo);
            var sameDrive = srcDriveInfo.Name == destDriveInfo.Name;
            var isChannelVideo = Utils.IsSamePath(dl.Channel?.Path, destPath);
            var fileName = isChannelVideo
                ? Youtube.GenerateFilename(Settings.DefaultFilenameTemplate, dl.VideoId, dl.Title,
                    dl.Container, dl.SelectedVariant.Fps, dl.ChannelTitle, dl.UploadDate, dl.SelectedVariant.Width,
                    dl.SelectedVariant.Height, dl.SelectedVariant.VCodec, dl.SelectedVariant.ACodec,
                    dl.SelectedVariant.Abr)
                : dl.Filename;

            if (dl.Completed)
            {
                var downloadedFilepath = Path.Combine(dl.SaveTo, dl.Filename);
                if (File.Exists(downloadedFilepath))
                {
                    var destFilepath = Path.Combine(destPath, fileName);
                    var filesize = new FileInfo(downloadedFilepath).Length;

                    if (!sameDrive && filesize > destFreeSpace)
                    {
                        _logger.LogError("Move Files: Not enough space for {Filename}", dl.Filename);
                        errors.Add($"Not enough space: {dl.Title}");
                        continue;
                    }

                    try
                    {
                        File.Move(downloadedFilepath, destFilepath, true);
                        if (!sameDrive)
                            destFreeSpace -= filesize;
                    }
                    catch (FileNotFoundException)
                    {
                        _logger.LogDebug("Move Files: File not found {Filename}", dl.Filename);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Move Files: Unable to move file {Filename}", dl.Filename);
                        errors.Add($"{e.Message}: {dl.Title}");
                        continue;
                    }
                }
            }
            else // Not completed, Move temp files
            {
                if (Directory.Exists(dl.SaveTo))
                {
                    var filesToMove = new List<(string dir, List<FileInfo> files)>();
                    var directories = new List<(string dir, string[] files)> { ("", Directory.GetFiles(dl.SaveTo)) };

                    if (dl.SelectedVariant.VFormatProtocol == Protocol.HttpDashSegments)
                    {
                        var fragmentsDirectory = $"{dl.VideoId}_{dl.Uuid}_{dl.SelectedVariant.VFormatItagNoDash}";
                        var fragmentsDirectoryPath = Path.Combine(dl.SaveTo, fragmentsDirectory);
                        if (Directory.Exists(fragmentsDirectoryPath))
                        {
                            directories.Add((fragmentsDirectory, Directory.GetFiles(fragmentsDirectoryPath)));
                        }
                    }

                    if (dl.SelectedVariant.AFormatProtocol == Protocol.HttpDashSegments)
                    {
                        var fragmentsDirectory = $"{dl.VideoId}_{dl.Uuid}_{dl.SelectedVariant.AFormatItagNoDash}";
                        var fragmentsDirectoryPath = Path.Combine(dl.SaveTo, fragmentsDirectory);
                        if (Directory.Exists(fragmentsDirectoryPath))
                        {
                            directories.Add((fragmentsDirectory, Directory.GetFiles(fragmentsDirectoryPath)));
                        }
                    }

                    var pattern = @$"^{dl.VideoId}_{dl.Uuid}_\d+(?:_\d+)?\.part(?:\.aria2)?$";
                    var filenameRegex = new Regex(pattern);
                    long totalFilesize = 0;

                    foreach (var (dir, files) in directories)
                    {
                        var matchedFiles = new List<FileInfo>();
                        foreach (var filepath in files)
                        {
                            var filename = Path.GetFileName(filepath);
                            var match = filenameRegex.IsMatch(filename);
                            if (!match) continue;

                            var fileInfo = new FileInfo(filepath);
                            matchedFiles.Add(fileInfo);
                            totalFilesize += fileInfo.Length;
                        }

                        filesToMove.Add((dir, matchedFiles));
                    }

                    if (!sameDrive && totalFilesize > destFreeSpace)
                    {
                        _logger.LogError("Move Files: Not enough space for {Filename}", dl.Filename);
                        errors.Add($"Not enough space: {dl.Title}");
                        continue;
                    }

                    // Move files
                    foreach (var (dir, files) in filesToMove)
                    {
                        var destDirectory = Path.Combine(destPath, dir);
                        if (!Directory.Exists(destDirectory))
                        {
                            try
                            {
                                Directory.CreateDirectory(destDirectory);
                            }
                            catch (Exception e)
                            {
                                _logger.LogError(e, "Move Files: Unable to create directory {Directory}",
                                    destDirectory);
                                errors.Add($"{e.Message}: {dl.Title}");
                                continue;
                            }
                        }

                        foreach (var file in files)
                        {
                            try
                            {
                                var destFilePath = Path.Combine(destDirectory, file.Name);
                                File.Move(file.FullName, destFilePath, true);
                                if (!sameDrive)
                                    destFreeSpace -= file.Length;
                            }
                            catch (Exception e)
                            {
                                _logger.LogError(e, "Move Files: Unable to move file {Filename}", dl.Filename);
                                errors.Add($"{e.Message}: {dl.Title}");
                            }
                        }

                        if (dir.Length == 0) continue;
                        try
                        {
                            Directory.Delete(Path.Combine(dl.SaveTo, dir));
                        }
                        catch (Exception e)
                        {
                            _logger.LogError(e, "Move Files: Unable to delete source directory {Directory}",
                                dl.Filename);
                        }
                    }
                }
            }

            // Update Database
            if (isChannelVideo)
            {
                dl.Filename = fileName;
                _downloadData.UpdateFilename(dl.Id, dl.Filename);
            }

            _downloadData.UpdateSaveTo(dl.Id, destPath);
            dl.SaveTo = destPath;
        }

        return errors;
    }

    public bool CheckAvailableFreeSpace(string path, long requiredSpace)
    {
        // Get the target path
        path = new DirectoryInfo(path).GetActualPath();

        // Get drive's free space
        DriveInfo driveInfo;
        try
        {
            driveInfo = new DriveInfo(path);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Destination drive info is not available");
            return false;
        }

        var freeSpace = driveInfo.AvailableFreeSpace;
        return freeSpace > requiredSpace;
    }
}