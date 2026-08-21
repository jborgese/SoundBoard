# Building SoundBoard

SoundBoard is a WPF application targeting **.NET Framework 4.8**, built from a classic
(non-SDK) `.csproj` with `packages.config` NuGet restore. It must be built with the
full-framework MSBuild that ships with Visual Studio — `dotnet build` does **not** work
(see [Why `dotnet build` fails](#why-dotnet-build-fails)).

## Prerequisites

| Requirement | Details |
|---|---|
| OS | Windows 10 / 11 |
| Visual Studio | **Visual Studio 2026, 2022, or 2019** (any edition, including Community — CI uses whatever `windows-latest` ships, currently 2026). Visual Studio Build Tools also works for command-line-only builds. |
| VS workload | **.NET desktop development** |
| VS components | **.NET Framework 4.8 SDK** and **.NET Framework 4.8 targeting pack** (included in the workload by default). Without the targeting pack the build fails with `MSB3644`. |
| MSBuild | 16.0 or newer (installed by the above). The pinned Fody 5.1.1 build weaver does not support older MSBuild versions. |
| NuGet | `nuget.exe` 5.x or newer for command-line restore (verified with 7.9.0), or just let Visual Studio restore. Download: <https://dist.nuget.org/win-x86-commandline/latest/nuget.exe> |

The .NET Framework 4.8 *runtime* (needed to run the app, preinstalled on Windows 10
1903+ and all Windows 11) is declared in `SoundBoard/App.config` and enforced at
startup by `Utilities.IsRequiredNetFrameworkInstalled()` (registry release key
`528040`). All three agree on 4.8; keep them in sync if the target ever changes.

## Restore

From the repository root:

```powershell
nuget restore SoundBoard.sln
```

Packages are restored to `packages\` at the repository root (the `.csproj` hint paths
expect this location). Two additional prebuilt dependencies are checked into
`SoundBoard\lib\` (`Dsafa.WpfColorPicker.dll`, `HotKeyManagement.WPF.4.dll`) and need
no restore step.

## Build

From a *Developer Command Prompt / Developer PowerShell for VS* (or any shell where
`msbuild.exe` is on `PATH`):

```powershell
msbuild SoundBoard.sln /p:Configuration=Release /m
```

Or open `SoundBoard.sln` in Visual Studio and build the `Release` configuration.

**Output:** `SoundBoard\bin\Release\SoundBoard.exe`. Costura.Fody embeds all managed
dependencies into the executable at build time — including `SoundBoard.Model.dll` from
the model project below — so this single `.exe` is the complete, distributable
application.

## Projects

| Project | Type | Purpose |
|---|---|---|
| `SoundBoard\SoundBoard.csproj` | classic WPF `.csproj`, `packages.config` | The application. |
| `SoundBoard.Model\SoundBoard.Model.csproj` | SDK-style class library, `net48` | UI-free data model (`SoundBoardConfig` → `Page` → `Sound`) and the `soundboard.config` serializer. Deliberately references **no** WPF presentation assemblies (only `WindowsBase` for the `Key` enum and `System.Drawing` for color parsing) so nothing UI-specific can leak into it. |
| `SoundBoard.Tests\SoundBoard.Tests.csproj` | SDK-style xUnit, `net48` | Unit tests for the model and serializer, including golden-file config round-trips (`SoundBoard.Tests\Fixtures`). |

`nuget restore SoundBoard.sln` restores both the `packages.config` project and the
`PackageReference` projects (NuGet 5+). The two SDK-style projects *can* be built and
tested with `dotnet build` / `dotnet test SoundBoard.Tests` on their own; the solution as
a whole cannot (see below).

## Test

After a Release build:

```powershell
vstest.console.exe SoundBoard.Tests\bin\Release\SoundBoard.Tests.dll
```

(`vstest.console.exe` ships with Visual Studio under
`Common7\IDE\CommonExtensions\Microsoft\TestWindow\` and is on `PATH` in a Developer
shell.) Or run `dotnet test SoundBoard.Tests\SoundBoard.Tests.csproj`, which builds only
the model and test projects.

## Notes and gotchas

- **COM reference (`stdole`):** the project has a `<COMReference>` to `stdole`, which
  is resolved by Visual Studio's interop tooling during the build. This is installed
  with the .NET desktop development workload; nothing extra to do, but it is one of
  the reasons full MSBuild is required (`MSB3091` if the SDK tools are missing).
- **Fody / Costura are pinned** (`Fody 5.1.1`, `Costura.Fody 4.0.0`) and imported by
  explicit path from `packages\` in the `.csproj`. Don't bump them casually — Fody 6
  changes the weaver contract, and the `.csproj` import paths encode the versions.
- **Debug builds are stricter than Release:** the Debug configuration turns warning
  `CS1591` (missing XML doc comment) into an error and generates `SoundBoard.xml`.
  Release does neither, so code that builds in Release may still fail in Debug.

### Why `dotnet build` fails

`dotnet build` / the .NET SDK MSBuild cannot build this project: the `stdole`
`<COMReference>` is unsupported there (error `MSB4803: The task "ResolveComReference"
is not supported on the .NET Core version of MSBuild`), and classic WPF `.csproj`
projects are generally not supported by the SDK toolchain. Always use `msbuild.exe`
from Visual Studio or Build Tools.

## Continuous integration

[.github/workflows/build.yml](.github/workflows/build.yml) runs on every push and
pull request: it checks out the repo on `windows-latest` (Windows Server 2025 with
Visual Studio 2026 preinstalled, as of the June 2026 runner-image migration —
pin to `windows-2022` instead if you ever need the older VS 2022 image), restores
with `nuget restore`, builds `Release` with MSBuild, runs the unit tests with
`vstest.console.exe`, and uploads the resulting `SoundBoard.exe` as a workflow artifact.

Visual Studio 2026 has an open IDE-integrated NuGet Package Manager bug
([NU1109](https://github.com/nuget/home/issues/14653)) around central package
management version resolution. It's specific to `PackageReference`/central
transitive pinning and does not apply to this project's `packages.config`
restore, but if a restore ever misbehaves inside the VS 2026 IDE, fall back to
the command-line `nuget restore` shown above.
