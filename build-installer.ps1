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

function Build-WindowsExecutable {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Source,

        [Parameter(Mandatory = $true)]
        [string] $Output,

        [string] $Icon = ''
    )

    $compilerArguments = @(
        '/nologo',
        '/optimize+',
        '/target:winexe',
        '/reference:System.Drawing.dll',
        '/reference:System.Windows.Forms.dll',
        "/out:$Output"
    )

    if (-not [string]::IsNullOrWhiteSpace($Icon) -and (Test-Path -LiteralPath $Icon)) {
        $compilerArguments += "/win32icon:$Icon"
    }

    $compilerArguments += $Source

    & $compiler @compilerArguments

    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    Write-Host "Built $Output" -ForegroundColor Cyan
}

Build-WindowsExecutable `
    -Source (Join-Path $repositoryRoot 'src\MbsTerminalSetup.cs') `
    -Output (Join-Path $repositoryRoot 'MBS-Terminal-Setup.exe') `
    -Icon (Join-Path $repositoryRoot 'assets\terminal-icons\mbs-terminal.ico')

Build-WindowsExecutable `
    -Source (Join-Path $repositoryRoot 'src\MbsTerminalRestore.cs') `
    -Output (Join-Path $repositoryRoot 'MBS-Terminal-Restore.exe') `
    -Icon (Join-Path $repositoryRoot 'assets\terminal-icons\mbs-terminal.ico')
