using MAUI_POS_DASH.Core.FleetCard;

namespace MAUI_POS_DASH.Core.Tests.FleetCard;

[TestFixture]
public class SimulatedFleetCardAuthorizationServiceTests
{
    #region Tests
    [Test]
    public async Task AuthorizeAsync_RandomBelowDeclineThreshold_ReturnsDeclined()
    {
        #region Arrange
        var sut = new SimulatedFleetCardAuthorizationService(new FixedRandom(0.1));
        #endregion

        #region Act
        FleetCardAuthorizationResult result = await sut.AuthorizeAsync(250.00m);
        #endregion

        #region Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(FleetCardAuthorizationStatus.Declined));
            Assert.That(result.DeclineReason, Is.Not.Null);
        });
        #endregion
    }

    [Test]
    public async Task AuthorizeAsync_RandomAboveDeclineThreshold_ReturnsApproved()
    {
        #region Arrange
        var sut = new SimulatedFleetCardAuthorizationService(new FixedRandom(0.5));
        #endregion

        #region Act
        FleetCardAuthorizationResult result = await sut.AuthorizeAsync(250.00m);
        #endregion

        #region Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(FleetCardAuthorizationStatus.Approved));
            Assert.That(result.DeclineReason, Is.Null);
        });
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
