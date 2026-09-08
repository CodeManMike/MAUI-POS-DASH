using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MAUI_POS_DASH.Core.Attendants;
using MAUI_POS_DASH.Core.Cash;
using MAUI_POS_DASH.Core.Shifts;

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

    public bool IsNoShift => CurrentShift is null;

    public bool IsInputForm => CurrentShift is not null && Result is null;

    public bool HasResult => Result is not null;
    #endregion

    #region Constructor
    public CashSaleViewModel(AttendantService attendantService, ShiftService shiftService, CashSaleService cashSaleService)
        : base(attendantService, shiftService)
    {
        _cashSaleService = cashSaleService;
    }
    #endregion

    #region Protected Methods
    /// <summary>
    /// CurrentShift's setter lives in the sealed OnAuthenticatedAppearingAsync up in
    /// ShiftAwareViewModelBase, so we can't attach NotifyPropertyChangedFor to it there — we
    /// forward the notification ourselves instead.
    /// </summary>
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName == nameof(CurrentShift))
        {
            OnPropertyChanged(nameof(IsNoShift));
            OnPropertyChanged(nameof(IsInputForm));
        }
    }
    #endregion

    #region Commands
    [RelayCommand]
    private async Task ProcessSaleAsync()
    {
        ErrorMessage = null;

        try
        {
            Result = await _cashSaleService.ProcessSaleAsync(CurrentShift!.Id, AmountOwed, AmountTendered);
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
