param(
    [string]$RepositoryRoot = (Join-Path $PSScriptRoot '..\..\..\..'),
    [string]$RimWorldManagedDir = 'D:\Steam\steamapps\common\RimWorld\RimWorldWin64_Data\Managed',
    [string]$ReferenceAssemblyDirectory = (Join-Path $env:USERPROFILE '.nuget\packages\microsoft.netframework.referenceassemblies.net472\1.0.3\build\.NETFramework\v4.7.2')
)

$ErrorActionPreference = 'Stop'
# Compile with the SDK compiler and the mod build's cached framework references.
# No package restore, Unity player, saved game or user configuration is needed.
$sdkLine = (& dotnet --list-sdks | Select-Object -Last 1)
if ($sdkLine -notmatch '^([^ ]+) \[(.+)\]$') { throw 'No .NET SDK found.' }
$compiler = Join-Path $Matches[2] ($Matches[1] + '\Roslyn\bincore\csc.dll')
$outputDirectory = Join-Path $PSScriptRoot 'bin'
[void][IO.Directory]::CreateDirectory($outputDirectory)
$executable = Join-Path $outputDirectory 'ModOptionsRegression.exe'
$arguments = [Collections.Generic.List[string]]::new()
foreach ($arg in @('/nologo', '/nostdlib+', '/langversion:latest', '/target:exe')) { $arguments.Add($arg) }
$arguments.Add('/out:"' + $executable + '"')
foreach ($name in @('mscorlib.dll', 'System.dll', 'System.Core.dll', 'System.Xml.dll')) {
    $arguments.Add('/reference:"' + (Join-Path $ReferenceAssemblyDirectory $name) + '"')
}
foreach ($name in @('Assembly-CSharp.dll', 'UnityEngine.CoreModule.dll')) {
    $arguments.Add('/reference:"' + (Join-Path $RimWorldManagedDir $name) + '"')
}
$arguments.Add('"' + (Join-Path $PSScriptRoot 'Program.cs') + '"')
$responseFile = Join-Path $outputDirectory 'compile.rsp'
[IO.File]::WriteAllLines($responseFile, $arguments)
& dotnet $compiler "@$responseFile"
if ($LASTEXITCODE -ne 0) { throw 'Regression runner compilation failed.' }
& $executable ([IO.Path]::GetFullPath($RepositoryRoot)) $RimWorldManagedDir
if ($LASTEXITCODE -ne 0) { throw 'Mod-option regression checks failed.' }
