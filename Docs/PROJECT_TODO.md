# HORDEBREAKERS — Project To-Do / Backlog

> Living checklist — check items off as they land. Code can read and update this too.
> **Legend:** `[ ]` open · `[x]` done · 🔨 in flight with Code · 🎯 playtest gate · ⏸ parked/blocked · 🧍 needs you driving (Code can only test an idle dummy)

---

## 🔥 Loose ends — NOW
- [x] **Commit the commander / husk-taming pass** *(committed `cb6f255d`)*
- [x] **Resolve the ring tension** *(resolved: `holdRingMult` → 1.4 for a wide circle, but the rusher now STALKS into melee range during its wind-up so it strikes from close regardless — wide encirclement AND interruptible strikes)*
- [ ] **The big tuning pass** — dial the whole sheet to taste: waves (cadence / size / composition), enemy speed / damage / telegraphs, the XP curve. *Getting the push-luck-vs-cash-out gamble and the melee feel right IS the playtest.* (Maybe fold the knobs into `HORDEBREAKERS_Tuning.xlsx`.)
- [ ] **Merge PR #2 (enemy patrol)** then **promote `combat-aaa-integration` → `master`** once it's lived-in (master is still the old pre-combat state).
- [ ] **Landing SFX** — currently a footstep placeholder; source a real landing thud (+ optional sprint-transition + patrol-amble anim polish).

