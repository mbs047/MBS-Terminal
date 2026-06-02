[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Path $PSScriptRoot -Parent
$installScript = Join-Path $repositoryRoot 'install.ps1'
$settingsTemplate = Join-Path $repositoryRoot 'configs\windows-terminal\settings.json'
$expectedCommandLine = '%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe -NoLogo -ExecutionPolicy Bypass'
$profileGuid = '{61c54bbd-c2c6-5271-96e7-009a87ff44bf}'

function Assert-MbsDevShellCommandLine {
    param(
        [Parameter(Mandatory = $true)]
        [string] $SettingsPath,

        [Parameter(Mandatory = $true)]
        [string] $Context
    )

    $settings = Get-Content -LiteralPath $SettingsPath -Raw | ConvertFrom-Json
    $profile = $settings.profiles.list | Where-Object { $_.guid -eq $profileGuid } | Select-Object -First 1

    if (-not $profile) {
        throw "$Context did not include the MBS Dev Shell profile."
    }

    if ($profile.commandline -ne $expectedCommandLine) {
        throw "$Context commandline was '$($profile.commandline)' instead of '$expectedCommandLine'."
    }
}

Assert-MbsDevShellCommandLine -SettingsPath $settingsTemplate -Context 'Template settings'

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
}

function Write-SoftWarning {
    param([string] $Message)
}

$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('MBS-Terminal-ProfileLaunchTest-' + [Guid]::NewGuid().ToString('N'))
$script:SettingsPath = Join-Path $tempRoot 'settings.json'
$iconsDirectory = Join-Path $tempRoot 'icons'

function Resolve-WindowsTerminalSettingsPath {
    return $script:SettingsPath
}

try {
    New-Item -ItemType Directory -Path $iconsDirectory | Out-Null

    Install-WindowsTerminalSettings `
        -TemplatePath $settingsTemplate `
        -IconsDirectory $iconsDirectory `
        -StartingDirectory $HOME

    Assert-MbsDevShellCommandLine -SettingsPath $script:SettingsPath -Context 'Installed settings'

    Write-Host 'PASS MBS Dev Shell launches PowerShell with process execution-policy bypass.'
} finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
