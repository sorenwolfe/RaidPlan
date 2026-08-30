# RaidPlan

**Your static's raid plan, inside the game.**

Draw the fight out slide by slide, tell each player which cooldown to press and when, and RaidPlan
shouts the call at them a few seconds before the boss cast lands.

No second monitor. No alt-tabbing to a website in the middle of a pull.

Type `/raidplan` to open it.

---

## Installing it

You need XIVLauncher with Dalamud. If you already use any plugins, you have it.

RaidPlan isn't in the official plugin list yet, so you add it by hand the first time. It takes
about a minute, and you only ever do it once.

1. In game, type `/xlplugins` and press Enter. The plugin installer window opens.
2. Click **Settings** at the bottom-left of that window.
3. Click the **Experimental** tab along the top.
4. Scroll to **Custom Plugin Repositories**. There's an empty text box at the bottom of the list.
   Paste this into it:

   ```
   https://raw.githubusercontent.com/sorenwolfe/RaidPlan/main/repo.json
   ```

5. Click the **+** button next to the box.
6. Click **Save and Close**.
7. You're back at the plugin list. Type `raid` in the search box at the top. **RaidPlan** will
   appear — click **Install**.

Now type `/raidplan` and the planner opens.

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

**Everyone gets their own instructions.** You build the plan around *positions* — MT, H2, R1 —
not names. Give each position a job, and RaidPlan shows you that job's real spell list to pick
from. So you can say "H2 presses Bell here", and the person playing H2 that night is the one who
gets told, whoever it is.

**It calls things out before they happen.** A few seconds before the boss cast lands, the player
who has a job to do gets told what to press. It can appear as a banner on screen, in your chat
log, as a pop-up notification, with a sound, or any mix of those. Everyone chooses how loud it is
for them — that setting is yours, not the plan's.

**The slides follow the fight on their own.** When the boss starts casting something your plan
knows about, the plugin flips to the right slide by itself. Nobody has to click through it while
dodging. Wipe, and it goes back to slide one ready for the next pull. Click to a different slide
yourself and it'll leave you alone for a few seconds.

**It learns your fights.** RaidPlan quietly watches your pulls and remembers when each thing
happens. After three or four attempts it knows the fight well enough to warn people *before* the
boss even starts casting — which is the only way to get more notice than a cast bar gives you.
It also handles pulls that run fast or slow, so the timings don't drift.

**Build the timeline for you.** Once it's watched a few pulls, one button on the **Learned** tab
lays out the whole fight as a list of steps, with a blank slide for each mechanic. You just draw
into them.

**Import from FF Logs.** Paste a link to a log and RaidPlan pulls the fight's timeline straight
in — and if the jobs match up, it can even copy the cooldowns those players actually pressed onto
the right mechanics. This one needs a free key from FF Logs (they don't allow anonymous access);
only the person building the plan needs it, not the whole group. The key is checked the moment you
paste it in, so a typo tells you straight away rather than on your first import, and you can change
it later in settings.

**A small window for during the fight.** In raids, a compact copy of the current slide appears on
screen, about the size of your minimap. Drag it wherever you like. It shows the same slide the
planner is on, so it keeps up with the fight by itself, and it puts a ring around *your* marker —
found by your character name, or by your job when it only appears once in the plan. Whatever's
written on the slide shows underneath it, so you can read the call without opening the planner.
While you're
in combat it ignores the mouse completely, so it can never swallow a click meant for the game.
Hover it out of combat to move it or close it, and `/raidplan mini` brings it back.

**It looks like it belongs there.** Dark panels, one accent colour you can change, soft shadows,
and icons on the drawing tools. The headings use the game's own font rather than a generic one.
If you'd rather it matched your other plugins, one switch in settings turns the whole thing off.

**Bring in a plan from raidplan.io.** Paste a link and RaidPlan rebuilds it: every slide, the
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

1. `/raidplan` to open it.
2. On the **Roster** tab, set the jobs your group is running.
3. Pull the fight three or four times, normally. RaidPlan is watching.
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
| `/raidplan` | Opens and closes the planner (`/rp` works too) |
| `/raidplan config` | Opens the settings |
| `/raidplan next` / `prev` | Move a slide forward or back — good on a hotbar macro |
| `/raidplan calls` | Turns the spoken calls on and off |
| `/raidplan follow` | Turns automatic slide-changing on and off |
| `/raidplan mini` | Shows or hides the small in-fight window |
| `/raidplan reset` | Jumps back to the first slide |

---

## Changing what the calls say

Every call is just text you can rewrite, in settings or on the step itself. Drop these in and they
fill themselves in: `{player}`, `{job}`, `{seat}`, `{ability}`, `{label}`, `{cast}`, `{time}`.

So `{seat}: {ability} NOW` becomes **H2: Bell NOW** on the healer's screen, and the tank sees their
own line instead. Anything that has nothing to fill in just disappears, so the wording never comes
out broken.

---

## If something isn't working

**RaidPlan doesn't show up in the plugin list.** The repository link probably didn't save. Go back
to `/xlplugins` → Settings → Experimental and check the URL is there and spelled exactly right.

**An update fails to install.** Restart the game and try again. If it still fails after a restart,
open an issue and mention what version you're coming from.

**An imported plan looks too big or too small.** The scale is taken from the plan's waymarks,
which is reliable as long as all eight are placed. If a plan doesn't use them, it falls back to
guessing from the drawing, and that can come out small.

**FF Logs importing won't connect.** Open settings and look at the FF Logs section — it says
whether the key works. "Forget them" clears both boxes so you can paste a fresh pair.

**Calls aren't firing.** Check `/raidplan calls` is on, and that the step is actually anchored to a
boss cast rather than set to Manual.

**It's not learning the fight.** Very short pulls are ignored on purpose, so striking dummies and
instant deaths don't pollute the timings. Give it a few real attempts.

Anything else — [open an issue](https://github.com/sorenwolfe/RaidPlan/issues) and describe what
happened. Screenshots help a lot.

---

## Licence

AGPL-3.0-or-later. Full text in `LICENSE`.

Building it yourself, cutting releases, and how the timing engine actually works are covered in
[docs/DEVELOPING.md](docs/DEVELOPING.md).
