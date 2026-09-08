using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MAUI_POS_DASH.Core.Attendants;

namespace MAUI_POS_DASH.MAUI_Android.ViewModels;

/// <summary>
/// We centralize the "is anyone signed in, and if so who" check every authenticated page needs —
/// each Blazor page in the sibling app repeated its own session guard; here it's one base class
/// instead. Pages that need to load their own data on top of this override
/// <see cref="OnAuthenticatedAppearingAsync"/> rather than redoing the session check themselves.
/// </summary>
public abstract partial class AuthenticatedViewModelBase : BaseViewModel, IAppearingAware
{
    #region Fields
    protected readonly AttendantService AttendantService;

    [ObservableProperty]
    private string? _currentAttendantName;
    #endregion

    #region Properties
    protected Guid CurrentAttendantId { get; private set; }

    protected AttendantRole CurrentAttendantRole { get; private set; }
    #endregion

    #region Constructor
    protected AuthenticatedViewModelBase(AttendantService attendantService)
    {
        AttendantService = attendantService;
    }
    #endregion

    #region Public Methods
    public virtual async Task OnAppearingAsync()
    {
        var session = await AttendantService.GetActiveSessionAsync();
        if (session is null)
        {
            await Shell.Current.GoToAsync($"//{RouteNames.Login}");
            return;
        }

        CurrentAttendantId = session.AttendantId;
        CurrentAttendantName = session.Attendant?.Name;
        CurrentAttendantRole = session.Attendant?.Role ?? AttendantRole.Attendant;

        await OnAuthenticatedAppearingAsync();
    }
    #endregion

    #region Protected Methods
    /// <summary>We run this only once a signed-in attendant is confirmed present.</summary>
    protected virtual Task OnAuthenticatedAppearingAsync() => Task.CompletedTask;
    #endregion

    #region Commands
    [RelayCommand]
    private async Task SignOutAsync()
    {
        await AttendantService.LogoutAsync();
        await Shell.Current.GoToAsync($"//{RouteNames.Login}");
    }

    /// <summary>
    /// Every authenticated page except Home itself uses this for its "Back to terminal home"
    /// affordance — the Blazor sibling app has the equivalent link on every page it can land on
    /// without a route forward (no open shift, unauthorized, or a completed sale).
    /// </summary>
    [RelayCommand]
    private Task GoToHomeAsync() => Shell.Current.GoToAsync($"//{RouteNames.Home}");
    #endregion
}
