[CmdletBinding()]
param(
    [ValidateSet('Custom', 'Recommended', 'Full', 'Minimal')]
    [string] $Preset = 'Custom',

    [switch] $Yes,
    [switch] $DryRun,
    [switch] $NoAdminRelaunch
)

$ErrorActionPreference = 'Stop'

$script:Warnings = New-Object System.Collections.Generic.List[string]

function Write-Banner {
    Write-Host ''
    Write-Host 'MBS Terminal interactive installer' -ForegroundColor Cyan
    Write-Host 'Fresh Windows setup, one step at a time.' -ForegroundColor DarkCyan
    Write-Host ''
}

function Write-Step {
    param([string] $Message)

    Write-Host ''
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Write-Info {
    param([string] $Message)

    Write-Host "    $Message" -ForegroundColor Gray
}

function Write-Ok {
    param([string] $Message)

    Write-Host "    OK: $Message" -ForegroundColor Green
}

function Write-SoftWarning {
    param([string] $Message)

    [void] $script:Warnings.Add($Message)
    Write-Host "    Warning: $Message" -ForegroundColor Yellow
}

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Invoke-SelfAsAdministrator {
    $arguments = @(
        '-NoProfile',
        '-ExecutionPolicy',
        'Bypass',
        '-File',
        $PSCommandPath,
        '-Preset',
        $Preset
    )

    if ($Yes) {
        $arguments += '-Yes'
    }

    if ($DryRun) {
        $arguments += '-DryRun'
    }

    if ($NoAdminRelaunch) {
        $arguments += '-NoAdminRelaunch'
    }

    $process = Start-Process -FilePath 'powershell.exe' -ArgumentList $arguments -Verb RunAs -Wait -PassThru
    exit $process.ExitCode
}

function Ask-YesNo {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Question,

        [bool] $Default = $true
    )

    if ($Yes -or $Preset -ne 'Custom') {
        return $Default
    }

    $suffix = if ($Default) { 'Y/n' } else { 'y/N' }

    while ($true) {
        $answer = (Read-Host "$Question [$suffix]").Trim()

        if ([string]::IsNullOrWhiteSpace($answer)) {
            return $Default
        }

        if ($answer -match '^(y|yes)$') {
            return $true
        }

        if ($answer -match '^(n|no)$') {
            return $false
        }

        Write-Host 'Please answer y or n.' -ForegroundColor Yellow
    }
}

function Read-TextValue {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Prompt,

        [string] $Default = ''
    )

    if ($Yes -or $Preset -ne 'Custom') {
        return $Default
    }

    if ([string]::IsNullOrWhiteSpace($Default)) {
        $value = Read-Host $Prompt
    } else {
        $value = Read-Host "$Prompt [$Default]"
    }

    if ([string]::IsNullOrWhiteSpace($value)) {
        return $Default
    }

    return $value.Trim()
}

function Read-PhpVersion {
    param([string] $Default = '8.4')

    if ($Yes -or $Preset -ne 'Custom') {
        return $Default
    }

    $supportedVersions = @('8.2', '8.3', '8.4', '8.5')

    while ($true) {
        $value = Read-TextValue -Prompt 'PHP version' -Default $Default

        if ($supportedVersions -contains $value) {
            return $value
        }

        Write-Host 'Choose one of: 8.2, 8.3, 8.4, 8.5.' -ForegroundColor Yellow
    }
}

function Resolve-RepositoryRoot {
    if ($PSScriptRoot) {
        return $PSScriptRoot
    }

    return (Get-Location).ProviderPath
}

function Refresh-ProcessPath {
    $machinePath = [Environment]::GetEnvironmentVariable('Path', 'Machine')
    $userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
    $env:Path = @($machinePath, $userPath) -join ';'
}

function Test-CommandAvailable {
    param([string] $Name)

    return $null -ne (Get-Command $Name -ErrorAction SilentlyContinue)
}

function Invoke-NativeCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string] $FilePath,

        [Parameter(Mandatory = $true)]
        [string[]] $Arguments,

        [Parameter(Mandatory = $true)]
        [string] $Description,

        [switch] $ContinueOnError
    )

    Write-Step $Description

    if ($DryRun) {
        Write-Info ("DRY RUN: {0} {1}" -f $FilePath, ($Arguments -join ' '))
        return $true
    }

    & $FilePath @Arguments
    $exitCode = $LASTEXITCODE

    if ($exitCode -ne 0) {
        $message = "$Description failed with exit code $exitCode."

        if ($ContinueOnError) {
            Write-SoftWarning $message
            return $false
        }

        throw $message
    }

    return $true
}

function Test-Winget {
    return Test-CommandAvailable -Name 'winget'
}

