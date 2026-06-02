[CmdletBinding()]
param()

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

function Test-IsAdministrator {
    return $true
}

$script:PathWriteTargets = New-Object System.Collections.Generic.List[string]
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

    [void] $script:PathWriteTargets.Add($Target)

    throw [System.UnauthorizedAccessException]::new("Simulated blocked $Target PATH write.")
}

$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('MBS-Terminal-PathFallbackTest-' + [Guid]::NewGuid().ToString('N'))
$oldPath = $env:Path

try {
    New-Item -ItemType Directory -Path $tempRoot | Out-Null
    $script:InstallScope = 'AllUsers'
    $script:ProfilePathFile = Join-Path $tempRoot 'mbs-terminal-paths.txt'
    $env:Path = "$env:SystemRoot\System32"

    Add-PathEntry -Directory $tempRoot

    if (-not ($script:PathWriteTargets -contains 'Machine')) {
        throw 'Machine PATH write was not attempted.'
    }

    if (-not ($script:PathWriteTargets -contains 'User')) {
        throw 'User PATH fallback was not attempted after machine PATH was blocked.'
    }

    if (-not (Test-Path -LiteralPath $script:ProfilePathFile)) {
        throw 'Profile PATH fallback file was not created after user PATH was blocked.'
    }

    $profilePathEntries = Get-Content -LiteralPath $script:ProfilePathFile
    $hasProfileEntry = $profilePathEntries | Where-Object { $_.TrimEnd('\') -ieq $tempRoot.TrimEnd('\') } | Select-Object -First 1

    if (-not $hasProfileEntry) {
        throw 'Profile PATH fallback file does not contain the directory.'
    }

    $processPathEntries = Get-PathEntries -PathValues @($env:Path)
    $hasProcessEntry = $processPathEntries | Where-Object { $_.TrimEnd('\') -ieq $tempRoot.TrimEnd('\') } | Select-Object -First 1

    if (-not $hasProcessEntry) {
        throw 'Process PATH did not keep the directory after machine PATH fallback.'
    }

    Refresh-ProcessPath
    $refreshedEntries = Get-PathEntries -PathValues @($env:Path)
    $hasRefreshedEntry = $refreshedEntries | Where-Object { $_.TrimEnd('\') -ieq $tempRoot.TrimEnd('\') } | Select-Object -First 1

    if (-not $hasRefreshedEntry) {
        throw 'Refresh-ProcessPath removed the process PATH entry.'
    }

    Write-Host "PASS blocked PATH writes fell back to profile PATH and preserved process PATH: $tempRoot"
} finally {
    $env:Path = $oldPath

    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
