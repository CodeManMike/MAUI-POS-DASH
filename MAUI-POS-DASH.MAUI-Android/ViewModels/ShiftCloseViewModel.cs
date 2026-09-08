using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MAUI_POS_DASH.Core.Attendants;
using MAUI_POS_DASH.Core.Shifts;

namespace MAUI_POS_DASH.MAUI_Android.ViewModels;

/// <summary>
/// We own the two-step cash reconciliation flow for closing a shift: count cash and preview the
/// variance first, then either confirm the close or go back and recount.
/// </summary>
public partial class ShiftCloseViewModel : ShiftAwareViewModelBase
{
    #region Fields
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoOpenShift))]
    [NotifyPropertyChangedFor(nameof(IsCounting))]
    [NotifyPropertyChangedFor(nameof(IsReviewing))]
    private bool _hasOpenShift;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCounting))]
    [NotifyPropertyChangedFor(nameof(IsReviewing))]
    [NotifyPropertyChangedFor(nameof(ToleranceStatus))]
    private ReconciliationResult? _reconciliation;

    [ObservableProperty]
    private decimal _cashCounted;
    #endregion

    #region Properties
    public bool NoOpenShift => !HasOpenShift;

    public bool IsCounting => HasOpenShift && Reconciliation is null;

    public bool IsReviewing => HasOpenShift && Reconciliation is not null;

    public string? ToleranceStatus => Reconciliation switch
    {
        null => null,
        { WithinTolerance: true } => "Within tolerance",
        _ => "OUT OF TOLERANCE"
    };
    #endregion

    #region Constructor
    public ShiftCloseViewModel(AttendantService attendantService, ShiftService shiftService)
        : base(attendantService, shiftService)
    {
    }
    #endregion

    #region Protected Methods
    protected override string NoOpenShiftMessage => "There's no open shift to close.";

    protected override Task OnShiftReadyAsync()
    {
        HasOpenShift = true;
        return Task.CompletedTask;
    }
    #endregion

    #region Commands
    [RelayCommand]
    private async Task ReviewAsync()
    {
        Reconciliation = await ShiftService.PreviewCloseAsync(CurrentShift!, CashCounted);
    }

    [RelayCommand]
    private async Task ConfirmCloseAsync()
    {
        await ShiftService.CloseShiftAsync(CurrentShift!, CashCounted);
        await Shell.Current.GoToAsync($"//{RouteNames.Home}");
    }

    [RelayCommand]
    private void Recount()
    {
        Reconciliation = null;
    }
    #endregion
}
