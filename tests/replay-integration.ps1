$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$sources = @(Get-ChildItem "$root/Shikari/Model/*.cs" | ForEach-Object FullName)
$sources += "$root/Shikari/Services/PlanJson.cs", "$root/Shikari/Services/Storage/AtomicFile.cs"
$sources += @(Get-ChildItem "$root/Shikari/Services/Replay/*.cs" | ForEach-Object FullName)
$sources += "$PSScriptRoot/ReplayIntegrationStubs.cs"
$refs = @(Get-ChildItem "$PSHOME/ref/*.dll" | ForEach-Object FullName) + "$PSHOME/Newtonsoft.Json.dll"
Add-Type -Path $sources -ReferencedAssemblies $refs -CompilerOptions '/nullable:enable','/nowarn:1701'
$temp = Join-Path ([IO.Path]::GetTempPath()) ('shikari-replay-' + [guid]::NewGuid().ToString('N'))
try { [Shikari.Tests.ReplayIntegration]::Run($temp) }
finally {
    if ((Test-Path -LiteralPath $temp) -and ([IO.Path]::GetFullPath($temp)).StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath()))) {
        Remove-Item -LiteralPath $temp -Recurse -Force
    }
}
