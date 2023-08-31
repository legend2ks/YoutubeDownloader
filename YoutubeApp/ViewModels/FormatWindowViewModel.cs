using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YoutubeApp.Database;
using YoutubeApp.Downloader;
using YoutubeApp.Media;
using YoutubeApp.Models;
using SelectionChangedEventArgs = YoutubeApp.Models.SelectionChangedEventArgs;

namespace YoutubeApp.ViewModels;

public class CommonVariant
{
    public required Variant Variant { get; init; }
    public Dictionary<int, Download> Downloads { get; } = new();
}

public partial class Container : ObservableObject
{
    public required string Name { get; set; }

    [ObservableProperty] private int _index;
}

public partial class FormatWindowViewModel : ViewModelBase
{
    private readonly DownloadData _downloadData;
    private readonly DownloaderUtils _downloaderUtils;
    private readonly Settings _settings;
    private IReadOnlyList<Download> Downloads { get; }

    [ObservableProperty] private ObservableCollection<ComposedVariant> _composedVariants;

    [ObservableProperty] private ObservableCollection<Container> _containerPreference = new()
    {
        new Container { Index = 0, Name = "WEBM" },
        new Container { Index = 1, Name = "MP4" },
        new Container { Index = 2, Name = "MKV" },
    };

    [ObservableProperty] private int _remainingVideoCount;


    public FormatWindowViewModel(IReadOnlyList<Download> downloads, DownloadData downloadData,
        DownloaderUtils downloaderUtils, Settings settings)
    {
        _downloadData = downloadData;
        _downloaderUtils = downloaderUtils;
        _settings = settings;
        Downloads = downloads;

        var variants = ProcessVariants(downloads);

        var composedVariant = new ComposedVariant(variants, downloads.ToList());
        composedVariant.SelectionChanged += ComposedVariant_SelectionChanged;
        ComposedVariants = new ObservableCollection<ComposedVariant>
        {
            composedVariant
        };

        ComposedVariant.Undo += ComposedVariant_Undo;
    }

    protected FormatWindowViewModel()
    {
    }

    private void ComposedVariant_Undo(object? sender, UndoEventArgs e)
    {
        var composedVariant = sender as ComposedVariant;
        var newCount = 0;
        foreach (var cv in ComposedVariants)
        {
            if (cv == composedVariant)
            {
                break;
            }

            newCount++;
        }

        while (ComposedVariants.Count > newCount)
        {
            ComposedVariants.RemoveAt(ComposedVariants.Count - 1);
        }

        composedVariant = new ComposedVariant(e.FilteredVariants, e.Downloads);
        composedVariant.SelectionChanged += ComposedVariant_SelectionChanged;
        ComposedVariants.Add(composedVariant);

        RemainingVideoCount = 0;
    }

