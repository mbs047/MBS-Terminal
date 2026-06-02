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

$script:AddedPath = ''

function Add-PathEntry {
    param([string] $Directory)

    if (-not (Test-Path -LiteralPath $Directory)) {
        throw "PATH candidate missing: $Directory"
    }

    $script:AddedPath = $Directory
    $env:Path = "$Directory;$env:Path"
    Write-Host "[PATH] $Directory"
}

function Refresh-ProcessPath {
    if ([string]::IsNullOrWhiteSpace($script:AddedPath)) {
        return
    }

    $entries = $env:Path -split ';' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    $hasEntry = $entries | Where-Object { $_.TrimEnd('\') -ieq $script:AddedPath.TrimEnd('\') } | Select-Object -First 1

    if (-not $hasEntry) {
        $env:Path = "$script:AddedPath;$env:Path"
    }
}

function Install-VisualCRuntimeForPhpIfPossible {
    param([string] $Architecture = '')

    Write-Host "[SKIP-VC] PHP verification test does not install VC++ runtime. Architecture: $Architecture"
}

$script:InstallScope = 'CurrentUser'
$script:PhpVersion = $PhpVersion
$script:PhpDirectory = ''
$script:InstallPhp = $true
$script:UpdateTools = $false

$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('MBS-Terminal-PhpFallbackTest-' + [Guid]::NewGuid().ToString('N'))
$oldLocalAppData = $env:LOCALAPPDATA
$oldTemp = $env:TEMP
$oldPath = $env:Path

try {
    $env:LOCALAPPDATA = Join-Path $tempRoot 'LocalAppData'
    $env:TEMP = Join-Path $tempRoot 'Temp'
    $env:Path = "$env:SystemRoot\System32;$env:SystemRoot\System32\WindowsPowerShell\v1.0"
    New-Item -ItemType Directory -Path $env:LOCALAPPDATA, $env:TEMP | Out-Null

    Install-PhpIfRequested

    $php = Join-Path $env:LOCALAPPDATA "MBS-Terminal\PHP\$PhpVersion\php.exe"

    if (-not (Test-Path -LiteralPath $php)) {
        throw "Expected fallback php.exe was not installed: $php"
    }

    $versionOutput = @(& $php --version 2>&1)

    if ($LASTEXITCODE -ne 0) {
        throw "php.exe --version failed with exit code $LASTEXITCODE. $($versionOutput -join ' ')"
    }

    $modules = @(& $php -m 2>&1)

    if ($LASTEXITCODE -ne 0) {
        throw "php.exe -m failed with exit code $LASTEXITCODE. $($modules -join ' ')"
    }

    foreach ($module in @('curl', 'fileinfo', 'mbstring', 'openssl', 'pdo_mysql', 'pdo_sqlite', 'zip')) {
        if (-not ($modules -contains $module)) {
            throw "Expected PHP module was not enabled: $module"
        }
    }

    Write-Host "PASS fallback installed and verified: $php"
    Write-Host ($versionOutput | Select-Object -First 1)
} finally {
    $env:LOCALAPPDATA = $oldLocalAppData
    $env:TEMP = $oldTemp
    $env:Path = $oldPath

    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
