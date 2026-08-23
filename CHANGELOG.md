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
- A `--config <path>` command-line switch, naming the configuration file to use instead of the one
  under `%AppData%`. Everything follows it — the configuration itself, its `.bak` backups and the
  `.temp` file — and the pre-1.3 legacy migration is skipped, so a board started this way cannot
  disturb your real one. A missing or unusable path stops the app with an error rather than quietly
  falling back to the default configuration, which would be the very thing the switch is for
  avoiding.

## [1.13.0] - 2026-08-23

### Added
- A **progress bar** along the bottom edge of every sound. It is there whenever the button has a
  sound, so every card has the same shape: empty while the sound is stopped, filling as it plays,
  holding while paused, and empty again when it stops. A looping sound's bar runs to the end and
  starts over. It is drawn in black or white, whichever reads on the sound's colour, like the
  controls above it.

### Changed
- The controls on a sound are now laid out in groups — **play/pause, stop, loop**, then
  **mute, solo**, then **remove** — with a little space between them, so the one control that
  cannot be undone with another click no longer sits flush against the toggles beside it. The
  volume slider now spans the full width of the row beneath it, and the strip sits slightly
  higher, in line with the **···** button, clear of the progress bar.

### Fixed
- The progress bar that appeared while a sound played sometimes did not: the loop that moved it
  along could mistake "nothing read from the file yet" on its first tick for "the sound has
  stopped", and give up before drawing anything. Whether it did depended on timing, so some
  sounds showed progress and others never did.

## [1.12.0] - 2026-08-23

### Added
- A **volume slider** on every sound, along the top of its row of controls, setting how loud
  that one sound plays. It takes effect straight away on a sound that is already playing, and
  it is saved with the sound. The bottom of the slider is the same state as **mute**: dragging
  all the way down mutes, the mute control moves the slider to the bottom, and unmuting either
  way comes back to the level the sound was at. Like the other controls it applies to the whole
  selection when several sounds are selected.

### Changed
- **Adjust volume** in the **···** menu is now **Adjust volume offset**, to tell it apart from
  the new slider. It is otherwise unchanged: the offset is the sound's own trim, and the slider
  scales whatever the offset puts it at.

### Fixed
- In the Dark theme, the controls on a sound with no colour of its own were drawn in black on a
  near-black button and were all but invisible. A sound with no colour is painted in the theme's
  own button colour, so the theme now decides whether its controls are black or white — and it
  does so as soon as the theme is switched, with no restart.
- A negative volume offset inverted the waveform as well as turning the sound down, because the
  offset's own sign was left in the reciprocal that produced the gain. Every sound is exactly as
  loud as it was before; only sounds played against a non-inverted copy of themselves were ever
  affected, and those no longer cancel out.

## [1.11.0] - 2026-08-23

### Added
- File logging to `%AppData%\SoundBoard\soundboard.log` and an "Open log folder" menu item.
- Reproducible build documentation (`BUILDING.md`), a CI build on every push, and unit
  tests for the model, serializer, utilities and audio device handling.
- Tag-driven releases: the version is stamped from the `v*` git tag, `VersionInfo.xml`
  is generated (with a SHA-256 `<FileHash>` so the updater verifies downloads), and the
  GitHub Release is created automatically.
- A `LICENSE` file (MIT, which the README already claimed), `CONTRIBUTING.md`, GitHub issue
  and pull request templates, and `docs/update-manifest.md` describing the `VersionInfo.xml`
  update-manifest format and how the in-app updater consumes it.
- Support for more than one interface language, and a **Language** submenu under **•••** to
  choose one. The choice is saved as `Language` in `soundboard.config` alongside the other
  global settings; the default, and the "Same as Windows" entry, follow the operating
  system. Changing it applies on the next start, which the app offers to do for you.
