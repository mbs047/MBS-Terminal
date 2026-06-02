param(
    [string] $StartingDirectory = '',
    [switch] $InstallDependencies,
    [switch] $InstallPhp,
    [ValidateSet('8.2', '8.3', '8.4', '8.5')]
    [string] $PhpVersion = '8.4',
    [string] $PhpDirectory = '',
    [switch] $InstallComposer,
    [switch] $InstallLaravel,
    [switch] $InstallValet,
    [switch] $InstallPint,
    [switch] $InstallEnvoy,
    [switch] $InstallVapor,
    [switch] $InstallGit,
    [switch] $InstallNode,
    [switch] $InstallNvm,
    [switch] $InstallGhCli,
    [switch] $InstallPest,
    [switch] $InstallLarastan,
    [switch] $InstallRector,
    [switch] $InstallRay,
    [switch] $InstallMkcert,
    [switch] $InstallRedis,
    [switch] $InstallDocker,
    [switch] $InstallTablePlus,
    [switch] $InstallFzf,
    [switch] $InstallBat,
    [switch] $InstallRipgrep,
    [switch] $InstallLazygit,
    [switch] $UpdateTools,
    [ValidateSet('CurrentUser', 'AllUsers')]
    [string] $InstallScope = 'CurrentUser',
    [string] $DisplayName = ''
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

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-IsAdministrator)) {
    throw 'MBS Terminal installer must be run as administrator.'
}

function Resolve-DisplayName {
    param([string] $Value)

    $resolvedName = $Value

    if ([string]::IsNullOrWhiteSpace($resolvedName)) {
        $resolvedName = $env:USERNAME
    }

    if ([string]::IsNullOrWhiteSpace($resolvedName)) {
        $resolvedName = 'Developer'
    }

    $resolvedName = $resolvedName.Trim()
    $resolvedName = $resolvedName -replace '[\r\n\[\]]', ''

    if ([string]::IsNullOrWhiteSpace($resolvedName)) {
        return 'Developer'
    }

    return $resolvedName
}

function Get-EnvironmentTarget {
    if ($InstallScope -eq 'AllUsers') {
        return 'Machine'
    }

    return 'User'
}

function Add-PathEntry {
    param([string] $Directory)

    if ([string]::IsNullOrWhiteSpace($Directory)) {
        return
    }

    $resolvedDirectory = [Environment]::ExpandEnvironmentVariables($Directory)

    if (-not (Test-Path -LiteralPath $resolvedDirectory)) {
        Write-SoftWarning "Path entry was not found, skipping PATH update: $resolvedDirectory"
        return
    }

    $target = Get-EnvironmentTarget

    if ($target -eq 'Machine' -and -not (Test-IsAdministrator)) {
        Write-SoftWarning "Administrator rights are required to add machine PATH entries. Falling back to user PATH for: $resolvedDirectory"
        $target = 'User'
    }

    $currentPath = [Environment]::GetEnvironmentVariable('Path', $target)
    $entries = @()

    if (-not [string]::IsNullOrWhiteSpace($currentPath)) {
        $entries = $currentPath -split ';' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    }

    $hasEntry = $entries | Where-Object { $_.TrimEnd('\') -ieq $resolvedDirectory.TrimEnd('\') } | Select-Object -First 1

    if (-not $hasEntry) {
        $entries += $resolvedDirectory
        [Environment]::SetEnvironmentVariable('Path', ($entries -join ';'), $target)
        Write-Step "Added to $target PATH: $resolvedDirectory"
    }

    $processEntries = $env:Path -split ';' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    $hasProcessEntry = $processEntries | Where-Object { $_.TrimEnd('\') -ieq $resolvedDirectory.TrimEnd('\') } | Select-Object -First 1

    if (-not $hasProcessEntry) {
        $env:Path = "$resolvedDirectory;$env:Path"
    }
}

function Refresh-ProcessPath {
    $machinePath = [Environment]::GetEnvironmentVariable('Path', 'Machine')
    $userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
    $env:Path = @($machinePath, $userPath) -join ';'
}

function Get-WingetScopeArguments {
    if ($InstallScope -eq 'AllUsers') {
        return @('--scope', 'machine')
    }

    return @()
}

function Get-PhpWingetPackageId {
    return "PHP.PHP.$PhpVersion"
}

function Get-PhpExecutable {
    if (-not [string]::IsNullOrWhiteSpace($PhpDirectory)) {
        $expandedPath = [Environment]::ExpandEnvironmentVariables($PhpDirectory)
        $candidate = if ((Split-Path -Leaf $expandedPath) -ieq 'php.exe') {
            $expandedPath
        } else {
            Join-Path $expandedPath 'php.exe'
        }

        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }

    $command = Get-Command php -ErrorAction SilentlyContinue

    if ($command) {
        return $command.Source
    }

    return ''
}

function Get-ComposerGlobalBin {
    return Join-Path $env:APPDATA 'Composer\vendor\bin'
}

function Get-ValetComposerHome {
    return Join-Path $env:APPDATA 'Composer\mbs-valet'
}

function Get-ComposerBinFromHome {
    param([string] $ComposerHome)

    return Join-Path $ComposerHome 'vendor\bin'
}

function Invoke-ExternalCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string] $FilePath,

        [Parameter(Mandatory = $true)]
        [string[]] $Arguments,

        [Parameter(Mandatory = $true)]
        [string] $Description
    )

    Write-Step $Description
    & $FilePath @Arguments

    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE."
    }
}

