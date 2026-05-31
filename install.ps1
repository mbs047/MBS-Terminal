param(
    [string] $StartingDirectory = '',
    [switch] $InstallDependencies
)

$ErrorActionPreference = 'Stop'

function Write-Step {
    param([string] $Message)

    Write-Host "[MBS-Terminal] $Message" -ForegroundColor Cyan
}

function Write-SoftWarning {
    param([string] $Message)

    Write-Host "[MBS-Terminal] Warning: $Message" -ForegroundColor Yellow
}

function Get-RepositoryRoot {
    if ($PSScriptRoot) {
        return $PSScriptRoot
    }

    return (Get-Location).ProviderPath
}

function Copy-FileEnsuringDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Source,

        [Parameter(Mandatory = $true)]
        [string] $Destination
    )

    $destinationDirectory = Split-Path -Path $Destination -Parent

    if (-not (Test-Path -LiteralPath $destinationDirectory)) {
        New-Item -ItemType Directory -Path $destinationDirectory | Out-Null
    }

    Copy-Item -LiteralPath $Source -Destination $Destination -Force
}

function Install-PSReadLineIfPossible {
    if (-not (Get-Command Install-Module -ErrorAction SilentlyContinue)) {
        Write-SoftWarning 'Install-Module is not available, skipping PSReadLine update.'
        return
    }

    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Install-Module PSReadLine -Scope CurrentUser -Force -SkipPublisherCheck -AllowClobber -ErrorAction Stop
        Write-Step 'PSReadLine installed or updated for the current user.'
    } catch {
        Write-SoftWarning "Could not install PSReadLine automatically. $($_.Exception.Message)"
    }
}

function Install-StarshipIfRequested {
    if (Get-Command starship -ErrorAction SilentlyContinue) {
        return
    }

    if (-not $InstallDependencies) {
        Write-SoftWarning 'Starship is not installed. Re-run with -InstallDependencies to try winget install Starship. The prompt config was still copied.'
        return
    }

    if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
        Write-SoftWarning 'winget is not available, so Starship could not be installed automatically.'
        return
    }

    try {
        winget install --id Starship.Starship --exact --accept-package-agreements --accept-source-agreements
        Write-Step 'Starship installation requested through winget.'
    } catch {
        Write-SoftWarning "Could not install Starship automatically. $($_.Exception.Message)"
    }
}

function Install-PowerShellProfile {
    param(
        [Parameter(Mandatory = $true)]
        [string] $SourceProfile,

        [Parameter(Mandatory = $true)]
        [string] $TargetProfile,

        [Parameter(Mandatory = $true)]
        [string] $PortfolioPath
    )

    $targetHelper = Join-Path $HOME '.config\powershell\laravel-dev.ps1'
    Copy-FileEnsuringDirectory -Source $SourceProfile -Destination $targetHelper

    $escapedPortfolioPath = $PortfolioPath.Replace("'", "''")
    $helperContent = Get-Content -LiteralPath $targetHelper -Raw
    $helperContent = $helperContent -replace "\`$script:MbsPortfolioPath = '.*?'", "`$script:MbsPortfolioPath = '$escapedPortfolioPath'"
    Set-Content -LiteralPath $targetHelper -Value $helperContent -Encoding ASCII

    $profileDirectory = Split-Path -Path $TargetProfile -Parent

    if (-not (Test-Path -LiteralPath $profileDirectory)) {
        New-Item -ItemType Directory -Path $profileDirectory | Out-Null
    }

    $profileContent = ''

    if (Test-Path -LiteralPath $TargetProfile) {
        $profileContent = Get-Content -LiteralPath $TargetProfile -Raw
    }

    $hasHelper = $profileContent -match 'laravel-dev\.ps1'
    $hasStarship = $profileContent -match 'starship init powershell'
    $markerStart = '# >>> MBS-Terminal'
    $markerEnd = '# <<< MBS-Terminal'

    $profileContent = [regex]::Replace(
        $profileContent,
        "(?s)\r?\n?# >>> MBS-Terminal.*?# <<< MBS-Terminal\r?\n?",
        ''
    )

    $profileBlock = @()

    if (-not $hasHelper) {
        $profileBlock += 'if (Test-Path -LiteralPath "$HOME\.config\powershell\laravel-dev.ps1") {'
        $profileBlock += '    . "$HOME\.config\powershell\laravel-dev.ps1"'
        $profileBlock += '}'
    }

    if (-not $hasStarship) {
        $profileBlock += 'if (Get-Command starship -ErrorAction SilentlyContinue) {'
        $profileBlock += '    Invoke-Expression (&starship init powershell)'
        $profileBlock += '}'
    }

    if ($profileBlock.Count -gt 0) {
        $profileBlock = @($markerStart) + $profileBlock + @($markerEnd)
        $profileContent = $profileContent.TrimEnd() + [Environment]::NewLine + [Environment]::NewLine + ($profileBlock -join [Environment]::NewLine) + [Environment]::NewLine
        Set-Content -LiteralPath $TargetProfile -Value $profileContent -Encoding ASCII
    }

    Write-Step 'PowerShell helper profile installed.'
}

