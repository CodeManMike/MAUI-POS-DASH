using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MAUI_POS_DASH.Core.Attendants;
using MAUI_POS_DASH.Core.Cash;
using MAUI_POS_DASH.Core.Shifts;
using MAUI_POS_DASH.Core.Sync;

namespace MAUI_POS_DASH.MAUI_Android.ViewModels;

/// <summary>
/// We own the cash tender + change flow. Unlike FleetCard/MobileMoney, cash has no hardware or
/// network round-trip to wait on, so there's no busy state here — the service call resolves
/// immediately.
/// </summary>
public partial class CashSaleViewModel : ShiftAwareViewModelBase
{
    #region Fields
    private readonly CashSaleService _cashSaleService;
    private readonly OfflineTransactionQueue _offlineTransactionQueue;

    [ObservableProperty]
    private decimal _amountOwed;

    [ObservableProperty]
    private decimal _amountTendered;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResult))]
    [NotifyPropertyChangedFor(nameof(IsInputForm))]
    private CashSaleResult? _result;
    #endregion

    #region Properties
    protected override string NoOpenShiftMessage => "There's no open shift — open one before taking payments.";

    public bool IsInputForm => HasOpenShift && Result is null;

    public bool HasResult => Result is not null;
    #endregion

    #region Constructor
    public CashSaleViewModel(AttendantService attendantService, ShiftService shiftService, CashSaleService cashSaleService, OfflineTransactionQueue offlineTransactionQueue)
        : base(attendantService, shiftService)
    {
        _cashSaleService = cashSaleService;
        _offlineTransactionQueue = offlineTransactionQueue;
    }
    #endregion

    #region Protected Methods
    protected override void OnShiftChanged(Shift? currentShift)
    {
        OnPropertyChanged(nameof(IsInputForm));
    }

    protected override void ResetVisitState() => Reset();
    #endregion

    #region Commands
    [RelayCommand]
    private async Task ProcessSaleAsync()
    {
        ErrorMessage = null;

        try
        {
            Result = await _cashSaleService.ProcessSaleAsync(CurrentShift!.Id, AmountOwed, AmountTendered);
            await _offlineTransactionQueue.TryFlushAsync();
        }
        catch (ArgumentException ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void Reset()
    {
        AmountOwed = 0;
        AmountTendered = 0;
        Result = null;
        ErrorMessage = null;
    }
    #endregion
}