function Invoke-WingetInstall {
    param(
        [Parameter(Mandatory = $true)]
        [string] $PackageId,

        [Parameter(Mandatory = $true)]
        [string] $Description,

        [switch] $Upgrade
    )

    $verb = if ($Upgrade) { 'upgrade' } else { 'install' }

    $baseArguments = @(
        $verb,
        '--id', $PackageId,
        '--exact',
        '--source', 'winget',
        '--accept-package-agreements',
        '--accept-source-agreements'
    )

    Write-Step $Description

    if ($Upgrade) {
        & winget @baseArguments

        if ($LASTEXITCODE -eq 0) {
            Refresh-ProcessPath
            return $true
        }

        # winget returns a non-zero code when nothing newer is available; that is not a failure.
        return $false
    }

    # This installer always runs elevated. Installing machine-wide writes to admin-owned
    # locations (Program Files\WinGet) and avoids the portable-package "Access is denied"
    # (0x80070005) failure that happens with user-scope portable installs under elevation.
    & winget @($baseArguments + @('--scope', 'machine'))

    if ($LASTEXITCODE -eq 0) {
        Refresh-ProcessPath
        return $true
    }

    $machineExitCode = $LASTEXITCODE
    Write-SoftWarning "$Description did not complete with machine scope (exit code $machineExitCode). Retrying with the default scope."

    & winget @baseArguments

    if ($LASTEXITCODE -eq 0) {
        Refresh-ProcessPath
        return $true
    }

    Write-SoftWarning "$Description failed with exit code $LASTEXITCODE."
    return $false
}

function Install-PhpIfRequested {
    if (-not [string]::IsNullOrWhiteSpace($PhpDirectory)) {
        $phpExecutable = Get-PhpExecutable

        if ([string]::IsNullOrWhiteSpace($phpExecutable)) {
            Write-SoftWarning "Could not find php.exe in the selected PHP path: $PhpDirectory"
            return
        }

        Add-PathEntry -Directory (Split-Path -Path $phpExecutable -Parent)
        Write-Step "Using PHP: $phpExecutable"
        return
    }

    if (-not $InstallPhp) {
        return
    }

    $phpPackageId = Get-PhpWingetPackageId
    $phpAvailable = $null -ne (Get-Command php -ErrorAction SilentlyContinue)

    if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
        if ($phpAvailable) {
            Write-Step 'PHP is already available.'
        } else {
            Write-SoftWarning 'winget is not available, so PHP could not be installed automatically.'
        }
        return
    }

    if ($phpAvailable) {
        if ($UpdateTools) {
            [void](Invoke-WingetInstall -PackageId $phpPackageId -Description "Updating PHP $PhpVersion through winget." -Upgrade)
        } else {
            Write-Step 'PHP is already available. Skipping PHP install.'
        }
        return
    }

    if (-not (Invoke-WingetInstall -PackageId $phpPackageId -Description "Installing PHP $PhpVersion through winget.")) {
        Write-SoftWarning "Automatic PHP install failed. Re-run setup and choose 'use an existing PHP folder', or install PHP manually, then run setup again."
    }
}

