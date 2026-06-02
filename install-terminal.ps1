[CmdletBinding()]
param(
    [ValidateSet('Custom', 'Recommended', 'Full', 'Minimal')]
    [string] $Preset = 'Custom',

    [switch] $Yes,
    [switch] $DryRun,
    [switch] $NoAdminRelaunch,
    [switch] $WaitAtEnd
)

$ErrorActionPreference = 'Stop'

$script:Warnings = New-Object System.Collections.Generic.List[string]
$script:StepNumber = 0
$script:FinalState = 'SUCCESS'

function Write-Banner {
    Write-Host ''
    Write-Host '+----------------------------------------------------------------------+' -ForegroundColor DarkCyan
    Write-Host '| MBS Terminal Installer                                               |' -ForegroundColor Cyan
    Write-Host '| Fresh Windows setup, one clear step at a time.                       |' -ForegroundColor DarkCyan
    Write-Host '+----------------------------------------------------------------------+' -ForegroundColor DarkCyan
    Write-Host ''
}

function Write-Step {
    param([string] $Message)

    $script:StepNumber++
    Write-Host ''
    Write-Host ("[{0:00}] {1}" -f $script:StepNumber, $Message) -ForegroundColor Cyan
    Write-Host ('-' * 72) -ForegroundColor DarkCyan
}

function Write-Info {
    param([string] $Message)

    Write-Host "    $Message" -ForegroundColor Gray
}

function Write-Help {
    param([string] $Message)

    Write-Host "    Hint: $Message" -ForegroundColor DarkGray
}

function Write-Status {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Label,

        [Parameter(Mandatory = $true)]
        [string] $Message,

        [ConsoleColor] $Color = [ConsoleColor]::Gray
    )

    Write-Host ("    [{0,-7}] {1}" -f $Label.ToUpperInvariant(), $Message) -ForegroundColor $Color
}

function Write-Ok {
    param([string] $Message)

    Write-Status -Label 'READY' -Message $Message -Color Green
}

function Write-Done {
    param([string] $Message)

    Write-Status -Label 'DONE' -Message $Message -Color Green
}

function Write-Skip {
    param([string] $Message)

    Write-Status -Label 'SKIP' -Message $Message -Color DarkGray
}

function Write-SoftWarning {
    param([string] $Message)

    [void] $script:Warnings.Add($Message)
    Write-Status -Label 'WARN' -Message $Message -Color Yellow
}

function Write-PlanRow {
    param(
        [string] $Label,
        [string] $Value,
        [ConsoleColor] $Color = [ConsoleColor]::Gray
    )

    Write-Host ("    {0,-20} {1}" -f ($Label + ':'), $Value) -ForegroundColor $Color
}

function Wait-ForExitIfRequested {
    if (-not $WaitAtEnd) {
        return
    }

    Write-Host ''
    Write-Host 'Press Enter to close this installer...' -ForegroundColor DarkGray
    [void](Read-Host)
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

    if ($WaitAtEnd) {
        $arguments += '-WaitAtEnd'
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
        Write-Status -Label 'DRYRUN' -Message ("{0} {1}" -f $FilePath, ($Arguments -join ' ')) -Color DarkGray
        return $true
    }

    Write-Status -Label 'INSTALL' -Message ("Starting {0}" -f $FilePath) -Color Cyan
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

    Write-Done "$Description completed."
    return $true
}

function Test-Winget {
    return Test-CommandAvailable -Name 'winget'
}

