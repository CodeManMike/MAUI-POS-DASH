using CommunityToolkit.Mvvm.ComponentModel;

namespace MAUI_POS_DASH.MAUI_Android.ViewModels;

/// <summary>
/// We centralize the two properties almost every page needs — a busy flag to disable input while
/// an async operation runs, and an error message to display — so pages don't each redeclare them.
/// </summary>
public abstract partial class BaseViewModel : ObservableObject
{
    #region Fields
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorMessage;
    #endregion

    #region Properties
    /// <summary>
    /// We expose this instead of asking every page's XAML to null-check ErrorMessage itself with
    /// a value converter — the "is there an error to show" decision belongs here, not in a
    /// converter buried in a resource dictionary.
    /// </summary>
    public bool HasError => ErrorMessage is not null;
    #endregion

    #region Property Change Hooks
    /// <summary>
    /// CommunityToolkit.Mvvm generates a partial OnIsBusyChanged(bool) hook for _isBusy, but it's
    /// only reachable from the class that declares the property — a subclass can't implement a
    /// partial method declared here. We implement it once, in the class where it's declared, and
    /// forward to a differently-named virtual method a subclass CAN override.
    /// </summary>
    partial void OnIsBusyChanged(bool value) => OnBusyChanged(value);

    /// <summary>A subclass overrides this to keep a computed property (e.g. button text) in sync with IsBusy.</summary>
    protected virtual void OnBusyChanged(bool isBusy)
    {
    }
    #endregion
}
