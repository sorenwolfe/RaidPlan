# RaidPlan

An in-game raid strategy sheet for FFXIV, as a Dalamud plugin. Draw the fight slide by slide,
assign every cooldown to a specific player at a specific point in the pull, and let the plugin
call it out a few seconds before the boss cast lands.

`/raidplan` opens the planner.

---

## What it does

**Slides.** A slide is one drawn arena plus notes. Player tokens show the seat's job icon, so a
board reads at a glance. Drop tokens bound to roster seats,
boss markers, waymarks A–D and 1–4, AoE telegraphs (circle, donut, rectangle, cone, line,
cross), movement arrows, tethers, text labels, and freehand pen strokes. Arena presets cover
circle, square, rectangle at any aspect ratio, octagon and hexagon, with an optional grid,
compass letters and waymark guides.

**Roster.** Eight seats by default, up to twenty-four. A plan is written against *seats* —
MT, H2, R1 — not character names, so swapping a body in doesn't invalidate a single assignment.
"Fill from my party" reads the live party and drops everyone into a matching seat.

**Timeline and assignments.** Each step of the fight is a row: when it happens, which slide
explains it, what each seat presses, and what each player gets told. The action picker searches
the game's own Action sheet and filters to the seat's job, so a Scholar's list is a Scholar's
list. By default it shows cooldowns only — anything on a 30 second or longer recast, plus the
role actions — which is mitigation, utility and burst windows without the rotation. Untick
**Cooldowns only** in the picker for the full kit. Every seat can take several actions on the
same step, each with a note.

**Following the fight.** During a pull the plan drives itself. A boss cast that one of your
steps is anchored to switches the slide the instant it is seen; a step firing its call moves the
plan too. A wipe puts you back on slide one, ready for the re-pull. Changing slides by hand
during a pull parks the automation for a few seconds so it doesn't drag you back.

**Learning the fight.** RaidPlan watches your pulls and remembers when each cast happens. After
three or four attempts it knows the timeline well enough to call a mechanic *before* the boss
starts casting it — which is the only way to get more warning than a cast bar is long.

**Shotcalls.** A step fires a call ahead of the moment it names. The wording is a template with
placeholders, customisable per team profile, per step, and per seat — so the same plan can tell
your H2 "Bell NOW, then Temperance" while everyone else just sees "Akh Morn 1". Calls arrive on a
screen banner, in the chat log, as a Dalamud notification, with a sound, or any combination.

**Sharing.** A plan compresses to a single share code. Copy it, paste it in Discord, everyone
pastes it back. No server, no account, nothing leaves your machine.

---

## How the timing works

This is the part worth understanding, because it's what makes the calls survive a real prog night.

A step is anchored one of four ways:

| Trigger | Fires when | Use it for |
|---|---|---|
| **Boss cast** | The boss actually starts casting the named action; the call lands `lead` seconds before the cast resolves | Anything with a cast bar |
| **Learned timing** | When RaidPlan expects the cast, from your own previous pulls, corrected for how this one is running | Anything you want more warning for than the cast bar gives |
| **After a cast** | `offset` seconds after a named cast begins, minus your lead | Mechanics with no cast bar of their own — a second hit, a delayed tower, a spread that follows a stack |
| **Combat clock** | A plain stopwatch from the pull | Openers, and fights whose timing never varies |
| **Manual** | Never on its own — only the Test button | Steps you call yourself |

**Boss cast** is the one to reach for. The plugin watches every hostile actor for a cast starting,
counts occurrences per action per pull ("the *second* Akh Morn"), and schedules the call against
the cast bar's real end time. That means a slow pull, a fast pull, a phase pushed early, and a
resurrection mid-fight all still line up — none of which is true of a stopwatch.

Lead time is measured **back from the moment the cast resolves**. A 5-second lead on a 4-second
cast simply calls it the instant the cast starts; a cast trigger cannot warn you before the boss
commits. Two triggers get around that: anchor to an *earlier* cast with **After a cast** and a
positive offset, or use **Learned timing** and let the plugin work the timing out itself.

### Learned timings

Turn learning on (it is on by default) and every pull is recorded: each cast, which use of it
this was, and how many seconds into the pull it happened. Across pulls RaidPlan keeps the
**median** of those times — a median rather than an average, so the one attempt where somebody
pulled early doesn't move the whole model.

The part that makes it usable is the correction applied live. Pulls run fast or slow, and a
phase that ends on a health check shifts everything after it. So whenever a cast RaidPlan
recognises actually happens, it measures how far ahead or behind the usual timing this pull is
running, and shifts every later prediction by the same amount. **One confirmed cast re-anchors
the rest of the fight.** Only casts it already knows well are allowed to re-anchor, so a noisy
add spawn can't drag the whole timeline around.

A learned step falls back to **Boss cast** behaviour whenever the timing isn't trusted yet, so it
is never worse than a cast-anchored step — it just gets better as you pull more.

