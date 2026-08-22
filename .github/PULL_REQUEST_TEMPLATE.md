## What does this change?

<!-- A sentence or two, and the issue it addresses. -->

Fixes #

## How was it tested?

<!-- Unit tests, and what you exercised manually. Playback changes deserve a note about which
     output device configuration you tried: single device, multiple devices, passthrough. -->

- [ ] `msbuild SoundBoard.sln /restore /p:Configuration=Release /m` succeeds
- [ ] `vstest.console.exe SoundBoard.Tests\bin\Release\SoundBoard.Tests.dll` passes
- [ ] Ran the built `SoundBoard.exe` and exercised the change

## Checklist

- [ ] The change is focused on one topic, with no unrelated reformatting
- [ ] Style matches the surrounding code (see [CONTRIBUTING.md](../CONTRIBUTING.md))
- [ ] New user-facing strings were added to `SoundBoard/Properties/Resources.resx`
- [ ] Tests were added or updated where the change is testable
- [ ] User-visible changes are listed under `## [Unreleased]` in `CHANGELOG.md`
- [ ] No hand-edited version numbers or `SoundBoard/VersionInfo.xml` (both are generated from the git tag)

## Screenshots

<!-- For UI changes. Delete this section otherwise. -->
