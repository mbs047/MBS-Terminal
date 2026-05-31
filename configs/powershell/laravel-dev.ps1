$script:MbsPortfolioPath = 'W:\GitHub\MBS-Portfolio'
$script:MbsDisplayName = '__MBS_DISPLAY_NAME__'
$script:LaravelArtisanCommandCache = @{}
$script:ComposerScriptsCache = @{}
$script:NpmScriptsCache = @{}

try {
    $script:MbsUtf8Encoding = [System.Text.UTF8Encoding]::new($false)
    [Console]::InputEncoding = $script:MbsUtf8Encoding
    [Console]::OutputEncoding = $script:MbsUtf8Encoding
    $global:OutputEncoding = $script:MbsUtf8Encoding
} catch {
}

if (Get-Module -ListAvailable -Name PSReadLine) {
    Import-Module PSReadLine -ErrorAction SilentlyContinue

    Set-PSReadLineOption -EditMode Windows -BellStyle None -ErrorAction SilentlyContinue
    Set-PSReadLineOption -HistoryNoDuplicates -ErrorAction SilentlyContinue

    $psReadLineOptions = (Get-Command Set-PSReadLineOption -ErrorAction SilentlyContinue).Parameters

    $supportsInteractivePrediction = -not [Console]::IsOutputRedirected -and -not [Console]::IsInputRedirected

    if ($supportsInteractivePrediction -and $psReadLineOptions.ContainsKey('PredictionSource')) {
        Set-PSReadLineOption -PredictionSource History -ErrorAction SilentlyContinue
    }

    if ($supportsInteractivePrediction -and $psReadLineOptions.ContainsKey('PredictionViewStyle')) {
        Set-PSReadLineOption -PredictionViewStyle ListView -ErrorAction SilentlyContinue
    }

    Set-PSReadLineKeyHandler -Key Tab -Function AcceptSuggestion -ErrorAction SilentlyContinue
    Set-PSReadLineKeyHandler -Key RightArrow -Function AcceptSuggestion -ErrorAction SilentlyContinue
    Set-PSReadLineKeyHandler -Key Alt+RightArrow -Function AcceptNextSuggestionWord -ErrorAction SilentlyContinue
    Set-PSReadLineKeyHandler -Key Ctrl+Spacebar -Function MenuComplete -ErrorAction SilentlyContinue
    Set-PSReadLineKeyHandler -Key Ctrl+r -Function ReverseSearchHistory -ErrorAction SilentlyContinue
    Set-PSReadLineKeyHandler -Key Ctrl+d -Function DeleteCharOrExit -ErrorAction SilentlyContinue
}

function Find-UpFile {
    param(
        [Parameter(Mandatory = $true)]
        [string] $FileName
    )

    $currentPath = (Get-Location).ProviderPath

    while ($currentPath) {
        $candidate = Join-Path $currentPath $FileName

        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }

        $parentPath = Split-Path -Path $currentPath -Parent

        if ([string]::IsNullOrWhiteSpace($parentPath) -or $parentPath -eq $currentPath) {
            break
        }

        $currentPath = $parentPath
    }

    return $null
}

function Get-LaravelProjectPath {
    $artisanPath = Find-UpFile -FileName 'artisan'

    if ($artisanPath) {
        return Split-Path -Path $artisanPath -Parent
    }

    return $null
}

function Invoke-InLaravelProject {
    param(
        [Parameter(Mandatory = $true)]
        [scriptblock] $ScriptBlock
    )

    $projectPath = Get-LaravelProjectPath

    if (-not $projectPath) {
        Write-Warning 'No Laravel artisan file found in this directory or its parents.'
        return
    }

    Push-Location $projectPath

    try {
        & $ScriptBlock $projectPath
    } finally {
        Pop-Location
    }
}

function Set-MbsPortfolio {
    Set-Location $script:MbsPortfolioPath
}