function Install-ComposerIfRequested {
    if (-not $InstallComposer) {
        return
    }

    if (Get-Command composer -ErrorAction SilentlyContinue) {
        if ($UpdateTools) {
            try {
                $composer = Get-Command composer -ErrorAction Stop
                Invoke-ExternalCommand -FilePath $composer.Source -Arguments @('self-update') -Description 'Updating Composer.'
            } catch {
                Write-SoftWarning "Could not update Composer automatically. $($_.Exception.Message)"
            }
        } else {
            Write-Step 'Composer is already available.'
        }
        Add-PathEntry -Directory (Get-ComposerGlobalBin)
        return
    }

    $phpExecutable = Get-PhpExecutable

    if ([string]::IsNullOrWhiteSpace($phpExecutable)) {
        Write-SoftWarning 'Composer needs PHP first. Install PHP or select an existing PHP directory, then run this installer again.'
        return
    }

    $setupPath = Join-Path $env:TEMP 'Composer-Setup.exe'

    try {
        Write-Step 'Downloading Composer setup.'
        Invoke-WebRequest -Uri 'https://getcomposer.org/Composer-Setup.exe' -OutFile $setupPath -UseBasicParsing

        $setupArguments = @(
            '/VERYSILENT',
            '/SUPPRESSMSGBOXES',
            '/NORESTART'
        )

        if ($InstallScope -eq 'AllUsers') {
            $setupArguments += '/ALLUSERS'
        } else {
            $setupArguments += '/CURRENTUSER'
        }

        $setupArguments += "/PHP=$phpExecutable"

        $process = Start-Process -FilePath $setupPath -ArgumentList $setupArguments -Wait -PassThru

        if ($process.ExitCode -ne 0) {
            throw "Composer setup failed with exit code $($process.ExitCode)."
        }

        Refresh-ProcessPath
        Add-PathEntry -Directory (Get-ComposerGlobalBin)

        if (Get-Command composer -ErrorAction SilentlyContinue) {
            Write-Step 'Composer installed.'
        } else {
            Write-SoftWarning 'Composer setup finished, but composer was not found in this process PATH yet. Open a new terminal and try again.'
        }
    } catch {
        Write-SoftWarning "Could not install Composer automatically. $($_.Exception.Message)"
    } finally {
        if (Test-Path -LiteralPath $setupPath) {
            Remove-Item -LiteralPath $setupPath -Force
        }
    }
}

function Install-ComposerGlobalPackage {
    param(
        [Parameter(Mandatory = $true)]
        [string] $PackageName,

        [Parameter(Mandatory = $true)]
        [string] $Label,

        [string] $ComposerHome = ''
    )

    $composer = Get-Command composer -ErrorAction SilentlyContinue

    if (-not $composer) {
        Write-SoftWarning "$Label needs Composer. Select Composer in the installer, or install Composer first."
        return $false
    }

    $previousComposerHome = $env:COMPOSER_HOME

    try {
        if (-not [string]::IsNullOrWhiteSpace($ComposerHome)) {
            if (-not (Test-Path -LiteralPath $ComposerHome)) {
                New-Item -ItemType Directory -Path $ComposerHome | Out-Null
            }

            $env:COMPOSER_HOME = $ComposerHome
        }

        Invoke-ExternalCommand -FilePath $composer.Source -Arguments @(
            'global',
            'require',
            $PackageName,
            '--with-all-dependencies',
            '--no-interaction'
        ) -Description "Installing $Label with Composer."
        $composerBin = if ([string]::IsNullOrWhiteSpace($ComposerHome)) {
            Get-ComposerGlobalBin
        } else {
            Get-ComposerBinFromHome -ComposerHome $ComposerHome
        }

        Add-PathEntry -Directory $composerBin
        Refresh-ProcessPath
        return $true
    } catch {
        Write-SoftWarning "Could not install $Label. $($_.Exception.Message)"
        return $false
    } finally {
        $env:COMPOSER_HOME = $previousComposerHome
    }
}

