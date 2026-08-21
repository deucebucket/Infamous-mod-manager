using InfamousModManager.Models;

namespace InfamousModManager.ViewModels;

public partial class Infamous1ViewModel : GamePageViewModelBase
{
    public Infamous1ViewModel() : base([GameEditions.Infamous1Psn, GameEditions.Infamous1Bcus])
    {
    }
}
