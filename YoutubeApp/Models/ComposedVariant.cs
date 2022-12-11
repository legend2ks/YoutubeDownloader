using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YoutubeApp.Comparers;
using YoutubeApp.ViewModels;

namespace YoutubeApp.Models;

public partial class ComposedVariant : ObservableObject
{
    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;
    public static event EventHandler<UndoEventArgs>? Undo;

    private List<CommonVariant> _initFilteredVariants;
    private List<CommonVariant> _filteredVariants;

    public List<Download> Downloads { get; private set; }
    private int _totalDownloadCount;

    private bool _init = true;

    [ObservableProperty] private bool _undoVisible;

    public HashSet<int> CompatibleDownloadIds { get; private set; }

    private int _compatibleDownloadCount;

    private int CompatibleDownloadCount
    {
        get => _compatibleDownloadCount;
        set
        {
            _compatibleDownloadCount = value;
            OnPropertyChanged(nameof(DownloadCountStats));
            SelectionChanged?.Invoke(this,
                new SelectionChangedEventArgs { RemainingDownloadCount = Downloads.Count - value });
        }
    }

    public string DownloadCountStats => $"({_compatibleDownloadCount}/{_totalDownloadCount})";

    [ObservableProperty] private Dictionary<string, Criteria> _videoCodecs = new();
    [ObservableProperty] private Dictionary<string, Criteria> _audioCodecs = new();

    [ObservableProperty]
    private SortedDictionary<float, Criteria> _audioBitrates = new(new DescendingComparer<float>());

    [ObservableProperty] private Dictionary<string, Criteria> _resolutions = new();
    [ObservableProperty] private SortedDictionary<float, Criteria> _fpsList = new(new DescendingComparer<float>());
    [ObservableProperty] private Dictionary<string, Criteria> _containers = new();

    [ObservableProperty] private bool _videoCodecListEnabled = true;
    [ObservableProperty] private bool _audioCodecListEnabled = true;
    [ObservableProperty] private bool _audioBitrateListEnabled = true;
    [ObservableProperty] private bool _resolutionListEnabled = true;
    [ObservableProperty] private bool _fpsListEnabled = true;
    [ObservableProperty] private bool _containerListEnabled = true;


    private bool _videoCodecListOpen;

    public bool VideoCodecListOpen
    {
        get => _videoCodecListOpen;
        set
        {
            _videoCodecListOpen = value;
            if (value == true) return;
            // Closed
            foreach (var item in VideoCodecs)
            {
                if (item.Value.Checked == true || item.Value.DownloadIds.Count == 0) continue;
                VideoCodecListEnabled = false;
                // Process Criterias
                UndoVisible = true;
                //var checkedVideoCodecs = VideoCodecs.Where(x => x.Value.Checked).Select(x => x.Key).ToHashSet();
                var checkedVideoCodecs =
                    VideoCodecs.Where(x => x.Value.Checked);
                var checkedVideoCodecKeys = checkedVideoCodecs.Select(x => x.Key).ToHashSet();
                var checkedVideoCodecDownloadIds = checkedVideoCodecs.SelectMany(x => x.Value.DownloadIds).ToHashSet();
                CompatibleDownloadIds = checkedVideoCodecDownloadIds;
                CompatibleDownloadCount = checkedVideoCodecDownloadIds.Count;
                ProcessCriterias(videoCodecs: checkedVideoCodecKeys);
                OnPropertyChanged(nameof(VideoCodecListText));
                break;
            }
        }
    }

    private bool _audioCodecListOpen;