    private void ComposedVariant_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        RemainingVideoCount = e.RemainingDownloadCount;
    }

    private static List<CommonVariant> ProcessVariants(IEnumerable<Download> downloads)
    {
        var commonVariants = new Dictionary<string, CommonVariant>();

        foreach (var dl in downloads)
        {
            foreach (var variant in dl.Variants)
            {
                var sign = variant.VCodec + "+" + variant.ACodec + "+" + variant.Width + "x" + variant.Height + "+" +
                           variant.Fps + "+" + variant.Abr;
                if (commonVariants.TryGetValue(sign, out var value))
                {
                    value.Downloads.TryAdd(dl.Id, dl);
                }
                else
                {
                    commonVariants[sign] = new CommonVariant
                    {
                        Variant = variant
                    };
                    commonVariants[sign].Downloads.Add(dl.Id, dl);
                }
            }
        }

        return commonVariants.Values.ToList();
    }

    [RelayCommand]
    private void AddVariantButtonClicked()
    {
        var currentVariant = ComposedVariants.Last();
        var downloadIds = currentVariant.CompatibleDownloadIds;
        var remainingDownloads = currentVariant.Downloads.FindAll(x => !downloadIds.Contains(x.Id));
        var remainingVariants = ProcessVariants(remainingDownloads);
        currentVariant.SetDone();

        var composedVariant = new ComposedVariant(remainingVariants, remainingDownloads);
        composedVariant.SelectionChanged += ComposedVariant_SelectionChanged;
        ComposedVariants.Add(composedVariant);

        RemainingVideoCount = 0;
    }

    [RelayCommand]
    private void ApplyButtonClicked(Window window)
    {
        foreach (var cvar in ComposedVariants)
        {
            var selectedVideoCodecs = cvar.VideoCodecs.Where(x => x.Value.Checked).Select(x => x.Key).ToHashSet();
            var selectedAudioCodecs = cvar.AudioCodecs.Where(x => x.Value.Checked).Select(x => x.Key).ToHashSet();

            foreach (var downloadId in cvar.CompatibleDownloadIds)
            {
                var dl = Downloads.First(x => x.Id == downloadId);
                var variantsQuery = dl.Variants.AsQueryable();

                // Video Codec
                if (cvar.VideoCodecListEnabled == false)
                {
                    variantsQuery = variantsQuery.Where(x => selectedVideoCodecs.Contains(x.VCodec));
                }

                // Audio Codec
                if (cvar.AudioCodecListEnabled == false)
                {
                    variantsQuery = variantsQuery.Where(x => selectedAudioCodecs.Contains(x.ACodec));
                }

                // Resolution
                if (cvar.SelectedResolution != -1)
                {
                    var resolution = cvar.Resolutions.ElementAt(cvar.SelectedResolution).Key;
                    var res = resolution.Split('x');
                    var width = int.Parse(res[0]);
                    var height = int.Parse(res[1]);
                    variantsQuery = variantsQuery.Where(x => x.Width == width && x.Height == height);
                }

                // FPS
                if (cvar.SelectedFps != -1)
                {
                    var fps = cvar.FpsList.ElementAt(cvar.SelectedFps).Key;
                    variantsQuery = variantsQuery.Where(x => x.Fps == fps);
                }

                // Audio Bitrate
                if (cvar.SelectedAudioBitrate != -1)
                {
                    var abr = cvar.AudioBitrates.ElementAt(cvar.SelectedAudioBitrate).Key;
                    variantsQuery = variantsQuery.Where(x => x.Abr == abr);
                }

                var variants = variantsQuery.ToList();
                var bestVariant = variants[0];
                variants = variants.FindAll(x => x.Width == bestVariant.Width
                                                 && x.Height == bestVariant.Height
                                                 && x.Fps == bestVariant.Fps
                                                 && x.Abr == bestVariant.Abr);
                variants.Sort((a, b) => b.Filesize < a.Filesize ? 1 : -1);

                var chosenVariant = variants[0];

                var containerOptions = Youtube.GetContainerOptions(chosenVariant.VCodec, chosenVariant.ACodec);

                string? container = null;

                foreach (var c in ContainerPreference)
                {
                    foreach (var co in containerOptions)
                    {
                        if (!co.Equals(c.Name, StringComparison.OrdinalIgnoreCase)) continue;
                        container = co;
                        break;
                    }

                    if (container is not null) break;
                }

                SelectedVariant selectedVariant;
                long bytesLoaded;

                if (chosenVariant.Id == dl.SelectedVariant.Id)
                {
                    selectedVariant = dl.SelectedVariant;
                    bytesLoaded = dl.BytesLoaded;
                }
                else
                {
                    var vformat = dl.Formats[chosenVariant.VFormatId];
                    var aformat = dl.Formats[chosenVariant.AFormatId];

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
                        VideoLmt = Utils.ExtractLmt(vformat.Url),
                        AudioLmt = Utils.ExtractLmt(aformat.Url),
                        Description = Utils.GenerateVariantDescription(chosenVariant, vformat.Protocol,
                            aformat.Protocol, vformat.Throttled, aformat.Throttled),
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

                var isChannelVideo = Utils.IsSamePath(dl.Channel?.Path, dl.SaveTo);
                var filenameTemplate = isChannelVideo ? Settings.DefaultFilenameTemplate : _settings.FilenameTemplate;
                var filename = Youtube.GenerateFilename(filenameTemplate, dl.VideoId, dl.Title, container,
                    chosenVariant.Fps, dl.ChannelTitle, dl.UploadDate, chosenVariant.Width, chosenVariant.Height,
                    chosenVariant.VCodec, chosenVariant.ACodec, chosenVariant.Abr);

                _downloadData.UpdateSelectedVariant(downloadId, selectedVariant, container, filename,
                    chosenVariant.Filesize, bytesLoaded);

                dl.Filename = filename;
                _downloaderUtils.DeleteUselessFiles(dl, selectedVariant);
                dl.ChangeSelectedVariant(selectedVariant);
                dl.Filesize = chosenVariant.Filesize;
                dl.Container = container;
                dl.BytesLoaded = bytesLoaded;
            }
        }

        window.Close();
    }

    [RelayCommand]
    private void ContainerClicked(int index)
    {
        if (index == 0) return;
        var tmp = ContainerPreference[index];
        tmp.Index--;
        ContainerPreference.RemoveAt(index);
        ContainerPreference.Insert(index - 1, tmp);
        ContainerPreference[index].Index++;
    }
}