function Install-WindowsTerminalSettings {
    param(
        [Parameter(Mandatory = $true)]
        [string] $TemplatePath,

        [Parameter(Mandatory = $true)]
        [string] $IconsDirectory,

        [Parameter(Mandatory = $true)]
        [string] $StartingDirectory
    )

    $settingsCandidates = @(
        (Join-Path $env:LOCALAPPDATA 'Packages\Microsoft.WindowsTerminal_8wekyb3d8bbwe\LocalState\settings.json'),
        (Join-Path $env:LOCALAPPDATA 'Packages\Microsoft.WindowsTerminalPreview_8wekyb3d8bbwe\LocalState\settings.json'),
        (Join-Path $env:LOCALAPPDATA 'Microsoft\Windows Terminal\settings.json')
    )

    $settingsPath = $settingsCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1

    if (-not $settingsPath) {
        Write-SoftWarning 'Windows Terminal settings.json was not found. Install Windows Terminal first, then run this installer again.'
        return
    }

    $settingsDirectory = Split-Path -Path $settingsPath -Parent

    if (-not (Test-Path -LiteralPath $settingsDirectory)) {
        New-Item -ItemType Directory -Path $settingsDirectory | Out-Null
    }

    $backupPath = "$settingsPath.bak-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    Copy-Item -LiteralPath $settingsPath -Destination $backupPath -Force

    $settings = Get-Content -LiteralPath $TemplatePath -Raw | ConvertFrom-Json
    $devIcon = (Join-Path $IconsDirectory 'mbs-pixel-avatar.png').Replace('\', '/')
    $cmdIcon = (Join-Path $IconsDirectory 'mbs-cmd.png').Replace('\', '/')
    $cloudIcon = (Join-Path $IconsDirectory 'mbs-cloud.png').Replace('\', '/')

    $settings.profiles.defaults.opacity = 68
    $settings.profiles.defaults.useAcrylic = $true

    foreach ($profile in $settings.profiles.list) {
        switch ($profile.guid) {
            '{61c54bbd-c2c6-5271-96e7-009a87ff44bf}' {
                $profile.icon = $devIcon
                $profile.startingDirectory = $StartingDirectory
                $profile.commandline = '%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe -NoLogo'
            }
            '{0caa0dad-35be-5f56-a8ff-afceeeaa6101}' {
                $profile.icon = $cmdIcon
                $profile.startingDirectory = $StartingDirectory
            }
            '{b453ae62-4e3d-5e58-b989-0a998ec441b8}' {
                $profile.icon = $cloudIcon
            }
        }
    }

    $settings | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $settingsPath -Encoding UTF8
    Write-Step "Windows Terminal settings installed. Backup: $backupPath"
}

$repositoryRoot = Get-RepositoryRoot
$defaultPortfolioPath = 'W:\GitHub\MBS-Portfolio'

if ([string]::IsNullOrWhiteSpace($StartingDirectory)) {
    if (Test-Path -LiteralPath $defaultPortfolioPath) {
        $StartingDirectory = $defaultPortfolioPath
    } else {
        $StartingDirectory = $HOME
    }
}

$starshipSource = Join-Path $repositoryRoot 'configs\starship.toml'
$terminalSource = Join-Path $repositoryRoot 'configs\windows-terminal\settings.json'
$profileSource = Join-Path $repositoryRoot 'configs\powershell\laravel-dev.ps1'
$iconsSource = Join-Path $repositoryRoot 'assets\terminal-icons'
$iconsTarget = Join-Path $HOME '.config\terminal-icons'

Write-Step 'Installing terminal icons.'
if (-not (Test-Path -LiteralPath $iconsTarget)) {
    New-Item -ItemType Directory -Path $iconsTarget | Out-Null
}

Get-ChildItem -Path (Join-Path $iconsSource '*.png') | Copy-Item -Destination $iconsTarget -Force

Write-Step 'Installing Starship config.'
Copy-FileEnsuringDirectory -Source $starshipSource -Destination (Join-Path $HOME '.config\starship.toml')

Write-Step 'Installing PowerShell helper profile.'
Install-PowerShellProfile -SourceProfile $profileSource -TargetProfile $PROFILE.CurrentUserCurrentHost -PortfolioPath $StartingDirectory

Write-Step 'Installing Windows Terminal settings.'
Install-WindowsTerminalSettings -TemplatePath $terminalSource -IconsDirectory $iconsTarget -StartingDirectory $StartingDirectory

if ($InstallDependencies) {
    Install-StarshipIfRequested
}

Install-PSReadLineIfPossible

Write-Step 'Done. Open a new Windows Terminal tab to see the setup.'
