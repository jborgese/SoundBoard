# Building SoundBoard

SoundBoard is a WPF application targeting **.NET Framework 4.8**, built from a classic
(non-SDK) `.csproj` that uses `PackageReference` for its NuGet dependencies. It must be built with the
full-framework MSBuild that ships with Visual Studio — `dotnet build` does **not** work
(see [Why `dotnet build` fails](#why-dotnet-build-fails)).

## Prerequisites

| Requirement | Details |
|---|---|
| OS | Windows 10 / 11 |
| Visual Studio | **Visual Studio 2026, 2022, or 2019** (any edition, including Community — CI uses whatever `windows-latest` ships, currently 2026). Visual Studio Build Tools also works for command-line-only builds. |
| VS workload | **.NET desktop development** |
| VS components | **.NET Framework 4.8 SDK** and **.NET Framework 4.8 targeting pack** (included in the workload by default). Without the targeting pack the build fails with `MSB3644`. |
| MSBuild | 16.0 or newer (installed by the above). Fody 6 requires MSBuild 16+ (VS 2019+). |
| NuGet | Nothing extra: all projects use `PackageReference`, so `msbuild /restore` (or Visual Studio) restores them. `nuget.exe` is not needed. |

The .NET Framework 4.8 *runtime* (needed to run the app, preinstalled on Windows 10
1903+ and all Windows 11) is declared in `SoundBoard/App.config` and enforced at
startup by `Utilities.IsRequiredNetFrameworkInstalled()` (registry release key
`528040`). All three agree on 4.8; keep them in sync if the target ever changes.

## Restore

From the repository root:

```powershell
msbuild SoundBoard.sln /t:Restore
```

(or pass `/restore` to the build command below to restore and build in one go).
Packages are restored to the global NuGet package cache (`%UserProfile%\.nuget\packages`)
and the resolved graph is written to each project's `obj\project.assets.json`; there is
no `packages\` folder any more. There are no prebuilt binaries in the repository: the two
small third-party libraries the app uses are built from vendored source (see
[Projects](#projects)).

## Build

From a *Developer Command Prompt / Developer PowerShell for VS* (or any shell where
`msbuild.exe` is on `PATH`):

```powershell
msbuild SoundBoard.sln /restore /p:Configuration=Release /m
```

Or open `SoundBoard.sln` in Visual Studio and build the `Release` configuration.

**Output:** `SoundBoard\bin\Release\SoundBoard.exe`. Costura.Fody embeds all managed
dependencies into the executable at build time — including `SoundBoard.Model.dll`,
`Dsafa.WpfColorPicker.dll` and `HotKeyManagement.WPF.4.dll` from the projects below — so
this single `.exe` is the complete, distributable application.

## Projects

| Project | Type | Purpose |
|---|---|---|
| `SoundBoard\SoundBoard.csproj` | classic WPF `.csproj`, `PackageReference` | The application. |
| `SoundBoard.Model\SoundBoard.Model.csproj` | SDK-style class library, `net48` | UI-free data model (`SoundBoardConfig` → `Page` → `Sound`) and the `soundboard.config` serializer. Deliberately references **no** WPF presentation assemblies (only `WindowsBase` for the `Key` enum and `System.Drawing` for color parsing) so nothing UI-specific can leak into it. |
| `SoundBoard.Tests\SoundBoard.Tests.csproj` | SDK-style xUnit, `net48` | Unit tests for the model and serializer, including golden-file config round-trips (`SoundBoard.Tests\Fixtures`). |
| `Dsafa.WpfColorPicker\Dsafa.WpfColorPicker.csproj` | SDK-style WPF class library, `net48` | Vendored **fork** of [dsafa/wpf-color-picker](https://github.com/dsafa/wpf-color-picker) 1.2.0 (MIT) — the colour picker dialog. `NOTICE.md` in that folder lists the changes from upstream. |
| `HotKeyManagement.WPF\HotKeyManagement.WPF.csproj` | SDK-style WPF class library, `net48` | Vendored copy of [BondTech.HotKeyManagement.WPF.4](https://github.com/bondtech/HotKey-Manager-for-WinForm-and-WPF-Apps) (MIT) — local/global hotkey registration. Unmodified apart from the project file; see its `NOTICE.md`. |

`msbuild SoundBoard.sln /t:Restore` restores all five projects. The SDK-style library projects
*can* be built with `dotnet build` on their own; the solution as a whole cannot, and neither can
the test project, because it references the app exe (see below).

## Test

After a Release build:

```powershell
vstest.console.exe SoundBoard.Tests\bin\Release\SoundBoard.Tests.dll
```

(`vstest.console.exe` ships with Visual Studio under
`Common7\IDE\CommonExtensions\Microsoft\TestWindow\` and is on `PATH` in a Developer
shell. It is *not* on `PATH` on GitHub's hosted runners, which is why the CI workflow
locates it with `vswhere`.)

## Notes and gotchas

- **COM reference (`stdole`):** the project has a `<COMReference>` to `stdole`, which
  is resolved by Visual Studio's interop tooling during the build. This is installed
  with the .NET desktop development workload; nothing extra to do, but it is one of
  the reasons full MSBuild is required (`MSB3091` if the SDK tools are missing).
- **Fody / Costura** (`Fody 6.9.3`, `Costura.Fody 6.2.0`) are ordinary
  `PackageReference`s with `PrivateAssets="all"`; their `.props`/`.targets` are imported
  automatically by NuGet, so there are no hard-coded package paths in the `.csproj`.
  `FodyWeavers.xml` (just `<Costura/>`) controls what is woven.
- **Debug builds are stricter than Release:** the Debug configuration turns warning
  `CS1591` (missing XML doc comment) into an error and generates `SoundBoard.xml`.
  Release does neither, so code that builds in Release may still fail in Debug.

### Why `dotnet build` fails

`dotnet build` / the .NET SDK MSBuild cannot build this project: the `stdole`
`<COMReference>` is unsupported there (error `MSB4803: The task "ResolveComReference"
is not supported on the .NET Core version of MSBuild`), and classic WPF `.csproj`
projects are generally not supported by the SDK toolchain. Always use `msbuild.exe`
from Visual Studio or Build Tools.

## Localization

All user-facing text comes from `SoundBoard/Properties/Resources.resx` (the neutral
resource set, in English). Each additional language is a sibling
`Properties/Resources.<tag>.resx`, which MSBuild compiles into a satellite assembly
(`bin\Release\<tag>\SoundBoard.resources.dll`). The app picks the language up at
startup, from the `Language` attribute of `<GlobalSettings>` in `soundboard.config`;
an absent or empty value means "follow the operating system", and the **•••** menu's
**Language** submenu writes it. Changing it takes effect on the next start, so the menu
offers to restart. `SoundBoard/Localization.cs` holds the list of shipped languages and
the `{local:Loc Key}` markup extension that XAML uses.

### Adding a language

1. Copy `SoundBoard/Properties/Resources.resx` to `Properties/Resources.<tag>.resx`
   (`<tag>` is an IETF language tag: `de`, `pt-BR`, …) and translate every `<value>`.
   Keep the `<comment>` notes — they say what each `{0}` is. A translation **may**
   reorder placeholders (`"{1} … {0}"`); it **must not** add or drop one.
2. Add it to `SoundBoard.csproj` next to the other `Resources.*.resx` entries:
   ```xml
   <EmbeddedResource Include="Properties\Resources.de.resx">
     <DependentUpon>Resources.resx</DependentUpon>
     <SubType>Designer</SubType>
   </EmbeddedResource>
   ```
3. Add the tag to `TranslatedLanguageTags` in `SoundBoard/Localization.cs` so it appears
   in the menu.
4. Build and run the tests. `SoundBoard.Tests/LocalizationTests.cs` fails if the
   translation is missing a string, has one the neutral resources no longer define, uses
   a different set of placeholders, or never made it into the build at all.

> **The Spanish translation has not been reviewed by a native speaker.** It is here to prove
> the mechanism end to end — that a translation reaches the running app, survives the
> single-file packing, and is caught by the tests when it drifts. Treat `Resources.es.resx`
> as a worked example of the *shape* of a translation, not as vetted Spanish, and have a
> speaker of the language check any translation before relying on it. Corrections to it are as
> welcome as new languages — open an issue or a pull request.

### Why the `.csproj` has an `EmbedOwnSatelliteAssemblies` target

Costura only embeds what MSBuild hands the weaver in `@(ReferenceCopyLocalPaths)`, and
Fody weaves during `CoreCompile` — before `CreateSatelliteAssemblies` has built *this*
project's own `SoundBoard.resources.dll`. Left alone, the single-file `SoundBoard.exe`
would contain the English strings and nothing else, while the translations sat in
`bin\Release\<tag>\` next to it. (Satellites belonging to *referenced packages*, such as
MahApps, are embedded normally — they are already copy-local by the time Fody runs, which
is what makes the omission easy to miss.)

Two things in `SoundBoard.csproj` fix it, both immediately above the `Version.targets`
import:

- `EmbedOwnSatelliteAssemblies`, hooked in through Fody's own `$(FodyDependsOnTargets)`,
  builds the satellites early and adds them to `@(ReferenceCopyLocalPaths)` (as full
  paths — Fody rejects relative ones). Costura then embeds each as
  `costura.<tag>.soundboard.resources.dll.compressed`, and its runtime assembly resolver
  serves it to `ResourceManager` on demand.
- `@(CustomAdditionalCompileInputs)` lists the translation `.resx` files, because nothing
  else makes editing one invalidate `CoreCompile`. Without it, an incremental build after
  a translation-only change leaves the *previous* translation embedded in the exe while
  the loose satellite on disk is up to date — which looks like the translation working
  everywhere except in the file you ship.

To check the packing by hand, copy **only** `SoundBoard.exe` to an empty folder and ask
its `ResourceManager` for a string in that culture:

```powershell
$asm = [Reflection.Assembly]::LoadFrom("$pwd\SoundBoard.exe")
[Runtime.CompilerServices.RuntimeHelpers]::RunModuleConstructor($asm.ManifestModule.ModuleHandle)
$rm = New-Object Resources.ResourceManager('SoundBoard.Properties.Resources', $asm)
$rm.GetString('AddPageButton', [Globalization.CultureInfo]::GetCultureInfo('es'))   # -> añadir página
```

`RunModuleConstructor` is what installs Costura's assembly resolver; without it the
lookup falls back to English and the check passes for the wrong reason.

## Continuous integration

[.github/workflows/build.yml](.github/workflows/build.yml) runs on every push and
pull request: it checks out the repo on `windows-latest` (Windows Server 2025 with
Visual Studio 2026 preinstalled, as of the June 2026 runner-image migration —
pin to `windows-2022` instead if you ever need the older VS 2022 image), restores and
builds `Release` with `msbuild /restore`, runs the unit tests with
`vstest.console.exe`, and uploads the resulting `SoundBoard.exe` as a workflow artifact.
Releases are produced by a separate tag-triggered workflow; see
[Versioning and releases](#versioning-and-releases).

Visual Studio 2026 has an open IDE-integrated NuGet Package Manager bug
([NU1109](https://github.com/nuget/home/issues/14653)) around central package
management version resolution. This project does not use central package management,
but if a restore ever misbehaves inside the VS 2026 IDE, fall back to the command-line
`msbuild /t:Restore` shown above.

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
[SoundBoard/AppUpdate.xsd](SoundBoard/AppUpdate.xsd) and documented element by element -
together with the notify modes, the download-entry resolution and the apply step - in
[docs/update-manifest.md](docs/update-manifest.md).

Because `VersionInfo.xml` on `master` is overwritten by the workflow, never edit it by
hand; to preview what a tag would produce, run the script locally:

```powershell
.\scripts\New-VersionInfo.ps1 -Tag v1.10.3 -ExePath SoundBoard\bin\Release\SoundBoard.exe `
    -Repository micahmo/SoundBoard -OutputPath $env:TEMP\VersionInfo.xml -AllowMissingNotes
```
