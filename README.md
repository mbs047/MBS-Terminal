# MBS Terminal

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

## What The Installer Changes

- Copies terminal icons to `%USERPROFILE%\.config\terminal-icons`.
- Copies Starship config to `%USERPROFILE%\.config\starship.toml`.
- Copies PowerShell helpers to `%USERPROFILE%\.config\powershell\laravel-dev.ps1`.
- Updates the current Windows PowerShell profile to load the helpers and Starship.
- Updates Windows Terminal `settings.json` and creates a timestamped backup first.

## Build The EXE

The launcher is intentionally small. It runs `install.ps1` through Windows PowerShell.

```powershell
.\build-installer.ps1
```

The build uses the built-in .NET Framework C# compiler on Windows.

## License

MIT License. See [LICENSE](LICENSE).
