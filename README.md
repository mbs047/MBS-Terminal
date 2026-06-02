# MBS Terminal

[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![Platform: Windows](https://img.shields.io/badge/platform-Windows-0078D4.svg)](#requirements)
[![Laravel Ready](https://img.shields.io/badge/Laravel-ready-FF2D20.svg)](#what-you-get)
[![Sponsor](https://img.shields.io/badge/sponsor-GitHub%20Sponsors-EA4AAA.svg)](https://github.com/sponsors/mbs047)

A polished Windows Terminal setup for Laravel and PHP development.

MBS Terminal gives you a modern dark terminal profile, pixel avatar icons, a colorful Starship prompt, Laravel shortcuts, smarter PowerShell autocomplete, a friendly welcome banner, and a cleaner `ls` experience.

![MBS Terminal preview](assets/screenshots/terminal-preview.png)

## Install

Download or clone this repo, then run:

```powershell
.\MBS-Terminal-Install.cmd
```

The terminal installer is the recommended path on fresh PCs. It checks administrator access, installs missing requirements in sequence, and then runs the main setup script.

If you prefer the GUI wizard, run:

```powershell
.\MBS-Terminal-Setup.exe
```

The installers can install or configure:

- Windows Terminal profile and icons.
- Windows Terminal, when missing from a fresh PC.
- Starship prompt.
- PHP `8.2`, `8.3`, `8.4`, or `8.5`.
- Existing PHP directory from Laragon, XAMPP, Herd, or a custom build.
- Composer.
- Laravel Installer.
- Valet for Windows.
- Laravel Pint, Envoy, and Vapor CLI.

After installation, open a new Windows Terminal tab.

## What You Get

- **MBS Midnight theme** with acrylic, crisp colors, and custom tab styling.
- **Pixel avatar icons** for the main developer shell and bundled terminal profiles.
- **Laravel-aware prompt** with Git, PHP, Node, package, duration, and time segments.
- **Developer shortcuts** like `pa migrate`, `pat`, `pclear`, `proutes`, `ptinker`, `ldev`, and `nr dev`.
- **Interactive navigation** with `ls -nav` or empty `cd`.
- **Colorized `ls` output** for directories, scripts, docs, configs, executables, images, logs, and hidden files.
- **Restore utility** to safely undo the setup and return to your previous terminal configuration.

## Requirements

- Windows 10 or Windows 11.
- Windows Terminal, or `winget` so the terminal installer can install it.
- Windows PowerShell 5.1 or later.
- Optional: Git, Node.js, Python, and extra Laravel tooling.

## Advanced Docs

See [ADVANCED.md](ADVANCED.md) for:

- Prompt icon and Git status meanings.
- Full color palette.
- Included icon assets.
- Full [custom command reference](CUSTOM_COMMANDS.md).
- `cd`, `ls`, and autocomplete behavior.
- Script install options.
- Restore details.
- Build instructions.

## Restore Defaults

Run:

```powershell
.\MBS-Terminal-Restore.exe
```

The restore tool creates backups before changing anything.

## Safety

The installer changes local Windows Terminal and PowerShell profile configuration. It creates timestamped backups before writing Windows Terminal settings.

Review `install.ps1` before running the setup if you are installing from a fork or untrusted download.

## Support

For setup help, see [SUPPORT.md](SUPPORT.md). For responsible disclosure, see [SECURITY.md](SECURITY.md). For contributing, see [CONTRIBUTING.md](CONTRIBUTING.md).

If MBS Terminal saves you setup time, you can support it through [GitHub Sponsors](https://github.com/sponsors/mbs047). Donations are optional and never required.

## License

MIT License. See [LICENSE](LICENSE).
