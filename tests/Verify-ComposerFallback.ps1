[CmdletBinding()]
param(
    [ValidateSet('8.2', '8.3', '8.4', '8.5')]
    [string] $PhpVersion = '8.4'
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Path $PSScriptRoot -Parent
$installScript = Join-Path $repositoryRoot 'install.ps1'
$scriptText = Get-Content -LiteralPath $installScript -Raw
$tokens = $null
$parseErrors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseInput($scriptText, [ref] $tokens, [ref] $parseErrors)

if ($parseErrors.Count -gt 0) {
    throw ($parseErrors | ForEach-Object { $_.Message } | Out-String)
}

$functionAsts = $ast.FindAll({ param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] }, $true)

foreach ($functionAst in $functionAsts) {
    . ([scriptblock]::Create($functionAst.Extent.Text))
}

function Write-Step {
    param([string] $Message)

    Write-Host "[STEP] $Message"
}

function Write-SoftWarning {
    param([string] $Message)

    Write-Host "[WARN] $Message"
}

function winget {
    Write-Host '[MOCK-WINGET] winget command was discovered by the installer.'
}

function Invoke-WingetInstall {
    param(
        [string] $PackageId,
        [string] $Description,
        [switch] $Upgrade
    )

    Write-Host "[MOCK-WINGET] $Description -> failed"
    return $false
}

function Get-Command {
    [CmdletBinding()]
    param(
        [Parameter(Position = 0, ValueFromPipeline = $true)]
        [string[]] $Name
    )

    if ($Name.Count -eq 1 -and $Name[0] -ieq 'composer') {
        return $null
    }

    Microsoft.PowerShell.Core\Get-Command @PSBoundParameters
}

function Install-VisualCRuntimeForPhpIfPossible {
    param([string] $Architecture = '')

    Write-Host "[SKIP-VC] PHP verification test does not install VC++ runtime. Architecture: $Architecture"
}

function Start-Process {
    param(
        [string] $FilePath,
        [object] $ArgumentList,
        [switch] $Wait,
        [switch] $PassThru,
        [string] $Verb
    )

    Write-Host "[MOCK-START] $FilePath -> exit code 7"
    return [pscustomobject]@{ ExitCode = 7 }
}

$script:ProfilePathFile = ''

function Get-MbsProfilePathFile {
    return $script:ProfilePathFile
}

function Set-PersistentPathVariable {
    param(
        [Parameter(Mandatory = $true)]
        [string] $PathValue,

        [Parameter(Mandatory = $true)]
        [ValidateSet('User', 'Machine')]
        [string] $Target
    )

    throw [System.UnauthorizedAccessException]::new("Simulated blocked $Target PATH write.")
}

$script:InstallScope = 'CurrentUser'
$script:PhpVersion = $PhpVersion
$script:PhpDirectory = ''
$script:InstallPhp = $true
$script:InstallComposer = $true
$script:UpdateTools = $false

$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('MBS-Terminal-ComposerFallbackTest-' + [Guid]::NewGuid().ToString('N'))
$oldLocalAppData = $env:LOCALAPPDATA
$oldTemp = $env:TEMP
$oldAppData = $env:APPDATA
$oldPath = $env:Path

