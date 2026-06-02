# Changelog

## Unreleased

## v1.2.0 - 2026-06-02

### Added

- Stable release installer asset: `MBS-Terminal-Install.exe`.
- Beta graphical setup wizard asset: `MBS-Terminal-Setup.exe`.
- Restore utility asset: `MBS-Terminal-Restore.exe`.
- Interactive terminal installer for fresh PCs, including selected install scope handling, `winget` checks, Windows Terminal installation, presets, dry-run support, and sequential handoff to `install.ps1`.
- Optional installer support for PHP 8.2, 8.3, 8.4, and 8.5.
- Optional Laravel/PHP tooling installs for Composer, Laravel Installer, Valet for Windows, Pint, Envoy, Vapor CLI, Pest PHP, Larastan, Rector, and Ray.
- Optional system and productivity tool installs for Git, Node.js LTS, nvm-windows, GitHub CLI, mkcert, Memurai (Redis-compatible), Docker Desktop, TablePlus, fzf, bat, ripgrep, and lazygit.
- Official PHP Windows zip fallback when `winget` is unavailable or cannot complete a portable PHP install.
- Composer `composer.phar` fallback path when Composer setup cannot complete.
- PowerShell profile PATH fallback file for machines where persistent Windows PATH writes are blocked.

### Changed

- Marked `MBS-Terminal-Install.exe` as the stable, recommended release installer.
- Marked `MBS-Terminal-Setup.exe` as beta and not stable.
- Kept optional extra tools defaulted to `No` unless explicitly selected.
- Allowed portable `winget` installs to run without forced elevation when needed.
- Improved terminal installer output with clearer status rows, final summaries, and quieter progress logging.
- Rebuilt release installer assets with the latest fallback and logging fixes.

### Fixed

- Windows Terminal settings can be created when Windows Terminal exists but `settings.json` has not been generated yet.
- PHP install no longer fails the whole setup when `winget` returns access denied for portable packages.
- Composer setup failures recover through the bundled `composer.phar` fallback path.
- Composer fallback global package installs now run through the verified `php.exe composer.phar` runner.
- PATH updates fall back from machine PATH to user PATH, then to the MBS Terminal profile PATH fallback when Windows blocks persistent writes.
- MBS Terminal profile launch uses a process execution-policy bypass for the installed PowerShell command line.
- Installer logs avoid noisy Composer progress lines and false warning output.

## v1.1.0 - 2026-06-02

### Added

- Interactive terminal installer for fresh PCs, including administrator relaunch, `winget` checks, Windows Terminal installation, presets, dry-run support, and sequential handoff to `install.ps1`.
- Double-clickable terminal installer through `MBS-Terminal-Install.exe`.
- Richer terminal installer output with status badges, helper messages, clear sections, and final success, warning, canceled, or failed states.
- Optional installer support for Git, Node.js LTS, nvm-windows, GitHub CLI, Pest PHP, Larastan, Rector, Ray, mkcert, Memurai (Redis), Docker Desktop, TablePlus, fzf, bat, ripgrep, and lazygit.

### Changed

- Optional extra tools now default to `No` unless explicitly selected.

### Fixed

- Windows Terminal settings can now be created when Windows Terminal is freshly installed and has not generated `settings.json` yet.
- Installer prompt configuration uses icon markers again and writes Starship config as UTF-8.

### Removed

- Legacy GUI setup executable and command-file installer fallback so releases have a single primary installer path.

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