    public bool AudioCodecListOpen
    {
        get => _audioCodecListOpen;
        set
        {
            _audioCodecListOpen = value;
            if (value == true) return;
            // Closed
            foreach (var item in AudioCodecs)
            {
                if (item.Value.Checked == true || item.Value.DownloadIds.Count == 0) continue;
                AudioCodecListEnabled = false;
                // Process Criterias
                UndoVisible = true;
                var checkedAudioCodecs =
                    AudioCodecs.Where(x => x.Value.Checked);
                var checkedAudioCodecKeys = checkedAudioCodecs.Select(x => x.Key).ToHashSet();
                var checkedAudioCodecDownloadIds = checkedAudioCodecs.SelectMany(x => x.Value.DownloadIds).ToHashSet();
                CompatibleDownloadIds = checkedAudioCodecDownloadIds;
                CompatibleDownloadCount = checkedAudioCodecDownloadIds.Count;
                ProcessCriterias(audioCodecs: checkedAudioCodecKeys);
                OnPropertyChanged(nameof(AudioCodecListText));
                break;
            }
        }
    }

    public string VideoCodecListText
    {
        get
        {
            if (VideoCodecListEnabled)
            {
                return "Smallest";
            }

            var videoCodecs = VideoCodecs.Where(x => x.Value.Checked)
                .Select(x => x.Key);
            return string.Join(" | ", videoCodecs);
        }
    }

    public string AudioCodecListText
    {
        get
        {
            if (AudioCodecListEnabled)
            {
                return "Smallest";
            }

            var audioCodecs = AudioCodecs.Where(x => x.Value.Checked)
                .Select(x => x.Key);
            return string.Join(" | ", audioCodecs);
        }
    }

    private int _selectedResolution = -1;

    public int SelectedResolution
    {
        get => _selectedResolution;
        set
        {
            _selectedResolution = value;
            var selectedResolution = Resolutions.Keys.ElementAt(value);
            ResolutionListEnabled = false;
            UndoVisible = true;
            CompatibleDownloadIds = Resolutions[selectedResolution].DownloadIds.ToHashSet();
            CompatibleDownloadCount = Resolutions[selectedResolution].DownloadIds.Count;
            ProcessCriterias(resolution: selectedResolution);
        }
    }

    private int _selectedFps = -1;

    public int SelectedFps
    {
        get => _selectedFps;
        set
        {
            _selectedFps = value;
            var selectedFps = FpsList.Keys.ElementAt(value);
            FpsListEnabled = false;
            UndoVisible = true;
            CompatibleDownloadIds = FpsList[selectedFps].DownloadIds.ToHashSet();
            CompatibleDownloadCount = FpsList[selectedFps].DownloadIds.Count;
            ProcessCriterias(fps: selectedFps);
        }
    }

    private int _selectedAudioBitrate = -1;

    public int SelectedAudioBitrate
    {
        get => _selectedAudioBitrate;
        set
        {
            _selectedAudioBitrate = value;
            var selectedAudioBitrate = AudioBitrates.Keys.ElementAt(value);
            AudioBitrateListEnabled = false;
            UndoVisible = true;
            CompatibleDownloadIds = AudioBitrates[selectedAudioBitrate].DownloadIds.ToHashSet();
            CompatibleDownloadCount = AudioBitrates[selectedAudioBitrate].DownloadIds.Count;
            ProcessCriterias(audioBitrate: selectedAudioBitrate);
        }
    }

    private const int _noSelection = -1;

    public int NoSelection
    {
        get => _noSelection;
        set { return; }
    }


    public ComposedVariant(List<CommonVariant> filteredVariants, List<Download> downloads)
    {
        Containers = new()
        {
            { "mp4", new Criteria() },
            { "mkv", new Criteria() },
            { "webm", new Criteria() }
        };

        Criteria.Reset();

        _initFilteredVariants = filteredVariants;
        _filteredVariants = filteredVariants;
        Downloads = downloads;
        _totalDownloadCount = downloads.Count;
        _compatibleDownloadCount = downloads.Count;
        CompatibleDownloadIds = new HashSet<int>();

        ProcessCriterias();
    }

    [RelayCommand]
    private void UndoButtonPressed()
    {
        Undo?.Invoke(this, new UndoEventArgs { Downloads = Downloads, FilteredVariants = _initFilteredVariants });
    }

