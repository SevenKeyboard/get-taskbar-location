using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using SevenKeyboard;

if (args.Length > 1 || (args.Length == 1 && args[0] != "--native"))
{
    Console.Error.WriteLine("Usage: GetTaskbarLocation.Tests [--native]");
    return 2;
}

int checks = 0;

// RegGetValueW uses ERROR_FILE_NOT_FOUND for either a missing key or a missing value.
ExpectSuccess(Status(2), "Bottom", "Missing key");
ExpectSuccess(Status(2), "Bottom", "Existing key with missing Settings value");
ExpectSuccess(Status(3), "Bottom", "Missing key path");

for (int length = 0; length <= 12; length++)
    ExpectSuccess(Binary(new byte[length]), "Bottom", $"Short binary ({length} bytes)");

// Check every possible byte, including every unknown value, with a minimal and a normal payload.
for (int raw = 0; raw <= byte.MaxValue; raw++)
{
    string expected = raw switch { 0 => "Left", 1 => "Top", 2 => "Right", _ => "Bottom" };
    foreach (int length in new[] { 13, 48 })
    {
        byte[] bytes = new byte[length];
        Array.Fill(bytes, (byte)0xFF);
        bytes[12] = (byte)raw;
        ExpectSuccess(Binary(bytes), expected, $"Byte {raw} at offset 12, length {length}");
    }
}

// Nonzero adjacent bytes must not turn the field into a DWORD or shift its offset.
byte[] offsetFixture = new byte[48];
offsetFixture[11] = 2;
offsetFixture[12] = 1;
offsetFixture[13] = 0xFF;
offsetFixture[14] = 0xFF;
offsetFixture[15] = 0xFF;
ExpectSuccess(Binary(offsetFixture), "Top", "Only byte 12 controls direction");

byte[] large = new byte[4096];
large[12] = 2;
ExpectSuccess(Binary(large), "Right", "Retry a larger payload");

// After ERROR_MORE_DATA, only the successful read's size and data are meaningful.
ExpectSuccess(GrowingThen(Binary(new byte[12])), "Bottom", "Value shrank during retry");
ExpectSuccess(GrowingThen(Binary(Array.Empty<byte>())), "Bottom", "Value became empty during retry");
ExpectSuccess(GrowingThen(Status(2)), "Bottom", "Value disappeared during retry");
ExpectSuccess(GrowingThen(Status(3)), "Bottom", "Key path disappeared during retry");

foreach (int error in new[] { 5, 6, 13, 1018, 1117, 1630 })
{
    ExpectFailure(Status(error), error, $"Native error {error}");
    ExpectFailure(GrowingThen(Status(error)), error, $"Native error {error} during retry");
}

foreach (uint type in new uint[] { 0, 1, 2, 4, 7, 11 })
    ExpectFailure(Binary(new byte[48], type), 1630, $"Reject non-binary type {type}");

// A broken read contract must never produce a plausible direction from uninitialized bytes.
ExpectFailure((byte[] data, out uint type, ref uint size) =>
{
    type = 3;
    size = (uint)data.Length + 1;
    return 0;
}, 13, "Reject successful read larger than its buffer");
ExpectFailure((byte[] data, out uint type, ref uint size) =>
{
    type = 3;
    size = (uint)data.Length;
    return 234;
}, 234, "Non-growing ERROR_MORE_DATA fails without looping");

// The seam is per call: failed calls and fallbacks must not cache a setting.
int readCount = 0;
GetTaskbarLocation.SettingsReader changing = (byte[] data, out uint type, ref uint size) =>
{
    type = 3;
    size = 13;
    data[12] = (byte)(readCount++ % 4);
    return 0;
};
foreach (string expected in new[] { "Left", "Top", "Right", "Bottom", "Left" })
    Check(GetTaskbarLocation.Get(changing) == expected, "Each call reads the current setting");
Check(readCount == 5, "No cached registry result");

// TryGet handles registry errors only, so programming/runtime failures remain visible.
var unexpected = new InvalidOperationException("Injected non-registry failure");
GetTaskbarLocation.SettingsReader throws = (byte[] data, out uint type, ref uint size) =>
    throw unexpected;
CheckPropagates(() => GetTaskbarLocation.Get(throws), unexpected, "Get propagates other exceptions");
CheckPropagates(() => GetTaskbarLocation.TryGet(throws, out _), unexpected, "TryGet propagates other exceptions");

Console.WriteLine($"Passed {checks} injected checks ({RuntimeInformation.ProcessArchitecture}); no registry access.");

if (args.Length == 1)
{
    // Public calls are read-only; settings can change independently between these two reads.
    string current = GetTaskbarLocation.Get();
    Check(IsLocation(current), "Public Get returns an exact direction");
    if (!GetTaskbarLocation.TryGet(out string? location))
        throw new InvalidOperationException("Public TryGet failed during the native smoke check.");
    Check(IsLocation(location), "Public TryGet returns an exact direction");
    Console.WriteLine($"Read-only native smoke passed: Get={current}, TryGet={location}.");
}
return 0;

void ExpectSuccess(GetTaskbarLocation.SettingsReader read, string expected, string name)
{
    Check(GetTaskbarLocation.Get(read) == expected, name + ": Get");
    string? location = "stale output";
    bool success = GetTaskbarLocation.TryGet(read, out location);
    Check(success && location == expected, name + ": TryGet");
}

void ExpectFailure(GetTaskbarLocation.SettingsReader read, int expectedError, string name)
{
    bool threw = false;
    try
    {
        GetTaskbarLocation.Get(read);
    }
    catch (Win32Exception error)
    {
        threw = true;
        Check(error.NativeErrorCode == expectedError, name + ": exact error code");
    }
    Check(threw, name + ": Get must throw");
    string? location = "stale output";
    bool success = GetTaskbarLocation.TryGet(read, out location);
    Check(!success && location is null, name + ": TryGet must clear output");
}

void CheckPropagates(Action action, Exception expected, string name)
{
    bool threw = false;
    try
    {
        action();
    }
    catch (Exception error) when (ReferenceEquals(error, expected))
    {
        threw = true;
    }
    Check(threw, name);
}

void Check(bool passed, string name)
{
    if (!passed)
        throw new InvalidOperationException(name);
    checks++;
}

static bool IsLocation(string? value) => value is "Left" or "Top" or "Right" or "Bottom";

static GetTaskbarLocation.SettingsReader Status(int status) =>
    (byte[] data, out uint type, ref uint size) =>
    {
        // Error outputs are deliberately invalid and must never be decoded.
        type = uint.MaxValue;
        size = uint.MaxValue;
        return status;
    };

static GetTaskbarLocation.SettingsReader Binary(byte[] bytes, uint valueType = 3) =>
    (byte[] data, out uint type, ref uint size) =>
    {
        if (size != data.Length)
            throw new InvalidOperationException("Reader was not given the full buffer capacity.");
        type = valueType;
        size = (uint)bytes.Length;
        if (bytes.Length > data.Length)
        {
            Array.Fill(data, (byte)0xFF);
            return 234;
        }
        bytes.CopyTo(data, 0);
        return 0;
    };

static GetTaskbarLocation.SettingsReader GrowingThen(GetTaskbarLocation.SettingsReader next) =>
    (byte[] data, out uint type, ref uint size) =>
    {
        if (data.Length < 96)
        {
            type = 3;
            size = 96;
            Array.Fill(data, (byte)0);
            return 234;
        }
        return next(data, out type, ref size);
    };
