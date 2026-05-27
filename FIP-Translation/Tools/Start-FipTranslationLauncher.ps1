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

$scriptMap = [ordered]@{
    'Part 1 English Sync' = Join-Path $translationRoot 'Part 1 Updater\English Sync\Run-EnglishSync.ps1'
    'Part 1 Language Translation' = Join-Path $translationRoot 'Part 1 Updater\Language Translation\Run-LanguageTranslation.ps1'
    'Part 2 English Sync' = Join-Path $translationRoot 'Part 2 Updater\English Sync\Run-EnglishSync.ps1'
    'Part 2 Language Translation' = Join-Path $translationRoot 'Part 2 Updater\Language Translation\Run-LanguageTranslation.ps1'
    'Part 3 English Sync' = Join-Path $translationRoot 'Part 3 Updater\English Sync\Run-EnglishSync.ps1'
    'Part 3 Language Translation' = Join-Path $translationRoot 'Part 3 Updater\Language Translation\Run-LanguageTranslation.ps1'
    'Part 4 English Sync' = Join-Path $translationRoot 'Part 4 Updater\English Sync\Run-EnglishSync.ps1'
    'Part 4 Language Translation' = Join-Path $translationRoot 'Part 4 Updater\Language Translation\Run-LanguageTranslation.ps1'
}

function Start-LauncherScript {
    param(
        [string]$Label,
        [string]$ScriptPath,
        [System.Windows.Forms.Label]$StatusLabel
    )

    if (-not (Test-Path -LiteralPath $ScriptPath)) {
        [System.Windows.Forms.MessageBox]::Show(
            "Script not found:`r`n$ScriptPath",
            'FIP Translation Launcher',
            [System.Windows.Forms.MessageBoxButtons]::OK,
            [System.Windows.Forms.MessageBoxIcon]::Error
        ) | Out-Null
        return
    }

    $StatusLabel.Text = "Started: $Label"
    Start-Process -FilePath 'powershell.exe' -ArgumentList @(
        '-NoProfile',
        '-ExecutionPolicy', 'Bypass',
        '-NoExit',
        '-File', $ScriptPath
    ) -WorkingDirectory (Split-Path -Parent $ScriptPath) | Out-Null
}

$form = New-Object System.Windows.Forms.Form
$form.Text = 'FIP Translation Launcher'
$form.StartPosition = 'CenterScreen'
$form.Size = New-Object System.Drawing.Size(760, 420)
$form.MinimumSize = New-Object System.Drawing.Size(760, 420)
$form.MaximizeBox = $false

$titleLabel = New-Object System.Windows.Forms.Label
$titleLabel.Text = 'Translation Updater Launcher'
$titleLabel.Location = New-Object System.Drawing.Point(20, 15)
$titleLabel.Size = New-Object System.Drawing.Size(700, 28)
$titleLabel.Font = New-Object System.Drawing.Font('Segoe UI', 14, [System.Drawing.FontStyle]::Bold)
$form.Controls.Add($titleLabel)

$subtitleLabel = New-Object System.Windows.Forms.Label
$subtitleLabel.Text = 'Click a button to launch that updater in a separate PowerShell window.'
$subtitleLabel.Location = New-Object System.Drawing.Point(20, 48)
$subtitleLabel.Size = New-Object System.Drawing.Size(700, 20)
$form.Controls.Add($subtitleLabel)

$statusLabel = New-Object System.Windows.Forms.Label
$statusLabel.Text = 'Ready.'
$statusLabel.Location = New-Object System.Drawing.Point(20, 335)
$statusLabel.Size = New-Object System.Drawing.Size(700, 20)
$form.Controls.Add($statusLabel)

$openConfigButton = New-Object System.Windows.Forms.Button
$openConfigButton.Text = 'Open Config'
$openConfigButton.Location = New-Object System.Drawing.Point(20, 290)
$openConfigButton.Size = New-Object System.Drawing.Size(120, 32)
$openConfigButton.Add_Click({
    Start-Process -FilePath (Join-Path $toolsRoot 'config.json') | Out-Null
    $statusLabel.Text = 'Opened config.json'
})
$form.Controls.Add($openConfigButton)

$openReadmeButton = New-Object System.Windows.Forms.Button
$openReadmeButton.Text = 'Open README'
$openReadmeButton.Location = New-Object System.Drawing.Point(150, 290)
$openReadmeButton.Size = New-Object System.Drawing.Size(120, 32)
$openReadmeButton.Add_Click({
    Start-Process -FilePath (Join-Path $toolsRoot 'README.md') | Out-Null
    $statusLabel.Text = 'Opened README.md'
})
$form.Controls.Add($openReadmeButton)

$buttonWidth = 320
$buttonHeight = 42
$leftColumnX = 20
$rightColumnX = 390
$startY = 90
$verticalGap = 12

$index = 0
foreach ($entry in $scriptMap.GetEnumerator()) {
    $button = New-Object System.Windows.Forms.Button
    $button.Text = $entry.Key
    $columnX = if ($index % 2 -eq 0) { $leftColumnX } else { $rightColumnX }
    $rowIndex = [int][Math]::Floor($index / 2)
    $button.Location = New-Object System.Drawing.Point($columnX, ($startY + (($buttonHeight + $verticalGap) * $rowIndex)))
    $button.Size = New-Object System.Drawing.Size($buttonWidth, $buttonHeight)

    $buttonLabel = $entry.Key
    $buttonPath = $entry.Value
    $button.Add_Click({
        Start-LauncherScript -Label $buttonLabel -ScriptPath $buttonPath -StatusLabel $statusLabel
    })

    $form.Controls.Add($button)
    $index++
}

[void]$form.ShowDialog()