## 🌟 Vertical slice — next priorities (from 2026-06-30 planning)
- [x] **Enemy patrol** — passive enemies amble around their spawn (PR #2)
- [ ] **Progression spine** — the per-level **stat-growth curve** + how augments scale/progress. *Backbone of the power fantasy; the 2-round demo's "feel the upgrades" rests on this. Weapon-agnostic — do regardless of the spear call. (Broader than the Augment-content-pool item below; this is the baseline curve.)*
- [ ] **Swap the single objective** to playtest other arena-objective types for the slice — objective TBD (Destroy / Hold / Reach already stubbed via `IArenaObjective`; see Encounter-loop section). *Not a 2nd objective — replacing the current one.*
- [ ] 🧍 **DECIDE: vertical-slice faction** — lock which invading faction is in the slice. *Gates enemy models/anims; unblocks the enemy-anim dial-in.*
- [ ] 🧍 **DECIDE + ACQUIRE: Staff/Spear MELEE anim pack** — the player (Battle-Sorcerer) is a **staff/spear** wielder but currently runs the **sword** moveset. *Lock before more combat-moveset content so the melee feel isn't tuned twice — the feel work (i-frames/interrupts/hit-stop/momentum) transfers; only the moveset + reach numbers are weapon-specific.*
- [ ] **Musou animation swap** — replace the current Musou anim *(do it WITH the spear pack — the ult's moveset changes too)*.
- [ ] **More skills / abilities** — author into the augment/ability pools *(after the progression spine, so they slot into a real curve)*.
- [ ] **Enemy animation dial-in** *(after the faction decision, so the final enemies get polished)*.

## 🌀 Encounter loop (arena landed → follow-on)
- [ ] **Other objective types** — Destroy / Hold / Reach. (`IArenaObjective` is pluggable; only **Slay** exists.)
- [ ] **Post-clear flow** — *partially addressed: the 2-round demo now does clear → draft → round 2 → DEMO COMPLETE.* Still needs the real next-arena (village→keep arc) + despawn of the leftover field between rounds.
- [ ] **Death-beat polish** — death currently waits 2s then resets while enemies mill; add a real "YOU DIED" freeze.

## 🤖 Enemy AI / roster (behavior-tree foundation landed → the payoff)
- [ ] **Tune the regular Brute reinforcements** — only the *commander's* slam was shrunk; `BruteData`'s slam is still big.
- [ ] **Per-race variants** (goblin / undead / dwarf / elf…) — `EnemyData` variants on the existing archetype graph + signature quirks as data-gated branches. *Synty goblin/etc. anim packs already imported — teed up.*
- [ ] **New archetypes** — a ranged enemy, and a real commander/boss archetype with its own moveset (right now it's just a beefed Brute).
- [ ] **Enemy attack audio / feedback** — enemies are silent on telegraph / lunge / slam; add SFX + maybe a sharper tell.
- [ ] **Delete the FSM fallback** from `Enemy`/`Brute` once the BehaviorTree brain is fully trusted (dead code behind the Brain toggle).
- [ ] *(Later)* refactor the shared **telegraph → commit → recover** spine into a Unity Behavior **subgraph** if the roster grows.

## 🧍 Verify / polish — needs you driving
- [ ] **Real-play checks** — lock-on acquiring/tracking the BT enemies; the telegraph-abort actually firing when you dodge clear; hit-feel intact.
- [ ] **XP-gem pickup radius** — gems only vacuum within ~5m, so you must wade into the crowd to collect. Confirm that risk/reward feels right.
- [ ] **HUD layout pass** — health / musou / xp + dodge pips + objective + wave countdown + prompts may get cluttered.
- [ ] **Perf check** — profile a full arena (24+ behavior-tree agents ticking) for GC / frame spikes.

## 🎯 Playtest gates (the "is it fun" milestones)
- [ ] 🎯 **Encounter loop** *(now live to test)* — does the player *visibly hesitate* between farming and cashing out? (`encounter-loop-spec.md` §9). Blocked on the big tuning pass + you driving.
- [ ] 🎯 **Combat without stamina** — does it still feel deliberate? (commitment carries it; dodge isn't spammable; block isn't a free turtle)

## 🎨 Design decisions — OPEN
- [x] **Roster decided — 9 classes, Devastator 3-deep.** Anvil: Knight + Berserker · Devastator: Mage *(pure)* + Battle-Sorcerer *(hybrid)* + Gunslinger · Disruptor: Monk + Necromancer *(human meat-wall)* · Support: War-Priest + Warlord. *(Disruptor/Support still need asset-coverage confirmation — see asset review.)*
- [ ] **Run / campaign structure spec** — strongholds per run, run length, between-run meta. *(After the encounter loop is proven fun.)*
- [ ] **World design bible** — stronghold-races, per-race rosters & art, how the elven long-con surfaces, the final all-races stronghold. *(Include the Necromancer beat: a human using the Elves' own expendable-bodies logic against them.)*
- [ ] **Boss design** — per-stronghold bosses + the final boss(es)
- [ ] **Name the human kingdom + the game title** — deferred to the vertical slice

## 🧰 Asset review — OPEN (gates Disruptor & Support)
- [ ] **Attack / locomotion animation coverage — per class** *(the roster's long pole; check anims alongside models + VFX in the pass).* Motion is an art asset — Claude can wire Animator Controllers / blend trees / events but **can't create animation clips**; source from Synty anim packs → marketplace/Mixamo → commission/mocap.
  - *Likely covered by existing Synty anim packs:* Knight (sword+shield), Berserker (two-hander).
  - *Needs verifying / likely gap-filling:* Gunslinger (gun handling — fire/reload across revolver→AR; models confirmed, **animations are separate**), Monk (martial arts), Mage + Battle-Sorcerer (spellcasting), Necromancer (summoning / raising gestures).
- [ ] Confirm **martial-arts / unarmed animations** (Monk)
- [ ] Confirm **Necromancer minion assets** — raise/summon VFX + summoning anims. *(Minions can likely reuse the undead invading-race skeletons/corpses already in the game.)*
- [ ] Confirm **banner/totem props + holy/buff VFX** (War-Priest / Warlord)
- [x] Gunslinger firearm **models/packs** — confirmed (Western / War / Battle Royale cover the arc; firing/reload *animations* still to verify — see coverage item above)

## 🔨 Build — landed
- [x] **Remove stamina** → animation-commitment + re-gated dodge/block + Augment dials *(merged `0fa36c04`; further built in the combat overhaul)*
- [x] **Camera retune** to Elden-Ring values *(CM_PlayerCam + PlayerCameraRig; old ThirdPersonCamera deleted; AAA-feel pass added dynamic FOV)*; DOTS flagged unnecessary

## 🔧 Build — pending / next
- [ ] **Redefine the Battle-Sorcerer → hybrid (scale melee OR magic).** It's built as a ranged-bolt zoner; give it a real staff-melee build path and **fork its Augment pool into a magic wing + a melee/spellblade wing** (+ hybrid augments that reward mixing). Its co-op seat then flexes with the build. *(A redesign of the one already-built class — highest-effort pool.)*
- [ ] **Augment content pool** — author the class-gated pools (each class's escalation arc → its pool). Start with the locked Anvil/Devastator classes; the hybrid Sorcerer's forked pool and the Necromancer's summon/control pool are the unusual ones.
- [ ] **Persistence layer** — campaign saves + unlock tracking (required for hybrid death/meta; not built yet)
- [ ] **Big magical attacks** (Mage / Battle-Sorcerer) — pending from combat handoff
- [ ] **Block polish** — stance/pose/feedback (logic exists; intersects with stamina removal)
- [ ] **Heavy-combo + knockback feel-tuning**
- [ ] **Cleanup** — orphaned old fireball prefab + stray `Projectile.cs` fields

## 📄 Docs to keep synced
- [ ] **Update `encounter-loop-spec.md` + GDD to the WAVE model** — both still describe the old finite-budget/mobilization cadence; the build moved to a wave model.
- [ ] **GDD** — also reflect stamina removal (§4), scale walkback (§3/§5/§7/§9), final Augment model (§6) as they land
- [ ] **Roster bible** — ✅ updated to the 9-class roster (v0.2); revisit Disruptor/Support only if the asset review forces a change
- [ ] **Tuning sheet** — populate as values get playtested (still mostly `[PLACEHOLDER]`); fold in the arena knobs from the big tuning pass

---

## ✅ Recently locked / done (context)
- [x] **COMBAT FEEL OVERHAUL (2026-06-29/30) — BUILT + on `combat-aaa-integration`** (the big arc this TODO predates):
  - **Melee counterplay** — wider orbit ring + circle-before-attack dwell, rusher *stalks into melee range during its wind-up* (so you can interrupt it), husk/charger interrupted when hit mid-attack, **incoming-attack HUD arrow** (`IncomingAttackWarning`)
  - **Survivability** — dodge i-frames now span the roll (own timer, was tied to the 0.25s dash), longer enemy wind-ups, **wider attack cone + bigger sword reach/scale**, debug-invincible flag turned OFF
  - **Aggro / leash system** — enemies spawn **PASSIVE** until alerted (proximity / damage / ally-alarm), leash back when you break away; commander hunts. *Killed the Vampire-Survivors swarm.*
  - **Enemy patrol** — passive enemies amble around their spawn (PR #2, awaiting your merge)
  - **AAA-feel pass** (cloud ultraplan, integrated — PR #1 merged) — locomotion **momentum**, **sprint** (+ run anim), dynamic camera **FOV / directional shake**, **kill-confirm slow-mo**, **URP post-FX** (damage vignette / low-HP framing / dodge chromatic / musou grade)
  - **Encounter** — **2-round demo loop** (clear → draft → round 2 → DEMO COMPLETE), **hard charger cap (2)**, **no-spawn-within-8m-of-player**
  - **Repo** — pushed to `github.com/drummermandan334/Hordebreakers` (LFS; default branch `combat-aaa-integration`)
- [x] **Playable encounter arena — BUILT** — `GarrisonDirector`, wave spawning, Slay objective (`IArenaObjective`, pluggable), XP gems, accrue→bank→draft, ARENA CLEARED + death-reset
- [x] **Enemy behavior foundation — BUILT** — Unity Behavior "Brain", archetype graph, telegraph→commit→recover with abort, Husk/Charger/Brute ported (FSM fallback still present behind the toggle, pending delete)
- [x] **Identity** — musou × action-RPG on a Souls-deliberate floor → **action-RPG scale (dozens)**, **early-God-of-War** crowd combat
- [x] **Combat model** — deliberate melee combos + Musou meter (auto-weapons / mark-detonate cut); **lock-on as an opt-in tool**
- [x] **Stamina** — decision to **remove** it → commitment + re-gated defense (build in flight)
- [x] **Encounter loop design** — Objective-vs-Temptation dial, finite garrison, hybrid death, no stealth ("Braveheart, not Bond")
- [x] **Augment system** — composable effect SOs (stat/weapon/heal/grant-ability/evolution/on-hit-proc), class-gating, rarity, evolution chains, `PlayerLoadout` — built
- [x] **Grand Ability System** (server-authoritative-ready) — built
- [x] **Camera** — Elden-Ring-adjacent Cinemachine rig (follow/manual/recenter/lock-on framing)
- [x] **Enemy-AI foundation decision** — Unity Behavior + `EnemyData` split
- [x] **Roster framework** — 4 roles × 2, interlocking; tone = "expected archetype cranked to 11"
- [x] **Anvil locked** — Knight + Berserker · **Devastator locked** — Battle-Sorcerer + Gunslinger (Western→War→BR, register #2)
