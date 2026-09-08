using CommunityToolkit.Mvvm.ComponentModel;
using MAUI_POS_DASH.Core.Attendants;
using MAUI_POS_DASH.Core.Shifts;

namespace MAUI_POS_DASH.MAUI_Android.ViewModels;

/// <summary>
/// We centralize "does this attendant have an open shift" for the four pages that need one —
/// every payment page and shift close — instead of each repeating the same
/// GetActiveShiftAsync/null-check the Blazor pages each did independently. A subclass overrides
/// <see cref="OnShiftReadyAsync"/> for anything it needs once a shift is confirmed present.
/// </summary>
public abstract partial class ShiftAwareViewModelBase : AuthenticatedViewModelBase
{
    #region Fields
    protected readonly ShiftService ShiftService;

    [ObservableProperty]
    private Shift? _currentShift;

    [ObservableProperty]
    private string? _noShiftMessage;
    #endregion

    #region Constructor
    protected ShiftAwareViewModelBase(AttendantService attendantService, ShiftService shiftService)
        : base(attendantService)
    {
        ShiftService = shiftService;
    }
    #endregion

    #region Protected Methods
    /// <summary>Each page's own wording for why it can't proceed without an open shift.</summary>
    protected virtual string NoOpenShiftMessage => "There's no open shift.";

    protected sealed override async Task OnAuthenticatedAppearingAsync()
    {
        CurrentShift = await ShiftService.GetActiveShiftAsync(CurrentAttendantId);
        NoShiftMessage = CurrentShift is null ? NoOpenShiftMessage : null;

        if (CurrentShift is not null)
        {
            await OnShiftReadyAsync();
        }
    }

    /// <summary>We run this only once a signed-in attendant with an open shift is confirmed.</summary>
    protected virtual Task OnShiftReadyAsync() => Task.CompletedTask;
    #endregion
}
