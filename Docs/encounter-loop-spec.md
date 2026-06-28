# HORDEBREAKERS — Encounter Loop Spec

> Companion to `HORDEBREAKERS_GDD.md` (expands §3/§6 at the encounter altitude). **v0.2** — combat floor updated: stamina removed → attack-commitment + re-gated defense; power moved to Augment dials.
> **Scope:** what happens inside *one arena* — the moment-to-moment that decides whether the game is fun. Full run/campaign structure is the **next** doc; this one stops at the edge of a single encounter (plus the level container it sits in).
> **The question this doc exists to answer:** *is the core loop fun?* Everything downstream — world, roster, bosses, run length — waits behind a yes.

---

## 1. The core fantasy & the central tension

Two forces pull on the player at all times:

- **The Objective** — free the kingdom. *Move forward.* Take the gate, slay the commander, push to the keep.
- **The Temptation** — the power fantasy. *Stay.* Farm the garrison, bank XP and Augments, become a god mowing down the enemy.

**The entire encounter loop is one risk/reward dial between these two, and the player's hand is on it continuously.** That tension is the game. If lingering isn't tempting and leaving isn't pressured, there's no game — just a room you clear.

**Combat register: early God of War crowd-management** — not modern one-vs-few free-form, not Dynasty-Warriors one-vs-hundreds. One-vs-*dozens*: wide sweeping arcs, long reach, AoE, launches/knockback to control space, prioritizing the dangerous few while cleaving the many. The camera breathes to show the dozens; **lock-on is the opt-in *tool* for elites/the commander, not the default mode** (the default is crowd-facing).

The **deliberate floor is attack commitment, not a depleting bar** — there is no stamina. Swings carry real startup/recovery and are only dodge-cancelable in recovery, so reading the crowd *before* you commit is the skill; defense is re-gated by a cooldown/charge-banked dodge and a **guard-breakable** block (heavy/elite attacks shatter a held guard). The early-run friction is the moveset's weight; the power fantasy is **earned through Augments that loosen those gates** (§5).

---

## 2. The finite-garrison escalation system (the core mechanic)

The engine that drives the tension. **The stronghold has a *fixed* garrison** — a finite defender budget. The player's *pace* decides the *shape* of the fight, **not a punishment clock.**

- **Mobilization, not spawning.** The alarm doesn't create enemies; it controls how fast the fixed garrison *mobilizes and arrives*. You announced the raid (no stealth — §4); the keep is waking up and pouring out against a threat it can see.
- **Pace shapes the fight (the key property):**
  - *Aggressive / fast* — punch through pockets before the garrison fully wakes; outrun the mobilization, face thinner resistance, less farm.
  - *Methodical / greedy* — the garrison fully rouses; denser waves and **higher-tier composition** arrive; more farm, more danger.
- **Escalation is COMPOSITION, not just count.** At dozens-scale, composition is the real lever. Early: chaff. Later: chargers, then Brute elites, then elite packs. The garrison's heavy units mobilize **last** — so the longer you linger, the nastier what arrives.
- **The ceiling (the brake).** Escalation compounds toward a tier the player **cannot hold.** Greed eventually gets you killed. Without a real ceiling, the gamble is fake. `[PLACEHOLDER: ramp curve, ceiling tier, time-to-ceiling]`
- **Finite budget (the fairness).** Because the garrison is finite, a fully-committed player *could* grind the whole budget down (the Bodybuilder fantasy) — but diminishing returns + the ceiling mean infinite farm isn't *optimal*, just survivable-until-it-isn't.

> **Tuning levers:** garrison size · mobilization rate · composition-escalation curve (when each tier unlocks) · reinforcement wave size/cadence · ceiling tier + time-to-ceiling. All `[PLACEHOLDER]`, all in the tuning sheet.

---

## 3. The Objective ↔ Temptation dial (the heart)

For the gamble to be a *real decision* and not an obvious "always do X," **both sides must scale and the player must feel both.**

**The pull to STAY (must feel genuinely rewarding):**
- Higher-tier reinforcements drop **disproportionately** better XP/loot — non-linear, so late elites are *worth* the risk. `[PLACEHOLDER: drop scaling by tier]`
- Farming = more XP = **more Augment draws this run** (§5). The fantasy compounds *visibly* — you're measurably stronger for staying.
- The crowd-combat itself is the reward — lingering is how you get to swim in the early-GoW feel.

**The push to LEAVE (must feel genuinely threatening):**
- The ceiling (§2) — a real tier where greed kills you.
- **Death costs the greed spoils** (§6) — the unbanked XP/loot from this arena. The penalty *is* the temptation's stakes.
- Diminishing returns at the extreme — so the *sweet spot* is a choice, not an infinite grind.

**The target moment (repeated — this is the game):**
> *"I'm strong, the elites are dropping great loot, one more wave… no — that Brute pack will break me. Take the gate NOW."*

---

## 4. No stealth — the raid announces itself