function Install-WingetIfMissing {
    if (Test-Winget) {
        Write-Ok 'winget is available.'
        return $true
    }

    Write-SoftWarning 'winget was not found. Fresh Windows installs may need Microsoft App Installer first.'

    if (-not (Ask-YesNo -Question 'Try to install winget from Microsoft now?' -Default $true)) {
        return $false
    }

    $wingetBundlePath = Join-Path $env:TEMP 'Microsoft.DesktopAppInstaller_8wekyb3d8bbwe.msixbundle'

    try {
        Write-Step 'Downloading Microsoft App Installer.'

        if ($DryRun) {
            Write-Info "DRY RUN: download https://aka.ms/getwinget to $wingetBundlePath"
        } else {
            [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
            Invoke-WebRequest -Uri 'https://aka.ms/getwinget' -OutFile $wingetBundlePath -UseBasicParsing
            Add-AppxPackage -Path $wingetBundlePath
            Refresh-ProcessPath
        }

        if ($DryRun -or (Test-Winget)) {
            Write-Ok 'winget is available.'
            return $true
        }

        Write-SoftWarning 'Microsoft App Installer finished, but winget is still not visible in this terminal.'
        return $false
    } catch {
        Write-SoftWarning "Could not install winget automatically. $($_.Exception.Message)"
        return $false
    } finally {
        if ((-not $DryRun) -and (Test-Path -LiteralPath $wingetBundlePath)) {
            Remove-Item -LiteralPath $wingetBundlePath -Force
        }
    }
}

function Test-WindowsTerminal {
    if (Test-CommandAvailable -Name 'wt') {
        return $true
    }

    $packageRoots = @(
        (Join-Path $env:LOCALAPPDATA 'Packages\Microsoft.WindowsTerminal_8wekyb3d8bbwe'),
        (Join-Path $env:LOCALAPPDATA 'Packages\Microsoft.WindowsTerminalPreview_8wekyb3d8bbwe'),
        (Join-Path $env:LOCALAPPDATA 'Microsoft\Windows Terminal')
    )

    foreach ($packageRoot in $packageRoots) {
        if (Test-Path -LiteralPath $packageRoot) {
            return $true
        }
    }

    return $false
}

function Install-WindowsTerminalIfRequested {
    param([bool] $InstallWindowsTerminal)

    if (-not $InstallWindowsTerminal) {
        Write-Info 'Windows Terminal install was skipped by selection.'
        return
    }

    if (Test-WindowsTerminal) {
        Write-Ok 'Windows Terminal is already installed.'
        return
    }

    if (-not (Install-WingetIfMissing)) {
        Write-SoftWarning 'Windows Terminal cannot be installed automatically without winget.'
        return
    }

    [void](Invoke-NativeCommand `
        -FilePath 'winget' `
        -Arguments @(
            'install',
            '--id',
            'Microsoft.WindowsTerminal',
            '--exact',
            '--source',
            'winget',
            '--accept-package-agreements',
            '--accept-source-agreements'
        ) `
        -Description 'Installing Windows Terminal.' `
        -ContinueOnError)

    Refresh-ProcessPath

    if (Test-WindowsTerminal) {
        Write-Ok 'Windows Terminal is ready.'
    } else {
        Write-SoftWarning 'Windows Terminal was requested, but it was not detected yet. Open Microsoft Store or run this installer again after App Installer finishes updates.'
    }
}

function New-InstallPlan {
    $displayName = $env:USERNAME

    if ([string]::IsNullOrWhiteSpace($displayName)) {
        $displayName = 'Developer'
    }

    $plan = [pscustomobject]@{
        StartingDirectory      = $HOME
        DisplayName            = $displayName
        InstallScope           = 'CurrentUser'
        InstallWindowsTerminal = $true
        InstallStarship        = $true
        InstallPhp             = $true
        PhpVersion             = '8.4'
        PhpDirectory           = ''
        InstallComposer        = $true
        InstallLaravel         = $true
        InstallValet           = $false
        InstallPint            = $true
        InstallEnvoy           = $false
        InstallVapor           = $false
        UpdateTools            = $false
    }

    if ($Preset -eq 'Minimal') {
        $plan.InstallPhp = $false
        $plan.InstallComposer = $false
        $plan.InstallLaravel = $false
        $plan.InstallPint = $false
    }

    if ($Preset -eq 'Full') {
        $plan.InstallValet = $true
        $plan.InstallEnvoy = $true
        $plan.InstallVapor = $true
        $plan.UpdateTools = $true
    }

    return $plan
}

function Read-InstallPlan {
    $plan = New-InstallPlan

    if ($Preset -ne 'Custom') {
        return $plan
    }

    Write-Step 'Choose profile settings.'
    $plan.DisplayName = Read-TextValue -Prompt 'Prompt display name' -Default $plan.DisplayName
    $plan.StartingDirectory = Read-TextValue -Prompt 'Terminal starting directory' -Default $plan.StartingDirectory

    if (-not (Test-Path -LiteralPath $plan.StartingDirectory)) {
        if (Ask-YesNo -Question "Create starting directory '$($plan.StartingDirectory)'?" -Default $true) {
            if ($DryRun) {
                Write-Info "DRY RUN: create $($plan.StartingDirectory)"
            } else {
                New-Item -ItemType Directory -Path $plan.StartingDirectory | Out-Null
            }
        } else {
            Write-SoftWarning "Starting directory does not exist. Falling back to $HOME."
            $plan.StartingDirectory = $HOME
        }
    }

    if (Ask-YesNo -Question 'Install PATH entries for all users?' -Default $false) {
        $plan.InstallScope = 'AllUsers'
    }

    Write-Step 'Choose required tools.'
    $plan.InstallWindowsTerminal = Ask-YesNo -Question 'Install Windows Terminal when missing?' -Default $true
    $plan.InstallStarship = Ask-YesNo -Question 'Install Starship prompt when missing?' -Default $true
    $plan.InstallPhp = Ask-YesNo -Question 'Install PHP with winget?' -Default $true

    if ($plan.InstallPhp) {
        $plan.PhpVersion = Read-PhpVersion -Default $plan.PhpVersion
    } else {
        if (Ask-YesNo -Question 'Use an existing PHP directory instead?' -Default $false) {
            $plan.PhpDirectory = Read-TextValue -Prompt 'Existing PHP directory or php.exe path' -Default ''
        }
    }

    $plan.InstallComposer = Ask-YesNo -Question 'Install Composer?' -Default $true

    Write-Step 'Choose Laravel tooling.'
    $plan.InstallLaravel = Ask-YesNo -Question 'Install Laravel Installer?' -Default $true
    $plan.InstallValet = Ask-YesNo -Question 'Install Valet for Windows?' -Default $false
    $plan.InstallPint = Ask-YesNo -Question 'Install Laravel Pint?' -Default $true
    $plan.InstallEnvoy = Ask-YesNo -Question 'Install Laravel Envoy?' -Default $false
    $plan.InstallVapor = Ask-YesNo -Question 'Install Laravel Vapor CLI?' -Default $false
    $plan.UpdateTools = Ask-YesNo -Question 'Update existing tools while installing?' -Default $false

    return $plan
}

function Repair-PlanDependencies {
    param([pscustomobject] $Plan)

    $needsComposer = $Plan.InstallLaravel -or $Plan.InstallValet -or $Plan.InstallPint -or $Plan.InstallEnvoy -or $Plan.InstallVapor

    if ($needsComposer -and (-not $Plan.InstallComposer) -and (-not (Test-CommandAvailable -Name 'composer'))) {
        Write-SoftWarning 'Composer is required for selected Laravel tooling, so Composer will be installed.'
        $Plan.InstallComposer = $true
    }

    if ($Plan.InstallComposer -and (-not $Plan.InstallPhp) -and [string]::IsNullOrWhiteSpace($Plan.PhpDirectory) -and (-not (Test-CommandAvailable -Name 'php'))) {
        Write-SoftWarning 'Composer needs PHP, so PHP install has been enabled.'
        $Plan.InstallPhp = $true
    }
}

function Show-InstallPlan {
    param([pscustomobject] $Plan)

    Write-Step 'Install sequence.'
    Write-Info '1. Check administrator rights and repository files.'
    Write-Info '2. Ensure winget is available when selected installs need it.'
    Write-Info '3. Install Windows Terminal when missing.'
    Write-Info '4. Run install.ps1 to apply MBS Terminal profile, prompt, icons, and selected developer tools.'
    Write-Info ''
    Write-Info "Display name:        $($Plan.DisplayName)"
    Write-Info "Starting directory:  $($Plan.StartingDirectory)"
    Write-Info "Install scope:       $($Plan.InstallScope)"
    Write-Info "Windows Terminal:    $(ConvertTo-YesNo $Plan.InstallWindowsTerminal)"
    Write-Info "Starship:            $(ConvertTo-YesNo $Plan.InstallStarship)"
    Write-Info "PHP:                 $(ConvertTo-PhpSummary $Plan)"
    Write-Info "Composer:            $(ConvertTo-YesNo $Plan.InstallComposer)"
    Write-Info "Laravel Installer:   $(ConvertTo-YesNo $Plan.InstallLaravel)"
    Write-Info "Valet for Windows:   $(ConvertTo-YesNo $Plan.InstallValet)"
    Write-Info "Pint:                $(ConvertTo-YesNo $Plan.InstallPint)"
    Write-Info "Envoy:               $(ConvertTo-YesNo $Plan.InstallEnvoy)"
    Write-Info "Vapor CLI:           $(ConvertTo-YesNo $Plan.InstallVapor)"
    Write-Info "Update tools:        $(ConvertTo-YesNo $Plan.UpdateTools)"
}

function ConvertTo-YesNo {
    param([bool] $Value)

    if ($Value) {
        return 'yes'
    }

    return 'no'
}

function ConvertTo-PhpSummary {
    param([pscustomobject] $Plan)

    if ($Plan.InstallPhp) {
        return "install $($Plan.PhpVersion)"
    }

    if (-not [string]::IsNullOrWhiteSpace($Plan.PhpDirectory)) {
        return "use $($Plan.PhpDirectory)"
    }

    return 'no'
}

function Build-InstallScriptArguments {
    param(
        [Parameter(Mandatory = $true)]
        [string] $InstallScript,

        [Parameter(Mandatory = $true)]
        [pscustomobject] $Plan
    )

    $arguments = @(
        '-NoProfile',
        '-ExecutionPolicy',
        'Bypass',
        '-File',
        $InstallScript,
        '-StartingDirectory',
        $Plan.StartingDirectory,
        '-DisplayName',
        $Plan.DisplayName,
        '-InstallScope',
        $Plan.InstallScope
    )

    if ($Plan.InstallStarship) {
        $arguments += '-InstallDependencies'
    }

    if ($Plan.InstallPhp) {
        $arguments += @('-InstallPhp', '-PhpVersion', $Plan.PhpVersion)
    }

    if (-not [string]::IsNullOrWhiteSpace($Plan.PhpDirectory)) {
        $arguments += @('-PhpDirectory', $Plan.PhpDirectory)
    }

    if ($Plan.InstallComposer) {
        $arguments += '-InstallComposer'
    }

    if ($Plan.InstallLaravel) {
        $arguments += '-InstallLaravel'
    }

    if ($Plan.InstallValet) {
        $arguments += '-InstallValet'
    }

    if ($Plan.InstallPint) {
        $arguments += '-InstallPint'
    }

    if ($Plan.InstallEnvoy) {
        $arguments += '-InstallEnvoy'
    }

    if ($Plan.InstallVapor) {
        $arguments += '-InstallVapor'
    }

    if ($Plan.UpdateTools) {
        $arguments += '-UpdateTools'
    }

    return $arguments
}

function Write-Summary {
    Write-Step 'Finished.'

    if ($script:Warnings.Count -gt 0) {
        Write-Host 'Warnings:' -ForegroundColor Yellow

        foreach ($warning in $script:Warnings) {
            Write-Host "  - $warning" -ForegroundColor Yellow
        }

        Write-Host ''
        Write-Host 'Open a new terminal after fixing any warning above, then run this installer again if needed.' -ForegroundColor Yellow
        return
    }

    Write-Host 'MBS Terminal setup completed. Open a new Windows Terminal tab to use it.' -ForegroundColor Green
}

if ((-not (Test-IsAdministrator)) -and (-not $NoAdminRelaunch) -and (-not $DryRun)) {
    Write-Banner
    Write-Info 'Administrator permission is required. Relaunching this installer elevated...'
    Invoke-SelfAsAdministrator
}

Write-Banner

$repositoryRoot = Resolve-RepositoryRoot
$installScript = Join-Path $repositoryRoot 'install.ps1'

if (-not (Test-Path -LiteralPath $installScript)) {
    throw "install.ps1 was not found next to this script. Expected: $installScript"
}

if ((-not (Test-IsAdministrator)) -and (-not $DryRun)) {
    throw 'MBS Terminal installer must be run as administrator.'
}

$plan = Read-InstallPlan
Repair-PlanDependencies -Plan $plan
Show-InstallPlan -Plan $plan

if (-not (Ask-YesNo -Question 'Start installation now?' -Default $true)) {
    Write-Host 'Install canceled before changes were made.' -ForegroundColor Yellow
    exit 1
}

$needsWinget = $plan.InstallWindowsTerminal -or $plan.InstallStarship -or $plan.InstallPhp

if ($needsWinget) {
    Write-Step 'Checking winget.'
    [void](Install-WingetIfMissing)
}

Install-WindowsTerminalIfRequested -InstallWindowsTerminal $plan.InstallWindowsTerminal

$installArguments = Build-InstallScriptArguments -InstallScript $installScript -Plan $plan
[void](Invoke-NativeCommand `
    -FilePath 'powershell.exe' `
    -Arguments $installArguments `
    -Description 'Running MBS Terminal installer.')

Write-Summary
