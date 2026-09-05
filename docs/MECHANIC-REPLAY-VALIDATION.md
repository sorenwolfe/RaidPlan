# Mechanic replay validation

This branch was developed in an isolated writable clone of `D:/Shikari`, based on `d9eea66`.
The original checkout was not modified. No release was published or DLL installed.

## Automated checks

Run each script in a separate PowerShell 7 process (the regression assemblies share type names):

```powershell
pwsh -NoProfile -File tests/replay.ps1
pwsh -NoProfile -File tests/replay-integration.ps1
pwsh -NoProfile -File tests/reliability.ps1
pwsh -NoProfile -File tests/syntax.ps1
dotnet build Shikari/Shikari.csproj --configuration Release
```

The scripts use PowerShell's bundled Roslyn compiler and reference assemblies. They compile
actual production source. Integration tests substitute the game service boundary; they do not
validate the Dalamud bindings or render ImGui. CI runs these checks before its existing build.

Observed locally: 27 replay assertions passed; recording, disk persistence, reload, duplicate
end/wipe, zone change and clear tests passed; import bounds, atomic save failure/retry, and
pinned seat regressions passed. All 77 plugin C# files parsed without syntax errors.

Full plugin build is **not verified**: this machine exposes .NET SDK 5.0.202 and lacks
`Dalamud.NET.Sdk/15.0.0`. Network access prevents dependency download. The available PowerShell
runtime is .NET 10.0.11, which enables the isolated regression tests but is not a replacement
for the required SDK and Dalamud reference assemblies.

## In-game acceptance

1. Build with .NET 10 and Dalamud API 15 references. Load the development DLL through Dalamud.
2. Open an existing plan and verify Plan / Live / Review at default scale, 150%, and 200%.
   Resize to minimum width; verify compact Arena / Slides / Inspector tabs are reachable.
3. Verify slate theme and custom accent; disable theme and verify usable default controls.
4. Drag a token, edit notes, delete a shape and undo/redo each action. Switch plans and import
   an overwrite; verify undo cannot restore another document's history.
5. Place at least three matching waymarks. Pin a seat with duplicate jobs present. Confirm
   that callouts, your live ring and your highlighted planned spot refer to the same seat.
6. Configure a checkpoint on a known boss cast, link a slide and place one player token per
   assessed seat. Close all plugin windows and pull. Wipe, then open `/shikari review`.
7. Verify one attempt, observed mechanic anchors, play/pause, 0.25–2x speed, scrubbing, trails
   and seat focus. Confirm the live mini window does not follow review scrubbing.
8. Remove alignment during a pull. The replay must display gaps and stop movement trails at
   them; no distance observation should be available at an unaligned checkpoint.
9. Edit the plan after a pull, including waymarks. Verify the old attempt remains unchanged.
   Compare a new pull: overlays must be hidden when the recorded diagrams differ.
10. Change territory and unload/reload the plugin; verify recordings close and retained attempts
    reload. Disable recording; verify no new attempt begins. Reduce retention and verify deletion.
11. Verify mini-window mouse clickthrough during combat and drag/resize out of combat.
12. Make the plan file temporarily unwritable, edit, and verify the error remains visible and
    changes remain pending. Restore access; verify the next save succeeds.

## Scope and limits

- Distances are observations against authored checkpoints, not mechanic success/failure labels.
- No server-confirmed damage snapshots, encounter-specific branching, or pathfinding is added.
- Sampling is bounded and conservative; positions are held at most 250ms, with no interpolation
  through missing frames. This is a training replay, not a frame-perfect combat log.
- Capture follows the existing encounter monitor's combat lifecycle and observed active slide.
- The UI has source and syntax review but needs the in-game layout checks above before release.