This is a **power fantasy, not infiltration. Braveheart, not Bond.** The opening is a *declaration*, not a sneak: you hit loud, the alarm goes up because you *want* the keep to know you're coming, and the garrison mobilizes against a visible threat. The arc is **loud → louder → overwhelming**, never quiet→loud. No detection, no line-of-sight, no stealth system — simpler to build and truer to the character.

---

## 5. XP, leveling & the Augment draft within the loop

Ties the temptation directly to power.

- **XP accrues from kills** during the arena — *uncommitted* until you clear it.
- **Clearing the arena banks the accrued XP** → resolves into one or more **level-ups** → an **Augment draft per level gained** (the canonical leveling path, GDD §6). A heavy-farm clear yields *several* draws; a rush yields one. **Farming longer = more Augments = more power.** This is the temptation's concrete payoff. The Augment dials don't just add numbers — they **loosen the combat gates** (attack speed, dodge cooldown/extra charges, guard-break resist / hyperarmor), so "measurably stronger for staying" is felt in the *moveset*, not just the stat sheet.
- Leveling happens **only at arena clear** — milestone / D&D-style, never mid-fight. `[PLACEHOLDER: XP per tier, XP→level curve, draws per level]`

---

## 6. Death & checkpoints (hybrid)

The death model that makes the gamble real *without* roguelite harshness — this is a **campaign with roguelite texture**, not a roguelite.

- **Die in an arena →** respawn at the last checkpoint, **lose the uncommitted spoils** (this arena's unbanked XP/loot you were greedily farming), keep your character + banked progress, **re-fight the arena.** *What you gambled by lingering is exactly what you forfeit by dying.*
- **Checkpoints** sit at each major beat (clear the village → checkpoint before the keep). A death costs the current arena's spoils + a re-fight, not the whole level. `[PLACEHOLDER: checkpoint cadence]`
- **Failing the level objective or dying to the boss** carries a bigger cost (back to the level/stronghold checkpoint). `[PLACEHOLDER]`
- *Tuning safety valve:* if losing a full arena's XP proves too punishing, partial-bank at reinforcement-tier thresholds. Default is **bank-at-clear** for legibility.
- **Dependency:** confirms a **persistence layer** (campaign saves + unlock tracking) as a required system — flagged in the GDD, not needed for *this* slice, but no longer optional.

---

## 7. Objective types (one escalation system × many objectives = variety)

The escalation engine is constant; the **objective** makes each arena play differently and sets *who controls the pace.*

- **Slay** *(default)* — kill the commander. Carve to the officer; the commander is the player-controlled **"exit"** — engage whenever, but lingering piles on adds. *Player paces via when they commit to the commander.*
- **Destroy** — wreck the gate / siege the access. *You* set the pace, so escalation races your own demolition.
- **Hold / Protect** — defend the slaves / a position. You *can't* leave → escalation at its most tense; pure survival under the ramp.
- **Reach / Activate** — lower the drawbridge. Push forward through escalation to a point, then hold it to finish.

> **First build uses Slay** — the cleanest expression of the dial (the commander is the off-switch you choose when to flip). Keep objectives **pluggable** so the other three slot in later.

---

## 8. The level container (brief — full run structure is the next doc)

The arena sits inside a **village → keep → boss** arc: infiltrate the village (light resistance, the alarm trigger) → fight inward as the garrison mobilizes (a few escalating arenas, varied objectives) → assault the keep → **boss.** Mobilization is the connective tissue, so the difficulty arc is built into the *geography* of pushing inward. (Strongholds-per-run, run length, and between-run meta are the **next** spec; this doc stops here.)

---

## 9. Success criteria — what to playtest for ("broken" defined up front)

Know what failure looks like before testing, so you recognize it:

- **The dial is real** if playtesters *visibly hesitate* — push-luck vs. cash-out — rather than always rushing or always farming. If everyone does the same thing, the dial is mis-tuned.
- **The combat is fun** if lingering feels *good* (you *want* to be in the crowd) and the early-GoW crowd-management reads — you're prioritizing threats while cleaving chaff, not mashing.
- **The ceiling is fair** if deaths feel *earned* ("I got greedy"), not arbitrary.
- **Broken looks like:**
  - *always-rush* → temptation too weak / ceiling too low / rewards too thin.
  - *always-farm-forever* → no real ceiling / no diminishing returns / death too cheap.
  - *mash-through* → combat too shallow / enemies too passive (a combat-feel problem, not a loop problem). Structurally guarded now by **attack commitment** (a swing locks you into its active frames — mashing strands you) and **guard-break** (turtling through an elite's slam fails), but watch for it if commitment/recovery windows are tuned too loose.

---

## 10. Tuning levers (all `[PLACEHOLDER]` → tuning sheet)

Garrison size · mobilization rate · composition-escalation curve (tier-unlock timing) · reinforcement wave size + cadence · ceiling tier + time-to-ceiling · drop/XP scaling per tier · XP→level curve · draws per level · checkpoint cadence · arena spatial size + spawn-point layout · objective-specific params.
