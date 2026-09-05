namespace MAUI_POS_DASH.Core.Attendants;

public enum LoginStatus
{
    Success,
    InvalidPin,
    AttendantInactive
}

/// <summary>
/// We return this instead of throwing — a wrong PIN is expected user behavior, not an
/// exceptional condition.
/// </summary>
public record LoginResult(LoginStatus Status, AttendantSession? Session);
