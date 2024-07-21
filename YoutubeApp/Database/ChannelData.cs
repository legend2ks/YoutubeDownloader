using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using Dapper;
using YoutubeApp.Media;
using YoutubeApp.Models;
using YoutubeApp.ViewModels;

namespace YoutubeApp.Database;

public class ChannelData
{
    private readonly SQLiteConnection _dbConn;
    private List<Channel>? _channels;

    public ChannelData(SqliteDbAccessProvider sqliteDbAccessProvider)
    {
        _dbConn = sqliteDbAccessProvider.Connection;
    }

    public List<Channel> GetChannels(bool useCache = true)
    {
        if (useCache && _channels is not null) return _channels;

        var result = _dbConn.Query<ChannelDTO>("SELECT * FROM Channels");
        var channels = result.Select(DatabaseUtils.ToChannel).OrderBy(x => x.Title).ToList();
        _channels = channels;
        return channels;
    }

    public IEnumerable<ChannelCategory> GetChannelCategories()
    {
        var result = _dbConn.Query<ChannelCategoryDTO>("SELECT * FROM ChannelCategories ORDER BY Position");
        var channelCategories = result.Select(DatabaseUtils.ToChannelCategory);
        return channelCategories;
    }

    public IEnumerable<Video> GetVideos(int channelId)
    {
        const string stmt = "SELECT * FROM Videos WHERE ChannelId = @ChannelId";
        var videos = _dbConn.Query<Video>(stmt, new { ChannelId = channelId });
        return videos;
    }

    public void AddChannel(Channel channel, PlaylistInfoEntry[] videos, string updateDateTime)
    {
        using var transaction = _dbConn.BeginTransaction();

        // Insert channel
        const string stmt =
            @"INSERT INTO Channels (UniqueId, ListId, Title, Path, LastUpdate, VideoCount, IncompleteCount)
            VALUES (@UniqueId, @ListId, @Title, @Path, @LastUpdate, @VideoCount, @IncompleteCount)
            RETURNING Id";
        var parameters = new
        {
            channel.UniqueId,
            channel.ListId,
            channel.Title,
            channel.Path,
            channel.VideoCount,
            channel.IncompleteCount,
            LastUpdate = updateDateTime,
        };

        var channelId = _dbConn.QuerySingle<int>(stmt, parameters, transaction);
        channel.Id = channelId;

        // Insert Videos
        const string insertVideoStmt = "INSERT INTO Videos (VideoId, ChannelId, Title, Duration, Timestamp)" +
                                       "VALUES (@VideoId, @ChannelId, @Title, @Duration, @Timestamp)";
        foreach (var video in videos.Reverse())
        {
            var rowsAffected = _dbConn.Execute(insertVideoStmt, new
            {
                VideoId = video.id,
                ChannelId = channel.Id,
                Title = video.title,
                Duration = video.duration != null
                    ? Utils.DurationStringFromSeconds((int)video.duration)
                    : "Not Available",
                Timestamp = video.timestamp ?? 0,
            }, transaction);
            if (rowsAffected != 1) throw new Exception("Unexpected affected rows number.");
        }

        transaction.Commit();

        _channels!.Add(channel);
    }

    public int UpdateChannel(Channel channel, PlaylistInfo playlistInfo,
        Dictionary<string, int> prevVideoIds, string updateDateTime)
    {
        using var transaction = _dbConn.BeginTransaction();

        var addedVideoCount = 0;

        // Update Videos
        const string insertVideoStmt = @"INSERT INTO Videos (VideoId, ChannelId, Title, Duration, Timestamp)
            VALUES (@VideoId, @ChannelId, @Title, @Duration, @Timestamp)";
        const string updateVideoStmt = @"UPDATE Videos SET Title = @Title, Duration = @Duration
            WHERE Id = @Id";

        int rowsAffected;
        foreach (var video in playlistInfo.entries.Reverse())
        {
            if (prevVideoIds.TryGetValue(video.id, out var id))
            {
                // Update existing video
                var updateVideoParams = new
                {
                    Id = id,
                    Title = video.title,
                    Duration =
                        video.duration != null ? Utils.DurationStringFromSeconds((int)video.duration) : "Not Available",
                };
                rowsAffected = _dbConn.Execute(updateVideoStmt, updateVideoParams, transaction);
                if (rowsAffected != 1) throw new Exception("Unexpected affected rows number.");
            }
            else
            {
                // Insert new video
                addedVideoCount++;

                var insertVideoParams = new
                {
                    VideoId = video.id,
                    ChannelId = channel.Id,
                    Title = video.title,
                    Duration =
                        video.duration != null ? Utils.DurationStringFromSeconds((int)video.duration) : "Not Available",
                    Timestamp = video.timestamp ?? 0,
                };
                rowsAffected = _dbConn.Execute(insertVideoStmt, insertVideoParams, transaction);
                if (rowsAffected != 1) throw new Exception("Unexpected affected rows number.");
            }
        }

        // Update channel
        const string stmt =
            @"UPDATE Channels SET 
                UniqueId = @UniqueId,
                Title = @Title,
                LastUpdate = @LastUpdate,
                VideoCount = @VideoCount,
                IncompleteCount = @IncompleteCount,
                AddedVideoCount = @AddedVideoCount
                WHERE Id = @Id";
        var parameters = new
        {
            channel.Id,
            UniqueId = playlistInfo.channel_id,
            Title = playlistInfo.channel,
            VideoCount = prevVideoIds.Count + addedVideoCount,
            IncompleteCount = channel.IncompleteCount + addedVideoCount,
            AddedVideoCount = channel.AddedVideoCount + addedVideoCount,
            LastUpdate = updateDateTime,
        };

        rowsAffected = _dbConn.Execute(stmt, parameters, transaction);
        if (rowsAffected != 1) throw new Exception("Unexpected affected rows number.");

        transaction.Commit();

        return addedVideoCount;
    }

