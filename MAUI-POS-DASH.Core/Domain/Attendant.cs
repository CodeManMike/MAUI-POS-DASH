namespace MAUI_POS_DASH.Core.Domain;

/// <summary>
/// We represent a person who can sign in and work the terminal.
/// </summary>
public class Attendant
{
    #region Properties
    public Guid Id { get; set; }

    public required string Name { get; set; }

    /// <summary>
    /// We only ever store a hash of the PIN, never the PIN itself — the AttendantMgmt module
    /// owns the actual hashing scheme when it's built.
    /// </summary>
    public required string PinHash { get; set; }

    public AttendantRole Role { get; set; }
    #endregion
}
