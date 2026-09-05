$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Add-Type -Path "$root/Shikari/UI/Theme/NavigationMotion.cs"
function Assert($ok, $message) { if (!$ok) { throw $message } }
$motion = [Shikari.UI.Theme.NavigationMotion]::new()
Assert ($motion.Update(0,0) -eq 0) 'Initial selection must snap without an entrance animation'
$middle = $motion.Update(2,0.08)
Assert ($middle -gt 0 -and $middle -lt 2) 'Half-duration selection must be in flight'
Assert ($motion.Update(1,0) -eq $middle) 'Rapid retarget must preserve its current position'
Assert ($motion.Update(1,0.16) -eq 1) 'Animation must settle in 160ms'
Assert ($motion.Update(0,[float]::NaN) -eq 1) 'Invalid delta must not corrupt animation'
Assert ($motion.Update(0,1) -eq 0) 'A long frame must finish, not overshoot'
$slow = [Shikari.UI.Theme.NavigationMotion]::new()
$fast = [Shikari.UI.Theme.NavigationMotion]::new()
$null = $slow.Update(0,0); $null = $fast.Update(0,0)
1..3 | ForEach-Object { $null = $slow.Update(2,1.0/30) }
1..6 | ForEach-Object { $null = $fast.Update(2,1.0/60) }
Assert ([Math]::Abs($slow.Position-$fast.Position) -lt 0.0001) 'Motion must be independent of frame rate'
Write-Host 'PASS: 7 navigation animation checks'
