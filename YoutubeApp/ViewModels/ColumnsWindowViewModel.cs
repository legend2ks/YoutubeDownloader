using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Selection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace YoutubeApp.ViewModels;

public partial class ColumnsWindowViewModel : ViewModelBase
{
    public class ColumnItem
    {
        public string Name { get; set; }
        public KeyValuePair<string, ColumnConfig> Column { get; set; }
    }

    private readonly Dictionary<string, ColumnConfig> _currentColumnsState;
    private readonly Settings _settings;

    private readonly Dictionary<string, string> _colNames = new()
    {
        { "en", "Enabled" },
        { "tl", "Title" },
        { "fn", "File Name" },
        { "vr", "Variant" },
        { "fs", "Size" },
        { "pr", "Status" },
        { "sp", "Speed" },
        { "eta", "ETA" },
        { "ld", "Bytes Loaded" },
        { "sv", "Save To" },
        { "dr", "Duration" },
        { "ch", "Channel" },
    };

    [ObservableProperty] private ObservableCollection<ColumnItem> _columns;
    public SelectionModel<ColumnItem> Selection { get; }


    public ColumnsWindowViewModel(Settings settings)
    {
        _settings = settings;
        _currentColumnsState = Settings.ColumnsConfig;

        Selection = new SelectionModel<ColumnItem>();

        SetListItems();
    }

    private void SetListItems()
    {
        var listItems = new List<ColumnItem>();

        foreach (var col in _currentColumnsState)
        {
            listItems.Add(new ColumnItem { Name = _colNames[col.Key], Column = col });
        }

        listItems.Sort((a, b) => a.Column.Value.Order - b.Column.Value.Order);

        Columns = new ObservableCollection<ColumnItem>(listItems);
    }

    [RelayCommand]
    private void MoveUpButtonPressed()
    {
        var selectedIndex = Selection.SelectedIndex;
        if (selectedIndex is -1 or 0) return;

        if (Columns[selectedIndex - 1].Column.Value.Movable == false) return;
        (Columns[selectedIndex - 1], Columns[selectedIndex]) = (Columns[selectedIndex], Columns[selectedIndex - 1]);
        Selection.Select(selectedIndex - 1);
    }

    [RelayCommand]
    private void MoveDownButtonPressed()
    {
        var selectedIndex = Selection.SelectedIndex;
        if (selectedIndex == -1 || selectedIndex == Columns.Count - 1) return;

        if (Columns[selectedIndex + 1].Column.Value.Movable == false) return;
        (Columns[selectedIndex + 1], Columns[selectedIndex]) = (Columns[selectedIndex], Columns[selectedIndex + 1]);
        Selection.Select(selectedIndex + 1);
    }

    [RelayCommand]
    private void ResetButtonPressed()
    {
        var columnsConfig = Settings.ColumnsConfig;
        var listItems = new List<ColumnItem>();

        foreach (var col in columnsConfig)
        {
            listItems.Add(new ColumnItem { Name = _colNames[col.Key], Column = col });
        }

        Columns = new ObservableCollection<ColumnItem>(listItems);
    }

    [RelayCommand]
    private void ApplyButtonPressed(Window window)
    {
        var i = 0;
        foreach (var col in Columns)
        {
            col.Column.Value.Order = i;
            i++;
        }

        _settings.SaveColumnConfig(Columns.Select(x => x.Column).ToArray());

        var result = Columns.Select(x => x.Column.Value);
        window.Close(result);
    }
}