$ErrorActionPreference = 'Stop'
Add-Type -Path "$PSHOME/Microsoft.CodeAnalysis.dll"
Add-Type -Path "$PSHOME/Microsoft.CodeAnalysis.CSharp.dll"
$root = Split-Path $PSScriptRoot -Parent
$files = @(Get-ChildItem "$root/Shikari" -Recurse -Filter '*.cs')
$errors = @()
foreach ($file in $files) {
    $tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText($file.FullName))
    foreach ($diagnostic in $tree.GetDiagnostics()) {
        if ($diagnostic.Severity -eq 'Error') { $errors += "$($file.Name): $diagnostic" }
    }
}
if ($errors.Count) { throw ($errors -join [Environment]::NewLine) }
Write-Host "PASS: $($files.Count) C# files parsed without syntax errors (not a Dalamud binding/type check)"
