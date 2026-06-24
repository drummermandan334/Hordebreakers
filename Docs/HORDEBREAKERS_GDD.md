# HORDEBREAKERS — Game Design Document

> **Working title.** Codename is fine to ship under; rename before Steam page goes up.
> **Version:** 0.1 (initial foundation) · See [Changelog](#changelog) at the end.
> **One-line pitch:** A 1–4 player online co-op brawler where you actively fight through Vampire-Survivors-scale hordes — your hands stay on the combat while auto-weapons handle the swarm.
> **Fun hypothesis (the one thing that must feel good):** Wading into a packed horde, watching your auto-weapons stack marks on everything, and slamming a heavy attack to *detonate* a cluster into a satisfying burst — while dodging an elite's telegraphed slam. If that core isn't fun solo, nothing else matters. Prototype it first.

---

## 1. Vision & Pillars

This is a deliberate fusion of two genres that normally pull in opposite directions:

- **Beat-em-up DNA** — active, skill-expressive melee: combos, dodges, crowd control, hit-stop and juice. Co-op is native (Castle Crashers is the touchstone).
- **Vampire Survivors DNA** — hundreds of enemies, a build that assembles itself mid-run from drops with synergies, escalating density, and roguelite meta-progression.

Every design decision below is measured against four pillars. If a feature doesn't serve one, cut it.

1. **Hands-on power fantasy.** You are *always* actively fighting. Combat is juicy and reads at a glance even with 200 enemies on screen.
2. **Builds that change how you fight, not just your numbers.** Each run, drops recombine into a distinct playstyle and "oh, I'm *this* character this run" moments.
3. **Better together.** Drop-in 1–4 player co-op where roles interlock — a 4-stack feels like a coordinated wrecking crew, not four people soloing in the same room.
4. **Easy to start, deep to master, quick to replay.** 15–25 minute runs; roguelite meta keeps "one more run" alive. Synty's readable low-poly keeps hordes legible and performance high.

---

## 2. Scope & Targets

| Aspect | Decision |
|---|---|
| Players | 1–4, **online co-op only** (no local split-screen in v1) |
| Perspective | 3D third-person, **behind-player musou / action camera** (Dynasty Warriors-ish) |
| Engine | Unity 6 LTS |
| Art | Synty Polygon assets (low-poly stylized) |
| Performance target | **60 FPS** on mid-range PC, host and clients |
| Platform | PC first; Steam release if it's fun |
| Run length | 15–25 minutes |
| Audience | Friends first; Steam-ready if it holds up |

---

## 3. Core Gameplay Loop

### Moment-to-moment (0–30 seconds)
Move under the musou action camera. Your **auto-weapons fire continuously** at nearby enemies, shredding chaff and stacking **marks** on whatever they hit. You weave **light-attack combos** into the crowd and time **dodges** through telegraphed elite attacks. When a cluster is marked, you land a **heavy attack to detonate** it — a big AoE burst that clears chaff and chunks elites.
- **Feedback:** hit-stop on heavy hits, screen-shake + flash on detonations, XP gems popping and streaming toward you.
- **Reward:** the *whump* of a detonation clearing a knot of enemies; XP; the occasional drop.

### A run / session (15–25 minutes)
Escalating **wave-rounds** in an arena.
1. A wave spawns a swarm with rising density and new enemy types.
2. Clear it → short **breather** (~10–20s): enemies cleared, team regroups.
3. Short **breather**: regroup and optionally spend gold at a shrine. (Level-up upgrade choices resolve once the arena is cleared — see §6.3.)
4. Next wave, denser and nastier.
5. Every **few waves, a boss** — a full manual skill check.
6. **Final boss = run win.** All players downed simultaneously = run loss.

The breather is the heartbeat: it's where the build decision happens and where co-op regroups/revives.

### Meta (hours–weeks)
Banked currency between runs unlocks: **new characters** (each a distinct kit + role), **new weapons added to the drop pool**, **small permanent upgrades**, and **harder difficulty tiers** for better rewards. The "one more run — let's try the new character together" hook.

---

## 4. Combat System (the crux)

The single most important design problem this game has: **in a melee + auto hybrid, auto-weapons must not scale so hard that manual combat becomes pointless.** If that happens, you've accidentally built pure Vampire Survivors and killed Pillar #1. Two structural mechanisms prevent it.

### 4.1 Division of labor (via enemy taxonomy)
Auto-weapons handle **breadth** (the swarm); manual melee handles **depth** (the threats). As a run escalates, *both* pressures grow — more chaff (autos scale to match) and more/tougher elites (manual skill + build scale to match) — so neither system ever goes vestigial. See [§5 Enemy Taxonomy](#5-enemy-taxonomy).

### 4.2 The synergy loop: Mark → Detonate
This is what makes "hybrid" mean something instead of two parallel games running side by side.

**Mechanic: Mark**
- **Purpose:** give auto-weapons a payoff the player actively triggers.
- **Input:** any auto-weapon hit applies 1+ stacks of *Mark* to an enemy.
- **Output:** a visible stacking status (glow/icon intensity scales with stacks). Marks decay after `[PLACEHOLDER: mark duration ~4s]` if not refreshed.
- **Edge cases:** capped at `[PLACEHOLDER: max stacks]`; bosses can be marked but at reduced effect.

**Mechanic: Detonate**
- **Purpose:** the core skill-expression verb; converts passive auto-DPS into burst the player aims and times.
- **Input:** **heavy attack** (also does solid raw damage on its own).
- **Output:** detonates all Marks within `[PLACEHOLDER: detonation radius]` around the impact. Damage scales with total mark stacks consumed → clears chaff in a burst AND chunks elites.
- **Why it scales right:** more autos = more marks = bigger detonations. The manual layer stays relevant and grows *more* impactful as the build escalates — exactly the arc we want (carry early, godlike late, hands always busy).
- **Tuning levers:** mark duration, max stacks, detonation radius, damage-per-stack, heavy-attack speed.

This loop also seeds build identity: **detonation builds** (marks + detonation upgrades), **raw-auto builds** (more/stronger autos, less manual), **bruiser builds** (manual-combo heavy, autos as support). → Pillar #2.

> *Lighter-weight alternative if detonate proves fiddly in playtest:* a **Fury/Heat** loop where manual hits charge a meter that temporarily supercharges autos. Simpler, but less interactive. Keep in back pocket.

### 4.3 Manual moveset
| Action | Role | Notes |
|---|---|---|
| **Light attack** | Fast 3-hit combo string | Low per-hit damage; chaff cleanup, gap-filler, builds combo |
| **Heavy attack** | The **Detonator** | Slower, high single-target damage + detonates marks in AoE |
| **Dodge** | Short dash + i-frames | Cooldown-gated; defensive + repositioning; upgradeable (e.g. leaves a trail) |
| **Auto-weapons** | Continuous breadth DPS | Fire on timers, VS-style; apply marks; targeting per-weapon (nearest / forward arc / random) |

**Targeting & feel:** **auto-facing / soft lock** on melee — your attacks snap toward the nearest enemy in your facing arc — to keep combat accessible in chaos. Gamepad: left stick move, right stick face/aim; KBM: WASD + mouse aim. Controller-friendly throughout (likely how friends will play).

### 4.4 Juice requirements (Pillar #1, non-negotiable)
Readability and feel at horde scale: clear enemy silhouettes (Synty helps), telegraphs reinforced with height/animation cues (see §7), distinct detonation VFX, hit-stop on heavies, screen-shake scaled to detonation size, audio that cuts through density. Budget VFX carefully — see [§9 Tech](#9-technical-architecture).

---

## 5. Enemy Taxonomy

Three tiers, each aimed at a different system. (Starter bestiary uses Synty Polygon-style archetypes; rename/retheme freely.)

### Tier 1 — Swarm / Chaff *(the auto-weapon fodder)*
Weak, numerous; this is the horde fantasy. Players should never be drowning in trivial 1-HP inputs — autos exist to clear these.
- **Husk** — basic melee shambler. Baseline swarm unit.
- **Runner** — fast, very low HP. Pressure/positioning threat in numbers.
- **Spitter** — ranged chaff; lobs a slow, dodgeable projectile. Punishes standing still.
- Drops: XP gems. Occasionally gold.

### Tier 2 — Elites / Bruisers *(the manual-skill threats)*
High HP, **telegraphed** attacks; demand dodging and focused melee. This is where the beat-em-up skill lives.
- **Brute** — slow, heavy ground slam (large **wind-up + raised AoE-ring** telegraph). Reward for dodging then detonating.
- **Charger** — telegraphed dash across the arena; sidestep it.
- **Warden** — front shield; must be flanked or staggered by a detonation. Teaches positioning/coordination.
- Drops: more XP, gold, occasional upgrade.

### Tier 3 — Bosses *(full skill checks, wave milestones)*
Multi-phase, pattern-based. Appear every few waves; the final boss ends the run.
- **The Gravemaker** *(example boss)* — summons swarm adds (autos + detonate matter), plus telegraphed AoE patterns you must read and dodge.
- Drops: guaranteed upgrade + meta currency on first kill of the run.

> **Co-op scaling note:** spawn counts, elite frequency, and boss HP scale with player count so a 4-stack still feels pressured. Exact scaling in the tuning sheet (`[PLACEHOLDER]`).

---

## 6. Run Structure, Build & Progression

### 6.1 Wave structure
- A run = `[PLACEHOLDER: ~12–20]` wave-rounds over 15–25 min.
- **Wave goal:** survive a timer *or* clear a kill quota (lean toward a short survive-timer with a density ramp; quota for "clear" waves to vary pacing).
- **Breather** between waves: ~10–20s, enemies cleared, optional shrine purchase. (Level-up choices resolve when the whole arena is cleared — see §6.3 — not every breather.)
- **Boss cadence:** every `[PLACEHOLDER: 3–5]` waves; final wave = boss.
- **Escalation levers** (all in tuning sheet): enemies/wave, spawn rate, elite %, per-wave enemy stat scaling, wave modifiers (e.g. "double Runners," "no shrine this round").

### 6.2 Win / lose & co-op revives
- **Win:** defeat the final boss.
- **Lose:** all players in the *downed* state at once.
- **Downed state:** a felled player drops to the ground with a revive window; a teammate revives by standing nearby for `[PLACEHOLDER: ~3s]`. Optional self-revive on a charge/cooldown for solo play and clutch saves.

### 6.3 In-run build (action-RPG layer, VS-flavored)
- Enemies drop **XP gems** that bank into XP. **Level-ups resolve when an arena (spawn point) is defeated** — D&D-style, between encounters, **not** as a mid-fight pop-up. The **card-pick mechanic is retired** (played out); leveling reads as an action-RPG **level-up choice**.
- **On level-up: choose 1 of 3** — amp an **ability** or a **stat** (e.g. detonation radius/damage, an auto-weapon, a melee/dodge upgrade, a passive). Exact option set TBD; *weapon evolution* (weapon + passive → evolved form, e.g. *Orbiting Axes + Haste → Whirlwind*) stays on the table.
- **Gold/scrap:** secondary currency; spend at an optional shrine (buy a specific upgrade, heal) or bank the remainder toward meta.
- *Prototype status:* the upgrade UI is **disabled** for now; XP/level still track, and the post-arena choice flow is to be built.

### 6.4 Meta-progression (between runs)
- **Characters** — unlock new playable characters (each a distinct kit + signature auto + co-op role).
- **Weapon pool** — add new weapons into the in-run drop pool.
- **Permanent upgrades** — small persistent buffs (modest, to protect run-to-run skill expression).
- **Difficulty tiers / modifiers** — unlock harder difficulties for better rewards and replay.

### 6.5 Starter roster (co-op identity → Pillar #3)
Four characters so a full 4-stack has interlocking roles. Solo, each is self-sufficient.

| Character | Archetype | Kit highlights | Co-op role |
|---|---|---|---|
| **Vanguard** | Bruiser / Tank | High HP, heavy melee, taunt/aggro draw; signature auto = short-range shockwave | Holds the line; soaks elites |
| **Tempest** | Zoner / CC | Strong auto-weapons, slows/knockback, lower HP | Thins and controls the swarm; **stacks marks fast** |
| **Reaper** | Assassin / Detonator | Extra dodge/mobility, specializes in **detonations** (bigger/cheaper), bursts elites | Kills priority targets; cashes in Tempest's marks |
| **Warden** | Support | Buffs, heals, shields, faster revives | Keeps the team alive; force-multiplier |

**The synergy fantasy:** Tempest blankets the crowd in marks → Reaper detonates for huge bursts → Vanguard holds the frontline → Warden sustains the whole thing. That interplay is the reason the game is more fun with friends.

---

## 7. Camera & Controls

- **Camera:** **musou / Dynasty-Warriors-style action cam** — third-person, positioned **behind and slightly above** the player at a **shallow downward tilt** (not top-down). The player sits low-center; the crowd reads ahead and around, receding into the distance. Pulled back with a **wide FOV (~65°)** so a good chunk of the surrounding horde stays on screen. Each player has their own camera (trivial online — the reason we skipped split-screen).
- **Camera behavior:** yaw **eases to stay behind the player's movement heading** (gently damped; follows *movement*, not the per-swing soft-target face, so it doesn't jerk on attacks). **Manual orbit** on mouse X/Y (yaw + limited pitch); a **recenter** input snaps back behind the player (gamepad right-stick maps the same later). Smooth-follow on position (SmoothDamp).
- **Telegraphs — must be reinforced at this angle:** the shallow musou tilt **flattens the ground plane**, so **ground decals alone no longer read reliably** (they foreshorten into the crowd). Tells must lean on **vertical / height cues** — raised AoE rings or pillars, overhead world-space markers, enemy-mounted tells — **and clear, readable enemy wind-up animations**. Treat ground decals as a *secondary* layer beneath a primary 3D/animation tell, never the sole telegraph.
- **Controls (gamepad-first):** left stick move, **right stick camera orbit** (attacks auto-face nearest in arc), face buttons + triggers for light/heavy/dodge, **R3 to recenter**.
- **Controls (KBM):** WASD move (camera-relative), **mouse X/Y orbit**, **MMB recenter**, LMB light / RMB heavy / Space dodge.

---

## 8. Multiplayer Architecture

- **Model:** **listen server (host).** One player's machine runs server + client in one process; others join as pure clients. **Server-authoritative** (host is the authority) — correct for the horde sim and good-enough anti-cheat for PvE.
- **Connectivity:** **Steam relay (SDR)** via a Steam transport. Friends join through the Steam friends list / a lobby; no port-forwarding; host IP hidden; free on Steam. (Unity Relay + Lobby is the cross-platform fallback if we ever leave Steam.)
- **Stack:** **Netcode for GameObjects** (host mode; *Boss Room* sample as reference) with the transport swapped to Steam. **Mirror + FizzySteamworks** is the lighter alternative. Pin exact package versions at implementation time — they move.
- **Horde sync (the hard part — see §10 Risks):** **do NOT replicate every enemy transform.** Host simulates the authoritative horde (ECS); clients receive lightweight state. Approach: spawn events + shared RNG seed, aggregated/batched updates, interest management, and lower update rates for distant enemies; clients render interpolated representations.
- **Host leaves:** run ends in v1 (no host migration — it's genuinely hard). Consider save-and-resume later.
- **Host load:** host runs the full sim *plus* its own rendering → host wants a decent machine. This is *why* we don't replicate every enemy.

---

## 9. Technical Architecture (high-level)

- **Hybrid ECS + GameObject:** the **enemy horde runs on DOTS/ECS + Burst/Jobs** (movement, targeting, collision — the thing that needs to scale to hundreds/thousands of agents). **Players, camera, UI, and bosses stay GameObject-based** (easier to author, fewer of them).
- **Performance (60 FPS / ~16.7ms budget):** GPU instancing for swarm rendering, aggressive LODs, shared materials/atlases, and **object pooling everywhere** (never Instantiate/Destroy in tight loops). Profile from day one.
- **Synty pipeline:** low-poly is ideal for hordes (cheap to render in bulk, silhouettes stay readable when packed). Organize asset folders early; combine/atlas materials to keep draw calls down.
- **Data-driven design:** weapons, enemies, upgrades, and tuning live in **ScriptableObjects / data files**, not hardcoded — so the tuning sheet maps directly onto editable assets.

---

## 10. Onboarding (target: >90% complete the first run unaided)

- [ ] Core verb (move + light attack) usable within **30 seconds** of first control.
- [ ] **First wave is safe** — guaranteed first success, no failure possible.
- [ ] Introduce in low-stakes order: auto-weapon → **mark/detonate** → dodge → elite (first telegraph).
- [ ] Player discovers at least one **synergy** through play, not text.
- [ ] First run ends on a **hook** — an unlock teaser ("New character available").

---

## 11. Risks & Next Steps

### Biggest risk
**Horde-scale co-op netcode.** Syncing hundreds of enemies across four clients is the single hardest engineering problem here. **De-risk early** with a dedicated networking spike (host-authoritative ECS horde + lightweight client representation) before committing to content.

### Build order (de-risk fun before scale)
1. **Single-player core prototype** — one character, 2–3 autos, one swarm tier + one elite, the **mark/detonate loop**, the musou action camera. *No netcode.* Answer the fun hypothesis. **If this isn't fun solo, stop and fix it.**
2. **Vertical slice** — 1 character, 1 arena, ~5 waves + 1 boss, ~6 upgrades, the post-arena level-up choice flow.
3. **Co-op networking spike** — listen server + Steam relay + the horde-sync approach above, 2 players first.
4. **Scale** — more characters, weapons, enemies, waves, meta-progression.

### Open questions to resolve in playtest
- Wave goal: survive-timer vs kill-quota mix?
- Detonate vs Fury/Heat as the synergy loop?
- How modest do permanent meta upgrades stay (to protect skill expression)?
- Self-revive: always on, or solo-only?

---

## Changelog

| Version | Date | Notes |
|---|---|---|
| 0.1 | initial | Foundation: pillars, core loop, combat crux (mark/detonate + enemy taxonomy), run structure, starter roster, multiplayer (host + Steam relay), tech architecture, onboarding, risks/build order. All numeric values flagged `[PLACEHOLDER]` pending playtest — see companion tuning sheet. |

> **Living document.** Every significant revision gets a changelog row. Numeric values are hypotheses marked `[PLACEHOLDER]` until playtested — tune them in `HORDEBREAKERS_Tuning.xlsx`, not here.
