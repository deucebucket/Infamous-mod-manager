using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace InfamousModManager.ViewModels;

public partial class LeftPanelViewModel : ViewModelBase
{
    private const string DiscordInviteUrl = "https://discord.gg/2F9FyCSCXV";

    private readonly Action<ViewModelBase> _navigateTo;
    private readonly Action<string> _setLanguage;
    private readonly Action _openInfoOverlay;

    public LeftPanelViewModel(Action<ViewModelBase> navigateTo, Action<string> setLanguage, Action openInfoOverlay, string selectedLanguageCode)
    {
        _navigateTo = navigateTo;
        _setLanguage = setLanguage;
        _openInfoOverlay = openInfoOverlay;
        SelectedLanguage = Languages.FirstOrDefault(language => language.Code == selectedLanguageCode) ?? Languages[0];
    }

    public IReadOnlyList<LanguageOption> Languages { get; } =
    [
        new("en-US", "English"),
        new("ru-RU", "Русский")
    ];

    [ObservableProperty]
    private LanguageOption? _selectedLanguage;

    [RelayCommand]
    private void ShowInfamous1() => _navigateTo(new Infamous1ViewModel());

    [RelayCommand]
    private void ShowInfamous2() => _navigateTo(new Infamous2ViewModel());

    [RelayCommand]
    private void ShowInfamousFob() => _navigateTo(new InfamousFobViewModel());

    [RelayCommand]
    private static void OpenDiscord()
    {
        Process.Start(new ProcessStartInfo(DiscordInviteUrl) { UseShellExecute = true });
    }

    [RelayCommand]
    private void OpenInfo() => _openInfoOverlay();

    partial void OnSelectedLanguageChanged(LanguageOption? value)
    {
        if (value is not null)
        {
            _setLanguage(value.Code);
        }
    }
}

public sealed record LanguageOption(string Code, string DisplayName)
{
    public override string ToString() => DisplayName;
}
