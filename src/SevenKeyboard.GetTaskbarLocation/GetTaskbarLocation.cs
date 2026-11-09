//==============================================================
// GetTaskbarLocation - Read the saved Windows taskbar location
//
// GitHub: https://github.com/SevenKeyboard/get-taskbar-location
// Author: SevenKeyboard Ltd. (2026)
// License: The Unlicense
//==============================================================

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace SevenKeyboard;

public static partial class GetTaskbarLocation
{
    private const string SettingsKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StuckRects3";
    private const string SettingsValue = "Settings";
    private static readonly nint HkeyCurrentUser = unchecked((nint)(int)0x80000001);
    private const uint RrfRtRegBinary = 0x08;
    private const uint RegBinary = 3;
    private const int ErrorSuccess = 0;
    private const int ErrorFileNotFound = 2;
    private const int ErrorPathNotFound = 3;
    private const int ErrorInvalidData = 13;
    private const int ErrorMoreData = 234;
    private const int ErrorUnsupportedType = 1630;

    /// <summary>Returns the saved taskbar location: Left, Top, Right, or Bottom.</summary>
    /// <remarks>A missing key/value, short binary value, or unknown direction returns Bottom.</remarks>
    /// <exception cref="Win32Exception">The registry read fails or the value is not binary.</exception>
    public static string Get() => Get(ReadSettings);

    /// <summary>Reads the saved taskbar location; returns false and null on a registry read error.</summary>
    /// <remarks>The Bottom fallbacks are successful results. Other exceptions propagate.</remarks>
    public static bool TryGet([NotNullWhen(true)] out string? location) =>
        TryGet(ReadSettings, out location);

    // Per-call injection tests the same status handling as the public API without registry writes.
    internal delegate int SettingsReader(byte[] data, out uint type, ref uint dataSize);

    internal static string Get(SettingsReader read)
    {
        byte[] data = new byte[48];
        while (true)
        {
            uint dataSize = (uint)data.Length;
            int result = read(data, out uint type, ref dataSize);
            if (result is ErrorFileNotFound or ErrorPathNotFound)
                return "Bottom";

            // RegGetValueW leaves a too-small buffer undefined. Retry before decoding any byte.
            if (result == ErrorMoreData && dataSize > data.Length)
            {
                data = new byte[checked((int)dataSize)];
                continue;
            }

            if (result != ErrorSuccess)
                throw new Win32Exception(result);
            if (type != RegBinary)
                throw new Win32Exception(ErrorUnsupportedType);
            if (dataSize > data.Length)
                throw new Win32Exception(ErrorInvalidData);

            // AHK SubStr(hex, 25, 2) selects exactly this byte, not a DWORD.
            return dataSize <= 12 ? "Bottom" : data[12] switch
            {
                0 => "Left",
                1 => "Top",
                2 => "Right",
                _ => "Bottom"
            };
        }
    }

    internal static bool TryGet(SettingsReader read, [NotNullWhen(true)] out string? location)
    {
        location = null;
        try
        {
            location = Get(read);
            return true;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

    private static unsafe int ReadSettings(byte[] data, out uint type, ref uint dataSize)
    {
        fixed (byte* buffer = data)
        {
            return RegGetValueW(HkeyCurrentUser, SettingsKey, SettingsValue, RrfRtRegBinary,
                out type, buffer, ref dataSize);
        }
    }

    [LibraryImport("advapi32.dll", EntryPoint = "RegGetValueW", StringMarshalling = StringMarshalling.Utf16)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static unsafe partial int RegGetValueW(nint key, string subKey, string value,
        uint flags, out uint type, byte* data, ref uint dataSize);
}