try {
    $env:LOCALAPPDATA = Join-Path $tempRoot 'LocalAppData'
    $env:TEMP = Join-Path $tempRoot 'Temp'
    $env:APPDATA = Join-Path $tempRoot 'AppData\Roaming'
    $env:Path = "$env:SystemRoot\System32;$env:SystemRoot\System32\WindowsPowerShell\v1.0"
    $script:ProfilePathFile = Join-Path $tempRoot 'mbs-terminal-paths.txt'
    New-Item -ItemType Directory -Path $env:LOCALAPPDATA, $env:TEMP, $env:APPDATA | Out-Null

    Install-PhpIfRequested
    Install-ComposerIfRequested

    $phpExecutable = Join-Path $env:LOCALAPPDATA "MBS-Terminal\PHP\$PhpVersion\php.exe"
    $composerDirectory = Join-Path $env:LOCALAPPDATA 'MBS-Terminal\Composer'
    $composerCommand = Join-Path $composerDirectory 'composer.bat'
    $composerPhar = Join-Path $composerDirectory 'composer.phar'

    if (-not (Test-Path -LiteralPath $phpExecutable)) {
        throw "Expected fallback php.exe was not installed: $phpExecutable"
    }

    if (-not (Test-Path -LiteralPath $composerCommand)) {
        throw "Expected composer wrapper was not created: $composerCommand"
    }

    if (-not (Test-Path -LiteralPath $composerPhar)) {
        throw "Expected composer.phar was not downloaded: $composerPhar"
    }

    $processPathEntries = Get-PathEntries -PathValues @($env:Path)
    $hasComposerPath = $processPathEntries | Where-Object { $_.TrimEnd('\') -ieq $composerDirectory.TrimEnd('\') } | Select-Object -First 1

    if (-not $hasComposerPath) {
        throw "Composer directory was not added to the installer process PATH: $composerDirectory"
    }

    if (-not (Test-Path -LiteralPath $script:ProfilePathFile)) {
        throw 'Profile PATH fallback file was not created.'
    }

    $profilePathEntries = Get-Content -LiteralPath $script:ProfilePathFile
    $hasComposerProfilePath = $profilePathEntries | Where-Object { $_.TrimEnd('\') -ieq $composerDirectory.TrimEnd('\') } | Select-Object -First 1

    if (-not $hasComposerProfilePath) {
        throw 'Profile PATH fallback file does not contain the Composer directory.'
    }

    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'

    try {
        $composerOutput = @(& $phpExecutable $composerPhar --version --no-ansi 2>&1)
    } finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    if ($LASTEXITCODE -ne 0) {
        throw "composer.phar --version failed with exit code $LASTEXITCODE. $($composerOutput -join ' ')"
    }

    $script:ComposerInvocation = $null

    function Invoke-ExternalCommand {
        param(
            [Parameter(Mandatory = $true)]
            [string] $FilePath,

            [Parameter(Mandatory = $true)]
            [string[]] $Arguments,

            [Parameter(Mandatory = $true)]
            [string] $Description
        )

        $script:ComposerInvocation = [pscustomobject]@{
            FilePath = $FilePath
            Arguments = $Arguments
            Description = $Description
        }
    }

    $laravelComposerHome = Join-Path $tempRoot 'LaravelComposerHome'

    if (-not (Install-ComposerGlobalPackage -PackageName 'laravel/installer' -Label 'Laravel Installer' -ComposerHome $laravelComposerHome)) {
        throw 'Laravel Installer did not use the fallback Composer runner.'
    }

    if (-not $script:ComposerInvocation) {
        throw 'Laravel Installer did not invoke Composer.'
    }

    if ($script:ComposerInvocation.FilePath -ine $phpExecutable) {
        throw "Laravel Installer should run PHP directly. Expected $phpExecutable, got $($script:ComposerInvocation.FilePath)"
    }

    if ($script:ComposerInvocation.Arguments[0] -ine $composerPhar) {
        throw "Laravel Installer should run composer.phar directly. Expected $composerPhar, got $($script:ComposerInvocation.Arguments[0])"
    }

    $expectedComposerArguments = @('global', 'require', 'laravel/installer')

    foreach ($expectedArgument in $expectedComposerArguments) {
        if (-not ($script:ComposerInvocation.Arguments -contains $expectedArgument)) {
            throw "Laravel Installer Composer invocation missed argument: $expectedArgument"
        }
    }

    Write-Host "PASS Composer setup failure recovered with phar backup: $composerPhar"
    Write-Host ($composerOutput | Select-Object -First 1)
} finally {
    $env:LOCALAPPDATA = $oldLocalAppData
    $env:TEMP = $oldTemp
    $env:APPDATA = $oldAppData
    $env:Path = $oldPath

    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
