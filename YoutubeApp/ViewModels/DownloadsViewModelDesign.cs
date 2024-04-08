using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls.Selection;
using DynamicData.Binding;
using YoutubeApp.Media;
using YoutubeApp.Models;

namespace YoutubeApp.ViewModels;

class DownloadsViewModelDesign : DownloadsViewModel
{
    public DownloadsViewModelDesign()
    {
        GridConfig = Settings.ColumnsConfig;
        var columnsSorted = GridConfig.OrderBy(x => x.Value.Order).Select(x => x.Value);
        ColumnOrdered = new ObservableCollection<ColumnConfig>(columnsSorted);

        Selection = new SelectionModel<Download>
        {
            SingleSelect = false
        };

        Downloads = new ObservableCollectionExtended<Download>()
        {
            new()
            {
                Title = "Test Video 01",
                Filename = "[2020.07.05][abcdef1] Test Video 01.mp4",
                Filesize = 1024 * 1024 * 57,
                BytesLoaded = 1024 * 1024 * 57,
                Duration = "1:07:19",
                ChannelTitle = "Fake Channel",
                SaveTo = @"C:\Videos",
                Completed = true,
                SelectedVariant = new SelectedVariant
                {
                    Description = "av01 1080p60 mp4a@192",
                    Id = 0,
                    VFormatId = "95",
                    AFormatId = "11",
                    VFormatItagNoDash = "95",
                    AFormatItagNoDash = "11",
                    VFormatProtocol = Protocol.Https,
                    AFormatProtocol = Protocol.Https,
                    VFormatThrottled = false,
                    AFormatThrottled = true,
                    VideoLmt = "100",
                    AudioLmt = "100",
                    IsApproxFilesize = false,
                    VCodec = "av01",
                    ACodec = "mp4a",
                    Width = 1920,
                    Height = 1080,
                    Fps = 60,
                    Abr = 192,
                },
                Chapters = null,
            },
            new()
            {
                Title = "Test Video 02",
                Filename = "[2020.11.16][abcdef2] Test Video 02.mp4",
                Filesize = 1024 * 1024 * 78,
                BytesLoaded = 1024 * 1024 * 40,
                Duration = "19:32",
                ChannelTitle = "Fake Channel",
                SaveTo = @"C:\Videos",
                Error = "Failed",
                SelectedVariant = new SelectedVariant
                {
                    Description = "vp9 1080p30 opus@128",
                    Id = 0,
                    VFormatId = "92",
                    AFormatId = "18",
                    VFormatItagNoDash = "92",
                    AFormatItagNoDash = "18",
                    VFormatProtocol = Protocol.Https,
                    AFormatProtocol = Protocol.Https,
                    VFormatThrottled = false,
                    AFormatThrottled = false,
                    VideoLmt = "100",
                    AudioLmt = "100",
                    IsApproxFilesize = true,
                    VCodec = "vp9",
                    ACodec = "opus",
                    Width = 1920,
                    Height = 1080,
                    Fps = 30,
                    Abr = 128,
                },
                Chapters = null,
            },
            new()
            {
                Title = "Test Video 03",
                Filename = "[2020.01.19][abcdef3] Test Video 03.mp4",
                Filesize = 1024 * 1024 * 320,
                BytesLoaded = 1024 * 1024 * 110,
                Duration = "1:44:04",
                ChannelTitle = "Fake Channel",
                SaveTo = @"C:\Videos",
                Speed = (int)(1024 * 1024 * 1.2),
                Downloading = true,
                Connections = 4,
                Enabled = true,
                SelectedVariant = new SelectedVariant
                {
                    Description = "av01 1080p60 mp4a@192",
                    Id = 0,
                    VFormatId = "95",
                    AFormatId = "11",
                    VFormatItagNoDash = "95",
                    AFormatItagNoDash = "11",
                    VFormatProtocol = Protocol.Https,
                    AFormatProtocol = Protocol.Https,
                    VFormatThrottled = false,
                    AFormatThrottled = true,
                    VideoLmt = "100",
                    AudioLmt = "100",
                    IsApproxFilesize = false,
                    VCodec = "av01",
                    ACodec = "mp4a",
                    Width = 1920,
                    Height = 1080,
                    Fps = 60,
                    Abr = 192,
                },
                Chapters = null,
            },
            new()
            {
                Title = "Test Video 04",
                Filename = "[2020.02.04][abcdef4] Test Video 04.mp4",
                Filesize = 1024 * 1024 * 156,
                BytesLoaded = 1024 * 1024 * 27,
                Duration = "57:04",
                ChannelTitle = "Fake Channel",
                SaveTo = @"C:\Videos",
                Enabled = true,
                SelectedVariant = new SelectedVariant
                {
                    Description = "avc1 720p60 mp4a@192",
                    Id = 0,
                    VFormatId = "62",
                    AFormatId = "11",
                    VFormatItagNoDash = "62",
                    AFormatItagNoDash = "11",
                    VFormatProtocol = Protocol.Https,
                    AFormatProtocol = Protocol.Https,
                    VFormatThrottled = false,
                    AFormatThrottled = false,
                    VideoLmt = "100",
                    AudioLmt = "100",
                    IsApproxFilesize = false,
                    VCodec = "avc1",
                    ACodec = "mp4a",
                    Width = 1280,
                    Height = 720,
                    Fps = 60,
                    Abr = 192,
                },
                Chapters = null,
            },
        };
    }
}