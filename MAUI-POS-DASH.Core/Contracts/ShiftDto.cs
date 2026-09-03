namespace MAUI_POS_DASH.Core.Contracts;

/// <summary>
/// We flatten the attendant's name onto the shift so UI components don't need a second lookup.
/// Callers must eager-load <c>Shift.Attendant</c> before mapping — the mapper can't do that for
/// them, it only reshapes what's already loaded.
/// </summary>
public record ShiftDto(
    Guid Id,
    Guid AttendantId,
    string AttendantName,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt,
    decimal OpeningFloat,
    decimal? ClosingCashCounted,
    ShiftStatus Status);
