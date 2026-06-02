[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Path $PSScriptRoot -Parent
$hookSource = Join-Path $repositoryRoot '.githooks\pre-commit'

if (-not (Test-Path -LiteralPath $hookSource)) {
    throw "Main branch guard hook was not found: $hookSource"
}

$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('MBS-Terminal-MainBranchGuardTest-' + [Guid]::NewGuid().ToString('N'))

function Invoke-TestGit {
    param(
        [Parameter(Mandatory = $true)]
        [string[]] $Arguments
    )

    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'

    try {
        $output = @(& git -C $tempRoot @Arguments 2>&1)
        $exitCode = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    return [pscustomobject]@{
        ExitCode = $exitCode
        Output = ($output -join "`n")
    }
}

try {
    New-Item -ItemType Directory -Path $tempRoot | Out-Null

    $initResult = Invoke-TestGit -Arguments @('init')

    if ($initResult.ExitCode -ne 0) {
        throw "git init failed. $($initResult.Output)"
    }

    foreach ($configCommand in @(
        @('config', 'user.email', 'test@example.invalid'),
        @('config', 'user.name', 'MBS Terminal Test'),
        @('config', 'core.hooksPath', '.githooks')
    )) {
        $configResult = Invoke-TestGit -Arguments $configCommand

        if ($configResult.ExitCode -ne 0) {
            throw "git $($configCommand -join ' ') failed. $($configResult.Output)"
        }
    }

    $hooksDirectory = Join-Path $tempRoot '.githooks'
    New-Item -ItemType Directory -Path $hooksDirectory | Out-Null
    Copy-Item -LiteralPath $hookSource -Destination (Join-Path $hooksDirectory 'pre-commit')

    $mainResult = Invoke-TestGit -Arguments @('checkout', '-B', 'main')

    if ($mainResult.ExitCode -ne 0) {
        throw "git checkout -B main failed. $($mainResult.Output)"
    }

    $blockedCommit = Invoke-TestGit -Arguments @('commit', '--allow-empty', '-m', 'blocked main commit')

    if ($blockedCommit.ExitCode -eq 0) {
        throw 'Expected the pre-commit hook to block commits on main.'
    }

    if ($blockedCommit.Output -notmatch 'Direct commits to main are blocked') {
        throw "Blocked commit did not explain the main branch policy. Output: $($blockedCommit.Output)"
    }

    $branchResult = Invoke-TestGit -Arguments @('switch', '-c', 'codex/test-branch')

    if ($branchResult.ExitCode -ne 0) {
        throw "git switch -c codex/test-branch failed. $($branchResult.Output)"
    }

    $allowedCommit = Invoke-TestGit -Arguments @('commit', '--allow-empty', '-m', 'allowed feature commit')

    if ($allowedCommit.ExitCode -ne 0) {
        throw "Expected the pre-commit hook to allow commits on a feature branch. $($allowedCommit.Output)"
    }

    Write-Host 'PASS main branch commits are blocked while feature branch commits are allowed.'
} finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
