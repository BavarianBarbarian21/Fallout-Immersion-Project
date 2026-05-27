Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$outPath = 'C:\Users\Matthias\Desktop\Fallout Immersion Project\Tools\TranslationSync\state\powershell-processes.json'
Get-CimInstance Win32_Process |
    Where-Object { $_.Name -eq 'powershell.exe' } |
    Select-Object ProcessId, ParentProcessId, CommandLine |
    ConvertTo-Json -Depth 5 |
    Set-Content -LiteralPath $outPath -Encoding UTF8
