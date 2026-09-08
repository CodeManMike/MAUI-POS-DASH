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
}
