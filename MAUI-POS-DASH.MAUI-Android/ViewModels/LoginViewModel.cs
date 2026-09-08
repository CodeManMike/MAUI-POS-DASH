using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MAUI_POS_DASH.Core.Attendants;

namespace MAUI_POS_DASH.MAUI_Android.ViewModels;

/// <summary>
/// We own the tap-a-tile-then-enter-a-PIN login flow. The page just renders whichever of the two
/// steps <see cref="IsPinEntry"/> says is current and forwards taps to our commands.
/// </summary>
public partial class LoginViewModel : BaseViewModel, IAppearingAware
{
    #region Fields
    private readonly AttendantService _attendantService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPinEntry))]
    [NotifyPropertyChangedFor(nameof(IsAttendantGrid))]
    private Attendant? _selectedAttendant;

    [ObservableProperty]
    private string _pin = string.Empty;
    #endregion

    #region Properties
    public ObservableCollection<Attendant> Attendants { get; } = [];

    public bool IsPinEntry => SelectedAttendant is not null;

    public bool IsAttendantGrid => SelectedAttendant is null;
    #endregion

    #region Constructor
    public LoginViewModel(AttendantService attendantService)
    {
        _attendantService = attendantService;
    }
    #endregion

    #region Public Methods
    public async Task OnAppearingAsync()
    {
        SelectedAttendant = null;
        Pin = string.Empty;
        ErrorMessage = null;

        await _attendantService.EnsureDefaultAttendantSeededAsync();
        var attendants = await _attendantService.GetActiveAttendantsAsync();

        Attendants.Clear();
        foreach (var attendant in attendants)
        {
            Attendants.Add(attendant);
        }
    }
    #endregion

    #region Commands
    [RelayCommand]
    private void SelectAttendant(Attendant attendant)
    {
        SelectedAttendant = attendant;
        Pin = string.Empty;
        ErrorMessage = null;
    }

    [RelayCommand]
    private void Back()
    {
        SelectedAttendant = null;
        Pin = string.Empty;
        ErrorMessage = null;
    }

    [RelayCommand]
    private async Task SubmitPinAsync()
    {
        if (SelectedAttendant is null)
        {
            return;
        }

        var result = await _attendantService.TryLoginAsync(SelectedAttendant.Id, Pin);

        if (result.Status == LoginStatus.Success)
        {
            await Shell.Current.GoToAsync($"//{RouteNames.Home}");
            return;
        }

        ErrorMessage = result.Status switch
        {
            LoginStatus.InvalidPin => "Wrong PIN — try again.",
            LoginStatus.AttendantInactive => "This attendant is no longer active.",
            _ => "Something went wrong — try again."
        };
        Pin = string.Empty;
    }
    #endregion
}
