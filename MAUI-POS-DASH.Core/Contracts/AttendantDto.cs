namespace MAUI_POS_DASH.Core.Contracts;

/// <summary>
/// We expose just enough of an Attendant to render a name and role in the UI — never the PIN hash.
/// </summary>
public record AttendantDto(Guid Id, string Name, AttendantRole Role);
