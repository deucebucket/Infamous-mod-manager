using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using InfamousModManager.ViewModels;
using InfamousModManager.Views;

namespace InfamousModManager;

public partial class App : Application
{
    private ResourceInclude? _localizationDictionary;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        SetLanguage("en-US");
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    public void SetLanguage(string languageCode)
    {
        if (_localizationDictionary is not null)
        {
            Resources.MergedDictionaries.Remove(_localizationDictionary);
        }

        _localizationDictionary = new ResourceInclude((Uri?)null)
        {
            Source = new Uri($"avares://InfamousModManager/Assets/Localization/Strings.{languageCode}.axaml")
        };
        Resources.MergedDictionaries.Add(_localizationDictionary);
    }
}
