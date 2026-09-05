# Shikari

**Your static's raid plan, inside the game.**

Draw the fight out slide by slide, tell each player which cooldown to press and when, and Shikari
shouts the call at them a few seconds before the boss cast lands.

No second monitor. No alt-tabbing to a website in the middle of a pull.

Type `/shikari` to open it.

## Plan, Live, Review

The studio now has three workspaces. **Plan** keeps the drawing tools, timeline, roster,
learning, import and sharing together. Pick a mechanic to move to its linked slide, then edit
its timing and assignments. Undo and redo group a drag or text edit into one change.
**Live** shows the current strategy and arena alignment. **Review** opens the mechanic replay.

### Mechanic replay

Shikari records locally while you are in combat, even with its windows closed. After the pull,
open **Review** or type `/shikari review`. Choose an attempt, select a mechanic, and play or scrub
through the recorded positions over the plan. Filled tokens are planned positions; hollow rings
are recorded players. Trails show the last five seconds. Choose another attempt to compare the
same mechanic relative to its cast start. When the saved diagrams differ, the comparison overlay
is hidden; checkpoint distances still use each attempt's own plan.

Each recording includes the plan as it was at pull start, party names and positions, and observed
cast anchors. Editing today's plan does not rewrite yesterday's replay. Missing alignment is
shown as a gap. It never fills missing movement with a guessed route.

For distance observations, select a timeline step in **Plan → Timeline**, enable **Review
checkpoint**, and set its timing offset and target radius. Put one token per seat on the linked
slide. Checkpoints are opt-in: old plans remain usable, and a drawing alone is never graded as
a mechanic failure. Cast endpoints are expected times from the observed cast bar, not confirmed
damage snapshots. Configure checkpoints before the pull; replay keeps the saved configuration.

Under **Review → Recording & storage**, turn recording off, choose 1–30 retained attempts
(10 by default), or delete recordings. A pull is limited to 30 minutes at up to ten samples per
second. Replays stay in the plugin's `replays/` config folder and are not included in share codes.

The redesigned studio uses a slate and sea-glass palette, a compass identity, and persistent
alignment status in the small combat window. Existing custom accent and theme preferences remain.

---

## Installing it

You need XIVLauncher with Dalamud. If you already use any plugins, you have it.

Shikari isn't in the official plugin list yet, so you add it by hand the first time. It takes
about a minute, and you only ever do it once.

1. In game, type `/xlplugins` and press Enter. The plugin installer window opens.
2. Click **Settings** at the bottom-left of that window.
3. Click the **Experimental** tab along the top.
4. Scroll to **Custom Plugin Repositories**. There's an empty text box at the bottom of the list.
   Paste this into it:

   ```
   https://raw.githubusercontent.com/sorenwolfe/Shikari/main/repo.json
   ```

5. Click the **+** button next to the box.
6. Click **Save and Close**.
7. You're back at the plugin list. Type `raid` in the search box at the top. **Shikari** will
   appear — click **Install**.

Now type `/shikari` and the planner opens.

Everyone in your group does this once. After that, updates work like every other plugin: an
**Update** button appears in `/xlplugins` when there's a new version.

---

## What it does

**Draw the fight.** Each slide is one moment of the fight — an arena with your party on it. Drop
player tokens, waymarks, boss markers, AoE shapes (circles, donuts, cones, lines), arrows, tethers,
text and freehand drawing. Pick the arena shape to match the real one. Players show up with their
job icon, so a board reads at a glance. Hover any tool to see what it does.

**Zoom in for the fiddly bits.** Roll the mouse wheel over the arena to zoom in where the cursor
is, or use the magnifier buttons at the top. Hold the middle mouse button to drag the view around.
The button next to them puts the whole arena back.

**Only your own instruction, by default.** On the small in-fight window the other seven players
and their movement are faded down so the thing you need is the thing you see first. Nothing is
hidden — you can still check on the person next to you — and every shape describing the mechanic
itself stays fully lit, because that's what you're dodging. One switch in settings turns it off.

