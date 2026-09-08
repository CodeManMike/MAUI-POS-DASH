using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MAUI_POS_DASH.Core.Attendants;
using MAUI_POS_DASH.Core.Shifts;

namespace MAUI_POS_DASH.MAUI_Android.ViewModels;

/// <summary>
/// We own opening a shift for the signed-in attendant — the one screen that has to work when no
/// shift exists yet, so unlike the sale pages we extend AuthenticatedViewModelBase directly rather
/// than ShiftAwareViewModelBase.
/// </summary>
public partial class ShiftOpenViewModel : AuthenticatedViewModelBase
{
    #region Fields
    private readonly ShiftService _shiftService;

    [ObservableProperty]
    private decimal _openingFloat;
    #endregion

    #region Constructor
    public ShiftOpenViewModel(AttendantService attendantService, ShiftService shiftService)
        : base(attendantService)
    {
        _shiftService = shiftService;
    }
    #endregion

    #region Protected Methods
    protected override void ResetVisitState() => OpeningFloat = 0;
    #endregion

    #region Commands
    [RelayCommand]
    private async Task OpenShiftAsync()
    {
        ErrorMessage = null;

        try
        {
            await _shiftService.OpenShiftAsync(CurrentAttendantId, OpeningFloat);
            await Shell.Current.GoToAsync($"//{RouteNames.Home}");
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
        }
    }
    #endregion
}
