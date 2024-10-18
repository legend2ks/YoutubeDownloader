using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data.SQLite;
using System.Linq;
using System.Text.Json;
using Dapper;
using YoutubeApp.Downloader;
using YoutubeApp.Media;
using YoutubeApp.Models;

namespace YoutubeApp.Database;

public class DownloadData
{
    private readonly SQLiteConnection _dbConn;
    public IReadOnlyList<Download> Downloads;

    public DownloadData(SqliteDbAccessProvider sqliteDbAccessProvider)
    {
        _dbConn = sqliteDbAccessProvider.Connection;
    }

    public IEnumerable<Download> GetDownloadList()
    {
        var result = _dbConn.Query<DownloadDTO>("SELECT * FROM Downloads ORDER BY Priority");
        var downloads = result.Select(DatabaseUtils.ToDownload).ToList();
        return downloads;
    }

    public void AddDownload(Download dl)
    {
        const string stmt = """
                            INSERT INTO
                            Downloads (VideoId, Title, SelectedVariant, Uuid, ChannelId, Container, Variants, Formats, Chapters, Duration, Filename, SaveTo, ChannelTitle, UploadDate, Filesize, BytesLoaded, MissingFormats, Priority)
                            VALUES (@VideoId, @Title, @SelectedVariant, @Uuid, @ChannelId, @Container, @Variants, @Formats, @Chapters, @Duration, @Filename, @SaveTo, @ChannelTitle, @UploadDate, @Filesize, @BytesLoaded, @MissingFormats, (SELECT COUNT(*)+1 FROM Downloads))
                            RETURNING Id, Priority
                            """;

        var parameters = new
        {
            dl.VideoId,
            dl.Title,
            SelectedVariant = JsonSerializer.Serialize(dl.SelectedVariant),
            dl.Uuid,
            dl.ChannelId,
            dl.Container,
            Variants = JsonSerializer.Serialize(dl.Variants),
            Formats = JsonSerializer.Serialize(dl.Formats),
            Chapters = dl.Chapters is not null ? JsonSerializer.Serialize(dl.Chapters) : null,
            dl.Duration,
            dl.Filename,
            dl.SaveTo,
            dl.ChannelTitle,
            dl.UploadDate,
            dl.Filesize,
            BytesLoaded = 0,
            dl.MissingFormats,
        };

        var (id, priority) = _dbConn.QuerySingle<(int id, int priority)>(stmt, parameters);

        dl.Id = id;
        dl.Priority = priority;
    }

    public void UpdateDownload(
        int id,
        string title,
        SelectedVariant selectedVariant,
        List<Variant> variants,
        Dictionary<string, Format> formats,
        List<Chapter>? chapters,
        string duration,
        bool missingFormats,
        long filesize,
        string channelId,
        string channelTitle,
        string uploadDate,
        bool enabled
    )
    {
        const string stmt = """
                            UPDATE Downloads SET
                            Title = @Title,
                            SelectedVariant = @SelectedVariant,
                            Variants = @Variants,
                            Formats = @Formats,
                            Chapters = @Chapters,
                            Duration = @Duration,
                            Filesize = @Filesize,
                            ChannelId = @ChannelId,
                            ChannelTitle = @ChannelTitle,
                            UploadDate = @UploadDate,
                            MissingFormats = @MissingFormats,
                            Enabled = @Enabled
                            WHERE Id = @Id
                            """;

        var parameters = new
        {
            Id = id,
            Title = title,
            SelectedVariant = JsonSerializer.Serialize(selectedVariant),
            Variants = JsonSerializer.Serialize(variants),
            Formats = JsonSerializer.Serialize(formats),
            Chapters = chapters is not null ? JsonSerializer.Serialize(chapters) : null,
            Duration = duration,
            Filesize = filesize,
            ChannelId = channelId,
            ChannelTitle = channelTitle,
            UploadDate = uploadDate,
            MissingFormats = missingFormats,
            Enabled = enabled,
        };

        var rowsAffected = _dbConn.Execute(stmt, parameters);
        if (rowsAffected != 1) throw new Exception("Unexpected affected rows number.");
    }


    public void RemoveDownloads(IReadOnlyList<Download> downloads)
    {
        using var transaction = _dbConn.BeginTransaction();

        foreach (var dl in downloads)
        {
            var rowsAffected = _dbConn.Execute("DELETE FROM Downloads WHERE Id = @Id",
                new { dl.Id },
                transaction);

            if (rowsAffected != 1)
            {
                throw new Exception("Unexpected affected rows number.");
            }

            // Remove DownloadFiles
            _dbConn.Execute("DELETE FROM DownloadFiles WHERE DownloadId = @DownloadId",
                new { DownloadId = dl.Id },
                transaction);
        }

        // Update priorities
        var removedPriorities = downloads.Select(dl => dl.Priority)
            .ToImmutableSortedSet().Reverse();

        foreach (var pr in removedPriorities)
        {
            _dbConn.Execute("UPDATE Downloads SET priority = priority - 1 WHERE priority > @Priority",
                new { Priority = pr },
                transaction);
        }

        transaction.Commit();
    }

