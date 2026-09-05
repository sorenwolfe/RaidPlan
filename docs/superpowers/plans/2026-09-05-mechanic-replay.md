# Mechanic replay implementation plan

**Goal:** Deliver local mechanic replay and a distinct Plan / Live / Review UI, preserving imports and existing combat guidance.

**Architecture:** Snapshot the active plan and resolved roster at pull start. A bounded recorder samples on framework updates independently of windows, stores alignment validity per frame, and captures cast anchors. Review renders snapshot slides and recorded positions without changing live slide state. Optional per-step checkpoints report distance against authored timing, never claim mechanic success.

**Tech stack:** C#, Dalamud API 15, ImGui, Newtonsoft.Json. No added runtime packages or hosted services.

**Approved spec:** User approved mechanic replay and the Plan / Live / Review design described in this task, including reliability fixes. Make routine design decisions without further approval.

## Constraints
- D:/Shikari is read-only in this task. Implement in this isolated clone and deliver a patch and source archive.
- Network is blocked; only .NET 5 SDK exists. PowerShell includes .NET 10 and Roslyn: compile and execute pure logic regression tests there; do not claim a Dalamud build passed.
- Bound samples, duration, retained attempts, file reads, and decompressed imports.
- No interpolation across missing data, different slides, or roster identities. Store a snapshot with every attempt.
- Checkpoints default disabled for old plans; unknown data remains unknown.
- Preserve theme opt-out, UI scaling, all import/export tools, combat clickthrough.

## Tasks
- [x] Reliability: reproduce pinned seat, oversized gzip, and failed-save behavior; bound decoder; centralize atomic writes and retry state; test fixes.
- [x] Recording core: pure sample/buffer/playback/checkpoint models; test sample throttling, bounds, invalid gaps, distances, duplicate finish and frozen snapshots. Integrate framework capture, durable retention and teardown.
- [x] Workspace UI: branded header and Plan / Live / Review navigation; accessible plan tools, mechanic context, undo/redo and live status. Preserve existing editor operations.
- [x] Review UI: attempt and mechanic selection, scrub/play/speed, trails, seat focus, checkpoint observation, comparison, edit jump, retention controls and empty/error states.
- [x] Verify locally: regression harnesses, Roslyn syntax checks, and diff checks pass. Plugin build attempted and blocked by unavailable SDK. Package source and patch with the in-game validation checklist; build and runtime validation remain release gates.

## Execution ledger
Ruling: use a separate clone in writable workspace because repository access is read-only. No writes to D:/Shikari.
Ruling: approved chat design is the binding spec; proceed directly with implementation.
Ruling: independent UI implementation may be delegated under subagent-driven-development skill; parent owns recording and tests.

| Shared interface | Contract |
| --- | --- |
| Workspace / Review | MainWindow.DrawReviewWorkspace(), MainWindow.OpenReview(), private workspace integer 0 Plan, 1 Live, 2 Review |
| Recorder / Review | Plugin.Replays; ReplayStore.Attempts; ReplayAttempt.Plan, Frames, Mechanics, Duration, StartedUtc, Id, LocalSlot, EndReason |
| Frames / canvas | ReplayFrame.Time, SlideId, Valid, BoardPerYalm, Players; ReplayPlayer.Name, JobId, SlotIndex, Board, IsLocal |
| Checkpoint / editor | TimelineEntry.ReviewCheckpointEnabled, ReviewOffsetSeconds (relative to expected cast end), ReviewRadiusYalms |

Each task has separate owned files; MainWindow integration changes are communicated before edits.

Review: independent agent found idle-frame serialization, missing edit-jump context, comparison
across different diagrams, and mutable mechanic anchors. All four corrected; anchor ownership
regression reproduced failing before the fix. Further agent turns hit usage limits, so final
integration review continued locally. No usage reset consumed.

Verification: pure replay and adapter regression suites pass. Full build blocked by missing SDK15
and .NET10 SDK; document this as an outstanding release gate, not a successful build.
