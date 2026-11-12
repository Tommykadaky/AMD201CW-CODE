using Sensor.Api.Models;
using Sensor.Api.Services;

namespace Sensor.Tests;

public class ReadingPolicyTests
{
    private static readonly SensorSettings S = new();                  // defaults: dry 30, wet 60, store every 60 s / 3 %
    private static readonly DateTime Now = new(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc);

    // ----- ShouldStore -----

    [Fact]
    public void ShouldStore_FirstReadingEver_IsStored() =>
        Assert.True(ReadingPolicy.ShouldStore(null, null, false, 40, false, Now, S));

    [Fact]
    public void ShouldStore_SameValueFiveSecondsLater_IsSkipped() =>
        Assert.False(ReadingPolicy.ShouldStore(Now.AddSeconds(-5), 40, false, 41, false, Now, S));

    [Fact]
    public void ShouldStore_AfterStoreInterval_IsStored() =>
        Assert.True(ReadingPolicy.ShouldStore(Now.AddSeconds(-60), 40, false, 40, false, Now, S));

    [Fact]
    public void ShouldStore_BigMoistureChange_IsStoredImmediately() =>
        Assert.True(ReadingPolicy.ShouldStore(Now.AddSeconds(-5), 40, false, 43, false, Now, S));

    [Fact]
    public void ShouldStore_PumpSwitched_IsStoredImmediately() =>
        Assert.True(ReadingPolicy.ShouldStore(Now.AddSeconds(-5), 40, false, 40, true, Now, S));

    // ----- IsOnline -----

    [Fact]
    public void IsOnline_RecentReading_IsOnline() =>
        Assert.True(ReadingPolicy.IsOnline(Now.AddSeconds(-10), Now, S));

    [Fact]
    public void IsOnline_OldReadingOrNever_IsOffline()
    {
        Assert.False(ReadingPolicy.IsOnline(Now.AddSeconds(-31), Now, S));
        Assert.False(ReadingPolicy.IsOnline(null, Now, S));
    }

    // ----- Commands -----

    [Fact]
    public void ActiveCommand_FreshCommand_IsReturned() =>
        Assert.Equal(5, ReadingPolicy.ActiveCommand(5, Now.AddSeconds(-10), Now, S));

    [Fact]
    public void ActiveCommand_ExpiredOrMissing_IsNull()
    {
        Assert.Null(ReadingPolicy.ActiveCommand(5, Now.AddSeconds(-61), Now, S));
        Assert.Null(ReadingPolicy.ActiveCommand(null, Now, Now, S));
        Assert.Null(ReadingPolicy.ActiveCommand(0, Now, Now, S));
    }

    [Theory]
    [InlineData(null, 5)]     // default
    [InlineData(8, 8)]
    [InlineData(0, 1)]        // never less than 1 s
    [InlineData(99, 15)]      // never more than the maximum
    public void ClampPumpSeconds_StaysInRange(int? requested, int expected) =>
        Assert.Equal(expected, ReadingPolicy.ClampPumpSeconds(requested, S));

    [Fact]
    public void ClassifyTrigger_PumpRightAfterCommand_IsManual() =>
        Assert.Equal(PumpTriggers.Manual, ReadingPolicy.ClassifyTrigger(Now.AddSeconds(-5), Now));

    [Fact]
    public void ClassifyTrigger_NoRecentCommand_IsAuto()
    {
        Assert.Equal(PumpTriggers.Auto, ReadingPolicy.ClassifyTrigger(null, Now));
        Assert.Equal(PumpTriggers.Auto, ReadingPolicy.ClassifyTrigger(Now.AddMinutes(-5), Now));
    }

    // ----- Alerts -----

    [Fact]
    public void DryAlert_DryAndAutoOff_IsSent() =>
        Assert.True(ReadingPolicy.ShouldSendDryAlert(20, autoWatering: false, null, Now, S));

    [Fact]
    public void DryAlert_AutoWateringOn_IsNotSent() =>
        Assert.False(ReadingPolicy.ShouldSendDryAlert(20, autoWatering: true, null, Now, S));

    [Fact]
    public void DryAlert_WithinCooldown_IsNotRepeated()
    {
        Assert.False(ReadingPolicy.ShouldSendDryAlert(20, false, Now.AddHours(-2), Now, S));
        Assert.True(ReadingPolicy.ShouldSendDryAlert(20, false, Now.AddHours(-6), Now, S));
    }

    [Fact]
    public void DryAlert_SoilNotDry_IsNotSent() =>
        Assert.False(ReadingPolicy.ShouldSendDryAlert(30, false, null, Now, S));

    [Fact]
    public void EmptyTank_PumpRanButSoilStayedDry_IsDetected() =>
        Assert.True(ReadingPolicy.LooksLikeEmptyTank(25, 26, TimeSpan.FromSeconds(6)));

    [Fact]
    public void EmptyTank_SoilGotWetterOrRunTooShort_IsNotDetected()
    {
        Assert.False(ReadingPolicy.LooksLikeEmptyTank(25, 60, TimeSpan.FromSeconds(6)));
        Assert.False(ReadingPolicy.LooksLikeEmptyTank(25, 25, TimeSpan.FromSeconds(1)));
        Assert.False(ReadingPolicy.LooksLikeEmptyTank(null, 25, TimeSpan.FromSeconds(6)));
    }

    // ----- Delivering commands -----

    [Fact]
    public void CanDeliverCommand_PumpOffAndSoilNotWet_IsDelivered() =>
        Assert.True(ReadingPolicy.CanDeliverCommand(pumpOn: false, moisture: 40, S));

    [Fact]
    public void CanDeliverCommand_PumpRunningOrSoilAtHardStop_IsHeldBack()
    {
        Assert.False(ReadingPolicy.CanDeliverCommand(pumpOn: true, moisture: 40, S));
        Assert.False(ReadingPolicy.CanDeliverCommand(pumpOn: false, moisture: 85, S));
    }

    [Theory]
    [InlineData(null, false)]   // no reading yet: let the device decide
    [InlineData(84, false)]
    [InlineData(85, true)]
    public void IsTooWetToWater_UsesHardStop(int? moisture, bool expected) =>
        Assert.Equal(expected, ReadingPolicy.IsTooWetToWater(moisture, S));

    [Fact]
    public void ClassifyTrigger_PumpStartedBeforeCommand_IsAuto() =>
        Assert.Equal(PumpTriggers.Auto, ReadingPolicy.ClassifyTrigger(Now.AddSeconds(5), Now));

    // ----- Offline and delayed checks -----

    [Fact]
    public void WasOffline_OnlyWhenSeenBeforeAndSilentTooLong()
    {
        Assert.False(ReadingPolicy.WasOffline(null, Now, S));                 // brand-new device
        Assert.False(ReadingPolicy.WasOffline(Now.AddSeconds(-10), Now, S));
        Assert.True(ReadingPolicy.WasOffline(Now.AddSeconds(-31), Now, S));
    }

    [Fact]
    public void IsTankCheckDue_OnlyAfterTheDueTime()
    {
        Assert.False(ReadingPolicy.IsTankCheckDue(null, Now));
        Assert.False(ReadingPolicy.IsTankCheckDue(Now.AddSeconds(1), Now));
        Assert.True(ReadingPolicy.IsTankCheckDue(Now, Now));
    }
}
