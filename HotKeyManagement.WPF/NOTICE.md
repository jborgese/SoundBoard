# HotKeyManagement.WPF

This directory is a vendored copy of **BondTech.HotKeyManagement.WPF.4** by Bond Technologies,
taken from <https://github.com/bondtech/HotKey-Manager-for-WinForm-and-WPF-Apps>
(project folder `BondTech.HotKeyManagement.WPF.4`, last upstream change 2014).
It is licensed under the MIT License; see [LICENSE.md](LICENSE.md).

It replaces the prebuilt `HotKeyManagement.WPF.4.dll` (1.0.0, a Debug build) that was previously
checked into `SoundBoard\lib`, so that the code is built from source, in Release, with the rest of
the solution. The assembly name (`HotKeyManagement.WPF.4`) and namespace
(`BondTech.HotKeyManagement.WPF._4`) are unchanged.

## Changes from upstream

- Replaced the old-style `.csproj` (targeting .NET Framework 4.0) with an SDK-style project
  targeting .NET Framework 4.8. The unused `Properties\Resources.*` / `Settings.*` files were not
  carried over; `Properties\AssemblyInfo.cs` now only contains the `ThemeInfo` attribute (the
  other assembly attributes are generated from the project file).
- No source (.cs / .xaml) changes.
