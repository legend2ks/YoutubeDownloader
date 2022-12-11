using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace YoutubeApp.ViewModels;

public partial class SettingsWindowViewModel : ViewModelBase
{
    public SettingsWindowViewModel(ILogger<SettingsWindowViewModel> logger, Settings settings)
    {
        _logger = logger;
        _settings = settings;

        FilenameTemplate = _settings.FilenameTemplate;
        MaxConnections.Find(x => x.MaxCount == Settings.MaxConnections)!.Checked = true;
    }

    private readonly ILogger<SettingsWindowViewModel> _logger;
    private readonly Settings _settings;

    [ObservableProperty] private bool _applyButtonEnabled = true;

    public class MaxConnectionsItem
    {
        public int MaxCount { get; set; }
        public bool Checked { get; set; }
    }

    private string _filenameTemplate;

    public string FilenameTemplate
    {
        get => _filenameTemplate;
        set
        {
            if (!SetProperty(ref _filenameTemplate, value)) return;
            ApplyButtonEnabled = value.Trim().Length != 0;
        }
    }

    public List<MaxConnectionsItem> MaxConnections { get; set; } = new()
    {
        new MaxConnectionsItem { MaxCount = 1, Checked = false },
        new MaxConnectionsItem { MaxCount = 2, Checked = false },
        new MaxConnectionsItem { MaxCount = 4, Checked = false },
        new MaxConnectionsItem { MaxCount = 8, Checked = false },
        new MaxConnectionsItem { MaxCount = 16, Checked = false },
    };


    [RelayCommand]
    private void ApplyButtonClicked(Window window)
    {
        _settings.SaveFilenameTemplate(FilenameTemplate.Trim());

        var selectedMaxConnections = MaxConnections.First(x => x.Checked).MaxCount;
        _settings.SaveMaxConnections(selectedMaxConnections);

        window.Close();
    }

    [RelayCommand]
    private void ResetFilenameTemplateButtonPressed()
    {
        FilenameTemplate = Settings.DefaultFilenameTemplate;
    }
}