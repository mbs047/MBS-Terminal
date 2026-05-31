# MBS Terminal

[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![Platform: Windows](https://img.shields.io/badge/platform-Windows-0078D4.svg)](#requirements)
[![Sponsor](https://img.shields.io/badge/sponsor-GitHub%20Sponsors-EA4AAA.svg)](https://github.com/sponsors/mbs047)

MBS Terminal is my Windows Terminal setup for Laravel development. It packages the same theme, prompt, icons, welcome message, aliases, autocomplete behavior, and custom `ls` helpers that I use locally.

![MBS Terminal preview](assets/screenshots/terminal-preview.png)

## Features

- Windows Terminal theme with acrylic transparency and the MBS Midnight color scheme.
- Pixel avatar tab icon and custom profile icons.
- Starship prompt configured for Laravel, Git, PHP, Node, Python, package version, duration, and time.
- PowerShell welcome banner with a random encouraging quote on every new terminal session.
- Laravel-focused shortcuts:
  - `pa migrate` runs `php artisan migrate`.
  - `pat` runs `php artisan test --compact`.
  - `pclear` runs `php artisan optimize:clear`.
  - `proutes` runs `php artisan route:list --except-vendor`.
  - `ptinker` runs `php artisan tinker`.
  - `ldev` starts the best available dev command.
  - `nr dev` runs `npm run dev`.
- Improved autocomplete behavior:
  - `Tab` accepts the gray suggestion.
  - `RightArrow` accepts the full suggestion.
  - `Alt+RightArrow` accepts the next suggestion word.
  - `Ctrl+Space` opens the completion menu.
- Custom `ls` table view:
  - `ls` hides dot-prefixed entries.
  - `ls -la` or `ls -all` shows hidden files.
  - `ls -nav` opens an arrow-key folder navigator.
  - `ls -file`, `ls -dir`, and `ls -recursive` filter the listing.

## Requirements

- Windows 10 or Windows 11.
- Windows Terminal.
- Windows PowerShell 5.1 or later.
- Starship for the prompt. The installer can try to install it through `winget` when you pass `-InstallDependencies`.
- Optional: Git, PHP, Node.js, Python, and Laravel tooling for the development shortcuts.

## Install

Clone or download this repository, then run:

```powershell
.\MBS-Terminal-Setup.exe
```

To also try installing missing dependencies such as Starship through `winget`, run:

```powershell
.\install.ps1 -InstallDependencies
```

You can choose a default terminal starting directory:

```powershell
.\install.ps1 -StartingDirectory "W:\GitHub\MBS-Portfolio"
```

After installation, open a new Windows Terminal tab.

## Restore Defaults

If you want to undo the setup and go back to a plain Windows Terminal configuration, run:

```powershell
.\MBS-Terminal-Restore.exe
```

Or run the PowerShell restore script directly:

```powershell
.\restore-default.ps1
```

The restore script creates timestamped backups before changing anything. It resets Windows Terminal settings, removes the MBS PowerShell startup hooks, and moves the MBS Starship/helper/icon files aside.

## Safety Notes

The installer changes local terminal configuration. It creates a timestamped backup of Windows Terminal `settings.json` before writing the MBS Terminal settings.

Review `install.ps1` before running it if you are installing from a fork or from an untrusted download. You can also build the launcher locally with `.\build-installer.ps1`.

## What The Installer Changes

- Copies terminal icons to `%USERPROFILE%\.config\terminal-icons`.
- Copies Starship config to `%USERPROFILE%\.config\starship.toml`.
- Copies PowerShell helpers to `%USERPROFILE%\.config\powershell\laravel-dev.ps1`.
- Updates the current Windows PowerShell profile to load the helpers and Starship.
- Updates Windows Terminal `settings.json` and creates a timestamped backup first.

## Build The EXEs

The launchers are intentionally small. They run `install.ps1` and `restore-default.ps1` through Windows PowerShell.

```powershell
.\build-installer.ps1
```

The build uses the built-in .NET Framework C# compiler on Windows.

## Contributing

Issues and pull requests are welcome. Please use the GitHub issue templates, keep changes focused, and see [CONTRIBUTING.md](CONTRIBUTING.md) before opening a pull request.

For responsible disclosure, see [SECURITY.md](SECURITY.md). For setup help, see [SUPPORT.md](SUPPORT.md).

## Support This Project

If MBS Terminal saves you setup time, you can support it through [GitHub Sponsors](https://github.com/sponsors/mbs047). Donations are optional and never required to use or contribute to the project.

## License

MIT License. See [LICENSE](LICENSE).
