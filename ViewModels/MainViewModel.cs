using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace InfamousModManager.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase _leftPanelViewModel;

    [ObservableProperty]
    private ViewModelBase? _currentRightViewModel;

    [ObservableProperty]
    private bool _isInfoOverlayOpen;

    public MainViewModel()
    {
        _leftPanelViewModel = new LeftPanelViewModel(NavigateTo, SetLanguage, OpenInfoOverlay);
        _currentRightViewModel = new GreetingViewModel();
    }

    private void NavigateTo(ViewModelBase viewModel)
    {
        CurrentRightViewModel = viewModel;
    }

    private static void SetLanguage(string languageCode)
    {
        ((App)Avalonia.Application.Current!).SetLanguage(languageCode);
    }

    [RelayCommand]
    private void CloseInfoOverlay() => IsInfoOverlayOpen = false;

    private void OpenInfoOverlay() => IsInfoOverlayOpen = true;
}
