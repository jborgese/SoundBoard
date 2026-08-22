# The update manifest (`VersionInfo.xml`)

SoundBoard updates itself from a single XML file published on `master`:

```
https://raw.githubusercontent.com/<owner>/SoundBoard/master/SoundBoard/VersionInfo.xml
```

That file is the **update manifest**. It declares the newest released version, where to download it,
and what its hash is. Its shape is defined by [`SoundBoard/AppUpdate.xsd`](../SoundBoard/AppUpdate.xsd),
and it is read by the [Bluegrams `AppHelpers.WPF`](https://github.com/bluegrams/apphelpers) update
checker, which SoundBoard subclasses in [`SoundBoard/MyUpdateChecker.cs`](../SoundBoard/MyUpdateChecker.cs)
and backs with [`SoundBoard/Update/`](../SoundBoard/Update/) (`UpdateVerifier`, `UpdateApplier`).

`VersionInfo.xml` is **generated** by [`scripts/New-VersionInfo.ps1`](../scripts/New-VersionInfo.ps1)
during the release workflow and committed back to `master`. Never edit it by hand — the next release
overwrites it. See [Cutting a release](../BUILDING.md#cutting-a-release).

## Format

```xml
<?xml version="1.0" encoding="utf-8" ?>
<AppUpdate xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
           xsi:noNamespaceSchemaLocation="https://raw.githubusercontent.com/micahmo/SoundBoard/master/SoundBoard/AppUpdate.xsd">
  <Version>1.10.3.0</Version>
  <ReleaseDate>2026-08-22</ReleaseDate>
  <!-- Default download -->
  <DownloadLink>https://github.com/micahmo/SoundBoard/releases/download/v1.10.3/SoundBoard.exe</DownloadLink>
  <DownloadFileName>SoundBoard.exe</DownloadFileName>
  <!-- All download options -->
  <Downloads>
    <Download key="portable">
      <Link>https://github.com/micahmo/SoundBoard/releases/download/v1.10.3/SoundBoard.exe</Link>
      <FileName>SoundBoard.exe</FileName>
      <FileHash algorithm="SHA256">3F2B...</FileHash>
    </Download>
  </Downloads>
  <!-- Release notes -->
  <VersionNotes> - Add ability to choose sound output separately from audio passthrough output</VersionNotes>
  <ReleaseNotes></ReleaseNotes>
</AppUpdate>
```

The root element is `AppUpdate`, in **no namespace**. The schema declares its children as `xs:all`, so
they may appear in any order, but each at most once.

| Element | Required | Meaning |
|---|---|---|
| `<Version>` | yes | The newest released version. Parsed with `System.Version` and compared with `>` against the running assembly's `AssemblyVersion`, so it must be a plain 2-to-4-part numeric version — `1.10.3.0`, never `v1.10.3` or `1.10.3-beta1`. A malformed value throws and the check fails. |
| `<ReleaseDate>` | yes | `xs:date` (`YYYY-MM-DD`). Shown in the update dialog. |
| `<DownloadLink>` | no | Fallback download URL, used when no `<Download>` entry matches (see [Choosing a download](#choosing-a-download)). |
| `<DownloadFileName>` | no | File name for that fallback download. If omitted, the last segment of the URL is used. |
| `<Downloads>` | no | Zero or more `<Download key="...">` entries — the named download options. |
| `<VersionNotes>` | no | Plain-text release notes shown in the update dialog. The generator flattens the changelog section into `" - item"` lines. |
| `<ReleaseNotes>` | no | Zero or more `<ReleaseNote lang="...">` elements for localized notes. The checker picks the entry matching the requested language and falls back to `<VersionNotes>`. SoundBoard does not currently publish any. |

### `<Download>`

| Part | Required | Meaning |
|---|---|---|
| `key` attribute | yes | The identifier the app asks for. SoundBoard uses **`portable`**. |
| `<Link>` | yes | Absolute download URL. |
| `<FileName>` | no | Name to save as, under `%TEMP%`. Defaults to the last segment of `<Link>`. |
| `<FileHash algorithm="...">` | by the schema, no — **by SoundBoard, yes** | Expected hash of the downloaded file, hex, compared case-insensitively. It must be `algorithm="SHA256"` with a 64-hex-digit value: SoundBoard refuses to apply an update otherwise (see [Verification is fail-closed](#verification-is-fail-closed)). The attribute is optional in the schema, and the library defaults to `MD5` when it is absent, which is exactly why SoundBoard checks it itself. |

## How the app consumes it

[`MainWindow`](../SoundBoard/MainWindow.xaml.cs) creates one `MyUpdateChecker` at startup:

```csharp
_updateChecker = new MyUpdateChecker(BuildInfo.UpdateManifestUrl)
{
    Owner = this,
    DownloadIdentifier = "portable"
};
```

`BuildInfo.UpdateManifestUrl` is generated at build time by
[`SoundBoard/Version.targets`](../SoundBoard/Version.targets) from `/p:SoundBoardRepository=owner/name`,
so a build produced by a fork polls the fork's own manifest — the updater can never be pointed at a
different repository than the one that published the binary.

The check runs in two places:

* `Window_Loaded` → `CheckForUpdates(UpdateNotifyMode.Auto)` — silent on startup unless there is
  something new to show.
* the **about** dialog's *Check for updates* button → `CheckForUpdates(UpdateNotifyMode.Always)` —
  which also reports "no new update" and check failures, where `Auto` stays quiet.

The check then proceeds as follows. Steps 1-4 are the library's; 5 and 6 are the hooks SoundBoard
overrides:

1. **Fetch and deserialize.** The manifest is downloaded with `WebClient` and deserialized with
   `XmlSerializer` into an `AppUpdate` object. A network error or malformed XML is a failed check
   (reported only in `Always` mode).
2. **Compare versions.** `NewVersion` is `new Version(<Version>) > new Version(AppInfo.Version)`, where
   `AppInfo.Version` is the running assembly's version. Equal or lower means no update. This is why a
   local build stamps itself with the last reachable tag: it then never offers itself an update.
3. **Decide whether to notify.** Two settings are persisted per install: `CheckedUpdate` (the highest
   version already shown) and `SkipVersion` (set when the user dismisses the update dialog with *skip*).
   A version higher than `CheckedUpdate` resets `SkipVersion`, so a *skip* applies only to the version it
   was made for. Under `Auto` the dialog is suppressed while `SkipVersion` is set and the manifest
   version is not newer than the last one shown; `Always` ignores both.
4. **Resolve the download.** See [Choosing a download](#choosing-a-download) below.
5. **Download and verify.** The file is fetched into `%TEMP%` under `<FileName>`, then passed to the
   `VerifyHash` hook — which SoundBoard overrides to be
   [fail-closed](#verification-is-fail-closed). On failure the library **deletes** the file and raises
   `UpdateFailedException`.
6. **Apply.** `MyUpdateChecker.ShowUpdateDownload` replaces the library default (which merely reveals the
   downloaded file in Explorer) with [`UpdateApplier`](../SoundBoard/Update/UpdateApplier.cs), see
   [Applying the update](#applying-the-update).

### Choosing a download

`ResolveDownloadEntry` picks the first `<Download>` whose `key` equals `DownloadIdentifier`
(`"portable"`). **If no entry matches, it silently falls back** to an entry synthesized from
`<DownloadLink>` / `<DownloadFileName>` — and that synthesized entry carries **no hash at all**. So a
manifest whose `portable` key is missing or misspelled does not fail: it quietly downgrades to the
unverified default download. Keep the `key="portable"` entry present and correct.

### Verification is fail-closed

The library's own hash check is permissive in three ways, all of which mean "not verified" rather than
"rejected":

* `VerifyHash` returns **true** for an empty `<FileHash></FileHash>` — as published in every manifest
  before SHA-256 was added;
* a missing `<FileHash>` element (including the `<DownloadLink>` fallback above) is never hashed at all; and
* `algorithm` is optional and defaults to **MD5**, which is useless against a deliberately altered file.

[`UpdateVerifier`](../SoundBoard/Update/UpdateVerifier.cs) closes all three. SoundBoard requires the
resolved entry to carry `<FileHash algorithm="SHA256">` holding exactly 64 hex digits, and requires the
downloaded file to match it. Anything else — absent, empty, MD5 or SHA-1, malformed — is a failure: the
download is deleted, the reason is logged, and an error dialog names it. Nothing is ever applied
unverified.

The check runs twice, because the two paths catch different things.
`MyUpdateChecker.VerifyHash` tightens the library hook, and `ShowUpdateDownload` then re-derives the
expected hash from the manifest entry independently — which is what catches the cases the hook is never
called for, namely a missing `<FileHash>` element or a silent fallback to `<DownloadLink>`.
`UpdateApplier` verifies once more immediately before the swap, closing the window in which the file
could be replaced while it sits in `%TEMP%`.

The release workflow keeps manifests on the right side of this: it always emits a `portable` entry with a
populated `<FileHash algorithm="SHA256">` computed from the exact `SoundBoard.exe` attached to the
release, and validates the generated file against `AppUpdate.xsd` before publishing. A hand-written
manifest that omits the hash will not silently install something unchecked — it will refuse to install
at all.

### Applying the update

Windows lets a running executable be *renamed*, just not deleted or overwritten, so
[`UpdateApplier`](../SoundBoard/Update/UpdateApplier.cs) swaps it in-process with two file moves and no
shell at all: `SoundBoard.exe` → `SoundBoard.exe.old`, then the download into `SoundBoard.exe`. If the
second move fails the first is rolled back, so the executable is never left missing. The app then shuts
down through the normal WPF `Exit` path — settings are saved — and only then starts the new executable.

For a portable exe in a user-writable folder this needs **no elevation**. Only when the folder is not
writable (say the exe lives under `Program Files`) is UAC requested, and the elevated process is this
same executable run as `SoundBoard.exe --apply-update <file> <sha256>`: a mode handled in
[`App.xaml.cs`](../SoundBoard/App.xaml.cs) that shows no UI, can only replace *its own* image, and only
with a file whose SHA-256 equals the hash on the command line (arguments are quoted per
`CommandLineToArgvW` rules by [`CommandLine`](../SoundBoard/Update/CommandLine.cs)). The new instance is
always started by the *non-elevated* process, so it runs with the user's normal token — an elevated
instance would break drag-and-drop through UIPI.

`SoundBoard.exe.old` is deleted at the next startup.

## How it is generated

[`scripts/New-VersionInfo.ps1`](../scripts/New-VersionInfo.ps1) derives everything from the git tag:

| Manifest part | Source |
|---|---|
| `<Version>` | the tag — `v1.10.3` → `1.10.3.0`, `v1.5` → `1.5.0.0`; a `-beta1` suffix is dropped here |
| `<ReleaseDate>` | today (UTC), or `-ReleaseDate` |
| `<DownloadLink>`, `<Link>` | `https://github.com/<Repository>/releases/download/<tag>/SoundBoard.exe` |
| `<FileHash>` | SHA-256 of the built exe passed as `-ExePath` |
| `<VersionNotes>` | the `## [X.Y.Z]` section of [`CHANGELOG.md`](../CHANGELOG.md), flattened to `" - item"` lines |

To preview what a tag would produce, without releasing anything:

```powershell
.\scripts\New-VersionInfo.ps1 -Tag v1.10.3 -ExePath SoundBoard\bin\Release\SoundBoard.exe `
    -Repository micahmo/SoundBoard -OutputPath $env:TEMP\VersionInfo.xml -AllowMissingNotes
```

[`.github/workflows/release.yml`](../.github/workflows/release.yml) runs the script, validates the result
against the schema, attaches `SoundBoard.exe`, `SoundBoard.exe.sha256` and `VersionInfo.xml` to the GitHub
Release, and only then commits the manifest to `master` — so the manifest never points at a file that does
not exist yet. Pre-releases skip that commit, which is what keeps existing installs from being offered a
beta.

## Changing the schema

`AppUpdate.xsd` describes the format `AppHelpers.WPF` knows how to deserialize; it is not a place to
invent new elements. Every copy of SoundBoard in the field reads the manifest with the deserializer that
shipped inside it, so anything added must be optional and safely ignorable by older builds. The
`xsi:noNamespaceSchemaLocation` on the manifest points at the copy of the schema on `master`, while the
release workflow validates against the copy in the working tree.
