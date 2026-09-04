using MAUI_POS_DASH.Core.Attendants;

namespace MAUI_POS_DASH.Core.Tests.Attendants;

[TestFixture]
public class Pbkdf2PinHasherTests
{
    #region Fields
    private Pbkdf2PinHasher _sut = null!;
    #endregion

    #region Setup
    [SetUp]
    public void SetUp()
    {
        _sut = new Pbkdf2PinHasher();
    }
    #endregion

    #region Tests
    [Test]
    public void Verify_CorrectPin_ReturnsTrue()
    {
        #region Arrange
        var hash = _sut.Hash("1234");
        #endregion

        #region Act
        var result = _sut.Verify("1234", hash);
        #endregion

        #region Assert
        Assert.That(result, Is.True);
        #endregion
    }

    [Test]
    public void Verify_IncorrectPin_ReturnsFalse()
    {
        #region Arrange
        var hash = _sut.Hash("1234");
        #endregion

        #region Act
        var result = _sut.Verify("9999", hash);
        #endregion

        #region Assert
        Assert.That(result, Is.False);
        #endregion
    }

    [Test]
    public void Hash_CalledTwiceForSamePin_ProducesDifferentHashes()
    {
        #region Arrange & Act
        var first = _sut.Hash("1234");
        var second = _sut.Hash("1234");
        #endregion

        #region Assert
        Assert.That(first, Is.Not.EqualTo(second));
        #endregion
    }
    #endregion
}
