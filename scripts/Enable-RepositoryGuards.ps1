[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Path $PSScriptRoot -Parent
$hooksPath = Join-Path $repositoryRoot '.githooks'

if (-not (Test-Path -LiteralPath $hooksPath)) {
    throw "Repository hooks directory was not found: $hooksPath"
}

git -C $repositoryRoot config core.hooksPath .githooks

Write-Host 'Repository guards enabled.'
Write-Host 'Direct commits to main/master are blocked locally; create a branch and open a pull request.'
