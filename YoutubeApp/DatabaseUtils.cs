using System;
using System.Collections.Generic;
using System.Text.Json;
using YoutubeApp.Media;
using YoutubeApp.Models;

namespace YoutubeApp;

internal static class DatabaseUtils
{
    public static Download ToDownload(DownloadDTO dto)
    {
        return new Download
        {
            Id = dto.Id,
            BytesLoaded = dto.BytesLoaded,
            ChannelTitle = dto.ChannelTitle,
            Container = dto.Container,
            Duration = dto.Duration,
            Enabled = dto.Enabled,
            Completed = dto.Completed,
            Filename = dto.Filename,
            Filesize = dto.Filesize,
            Priority = dto.Priority,
            SaveTo = dto.SaveTo,
            Title = dto.Title,
            Uuid = dto.Uuid,
            VideoId = dto.VideoId,
            UploadDate = DateTime.Parse(dto.UploadDate).ToString("yyyy-MM-dd"),
            Variants = JsonSerializer.Deserialize<List<Variant>>(dto.Variants),
            Formats = JsonSerializer.Deserialize<Dictionary<string, Format>>(dto.Formats),
            SelectedVariant = JsonSerializer.Deserialize<SelectedVariant>(dto.SelectedVariant),
        };
    }

    public static Channel ToChannel(ChannelDTO dto)
    {
        return new Channel
        {
            Id = dto.Id,
            ListId = dto.ListId,
            Title = dto.Title,
            Path = dto.Path,
            CategoryId = dto.CategoryId,
            VideoCount = dto.VideoCount,
            LastUpdate = DateTime.Parse(dto.LastUpdate).ToLocalTime().ToString(),
        };
    }

    public static ChannelCategory ToChannelCategory(ChannelCategoryDTO dto)
    {
        return new ChannelCategory
        {
            Id = dto.Id,
            Title = dto.Title,
            Parent = dto.Parent,
        };
    }
}