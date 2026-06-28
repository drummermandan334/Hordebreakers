# HORDEBREAKERS — Game Design Document

> **Working title `[TBD]`.** Title and the human kingdom's name are intentionally deferred to the vertical slice.
> **Version:** 0.4 · See [Changelog](#changelog).
> **One-line pitch:** A 1–4 player online co-op action game where an enslaved human warrior fights to retake a fallen kingdom, stronghold by stronghold — deliberate, weighty crowd combat whose godlike power is *earned* through a run-defining Augment draft.
> **Fun hypothesis (still the thing that must feel good):** A fight you have to *read* — dodging and blocking under real pressure when you're under-equipped — that slides toward exhilarating crowd-clearing power as your Augments compound. Discipline early, abandon earned late. If the early, under-equipped fight isn't satisfying on its own, tune that first.

---

## 1. Vision & Pillars

This is an **action-RPG** built on a **Souls-deliberate** combat floor, at **action-RPG scale — dozens of enemies on screen at most, never hundreds.** Vampire Survivors is a *donor*, not the chassis — it lends exactly two ideas (spawn-point wave arenas; a randomized choose-from-three upgrade moment) and nothing else. Everything VS-flavored that made the prototype feel like "3D VS with sword swings" — auto-weapons, continuous mid-fight leveling, XP-gem vacuuming, the card-draft firehose of +5% trickles — has been cut.

Four pillars. If a feature serves none, cut it.

1. **Deliberate combat that earns its power fantasy.** Every fight demands reading and answering enemies — dodge, block, spacing, and committing to your attacks. The godlike crowd-clearing feeling is *earned* through the run, not the default state: hardest when you're under-equipped early, looser as your build compounds.
2. **Every run, a different build.** The Augment draft reshapes *how you fight* each run, and its randomness deliberately swings difficulty — a lucky synergy spread feels god-tier, an unlucky one forces clean, disciplined play. Skill is the floor that keeps a low roll winnable. Short runs, roguelite replay.
3. **Better together.** Drop-in 1–4 online co-op with interlocking roles — a 4-stack is a coordinated war-band, not four soloists in a room.
4. **A kingdom worth retaking.** Each stronghold is a distinct occupying faction with its own roster, mechanics, and art; a serious reconquest carries a light, absurdist thread underneath. Synty's readable low-poly keeps the crowds legible and the frame budget healthy.

---

## 2. Narrative & Setting

**The spine is the focus: a soldier retaking his homeland.** The theme underneath is delivered with a light, absurdist touch — woven through specific fictional events, never monologued at the player.

### Premise
A human kingdom `[TBD]` — once the most powerful and prosperous realm on the planet — fell, conquered from within. You are human: a former warrior of its army, now toiling in the invaders' slave pits. You've had enough. With a handful of like-minded humans you break out and begin taking the kingdom back, **stronghold by stronghold**.

### The fall (subtext, not sermon)
The realm was undone by betrayal and misplaced trust, exploited by enemies who saw a weakness and played a long game. The architects were the **elves** — the *last* race anyone would suspect, smug stewards of their own enlightenment — who quietly took key political positions and ran a cynical long-con. **They never fielded an army. They used the other races as unwitting muscle.**

The twist that drives the whole tone: **the other races are dupes.** Goblins, undead, dwarves, and the rest were late-arriving opportunists who think they conquered a dying kingdom on their own steam — with no idea they were the elves' hired help. Each now occupies a stronghold, strutting as proud conquerors, sincerely and comically wrong.

### Delivery (light / absurdist — the joke is on the elves)
The elves are too self-congratulatory to keep the secret. The reveal is never a speech; it's an aside they can't resist, because not admiring their own cleverness would be a tragedy:
- **Combat asides:** a losing elven commander sniffs *"You weren't conquered, you were... onboarded. Honestly, the paperwork was the hard part —"* then catches himself, *"...I've said too much."*
- **Environmental gags:** a liberated elven war room where the invasion plan is a mortifyingly tidy filing system — labeled ledgers, a flowchart titled *Compassion: A Roadmap*, a commemorative tapestry of the whole scheme they couldn't resist weaving.
- **Codex / loading voice:** insufferable elven historiography in euphemism — "a peaceful demographic transition," "a generous resettlement initiative" — the horror sitting in the gap between the bureaucratic language and what happened.
- **Running gags:** the elves are *offended* anyone calls it an invasion (*"Invasion is such an ugly word. We prefer 'inheritance.'"*); the other races bristle at any hint they were pawns (*"We took this kingdom!"*) while the elves murmur *"Yes, yes, you were instrumental,"* and the muscle never quite hears it.

**Dramatic irony runs the entire game.** The player pieces together the elven hand long before the other occupiers do. Each stronghold's villain gets a free comic arc — boasting about a victory that was never theirs. The **final stronghold (held by all the races together)** is where the truth surfaces: the muscle's dawning, too-late realization, played for comedy; the elves, naturally, find the whole thing *gauche*.

The theme — a tolerant nation undone by having its compassion exploited — is present as flavor and subtext, in proportion, under a reconquest story that stays foreground. (Sequel seed, unstated in-game: the betrayed, enlightened muscle is a second game.)

> **Tone:** heightened, satirical, a little grotesque. Authored and funny, not a thesis.

---

## 3. Scope & Targets

| Aspect | Decision |
|---|---|
| Players | 1–4, **online co-op only** (no local split-screen in v1) |
| Genre | Action-RPG on a Souls-deliberate combat floor (action-RPG scale — dozens of enemies, not hundreds) |
| Perspective | 3D third-person, **Elden-Ring-adjacent action cam** (behind & slightly above, **FOV ~50, close ~4.5m** — intimate, not pulled back) |
| Engine | **Unity 6.3 LTS** (`6000.3.x`), **URP** |
| Art | Synty Polygon (low-poly); a distinct faction pack per stronghold |
| Performance target | **60 FPS**, host and clients |
| Platform | PC first; Steam if it's fun |
| Run length | ~20–40 min (a campaign of strongholds; tune as content grows) |

---

## 4. Combat System

The core problem v0.2 solves: the prototype felt like *3D Vampire Survivors with swings* — passive, mashy. The fix is a **deliberate, committal combat floor** whose power fantasy is **earned through Augments**, not handed over by default.

### 4.1 Design tension & its resolution
Souls-deliberate and the action-RPG power fantasy pull in opposite directions. They're reconciled by **putting the power curve in the build, not the baseline:**
- The **baseline character stays deliberate all game** — committal attacks, real recovery, punishable mistakes, mandatory defense.
- **Augments bend it toward abandon.** "Wave a huge sword through the crowd with impunity" is a *build outcome* (poise/hyperarmor, guard-break resist, faster attacks, extra dodges, shorter dodge cooldown, lifesteal, crowd-clear), not the starting state. The Augments visibly **loosen the combat gates**, not just pad a stat sheet.
- Therefore difficulty is **front-loaded**: under-equipped early = combat at its most disciplined; as Augments compound, you slide toward the power fantasy. The curve emerges from progression, not a difficulty slider.

What stops button-mashing is the **enemies**, not a restrictive moveset: even chaff can hurt and *interrupt* you, elites and commanders demand real defensive reads, so you cannot ignore the crowd and swing through it. The power-fantasy *feeling* comes from clearing a crowd you've **earned the tools** to clear — never from enemies being harmless. (Action-RPG scale: dozens at most, never hundreds.)

### 4.2 Moveset
| Action | Role | Notes |
|---|---|---|
| **Light attack** | Fast combo string | Crowd-engagement, lower commitment; chains into heavy. Committed through startup/active — only dodge-cancelable in recovery |
| **Heavy attack** | Committal crowd-clear / launcher | Big, weighty swings with knockback/launch and **real recovery you commit to** — the weight is the swing's commitment, not a resource cost |
| **Dodge** | Defensive reposition | I-frames; **cooldown/charge-gated** (a refilling charge bank, default 1 — Augments add charges / cut the cooldown) |
| **Block** | Mitigation under pressure | Frontal chip-mitigation; **guard-breakable** — heavy/elite attacks shatter a held guard and stagger you, so turtling isn't a free answer |
| **Musou meter** | Earned release valve | Builds from dealing/taking hits (**aggression speeds the charge**); unleashes a **screen-clearing finisher with i-frames** |
| **Secondary / thrown** | Manual ranged option | A player-triggered throw (not auto-fire); a tunable target for Augments |
| **Abilities** | Dynasty-Warriors-style specials | The **Grand Ability System (GAS)** — see §6/§11; granted/upgraded via Augments, built server-authoritative for co-op from the start |

Movement is camera-relative. Facing follows your movement/aim; on a swing, a subtle, tunable facing assist rotates you a few degrees toward the nearest enemy in a narrow aim cone so attacks connect in a crowd — never a lock-on or positional pull, and tunable to zero for fully manual facing. Controller-first.

### 4.3 The deliberate floor — commitment & re-gated defense (no stamina bar)
There is **no stamina bar.** A depleting resource is a duel mechanic; in a crowd game it didn't meaningfully gate the player, wasn't fun, and tied the "deliberate" feeling to a number ticking down. Instead the friction is **structural** — it lives in the animations and the defensive cooldowns, where it reads directly off what's on screen. **Pressure, not punishment** still holds: this is deliberately *less harsh than Souls*, the discipline comes from commitment and timing, not a bar you can bankrupt yourself on.

**Commitment & cancel windows (replaces the cost on attacks).**
- A swing is **committed** through its startup and active frames and **cancelable only in recovery** — i.e. after the hit lands (`dodgeCancelPhase`, default ~0.55). You can't instantly dodge-cancel out of the frames you started; the weight of the swing *is* the commitment.
- **Mashing is self-punishing.** A dodge pressed during a swing's committed frames **buffers** and fires the instant recovery opens (responsive), but it can't yank you out of the active frames — so spamming locks you into the string instead of escaping it. A hard wall-clock `minSwingInterval` also floors the swing rate so a burst of presses can't fire several swings (and whooshes) at once.
- **Generous i-frames + input buffer.** Forgiving dodge windows and buffered inputs — Souls discipline without twitch-perfect timing. `[PLACEHOLDER: dodge i-frames, buffer window]`.

**Re-gated defense (replaces the cost on dodge/block).**
- **Dodge** is gated by a **refilling charge bank** (`dodgeMaxCharges`, default 1) that recharges over `dodgeCooldown`: each roll spends a charge, a charge refills on the cooldown. At one charge it's a single cooldown-gated roll; Augments add charges or shorten the cooldown.
- **Block** gives frontal chip-mitigation but is **guard-breakable**: heavy and elite attacks (`guardBreaks`) shatter a held guard, landing full damage and staggering you. Turtling answers chaff but **fails against the attacks that demand a dodge** — so block is a tool, never a free panic button.

**Poise is the Augment payoff that loosens those gates.** `GuardBreakResist` lets a held block hold even against guard-breaking attacks; `Hyperarmor` softens a guard-break from a rooted stagger into a mere flinch. Together with faster attacks, extra dodge charges, a shorter dodge cooldown, and reduced block chip, the same gates that enforce discipline when under-equipped are what you progressively *loosen* into the power fantasy — a real constraint early, nearly vanished on a strong late-run build.

### 4.4 Juice (Pillar #1)
Weight reads through hitstop on heavies, knockback/launch and ragdolls on crowd clears, screen-shake scaled to impact, distinct Musou-release VFX, and audio that cuts through density. Reinforced via PrimeTween (unscaled-safe) + Cinemachine Impulse (see §11 juice pass). All telegraphs must read at the close behind-and-above camera angle — see §7.

---

## 5. Enemy Taxonomy

Four tiers, each aimed at a different demand. Rosters are **re-skinned per stronghold** to the occupying race (a goblin's chaff differs from the undead's), but the structural roles are constant.

### Tier 1 — Crowd / chaff *(the action-RPG fodder)*
Cleavable in numbers — the power-fantasy substrate — **but never harmless.** They can chip and *interrupt*, so you can't mindlessly swing through them. This is the Souls discipline at the lowest level.

### Tier 2 — Wave units / bruisers *(the pressure)*
Tougher, telegraphed attackers that demand defensive reads (dodge/block) and target priority. The reason a crowd is a *threat*, not just volume.

### Tier 3 — Mini-boss / Elite commander *(the arena objective)*
A single hard skill-check that can serve as the **arena's win condition** — carve through the crowd to reach and kill the officer. Sincere and puffed-up about a "conquest" that was never theirs (dramatic irony, §2).

### Tier 4 — Boss *(the stronghold capstone)*
A massive, multi-phase encounter that **completes a Level**. The race's champion; the final stronghold's bosses are where the elven truth surfaces.

> **Telegraphs (camera consequence):** even behind-and-above, ground decals foreshorten — so tells lean on **clear enemy wind-up animations and on-enemy/height cues** (the `AttackTelegraph` wind-up glow), not ground decals alone (§7).
> **Co-op scaling:** crowd counts, elite frequency, and boss/commander HP scale with player count. `[PLACEHOLDER]` in the tuning sheet.

---

## 6. Structure, Leveling & the Augment System

### 6.1 The three-tier nest
- **Arena** — a set of **designed, escalating waves** (hand-tuned for an epic fight, *not* procedural VS density). Spawn points feed the crowd. Cleared by **killing the mini-boss/Elite commander OR completing an objective** (hold the gate, destroy the thing, escort, etc.) — so moment-to-moment isn't a single verb. On clear → **level up**.
- **Level = a stronghold**, each occupied by a **different invading race**, each with its own roster, mechanics, and Synty art direction. Capped by a **massive Boss battle**.
- **Run = the campaign** — a sequence of strongholds, culminating in the **final stronghold held by all races at once**.

**Liberating a stronghold unlocks more upgrade options** — tying meta-progression directly to the narrative spine of taking the kingdom back.

### 6.2 Leveling — milestone, D&D-style
**Level-ups occur only after combat ends** (an arena is cleared) — never mid-fight. There are **no XP gems and no continuous fill-bar** (those are the VS feel we cut). Clear an arena → gain a level → draft an Augment. Enemies may still drop loot/currency for the ARPG layer; they do not drip XP you vacuum. `[PLACEHOLDER: one level per arena vs. accumulate-and-cash-in — milestone is the default read.]`

### 6.3 The Augment system (the single canonical leveling path)
**All** leveling upgrades flow through one system — a blend of **Hades boons × VS upgrades × League of Legends Arena augments**. It is explicitly *not* the VS card firehose: choices are **chunky and build-defining**, not a stream of +5% trickles. Every advancement — stat, weapon, ability, proc, evolution — is an Augment. There is no parallel upgrade path.

**Delivery:** on arena clear, a **level-up panel** offers **3 randomized choices**, filtered to your class, weighted by rarity. (Augment-style cards in the LoL-Arena sense — refined and build-shaping — not VS draft cards.)

**Architecture — composable effect ScriptableObjects** (in build by Code; keep the GDD aligned to the shipped model):
- **Heterogeneous effects.** Each effect is its own SO type composed onto an Augment: **stat mods, weapon upgrades, ability grants, on-hit procs, and evolutions/transforms** — "anything," not just stat bumps. Effects apply via an `UpgradeContext` that can reach the player, weapons (e.g. the throw), and the ability system — not only `PlayerCombatData`.
- **Class-gated pools.** Each Augment declares which character class(es) can roll it; the draft filters to the active character's class (a Barbarian never sees Archer augments). Baked into the data model now, before there are multiple classes.
- **Rarity tiers + weighted randomization.** Augments have a rarity (common → legendary); the draft rolls weighted by a tunable `RarityWeightTable`. **This is the deliberate engine of difficulty variance** — a lucky roll of synergizing rares feels god-tier; an unlucky spread forces a combat-focused grind.
- **Synergy-aware + evolutions.** Effects can read the current build, so Augments scale with, combine with, or **evolve** prior picks; **EvolutionEffect** consumes prerequisite augments and yields an evolved result. This is the Hades/LoL quality the system exists for.
- **`PlayerLoadout`** is the single, run-scoped, **per-player** build model (taken augments, tag counts, weapons, future abilities) — created at run start, discarded at run end. It is the build, not a service locator: effects only get what the context hands them. Per-player by construction, so **co-op = four loadouts, no refactor**; run bookkeeping (level, waves) stays in the run manager.

**Why the wild difficulty swing is intentional and fair:** the build lottery is the point (Pillar #2). It stays *fair* because the **deliberate-melee skill floor keeps a low-roll run winnable through execution** — a bad spread means "focus up and fight clean," not "you lost at the draft." The two systems reinforce each other.

> The **Augment pool itself** and **per-class pools** for the co-op roster are content we flesh out over time; this section specifies the *system*, not the full catalog.

---

## 7. Camera & Controls

- **Camera:** **Elden-Ring-adjacent action cam** — behind and slightly above the player, looking at the upper back/head; **close and intimate (not pulled back), narrow-ish FOV** so the character carries weight. Yaw **eases to stay behind the movement heading** (gently damped; it follows *movement*, NOT the per-swing facing assist, so it never jerks on a swing). **Manual orbit** (mouse / right-stick) with a **recenter** input. Smooth follow on position (SmoothDamp). Tuned defaults: **FOV 50, distance 4.5, pitch 15°, orbit height 2.0, look-at offset 1.5, follow damping 0.12** — all tunable on the `CM_PlayerCam` Cinemachine rig + `PlayerCameraRig`.
- **Telegraph consequence (important):** even behind-and-above, ground decals foreshorten and read poorly — so enemy tells rely on **clear wind-up animations and on-enemy/height cues** (the `AttackTelegraph` wind-up glow already serves this), not ground decals alone. Account for this in every elite/boss attack.
- **Controls:** controller-first. Left stick move, right stick camera; face buttons + triggers for light/heavy/dodge/block/Musou; abilities on remaining inputs. KBM mirror.

---

## 8. Multiplayer Architecture

- **Model:** **listen server (host).** One player hosts (server + client in one process); others join as pure clients. **Server-authoritative** — correct for the enemy-crowd sim and good-enough anti-cheat for PvE. Build **GAS server-authoritative from the start** so abilities don't need re-architecting for co-op.
- **Connectivity:** **Steam relay (SDR)** via a Steam transport — friends join through Steam, no port-forwarding, host IP hidden, free. (Unity Relay + Lobby is the cross-platform fallback.)
- **Stack:** **Netcode for GameObjects** (host mode; *Boss Room* as reference) with the transport swapped to Steam; **Mirror + FizzySteamworks** the lighter alternative. Pin versions at implementation.
- **Crowd sync (the hard part — §11):** **do not replicate every enemy.** Host simulates the authoritative enemy crowd; clients get lightweight state (spawn events + shared seed, aggregated/batched updates, interest management, lower rates for distant enemies) and render interpolated representations. At action-RPG scale (dozens) this is far more tractable than a musou swarm would be.
- **Host leaves:** run ends in v1 (no host migration — genuinely hard). Consider save-and-resume later.
- **Co-op roster:** the four-character roster needs **re-theming around action-RPG synergies** (launch→juggle, CC→burst-the-commander, frontline→support) — the old mark→detonate roles are gone. **Parked until co-op begins** (the prototype is solo); not blocking.

---

## 9. Technical Architecture

- **Pooled MonoBehaviours — DOTS/ECS likely unnecessary.** At **action-RPG scale (dozens on screen)**, pooled MonoBehaviours are comfortable at 60 FPS; ECS only earns its complexity at hundreds-to-thousands of agents. So the **DOTS/ECS migration is NOT an assumed step** — revisit it only if on-screen counts ever exceed what pooled GameObjects can hold at 60 FPS (likely never at this scale). **Players, camera, UI, bosses, and the enemy crowd all stay GameObject-based**; the prototype is **plain MonoBehaviour + object pooling**. **Do not prematurely convert; do not remove the pooling.**
- **Performance (60 FPS).** GPU instancing for crowd rendering, aggressive LODs, shared materials/atlases, object pooling everywhere (no `Instantiate`/`Destroy` in loops), profile from day one.
- **Data-driven.** Weapons, enemies, **Augments/effects**, and tuning live in ScriptableObjects mirroring `HORDEBREAKERS_Tuning.xlsx`; no magic numbers in code.
- **Synty pipeline.** Low-poly suits crowds (cheap in bulk, readable when packed); a faction pack per stronghold; convert Built-in materials to URP on import.
- **Prototype reality (so the doc matches the code):** combos are **timer + animator-tag driven** in `PlayerController` (not a formal FSM — that's fine, just not what "don't convert" is protecting). `GameManager` holds run state **in-memory only — no persistence layer yet.** The Augment system is the composable-SO model in §6, currently with a small starter effect library (StatMod, WeaponMod, GrantAbility-stub, Evolution); the **on-hit proc hook** is the next increment (it's the only effect that touches the swing path, so it lands isolated).

---

## 10. Onboarding (target: a new player completes the first stronghold unaided)

- [ ] Core verb (move + light attack) usable within **30 seconds**.
- [ ] **Defense taught early and as mandatory** — the first pressuring enemy forces a dodge/block; attack commitment and the dodge cooldown introduced gently in a low-stakes beat.
- [ ] **First arena is survivable** — early discipline, not early death; the front-loaded difficulty is "demanding," not "punishing."
- [ ] First **Augment draft** lands within the first arena clear — the player feels the build lever immediately.
- [ ] First run/stronghold ends on a **hook** — a new Augment, a new class, or the first elven aside that makes the player go "...wait, what did he just say?"

---

## 11. Risks, Build Order & Next Steps

### Biggest risk
**Co-op netcode** — syncing the enemy crowd across four clients. At action-RPG scale (dozens, not hundreds) this is far more tractable than a musou swarm, but still de-risk with a dedicated networking spike before scaling content.

### Build order (de-risk fun before scale)
1. **Single-player core prototype** *(in progress)* — deliberate melee (attack commitment) + cooldown/charge-gated dodge + guard-breakable block + Musou meter, on the Elden-Ring-adjacent action camera. Prove the §1 fun hypothesis: is the under-equipped fight satisfying on its own?
2. **Augment system increments** *(in progress with Code)* — composable effect SOs, `PlayerLoadout`, class-gating, rarity weights, the four starter effects (incl. one working Evolution chain as proof). **Next increment:** the on-hit proc hook + `OnHitProcEffect`, landed isolated. Then update GDD §6 to the final shipped model.
3. **Grand Ability System (Task A)** — 1–2 Dynasty-Warriors abilities alongside melee, damaging via `IDamageable`, server-authoritative, plugging into the Augment `GrantAbility` seam (no parallel path).
4. **Vertical slice** — one stronghold (one race), its arena set (waves → mini-boss/objective) + a Boss, a real Augment pool, the level-up flow. **Name the kingdom + game title here.**
5. **Co-op networking spike**, then **scale** — more strongholds/content. (DOTS migration only if pooled GameObjects ever can't hold 60 FPS — likely never at this scale; see §9.)

### Parked (deliberately, not forgotten)
- **Co-op roster re-theme** around action-RPG synergies — when co-op begins.
- **Augment pool + per-class pools** — grown continuously.
- **Persistence/save layer** — not built yet; needed before meta-progression and save-resume.
- **Names** (kingdom, title) — at the vertical slice.

---

## Changelog

| Version | Date | Notes |
|---|---|---|
| 0.1 | initial | Foundation: pillars, core loop, mark/detonate hybrid combat + enemy taxonomy, hybrid waves+boss, starter roster, host+Steam multiplayer, tech, onboarding. |
| 0.4 | 2026-06-28 | **Stamina removed.** The depleting bar (a duel mechanic that didn't gate a crowd game and wasn't fun) is gone from §1/§4. The deliberate floor is now **structural**: **attack commitment** (swings committed through startup/active, dodge-cancelable only in recovery via `dodgeCancelPhase`; mashing buffers into the string instead of escaping it) + **re-gated defense** (dodge gated by a refilling **charge bank** `dodgeMaxCharges`/`dodgeCooldown`; **block is guard-breakable** — heavy/elite `guardBreaks` attacks shatter a held guard and stagger you). Power curve moved to **Augment dials** that visibly loosen the gates: `AttackSpeed`, `DodgeCooldown`, `DodgeMaxCharges`, `DodgeCancelPhase`, `BlockMitigation`, `GuardBreakResist`, `Hyperarmor`, `MusouGainDealt/Taken`. Musou dealt-gain bumped so aggression accelerates the meter. §4.3 rewritten ("Stamina — a rhythm, not a noose" → "commitment & re-gated defense"); moveset/pillar/onboarding/build-order rows updated. Implemented in Code on `PlayerController`/`PlayerCombatData`/`StatModEffect`/`IDamageable`/`Enemy`/`Brute`. |
| 0.3 | 2026-06-24 | **Action-RPG scale + camera reframe.** Confirmed **action-RPG scale — dozens on screen at most, never hundreds**; **musou framing removed** throughout (§1/§3/§4/§5/§7/§8). Camera retuned to **Elden-Ring-adjacent** values on the `CM_PlayerCam` Cinemachine rig + `PlayerCameraRig`: **FOV 50, distance 4.5, pitch 15°, orbit height 2.0, look-at offset 1.5, follow damping 0.12** (all tunable; intimate, not pulled back). §9: **DOTS/ECS reframed as likely unnecessary** at this scale — revisit only if pooled GameObjects can't hold 60 FPS; prototype stays pooled-MonoBehaviour, **no code removed**. The "Musou meter" *mechanic* name is kept (it's the screen-clear ultimate, distinct from the cut musou *framing*). |
| 0.2.2 | 2026-06-24 | **Enemy telegraph legibility + a second archetype (in Code) & playtested.** New `AttackTelegraph` component: the whole enemy throb-glows (emissive MPB, crescendos toward the strike) during any wind-up — the asset-light, camera-angle-proof tell §5/§7 ask for; wired into the Husk lunge, the Charger, and the Brute slam (runs before `HitFlash` so a hit overrides it). New **Charger** archetype (Tier-2): data-driven on the existing `Enemy` via `EnemyData.archetype` (reuses the Husk prefab — no new art), rushes in and commits a long, very readable telegraphed dash that sweep-connects mid-charge with a big punish window if whiffed; spawns from the Husk pool (`WaveDirector.chargerData`, `firstChargerWave`/`chargerChance`). Open: ground-decal telegraphs still want a true height cue; more archetypes; an `EnemyBase` refactor (Enemy/Brute still duplicate). |
| 0.2.1 | 2026-06-24 | **Combat floor implemented (in Code) & playtested.** §4.2 facing reworded to a subtle, tunable rotation-only aim assist (0 = manual). Built on `PlayerController`/`PlayerCombatData`: the **stamina** system (§4.3 — costs on attack/dodge, fast regen, *empty ≠ defenseless* stumble dodge, movement free); **block** (frontal-only, mitigation + chip + stamina cost, partial when empty); the **Musou meter** (builds from dealing/taking hits → i-frame screen-clear AoE); the **juice split** (hitstop on heavies only, lights snappy). Objective fixes: heavy hit/VFX contact sync, pooled Brute telegraph. HUD stamina + musou bars added. All values `[PLACEHOLDER]` — tune by feel. Still open: dodge cooldown vs stamina, enemy telegraph legibility, enemy variety, audio. |
| 0.2 | this revision | **Major post-playtest pivot.** Identity reframed to **musou × action-RPG on a Souls-deliberate floor**; VS demoted to a donor of two ideas. **Cut:** auto-weapons, projectiles, mark/detonate, continuous mid-fight leveling, XP gems, the VS card draft, the elevated 3/4 camera. **Added/changed:** musou melee + **Musou meter**; **stamina-gated** dodge/block as a *rhythm, not a noose*; Augment-driven power curve with **front-loaded difficulty**; the **Augment system** (composable effect SOs, class-gated, rarity-weighted, synergy/evolution, `PlayerLoadout`) as the single canonical leveling path; **milestone (D&D-style) leveling** after combat only; the **three-tier nest** (Arena → stronghold Level → campaign Run) with arenas cleared by mini-boss *or* objective and stronghold-per-race structure; the **Dynasty-Warriors camera** + its telegraph consequence; a full **Narrative & Setting** section (enslaved-soldier reconquest spine; the elven long-con with the other races as unwitting muscle; light/absurdist delivery). Roster re-theme parked until co-op; names `[TBD]` until the vertical slice. Numeric values remain `[PLACEHOLDER]` pending playtest — tune in `HORDEBREAKERS_Tuning.xlsx`. |

> **Living document.** Version every significant revision. Numeric values are hypotheses marked `[PLACEHOLDER]` until playtested.
