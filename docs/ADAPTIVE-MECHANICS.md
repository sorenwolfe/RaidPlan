# Adaptive mechanics

The Adaptive page adds local-status branches to a plan. An enabled rule waits for a specific boss cast in a specific territory, then watches for newly observed statuses or refreshes on your character. Exactly one matching branch selects its linked slide. Rules are captured at pull start; editing is disabled during combat.

This release provides a generic rule editor and status discovery. It does not ship verified encounter presets, import WTFDIG guides, resolve party-wide priorities, or detect tethers/head markers. Do not assume a guide's prose is an executable mechanic rule.

## Set up a rule

1. Make slides for the possible assignments, including your planned player token on each slide.
2. Keep replay recording enabled and observe a pull. Open Plan > Adaptive > Observed assignments to inspect newly acquired local status IDs, initial observed durations and parameters. Earlier evidence remains in Review if recording was enabled. Existing statuses on the first readable snapshot form a baseline, not new assignments.
3. After the pull, add a status mechanic. Use the current territory and select the relevant assignment cast from observed casts. Set its occurrence (0 means every use) and an acquisition window measured from cast START, including the cast's duration.
4. Add a branch for each outcome. Choose an observed status, its parameter (-1 accepts any parameter), duration range and destination slide. Minimum duration is inclusive; maximum is exclusive. Use ranges around expected durations to accommodate observation delay, without overlapping branches. A parameter's meaning depends on the status; it is not universally a stack count.
5. Enable the rule once verified. Enable slide following in Shikari to let it navigate. Imported rules are always disabled until reviewed.

## Behavior and limits

- Initial duration means the remaining duration at the first observed acquisition/refresh, with approximately 100 ms polling. A countdown alone never changes a long assignment to a short one.
- Matches settle for 300 ms to collect competing observations, or until the assignment window expires. Conflicting matches and timeouts record a reason without selecting a slide. Signals arriving outside that settling interval cannot retroactively change a decision.
- A missing player/status snapshot is unknown. Reconnection establishes a new baseline; the plugin does not infer a missing debuff branch.
- Refresh detection uses a duration increase of more than one second, a changed parameter, or a newly present status/source pair. Very brief effects and same-duration reapplications between samples may not be distinguishable.
- Rules run once per matching cast occurrence. A later matching cast rearms them. Changing the active plan or territory ends the current evaluation. Wipes and new pulls reset state.
- Manual slide overrides and the follow setting are respected. Held navigation is recorded and is not applied later automatically.
- A generic delayed timeline call for the same cast occurrence does not overwrite an applied adaptive slide. Calls themselves are unchanged; this release selects slides and uses their existing position guidance and notes.
- Keep only one adaptive mechanic rule for a given territory/cast occurrence and express alternatives as its branches. Overlapping enabled rules are excluded from evaluation, including an every-use rule overlapping a specific occurrence. The editor prevents enabling overlaps. Separate rules are not a party-wide constraint solver.
- Review includes status observations and decision explanations. Authored checkpoints use the recorded applied branch for their cast occurrence. An unresolved or held adaptive assignment has no inferred checkpoint distance. Review does not re-run rules against your current plan.
- The discovery list keeps 256 observations, replay keeps up to 4,096 observations and 1,024 decisions per attempt. Old replay files remain readable.

Plans carrying adaptive rules explicitly use plan format 2. Older Shikari builds reject these plans instead of silently losing their rules. Plans without adaptive rules can still use format 1. The RPLAN1/RPLAN2 compression prefix is separate from this plan-schema version.

## Validation

Run each script in a separate PowerShell 7 process:

```powershell
pwsh -NoProfile -File tests/adaptive.ps1
pwsh -NoProfile -File tests/adaptive-runtime.ps1
pwsh -NoProfile -File tests/sharing.ps1
pwsh -NoProfile -File tests/replay.ps1
pwsh -NoProfile -File tests/replay-integration.ps1
pwsh -NoProfile -File tests/syntax.ps1
dotnet build Shikari/Shikari.csproj --configuration Release
```

The runtime test compiles the real adaptive service and slide director against game-service stubs. It is not a Dalamud API binding test. Validate in game with both assignment outcomes, manual hold, follow off, a wipe, and a missing/ambiguous observation before relying on a rule. Check Review after each pull and confirm the recorded status, duration and destination match what happened.
