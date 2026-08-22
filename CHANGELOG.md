# Changelog

All notable changes to SoundBoard are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses
[Semantic Versioning](https://semver.org/) tags (`vX.Y.Z`).

The `## [X.Y.Z]` section for a tag is what the release workflow publishes as the
GitHub Release body and as `<VersionNotes>` in the in-app update manifest, so add an
entry under **Unreleased** with each user-visible change and rename that heading to
the version when tagging. Entries before 1.10.3 were reconstructed from the git
history and release tags.

## [Unreleased]

### Added
- File logging to `%AppData%\SoundBoard\soundboard.log` and an "Open log folder" menu item.
- Reproducible build documentation (`BUILDING.md`), a CI build on every push, and unit
  tests for the model, serializer, utilities and audio device handling.
- Tag-driven releases: the version is stamped from the `v*` git tag, `VersionInfo.xml`
  is generated (with a SHA-256 `<FileHash>` so the updater verifies downloads), and the
  GitHub Release is created automatically.

### Changed
- Target framework unified at .NET Framework 4.8 across all projects.
- Build: NuGet dependencies moved from `packages.config` to `PackageReference`
  (`msbuild /restore`; no `nuget.exe` or `packages\` folder needed), Fody 5.1.1 → 6.9.3 and
  Costura.Fody 4.0.0 → 6.2.0.
- Internal refactor: the configuration model lives in `SoundBoard.Model` and is the live
  source of truth for the UI; undo works from in-memory snapshots; the playback engine
  was extracted from `SoundButton`; hotkeys and search operate on the model; the
  `MainWindow.Instance` singleton was retired.

### Fixed
- Crash on first launch when no configuration file exists.
- Progress-bar update loop that never terminated after a sound stopped.
- Tabs, tab items and menu items leaking through static dictionaries.
- The config file that actually failed to load is now the one that gets backed up.
- An unparseable audio device ID is no longer treated as the default device.
- `Truncate` running off the end of short strings.

## [1.10.2] - 2023-04-17

### Changed
- Sound outputs and passthrough outputs are configured separately (#18).

## [1.10.1] - 2023-04-14

### Fixed
- Error handling for audio passthrough.
- Color picker sizing; the picker now starts from the existing button colors.
- Updated AppHelpers (Bluegrams/AppHelpers#7).

## [1.10.0] - 2023-03-10

### Added
- Audio passthrough (route an input device to the outputs) (#18).
- Global hotkeys for sounds, with error handling and focusing the sound's tab when triggered.
- Multi-sound / folder selection when adding sounds.
- Multi-select operations and highlighted search results, with a scrollbar for results.
- Sounds can stop other sounds, and can chain to a "next" sound; indicators show stop and next.
- Multiple sounds can play at once; Ctrl-A selects all.
- Indicator on a tab when a sound on it is playing (not shown while paused).
- Default button grid for new pages.
- Warning when dragging and dropping sounds.

### Changed
- Better text resizing on buttons, including while playing and when restarting a sound.
- Improved startup error handling.

### Fixed
- "Next sound" issues.

## [1.9] - 2022-01-08

### Added
- Updates can be skipped.
- Global exception handling.
- Warning when a page has a very large number of buttons.
- Better file-type handling: any file can be dragged to a button; files that do not
  appear to contain audio (or do not exist) produce a warning up front; the file browser
  defaults to known audio/video types and offers "all file types".
- Broken sound links can be fixed from the button menu.

### Fixed
- Long sound names are wrapped/handled in the source menu and on buttons.
- Inability to drag after an in-app update.
- Sounds taking a long time to start because of duplicate audio devices.
- Caught exceptions that were slowing tab loading.

## [1.8] - 2021-12-26

### Added
- Audio can be routed to multiple output devices at once.

### Fixed
- Output-device menu closing unexpectedly.
- Small icon rendering issue.

## [1.7.1] - 2021-01-18

- Re-tag of 1.7 (both tags point at the same commit); no code changes.

## [1.7] - 2021-01-18

### Added
- In-app auto update.
- Selection of the audio output device (#11).
- Versioned config backups on save, with any backup restorable; old backups are cleaned up.
- Warning at startup if the required .NET Framework is not installed.

### Changed
- Target .NET Framework 4.8.
- Buttons keep normal casing.
- Faster overlay animations.

## [1.6] - 2019-08-23

### Added
- Drag and drop sounds onto other tabs (#4) and rearrange tabs by drag and drop (#5).
- Per-button colors (#8).
- Per-sound volume boost/cut and looping, with indicator icons on the button.
- Configurable number of sound buttons per page (with undo).
- Last selected tab is remembered and restored on load.
- Separators in the context menu.

### Changed
- Target .NET Framework 4.7.2.

### Fixed
- Drag and drop could be started with the right mouse button.
- Buttons with a user-chosen white or black background had no hover color.
- Progress update task kept running after the sound stopped.

## [1.5] - 2019-06-22

### Added
- Auto-save of the configuration every 2 minutes; configuration stored in `%AppData%`
  with legacy config files imported (and backed up) on upgrade.
- Import/export of configurations and clearing the current configuration, all undoable.
- Undo mechanism used for clearing sounds, removing pages, and clearing all sounds on a page (#6).
- Pause/resume or stop any playing sound.
- Clicking an empty button prompts for a sound; "Choose sound" and "Clear sound" in the button menu.
- Drag one sound onto another to swap them.
- "Go to sound" and "Open source file" from the search result context menu.
- Tooltip on the global silence button (#7).
- Strings moved to a resource file for localization.

### Changed
- Rename prompts are pre-populated with the existing name (#2).
- Play/pause/stop buttons use graphic assets instead of OS-dependent characters.
- Tab removal prompt uses Yes/No.
- System audio is unmuted via NAudio instead of an interop hack.

### Fixed
- Help tabs lacked a context menu and offered "Rename".
- Right-click on the menu button bubbled up to the tab (#9).
- Playing a sound from search now plays on the source button (#10).
- Clearing a playing sound, or removing a page with playing sounds, left audio playing.
- Tabs could accumulate multiple context menus.
- Importing over an empty configuration caused an error.
- Snackbar no longer closes when misclicked.

## [1.4] - 2016-09-25

### Added
- Context menus on tab items.
- Escape clears the search query.

### Changed
- UI tweaks and wording.

## [1.3] - 2016-08-15

### Changed
- Assorted improvements (see git history).

## [1.2] - 2016-08-13

### Added
- System audio is unmuted when a sound plays.

## [1.1] - 2016-08-13

### Changed
- Multiple improvements for 1.1 (see git history).

## [1.0] - 2016-08-12

### Added
- Initial release: tabbed pages of sound buttons, search, and a simple configuration file.

### Fixed
- Removing a page.

[Unreleased]: https://github.com/micahmo/SoundBoard/compare/v1.10.2...HEAD
[1.10.2]: https://github.com/micahmo/SoundBoard/compare/v1.10.1...v1.10.2
[1.10.1]: https://github.com/micahmo/SoundBoard/compare/v1.10.0...v1.10.1
[1.10.0]: https://github.com/micahmo/SoundBoard/compare/v1.9...v1.10.0
[1.9]: https://github.com/micahmo/SoundBoard/compare/v1.8...v1.9
[1.8]: https://github.com/micahmo/SoundBoard/compare/v1.7.1...v1.8
[1.7.1]: https://github.com/micahmo/SoundBoard/compare/v1.7...v1.7.1
[1.7]: https://github.com/micahmo/SoundBoard/compare/v1.6...v1.7
[1.6]: https://github.com/micahmo/SoundBoard/compare/v1.5...v1.6
[1.5]: https://github.com/micahmo/SoundBoard/compare/v1.4...v1.5
[1.4]: https://github.com/micahmo/SoundBoard/compare/v1.3...v1.4
[1.3]: https://github.com/micahmo/SoundBoard/compare/v1.2...v1.3
[1.2]: https://github.com/micahmo/SoundBoard/compare/v1.1...v1.2
[1.1]: https://github.com/micahmo/SoundBoard/compare/v1.0...v1.1
[1.0]: https://github.com/micahmo/SoundBoard/releases/tag/v1.0
