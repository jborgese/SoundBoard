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
shell. It is *not* on `PATH` on GitHub's hosted runners, which is why the CI workflow
locates it with `vswhere`.) Or run `dotnet test SoundBoard.Tests\SoundBoard.Tests.csproj`, which builds only
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
Releases are produced by a separate tag-triggered workflow; see
[Versioning and releases](#versioning-and-releases).

Visual Studio 2026 has an open IDE-integrated NuGet Package Manager bug
([NU1109](https://github.com/nuget/home/issues/14653)) around central package
management version resolution. It's specific to `PackageReference`/central
transitive pinning and does not apply to this project's `packages.config`
restore, but if a restore ever misbehaves inside the VS 2026 IDE, fall back to
the command-line `nuget restore` shown above.

## Versioning and releases

The **git tag is the single source of truth for the version.** Nothing is bumped by
hand: there are no version attributes in `AssemblyInfo.cs` and `SoundBoard/VersionInfo.xml`
is generated.

### How the version gets into the build

[SoundBoard/Version.targets](SoundBoard/Version.targets) (imported by `SoundBoard.csproj`)
generates `obj\<Configuration>\SoundBoardVersion.g.cs` containing `AssemblyVersion`,
`AssemblyFileVersion` and `AssemblyInformationalVersion` on every build, from the first of:

1. `/p:SoundBoardVersion=1.10.3.0` (plus optional `/p:SoundBoardInformationalVersion=v1.10.3`)
   on the MSBuild command line - this is what the release workflow passes.
2. The nearest reachable `v*` tag (`git describe --tags --abbrev=0 --match "v[0-9]*"`).
   A local build after `v1.10.3` is therefore stamped `1.10.3.0`, the same as the last
   release, so it never offers itself an update.
3. `0.0.0.0`, with a build warning, if neither is available (no git on `PATH`, shallow clone
   without tags, ...).

Tag formats: `v1.10.3` -> `1.10.3.0`; `v1.5` -> `1.5.0.0`; `v1.10.3-beta1` -> `1.10.3.0`
(the suffix is kept only in `AssemblyInformationalVersion`, shown as "Product version" in
the file properties dialog).

The same generated file defines `BuildInfo.Repository` and `BuildInfo.UpdateManifestUrl`
from `/p:SoundBoardRepository=owner/name` (default `micahmo/SoundBoard`). The release
workflow passes the repository it runs in, so an exe built by a fork polls the fork's own
manifest and download URLs - the updater can never point at a different repository than
the one that published the build.

### Cutting a release

1. Make sure every user-visible change is listed under `## [Unreleased]` in
   [CHANGELOG.md](CHANGELOG.md), then rename that heading to `## [1.10.3] - YYYY-MM-DD`
   (and start a fresh, empty `## [Unreleased]` above it). The release workflow fails
   early if the section for the tag is missing, because that section *is* the release
   notes.
2. Commit to `master` and tag it:

   ```powershell
   git tag v1.10.3
   git push origin master v1.10.3
   ```

   Use a suffix (`v1.11.0-beta1`) for a pre-release.

3. [.github/workflows/release.yml](.github/workflows/release.yml) then does everything else:
   - checks the tag is reachable from `master`;
   - builds `Release` with `/p:SoundBoardVersion` derived from the tag and
     `/p:SoundBoardRepository` set to the repository running the workflow, and runs the tests;
   - verifies the built exe really carries that version;
   - runs [scripts/New-VersionInfo.ps1](scripts/New-VersionInfo.ps1), which writes
     `SoundBoard/VersionInfo.xml` with the version, the download URLs for this repository,
     the **SHA-256 of the built exe in `<FileHash algorithm="SHA256">`**, and the changelog
     section as `<VersionNotes>`; the result is validated against `AppUpdate.xsd`;
   - creates the GitHub Release (`--prerelease` for suffixed tags) with `SoundBoard.exe`,
     `SoundBoard.exe.sha256` and `VersionInfo.xml` attached, using the changelog section
     as the release body;
   - for stable releases, commits the regenerated `VersionInfo.xml` back to `master`
     **after** the release exists, so the manifest never points at a missing file.
     Pre-releases skip this step so existing installs are never offered a beta.

### How the in-app updater consumes this

`MainWindow` constructs `MyUpdateChecker` with `BuildInfo.UpdateManifestUrl`
(`https://raw.githubusercontent.com/<owner>/SoundBoard/master/SoundBoard/VersionInfo.xml`).
The Bluegrams `AppHelpers.WPF` update checker downloads that manifest, compares
`<Version>` to the running `AssemblyVersion` and downloads the `<Download key="portable">`
entry into `%TEMP%`. `MyUpdateChecker` then enforces a fail-closed policy that is stricter
than the library's own (which passes an empty hash, skips a missing `<FileHash>` element,
and defaults the algorithm to MD5): the entry **must** carry
`<FileHash algorithm="SHA256">` with a 64-hex-digit value, and the download must match it,
or the file is deleted and an error dialog is shown. Nothing is ever applied unverified.

The verified file is then swapped into place by `SoundBoard/Update/UpdateApplier.cs`
without any shell: Windows allows a running executable to be *renamed* (not deleted), so
`SoundBoard.exe` is renamed to `SoundBoard.exe.old` and the download moved into its place
in-process, after which the app shuts down cleanly (saving settings) and starts the new
executable. No elevation is needed for a portable exe in a user-writable folder. Only if
the folder is not writable (e.g. `Program Files`) does it request UAC, and then the
elevated process is `SoundBoard.exe --apply-update <file> <sha256>` - a mode that can only
replace its own image, and only with a file matching the given hash. `SoundBoard.exe.old`
is deleted on the next start. The manifest format is described by
[SoundBoard/AppUpdate.xsd](SoundBoard/AppUpdate.xsd).

Because `VersionInfo.xml` on `master` is overwritten by the workflow, never edit it by
hand; to preview what a tag would produce, run the script locally:

```powershell
.\scripts\New-VersionInfo.ps1 -Tag v1.10.3 -ExePath SoundBoard\bin\Release\SoundBoard.exe `
    -Repository micahmo/SoundBoard -OutputPath $env:TEMP\VersionInfo.xml -AllowMissingNotes
```