function Remove-ComposerGlobalPackageIfInstalled {
    param(
        [Parameter(Mandatory = $true)]
        [string] $PackageName,

        [Parameter(Mandatory = $true)]
        [string] $Label
    )

    $composer = Get-Command composer -ErrorAction SilentlyContinue

    if (-not $composer) {
        return
    }

    $previousErrorActionPreference = $ErrorActionPreference

    try {
        $ErrorActionPreference = 'Continue'
        & $composer.Source global show $PackageName --no-interaction --no-ansi 1>$null 2>$null
    } catch {
        return
    } finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    if ($LASTEXITCODE -ne 0) {
        return
    }

    try {
        Invoke-ExternalCommand -FilePath $composer.Source -Arguments @(
            'global',
            'remove',
            $PackageName,
            '--no-interaction'
        ) -Description "Removing conflicting Composer package $Label."
    } catch {
        Write-SoftWarning "Could not remove conflicting Composer package $Label. $($_.Exception.Message)"
    }
}

function Invoke-ValetInstall {
    param(
        [Parameter(Mandatory = $true)]
        [string] $ValetCommand
    )

    Write-Step 'Running valet install.'
    $output = @(& $ValetCommand install --no-ansi --no-interaction 2>&1)
    $exitCode = $LASTEXITCODE
    $outputText = $output -join [Environment]::NewLine
    $hasSuccessMarker = $outputText -match 'Valet installed and started successfully'
    $hasKnownReturnCodeIssue = $outputText -match 'must return an integer value' -and $outputText -match '"?null"? was returned'

    foreach ($entry in $output) {
        $line = [string] $entry

        if ($hasSuccessMarker -and ($line -match 'PHP Fatal error:' -or $line -match 'Stack trace:' -or $line -match 'InvokableCommand\.php' -or $line -match '^\s*#\d+ ' -or $line -match '^\s*thrown in ')) {
            continue
        }

        if (-not [string]::IsNullOrWhiteSpace($line)) {
            Write-Host $line
        }
    }

    if ($exitCode -eq 0) {
        return $true
    }

    if ($hasSuccessMarker -and $hasKnownReturnCodeIssue) {
        Write-SoftWarning 'Valet install completed, then returned a known Symfony console exit-code warning. Continuing because Valet reported success.'
        return $true
    }

    throw "Running valet install. failed with exit code $exitCode."
}

function Install-LaravelIfRequested {
    if (-not $InstallLaravel) {
        return
    }

    [void](Install-ComposerGlobalPackage -PackageName 'laravel/installer' -Label 'Laravel Installer')
}

function Install-ValetIfRequested {
    if (-not $InstallValet) {
        return
    }

    Write-Step 'Valet for Windows uses ycodetech/valet-windows because the original cretueusebiu package is blocked by Composer security policy.'
    Remove-ComposerGlobalPackageIfInstalled -PackageName 'cretueusebiu/valet-windows' -Label 'legacy Valet for Windows'

    $valetComposerHome = Get-ValetComposerHome
    Write-Step "Installing Valet in an isolated Composer home: $valetComposerHome"

    $installed = Install-ComposerGlobalPackage -PackageName 'ycodetech/valet-windows' -Label 'Valet for Windows' -ComposerHome $valetComposerHome

    if (-not $installed) {
        return
    }

    $valetBin = Get-ComposerBinFromHome -ComposerHome $valetComposerHome
    $valetCommand = Join-Path $valetBin 'valet.bat'

    if (-not (Test-Path -LiteralPath $valetCommand)) {
        $valet = Get-Command valet -ErrorAction SilentlyContinue

        if ($valet) {
            $valetCommand = $valet.Source
        }
    }

    if (-not (Test-Path -LiteralPath $valetCommand)) {
        Write-SoftWarning "Valet was installed, but valet.bat was not found in $valetBin. Open a new terminal and run: valet install"
        return
    }

    try {
        [void](Invoke-ValetInstall -ValetCommand $valetCommand)
    } catch {
        Write-SoftWarning "Valet package installed, but valet install did not complete. $($_.Exception.Message)"
    }
}

