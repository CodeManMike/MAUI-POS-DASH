using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MAUI_POS_DASH.Core.Attendants;
using MAUI_POS_DASH.Core.FleetCard;
using MAUI_POS_DASH.Core.Shifts;

namespace MAUI_POS_DASH.MAUI_Android.ViewModels;

/// <summary>
/// FleetCardSaleService owns the whole tap-card flow — waiting for the card, authorizing it,
/// recording the Sale/Transaction — this ViewModel just tracks the amount typed in and presents
/// whatever result comes back.
/// </summary>
public partial class FleetCardSaleViewModel : ShiftAwareViewModelBase
{
    #region Fields
    private readonly FleetCardSaleService _fleetCardSaleService;

    [ObservableProperty]
    private decimal _amount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInputForm))]
    [NotifyPropertyChangedFor(nameof(IsResultView))]
    [NotifyPropertyChangedFor(nameof(IsApproved))]
    private FleetCardSaleResult? _result;
    #endregion

    #region Properties
    public bool IsInputForm => HasOpenShift && Result is null;

    public bool IsResultView => Result is not null;

    public bool IsApproved => Result?.Status == FleetCardSaleStatus.Approved;

    public bool CanTakePayment => !IsBusy;

    public string ProcessButtonText => IsBusy ? "Waiting for card…" : "Take Payment";
    #endregion

    #region Constructor
    public FleetCardSaleViewModel(
        AttendantService attendantService,
        ShiftService shiftService,
        FleetCardSaleService fleetCardSaleService)
        : base(attendantService, shiftService)
    {
        _fleetCardSaleService = fleetCardSaleService;
    }
    #endregion

    #region Protected Methods
    protected override string NoOpenShiftMessage => "There's no open shift — open one before taking payments.";

    protected override void OnShiftChanged(Shift? currentShift)
    {
        OnPropertyChanged(nameof(IsInputForm));
    }

    protected override void OnBusyChanged(bool isBusy)
    {
        OnPropertyChanged(nameof(CanTakePayment));
        OnPropertyChanged(nameof(ProcessButtonText));
    }
    #endregion

    #region Commands
    [RelayCommand]
    private async Task ProcessSaleAsync()
    {
        ErrorMessage = null;
        IsBusy = true;

        try
        {
            Result = await _fleetCardSaleService.ProcessSaleAsync(CurrentShift!.Id, Amount);
        }
        catch (ArgumentException ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Reset()
    {
        Amount = 0;
        Result = null;
        ErrorMessage = null;
    }
    #endregion
}