The **Learned** tab shows everything it knows for the current zone: when each cast usually lands,
how much it wanders (`±`), how many pulls back it up, and whether that adds up to *solid*,
*likely*, *rough* or a *guess*. During a pull it also shows how fast this attempt is running and
what it expects next.

**Build a whole timeline in one click.** The Learned tab's generator adds a step for every cast
that clears your pull-count and confidence bars, with a sensible lead time already set. It skips
casts already on your timeline, so it is safe to run again after more pulls have taught it more.
"Add steps and a slide each" also creates a blank slide per mechanic, which is a decent skeleton
to draw into.

### Building a timeline quickly

The fastest route is to let the plugin do it:

1. Pull the fight three or four times with learning on (the default).
2. Open the **Learned** tab and press **Add steps and a slide each**.
3. Draw into the slides it made, and put cooldowns on the seats that need them.

For a single pull, or for casts the learner filtered out, the **Live** tab lists every hostile
cast from the current pull with its time, occurrence number and cast bar length, and an
**Add step** button beside each. Either way the steps are anchored to real action ids rather than
to names you might have spelled differently.

---

## Commands

| Command | Effect |
|---|---|
| `/raidplan` | Toggle the planner (`/rp` also works) |
| `/raidplan config` | Open settings |
| `/raidplan calls` | Toggle live shotcalls |
| `/raidplan next` / `prev` | Step through slides — handy on a hotbar macro during prog |
| `/raidplan follow` | Toggle slides following the fight |
| `/raidplan reset` | Jump back to the first slide |

---

## Call text placeholders

Usable in a step's team-wide line, a seat's personal line, and the team profile's fallback:

| Token | Expands to |
|---|---|
| `{label}` | The step name, e.g. "Akh Morn 1" |
| `{cast}` | The boss cast the step is anchored to |
| `{player}` | The player in the seat this call is for |
| `{job}` | That player's job abbreviation |
| `{seat}` | The seat label, e.g. "H2" |
| `{ability}` | That seat's first assigned action |
| `{abilities}` | All of that seat's assigned actions |
| `{note}` | The note on that seat's assignment |
| `{time}` | The step's timeline position as m:ss |
| `{lead}` | The lead time in seconds |
| `{slide}` | Title of the linked slide |
| `{team}` | The active team profile's name |

A placeholder that has nothing to expand to disappears, and the surrounding punctuation is
tidied up, so `{seat}: {abilities}` degrades gracefully rather than leaving `": "` behind.

---

## Team profiles

The plan is shared; how loudly it talks to *you* is not. Each profile keeps its own delivery
channels, banner size and colours, fallback wording, chat tag, extra lead time, and which seat
this client occupies. Make one per static.

---

## Building it