    private void ProcessCriterias(HashSet<string>? videoCodecs = null,
        HashSet<string>? audioCodecs = null,
        string? resolution = null,
        float? fps = null,
        float? audioBitrate = null)
    {
        foreach (var vc in VideoCodecs)
        {
            vc.Value.DownloadIds.Clear();
        }

        foreach (var ac in AudioCodecs)
        {
            ac.Value.DownloadIds.Clear();
        }

        foreach (var res in Resolutions)
        {
            res.Value.DownloadIds.Clear();
        }

        foreach (var f in FpsList)
        {
            f.Value.DownloadIds.Clear();
        }

        foreach (var ab in AudioBitrates)
        {
            ab.Value.DownloadIds.Clear();
        }

        foreach (var c in Containers)
        {
            c.Value.DownloadIds.Clear();
        }

        var newFilteredVariants = new List<CommonVariant>();


        foreach (var uvar in _filteredVariants)
        {
            var resolutionSign = uvar.Variant.Width + "x" + uvar.Variant.Height;

            // Check Criteria
            if (videoCodecs is not null && !videoCodecs.Contains(uvar.Variant.VCodec)) continue;
            if (audioCodecs is not null && !audioCodecs.Contains(uvar.Variant.ACodec)) continue;
            if (resolution is not null && resolutionSign != resolution) continue;
            if (fps is not null && uvar.Variant.Fps != fps) continue;
            if (audioBitrate is not null && uvar.Variant.Abr != audioBitrate) continue;

            newFilteredVariants.Add(uvar);

            // Video Codec
            if (!VideoCodecs.ContainsKey(uvar.Variant.VCodec))
            {
                VideoCodecs[uvar.Variant.VCodec] = new Criteria("vc");
            }

            // Audio Codec
            if (!AudioCodecs.ContainsKey(uvar.Variant.ACodec))
            {
                AudioCodecs[uvar.Variant.ACodec] = new Criteria("ac");
            }

            // Resolution
            if (!Resolutions.ContainsKey(resolutionSign))
            {
                Resolutions[resolutionSign] = new Criteria();
            }

            // FPS
            if (!FpsList.ContainsKey(uvar.Variant.Fps))
            {
                FpsList[uvar.Variant.Fps] = new Criteria();
            }

            // Audio Bitrate
            if (!AudioBitrates.ContainsKey(uvar.Variant.Abr))
            {
                AudioBitrates[uvar.Variant.Abr] = new Criteria();
            }

            // Download Count
            foreach (var dl in uvar.Downloads)
            {
                VideoCodecs[uvar.Variant.VCodec].AddToDownloadIds(dl.Key);
                AudioCodecs[uvar.Variant.ACodec].AddToDownloadIds(dl.Key);
                Resolutions[resolutionSign].AddToDownloadIds(dl.Key);
                FpsList[uvar.Variant.Fps].AddToDownloadIds(dl.Key);
                AudioBitrates[uvar.Variant.Abr].AddToDownloadIds(dl.Key);

                if (_init)
                {
                    CompatibleDownloadIds.Add(dl.Key);
                }
            }
        }

        _filteredVariants = newFilteredVariants;

        if (_init)
        {
            _init = false;
            Resolutions = Resolutions
                .OrderBy<KeyValuePair<string, Criteria>, string>(x => x.Key, new ResolutionComparer())
                .ToDictionary(x => x.Key, x => x.Value);
        }
        else
        {
            if (VideoCodecListEnabled)
            {
                foreach (var vc in VideoCodecs)
                {
                    vc.Value.UpdateCheckedState();
                }
            }

            if (AudioCodecListEnabled)
            {
                foreach (var ac in AudioCodecs)
                {
                    ac.Value.UpdateCheckedState();
                }
            }
        }
    }

    public void SetDone()
    {
        VideoCodecListEnabled = false;
        ResolutionListEnabled = false;
        FpsListEnabled = false;
        AudioCodecListEnabled = false;
        AudioBitrateListEnabled = false;
    }
}

public class UndoEventArgs
{
    public required List<Download> Downloads { get; set; }
    public required List<CommonVariant> FilteredVariants { get; set; }
}

public class SelectionChangedEventArgs
{
    public required int RemainingDownloadCount { get; set; }
}