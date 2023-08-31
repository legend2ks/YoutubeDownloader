using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Selection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YoutubeApp.Database;
using YoutubeApp.Downloader;
using YoutubeApp.Media;
using YoutubeApp.Models;

namespace YoutubeApp.ViewModels;

public partial class ChooseSingleFormatWindowViewModel : ViewModelBase
{
    private readonly DownloadData _downloadData;
    private readonly DownloaderUtils _downloaderUtils;
    private readonly Settings _settings;

    public partial class Container : ObservableObject
    {
        public required string Name { get; set; }

        [ObservableProperty] private bool _isChecked;

        [ObservableProperty] private bool _isAvailable;
    }

    private Download DownloadItem { get; }
    public List<VariantItem> Variants { get; set; } = new();

    public Dictionary<string, Container> Containers { get; set; } = new()
    {
        { "mp4", new Container { Name = "MP4", IsChecked = false } },
        { "mkv", new Container { Name = "MKV", IsChecked = false } },
        { "webm", new Container { Name = "WebM", IsChecked = false } }
    };

    public SelectionModel<VariantItem> Selection { get; set; } = new();

    [ObservableProperty] private bool _applyButtonEnabled;

    public bool NoVariant { get; set; }

    public ChooseSingleFormatWindowViewModel(Download downloadItem, DownloadData downloadData,
        DownloaderUtils downloaderUtils, Settings settings)
    {
        _downloadData = downloadData;
        _downloaderUtils = downloaderUtils;
        _settings = settings;
        DownloadItem = downloadItem;

        int? w = null;
        int? h = null;
        var alt = true;
        foreach (var v in downloadItem.Variants)
        {
            if (w is null || v.Width != w || v.Height != h)
            {
                alt = !alt;
                w = v.Width;
                h = v.Height;
            }

            var vformat = downloadItem.Formats[v.VFormatId];
            var aformat = downloadItem.Formats[v.AFormatId];
            var desc = $"{v.VFormatId} ({vformat.Protocol}) + {v.AFormatId} ({aformat.Protocol})";

            Variants.Add(new VariantItem { Variant = v, Color = alt ? "#353c43" : "#0000", Description = desc });
        }

        if (Design.IsDesignMode) return;

        Selection.SelectedIndex = downloadItem.SelectedVariant.Id;
        Selection.SelectionChanged += SelectionChanged;

        NoVariant = DownloadItem.SelectedVariant.Id == -1;
        if (NoVariant) return;

        var currentVariant = downloadItem.Variants[DownloadItem.SelectedVariant.Id];
        var containerOptions = Youtube.GetContainerOptions(currentVariant.VCodec, currentVariant.ACodec);
        foreach (var c in Containers)
        {
            c.Value.IsAvailable = containerOptions.Contains(c.Key);
        }

        Containers[downloadItem.Container].IsChecked = true;
        ApplyButtonEnabled = true;
    }

    private void SelectionChanged(object? sender, SelectionModelSelectionChangedEventArgs<VariantItem> e)
    {
        if (Selection.SelectedItem is null) return;
        var containerOptions = Youtube.GetContainerOptions(Selection.SelectedItem!.Variant.VCodec,
            Selection.SelectedItem!.Variant.ACodec);
        foreach (var c in Containers)
        {
            c.Value.IsAvailable = containerOptions.Contains(c.Key);
            if (c.Value is not { IsAvailable: false, IsChecked: true }) continue;

            c.Value.IsChecked = false;
            ApplyButtonEnabled = false;
        }
    }

    [RelayCommand]
    private void ApplyButtonClicked(Window window)
    {
        var chosenVariant = Selection.SelectedItem!.Variant;

        SelectedVariant selectedVariant;
        long bytesLoaded;

        if (chosenVariant.Id == DownloadItem.SelectedVariant.Id)
        {
            selectedVariant = DownloadItem.SelectedVariant;
            bytesLoaded = DownloadItem.BytesLoaded;
        }
        else
        {
            var vformat = DownloadItem.Formats[chosenVariant.VFormatId];
            var aformat = DownloadItem.Formats[chosenVariant.AFormatId];

            selectedVariant = new SelectedVariant
            {
                Id = chosenVariant.Id,
                VFormatId = chosenVariant.VFormatId,
                AFormatId = chosenVariant.AFormatId,
                VFormatItagNoDash = chosenVariant.VFormatId.Replace("-dash", ""),
                AFormatItagNoDash = chosenVariant.AFormatId.Replace("-dash", ""),
                VFormatProtocol = vformat.Protocol,
                AFormatProtocol = aformat.Protocol,
                VFormatThrottled = vformat.Throttled,
                AFormatThrottled = aformat.Throttled,
                VideoLmt = Utils.ExtractLmt(vformat.Url!),
                AudioLmt = Utils.ExtractLmt(aformat.Url!),
                Description = Utils.GenerateVariantDescription(chosenVariant, vformat.Protocol, aformat.Protocol,
                    vformat.Throttled, aformat.Throttled),
                IsApproxFilesize = chosenVariant.IsApproxFilesize,
                VCodec = chosenVariant.VCodec,
                ACodec = chosenVariant.ACodec,
                Width = chosenVariant.Width,
                Height = chosenVariant.Height,
                Fps = chosenVariant.Fps,
                Abr = chosenVariant.Abr,
            };

            bytesLoaded = 0;
        }

        var container = Containers.First(x => x.Value.IsChecked).Key;

        var isChannelVideo = Utils.IsSamePath(DownloadItem.Channel?.Path, DownloadItem.SaveTo);
        var filenameTemplate = isChannelVideo ? Settings.DefaultFilenameTemplate : _settings.FilenameTemplate;
        var filename = Youtube.GenerateFilename(filenameTemplate, DownloadItem.VideoId, DownloadItem.Title,
            container, chosenVariant.Fps, DownloadItem.ChannelTitle, DownloadItem.UploadDate, chosenVariant.Width,
            chosenVariant.Height, chosenVariant.VCodec, chosenVariant.ACodec, chosenVariant.Abr);

        _downloadData.UpdateSelectedVariant(DownloadItem.Id, selectedVariant, container, filename,
            chosenVariant.Filesize, bytesLoaded);

        DownloadItem.Filename = filename;
        _downloaderUtils.DeleteUselessFiles(DownloadItem, selectedVariant);
        DownloadItem.ChangeSelectedVariant(selectedVariant);
        DownloadItem.Filesize = chosenVariant.Filesize;
        DownloadItem.Container = container;
        DownloadItem.BytesLoaded = bytesLoaded;

        window.Close();
    }

    [RelayCommand]
    private void ContainerChanged(string selectedContainer)
    {
        ApplyButtonEnabled = true;
    }
}