# The update manifest (`VersionInfo.xml`)

SoundBoard updates itself from a single XML file published on `master`:

```
https://raw.githubusercontent.com/<owner>/SoundBoard/master/SoundBoard/VersionInfo.xml
```

That file is the **update manifest**. It declares the newest released version, where to download it,
and what its hash is. Its shape is defined by [`SoundBoard/AppUpdate.xsd`](../SoundBoard/AppUpdate.xsd),
and it is read by the [Bluegrams `AppHelpers.WPF`](https://github.com/bluegrams/apphelpers) update
checker via [`SoundBoard/MyUpdateChecker.cs`](../SoundBoard/MyUpdateChecker.cs).

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
| `<FileHash algorithm="...">` | no | Expected hash of the downloaded file, hex, compared case-insensitively. **Always set `algorithm="SHA256"`** — the attribute is optional in the schema and the library defaults to `MD5` when it is absent. |

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

Then, inside the library:

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
5. **Download and verify.** The file is fetched into `%TEMP%` under `<FileName>`. If the resolved entry
   carries a non-empty `<FileHash>`, the hash is recomputed with the named algorithm and compared
   case-insensitively; on mismatch the file is **deleted** and an `UpdateFailedException`
   ("File verification failed.") is surfaced to the user.
6. **Apply.** `MyUpdateChecker.ShowUpdateDownload` overrides the library default (which merely reveals the
   downloaded file in Explorer) and swaps the running executable in place. It starts two processes: an
   elevated `cmd.exe` that kills the current process, renames the running `SoundBoard.exe` to
   `SoundBoard.exe.old` and moves the download into its place (so the swap prompts for UAC); and a
   non-elevated `powershell.exe` that waits until the file at that path exists *and* its SHA-256 matches
   the file that was just downloaded, then launches it. The new instance therefore runs with the same
   privileges as the one being replaced, and never starts from a half-written file.

### Choosing a download

`ResolveDownloadEntry` picks the first `<Download>` whose `key` equals `DownloadIdentifier`
(`"portable"`). **If no entry matches, it silently falls back** to an entry synthesized from
`<DownloadLink>` / `<DownloadFileName>` — and that synthesized entry carries **no hash**, so the download
is not verified at all. Two things follow, worth remembering when writing a manifest by hand:

* keep the `key="portable"` entry present and correct; and
* an empty `<FileHash></FileHash>` (as in manifests published before SHA-256 was added) means "no hash to
  check" — verification is skipped, not failed.

The release workflow avoids both traps: it always emits a `portable` entry with a populated
`<FileHash algorithm="SHA256">` computed from the exact `SoundBoard.exe` attached to the release, and it
validates the generated file against `AppUpdate.xsd` before publishing.

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
