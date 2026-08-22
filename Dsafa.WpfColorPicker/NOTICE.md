# Dsafa.WpfColorPicker

This directory is a **modified fork** of **WpfColorPicker 1.2.0** by Brandon Chong (dsafa),
<https://github.com/dsafa/wpf-color-picker> (tag `v1.2.0`, `src/WpfColorPicker`), licensed under
the MIT License; see [LICENSE](LICENSE).

SoundBoard previously shipped a prebuilt `Dsafa.WpfColorPicker.dll` (1.2.0) in `SoundBoard\lib`
that had been modified from upstream without the source being kept. This project rebuilds that
library from the upstream 1.2.0 source and re-implements the modifications.

## Changes from upstream

- `ColorPickerDialog.ShowTransparencyPicker` (bool, default `true`): setting it to `false` calls
  `ColorPicker.HideTransparencyPicker()`, which removes the transparency (alpha) slider from the
  picker. The numeric `A:` input is not touched, matching the previous binary. (SoundBoard sets
  this to `false`.)
- `ColorPicker.xaml`: the outer `DockPanel` and the `TransparencyPicker` are given `x:Name`s for
  the above.
- `ColorPickerDialog.DefaultPalette` is `public static readonly ReadOnlyCollection<Color>` instead
  of a private `ObservableCollection<Color>`, so SoundBoard can merge it into its own palette
  without reflection.
- Replaced the old-style `.csproj` (targeting .NET Framework 4.6.2) with an SDK-style project
  targeting .NET Framework 4.8. The unused `Properties\Resources.*` / `Settings.*` files were not
  carried over; `Properties\AssemblyInfo.cs` now only contains the `ThemeInfo` attribute.
- The `Example` app and `WpfColorPickerTests` from the upstream repository are not included.
