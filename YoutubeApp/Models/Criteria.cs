using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace YoutubeApp.Models;

public partial class Criteria : ObservableObject
{
    private static Dictionary<string, int> _count = new();
    public static void Reset() => _count = new();

    private readonly string? _type;

    public Criteria(string? type = null)
    {
        if (type is null) return;
        _count[type] = _count.GetValueOrDefault(type, 0) + 1;
        _type = type;
    }

    // public HashSet<int> DownloadIds { get; } = new();
    public ObservableCollection<int> DownloadIds { get; } = new();

    public void AddToDownloadIds(int downloadId)
    {
        var exists = DownloadIds.Contains(downloadId);
        if (exists) return;
        DownloadIds.Add(downloadId);
    }

    private bool _checked = true;

    public bool Checked
    {
        get => _checked;
        set
        {
            if (value == true)
            {
                _count[_type]++;
            }
            else // Uncheck
            {
                if (_count[_type] == 1) return;
                _count[_type]--;
            }

            _checked = value;
        }
    }

    public void UpdateCheckedState()
    {
        if (DownloadIds.Count == 0)
        {
            Checked = false;
        }

        OnPropertyChanged(nameof(Checked));
    }
}