- A Spanish translation. Translations are compiled into satellite assemblies that are
  embedded in the single-file `SoundBoard.exe` along with everything else, so nothing extra
  ships next to it. See [Localization](BUILDING.md#localization) for how to add a language,
  and `SoundBoard.Tests/LocalizationTests.cs` for the checks that keep translations honest
  (no missing strings, no stale ones, and the same `{0}` placeholders as the English).
- A **Theme** submenu under **•••** offering a Light and a Dark theme. Unlike the language it
  applies immediately with no restart, and the choice is saved as `Theme` in `soundboard.config`
  so it is still there the next time you start.
- A row of controls along the bottom of every sound: **play/pause**, **stop**, **loop**,
  **mute**, **solo** and **remove**, each with a tool tip. They are always there rather than
  appearing only while a sound plays, so a sound can be started, looped or cleared without
  going through the **···** menu. Remove is undoable through the usual snackbar, and all six
  apply to the whole selection when several sounds are selected, exactly as the menu does.
- **Mute** silences one sound without stopping it — it keeps its place, keeps its progress bar
  and still chains to its next sound — and takes effect immediately on a sound that is already
  playing. It is saved with the sound, so a muted sound comes back muted.
- **Solo** silences everything that is not soloed, for auditioning one sound against a busy
  board. Any number of sounds can be soloed at once. Unlike mute it is not saved, so the board
  never starts up silent for a reason you cannot see.

### Changed
- Target framework unified at .NET Framework 4.8 across all projects.
- The last of the user-facing text that was still hard-coded — the title bar buttons, the
  **ADMIN** badge, the search flyout, the snackbar's **UNDO**, the "Set Hotkeys" and
  "Change Button Grid" dialogs, the "All files" entry in the sound browser, and the reasons
  the updater gives for rejecting a download — now comes from the resource file like
  everything else.
- The loop indicator in a sound's bottom-right corner is gone: looping is now shown and toggled
  by the loop control on the new row of controls. The volume-offset indicator moves down into
  the space it leaves.
- The "hotkey already in use" warnings are now whole sentences joined at display time,
  instead of fragments concatenated in a fixed order with trailing spaces baked into the
  resource strings.
- Build: NuGet dependencies moved from `packages.config` to `PackageReference`
  (`msbuild /restore`; no `nuget.exe` or `packages\` folder needed), Fody 5.1.1 → 6.9.3 and
  Costura.Fody 4.0.0 → 6.2.0.
- Dependencies: System.Reactive 5.0.0 → 6.1.0, MouseKeyHook 5.6.0 → 5.7.1, NAudio 1.9.0 → 2.2.1,
  MahApps.Metro 1.6.5 → 2.4.11 (with ControlzEx 5.0.2 and MahApps.Metro.SimpleChildWindow 2.2.1).
  The UI looks the same apart from slightly wider Windows-10-style title bar buttons.
- The two prebuilt third-party DLLs (`Dsafa.WpfColorPicker.dll`, `HotKeyManagement.WPF.4.dll`)
  are now built from vendored source projects (MIT; licences and a NOTICE of local changes are
  included alongside the source).
- README screenshots are served from `docs/images/` in this repository instead of third-party
  image hosts, and the README documents how to build and run from source.

### Removed
- Extended.Wpf.Toolkit (Xceed) dependency: the row/column spinners in the "Change button grid"
  dialog are now MahApps `NumericUpDown` controls (newer Xceed versions are under a
  non-commercial license, and 3.x is unmaintained).
- Internal refactor: the configuration model lives in `SoundBoard.Model` and is the live
  source of truth for the UI; undo works from in-memory snapshots; the playback engine
  was extracted from `SoundButton`; hotkeys and search operate on the model; the
  `MainWindow.Instance` singleton was retired.

### Security
- The in-app updater now refuses any download that does not match a SHA-256
  `<FileHash>` in the update manifest (a missing or empty hash is a failure, not a pass).
- The update is applied in-process by renaming the running exe, instead of through an
  elevated `cmd.exe` / `powershell.exe` command line built from file paths. UAC is only
  requested when the exe's folder is not writable, and the elevated mode
  (`SoundBoard.exe --apply-update <file> <sha256>`) can only replace its own image with a
  file matching the given hash.

### Fixed
- Updating no longer kills the app with `taskkill /f`, so unsaved settings are written
  before the new version starts.
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

[Unreleased]: https://github.com/micahmo/SoundBoard/compare/v1.11.0...HEAD
[1.11.0]: https://github.com/micahmo/SoundBoard/compare/v1.10.2...v1.11.0
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
