using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MAUI_POS_DASH.Core.Attendants;

namespace MAUI_POS_DASH.MAUI_Android.ViewModels;

/// <summary>
/// We own Manager-only attendant CRUD — create, edit, PIN reset, and deactivate — mirroring the
/// Blazor AttendantManagement page's flow. Anyone else who lands here just sees the gate message;
/// we never even load the attendant list for them.
/// </summary>
public partial class AttendantManagementViewModel : AuthenticatedViewModelBase
{
    #region Fields
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsUnauthorized))]
    private bool _isAuthorized;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditing))]
    [NotifyPropertyChangedFor(nameof(FormTitle))]
    [NotifyPropertyChangedFor(nameof(PinLabel))]
    private Guid? _editingId;

    [ObservableProperty]
    private string _formName = string.Empty;

    [ObservableProperty]
    private AttendantRole _formRole = AttendantRole.Attendant;

    [ObservableProperty]
    private string _formPin = string.Empty;
    #endregion

    #region Properties
    public ObservableCollection<Attendant> Attendants { get; } = [];

    public IReadOnlyList<AttendantRole> Roles { get; } = Enum.GetValues<AttendantRole>();

    public bool IsUnauthorized => !IsAuthorized;

    public bool IsEditing => EditingId is not null;

    public string FormTitle => IsEditing ? "Edit attendant" : "New attendant";

    public string PinLabel => IsEditing ? "New PIN (leave blank to keep current)" : "PIN";
    #endregion

    #region Constructor
    public AttendantManagementViewModel(AttendantService attendantService) : base(attendantService)
    {
    }
    #endregion

    #region Protected Methods
    protected override async Task OnAuthenticatedAppearingAsync()
    {
        IsAuthorized = CurrentAttendantRole == AttendantRole.Manager;
        ResetForm();

        if (!IsAuthorized)
        {
            return;
        }

        await LoadAttendantsAsync();
    }
    #endregion

    #region Commands
    [RelayCommand]
    private void Edit(Attendant attendant)
    {
        EditingId = attendant.Id;
        FormName = attendant.Name;
        FormRole = attendant.Role;
        FormPin = string.Empty;
        ErrorMessage = null;
    }

    [RelayCommand]
    private void ResetForm()
    {
        EditingId = null;
        FormName = string.Empty;
        FormRole = AttendantRole.Attendant;
        FormPin = string.Empty;
        ErrorMessage = null;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            if (EditingId is null)
            {
                await AttendantService.CreateAttendantAsync(FormName, FormPin, FormRole);
                await LoadAttendantsAsync();
                ResetForm();
                return;
            }

            bool isSelfDemotion = EditingId == CurrentAttendantId && FormRole != AttendantRole.Manager;

            // We reset the PIN first, not last — ValidatePin runs before any write happens
            // inside ResetPinAsync, so an invalid PIN throws here with nothing persisted yet.
            // Doing UpdateAttendantAsync first would commit the name/role change even when
            // the PIN that came with it turns out to be invalid.
            if (!string.IsNullOrEmpty(FormPin))
            {
                await AttendantService.ResetPinAsync(EditingId.Value, FormPin);
            }

            await AttendantService.UpdateAttendantAsync(EditingId.Value, FormName, FormRole);

            if (isSelfDemotion)
            {
                // The role change is already committed at this point — we revoke Manager-only
                // access immediately rather than waiting for the next OnAppearingAsync, since
                // Shell keeps this page's ViewModel alive and would otherwise keep showing the
                // authorized view to an attendant who no longer qualifies for it.
                IsAuthorized = false;
                return;
            }

            await LoadAttendantsAsync();
            ResetForm();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task DeactivateAsync(Attendant attendant)
    {
        try
        {
            await AttendantService.DeactivateAttendantAsync(attendant.Id);
            await LoadAttendantsAsync();
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
        }
    }
    #endregion

    #region Private Methods
    private async Task LoadAttendantsAsync()
    {
        var attendants = await AttendantService.GetActiveAttendantsAsync();

        Attendants.Clear();
        foreach (var attendant in attendants)
        {
            Attendants.Add(attendant);
        }
    }
    #endregion
}
