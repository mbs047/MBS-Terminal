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

if (($InstallScope -eq 'AllUsers') -and (-not (Test-IsAdministrator))) {
    Write-SoftWarning 'An all-users install needs administrator rights. Continuing as a current-user install; PATH changes will apply to your account only.'
    $InstallScope = 'CurrentUser'
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

function Get-MbsProfilePathFile {
    return Join-Path $HOME '.config\powershell\mbs-terminal-paths.txt'
}

function Get-PathEntries {
    param([string[]] $PathValues)

    $entries = New-Object System.Collections.Generic.List[string]

    foreach ($pathValue in $PathValues) {
        if ([string]::IsNullOrWhiteSpace($pathValue)) {
            continue
        }

        foreach ($entry in ($pathValue -split ';')) {
            if ([string]::IsNullOrWhiteSpace($entry)) {
                continue
            }

            $hasEntry = $entries | Where-Object { $_.TrimEnd('\') -ieq $entry.TrimEnd('\') } | Select-Object -First 1

            if (-not $hasEntry) {
                [void] $entries.Add($entry)
            }
        }
    }

    return $entries.ToArray()
}

function Add-MbsProfilePathEntry {
    param([string] $ResolvedDirectory)

    try {
        $pathFile = Get-MbsProfilePathFile
        $pathDirectory = Split-Path -Path $pathFile -Parent

        if (-not (Test-Path -LiteralPath $pathDirectory)) {
            New-Item -ItemType Directory -Path $pathDirectory | Out-Null
        }

        $entries = @()

        if (Test-Path -LiteralPath $pathFile) {
            $entries = Get-Content -LiteralPath $pathFile | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
        }

        $hasEntry = $entries | Where-Object { $_.TrimEnd('\') -ieq $ResolvedDirectory.TrimEnd('\') } | Select-Object -First 1

        if (-not $hasEntry) {
            $entries = @($entries) + $ResolvedDirectory
            Set-Content -LiteralPath $pathFile -Value ($entries -join [Environment]::NewLine) -Encoding ASCII
            Write-Step "Added to MBS Terminal profile PATH fallback: $ResolvedDirectory"
        }

        return $true
    } catch {
        Write-SoftWarning "Could not update the MBS Terminal profile PATH fallback. $($_.Exception.Message)"
        return $false
    }
}

function Set-PersistentPathVariable {
    param(
        [Parameter(Mandatory = $true)]
        [string] $PathValue,

        [Parameter(Mandatory = $true)]
        [ValidateSet('User', 'Machine')]
        [string] $Target
    )

    try {
        [Environment]::SetEnvironmentVariable('Path', $PathValue, $Target)
        return
    } catch {
        if ($Target -ne 'User') {
            throw
        }
    }

    $environmentKey = $null

    try {
        $environmentKey = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Environment', $true)

        if (-not $environmentKey) {
            $environmentKey = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey('Environment')
        }

        $environmentKey.SetValue('Path', $PathValue, [Microsoft.Win32.RegistryValueKind]::ExpandString)
    } finally {
        if ($environmentKey) {
            $environmentKey.Dispose()
        }
    }
}

function Add-ProcessPathEntry {
    param([string] $ResolvedDirectory)

    $processEntries = Get-PathEntries -PathValues @($env:Path)
    $hasProcessEntry = $processEntries | Where-Object { $_.TrimEnd('\') -ieq $ResolvedDirectory.TrimEnd('\') } | Select-Object -First 1

    if (-not $hasProcessEntry) {
        $env:Path = ($(@($ResolvedDirectory) + $processEntries) -join ';')
        Write-Step "Added to current installer PATH: $ResolvedDirectory"
    }
}

function Add-PersistentPathEntry {
    param(
        [Parameter(Mandatory = $true)]
        [string] $ResolvedDirectory,

        [Parameter(Mandatory = $true)]
        [ValidateSet('User', 'Machine')]
        [string] $Target
    )

    $entries = Get-PathEntries -PathValues @([Environment]::GetEnvironmentVariable('Path', $Target))
    $hasEntry = $entries | Where-Object { $_.TrimEnd('\') -ieq $ResolvedDirectory.TrimEnd('\') } | Select-Object -First 1

    if (-not $hasEntry) {
        $entries = @($entries) + $ResolvedDirectory
        Set-PersistentPathVariable -PathValue ($entries -join ';') -Target $Target
        Write-Step "Added to $Target PATH: $ResolvedDirectory"
    }

    return $true
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

    Add-ProcessPathEntry -ResolvedDirectory $resolvedDirectory

    $target = Get-EnvironmentTarget
    $targets = @($target)

    if ($target -eq 'Machine') {
        $targets += 'User'
    }

    foreach ($pathTarget in $targets) {
        if ($pathTarget -eq 'Machine' -and -not (Test-IsAdministrator)) {
            Write-SoftWarning "Administrator rights are required to add machine PATH entries. Falling back to user PATH for: $resolvedDirectory"
            continue
        }

        try {
            if (Add-PersistentPathEntry -ResolvedDirectory $resolvedDirectory -Target $pathTarget) {
                return
            }
        } catch {
            if ($pathTarget -eq 'Machine') {
                Write-SoftWarning "Could not add to machine PATH. Falling back to user PATH for: $resolvedDirectory. $($_.Exception.Message)"
                continue
            }

            if (Add-MbsProfilePathEntry -ResolvedDirectory $resolvedDirectory) {
                Write-Step "Windows user PATH is locked; MBS Terminal will load this path from the PowerShell profile: $resolvedDirectory"
                return
            }

            Write-SoftWarning "Could not save PATH permanently. This installer can still use: $resolvedDirectory. $($_.Exception.Message)"
            return
        }
    }
}

function Refresh-ProcessPath {
    $machinePath = [Environment]::GetEnvironmentVariable('Path', 'Machine')
    $userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
    $env:Path = (Get-PathEntries -PathValues @($env:Path, $machinePath, $userPath)) -join ';'
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

function Get-PhpFallbackArchitecture {
    if ([Environment]::Is64BitOperatingSystem) {
        return 'x64'
    }

    return 'x86'
}

function Get-PhpVisualStudioRuntime {
    switch ($PhpVersion) {
        '8.2' { return 'vs16' }
        '8.3' { return 'vs16' }
        default { return 'vs17' }
    }
}

function Get-PhpFallbackDownloadCandidates {
    $runtime = Get-PhpVisualStudioRuntime
    $architecture = Get-PhpFallbackArchitecture
    $candidates = @(
        [pscustomobject]@{
            Url          = "https://windows.php.net/downloads/releases/latest/php-$PhpVersion-Win32-$runtime-$architecture-latest.zip"
            Architecture = $architecture
        }
    )

    if ($architecture -eq 'x64') {
        $candidates += [pscustomobject]@{
            Url          = "https://windows.php.net/downloads/releases/latest/php-$PhpVersion-Win32-$runtime-x86-latest.zip"
            Architecture = 'x86'
        }
    }

    return $candidates
}

function Get-PhpFallbackInstallDirectory {
    $baseDirectory = ''

    if ($InstallScope -eq 'AllUsers' -and (Test-IsAdministrator) -and -not [string]::IsNullOrWhiteSpace($env:ProgramData)) {
        $baseDirectory = Join-Path $env:ProgramData 'MBS-Terminal\PHP'
    } else {
        $localAppData = $env:LOCALAPPDATA

        if ([string]::IsNullOrWhiteSpace($localAppData)) {
            $localAppData = Join-Path $HOME 'AppData\Local'
        }

        $baseDirectory = Join-Path $localAppData 'MBS-Terminal\PHP'
    }

    return Join-Path $baseDirectory $PhpVersion
}

function Initialize-PhpIni {
    param([string] $InstallDirectory)

    $iniPath = Join-Path $InstallDirectory 'php.ini'

    if (-not (Test-Path -LiteralPath $iniPath)) {
        $templatePath = Join-Path $InstallDirectory 'php.ini-production'

        if (-not (Test-Path -LiteralPath $templatePath)) {
            $templatePath = Join-Path $InstallDirectory 'php.ini-development'
        }

        if (Test-Path -LiteralPath $templatePath) {
            Copy-Item -LiteralPath $templatePath -Destination $iniPath -Force
        }
    }

    if (-not (Test-Path -LiteralPath $iniPath)) {
        return
    }

    $content = Get-Content -LiteralPath $iniPath -Raw
    $content = [regex]::Replace($content, '(?m)^\s*;?\s*extension_dir\s*=\s*"?ext"?\s*$', 'extension_dir = "ext"')

    foreach ($extension in @('curl', 'fileinfo', 'mbstring', 'openssl', 'pdo_mysql', 'pdo_sqlite', 'sqlite3', 'zip')) {
        $escapedExtension = [regex]::Escape($extension)
        $content = [regex]::Replace($content, "(?m)^\s*;\s*extension\s*=\s*$escapedExtension\s*$", "extension=$extension")
    }

    Set-Content -LiteralPath $iniPath -Value $content -Encoding ASCII
}

function Test-PhpExecutable {
    param([string] $PhpExecutable)

    if ([string]::IsNullOrWhiteSpace($PhpExecutable) -or -not (Test-Path -LiteralPath $PhpExecutable)) {
        return $false
    }

    try {
        $output = @(& $PhpExecutable --version 2>&1)
        $exitCode = $LASTEXITCODE

        if ($exitCode -eq 0) {
            $firstLine = $output | Select-Object -First 1

            if (-not [string]::IsNullOrWhiteSpace($firstLine)) {
                Write-Step $firstLine
            }

            return $true
        }

        Write-SoftWarning "php.exe was found but did not run correctly (exit code $exitCode)."
    } catch {
        Write-SoftWarning "php.exe was found but could not start. $($_.Exception.Message)"
    }

    return $false
}

function Install-VisualCRuntimeForPhpIfPossible {
    param([string] $Architecture = '')

    $architecture = $Architecture

    if ([string]::IsNullOrWhiteSpace($architecture)) {
        $architecture = Get-PhpFallbackArchitecture
    }

    $packageId = "Microsoft.VCRedist.2015+.$architecture"

    if (Get-Command winget -ErrorAction SilentlyContinue) {
        if (Invoke-WingetInstall -PackageId $packageId -Description 'Installing Microsoft Visual C++ Redistributable for PHP.') {
            return
        }

        Write-SoftWarning 'winget could not install the Microsoft Visual C++ Redistributable. Trying the Microsoft download backup.'
    }

    $runtimeUrl = if ($architecture -eq 'x64') {
        'https://aka.ms/vs/17/release/vc_redist.x64.exe'
    } else {
        'https://aka.ms/vs/17/release/vc_redist.x86.exe'
    }

    $runtimeInstaller = Join-Path $env:TEMP "vc_redist.$architecture.exe"

    try {
        Write-Step 'Downloading Microsoft Visual C++ Redistributable for PHP.'
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri $runtimeUrl -OutFile $runtimeInstaller -UseBasicParsing

        $process = if (Test-IsAdministrator) {
            Start-Process -FilePath $runtimeInstaller -ArgumentList @('/install', '/quiet', '/norestart') -Wait -PassThru
        } else {
            Write-SoftWarning 'PHP may need the Microsoft Visual C++ Redistributable. Approve the Microsoft installer if Windows asks.'
            Start-Process -FilePath $runtimeInstaller -ArgumentList @('/install', '/quiet', '/norestart') -Verb RunAs -Wait -PassThru
        }

        if ($process.ExitCode -eq 0 -or $process.ExitCode -eq 3010) {
            Write-Step 'Microsoft Visual C++ Redistributable installed.'
        } else {
            Write-SoftWarning "Microsoft Visual C++ Redistributable installer exited with code $($process.ExitCode)."
        }
    } catch {
        Write-SoftWarning "Could not install Microsoft Visual C++ Redistributable automatically. $($_.Exception.Message)"
    } finally {
        if (Test-Path -LiteralPath $runtimeInstaller) {
            Remove-Item -LiteralPath $runtimeInstaller -Force
        }
    }
}

function Resolve-ExtractedPhpDirectory {
    param([string] $ExtractRoot)

    $rootPhp = Join-Path $ExtractRoot 'php.exe'

    if (Test-Path -LiteralPath $rootPhp) {
        return $ExtractRoot
    }

    $phpExecutable = Get-ChildItem -LiteralPath $ExtractRoot -Recurse -Filter 'php.exe' -File |
        Select-Object -First 1

    if ($phpExecutable) {
        return $phpExecutable.Directory.FullName
    }

    return ''
}

function Install-PhpFromOfficialZip {
    $installDirectory = Get-PhpFallbackInstallDirectory
    $phpExecutable = Join-Path $installDirectory 'php.exe'

    if ((-not $UpdateTools) -and (Test-PhpExecutable -PhpExecutable $phpExecutable)) {
        Add-PathEntry -Directory $installDirectory
        Write-Step "Using PHP from backup install: $installDirectory"
        return $true
    }

    $downloadPath = Join-Path $env:TEMP "mbs-terminal-php-$PhpVersion.zip"
    $extractRoot = Join-Path $env:TEMP ("MBS-Terminal-PHP-" + [Guid]::NewGuid().ToString('N'))
    $backupDirectory = ''

    foreach ($downloadCandidate in (Get-PhpFallbackDownloadCandidates)) {
        $downloadUrl = $downloadCandidate.Url
        $downloadArchitecture = $downloadCandidate.Architecture

        try {
            Write-Step "Installing PHP $PhpVersion from the official PHP zip backup."
            Write-Step "Downloading $downloadUrl"
            [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

            if (Test-Path -LiteralPath $downloadPath) {
                Remove-Item -LiteralPath $downloadPath -Force
            }

            if (Test-Path -LiteralPath $extractRoot) {
                Remove-Item -LiteralPath $extractRoot -Recurse -Force
            }

            Invoke-WebRequest -Uri $downloadUrl -OutFile $downloadPath -UseBasicParsing
            Expand-Archive -LiteralPath $downloadPath -DestinationPath $extractRoot -Force

            $extractedDirectory = Resolve-ExtractedPhpDirectory -ExtractRoot $extractRoot

            if ([string]::IsNullOrWhiteSpace($extractedDirectory)) {
                throw 'The PHP zip did not contain php.exe.'
            }

            $parentDirectory = Split-Path -Path $installDirectory -Parent

            if (-not (Test-Path -LiteralPath $parentDirectory)) {
                New-Item -ItemType Directory -Path $parentDirectory | Out-Null
            }

            if (Test-Path -LiteralPath $installDirectory) {
                $backupDirectory = "$installDirectory.bak-$(Get-Date -Format 'yyyyMMddHHmmss')"
                Move-Item -LiteralPath $installDirectory -Destination $backupDirectory
                Write-Step "Backed up existing PHP folder: $backupDirectory"
            }

            Move-Item -LiteralPath $extractedDirectory -Destination $installDirectory
            Initialize-PhpIni -InstallDirectory $installDirectory
            Add-PathEntry -Directory $installDirectory
            Refresh-ProcessPath

            if (-not (Test-PhpExecutable -PhpExecutable $phpExecutable)) {
                Install-VisualCRuntimeForPhpIfPossible -Architecture $downloadArchitecture
                Refresh-ProcessPath
            }

            if (Test-PhpExecutable -PhpExecutable $phpExecutable) {
                Write-Step "PHP installed from official backup: $installDirectory"
                return $true
            }

            throw 'PHP was extracted, but php.exe did not pass verification.'
        } catch {
            Write-SoftWarning "Official PHP zip backup failed. $($_.Exception.Message)"

            if (-not [string]::IsNullOrWhiteSpace($backupDirectory) -and
                (Test-Path -LiteralPath $backupDirectory) -and
                (-not (Test-Path -LiteralPath $installDirectory))) {
                Move-Item -LiteralPath $backupDirectory -Destination $installDirectory
                Write-Step "Restored previous PHP folder: $installDirectory"
            }
        } finally {
            if (Test-Path -LiteralPath $downloadPath) {
                Remove-Item -LiteralPath $downloadPath -Force
            }

            if (Test-Path -LiteralPath $extractRoot) {
                Remove-Item -LiteralPath $extractRoot -Recurse -Force
            }
        }
    }

    return $false
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

function Get-ComposerBackupInstallDirectory {
    $baseDirectory = ''

    if ($InstallScope -eq 'AllUsers' -and (Test-IsAdministrator) -and -not [string]::IsNullOrWhiteSpace($env:ProgramData)) {
        $baseDirectory = Join-Path $env:ProgramData 'MBS-Terminal\Composer'
    } else {
        $localAppData = $env:LOCALAPPDATA

        if ([string]::IsNullOrWhiteSpace($localAppData)) {
            $localAppData = Join-Path $HOME 'AppData\Local'
        }

        $baseDirectory = Join-Path $localAppData 'MBS-Terminal\Composer'
    }

    return $baseDirectory
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

    $arguments = @(
        $verb,
        '--id', $PackageId,
        '--exact',
        '--source', 'winget',
        '--accept-package-agreements',
        '--accept-source-agreements'
    )

    # Only request machine scope for an all-users install. For a per-user install we let
    # winget use its default (user) scope, which lets it install portable packages such as
    # PHP without elevation. winget prompts for elevation itself for packages that need it.
    if ((-not $Upgrade) -and ($InstallScope -eq 'AllUsers')) {
        $arguments += @('--scope', 'machine')
    }

    Write-Step $Description
    & winget @arguments

    if ($LASTEXITCODE -eq 0) {
        Refresh-ProcessPath
        return $true
    }

    if (-not $Upgrade) {
        # winget returns a non-zero code when nothing newer is available during an upgrade,
        # so only treat a fresh install failure as a real problem.
        Write-SoftWarning "$Description failed with exit code $LASTEXITCODE."
    }

    return $false
}

function Test-ComposerCommand {
    param([string] $ComposerCommand = '')

    if ([string]::IsNullOrWhiteSpace($ComposerCommand)) {
        $command = Get-Command composer -ErrorAction SilentlyContinue

        if (-not $command) {
            return $false
        }

        $ComposerCommand = $command.Source
    }

    if (-not (Test-Path -LiteralPath $ComposerCommand)) {
        return $false
    }

    $previousErrorActionPreference = $ErrorActionPreference

    try {
        $ErrorActionPreference = 'Continue'
        $output = @(& $ComposerCommand --version --no-ansi 2>&1)
        $exitCode = $LASTEXITCODE

        if ($exitCode -eq 0) {
            $firstLine = $output | Select-Object -First 1

            if (-not [string]::IsNullOrWhiteSpace($firstLine)) {
                Write-Step $firstLine
            }

            return $true
        }

        Write-SoftWarning "composer was found but did not run correctly (exit code $exitCode)."
    } catch {
        Write-SoftWarning "composer was found but could not start. $($_.Exception.Message)"
    } finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    return $false
}

function New-ComposerCommandWrappers {
    param(
        [Parameter(Mandatory = $true)]
        [string] $InstallDirectory,

        [Parameter(Mandatory = $true)]
        [string] $PhpExecutable
    )

    $batchPath = Join-Path $InstallDirectory 'composer.bat'
    $powershellPath = Join-Path $InstallDirectory 'composer.ps1'
    $escapedPhpForPowerShell = $PhpExecutable.Replace("'", "''")

    $batchContent = @(
        '@ECHO OFF',
        'SETLOCAL',
        ('CALL "{0}" "%~dp0composer.phar" %*' -f $PhpExecutable),
        'EXIT /B %ERRORLEVEL%'
    )

    $powershellContent = @(
        ('$php = ''{0}''' -f $escapedPhpForPowerShell),
        '$composer = Join-Path $PSScriptRoot ''composer.phar''',
        '& $php $composer @args',
        'exit $LASTEXITCODE'
    )

    Set-Content -LiteralPath $batchPath -Value ($batchContent -join [Environment]::NewLine) -Encoding ASCII
    Set-Content -LiteralPath $powershellPath -Value ($powershellContent -join [Environment]::NewLine) -Encoding ASCII
}

function Install-ComposerFromPhar {
    param([string] $PhpExecutable)

    if ([string]::IsNullOrWhiteSpace($PhpExecutable) -or -not (Test-Path -LiteralPath $PhpExecutable)) {
        Write-SoftWarning 'Composer phar backup needs a working PHP executable first.'
        return $false
    }

    $installDirectory = Get-ComposerBackupInstallDirectory
    $composerPhar = Join-Path $installDirectory 'composer.phar'
    $composerCommand = Join-Path $installDirectory 'composer.bat'

    try {
        if (-not (Test-Path -LiteralPath $installDirectory)) {
            New-Item -ItemType Directory -Path $installDirectory | Out-Null
        }

        Write-Step 'Installing Composer from composer.phar backup.'
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri 'https://getcomposer.org/download/latest-stable/composer.phar' -OutFile $composerPhar -UseBasicParsing
        New-ComposerCommandWrappers -InstallDirectory $installDirectory -PhpExecutable $PhpExecutable
        Add-PathEntry -Directory $installDirectory
        Refresh-ProcessPath

        if (Test-ComposerCommand -ComposerCommand $composerCommand) {
            Write-Step "Composer installed from phar backup: $installDirectory"
            return $true
        }

        return $false
    } catch {
        Write-SoftWarning "Composer phar backup failed. $($_.Exception.Message)"
        return $false
    }
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
            Write-SoftWarning 'winget is not available. Trying the official PHP zip backup.'
            if (-not (Install-PhpFromOfficialZip)) {
                Write-SoftWarning "Automatic PHP install failed. Re-run setup and choose 'use an existing PHP folder', or install PHP manually, then run setup again."
            }
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

    if (Invoke-WingetInstall -PackageId $phpPackageId -Description "Installing PHP $PhpVersion through winget.") {
        $phpExecutable = Get-PhpExecutable

        if (-not [string]::IsNullOrWhiteSpace($phpExecutable)) {
            Write-Step "PHP installed through winget: $phpExecutable"
            return
        }

        Write-SoftWarning 'winget finished, but php.exe was not found in PATH yet. Trying the official PHP zip backup.'
    } else {
        Write-SoftWarning 'Trying the official PHP zip backup.'
    }

    if (-not (Install-PhpFromOfficialZip)) {
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

    $composerSetupSucceeded = $false

    try {
        Write-Step 'Downloading Composer setup.'
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
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
            $composerSetupSucceeded = $true
        }
    } catch {
        Write-SoftWarning "Composer setup did not complete. $($_.Exception.Message)"
    } finally {
        if (Test-Path -LiteralPath $setupPath) {
            Remove-Item -LiteralPath $setupPath -Force
        }
    }

    if ($composerSetupSucceeded) {
        return
    }

    Write-SoftWarning 'Trying the Composer phar backup.'

    if (Install-ComposerFromPhar -PhpExecutable $phpExecutable) {
        return
    }

    Write-SoftWarning 'Could not install Composer automatically. Laravel Installer needs Composer first.'
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
                $profile.commandline = '%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe -NoLogo -ExecutionPolicy Bypass'
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