**Requirements:** .NET 10 SDK, and Dalamud installed via XIVLauncher (the project references the
assemblies from your local Dalamud install — that's how Dalamud plugins are built).

```
dotnet build -c Release
```

The output lands in `RaidPlan/bin/Release/` with a `RaidPlan` folder ready for Dalamud.

**To load it in game:**

1. `/xlsettings` → Experimental → add `…/RaidPlan/bin/Release/RaidPlan/RaidPlan.dll` to
   *Dev Plugin Locations*.
2. `/xlplugins` → Dev Tools → Installed Dev Plugins → enable RaidPlan.
3. `/raidplan`.

If MSBuild can't find the Dalamud assemblies, set `DALAMUD_HOME` to your Dalamud `dev` folder —
on Windows that's `%AppData%\XIVLauncher\addon\Hooks\dev`.

This targets **Dalamud API 15** (`Dalamud.NET.Sdk/15.0.0`, .NET 10). Bump `DalamudApiLevel` in
`RaidPlan.json` and the SDK version in the csproj together when the API moves on.

---

## Where things live

```
RaidPlan/
├── .github/workflows/           CI build, archive checks, tagged release
├── repo.json                    manifest for a custom Dalamud plugin repository
├── images/icon.png              512x512 icon shown in the plugin installer
├── Plugin.cs                    entry point, services, commands
├── Configuration.cs             team profiles and plugin settings
├── RaidPlan.json                Dalamud manifest
├── Model/                       the plan format — plain data, no game types
│   ├── RaidPlanDocument.cs      document, arena settings, role colours
│   ├── FightMemory.cs           learned timings and their confidence
│   ├── Slide.cs, CanvasItem.cs  slides and the things on them
│   ├── PlayerSlot.cs            roster seats
│   ├── TimelineEntry.cs         steps and assignments
│   └── Enums.cs
├── Services/
│   ├── ActionIndex.cs           searchable index over the Action sheet
│   ├── EncounterMonitor.cs      combat clock, live cast detection, wipe detection
│   ├── EncounterLearner.cs      records pulls, learns timings, predicts mechanics
│   ├── TimelinePrediction.cs    the drift and due-time arithmetic, kept testable
│   ├── SlideDirector.cs         decides which slide should be on screen
│   ├── ReminderEngine.cs        scheduling and delivering calls
│   ├── CallTemplate.cs          placeholder expansion
│   ├── RosterResolver.cs        party ↔ seat matching
│   ├── PlanStore.cs             the on-disk plan library
│   ├── PlanJson.cs              serializer settings and the compact Vector2 form
│   └── ShareCode.cs             share code encode/decode
└── UI/
    ├── MainWindow*.cs           the planner, one file per tab
    ├── ArenaCanvas.cs           the arena widget and its interaction
    ├── SpellPicker.cs           searchable action dropdown
    ├── ConfigWindow.cs          settings
    ├── OverlayWindow.cs         the shotcall banner
    └── UiHelpers.cs             drawing helpers and ImGui binding shims
```

Plans are stored as individual JSON files in the plugin's config directory under `plans/`, so
they can be backed up, diffed or hand-edited. Codes exported to a file land in `shared/`, and
learned timings in `learned/`, one file per zone.

---

## Notes and limits

**Share code size.** A code for a typical plan is around 1,000–2,500 characters. Discord cuts
messages off at 2,000, and the Share tab warns you when a plan crosses that line — use
**Save code to a file** and attach the text file instead. The format drops every field still
holding its default, stores points as rounded two-element arrays and gzips the result, which
takes a heavy plan (21 slides, 500 objects) from about 20,000 characters down to 3,800.

**Sync is manual by design.** Nobody's plan changes under them mid-pull, and there's no service
to keep running. The cost is that a plan edited by the raid lead has to be re-shared. Importing
with **Import and overwrite** replaces the stored copy that shares the code's id, so re-importing
an updated plan doesn't leave you with six near-identical copies.

**Cast detection is client-side.** It sees what your client sees: hostile actors in the object
table with a cast bar up. Untelegraphed mechanics, and anything the boss does without a cast,
can't be anchored with **Boss cast** — anchor those to a nearby cast with **After a cast**.

**Occurrence counters reset** on entering combat, on a wipe, and on a duty recommence. If you
need them cleared mid-session for any other reason, the Live tab has a **Reset counters** button.

**Wipe detection** uses the duty's own wipe event where the content has one. Outside that it
falls back on what the client can see: combat ended while you were dead, or something that was
casting at you a moment ago is still standing. That covers the normal cases; an unusual one might
be read as a clear, which only means the plan doesn't reset itself.

**Learning is keyed by zone, not by boss.** For a raid tier — one boss per instance — that is the
same thing. In a multi-boss dungeon all three end up in one list on the Learned tab, ordered by
time. They don't corrupt each other, since each boss has its own action ids and each pull is its
own combat, but the list is longer than you might expect.

**Pulls under 15 seconds, or with fewer than three casts, are discarded** rather than learned
from. That keeps striking dummies and pulls that die on contact out of the data.

**Learned timings are per-patch.** If a fight is retuned, use **Forget these timings** on the
Learned tab and let it relearn from the next pull.

**Sound uses the game's chat sound effects** (`<se.1>`–`<se.16>`), the same ones a macro plays.

---

## Getting it to your static

Dev-plugin loading is fine for you, but nobody wants to walk seven other people through it.
Two easier routes:

**A custom plugin repository.** This makes RaidPlan show up in `/xlplugins` like any other
plugin, with working updates. `repo.json` in this repo is the manifest for it, and your static
adds its raw URL under `/xlsettings` → Experimental → Custom Plugin Repositories:

```
https://raw.githubusercontent.com/sorenwolfe/RaidPlan/main/repo.json
```

### Cutting a release

```bash
# 1. bump the version in RaidPlan/RaidPlan.csproj and in repo.json (AssemblyVersion)
# 2. commit, then tag it
git commit -am "Release v0.1.1"
git tag v0.1.1
git push origin main --tags
```

The release workflow builds the plugin, checks the archive, and attaches it to a GitHub release
as `RaidPlan.zip`. The download URL in `repo.json` points at
`releases/latest/download/RaidPlan.zip`, which never changes — so the only thing you edit
between releases is `AssemblyVersion`, and that is what tells everyone an update exists.

### If the installer says "Failed to install plugin"

That message means Dalamud could not fetch or unpack what `DownloadLinkInstall` points at. In
order of likelihood:

1. **The URL 404s.** Paste `DownloadLinkInstall` into a browser — you should get a download, not
   a GitHub 404 page. A release with no matching asset, or a placeholder left in the URL, both
   look like this.
2. **The archive has the wrong shape.** Dalamud expects `RaidPlan.dll` and `RaidPlan.json` at the
   *root* of the zip. DalamudPackager already produces exactly this at
   `RaidPlan/bin/Release/RaidPlan/latest.zip` after a Release build — upload that file, don't
   build your own zip from the output folder, or you end up shipping an archive containing an
   archive. Both CI workflows check this now.
3. **The versions disagree.** `AssemblyVersion` in `repo.json` should match the version the DLL
   was built with.

The plugin appearing in the list with its name, icon and description only proves `repo.json` was
read. Installing is a separate fetch, and fails separately.

---

## Licence

AGPL-3.0-or-later, matching the Dalamud sample plugin. The full text is in `LICENSE`.
