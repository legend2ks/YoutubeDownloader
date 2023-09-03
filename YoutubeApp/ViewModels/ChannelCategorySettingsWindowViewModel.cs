using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Selection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using YoutubeApp.Database;
using YoutubeApp.Models;

namespace YoutubeApp.ViewModels;

public partial class ChannelCategorySettingsWindowViewModel : ViewModelBase
{
    private readonly Channel _channel;
    private readonly ChannelData _channelData;
    private ChannelCategory _currentCategory;
    private List<ChannelCategory> _removedCategories = new();

    public ChannelCategorySettingsWindowViewModel(ObservableCollection<ChannelCategory> categories, Channel channel,
        ChannelData channelData)
    {
        _channel = channel;
        _channelData = channelData;
        var categoryListItems = new List<ChannelCategoryItem>();
        var i = 0;
        foreach (var cat in categories)
        {
            categoryListItems.Add(new() { Title = cat.Title, InitialPosition = i, ChannelCategory = cat });
            i++;
        }

        CategoryListItems = new ObservableCollection<ChannelCategoryItem>(categoryListItems);

        _currentCategory = categories.First(x => x.Id == channel.CategoryId);
        Selection.Select(categories.IndexOf(_currentCategory));
        Selection.LostSelection += Selection_LostSelection;
        Selection.SelectionChanged += Selection_SelectionChanged;
    }

    private void Selection_SelectionChanged(object? sender,
        SelectionModelSelectionChangedEventArgs<ChannelCategoryItem> e)
    {
        OnPropertyChanged(nameof(Editable));
    }

    private void Selection_LostSelection(object? sender, EventArgs e)
    {
        Selection.Select(Selection.SelectedIndex);
    }

    [RelayCommand]
    private void MoveUp()
    {
        var selectedIndex = Selection.SelectedIndex;
        if (selectedIndex is 0 or 1) return;

        (CategoryListItems[selectedIndex - 1], CategoryListItems[selectedIndex]) =
            (CategoryListItems[selectedIndex], CategoryListItems[selectedIndex - 1]);
        Selection.Select(selectedIndex - 1);
    }

    [RelayCommand]
    private void MoveDown()
    {
        var selectedIndex = Selection.SelectedIndex;
        if (selectedIndex == CategoryListItems.Count - 1 || selectedIndex == 0) return;

        (CategoryListItems[selectedIndex + 1], CategoryListItems[selectedIndex]) =
            (CategoryListItems[selectedIndex], CategoryListItems[selectedIndex + 1]);
        Selection.Select(selectedIndex + 1);
    }

    [RelayCommand]
    private void AddCategory()
    {
        CategoryListItems.Add(new()
        {
            Title = "New Category",
        });
        Selection.Select(CategoryListItems.Count - 1);
    }

    [RelayCommand]
    private void RemoveCategory()
    {
        if (Selection.SelectedIndex == 0) return;
        var selectedIndex = Selection.SelectedIndex;
        _removedCategories.Add(Selection.SelectedItem.ChannelCategory);
        CategoryListItems.RemoveAt(selectedIndex);
        Selection.Select(selectedIndex < CategoryListItems.Count ? selectedIndex : CategoryListItems.Count - 1);
    }

    [RelayCommand]
    private void Apply(Window window)
    {
        var changedItems = new List<ChangedCategory>();
        var i = 0;
        foreach (var pl in CategoryListItems)
        {
            if (pl.ChannelCategory is null)
            {
                pl.ChannelCategory = new ChannelCategory { Title = pl.Title };
                changedItems.Add(new ChangedCategory
                    { Position = i, ChannelCategory = pl.ChannelCategory, IsNew = true });
            }
            else if (pl.InitialPosition != i || pl.Title != pl.ChannelCategory.Title)
            {
                pl.ChannelCategory.Title = pl.Title;
                changedItems.Add(new ChangedCategory { Position = i, ChannelCategory = pl.ChannelCategory });
            }

            i++;
        }

        foreach (var cat in _removedCategories)
        {
            changedItems.Add(new ChangedCategory { ChannelCategory = cat, IsRemoved = true });

            if (cat.Id == _channel.CategoryId)
                _currentCategory = CategoryListItems[0].ChannelCategory;

            foreach (var pl in cat.Channels)
            {
                pl.CategoryId = 0;
            }

            CategoryListItems[0].ChannelCategory.Channels.AddRange(cat.Channels);
        }

        if (changedItems.Count > 0)
            _channelData.UpdateCategories(changedItems);

        if (_channel.CategoryId != Selection.SelectedItem.ChannelCategory.Id)
        {
            _channelData.SetChannelCategory(_channel, Selection.SelectedItem.ChannelCategory.Id);
            _currentCategory.Channels.Remove(_channel);

            var idx = Selection.SelectedItem.ChannelCategory.Channels
                .BinarySearch(_channel.Title,
                    (s, ch) => string.Compare(s, ch.Title, StringComparison.OrdinalIgnoreCase));
            if (idx < 0) idx = ~idx;
            Selection.SelectedItem.ChannelCategory.Channels.Insert(idx, _channel);
            _channel.CategoryId = Selection.SelectedItem.ChannelCategory.Id;
        }

        var channelCategories = CategoryListItems.Select(x => x.ChannelCategory);
        window.Close(channelCategories);
    }

    public ObservableCollection<ChannelCategoryItem> CategoryListItems { get; }
    public SelectionModel<ChannelCategoryItem> Selection { get; } = new();

    public bool Editable => Selection.SelectedIndex != 0;

    public partial class ChannelCategoryItem : ObservableObject
    {
        [ObservableProperty] private string _title;
        public int InitialPosition { get; set; }
        public ChannelCategory? ChannelCategory { get; set; }
    }
}

public class ChangedCategory
{
    public int Position { get; set; }
    public required ChannelCategory? ChannelCategory { get; set; }
    public bool IsNew { get; set; }
    public bool IsRemoved { get; set; }
}

public class ChannelCategorySettingsWindowViewModelDesign : ChannelCategorySettingsWindowViewModel
{
    public ChannelCategorySettingsWindowViewModelDesign() : base(new()
        {
            new()
            {
                Id = 1,
                Title = "Category One",
                Parent = 0,
            },
            new()
            {
                Id = 2,
                Title = "Category Two",
                Parent = 0,
            },
            new()
            {
                Id = 3,
                Title = "Category Three",
                Parent = 2,
            },
            new()
            {
                Id = 4,
                Title = "Category Four",
                Parent = 2,
            },
            new()
            {
                Id = 5,
                Title = "Category Five",
                Parent = 0,
            }
        },
        new Channel() { Id = 0, Title = "Test" },
        null!)
    {
    }
}