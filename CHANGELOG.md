# Changelog

## Unreleased

### Added

- Interactive terminal installer for fresh PCs, including administrator relaunch, `winget` checks, Windows Terminal installation, presets, dry-run support, and sequential handoff to `install.ps1`.
- Double-clickable terminal installer through `MBS-Terminal-Install.exe`.
- Command launcher fallback for the terminal installer through `MBS-Terminal-Install.cmd`.
- Richer terminal installer output with status badges, helper messages, clear sections, and final success, warning, canceled, or failed states.

### Fixed

- Windows Terminal settings can now be created when Windows Terminal is freshly installed and has not generated `settings.json` yet.

## v1.0.0 - 2026-05-31

First public release of MBS Terminal.

### Added

- Modern visual setup wizard for installing and configuring MBS Terminal.
- Standalone setup executable that embeds the installer script, terminal configs, and icon assets for release downloads.
- Restore utility for safely returning Windows Terminal and PowerShell profile settings to defaults.
- MBS Midnight Windows Terminal theme with acrylic, custom tab styling, and profile icons.
- Pixel avatar icon assets for the main developer shell and bundled terminal profiles.
- Starship prompt with MBS, username, directory, Git, Laravel, PHP, Node, Python, package, duration, and time segments.
- UTF-8-safe Starship and PowerShell profile installation.
- Laravel-focused PowerShell helpers, aliases, completions, and welcome banner.
- Interactive folder navigator through `ls -nav` and empty `cd`.
- Generic `devhome` command for jumping to the configured starting folder without personal project naming.
- Colorized `ls` table output for directories, executables, scripts, docs, configs, images, archives, hidden items, locks, and logs.
- Optional installer support for PHP 8.2, 8.3, 8.4, 8.5, Composer, Laravel Installer, Valet for Windows, Pint, Envoy, Vapor CLI, Starship, and tool updates.
- Valet for Windows Composer isolation and known post-success Symfony exit handling.

### Fixed

- Prevented terminal prompt icon mojibake by reading and writing Starship config as UTF-8.
- Removed the personal `MBS-Portfolio` default path so public installs default to the user's home folder unless a starting directory is selected.
- Cleaned installer running-status messages so raw progress glyphs and stack traces do not become the primary status text.
- Improved README with updated screenshot, prompt icon guide, color guide, install options, and command documentation.
