param(
    [switch] $KeepStarship
)

$ErrorActionPreference = 'Stop'

function Write-Step {
    param([string] $Message)

    Write-Host "[MBS-Terminal Restore] $Message" -ForegroundColor Cyan
}

function Write-SoftWarning {
    param([string] $Message)

    Write-Host "[MBS-Terminal Restore] Warning: $Message" -ForegroundColor Yellow
}

function Backup-File {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [string] $Timestamp
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return $null
    }

    $backupPath = "$Path.mbs-restore-backup-$Timestamp"
    Copy-Item -LiteralPath $Path -Destination $backupPath -Force

    return $backupPath
}

function Move-FileToBackup {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [string] $Timestamp
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return $null
    }

    $backupPath = "$Path.mbs-restore-backup-$Timestamp"
    Move-Item -LiteralPath $Path -Destination $backupPath -Force

    return $backupPath
}

function Get-DefaultWindowsTerminalSettings {
    return @'
{
    "$help": "https://aka.ms/terminal-documentation",
    "$schema": "https://aka.ms/terminal-profiles-schema",
    "actions": [],
    "copyFormatting": "none",
    "copyOnSelect": false,
    "defaultProfile": "{61c54bbd-c2c6-5271-96e7-009a87ff44bf}",
    "keybindings": [
        {
            "id": "Terminal.CopyToClipboard",
            "keys": "ctrl+c"
        },
        {
            "id": "Terminal.PasteFromClipboard",
            "keys": "ctrl+v"
        },
        {
            "id": "Terminal.DuplicatePaneAuto",
            "keys": "alt+shift+d"
        }
    ],
    "newTabMenu": [
        {
            "type": "remainingProfiles"
        }
    ],
    "profiles": {
        "defaults": {},
        "list": [
            {
                "commandline": "%SystemRoot%\\System32\\WindowsPowerShell\\v1.0\\powershell.exe",
                "guid": "{61c54bbd-c2c6-5271-96e7-009a87ff44bf}",
                "hidden": false,
                "name": "Windows PowerShell"
            },
            {
                "commandline": "%SystemRoot%\\System32\\cmd.exe",
                "guid": "{0caa0dad-35be-5f56-a8ff-afceeeaa6101}",
                "hidden": false,
                "name": "Command Prompt"
            },
            {
                "guid": "{b453ae62-4e3d-5e58-b989-0a998ec441b8}",
                "hidden": false,
                "name": "Azure Cloud Shell",
                "source": "Windows.Terminal.Azure"
            }
        ]
    },
    "schemes": [],
    "themes": []
}
'@
}

function Restore-WindowsTerminalSettings {
    param([string] $Timestamp)

    $settingsCandidates = @(
        (Join-Path $env:LOCALAPPDATA 'Packages\Microsoft.WindowsTerminal_8wekyb3d8bbwe\LocalState\settings.json'),
        (Join-Path $env:LOCALAPPDATA 'Packages\Microsoft.WindowsTerminalPreview_8wekyb3d8bbwe\LocalState\settings.json'),
        (Join-Path $env:LOCALAPPDATA 'Microsoft\Windows Terminal\settings.json')
    )

    $settingsPaths = @($settingsCandidates | Where-Object { Test-Path -LiteralPath $_ })

    if ($settingsPaths.Count -eq 0) {
        Write-SoftWarning 'No Windows Terminal settings.json file was found.'
        return
    }

    foreach ($settingsPath in $settingsPaths) {
        $backupPath = Backup-File -Path $settingsPath -Timestamp $Timestamp
        Get-DefaultWindowsTerminalSettings | Set-Content -LiteralPath $settingsPath -Encoding UTF8
        Write-Step "Restored Windows Terminal settings: $settingsPath"
        Write-Step "Backup created: $backupPath"
    }
}

function Restore-PowerShellProfile {
    param([string] $Timestamp)

    $profilePath = $PROFILE.CurrentUserCurrentHost

    if (-not (Test-Path -LiteralPath $profilePath)) {
        Write-SoftWarning 'PowerShell profile was not found.'
        return
    }

    $backupPath = Backup-File -Path $profilePath -Timestamp $Timestamp
    $content = Get-Content -LiteralPath $profilePath -Raw

    $content = [regex]::Replace(
        $content,
        "(?s)\r?\n?# >>> MBS-Terminal.*?# <<< MBS-Terminal\r?\n?",
        [Environment]::NewLine
    )

    $content = [regex]::Replace(
        $content,
        '(?m)^\s*if\s*\(\s*Test-Path\s+-LiteralPath\s+["'']\$HOME\\\.config\\powershell\\laravel-dev\.ps1["'']\s*\)\s*\{\s*\r?\n\s*\.\s+["'']\$HOME\\\.config\\powershell\\laravel-dev\.ps1["'']\s*\r?\n\s*\}\s*\r?\n?',
        ''
    )

    if (-not $KeepStarship) {
        $content = [regex]::Replace(
            $content,
            "(?m)^\s*(Invoke-Expression\s+\(&starship\s+init\s+powershell\)|if\s*\(\s*Get-Command\s+starship.*?\}\s*)\r?\n?",
            ''
        )
    }

    Set-Content -LiteralPath $profilePath -Value $content.Trim() -Encoding ASCII
    Write-Step "Cleaned PowerShell profile: $profilePath"
    Write-Step "Backup created: $backupPath"
}

function Restore-UserConfigFiles {
    param([string] $Timestamp)

    $helperPath = Join-Path $HOME '.config\powershell\laravel-dev.ps1'
    $helperBackup = Move-FileToBackup -Path $helperPath -Timestamp $Timestamp

    if ($helperBackup) {
        Write-Step "Moved MBS PowerShell helper to backup: $helperBackup"
    }

    if (-not $KeepStarship) {
        $starshipPath = Join-Path $HOME '.config\starship.toml'
        $starshipBackup = Move-FileToBackup -Path $starshipPath -Timestamp $Timestamp

        if ($starshipBackup) {
            Write-Step "Moved Starship config to backup: $starshipBackup"
        }
    }

    $iconsPath = Join-Path $HOME '.config\terminal-icons'

    if (Test-Path -LiteralPath $iconsPath) {
        $iconBackupPath = "$iconsPath.mbs-restore-backup-$Timestamp"

        if (-not (Test-Path -LiteralPath $iconBackupPath)) {
            New-Item -ItemType Directory -Path $iconBackupPath | Out-Null
        }

        Get-ChildItem -LiteralPath $iconsPath -Filter 'mbs-*.png' -ErrorAction SilentlyContinue | ForEach-Object {
            Move-Item -LiteralPath $_.FullName -Destination (Join-Path $iconBackupPath $_.Name) -Force
        }

        if ((Get-ChildItem -LiteralPath $iconBackupPath -ErrorAction SilentlyContinue | Measure-Object).Count -gt 0) {
            Write-Step "Moved MBS terminal icons to backup: $iconBackupPath"
        }
    }
}

$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'

Write-Step 'Restoring plain Windows Terminal settings.'
Restore-WindowsTerminalSettings -Timestamp $timestamp

Write-Step 'Removing MBS PowerShell startup hooks.'
Restore-PowerShellProfile -Timestamp $timestamp

Write-Step 'Moving MBS config files aside.'
Restore-UserConfigFiles -Timestamp $timestamp

Write-Step 'Done. Open a new Windows Terminal tab to verify the default look.'