function Install-PintIfRequested {
    if (-not $InstallPint) {
        return
    }

    [void](Install-ComposerGlobalPackage -PackageName 'laravel/pint' -Label 'Laravel Pint')
}

function Install-EnvoyIfRequested {
    if (-not $InstallEnvoy) {
        return
    }

    [void](Install-ComposerGlobalPackage -PackageName 'laravel/envoy' -Label 'Laravel Envoy')
}

function Install-VaporIfRequested {
    if (-not $InstallVapor) {
        return
    }

    [void](Install-ComposerGlobalPackage -PackageName 'laravel/vapor-cli' -Label 'Laravel Vapor CLI')
}

function Install-WingetPackageIfRequested {
    param(
        [bool] $Requested,
        [string] $PackageId,
        [string] $Label,
        [string] $CommandName = '',
        [bool] $ApplyScope = $true
    )

    if (-not $Requested) {
        return
    }

    $available = $false

    if (-not [string]::IsNullOrWhiteSpace($CommandName)) {
        $available = $null -ne (Get-Command $CommandName -ErrorAction SilentlyContinue)
    }

    if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
        if ($available) {
            Write-Step "$Label is already available."
        } else {
            Write-SoftWarning "winget is not available, so $Label could not be installed automatically."
        }
        return
    }

    if ($available) {
        if ($UpdateTools) {
            [void](Invoke-WingetInstall -PackageId $PackageId -Description "Updating $Label." -Upgrade)
        } else {
            Write-Step "$Label is already available."
        }
        return
    }

    [void](Invoke-WingetInstall -PackageId $PackageId -Description "Installing $Label.")
}

function Install-GitIfRequested {
    Install-WingetPackageIfRequested -Requested $InstallGit -PackageId 'Git.Git' -Label 'Git' -CommandName 'git'
}

function Install-NodeIfRequested {
    if ($InstallNvm) {
        Install-WingetPackageIfRequested -Requested $true -PackageId 'CoreyButler.NVMforWindows' -Label 'nvm-windows'
        Write-SoftWarning 'nvm-windows installed. Open a new terminal and run: nvm install lts && nvm use lts'
        return
    }

    Install-WingetPackageIfRequested -Requested $InstallNode -PackageId 'OpenJS.NodeJS.LTS' -Label 'Node.js LTS' -CommandName 'node'
}

function Install-GhCliIfRequested {
    Install-WingetPackageIfRequested -Requested $InstallGhCli -PackageId 'GitHub.cli' -Label 'GitHub CLI' -CommandName 'gh'
}

function Install-PestIfRequested {
    if (-not $InstallPest) {
        return
    }

    [void](Install-ComposerGlobalPackage -PackageName 'pestphp/pest' -Label 'Pest PHP')
}

function Install-LarastanIfRequested {
    if (-not $InstallLarastan) {
        return
    }

    [void](Install-ComposerGlobalPackage -PackageName 'larastan/larastan' -Label 'Larastan')
}

function Install-RectorIfRequested {
    if (-not $InstallRector) {
        return
    }

    [void](Install-ComposerGlobalPackage -PackageName 'rector/rector' -Label 'Rector')
}