    public void RemoveChannel(int id)
    {
        const string stmt = "DELETE FROM Channels WHERE Id = @Id";
        _channels!.Remove(_channels.First(x => x.Id == id));
        var rowsAffected = _dbConn.Execute(stmt, new { Id = id });
        if (rowsAffected != 1) throw new Exception("Unexpected affected rows number.");
    }

    public void UpdateWatchedState(int id, bool watched)
    {
        const string stmt = "UPDATE Videos SET WATCHED = @WATCHED WHERE Id = @Id";
        var rowsAffected = _dbConn.Execute(stmt, new { WATCHED = watched, Id = id });
        if (rowsAffected != 1) throw new Exception("Unexpected affected rows number.");
    }

    public void UpdateCategories(List<ChangedCategory> changedCategories)
    {
        const string insertSql =
            @"INSERT INTO ChannelCategories (Title, Position) VALUES (@Title, @Position) RETURNING Id";
        const string updateSql = "UPDATE ChannelCategories SET Title = @Title, Position = @Position WHERE Id = @Id";

        using var transaction = _dbConn.BeginTransaction();
        foreach (var cat in changedCategories)
        {
            if (cat.IsNew)
            {
                var id = _dbConn.QuerySingle<int>(insertSql,
                    new { cat.ChannelCategory!.Title, cat.Position },
                    transaction);
                cat.ChannelCategory.Id = id;
            }
            else if (cat.IsRemoved)
            {
                var channelIds = cat.ChannelCategory.Channels.Select(x => x.Id).ToArray();
                var rowsAffected = _dbConn.Execute("UPDATE Channels SET CategoryId = NULL WHERE Id IN @Ids",
                    new { Ids = channelIds },
                    transaction);
                if (rowsAffected != channelIds.Length) throw new Exception("Unexpected affected rows number.");

                rowsAffected = _dbConn.Execute("DELETE FROM ChannelCategories WHERE Id = @Id",
                    new { cat.ChannelCategory!.Id },
                    transaction);
                if (rowsAffected != 1) throw new Exception("Unexpected affected rows number.");
            }
            else
            {
                var rowsAffected = _dbConn.Execute(updateSql,
                    new { cat.ChannelCategory!.Title, cat.Position, cat.ChannelCategory.Id },
                    transaction);
                if (rowsAffected != 1) throw new Exception("Unexpected affected rows number.");
            }
        }

        transaction.Commit();
    }

    public void SetChannelCategory(Channel channel, int categoryId)
    {
        var sql = categoryId == 0
            ? "UPDATE Channels SET CategoryId = NULL WHERE Id = @Id"
            : "UPDATE Channels SET CategoryId = @CategoryId WHERE Id = @Id";
        var rowsAffected = _dbConn.Execute(sql, new { CategoryId = categoryId, channel.Id });
        if (rowsAffected != 1) throw new Exception("Unexpected affected rows number.");
    }

    public void SetChannelPath(Channel channel, string path)
    {
        const string sql = "UPDATE Channels SET Path = @Path WHERE Id = @Id";
        var rowsAffected = _dbConn.Execute(sql, new { Path = path, channel.Id });
        if (rowsAffected != 1) throw new Exception("Unexpected affected rows number.");
    }

    public void SetIncompleteCount(int channelId, int count)
    {
        var rowsAffected = _dbConn.Execute("UPDATE Channels SET IncompleteCount = @IncompleteCount WHERE Id = @Id",
            new { Id = channelId, IncompleteCount = count });
        if (rowsAffected != 1) throw new Exception("Unexpected affected rows number.");
    }

    public void SetAddedVideoCount(int channelId, int count)
    {
        var rowsAffected = _dbConn.Execute("UPDATE Channels SET AddedVideoCount = @AddedVideoCount WHERE Id = @Id",
            new { Id = channelId, AddedVideoCount = count });
        if (rowsAffected != 1) throw new Exception("Unexpected affected rows number.");
    }
}