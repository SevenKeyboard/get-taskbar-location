# Validation

Verified 2026-11-10 on Windows 11 x64 with SDK 10.0.403.

Managed x64, managed x86 and NativeAOT x64 all built without warnings/errors
and passed 1,136 injected checks each. Signed binaries also passed the read-only
public registry smoke check: Get and TryGet both returned Bottom on this machine.

Coverage: missing key/value, empty/short binary values, every direction byte,
offset isolation, resized/shrunk/deleted values between reads, native read errors,
invalid data types, TryGet clearing its output, and unexpected errors propagating.
No registry settings were changed. Windows 10 execution was not tested.