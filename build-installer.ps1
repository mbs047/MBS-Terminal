$ErrorActionPreference = 'Stop'

$repositoryRoot = if ($PSScriptRoot) {
    $PSScriptRoot
} else {
    (Get-Location).ProviderPath
}

$compilerCandidates = @(
    'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe',
    'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe'
)

$compiler = $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1

if (-not $compiler) {
    throw 'C# compiler was not found. Expected .NET Framework csc.exe.'
}

$source = Join-Path $repositoryRoot 'src\MbsTerminalSetup.cs'
$output = Join-Path $repositoryRoot 'MBS-Terminal-Setup.exe'

& $compiler `
    /nologo `
    /optimize+ `
    /target:winexe `
    /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll `
    "/out:$output" `
    $source

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host "Built $output" -ForegroundColor Cyan