**See where you actually are.** This is the one that helps most if you're new. Once your group
has placed the waymarks, Shikari works out how the arena lines up with the plan and draws
everyone's real position on the board as a hollow ring — with a dashed line from you to the spot
the plan gives you. You don't have to work out which way "north-east tower" is while something is
casting at you; you can see the move. Your spot pulses gold until you reach it, then goes solid,
so you get the confirmation without reading anything. It only ever *shows* you things — it never
moves you and never places a marker.

**Copy the arena's waymarks in one click.** On the Slides tab, **Copy the arena's waymarks** drops
the real ones straight onto your slide, in the right shape and facing the right way. It saves
placing eight markers by hand, and it's what makes the position display above line up properly.

**Everyone gets their own instructions.** You build the plan around *positions* — MT, H2, R1 —
not names. Give each position a job, and Shikari shows you that job's real spell list to pick
from. So you can say "H2 presses Bell here", and the person playing H2 that night is the one who
gets told, whoever it is.

**It calls things out before they happen.** A few seconds before the boss cast lands, the player
who has a job to do gets told what to press. It can appear as a banner on screen, in your chat
log, as a pop-up notification, with a sound, **read out loud**, or any mix of those. Everyone
chooses how loud it is for them — that setting is yours, not the plan's.

**It can just tell you.** Turn on **Read out loud** in settings and Shikari speaks the call using
Windows' own voice, with speed, volume and voice to choose from and a button to hear it. Worth
trying even if you can read the banner fine — you don't have to look away from the boss to hear
it, which is the whole problem with reading anything mid-mechanic.

**The slides follow the fight on their own.** When the boss starts casting something your plan
knows about, the plugin flips to the right slide by itself. Nobody has to click through it while
dodging. Wipe, and it goes back to slide one ready for the next pull. Click to a different slide
yourself and it'll leave you alone for a few seconds.

**It learns your fights.** Shikari quietly watches your pulls and remembers when each thing
happens. After three or four attempts it knows the fight well enough to warn people *before* the
boss even starts casting — which is the only way to get more notice than a cast bar gives you.
It also handles pulls that run fast or slow, so the timings don't drift.

**Build the timeline for you.** Once it's watched a few pulls, one button on the **Learned** tab
lays out the whole fight as a list of steps, with a blank slide for each mechanic. You just draw
into them.

