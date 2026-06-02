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

function Build-Executable {
    param(
        [Parameter(Mandatory = $true)]
        [string[]] $Source,

        [Parameter(Mandatory = $true)]
        [string] $Output,

        [ValidateSet('exe', 'winexe')]
        [string] $Target = 'exe',

        [string] $Icon = ''
    )

    $compilerArguments = @(
        '/nologo',
        '/optimize+',
        "/target:$Target",
        "/out:$Output"
    )

    if ($Target -eq 'winexe') {
        $compilerArguments += '/reference:System.Drawing.dll'
        $compilerArguments += '/reference:System.Windows.Forms.dll'
    }

    if (-not [string]::IsNullOrWhiteSpace($Icon) -and (Test-Path -LiteralPath $Icon)) {
        $compilerArguments += "/win32icon:$Icon"
    }

    $compilerArguments += $Source

    & $compiler @compilerArguments

    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    if (-not (Test-Path -LiteralPath $Output) -or (Get-Item -LiteralPath $Output).Length -eq 0) {
        throw "Build produced no output or an empty file: $Output"
    }

    Write-Host "Built $Output" -ForegroundColor Cyan
}

function ConvertTo-CSharpStringLiteral {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Value
    )

    return '"' + $Value.Replace('\', '\\').Replace('"', '\"') + '"'
}

function ConvertTo-CSharpBase64Expression {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Value
    )

    $chunkSize = 7600
    $chunks = New-Object System.Collections.Generic.List[string]

    for ($index = 0; $index -lt $Value.Length; $index += $chunkSize) {
        $length = [Math]::Min($chunkSize, $Value.Length - $index)
        $chunks.Add((ConvertTo-CSharpStringLiteral -Value $Value.Substring($index, $length)))
    }

    if ($chunks.Count -eq 1) {
        return $chunks[0]
    }

    return 'string.Concat(new string[] { ' + (($chunks.ToArray()) -join ', ') + ' })'
}

function Assert-AsciiFile {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    $bytes = [IO.File]::ReadAllBytes($Path)

    for ($index = 0; $index -lt $bytes.Length; $index++) {
        if ($bytes[$index] -gt 127) {
            throw "Non-ASCII byte found in installer support file: $Path"
        }
    }
}

function New-TerminalInstallerSupportSource {
    param(
        [Parameter(Mandatory = $true)]
        [string] $OutputPath
    )

    $relativePaths = @(
        'install-terminal.ps1',
        'install.ps1',
        'configs\starship.toml',
        'configs\windows-terminal\settings.json',
        'configs\powershell\laravel-dev.ps1',
        'assets\terminal-icons\mbs-pixel-avatar.png',
        'assets\terminal-icons\mbs-dev-shell.png',
        'assets\terminal-icons\mbs-cmd.png',
        'assets\terminal-icons\mbs-cloud.png'
    )

    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add('using System;')
    $lines.Add('using System.IO;')
    $lines.Add('')
    $lines.Add('namespace MbsTerminalInstall')
    $lines.Add('{')
    $lines.Add('    internal static class EmbeddedTerminalInstallerSupportFiles')
    $lines.Add('    {')
    $lines.Add('        internal static void ExtractTo(string root)')
    $lines.Add('        {')

    foreach ($relativePath in $relativePaths) {
        $sourcePath = Join-Path $repositoryRoot $relativePath

        if (-not (Test-Path -LiteralPath $sourcePath)) {
            throw "Support file was not found: $relativePath"
        }

        if ($relativePath -match '\.(ps1|toml|json)$') {
            Assert-AsciiFile -Path $sourcePath
        }

        $base64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes($sourcePath))
        $pathLiteral = ConvertTo-CSharpStringLiteral -Value $relativePath
        $dataExpression = ConvertTo-CSharpBase64Expression -Value $base64
        $lines.Add("            WriteFile(root, $pathLiteral, $dataExpression);")
    }

    $lines.Add('        }')
    $lines.Add('')
    $lines.Add('        private static void WriteFile(string root, string relativePath, string base64Content)')
    $lines.Add('        {')
    $lines.Add('            string path = Path.Combine(root, relativePath);')
    $lines.Add('            string directory = Path.GetDirectoryName(path);')
    $lines.Add('')
    $lines.Add('            if (!string.IsNullOrWhiteSpace(directory))')
    $lines.Add('            {')
    $lines.Add('                Directory.CreateDirectory(directory);')
    $lines.Add('            }')
    $lines.Add('')
    $lines.Add('            File.WriteAllBytes(path, Convert.FromBase64String(base64Content));')
    $lines.Add('        }')
    $lines.Add('    }')
    $lines.Add('}')

    [IO.File]::WriteAllText($OutputPath, ($lines -join [Environment]::NewLine), [System.Text.UTF8Encoding]::new($false))
}

$generatedDirectory = Join-Path ([IO.Path]::GetTempPath()) 'MBS-Terminal-Build'

if (-not (Test-Path -LiteralPath $generatedDirectory)) {
    New-Item -ItemType Directory -Path $generatedDirectory | Out-Null
}

$terminalInstallerSupportSource = Join-Path $generatedDirectory 'MbsTerminalInstallSupport.g.cs'
New-TerminalInstallerSupportSource -OutputPath $terminalInstallerSupportSource

Build-Executable `
    -Source @(
        (Join-Path $repositoryRoot 'src\MbsTerminalInstall.cs'),
        $terminalInstallerSupportSource
    ) `
    -Output (Join-Path $repositoryRoot 'MBS-Terminal-Install.exe') `
    -Target 'exe' `
    -Icon (Join-Path $repositoryRoot 'assets\terminal-icons\mbs-terminal.ico')

Build-Executable `
    -Source (Join-Path $repositoryRoot 'src\MbsTerminalRestore.cs') `
    -Output (Join-Path $repositoryRoot 'MBS-Terminal-Restore.exe') `
    -Target 'winexe' `
    -Icon (Join-Path $repositoryRoot 'assets\terminal-icons\mbs-terminal.ico')

if (Test-Path -LiteralPath $generatedDirectory) {
    Remove-Item -LiteralPath $generatedDirectory -Recurse -Force
    Write-Host "Cleaned up temp build directory." -ForegroundColor DarkGray
}
