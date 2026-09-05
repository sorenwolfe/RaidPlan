# Adaptive mechanics implementation plan

Goal: Let a player author cast-scoped, local-status branches which select a slide and retain decision evidence in replay.

Architecture: Pure status-difference tracker and deterministic rule evaluator; a framework-owned service reads Dalamud status snapshots, freezes the plan at pull start and dispatches decisions through SlideDirector. UI edits declarative rules; replay stores evidence independent of position alignment.

Scope: Positive local status matches, initial observed duration ranges, optional parameter, territory and cast occurrence, bounded acquisition window. No absence inference, party assignment solver, unverified encounter presets or automatic interpretation of WTFDIG prose. WTFDIG import is a subsequent independent increment.

Constraints: New rules disabled initially and on import; manual slide suppression and auto-follow preferences respected. Missing data never selects a branch. Conflicting matches produce evidence but no navigation. Status observations from before arming cannot choose a branch. Freeze duration and detect refreshes. No disk/network work each frame. Schema version 2 must be explicit for plans carrying adaptive rules so older plugins reject rather than ignore them.

1. Add failing pure tests for duration capture, refresh, missing snapshots, scope, occurrence, conflicts, timeout and repeated pulls.
2. Implement model, tracker and evaluator, with bounded histories and input validation.
3. Integrate framework lifecycle, slide routing, replay evidence and tests using game-service stubs.
4. Add Adaptive editor and discovery UI, live decision text and replay evidence timeline.
5. Verify share round trips, old plans, malformed rules, syntax and lifecycle regressions. Produce incremental patch checked against D:/Shikari and in-game validation instructions.

Validation limit: No verified live encounter recording is available in this workspace. Synthetic tests verify rules, not a named encounter strategy. Full Dalamud build and in-game acceptance must be clearly distinguished from pure/stub tests.
