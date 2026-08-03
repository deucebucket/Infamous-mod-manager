using CommunityToolkit.Mvvm.ComponentModel;
using InfamousModManager.Views;

namespace InfamousModManager.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase _leftPanelViewModel;

    [ObservableProperty]
    private ViewModelBase? _currentRightViewModel;

    public MainViewModel()
    {
        _leftPanelViewModel = new LeftPanelViewModel();
        _currentRightViewModel = new GreetingViewModel();
    }
}
