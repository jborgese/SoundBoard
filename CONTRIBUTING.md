# Contributing to SoundBoard

Thanks for taking the time to help out. Bug reports, feature requests and pull requests are all welcome.

## Reporting a bug

Open a [bug report](https://github.com/micahmo/SoundBoard/issues/new?template=bug_report.yml) and include:

* the SoundBoard version (**about** in the title bar shows it) and your Windows version;
* what you did, what you expected, and what happened instead;
* the log from `%AppData%\SoundBoard\soundboard.log` (the **···** menu has an *Open log folder* item) —
  scrub it first if your sound file paths are sensitive;
* anything device-specific: which audio output(s) were selected, whether passthrough was on, and the
  hardware involved. Audio bugs are usually device-dependent, and that is the part we cannot guess.

Please check the [existing issues](https://github.com/micahmo/SoundBoard/issues) first — a "me too" with
your own log on an open issue is more useful than a duplicate.

## Suggesting a feature

Open a [feature request](https://github.com/micahmo/SoundBoard/issues/new?template=feature_request.yml).
Describe the problem you are trying to solve rather than only the solution you have in mind; it often
turns out there is a simpler way to get there.

## Setting up

See [BUILDING.md](BUILDING.md). The short version:

```powershell
git clone https://github.com/micahmo/SoundBoard.git
cd SoundBoard
msbuild SoundBoard.sln /restore /p:Configuration=Release /m
vstest.console.exe SoundBoard.Tests\bin\Release\SoundBoard.Tests.dll
```

You need Visual Studio 2019+ (or Build Tools) with the **.NET desktop development** workload and the
**.NET Framework 4.8** SDK and targeting pack. `dotnet build` cannot build this solution;
[BUILDING.md explains why](BUILDING.md#why-dotnet-build-fails).

## Pull requests

1. **Open an issue first** for anything larger than a bug fix, so the approach can be agreed before you
   write the code.
2. Branch off `master`, and keep the change focused — one topic per pull request. Unrelated
   reformatting makes a change much harder to review.
3. **Match the surrounding style.** The codebase uses Allman braces, 4-space indent, `_camelCase`
   private fields, XML doc comments on public members, and `#region` blocks grouping members by kind.
   Follow what the file you are editing already does.
4. **Keep the model UI-free.** `SoundBoard.Model` is the UI-independent data model
   (`SoundBoardConfig` → `Page` → `Sound`) and deliberately references no WPF presentation assemblies.
   Logic that can live there should, because that is the part that is straightforward to unit test.
5. **Add tests** for anything testable, in `SoundBoard.Tests` (xUnit). The test project sees the app's
   internals via `InternalsVisibleTo`, so internal helpers can be tested directly.
6. **User-visible changes go in [CHANGELOG.md](CHANGELOG.md)** under `## [Unreleased]`. That section
   becomes the GitHub Release body and the in-app update notes, so write it for users, not for reviewers.
7. **Never edit `SoundBoard/VersionInfo.xml` or version numbers by hand.** Both are generated — the git
   tag is the single source of truth. See [Versioning and releases](BUILDING.md#versioning-and-releases)
   and [docs/update-manifest.md](docs/update-manifest.md).
8. Make sure `Release` builds and the tests pass before pushing; CI
   ([.github/workflows/build.yml](.github/workflows/build.yml)) runs both on every push and pull request.

Adding a NuGet dependency? Mention why in the pull request. Everything is embedded into a single exe by
Costura, so each dependency is paid for in download size, and license terms matter — the project is MIT
and stays that way.

## Localization

User-facing strings live in `SoundBoard/Properties/Resources.resx`. Never write a literal: use
`Properties.Resources.<Name>` from C#, and `{soundBoard:Loc <Name>}` from XAML.

`Resources.resx` is the neutral (English) source of truth. Each translation is a sibling
`Resources.<tag>.resx` — today only `Resources.es.resx` — compiled into a satellite assembly and
embedded in the single-file exe.

**Touching a string means touching every translation**, and the tests enforce it, so a change that
compiles can still fail the suite. `SoundBoard.Tests/LocalizationTests.cs` fails when a translation is
missing a string the neutral file defines, still has one the neutral file has dropped, or uses a
different set of `{0}` placeholders. So:

* adding a string → add it to `Resources.es.resx` too;
* removing one → remove it there too;
* changing the placeholders in a format string → keep them identical in both.

If you cannot translate it well, say so in the pull request and put the English text in the translation
rather than guessing — the Spanish that ships today is a worked example rather than vetted Spanish, and
is documented as such.

Adding a whole new language is a three-step checklist — the `.resx`, an entry in `SoundBoard.csproj`, and
the tag in `TranslatedLanguageTags` — and you need all three. Miss the last one and nothing complains:
the build is green, the tests pass, and the language simply never appears in the menu, because the tests
only check languages that are registered. The steps are in
[Localization](BUILDING.md#localization) in BUILDING.md.

## License

By contributing, you agree that your contributions are licensed under the [MIT License](LICENSE) that
covers this project.
