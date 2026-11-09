# GetTaskbarLocation

Reads the current user's saved taskbar location on .NET 10 for Windows; version 1.0.1.

```csharp
using SevenKeyboard;

string location = GetTaskbarLocation.Get();
if (GetTaskbarLocation.TryGet(out string? savedLocation))
    System.Console.WriteLine(savedLocation);
```

Reads only `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\StuckRects3\Settings`.
As in the AHK original, byte offset 12 of the binary value maps 0 to `"Left"`, 1 to
`"Top"`, 2 to `"Right"`, and every other byte to `"Bottom"`. A missing key or value,
or a binary payload shorter than 13 bytes, also returns `"Bottom"`.
This reports the saved setting, which may differ from the visible taskbar; it does
not query monitor-specific taskbars. No registry settings are changed.

C# exception policy: unlike the original catch-all fallback, `Get()` throws
`System.ComponentModel.Win32Exception` for registry read/access errors or a
non-binary value. `TryGet` returns `false` with `null` for these failures;
fallbacks are successful results. Other exceptions propagate from either method.
The original version 1.0.1 is retained for this first C# API.

Build and run the dependency-free console tests:

```powershell
dotnet run --project tests/GetTaskbarLocation.Tests -c Release -r win-x64
dotnet build tests/GetTaskbarLocation.Tests -c Release -r win-x86
dotnet publish tests/GetTaskbarLocation.Tests -c Release -r win-x64 -p:PublishAot=true -o artifacts/native-x64
```

The default tests use an injected registry reader and never access the registry.
Append `-- --native` to `dotnet run` (or `--native` to the published executable)
for an optional read-only smoke check of the public API. Run executables only
after any signing required by local policy. The library is NativeAOT-compatible.

Native read contract: [RegGetValueW](https://learn.microsoft.com/en-us/windows/win32/api/winreg/nf-winreg-reggetvaluew).