function Install-WingetIfMissing {
    Write-Status -Label 'CHECK' -Message 'Looking for winget package manager.' -Color Cyan

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
            Write-Status -Label 'DRYRUN' -Message "download https://aka.ms/getwinget to $wingetBundlePath" -Color DarkGray
        } else {
            [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
            Write-Status -Label 'INSTALL' -Message 'Downloading App Installer bundle from Microsoft.' -Color Cyan
            Invoke-WebRequest -Uri 'https://aka.ms/getwinget' -OutFile $wingetBundlePath -UseBasicParsing
            Write-Status -Label 'INSTALL' -Message 'Registering App Installer package.' -Color Cyan
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
        Write-Skip 'Windows Terminal install was skipped by selection.'
        return
    }

    Write-Status -Label 'CHECK' -Message 'Looking for Windows Terminal.' -Color Cyan

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
        Write-Done 'Windows Terminal is ready.'
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
    Write-Help 'These values control the welcome prompt name and the folder opened by new terminal tabs.'
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
    Write-Help 'Windows Terminal is the shell window, winget installs missing packages, and Starship powers the prompt theme.'
    $plan.InstallWindowsTerminal = Ask-YesNo -Question 'Install Windows Terminal when missing?' -Default $true
    $plan.InstallStarship = Ask-YesNo -Question 'Install Starship prompt when missing?' -Default $true
    Write-Help 'PHP and Composer are required for Laravel installer, Pint, Valet, Envoy, and Vapor.'
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
    Write-Help 'Recommended installs Laravel Installer and Pint. Valet, Envoy, and Vapor are useful but more specialized.'
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

    Write-Step 'Review install sequence.'
    Write-Status -Label 'CHECK' -Message 'Confirm administrator rights and required repository files.' -Color Cyan
    Write-Status -Label 'CHECK' -Message 'Verify winget when selected installs need packages.' -Color Cyan
    Write-Status -Label 'INSTALL' -Message 'Install Windows Terminal when it is missing.' -Color Cyan
    Write-Status -Label 'INSTALL' -Message 'Apply MBS profile, prompt, icons, and selected developer tools.' -Color Cyan
    Write-Info ''
    Write-PlanRow -Label 'Display name' -Value $Plan.DisplayName -Color White
    Write-PlanRow -Label 'Starting directory' -Value $Plan.StartingDirectory -Color White
    Write-PlanRow -Label 'Install scope' -Value $Plan.InstallScope
    Write-PlanRow -Label 'Windows Terminal' -Value (ConvertTo-YesNo $Plan.InstallWindowsTerminal)
    Write-PlanRow -Label 'Starship' -Value (ConvertTo-YesNo $Plan.InstallStarship)
    Write-PlanRow -Label 'PHP' -Value (ConvertTo-PhpSummary $Plan)
    Write-PlanRow -Label 'Composer' -Value (ConvertTo-YesNo $Plan.InstallComposer)
    Write-PlanRow -Label 'Laravel Installer' -Value (ConvertTo-YesNo $Plan.InstallLaravel)
    Write-PlanRow -Label 'Valet for Windows' -Value (ConvertTo-YesNo $Plan.InstallValet)
    Write-PlanRow -Label 'Pint' -Value (ConvertTo-YesNo $Plan.InstallPint)
    Write-PlanRow -Label 'Envoy' -Value (ConvertTo-YesNo $Plan.InstallEnvoy)
    Write-PlanRow -Label 'Vapor CLI' -Value (ConvertTo-YesNo $Plan.InstallVapor)
    Write-PlanRow -Label 'Update tools' -Value (ConvertTo-YesNo $Plan.UpdateTools)
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
    Write-Step 'Final status.'

    if ($script:Warnings.Count -gt 0) {
        $script:FinalState = 'COMPLETED WITH WARNINGS'
        Write-Status -Label 'WARN' -Message $script:FinalState -Color Yellow
        Write-Host ''
        Write-Host '    Warnings:' -ForegroundColor Yellow

        foreach ($warning in $script:Warnings) {
            Write-Host "      - $warning" -ForegroundColor Yellow
        }

        Write-Host ''
        Write-Host '    Open a new terminal after fixing any warning above, then run this installer again if needed.' -ForegroundColor Yellow
        return
    }

    $script:FinalState = 'SUCCESS'
    Write-Status -Label 'SUCCESS' -Message 'MBS Terminal setup completed.' -Color Green
    Write-Info 'Open a new Windows Terminal tab to use it.'
}

function Write-FailureSummary {
    param([string] $Message)

    $script:FinalState = 'FAILED'
    Write-Step 'Final status.'
    Write-Status -Label 'FAILED' -Message 'MBS Terminal setup did not complete.' -Color Red
    Write-Info $Message
    Write-Info 'Fix the message above, then run MBS-Terminal-Install.exe again.'
}

function Write-CancelSummary {
    $script:FinalState = 'CANCELED'
    Write-Step 'Final status.'
    Write-Status -Label 'CANCELED' -Message 'Install canceled before changes were made.' -Color Yellow
}

function Invoke-MbsTerminalInstall {
    if ((-not (Test-IsAdministrator)) -and (-not $NoAdminRelaunch) -and (-not $DryRun)) {
        Write-Banner
        Write-Status -Label 'ADMIN' -Message 'Administrator permission is required. Relaunching elevated...' -Color Yellow
        Invoke-SelfAsAdministrator
    }

    Write-Banner

    $repositoryRoot = Resolve-RepositoryRoot
    $installScript = Join-Path $repositoryRoot 'install.ps1'

    Write-Step 'Preparing installer.'
    Write-Status -Label 'CHECK' -Message "Repository root: $repositoryRoot" -Color Cyan

    if (-not (Test-Path -LiteralPath $installScript)) {
        throw "install.ps1 was not found next to this script. Expected: $installScript"
    }

    Write-Ok 'install.ps1 was found.'

    if ((-not (Test-IsAdministrator)) -and (-not $DryRun)) {
        throw 'MBS Terminal installer must be run as administrator.'
    }

    if ($DryRun) {
        Write-Status -Label 'DRYRUN' -Message 'No machine changes will be made in this run.' -Color DarkGray
    }

    $plan = Read-InstallPlan
    Repair-PlanDependencies -Plan $plan
    Show-InstallPlan -Plan $plan

    if (-not (Ask-YesNo -Question 'Start installation now?' -Default $true)) {
        Write-CancelSummary
        return 1
    }

    $needsWinget = $plan.InstallWindowsTerminal -or $plan.InstallStarship -or $plan.InstallPhp

    if ($needsWinget) {
        Write-Step 'Checking package manager.'
        [void](Install-WingetIfMissing)
    } else {
        Write-Step 'Checking package manager.'
        Write-Skip 'winget is not needed for the selected options.'
    }

    Install-WindowsTerminalIfRequested -InstallWindowsTerminal $plan.InstallWindowsTerminal

    $installArguments = Build-InstallScriptArguments -InstallScript $installScript -Plan $plan
    [void](Invoke-NativeCommand `
        -FilePath 'powershell.exe' `
        -Arguments $installArguments `
        -Description 'Running MBS Terminal installer.')

    Write-Summary
    return 0
}

$exitCode = 1

try {
    $exitCode = Invoke-MbsTerminalInstall
} catch {
    Write-FailureSummary -Message $_.Exception.Message
    $exitCode = 1
} finally {
    Wait-ForExitIfRequested
}

exit $exitCode
