using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapper;

namespace YoutubeApp.Database.Migrations;

public static class Migration1
{
    public static void Apply(SQLiteConnection connection)
    {
        connection.Execute("PRAGMA foreign_keys=off");
        using var transaction = connection.BeginTransaction();

        // Add UniqueId, IncompleteCount and AddedVideoCount columns to Channels table

        connection.Execute(@"
            CREATE TABLE NewChannels (
                Id INTEGER NOT NULL,
                UniqueId TEXT NOT NULL,
                ListId TEXT NOT NULL,
                Title TEXT NOT NULL,
                Path TEXT NOT NULL,
                VideoCount INTEGER,
                IncompleteCount INTEGER NOT NULL,
                AddedVideoCount INTEGER NOT NULL DEFAULT 0,
                CategoryId INTEGER,
                LastUpdate TEXT,
                PRIMARY KEY (Id),
                FOREIGN KEY (CategoryId)
                    REFERENCES ChannelCategories (Id)
                    ON DELETE CASCADE,
                UNIQUE (ListId ASC)
            )", null, transaction);

        var channels = connection.Query("SELECT * FROM Channels");

        foreach (var channel in channels)
        {
            channel.UniqueId = $"UC{((string)channel.ListId)[2..]}";
            connection.Execute(
                @"INSERT INTO NewChannels (Id, UniqueId, ListId, Title, Path, VideoCount, CategoryId, LastUpdate, IncompleteCount)
                        VALUES (@Id, @UniqueId, @ListId, @Title, @Path, @VideoCount, @CategoryId, @LastUpdate, @VideoCount)",
                new
                {
                    channel.Id,
                    channel.UniqueId,
                    channel.ListId,
                    channel.Title,
                    channel.Path,
                    channel.VideoCount,
                    channel.CategoryId,
                    channel.LastUpdate,
                }, transaction);
        }

        connection.Execute(@"DROP TABLE Channels; ALTER TABLE NewChannels RENAME TO Channels;", null, transaction);

        // Add additional fields to Downloads.SelectedVariant

        var downloads = connection.Query("SELECT * FROM Downloads");

        foreach (var dl in downloads)
        {
            var selectedVariantJson = dl.SelectedVariant;
            var selectedVariant = JsonSerializer.Deserialize<JsonObject>(selectedVariantJson);
            var selectedVariantId = (int)selectedVariant["Id"];

            var variantsJson = (string)dl.Variants;
            var variants = JsonSerializer.Deserialize<IEnumerable<JsonObject>>(variantsJson);
            var variant = variants.FirstOrDefault(x => (int)x["Id"] == selectedVariantId);

            selectedVariant["VCodec"] = variant is not null ? (string)variant["VCodec"]! : "Unknown";
            selectedVariant["ACodec"] = variant is not null ? (string)variant["ACodec"]! : "Unknown";
            selectedVariant["Width"] = variant is not null ? (int)variant["Width"]! : 0;
            selectedVariant["Height"] = variant is not null ? (int)variant["Height"]! : 0;
            selectedVariant["Fps"] = variant is not null ? (float)variant["Fps"]! : 0;
            selectedVariant["Abr"] = variant is not null ? (float)variant["Abr"]! : 0;

            var rowsAffected = connection.Execute(
                "UPDATE Downloads SET SelectedVariant = @SelectedVariant WHERE Id = @Id", new
                {
                    dl.Id,
                    SelectedVariant = JsonSerializer.Serialize(selectedVariant),
                }, transaction);

            if (rowsAffected != 1)
                throw new Exception("Unexpected affected rows number.");
        }

        // Set user_version

        connection.Execute("PRAGMA user_version = 1", null, transaction);

        transaction.Commit();
        connection.Execute("PRAGMA foreign_keys=on");
    }
}