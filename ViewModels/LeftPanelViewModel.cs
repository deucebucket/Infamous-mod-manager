using System;
using CommunityToolkit.Mvvm.Input;

namespace InfamousModManager.ViewModels;

public partial class LeftPanelViewModel : ViewModelBase
{
    private readonly Action<ViewModelBase> _navigateTo;

    public LeftPanelViewModel(Action<ViewModelBase> navigateTo)
    {
        _navigateTo = navigateTo;
    }

    [RelayCommand]
    private void ShowInfamous1() => _navigateTo(new Infamous1ViewModel());

    [RelayCommand]
    private void ShowInfamous2() => _navigateTo(new Infamous2ViewModel());

    [RelayCommand]
    private void ShowInfamousFob() => _navigateTo(new InfamousFobViewModel());
}
