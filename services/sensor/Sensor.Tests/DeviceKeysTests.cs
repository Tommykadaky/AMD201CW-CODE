using Sensor.Api.Services;

namespace Sensor.Tests;

public class DeviceKeysTests
{
    [Fact]
    public void Hash_IsStableAndNotThePlainKey()
    {
        var hash = DeviceKeys.Hash("my-secret-device-key-123");

        Assert.Equal(64, hash.Length);                                  // SHA-256 as hex
        Assert.Equal(hash, DeviceKeys.Hash("my-secret-device-key-123"));
        Assert.DoesNotContain("secret", hash, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Matches_RightKey_True_WrongOrMissingKey_False()
    {
        var hash = DeviceKeys.Hash("my-secret-device-key-123");

        Assert.True(DeviceKeys.Matches("my-secret-device-key-123", hash));
        Assert.False(DeviceKeys.Matches("my-secret-device-key-124", hash));
        Assert.False(DeviceKeys.Matches(null, hash));
        Assert.False(DeviceKeys.Matches("", hash));
    }
}
