# MBS Terminal Custom Commands

This file lists every custom command installed by the MBS Terminal PowerShell helper.

The commands are loaded from:

```powershell
%USERPROFILE%\.config\powershell\laravel-dev.ps1
```

## Laravel Commands

These commands automatically look for the nearest parent folder that contains an `artisan` file, then run from that Laravel project root.

| Command | Example | What It Does |
| --- | --- | --- |
| `pa` | `pa migrate` | Runs `php artisan <args>` |
| `art` | `art make:model Post -m` | Alias for `pa`; runs `php artisan <args>` |
| `pat` | `pat --filter=UserTest` | Runs `php artisan test --compact <args>` |
| `pintd` | `pintd` | Runs Laravel Pint on dirty files with agent-friendly output |
| `pclear` | `pclear` | Runs `php artisan optimize:clear` |
| `proutes` | `proutes --path=api` | Runs `php artisan route:list --except-vendor <args>` |
| `ptinker` | `ptinker` | Runs `php artisan tinker <args>` |
| `ldev` | `ldev` | Starts local dev using the best available command |

### `ldev` Priority

`ldev` chooses the first available option:

1. `composer run dev` if `composer.json` has a `dev` script.
2. `npm run dev` if `package.json` has a `dev` script.
3. `php artisan serve` as the fallback.

## Composer And NPM Commands

| Command | Example | What It Does |
| --- | --- | --- |
| `cr` | `cr test` | Runs `composer run <args>` |
| `nr` | `nr dev` | Runs `npm run <args>` |

Both commands support script-name autocomplete from the nearest `composer.json` or `package.json`.

## Navigation Commands

| Command | Example | What It Does |
| --- | --- | --- |
| `devhome` | `devhome` | Jumps to the configured starting folder |
| `cd` | `cd` | Opens the interactive folder navigator |
| `cd <path>` | `cd W:\GitHub\MBS-Terminal` | Keeps normal PowerShell directory changing behavior |
| `ls -nav` | `ls -nav` | Opens the interactive folder navigator |

### Folder Navigator Keys

| Key | Behavior |
| --- | --- |
| `Up` / `Down` | Move selection |
| `Enter` | Open selected folder |
| `Backspace` | Move to parent folder |
| `Esc` | Exit and show the current folder listing |
| `Q` | Exit and show the current folder listing |

## File Listing Commands

`ls` replaces the default PowerShell alias with a clean table view.

| Command | What It Does |
| --- | --- |
| `ls` | Lists visible files and folders in a clean table |
| `ls <path>` | Lists a specific path |
| `ls -a` | Shows hidden dot-prefixed entries |
| `ls -la` | Shows hidden dot-prefixed entries |
| `ls -al` | Shows hidden dot-prefixed entries |
| `ls -all` / `ls --all` | Shows hidden dot-prefixed entries |
| `ls -force` / `ls --force` | Shows hidden dot-prefixed entries |
| `ls -hidden` / `ls --hidden` | Shows hidden dot-prefixed entries |
| `ls -file` / `ls --file` | Shows files only |
| `ls -files` / `ls --files` | Shows files only |
| `ls -dir` / `ls --dir` | Shows directories only |
| `ls -directory` / `ls --directory` | Shows directories only |
| `ls -recursive` / `ls --recursive` | Lists recursively |
| `ls -recurse` / `ls --recurse` | Lists recursively |
| `ls -nav` / `ls --nav` | Opens the folder navigator |

### `ls` Colors

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

MBS Terminal adds command completion for Laravel, Composer, and NPM workflows.

| Completion Target | Behavior |
| --- | --- |
| `pa`, `art`, `Invoke-LaravelArtisan` | Completes Artisan commands from `php artisan list --raw` |
| `php artisan ...` | Completes Artisan commands when using native `php artisan` |
| `cr`, `Invoke-ComposerRun` | Completes scripts from nearest `composer.json` |
| `composer run ...` | Completes scripts from nearest `composer.json` |
| `composer run-script ...` | Completes scripts from nearest `composer.json` |
| `nr`, `Invoke-NpmRun` | Completes scripts from nearest `package.json` |
| `npm run ...` | Completes scripts from nearest `package.json` |
| `npm run-script ...` | Completes scripts from nearest `package.json` |

Completion results are cached for about five minutes per project/file so the prompt stays fast.

## System Helpers

| Helper | Purpose |
| --- | --- |
| Welcome banner | Shows a greeting, display name, current project folder, and random developer quote when a new terminal session starts |
| UTF-8 setup | Sets PowerShell input/output encoding to UTF-8 to prevent broken prompt symbols |
| PSReadLine options | Enables friendlier suggestions and key bindings when PSReadLine is available |

## PSReadLine Keys

| Key | Behavior |
| --- | --- |
| `Tab` | Accepts the visible suggestion |
| `RightArrow` | Accepts the full suggestion |
| `Alt+RightArrow` | Accepts the next suggestion word |
| `Ctrl+Space` | Opens the completion menu |
