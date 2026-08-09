using CommunityToolkit.Mvvm.ComponentModel;

namespace InfamousModManager.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase _leftPanelViewModel;

    [ObservableProperty]
    private ViewModelBase? _currentRightViewModel;

    public MainViewModel()
    {
        _leftPanelViewModel = new LeftPanelViewModel(NavigateTo, SetLanguage);
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
}
