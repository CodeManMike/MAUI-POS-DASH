using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MAUI_POS_DASH.Core.Attendants;
using MAUI_POS_DASH.Core.MobileMoney;
using MAUI_POS_DASH.Core.Shifts;

namespace MAUI_POS_DASH.MAUI_Android.ViewModels;

/// <summary>
/// We own the mobile money confirmation-based payment flow — request payment, sit in
/// <see cref="BaseViewModel.IsBusy"/> while the simulated customer confirms, then present whatever
/// came back. The page just renders whichever of the two steps <see cref="IsResult"/> says is current.
/// </summary>
public partial class MobileMoneySaleViewModel : ShiftAwareViewModelBase
{
    #region Fields
    private readonly MobileMoneySaleService _mobileMoneySaleService;

    [ObservableProperty]
    private decimal _amount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInputForm))]
    [NotifyPropertyChangedFor(nameof(IsResult))]
    [NotifyPropertyChangedFor(nameof(IsConfirmed))]
    private MobileMoneySaleResult? _result;
    #endregion

    #region Properties
    public bool IsInputForm => HasOpenShift && Result is null;

    public bool IsResult => Result is not null;

    public bool IsConfirmed => Result?.Status == MobileMoneySaleStatus.Confirmed;

    public bool CanTakePayment => !IsBusy;

    public string ProcessButtonText => IsBusy ? "Waiting for customer confirmation…" : "Request Payment";
    #endregion

    #region Constructor
    public MobileMoneySaleViewModel(
        AttendantService attendantService,
        ShiftService shiftService,
        MobileMoneySaleService mobileMoneySaleService)
        : base(attendantService, shiftService)
    {
        _mobileMoneySaleService = mobileMoneySaleService;
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

    protected override void ResetVisitState() => Reset();
    #endregion

    #region Commands
    [RelayCommand]
    private async Task ProcessSaleAsync()
    {
        ErrorMessage = null;
        IsBusy = true;

        try
        {
            Result = await _mobileMoneySaleService.ProcessSaleAsync(CurrentShift!.Id, Amount);
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
