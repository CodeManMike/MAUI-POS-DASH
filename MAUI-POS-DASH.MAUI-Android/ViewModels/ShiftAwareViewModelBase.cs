using CommunityToolkit.Mvvm.ComponentModel;
using MAUI_POS_DASH.Core.Attendants;
using MAUI_POS_DASH.Core.Shifts;

namespace MAUI_POS_DASH.MAUI_Android.ViewModels;

/// <summary>
/// We centralize "does this attendant have an open shift" for the four pages that need one —
/// every payment page and shift close — instead of each repeating the same
/// GetActiveShiftAsync/null-check the Blazor pages each did independently.
/// </summary>
public abstract partial class ShiftAwareViewModelBase : AuthenticatedViewModelBase
{
    #region Fields
    protected readonly ShiftService ShiftService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOpenShift))]
    [NotifyPropertyChangedFor(nameof(NoOpenShift))]
    private Shift? _currentShift;

    [ObservableProperty]
    private string? _noShiftMessage;
    #endregion

    #region Properties
    public bool HasOpenShift => CurrentShift is not null;

    public bool NoOpenShift => CurrentShift is null;
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

    /// <summary>We run this only once a signed-in attendant with an open shift is confirmed — for async follow-up work, if any.</summary>
    protected virtual Task OnShiftReadyAsync() => Task.CompletedTask;
    #endregion

    #region Property Change Hooks
    /// <summary>
    /// Same limitation as BaseViewModel.OnIsBusyChanged — CommunityToolkit.Mvvm's generated
    /// partial OnCurrentShiftChanged(Shift?) hook is only reachable from this class, so we
    /// implement it once here and forward to a differently-named virtual method a subclass can
    /// override (e.g. "show the input form once a shift exists AND nothing's been submitted yet").
    /// </summary>
    partial void OnCurrentShiftChanged(Shift? value) => OnShiftChanged(value);

    protected virtual void OnShiftChanged(Shift? currentShift)
    {
    }
    #endregion
}
