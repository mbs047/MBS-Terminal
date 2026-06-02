# MBS Terminal Advanced Guide

This guide documents the prompt, icons, colors, install options, restore behavior, and build flow behind MBS Terminal.

For the complete command list, see [CUSTOM_COMMANDS.md](CUSTOM_COMMANDS.md).

## Prompt Icons

| Icon | Segment | Color | Meaning |
| --- | --- | --- | --- |
| `⚡` | `mbs` | `#C099FF` | MBS prompt brand mark |
| `◆` | username | `#D6DEEB` | Display name from the installer or Windows user |
| `📁` | directory | `#7DCFFF` | Current working directory |
| `⑂` | Git branch | `#E0AF68` | Current branch name |
| `▲` | Laravel | `#F7768E` | Appears when an `artisan` file is detected |
| `🐘` | PHP | `#7AA2F7` | PHP context |
| `⬢` | Node | `#9ECE6A` | Node.js context |
| `◇` | package | `#C099FF` | Package version context |
| `🐍` | Python | `#9ECE6A` | Python context |
| `⏱` | duration | `#53627A` | Long-running command duration |
| `🕒` | time | `#53627A` | Right-aligned clock |
| `›` / `×` | prompt | green / red | Success or error prompt marker |

## Git Status Marks

| Mark | Meaning |
| --- | --- |
| `⇡` | Ahead of remote |
| `⇣` | Behind remote |
| `⇕` | Diverged from remote |
| `×` | Conflict |
| `−` | Deleted file |
| `●` | Modified file |
| `»` | Renamed file |
| `✓` | Staged file |
| `≡` | Stashed changes |
| `?` | Untracked file |

## MBS Midnight Palette

| Role | Hex |
| --- | --- |
| Background | `#0B1020` |
| Foreground | `#D6DEEB` |
| Cyan | `#7DCFFF` |
| Purple | `#C099FF` |
| Red | `#F7768E` |
| Yellow | `#E0AF68` |
| Green | `#9ECE6A` |
| Blue | `#7AA2F7` |
| Muted | `#53627A` |

## Included Profile Icons

| Asset | Used For |
| --- | --- |
| `assets/terminal-icons/mbs-pixel-avatar.png` | Main **MBS Dev Shell** tab icon |
| `assets/terminal-icons/mbs-dev-shell.png` | PowerShell developer shell profile |
| `assets/terminal-icons/mbs-cmd.png` | Command Prompt profile |
| `assets/terminal-icons/mbs-cloud.png` | Azure Cloud Shell profile |
| `assets/terminal-icons/mbs-terminal.ico` | Setup and restore executable icon |

## Custom Commands

The full Laravel, Composer, NPM, navigation, `ls`, autocomplete, and system helper command list lives in [CUSTOM_COMMANDS.md](CUSTOM_COMMANDS.md).

## `ls` Color Guide

| Type | Color | Examples |
| --- | --- | --- |
| Directories | Cyan | `assets`, `configs`, `src` |
| Hidden directories | Dark cyan | `.git` |
| Executables | Green | `.exe`, `.cmd`, `.bat`, `.msi` |
| PowerShell scripts | Yellow | `.ps1`, `.psm1`, `.psd1` |
| JavaScript / TypeScript | Yellow | `.js`, `.jsx`, `.ts`, `.tsx`, `.mjs`, `.cjs` |
| PHP / Blade | Magenta | `.php`, `.phtml`, `.blade.php` |
| Config files | Green | `.json`, `.jsonc`, `.toml`, `.yml`, `.yaml`, `.xml`, `.config` |
| Stylesheets | Blue | `.css`, `.scss`, `.sass`, `.less` |
| Documents | White | `.md`, `.markdown`, `.txt`, `.rst` |
| Images and icons | Purple | `.png`, `.jpg`, `.jpeg`, `.gif`, `.svg`, `.ico`, `.webp` |
| Archives and env files | Dark yellow | `.zip`, `.7z`, `.rar`, `.tar`, `.gz`, `.env` |
| Lock and log files | Dark gray | `.lock`, `.log` |

## Autocomplete

| Key | Behavior |
| --- | --- |
| `Tab` | Accepts the gray suggestion |
| `RightArrow` | Accepts the full suggestion |
| `Alt+RightArrow` | Accepts the next suggestion word |
| `Ctrl+Space` | Opens the completion menu |

## Terminal Install

For a fresh PC or when the GUI executable does not open, run the interactive terminal installer:

```powershell
.\MBS-Terminal-Install.exe
```

The fallback command launcher is also available:

```powershell
.\MBS-Terminal-Install.cmd
```

You can call the PowerShell script directly from a terminal, but do not double-click it:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\install-terminal.ps1
```

Useful presets:

```powershell
.\MBS-Terminal-Install.exe -Preset Minimal
.\MBS-Terminal-Install.exe -Preset Recommended -Yes
.\MBS-Terminal-Install.exe -Preset Full
.\MBS-Terminal-Install.exe -Preset Recommended -DryRun -NoAdminRelaunch
```

The terminal installer checks administrator access, verifies `winget`, installs Windows Terminal when missing, then runs `install.ps1` with the selected Starship, PHP, Composer, and Laravel tooling options. Double-click runs keep the final status visible.

## Script Install

The GUI wraps `install.ps1`. You can also run the script directly:

```powershell
.\install.ps1 -InstallDependencies -InstallPhp -PhpVersion 8.4 -InstallComposer -InstallLaravel
```

Useful options:

```powershell
.\install.ps1 -StartingDirectory "C:\Code\MyLaravelApp"
.\install.ps1 -DisplayName "Developer"
.\install.ps1 -InstallScope CurrentUser
.\install.ps1 -InstallValet -InstallPint -InstallEnvoy -InstallVapor
.\install.ps1 -PhpDirectory "C:\laragon\bin\php\php-8.4"
```

## What The Installer Changes

- Copies terminal icons to `%USERPROFILE%\.config\terminal-icons`.
- Copies Starship config to `%USERPROFILE%\.config\starship.toml`.
- Copies PowerShell helpers to `%USERPROFILE%\.config\powershell\laravel-dev.ps1`.
- Updates the current Windows PowerShell profile to load helpers and Starship.
- Updates Windows Terminal `settings.json`, creating it when Windows Terminal is freshly installed and no settings file exists yet.
- Creates a timestamped Windows Terminal settings backup before overwriting an existing file.
- Optionally installs supported development tools through `winget` and Composer.

## Restore Defaults

Run the restore utility:

```powershell
.\MBS-Terminal-Restore.exe
```

Or run the script directly:

```powershell
.\restore-default.ps1
```

The restore script creates timestamped backups before changing anything. It resets Windows Terminal settings, removes MBS PowerShell startup hooks, and moves the MBS Starship, helper, and icon files aside.

## Build The EXEs

```powershell
.\build-installer.ps1
```

The build uses the built-in .NET Framework C# compiler on Windows and produces `MBS-Terminal-Install.exe`, `MBS-Terminal-Setup.exe`, and `MBS-Terminal-Restore.exe`.