function Install-RayIfRequested {
    if (-not $InstallRay) {
        return
    }

    [void](Install-ComposerGlobalPackage -PackageName 'spatie/ray' -Label 'Ray (spatie/ray)')
}

function Install-MkcertIfRequested {
    if (-not $InstallMkcert) {
        return
    }

    Install-WingetPackageIfRequested -Requested $true -PackageId 'FiloSottile.mkcert' -Label 'mkcert' -CommandName 'mkcert'

    $mkcert = Get-Command mkcert -ErrorAction SilentlyContinue

    if (-not $mkcert) {
        Write-SoftWarning 'mkcert was not found after install. Open a new terminal and run: mkcert -install'
        return
    }

    try {
        Invoke-ExternalCommand -FilePath $mkcert.Source -Arguments @('-install') -Description 'Trusting mkcert local certificate authority.'
    } catch {
        Write-SoftWarning "mkcert was installed but could not trust the local CA. Run manually: mkcert -install. $($_.Exception.Message)"
    }
}

function Install-RedisIfRequested {
    Install-WingetPackageIfRequested -Requested $InstallRedis -PackageId 'Memurai.Memurai' -Label 'Memurai (Redis-compatible)' -ApplyScope $false
}

function Install-DockerIfRequested {
    if (-not $InstallDocker) {
        return
    }

    Write-SoftWarning 'Docker Desktop requires WSL2 or Hyper-V and a system restart. Ensure these prerequisites are enabled.'
    Install-WingetPackageIfRequested -Requested $true -PackageId 'Docker.DockerDesktop' -Label 'Docker Desktop' -CommandName 'docker' -ApplyScope $false
}

function Install-TablePlusIfRequested {
    Install-WingetPackageIfRequested -Requested $InstallTablePlus -PackageId 'TablePlus.TablePlus' -Label 'TablePlus' -ApplyScope $false
}

function Install-FzfIfRequested {
    Install-WingetPackageIfRequested -Requested $InstallFzf -PackageId 'junegunn.fzf' -Label 'fzf' -CommandName 'fzf'
}

function Install-BatIfRequested {
    Install-WingetPackageIfRequested -Requested $InstallBat -PackageId 'sharkdp.bat' -Label 'bat' -CommandName 'bat'
}

function Install-RipgrepIfRequested {
    Install-WingetPackageIfRequested -Requested $InstallRipgrep -PackageId 'BurntSushi.ripgrep.MSVC' -Label 'ripgrep' -CommandName 'rg'
}

function Install-LazygitIfRequested {
    Install-WingetPackageIfRequested -Requested $InstallLazygit -PackageId 'JesseDuffield.lazygit' -Label 'lazygit' -CommandName 'lazygit'
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
        [string] $StartPath,

        [Parameter(Mandatory = $true)]
        [string] $DisplayName
    )

    $targetHelper = Join-Path $HOME '.config\powershell\laravel-dev.ps1'
    Copy-FileEnsuringDirectory -Source $SourceProfile -Destination $targetHelper

    $escapedStartPath = $StartPath.Replace("'", "''")
    $escapedDisplayName = $DisplayName.Replace("'", "''")
    $helperContent = Get-Content -LiteralPath $targetHelper -Raw
    $helperContent = $helperContent -replace "\`$script:MbsStartPath = '.*?'", "`$script:MbsStartPath = '$escapedStartPath'"
    $helperContent = $helperContent -replace "\`$script:MbsDisplayName = '.*?'", "`$script:MbsDisplayName = '$escapedDisplayName'"
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

    $settingsPath = Resolve-WindowsTerminalSettingsPath

    if (-not $settingsPath) {
        Write-SoftWarning 'Windows Terminal settings.json was not found. Install Windows Terminal first, then run this installer again.'
        return
    }

    $settingsDirectory = Split-Path -Path $settingsPath -Parent

    if (-not (Test-Path -LiteralPath $settingsDirectory)) {
        New-Item -ItemType Directory -Path $settingsDirectory | Out-Null
    }

    $backupPath = ''

    if (Test-Path -LiteralPath $settingsPath) {
        $backupPath = "$settingsPath.bak-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
        Copy-Item -LiteralPath $settingsPath -Destination $backupPath -Force
    }

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

    if ([string]::IsNullOrWhiteSpace($backupPath)) {
        Write-Step "Windows Terminal settings installed: $settingsPath"
    } else {
        Write-Step "Windows Terminal settings installed. Backup: $backupPath"
    }
}

