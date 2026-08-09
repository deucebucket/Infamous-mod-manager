using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InfamousModManager.Services;

namespace InfamousModManager.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly UserDataStore _userDataStore;

    [ObservableProperty]
    private ViewModelBase _leftPanelViewModel;

    [ObservableProperty]
    private ViewModelBase? _currentRightViewModel;

    [ObservableProperty]
    private bool _isInfoOverlayOpen;

    public MainViewModel() : this(new UserDataStore())
    {
    }

    public MainViewModel(UserDataStore userDataStore)
    {
        _userDataStore = userDataStore;
        _leftPanelViewModel = new LeftPanelViewModel(NavigateTo, SetLanguage, OpenInfoOverlay, _userDataStore.SelectedLanguageCode);
        _currentRightViewModel = new GreetingViewModel();
    }

    private void NavigateTo(ViewModelBase viewModel)
    {
        if (viewModel is GamePageViewModelBase gamePageViewModel)
        {
            gamePageViewModel.LoadSavedData(_userDataStore);
        }

        CurrentRightViewModel = viewModel;
    }

    private void SetLanguage(string languageCode)
    {
        ((App)Avalonia.Application.Current!).SetLanguage(languageCode);
        _userDataStore.SaveSelectedLanguage(languageCode);
    }

    [RelayCommand]
    private void CloseInfoOverlay() => IsInfoOverlayOpen = false;

    private void OpenInfoOverlay() => IsInfoOverlayOpen = true;
}
