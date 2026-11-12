using System.Security.Cryptography;
using System.Text;

namespace Sensor.Api.Services;

// Each device proves who it is with a secret key (DEVICE_KEY in firmware/config.h), sent in the
// "X-Device-Key" header. Only a SHA-256 hash is stored, like a password. A plain SHA-256 is fine here
// because device keys are long random strings, not human passwords that could be guessed.
public static class DeviceKeys
{
    public const string Header = "X-Device-Key";
    public const int MinLength = 16;

    public static string Hash(string key) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

    // Constant-time comparison so response timing reveals nothing about the stored hash
    public static bool Matches(string? key, string storedHash)
    {
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(storedHash)) return false;
        var actual = Encoding.ASCII.GetBytes(Hash(key));
        var expected = Encoding.ASCII.GetBytes(storedHash);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