function Resolve-WindowsTerminalSettingsPath {
    $stablePackageRoot = Join-Path $env:LOCALAPPDATA 'Packages\Microsoft.WindowsTerminal_8wekyb3d8bbwe'
    $previewPackageRoot = Join-Path $env:LOCALAPPDATA 'Packages\Microsoft.WindowsTerminalPreview_8wekyb3d8bbwe'
    $unpackagedRoot = Join-Path $env:LOCALAPPDATA 'Microsoft\Windows Terminal'

    $settingsCandidates = @(
        (Join-Path $stablePackageRoot 'LocalState\settings.json'),
        (Join-Path $previewPackageRoot 'LocalState\settings.json'),
        (Join-Path $unpackagedRoot 'settings.json')
    )

    $existingSettingsPath = $settingsCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1

    if ($existingSettingsPath) {
        return $existingSettingsPath
    }

    if (Test-Path -LiteralPath $stablePackageRoot) {
        return Join-Path $stablePackageRoot 'LocalState\settings.json'
    }

    if (Test-Path -LiteralPath $previewPackageRoot) {
        return Join-Path $previewPackageRoot 'LocalState\settings.json'
    }

    if (Get-Command wt -ErrorAction SilentlyContinue) {
        return Join-Path $stablePackageRoot 'LocalState\settings.json'
    }

    if (Test-Path -LiteralPath $unpackagedRoot) {
        return Join-Path $unpackagedRoot 'settings.json'
    }

    return ''
}

$repositoryRoot = Get-RepositoryRoot

if ([string]::IsNullOrWhiteSpace($StartingDirectory)) {
    $StartingDirectory = $HOME
}

$DisplayName = Resolve-DisplayName -Value $DisplayName

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
$starshipTarget = Join-Path $HOME '.config\starship.toml'
Copy-FileEnsuringDirectory -Source $starshipSource -Destination $starshipTarget
$starshipContent = [System.IO.File]::ReadAllText($starshipTarget, [System.Text.Encoding]::UTF8)
$starshipContent = $starshipContent.Replace('__MBS_DISPLAY_NAME__', $DisplayName)
[System.IO.File]::WriteAllText($starshipTarget, $starshipContent, [System.Text.UTF8Encoding]::new($false))

Write-Step 'Installing PowerShell helper profile.'
Install-PowerShellProfile -SourceProfile $profileSource -TargetProfile $PROFILE.CurrentUserCurrentHost -StartPath $StartingDirectory -DisplayName $DisplayName

Write-Step 'Installing Windows Terminal settings.'
Install-WindowsTerminalSettings -TemplatePath $terminalSource -IconsDirectory $iconsTarget -StartingDirectory $StartingDirectory

if ($InstallDependencies) {
    Install-StarshipIfRequested
}

Install-PhpIfRequested
Install-ComposerIfRequested
Install-LaravelIfRequested
Install-ValetIfRequested
Install-PintIfRequested
Install-EnvoyIfRequested
Install-VaporIfRequested
Install-GitIfRequested
Install-NodeIfRequested
Install-GhCliIfRequested
Install-PestIfRequested
Install-LarastanIfRequested
Install-RectorIfRequested
Install-RayIfRequested
Install-MkcertIfRequested
Install-RedisIfRequested
Install-DockerIfRequested
Install-TablePlusIfRequested
Install-FzfIfRequested
Install-BatIfRequested
Install-RipgrepIfRequested
Install-LazygitIfRequested
Install-PSReadLineIfPossible

Write-Step 'Done. Open a new Windows Terminal tab to see the setup.'
