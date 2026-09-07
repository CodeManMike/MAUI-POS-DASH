using MAUI_POS_DASH.Core.MobileMoney;

namespace MAUI_POS_DASH.Core.Tests.MobileMoney;

[TestFixture]
public class SimulatedMobileMoneyPaymentServiceTests
{
    #region Tests
    [Test]
    public async Task RequestPaymentAsync_RandomBelowTimeoutThreshold_ReturnsTimedOut()
    {
        #region Arrange
        var sut = new SimulatedMobileMoneyPaymentService(new FixedRandom(0.10), TimeSpan.Zero);
        #endregion

        #region Act
        MobileMoneyPaymentResult result = await sut.RequestPaymentAsync(250.00m);
        #endregion

        #region Assert
        Assert.That(result.Status, Is.EqualTo(MobileMoneyPaymentStatus.TimedOut));
        #endregion
    }

    [Test]
    public async Task RequestPaymentAsync_RandomBetweenTimeoutAndDeclineThresholds_ReturnsDeclined()
    {
        #region Arrange
        var sut = new SimulatedMobileMoneyPaymentService(new FixedRandom(0.25), TimeSpan.Zero);
        #endregion

        #region Act
        MobileMoneyPaymentResult result = await sut.RequestPaymentAsync(250.00m);
        #endregion

        #region Assert
        Assert.That(result.Status, Is.EqualTo(MobileMoneyPaymentStatus.Declined));
        #endregion
    }

    [Test]
    public async Task RequestPaymentAsync_RandomAboveDeclineThreshold_ReturnsConfirmed()
    {
        #region Arrange
        var sut = new SimulatedMobileMoneyPaymentService(new FixedRandom(0.60), TimeSpan.Zero);
        #endregion

        #region Act
        MobileMoneyPaymentResult result = await sut.RequestPaymentAsync(250.00m);
        #endregion

        #region Assert
        Assert.That(result.Status, Is.EqualTo(MobileMoneyPaymentStatus.Confirmed));
        #endregion
    }
    #endregion

    #region Test Types
    private sealed class FixedRandom : Random
    {
        #region Fields
        private readonly double _value;
        #endregion

        #region Constructor
        public FixedRandom(double value)
        {
            _value = value;
        }
        #endregion

        #region Overrides
        public override double NextDouble() => _value;
        #endregion
    }
    #endregion
}