    public void UpdateDownloadPriorities(List<(int id, int offset)> changes)
    {
        using var transaction = _dbConn.BeginTransaction();

        foreach (var item in changes)
        {
            var rowsAffected = _dbConn.Execute("UPDATE Downloads SET Priority = Priority + @Offset WHERE Id = @Id",
                new { Id = item.id, Offset = item.offset },
                transaction);

            if (rowsAffected != 1) throw new Exception("Unexpected affected rows number.");
        }

        transaction.Commit();
    }

    public void UpdateDownloadEnabledState(int[] ids, bool enabled)
    {
        const string stmt = "UPDATE Downloads SET Enabled = @Enabled WHERE Id IN @Ids";
        var rowsAffected = _dbConn.Execute(stmt, new { Enabled = enabled, Ids = ids });
        if (rowsAffected != ids.Length) throw new Exception("Unexpected affected rows number.");
    }

    public void UpdateSelectedVariant(int id, SelectedVariant selectedVariant, string container, string filename,
        long filesize, long bytesLoaded)
    {
        const string stmt = @"UPDATE Downloads SET
            SelectedVariant = @SelectedVariant,
            Container = @Container,
            Filename = @Filename,
            Filesize = @Filesize,
            BytesLoaded = @BytesLoaded
            WHERE Id = @Id";
        var rowsAffected = _dbConn.Execute(stmt,
            new
            {
                Id = id, SelectedVariant = JsonSerializer.Serialize(selectedVariant), Container = container,
                Filename = filename, Filesize = filesize, BytesLoaded = bytesLoaded
            });
        if (rowsAffected != 1) throw new Exception("Unexpected affected rows number.");
    }

    public Dictionary<string, DownloadFile> GetDownloadFiles(int downloadId)
    {
        const string stmt = "SELECT * FROM DownloadFiles WHERE DownloadId = @DownloadId";
        var result = _dbConn.Query<DownloadFile>(stmt, new { DownloadId = downloadId });
        return result.ToDictionary(fileStat => fileStat.FileId);
    }

    public void SaveDownloadFile(int downloadId, string fileId, long filesize, bool update = false)
    {
        const string insertStmt =
            "INSERT INTO DownloadFiles (DownloadId, FileId, Filesize) VALUES (@DownloadId, @FileId, @Filesize)";
        const string updateStmt =
            "UPDATE DownloadFiles SET Filesize = @Filesize WHERE DownloadId = @DownloadId AND FileId = @FileId";

        var rowsAffected = _dbConn.Execute(!update ? insertStmt : updateStmt,
            new { DownloadId = downloadId, FileId = fileId, Filesize = filesize });
        if (rowsAffected != 1) throw new Exception("Unexpected affected rows number.");
    }

    public void RemoveDownloadFiles(int downloadId, string? fileIdPrefix = null)
    {
        if (fileIdPrefix is null)
        {
            _dbConn.Execute("DELETE FROM DownloadFiles WHERE DownloadId = @DownloadId",
                new { DownloadId = downloadId });
        }
        else
        {
            _dbConn.Execute("DELETE FROM DownloadFiles WHERE DownloadId = @DownloadId AND FileId LIKE '@FileIdPrefix%'",
                new { DownloadId = downloadId, FileIdPrefix = fileIdPrefix });
        }
    }

    public void SaveDownloadCompleted(int downloadId, long bytesLoaded)
    {
        const string stmt =
            "UPDATE Downloads SET BytesLoaded = @BytesLoaded, Completed = 1, Enabled = 0 WHERE Id = @Id";
        var rowsAffected = _dbConn.Execute(stmt, new { Id = downloadId, BytesLoaded = bytesLoaded });
        if (rowsAffected != 1) throw new Exception("Unexpected affected rows number.");

        _dbConn.Execute("DELETE FROM DownloadFiles WHERE DownloadId = @DownloadId", new { DownloadId = downloadId });
    }

    public void SaveDownloadProgress(int downloadId, long bytesLoaded)
    {
        const string stmt = "UPDATE Downloads SET BytesLoaded = @BytesLoaded WHERE Id = @Id";
        var rowsAffected = _dbConn.Execute(stmt, new { Id = downloadId, BytesLoaded = bytesLoaded });
        if (rowsAffected != 1) throw new Exception("Unexpected affected rows number.");
    }

    public void UpdateFilename(int downloadId, string filename)
    {
        const string stmt = "UPDATE Downloads SET Filename = @Filename WHERE Id = @Id";
        var rowsAffected = _dbConn.Execute(stmt, new { Id = downloadId, Filename = filename });
        if (rowsAffected != 1) throw new Exception("Unexpected affected rows number.");
    }

    public void UpdateSaveTo(int downloadId, string saveToPath)
    {
        const string stmt = "UPDATE Downloads SET SaveTo = @SaveTo WHERE Id = @Id";
        var rowsAffected = _dbConn.Execute(stmt, new { Id = downloadId, SaveTo = saveToPath });
        if (rowsAffected != 1) throw new Exception("Unexpected affected rows number.");
    }
}