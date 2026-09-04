namespace MAUI_POS_DASH.Core.Domain;

/// <summary>
/// We track this per transaction so the offline queue knows what still needs to reach the back
/// office, without needing to ask the network first.
/// </summary>
public enum TransactionStatus
{
    Pending,
    Synced,
    Failed
}