function Invoke-LaravelArtisan {
    param(
        [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
        [string[]] $Arguments
    )

    Invoke-InLaravelProject {
        param([string] $ProjectPath)

        & php (Join-Path $ProjectPath 'artisan') @Arguments
    }
}

function Invoke-LaravelTest {
    param(
        [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
        [string[]] $Arguments
    )

    Invoke-InLaravelProject {
        param([string] $ProjectPath)

        & php (Join-Path $ProjectPath 'artisan') test --compact @Arguments
    }
}

function Invoke-LaravelPintDirty {
    param(
        [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
        [string[]] $Arguments
    )

    Invoke-InLaravelProject {
        param([string] $ProjectPath)

        $pintPath = Join-Path $ProjectPath 'vendor\bin\pint.bat'

        if (-not (Test-Path -LiteralPath $pintPath)) {
            $pintPath = Join-Path $ProjectPath 'vendor\bin\pint'
        }

        & $pintPath --dirty --format agent @Arguments
    }
}

function Invoke-LaravelOptimizeClear {
    Invoke-LaravelArtisan optimize:clear
}

function Invoke-LaravelRoutes {
    param(
        [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
        [string[]] $Arguments
    )

    Invoke-LaravelArtisan route:list --except-vendor @Arguments
}

function Invoke-LaravelTinker {
    param(
        [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
        [string[]] $Arguments
    )

    Invoke-LaravelArtisan tinker @Arguments
}

function Start-LaravelDev {
    param(
        [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
        [string[]] $Arguments
    )

    Invoke-InLaravelProject {
        $composerScripts = Get-CachedJsonScripts -FileName 'composer.json' -Cache $script:ComposerScriptsCache
        $npmScripts = Get-CachedJsonScripts -FileName 'package.json' -Cache $script:NpmScriptsCache

        if ($composerScripts.Name -contains 'dev') {
            & composer run dev @Arguments
            return
        }

        if ($npmScripts.Name -contains 'dev') {
            & npm run dev @Arguments
            return
        }

        & php artisan serve @Arguments
    }
}

function Invoke-ComposerRun {
    param(
        [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
        [string[]] $Arguments
    )

    & composer run @Arguments
}

function Invoke-NpmRun {
    param(
        [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
        [string[]] $Arguments
    )

    & npm run @Arguments
}

function Get-CachedLaravelArtisanCommands {
    $projectPath = Get-LaravelProjectPath

    if (-not $projectPath) {
        return @()
    }

    $cacheKey = $projectPath.ToLowerInvariant()
    $cached = $script:LaravelArtisanCommandCache[$cacheKey]

    if ($cached -and ((Get-Date) - $cached.Time).TotalMinutes -lt 5) {
        return $cached.Commands
    }

    $artisanPath = Join-Path $projectPath 'artisan'
    $commands = @()

    try {
        $commands = & php $artisanPath list --raw --no-interaction 2>$null | ForEach-Object {
            if ($_ -match '^(\S+)\s*(.*)$') {
                [pscustomobject]@{
                    Name = $matches[1]
                    Description = $matches[2]
                }
            }
        }
    } catch {
        $commands = @()
    }

    $script:LaravelArtisanCommandCache[$cacheKey] = @{
        Time = Get-Date
        Commands = $commands
    }

    return $commands
}

function Get-CachedJsonScripts {
    param(
        [Parameter(Mandatory = $true)]
        [string] $FileName,

        [Parameter(Mandatory = $true)]
        [hashtable] $Cache
    )

    $filePath = Find-UpFile -FileName $FileName

    if (-not $filePath) {
        return @()
    }

    $cacheKey = $filePath.ToLowerInvariant()
    $cached = $Cache[$cacheKey]

    if ($cached -and ((Get-Date) - $cached.Time).TotalMinutes -lt 5) {
        return $cached.Scripts
    }

    try {
        $json = Get-Content -LiteralPath $filePath -Raw | ConvertFrom-Json
        $scripts = @($json.scripts.PSObject.Properties | ForEach-Object {
            [pscustomobject]@{
                Name = $_.Name
                Description = [string] $_.Value
            }
        })
    } catch {
        $scripts = @()
    }

    $Cache[$cacheKey] = @{
        Time = Get-Date
        Scripts = $scripts
    }

    return $scripts
}

function New-ScriptCompletionResult {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Script,

        [Parameter(Mandatory = $true)]
        [string] $WordToComplete
    )

    if ($Script.Name -like "$WordToComplete*") {
        [System.Management.Automation.CompletionResult]::new(
            $Script.Name,
            $Script.Name,
            [System.Management.Automation.CompletionResultType]::ParameterValue,
            $Script.Description
        )
    }
}

Register-ArgumentCompleter -CommandName Invoke-LaravelArtisan, pa, art -ParameterName Arguments -ScriptBlock {
    param($commandName, $parameterName, $wordToComplete)

    Get-CachedLaravelArtisanCommands | ForEach-Object {
        New-ScriptCompletionResult -Script $_ -WordToComplete $wordToComplete
    }
}

Register-ArgumentCompleter -Native -CommandName php -ScriptBlock {
    param($wordToComplete, $commandAst)

    $commandElements = @($commandAst.CommandElements | ForEach-Object {
        $_.Extent.Text.Trim('"', "'")
    })

    if ($commandElements.Count -lt 2) {
        return
    }

    $secondArgument = $commandElements[1] -replace '\\', '/'

    if ($secondArgument -notmatch '(^|/)artisan$') {
        return
    }

    Get-CachedLaravelArtisanCommands | ForEach-Object {
        New-ScriptCompletionResult -Script $_ -WordToComplete $wordToComplete
    }
}

Register-ArgumentCompleter -CommandName Invoke-ComposerRun, cr -ParameterName Arguments -ScriptBlock {
    param($commandName, $parameterName, $wordToComplete)

    Get-CachedJsonScripts -FileName 'composer.json' -Cache $script:ComposerScriptsCache | ForEach-Object {
        New-ScriptCompletionResult -Script $_ -WordToComplete $wordToComplete
    }
}

Register-ArgumentCompleter -Native -CommandName composer -ScriptBlock {
    param($wordToComplete, $commandAst)

    $commandElements = @($commandAst.CommandElements | ForEach-Object {
        $_.Extent.Text.Trim('"', "'")
    })

    if ($commandElements.Count -lt 2 -or $commandElements[1] -notin @('run', 'run-script')) {
        return
    }

    Get-CachedJsonScripts -FileName 'composer.json' -Cache $script:ComposerScriptsCache | ForEach-Object {
        New-ScriptCompletionResult -Script $_ -WordToComplete $wordToComplete
    }
}

Register-ArgumentCompleter -CommandName Invoke-NpmRun, nr -ParameterName Arguments -ScriptBlock {
    param($commandName, $parameterName, $wordToComplete)

    Get-CachedJsonScripts -FileName 'package.json' -Cache $script:NpmScriptsCache | ForEach-Object {
        New-ScriptCompletionResult -Script $_ -WordToComplete $wordToComplete
    }
}

Register-ArgumentCompleter -Native -CommandName npm -ScriptBlock {
    param($wordToComplete, $commandAst)

    $commandElements = @($commandAst.CommandElements | ForEach-Object {
        $_.Extent.Text.Trim('"', "'")
    })

    if ($commandElements.Count -lt 2 -or $commandElements[1] -notin @('run', 'run-script')) {
        return
    }

    Get-CachedJsonScripts -FileName 'package.json' -Cache $script:NpmScriptsCache | ForEach-Object {
        New-ScriptCompletionResult -Script $_ -WordToComplete $wordToComplete
    }
}

@('mbs', 'pa', 'art', 'pat', 'pintd', 'pclear', 'proutes', 'ptinker', 'ldev', 'cr', 'nr') | ForEach-Object {
    Remove-Item -Path "Alias:$_" -ErrorAction SilentlyContinue
}

function mbs {
    Set-MbsPortfolio
}

function pa {
    param(
        [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
        [string[]] $Arguments
    )

    Invoke-LaravelArtisan @Arguments
}

function art {
    param(
        [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
        [string[]] $Arguments
    )

    Invoke-LaravelArtisan @Arguments
}

function pat {
    param(
        [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
        [string[]] $Arguments
    )

    Invoke-LaravelTest @Arguments
}

function pintd {
    param(
        [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
        [string[]] $Arguments
    )

    Invoke-LaravelPintDirty @Arguments
}

function pclear {
    Invoke-LaravelOptimizeClear
}

function proutes {
    param(
        [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
        [string[]] $Arguments
    )

    Invoke-LaravelRoutes @Arguments
}

function ptinker {
    param(
        [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
        [string[]] $Arguments
    )

    Invoke-LaravelTinker @Arguments
}

function ldev {
    param(
        [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
        [string[]] $Arguments
    )

    Start-LaravelDev @Arguments
}

function cr {
    param(
        [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
        [string[]] $Arguments
    )

    Invoke-ComposerRun @Arguments
}

function nr {
    param(
        [Parameter(Position = 0, ValueFromRemainingArguments = $true)]
        [string[]] $Arguments
    )

    Invoke-NpmRun @Arguments
}

function Format-MbsFileSize {
    param(
        [Parameter(Mandatory = $true)]
        [long] $Bytes
    )

    if ($Bytes -ge 1GB) {
        return '{0:N1} GB' -f ($Bytes / 1GB)
    }

    if ($Bytes -ge 1MB) {
        return '{0:N1} MB' -f ($Bytes / 1MB)
    }

    if ($Bytes -ge 1KB) {
        return '{0:N1} KB' -f ($Bytes / 1KB)
    }

    return "$Bytes B"
}

function Get-MbsItemKind {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.FileSystemInfo] $Item
    )

    if ($Item.Name.StartsWith('.')) {
        if ($Item.PSIsContainer) {
            return 'HID-DIR'
        }

        return 'HIDDEN'
    }

    if ($Item.PSIsContainer) {
        return 'DIR'
    }

    if ([string]::IsNullOrWhiteSpace($Item.Extension)) {
        return 'FILE'
    }

    $extension = $Item.Extension.TrimStart('.').ToUpperInvariant()

    if ($extension.Length -gt 8) {
        return $extension.Substring(0, 8)
    }

    return $extension
}

function Get-MbsItemColor {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.FileSystemInfo] $Item
    )

    if ($Item.PSIsContainer) {
        return 'Cyan'
    }

    switch ($Item.Extension.ToLowerInvariant()) {
        '.php' { return 'Magenta' }
        '.blade.php' { return 'Magenta' }
        '.js' { return 'Yellow' }
        '.ts' { return 'Yellow' }
        '.json' { return 'Green' }
        '.env' { return 'DarkYellow' }
        '.css' { return 'Blue' }
        '.scss' { return 'Blue' }
        '.md' { return 'White' }
        '.lock' { return 'DarkGray' }
        default { return 'Gray' }
    }
}

function Show-MbsDirectoryList {
    param(
        [string[]] $Path = @('.'),
        [switch] $Force,
        [switch] $Recurse,
        [switch] $File,
        [switch] $Directory
    )

    foreach ($targetPath in $Path) {
        $childItemParameters = @{
            Path = $targetPath
            ErrorAction = 'Continue'
        }

        if ($Force) {
            $childItemParameters.Force = $true
        }

        if ($Recurse) {
            $childItemParameters.Recurse = $true
        }

        if ($File) {
            $childItemParameters.File = $true
        }

        if ($Directory) {
            $childItemParameters.Directory = $true
        }

        $items = @(Get-ChildItem @childItemParameters | Where-Object {
            $Force -or -not $_.Name.StartsWith('.')
        } | Sort-Object @{ Expression = 'PSIsContainer'; Descending = $true }, Name)

        Write-Host ''
        Write-Host ("Path: {0}" -f (Resolve-Path -Path $targetPath -ErrorAction SilentlyContinue)) -ForegroundColor DarkCyan
        Write-Host '+----------+------------+------------------+------------------------------' -ForegroundColor DarkGray
        Write-Host ('| {0,-8} | {1,10} | {2,-16} | {3}' -f 'Type', 'Size', 'Modified', 'Name') -ForegroundColor DarkGray
        Write-Host '+----------+------------+------------------+------------------------------' -ForegroundColor DarkGray

        foreach ($item in $items) {
            $kind = Get-MbsItemKind -Item $item
            $size = if ($item.PSIsContainer) { '<dir>' } else { Format-MbsFileSize -Bytes $item.Length }
            $modified = $item.LastWriteTime.ToString('MMM dd yyyy HH:mm')
            $color = Get-MbsItemColor -Item $item

            Write-Host ('| {0,-8} | {1,10} | {2,-16} | ' -f $kind, $size, $modified) -NoNewline -ForegroundColor DarkGray
            Write-Host $item.Name -ForegroundColor $color
        }

        if ($items.Count -eq 0) {
            Write-Host ('| {0,-8} | {1,10} | {2,-16} | {3}' -f '-', '-', '-', '(empty)') -ForegroundColor DarkGray
        }

        Write-Host '+----------+------------+------------------+------------------------------' -ForegroundColor DarkGray
    }
}

function Invoke-MbsDirectoryNavigator {
    param(
        [string] $StartPath = '.'
    )

    $resolvedPath = Resolve-Path -Path $StartPath -ErrorAction SilentlyContinue

    if (-not $resolvedPath) {
        Write-Warning "Path not found: $StartPath"
        return
    }

    $currentPath = $resolvedPath.ProviderPath
    $selectedIndex = 0

    while ($true) {
        $parentPath = Split-Path -Path $currentPath -Parent
        $items = @()

        if ($parentPath -and $parentPath -ne $currentPath) {
            $items += [pscustomobject]@{
                Name = '..'
                Path = $parentPath
                Kind = 'Parent'
            }
        }

        $items += @(Get-ChildItem -LiteralPath $currentPath -Directory -Force | Sort-Object Name | ForEach-Object {
            [pscustomobject]@{
                Name = $_.Name
                Path = $_.FullName
                Kind = 'Directory'
            }
        })

        if ($selectedIndex -ge $items.Count) {
            $selectedIndex = [Math]::Max(0, $items.Count - 1)
        }

        Clear-Host
        Write-Host 'Folder navigator' -ForegroundColor Cyan
        Write-Host ("Current: {0}" -f $currentPath) -ForegroundColor DarkGray
        Write-Host 'Up/Down: select | Enter: open | Backspace: parent | Esc or Q: done' -ForegroundColor DarkGray
        Write-Host ''

        if ($items.Count -eq 0) {
            Write-Host 'No folders here. Press Backspace, Esc, or Q.' -ForegroundColor DarkYellow
        }

        for ($index = 0; $index -lt $items.Count; $index++) {
            $prefix = if ($index -eq $selectedIndex) { '>' } else { ' ' }
            $color = if ($index -eq $selectedIndex) { 'Yellow' } elseif ($items[$index].Kind -eq 'Parent') { 'DarkYellow' } else { 'Cyan' }

            Write-Host (" {0} [{1}] {2}" -f $prefix, $items[$index].Kind, $items[$index].Name) -ForegroundColor $color
        }

        $key = [Console]::ReadKey($true)

        switch ($key.Key) {
            'UpArrow' {
                if ($items.Count -gt 0) {
                    $selectedIndex = if ($selectedIndex -le 0) { $items.Count - 1 } else { $selectedIndex - 1 }
                }
            }
            'DownArrow' {
                if ($items.Count -gt 0) {
                    $selectedIndex = ($selectedIndex + 1) % $items.Count
                }
            }
            'Home' {
                $selectedIndex = 0
            }
            'End' {
                if ($items.Count -gt 0) {
                    $selectedIndex = $items.Count - 1
                }
            }
            'Backspace' {
                if ($parentPath -and $parentPath -ne $currentPath) {
                    $currentPath = $parentPath
                    $selectedIndex = 0
                }
            }
            'Enter' {
                if ($items.Count -gt 0) {
                    $currentPath = $items[$selectedIndex].Path
                    Set-Location -LiteralPath $currentPath
                    $selectedIndex = 0
                }
            }
            'Escape' {
                Set-Location -LiteralPath $currentPath
                Clear-Host
                Show-MbsDirectoryList -Path '.'
                return
            }
            default {
                if ($key.KeyChar -in @('q', 'Q')) {
                    Set-Location -LiteralPath $currentPath
                    Clear-Host
                    Show-MbsDirectoryList -Path '.'
                    return
                }
            }
        }
    }
}

Remove-Item -Path Alias:ls -ErrorAction SilentlyContinue

function ls {
    param(
        [Parameter(ValueFromRemainingArguments = $true)]
        [string[]] $Arguments
    )

    $path = @()
    $navigate = $false
    $force = $false
    $recurse = $false
    $file = $false
    $directory = $false

    foreach ($argument in $Arguments) {
        switch ($argument) {
            '-nav' {
                $navigate = $true
                continue
            }
            '--nav' {
                $navigate = $true
                continue
            }
            '-all' {
                $force = $true
                continue
            }
            '--all' {
                $force = $true
                continue
            }
            '-force' {
                $force = $true
                continue
            }
            '--force' {
                $force = $true
                continue
            }
            '-hidden' {
                $force = $true
                continue
            }
            '--hidden' {
                $force = $true
                continue
            }
            '-recursive' {
                $recurse = $true
                continue
            }
            '--recursive' {
                $recurse = $true
                continue
            }
            '-recurse' {
                $recurse = $true
                continue
            }
            '--recurse' {
                $recurse = $true
                continue
            }
            '-file' {
                $file = $true
                continue
            }
            '--file' {
                $file = $true
                continue
            }
            '-files' {
                $file = $true
                continue
            }
            '--files' {
                $file = $true
                continue
            }
            '-dir' {
                $directory = $true
                continue
            }
            '--dir' {
                $directory = $true
                continue
            }
            '-directory' {
                $directory = $true
                continue
            }
            '--directory' {
                $directory = $true
                continue
            }
            '-a' {
                $force = $true
                continue
            }
            '-l' {
                continue
            }
            '-la' {
                $force = $true
                continue
            }
            '-al' {
                $force = $true
                continue
            }
            default {
                if ($argument -like '-*') {
                    Write-Warning "Unknown ls option '$argument'. Use 'ls -nav' for folder navigation."
                    continue
                }

                $path += $argument
            }
        }
    }

    if ($path.Count -eq 0) {
        $path = @('.')
    }

    if ($navigate) {
        Invoke-MbsDirectoryNavigator -StartPath $path[0]
        return
    }

    Show-MbsDirectoryList -Path $path -Force:$force -Recurse:$recurse -File:$file -Directory:$directory
}

function Show-MbsWelcome {
    if ([Console]::IsOutputRedirected -or [Console]::IsInputRedirected) {
        return
    }

    if ($Host.Name -ne 'ConsoleHost') {
        return
    }

    $hour = (Get-Date).Hour
    $greeting = 'Welcome back'

    if ($hour -lt 12) {
        $greeting = 'Good morning'
    } elseif ($hour -lt 18) {
        $greeting = 'Good afternoon'
    } else {
        $greeting = 'Good evening'
    }

    $quotes = @(
        'Small commits, clear intent, steady progress.',
        'Make it work, make it clear, then make it fast.',
        'Your future self deserves readable code.',
        'A clean test is a quiet mind.',
        'One focused change can move the whole project.',
        'Ship the simplest useful version, then sharpen it.',
        'Every solved bug is proof that you can reason through the fog.',
        'Good architecture is built one careful decision at a time.',
        'The best feature today is the one you can explain tomorrow.',
        'Tests are not chores, they are confidence you can run.',
        'Refactor when the code tells you where it hurts.',
        'You do not need perfect momentum, just the next honest step.'
    )

    $quote = Get-Random -InputObject $quotes
    $currentPath = Split-Path -Path (Get-Location).ProviderPath -Leaf
    $displayName = $script:MbsDisplayName

    if ([string]::IsNullOrWhiteSpace($displayName) -or $displayName -eq '__MBS_DISPLAY_NAME__') {
        $displayName = $env:USERNAME
    }

    Write-Host ''
    Write-Host '+----------------------------------------------------------+' -ForegroundColor DarkMagenta
    Write-Host ("  {0}, {1}. Ready to build {2}." -f $greeting, $displayName, $currentPath) -ForegroundColor Cyan
    Write-Host ("  Quote: {0}" -f $quote) -ForegroundColor DarkYellow
    Write-Host '+----------------------------------------------------------+' -ForegroundColor DarkMagenta
    Write-Host ''
}

Show-MbsWelcome
