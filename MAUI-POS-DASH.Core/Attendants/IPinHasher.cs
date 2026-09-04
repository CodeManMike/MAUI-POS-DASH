namespace MAUI_POS_DASH.Core.Attendants;

/// <summary>
/// We hash and verify PINs — never store or compare one in plain text.
/// </summary>
public interface IPinHasher
{
    string Hash(string pin);

    bool Verify(string pin, string hash);
}