**Import from FF Logs.** Paste a link to a log and Shikari pulls the fight's timeline straight
in — and if the jobs match up, it can even copy the cooldowns those players actually pressed onto
the right mechanics. This one needs a free key from FF Logs (they don't allow anonymous access);
only the person building the plan needs it, not the whole group. The key is checked the moment you
paste it in, so a typo tells you straight away rather than on your first import, and you can change
it later in settings.

**A small window for during the fight.** In raids, a compact copy of the current slide appears on
screen, about the size of your minimap. Drag it wherever you like. It shows the same slide the
planner is on, so it keeps up with the fight by itself, and it puts a gold ring around *your* spot —
found by your character name, or by your job when it only appears once in the plan. Whatever's
written on the slide shows underneath it, so you can read the call without opening the planner.
While you're in combat it ignores the mouse completely, so it can never swallow a click meant for
the game.
Hover it out of combat to move it, drag the bottom-right corner to resize it, or close it —
`/shikari mini` brings it back.

**It looks like it belongs there.** Dark panels, one accent colour you can change, soft shadows,
and icons on the drawing tools. The headings use the game's own font rather than a generic one.
If you'd rather it matched your other plugins, one switch in settings turns the whole thing off.

**Bring in a plan from raidplan.io.** Paste a link and Shikari rebuilds it: every slide, the
party tokens on the right seats with the roles they were drawn as, waymarks, AoE shapes and
arrows. Text boxes become the slide's notes. If the notes hold a fight timeline it reads that too,
so the steps and slide names come across as well. It comes in as a new plan, so nothing you
already have is touched.

**Trace a plan you already have.** Got a strat as a screenshot — from a website, a Discord post, a
photo of someone's whiteboard? Drop the picture behind the arena, draw your own slide on top of it,
then fade it out. Much faster than rebuilding from memory, and it works with any source. The
picture stays on your computer and isn't sent with the share code.

**Share it with a code.** A finished plan turns into one long code. Copy it, paste it in Discord,
everyone else pastes it back in. Nothing is uploaded anywhere and nobody needs an account.

---

## Making your first plan

1. `/shikari` to open it.
2. On the **Roster** tab, set the jobs your group is running.
3. Pull the fight three or four times, normally. Shikari is watching.
4. Open the **Learned** tab and click **Add steps and a slide each**. You now have a skeleton of
   the whole fight.
5. Go through the slides and draw what people should do.
6. On the **Timeline** tab, give each position the cooldown they should press.
7. **Share** tab → copy the code → paste it in your group's Discord.

You can skip straight to step 5 and build everything by hand if you'd rather.

---

## Commands

| Type this | What happens |
|---|---|
| `/shikari` | Opens and closes the planner (`/rp` works too) |
| `/shikari config` | Opens the settings |
| `/shikari next` / `prev` | Move a slide forward or back — good on a hotbar macro |
| `/shikari calls` | Turns the spoken calls on and off |
| `/shikari follow` | Turns automatic slide-changing on and off |
| `/shikari mini` | Shows or hides the small in-fight window |
| `/shikari reset` | Jumps back to the first slide |

---

## Changing what the calls say

Every call is just text you can rewrite, in settings or on the step itself. Drop these in and they
fill themselves in: `{player}`, `{job}`, `{seat}`, `{ability}`, `{label}`, `{cast}`, `{time}`.

So `{seat}: {ability} NOW` becomes **H2: Bell NOW** on the healer's screen, and the tank sees their
own line instead. Anything that has nothing to fill in just disappears, so the wording never comes
out broken.

---

## If something isn't working

**Shikari doesn't show up in the plugin list.** The repository link probably didn't save. Go back
to `/xlplugins` → Settings → Experimental and check the URL is there and spelled exactly right.

**An update fails to install.** Restart the game and try again. If it still fails after a restart,
open an issue and mention what version you're coming from.

**An imported plan looks too big or too small.** The scale is taken from the plan's waymarks,
which is reliable as long as all eight are placed. If a plan doesn't use them, it falls back to
guessing from the drawing, and that can come out small.

**FF Logs importing won't connect.** Open settings and look at the FF Logs section — it says
whether the key works. "Forget them" clears both boxes so you can paste a fresh pair.

**Live positions aren't showing.** The Slides tab says why, in the Waymarks box. Usually it's one
of three things: no waymarks are placed in the duty, the plan has none drawn on it, or the two
don't match closely enough to trust. **Copy the arena's waymarks** fixes the last two at once.

**Nothing is being read out.** Windows speech has to be present on the machine. If it isn't, the
settings panel says so where the speed and volume controls would be.

**Calls aren't firing.** Check `/shikari calls` is on, and that the step is actually anchored to a
boss cast rather than set to Manual.

**It's not learning the fight.** Very short pulls are ignored on purpose, so striking dummies and
instant deaths don't pollute the timings. Give it a few real attempts.

Anything else — [open an issue](https://github.com/sorenwolfe/Shikari/issues) and describe what
happened. Screenshots help a lot.

---

## Licence

AGPL-3.0-or-later. Full text in `LICENSE`.

Building it yourself, cutting releases, and how the timing engine actually works are covered in
[docs/DEVELOPING.md](docs/DEVELOPING.md).

---

## The old name

This was called **RaidPlan** until 0.5.0. The rename means Dalamud sees a different plugin, so:

- **Uninstall RaidPlan** in `/xlplugins` after installing Shikari. Both will otherwise run at once,
  with two mini windows and two sets of shotcalls.
- **Your plans come with you.** Dalamud stores a plugin's files under its internal name, so renaming
  points it at an empty folder. The first run copies the old settings and every plan across, and
  copies rather than moves — the originals stay where they are, so nothing is lost if you go back.
- **`/raidplan` still works.** It is registered as a hidden alias and will stay that way.
