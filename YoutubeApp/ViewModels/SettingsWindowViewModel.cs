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
        MaxConcurrentChannelUpdates.Find(x => x.MaxCount == Settings.MaxConcurrentChannelUpdates)!.Checked = true;
        CookiesBrowserName = _settings.CookiesBrowserName;
    }

    private readonly ILogger<SettingsWindowViewModel> _logger;
    private readonly Settings _settings;

    [ObservableProperty] private bool _applyButtonEnabled = true;

    public class NumberOption
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

    public List<NumberOption> MaxConnections { get; set; } = new()
    {
        new NumberOption { MaxCount = 1, Checked = false },
        new NumberOption { MaxCount = 2, Checked = false },
        new NumberOption { MaxCount = 4, Checked = false },
        new NumberOption { MaxCount = 8, Checked = false },
        new NumberOption { MaxCount = 16, Checked = false },
    };

    public List<NumberOption> MaxConcurrentChannelUpdates { get; set; } = new()
    {
        new NumberOption { MaxCount = 1, Checked = false },
        new NumberOption { MaxCount = 2, Checked = false },
        new NumberOption { MaxCount = 4, Checked = false },
    };

    [ObservableProperty] private string _cookiesBrowserName;


    [RelayCommand]
    private void ApplyButtonClicked(Window window)
    {
        _settings.SaveFilenameTemplate(FilenameTemplate.Trim());

        var selectedMaxConnections = MaxConnections.First(x => x.Checked).MaxCount;
        _settings.SaveMaxConnections(selectedMaxConnections);

        var selectedMaxConcurrentChannelUpdates = MaxConcurrentChannelUpdates.First(x => x.Checked).MaxCount;
        _settings.SaveMaxConcurrentChannelUpdates(selectedMaxConcurrentChannelUpdates);

        _settings.SaveCookiesBrowserName(CookiesBrowserName.Trim());

        window.Close();
    }

    [RelayCommand]
    private void ResetFilenameTemplateButtonPressed()
    {
        FilenameTemplate = Settings.DefaultFilenameTemplate;
    }

    [RelayCommand]
    private void SetBrowserName(string browserName)
    {
        CookiesBrowserName = browserName;
    }
}