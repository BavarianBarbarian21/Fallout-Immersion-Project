[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$toolsRoot = if (-not [string]::IsNullOrWhiteSpace($PSScriptRoot)) {
    $PSScriptRoot
}
else {
    Split-Path -Parent $MyInvocation.MyCommand.Path
}

$translationRoot = Split-Path -Parent $toolsRoot
$statusRoot = Join-Path $toolsRoot 'state\launcher-status'

$palette = @{
    Background   = [System.Drawing.Color]::FromArgb(18, 18, 24)
    Panel        = [System.Drawing.Color]::FromArgb(28, 28, 38)
    Text         = [System.Drawing.Color]::FromArgb(236, 236, 240)
    Muted        = [System.Drawing.Color]::FromArgb(156, 160, 176)
    Accent       = [System.Drawing.Color]::FromArgb(64, 140, 255)
    IdleButton   = [System.Drawing.Color]::FromArgb(45, 49, 60)
    Running      = [System.Drawing.Color]::FromArgb(170, 36, 52)
    Completed    = [System.Drawing.Color]::FromArgb(37, 134, 76)
    Failed       = [System.Drawing.Color]::FromArgb(184, 107, 16)
    ProgressBack = [System.Drawing.Color]::FromArgb(36, 39, 49)
}

$scriptEntries = @(
    [pscustomobject]@{ Label = 'Part 1 English Sync'; Category = 'part1'; Mode = 'EnglishSync'; ScriptPath = Join-Path $translationRoot 'Part 1 Updater\English Sync\Run-EnglishSync.ps1' },
    [pscustomobject]@{ Label = 'Part 1 Language Translation'; Category = 'part1'; Mode = 'LanguageTranslation'; ScriptPath = Join-Path $translationRoot 'Part 1 Updater\Language Translation\Run-LanguageTranslation.ps1' },
    [pscustomobject]@{ Label = 'Part 2 English Sync'; Category = 'part2'; Mode = 'EnglishSync'; ScriptPath = Join-Path $translationRoot 'Part 2 Updater\English Sync\Run-EnglishSync.ps1' },
    [pscustomobject]@{ Label = 'Part 2 Language Translation'; Category = 'part2'; Mode = 'LanguageTranslation'; ScriptPath = Join-Path $translationRoot 'Part 2 Updater\Language Translation\Run-LanguageTranslation.ps1' },
    [pscustomobject]@{ Label = 'Part 3 English Sync'; Category = 'part3'; Mode = 'EnglishSync'; ScriptPath = Join-Path $translationRoot 'Part 3 Updater\English Sync\Run-EnglishSync.ps1' },
    [pscustomobject]@{ Label = 'Part 3 Language Translation'; Category = 'part3'; Mode = 'LanguageTranslation'; ScriptPath = Join-Path $translationRoot 'Part 3 Updater\Language Translation\Run-LanguageTranslation.ps1' },
    [pscustomobject]@{ Label = 'Part 4 English Sync'; Category = 'part4'; Mode = 'EnglishSync'; ScriptPath = Join-Path $translationRoot 'Part 4 Updater\English Sync\Run-EnglishSync.ps1' },
    [pscustomobject]@{ Label = 'Part 4 Language Translation'; Category = 'part4'; Mode = 'LanguageTranslation'; ScriptPath = Join-Path $translationRoot 'Part 4 Updater\Language Translation\Run-LanguageTranslation.ps1' }
)

$scriptButtons = @{}
$script:PendingStates = @{}

function Read-SharedTextFile {
    param([string]$Path)

    $stream = $null
    $reader = $null
    try {
        $stream = New-Object System.IO.FileStream($Path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
        $reader = New-Object System.IO.StreamReader($stream, [System.Text.Encoding]::UTF8, $true)
        return $reader.ReadToEnd()
    }
    finally {
        if ($null -ne $reader) {
            $reader.Dispose()
        }
        elseif ($null -ne $stream) {
            $stream.Dispose()
        }
    }
}

function Get-StatusFilePath {
    param([pscustomobject]$Entry)

    return (Join-Path $statusRoot ('{0}-{1}.json' -f $Entry.Category, $Entry.Mode))
}

function Clear-LauncherStatusFiles {
    param([string]$StatusFolder)

    if (-not (Test-Path -LiteralPath $StatusFolder)) {
        return
    }

    Get-ChildItem -LiteralPath $StatusFolder -Filter '*.json' -File -ErrorAction SilentlyContinue |
        Remove-Item -Force -ErrorAction SilentlyContinue
}

function Start-LauncherScript {
    param(
        [pscustomobject]$Entry,
        [System.Windows.Forms.Label]$StatusLabel
    )

    if (-not (Test-Path -LiteralPath $Entry.ScriptPath)) {
        [System.Windows.Forms.MessageBox]::Show(
            "Script not found:`r`n$($Entry.ScriptPath)",
            'FIP Translation Launcher',
            [System.Windows.Forms.MessageBoxButtons]::OK,
            [System.Windows.Forms.MessageBoxIcon]::Error
        ) | Out-Null
        return
    }

    $StatusLabel.Text = "Started: $($Entry.Label). Waiting for updater status..."
    $pendingKey = '{0}|{1}' -f $Entry.Category, $Entry.Mode
    $script:PendingStates[$pendingKey] = [ordered]@{
        state = 'running'
        updatedAt = Get-Date
    }
    $statusPath = Get-StatusFilePath -Entry $Entry
    Remove-Item -LiteralPath $statusPath -Force -ErrorAction SilentlyContinue
    $escapedScriptPath = $Entry.ScriptPath.Replace("'", "''")
    $launchCommand = "& '$escapedScriptPath'"
    Start-Process -FilePath 'powershell.exe' -ArgumentList @(
        '-NoProfile',
        '-ExecutionPolicy', 'Bypass',
        '-NoExit',
        '-Command', $launchCommand
    ) -WorkingDirectory (Split-Path -Parent $Entry.ScriptPath) | Out-Null
}

function Get-LauncherStatuses {
    param([string]$StatusFolder)

    $results = @{}
    if (-not (Test-Path -LiteralPath $StatusFolder)) {
        return $results
    }

    foreach ($statusFile in Get-ChildItem -LiteralPath $StatusFolder -Filter '*.json' -File -ErrorAction SilentlyContinue) {
        try {
            $raw = Read-SharedTextFile -Path $statusFile.FullName
            if ([string]::IsNullOrWhiteSpace($raw)) {
                continue
            }
            $entry = $raw | ConvertFrom-Json
            $key = '{0}|{1}' -f [string]$entry.category, [string]$entry.mode
            $results[$key] = $entry
        }
        catch {
        }
    }

    return $results
}

function Get-ProgressInfo {
    param([string]$ProgressPath)

    if ([string]::IsNullOrWhiteSpace($ProgressPath) -or -not (Test-Path -LiteralPath $ProgressPath)) {
        return $null
    }

    $map = @{}
    $raw = Read-SharedTextFile -Path $ProgressPath
    if ([string]::IsNullOrWhiteSpace($raw)) {
        return $null
    }

    foreach ($line in ($raw -split "`r?`n")) {
        if ($line -match '^(?<key>[^:]+):\s*(?<value>.*)$') {
            $map[$Matches.key.Trim()] = $Matches.value.Trim()
        }
    }

    return $map
}

function Update-LauncherUi {
    param(
        [hashtable]$Statuses,
        [hashtable]$Buttons,
        [System.Windows.Forms.Label]$StatusLabel,
        [System.Windows.Forms.Label]$ProgressDetailLabel,
        [System.Windows.Forms.ProgressBar]$ProgressBar
    )

    foreach ($entry in $scriptEntries) {
        $key = '{0}|{1}' -f $entry.Category, $entry.Mode
        $button = $Buttons[$key]
        $button.BackColor = $palette.IdleButton
        $button.ForeColor = $palette.Text
        $button.FlatAppearance.BorderColor = $palette.Accent
        if ($Statuses.ContainsKey($key)) {
            $status = $Statuses[$key]
            $script:PendingStates[$key] = [ordered]@{ state = [string]$status.state; updatedAt = Get-Date }
            switch ([string]$status.state) {
                'running' { $button.BackColor = $palette.Running }
                'completed' { $button.BackColor = $palette.Completed }
                'failed' { $button.BackColor = $palette.Failed }
            }
        }
        elseif ($script:PendingStates.ContainsKey($key)) {
            $pending = $script:PendingStates[$key]
            $ageSeconds = ((Get-Date) - [datetime]$pending.updatedAt).TotalSeconds
            if ([string]$pending.state -eq 'running' -and $ageSeconds -le 30) {
                $button.BackColor = $palette.Running
            }
        }
    }

    $running = @($Statuses.Values | Where-Object { [string]$_.state -eq 'running' } | Sort-Object updatedAt -Descending)
    if ($running.Count -gt 0) {
        $status = $running[0]
        $progress = Get-ProgressInfo -ProgressPath ([string]$status.progressPath)
        $StatusLabel.Text = 'Running: ' + [string]$status.label
        if ($null -ne $progress -and $progress.ContainsKey('Completed outputs') -and $progress.ContainsKey('Outputs queued')) {
            $completed = 0
            $queued = 0
            [void][int]::TryParse([string]$progress['Completed outputs'], [ref]$completed)
            [void][int]::TryParse([string]$progress['Outputs queued'], [ref]$queued)
            $percent = if ($queued -gt 0) { [math]::Min(100, [math]::Max(0, [math]::Floor(($completed * 100.0) / $queued))) } else { 0 }
            $ProgressBar.Value = [int]$percent
            $currentOutput = if ($progress.ContainsKey('Current output')) { $progress['Current output'] } else { 'waiting for first file' }
            $ProgressDetailLabel.Text = "Progress: $completed/$queued ($percent%) - $currentOutput"
        }
        else {
            $ProgressBar.Value = 0
            $ProgressDetailLabel.Text = 'Progress: waiting for sync progress file...'
        }
        return
    }

    $latest = @($Statuses.Values | Sort-Object updatedAt -Descending | Select-Object -First 1)
    if ($latest.Count -eq 0) {
        $StatusLabel.Text = 'No updater has been launched yet.'
        $ProgressDetailLabel.Text = 'Progress: no active updater'
        $ProgressBar.Value = 0
        return
    }

    $entry = $latest[0]
    switch ([string]$entry.state) {
        'completed' { $StatusLabel.Text = 'Last finished: ' + [string]$entry.label }
        'failed' { $StatusLabel.Text = 'Last failed: ' + [string]$entry.label }
        default { $StatusLabel.Text = 'Last status: ' + [string]$entry.label }
    }
    $ProgressDetailLabel.Text = [string]$entry.message
    $ProgressBar.Value = 0
}

$form = New-Object System.Windows.Forms.Form
$form.Text = 'FIP Translation Launcher'
$form.StartPosition = 'CenterScreen'
$form.Size = New-Object System.Drawing.Size(820, 560)
$form.MinimumSize = New-Object System.Drawing.Size(820, 560)
$form.MaximizeBox = $false
$form.BackColor = $palette.Background
$form.ForeColor = $palette.Text

$titleLabel = New-Object System.Windows.Forms.Label
$titleLabel.Text = 'FIP Translation Control'
$titleLabel.Location = New-Object System.Drawing.Point(24, 18)
$titleLabel.Size = New-Object System.Drawing.Size(740, 30)
$titleLabel.Font = New-Object System.Drawing.Font('Segoe UI Semibold', 16, [System.Drawing.FontStyle]::Bold)
$titleLabel.ForeColor = $palette.Text
$form.Controls.Add($titleLabel)

$subtitleLabel = New-Object System.Windows.Forms.Label
$subtitleLabel.Text = 'Dark launcher with live progress, button state colors, and quick access to updater tools.'
$subtitleLabel.Location = New-Object System.Drawing.Point(24, 52)
$subtitleLabel.Size = New-Object System.Drawing.Size(740, 22)
$subtitleLabel.ForeColor = $palette.Muted
$form.Controls.Add($subtitleLabel)

$progressBar = New-Object System.Windows.Forms.ProgressBar
$progressBar.Location = New-Object System.Drawing.Point(24, 86)
$progressBar.Size = New-Object System.Drawing.Size(748, 18)
$progressBar.Style = 'Continuous'
$form.Controls.Add($progressBar)

$progressDetailLabel = New-Object System.Windows.Forms.Label
$progressDetailLabel.Text = 'Progress: no active updater'
$progressDetailLabel.Location = New-Object System.Drawing.Point(24, 110)
$progressDetailLabel.Size = New-Object System.Drawing.Size(748, 22)
$progressDetailLabel.ForeColor = $palette.Muted
$form.Controls.Add($progressDetailLabel)

$statusLabel = New-Object System.Windows.Forms.Label
$statusLabel.Text = 'No updater has been launched yet.'
$statusLabel.Location = New-Object System.Drawing.Point(24, 136)
$statusLabel.Size = New-Object System.Drawing.Size(748, 22)
$statusLabel.ForeColor = $palette.Text
$form.Controls.Add($statusLabel)

$openConfigButton = New-Object System.Windows.Forms.Button
$openConfigButton.Text = 'Open Config'
$openConfigButton.Location = New-Object System.Drawing.Point(24, 472)
$openConfigButton.Size = New-Object System.Drawing.Size(140, 36)
$openConfigButton.BackColor = $palette.Panel
$openConfigButton.ForeColor = $palette.Text
$openConfigButton.FlatStyle = 'Flat'
$openConfigButton.FlatAppearance.BorderColor = $palette.Accent
$openConfigButton.Add_Click({
    Start-Process -FilePath (Join-Path $toolsRoot 'config.json') | Out-Null
})
$form.Controls.Add($openConfigButton)

$openReadmeButton = New-Object System.Windows.Forms.Button
$openReadmeButton.Text = 'Open README'
$openReadmeButton.Location = New-Object System.Drawing.Point(176, 472)
$openReadmeButton.Size = New-Object System.Drawing.Size(140, 36)
$openReadmeButton.BackColor = $palette.Panel
$openReadmeButton.ForeColor = $palette.Text
$openReadmeButton.FlatStyle = 'Flat'
$openReadmeButton.FlatAppearance.BorderColor = $palette.Accent
$openReadmeButton.Add_Click({
    Start-Process -FilePath (Join-Path $toolsRoot 'README.md') | Out-Null
})
$form.Controls.Add($openReadmeButton)

$helpLabel = New-Object System.Windows.Forms.Label
$helpLabel.Text = 'Button colors: gray idle, red running, green finished, amber failed.'
$helpLabel.Location = New-Object System.Drawing.Point(336, 480)
$helpLabel.Size = New-Object System.Drawing.Size(436, 22)
$helpLabel.ForeColor = $palette.Muted
$form.Controls.Add($helpLabel)

$buttonWidth = 360
$buttonHeight = 56
$leftColumnX = 24
$rightColumnX = 412
$startY = 184
$verticalGap = 14

$index = 0
foreach ($entry in $scriptEntries) {
    $button = New-Object System.Windows.Forms.Button
    $button.Text = $entry.Label
    $button.UseVisualStyleBackColor = $false
    $button.BackColor = $palette.IdleButton
    $button.ForeColor = $palette.Text
    $button.FlatStyle = 'Flat'
    $button.FlatAppearance.BorderColor = $palette.Accent
    $button.FlatAppearance.BorderSize = 1
    $button.Font = New-Object System.Drawing.Font('Segoe UI', 11, [System.Drawing.FontStyle]::Regular)

    $columnX = if ($index % 2 -eq 0) { $leftColumnX } else { $rightColumnX }
    $rowIndex = [int][Math]::Floor($index / 2)
    $button.Location = New-Object System.Drawing.Point($columnX, ($startY + (($buttonHeight + $verticalGap) * $rowIndex)))
    $button.Size = New-Object System.Drawing.Size($buttonWidth, $buttonHeight)

    $capturedEntry = $entry
    $button.Tag = $capturedEntry
    $button.Add_Click({
        param($clickSource, $clickData)
        Start-LauncherScript -Entry ([pscustomobject]$clickSource.Tag) -StatusLabel $statusLabel
    })

    $key = '{0}|{1}' -f $entry.Category, $entry.Mode
    $scriptButtons[$key] = $button
    $form.Controls.Add($button)
    $index++
}

$statusTimer = New-Object System.Windows.Forms.Timer
$statusTimer.Interval = 1500
$statusTimer.Add_Tick({
    try {
        $statuses = Get-LauncherStatuses -StatusFolder $statusRoot
        Update-LauncherUi -Statuses $statuses -Buttons $scriptButtons -StatusLabel $statusLabel -ProgressDetailLabel $progressDetailLabel -ProgressBar $progressBar
    }
    catch {
        $statusLabel.Text = 'Launcher status refresh warning: ' + $_.Exception.Message
    }
})
$statusTimer.Start()

Clear-LauncherStatusFiles -StatusFolder $statusRoot
$statuses = Get-LauncherStatuses -StatusFolder $statusRoot
Update-LauncherUi -Statuses $statuses -Buttons $scriptButtons -StatusLabel $statusLabel -ProgressDetailLabel $progressDetailLabel -ProgressBar $progressBar

[void]$form.ShowDialog()