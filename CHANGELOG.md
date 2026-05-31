# Changelog

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
- Colorized `ls` table output for directories, executables, scripts, docs, configs, images, archives, hidden items, locks, and logs.
- Optional installer support for PHP 8.2, 8.3, 8.4, 8.5, Composer, Laravel Installer, Valet for Windows, Pint, Envoy, Vapor CLI, Starship, and tool updates.
- Valet for Windows Composer isolation and known post-success Symfony exit handling.

### Fixed

- Prevented terminal prompt icon mojibake by reading and writing Starship config as UTF-8.
- Cleaned installer running-status messages so raw progress glyphs and stack traces do not become the primary status text.
- Improved README with updated screenshot, prompt icon guide, color guide, install options, and command documentation.
