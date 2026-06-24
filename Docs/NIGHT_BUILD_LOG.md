# Night Build Log — autonomous session (2026-06-23, overnight)

## ☀️ MORNING SUMMARY (read this first)
Done & verified in play (zero console errors all night): **1 (combos), 2 (throw), 3 (jump), 5 (mobs).**
Task **4 (classes)** = researched + a full plan in `Docs/CHARACTER_CLASSES_PLAN.md` (NOT auto-built — Sidekick
only had Knight parts, and the others need decisions + systems best made with you awake).

**New controls:** LMB = light, RMB = heavy, **Space = jump**, **LeftShift = dodge**, **Q = throw dagger**.
Pad: X/Y light/heavy, A jump, B dodge, LB throw. Combo is **interchangeable** — mix Light/Heavy for 3 hits
(L-L-L, L-H-L, L-L-H, H-H-H…). The **heavy 3rd hit detonates Marks**.

**Test in the morning:** mash L/H mixes (watch the combo branch), jump (Space) + attack in the air, throw a
dagger (Q) at a husk, and watch the mobs — they're armed and some rush while others circle.

**Still set for testing (revert when ready):** `invincible` = ON on the player; slash VFX = OFF.
**Known rough edge:** enemy weapon **grips** are approximate (quick tune — see task 5 caveat).
**Tuning knobs:** combo chain window (transition exit times, currently 0.5), `lungeStep`/`attackStep`,
`jumpSpeed`/`airControl`, ThrowWeapon damage/cooldown, EnemyData attack ranges, the per-enemy aggression curve.

---


Daniel asked for a big 5-part batch and went to sleep with advance permission. This log records the
plan, every decision made without him, status per task, what still needs doing, and assets to acquire.
**Nothing here overrides the GDD or CLAUDE.md** — where it diverges, it's flagged.

## Guardrails I'm holding to
- Don't break the working combat (the prop-bone sword + LightCombo01 thrust we just fixed).
- Additive/reversible changes; document each.
- `invincible` stays ON (debug) so the loop is testable; slash VFX stays off until combat's settled.
- Unity may defer script compiles while unfocused — if a compile stalls, I do all compile-free work
  (animator/scene/asset via execute_code) and queue the scripts.

## Decisions made autonomously (review these)
1. **Heavy becomes a combo; detonation moves to the heavy-combo FINISHER.** CLAUDE.md says the heavy
   attack detonates Marks (core loop). To keep that alive while making heavy a real combo, the 3rd heavy
   hit triggers the AoE detonation. If you'd rather detonate on a dedicated button, easy change.
2. **Interchangeable combos = 3 slots, each Light or Heavy.** Slot1/2/3 each have a Light and a Heavy
   variant; input picks which. 8 routes (LLL…HHH). All chain (blended); some flow better than others.
3. **Input remap:** Space=Jump, Dodge=LeftShift (+gamepad). Ctrl stays camera-orbit. LMB=light, RMB=heavy,
   new throw key for the projectile (planned: Q / gamepad LB).

## Task status
- [x] 1. Heavy combo + interchangeable combo system — DONE & verified in play (Light→Heavy chained L1→H2).
- [x] 2. Projectile (thrown Synty dagger) — DONE (ThrowWeapon + Projectile_Dagger prefab, Q / pad LB).
- [x] 3. Jump + jump attacks — DONE (Space/A; JumpUp+Fall states; aerial attacks reuse the combo).
- [~] 4. Sidekick characters — RESEARCHED + PLANNED (not auto-built — see below). Full plan in
  **`Docs/CHARACTER_CLASSES_PLAN.md`**.
  - **Key finding:** installed Sidekick parts only cover **Knight** (`FANT_KNGT`). No Barbarian/Mage/Wizard/
    Rogue/Archer Sidekick parts — those need acquiring OR the pre-made characters we already own.
  - We OWN Humanoid-rigged characters for 5/6 classes: Barbarian (Rivals `BarbarianGiant`), Rogue
    (`Male_Rouge`), Mage (`Sorcerer`/`Witch`), Wizard (`Male_Wizard`). All verified Humanoid + Hand_R → they
    take our combo/anims directly. **Archer** is the only true gap (needs a bow + archer character + bow anims).
  - Did NOT auto-build them: each needs weapon placement + per-class tuning, and casters/archer need anim sets +
    a spell system we don't have. Better with Daniel awake. Recipe + asset map are in the plan doc.
- [x] 5. Mob weapons + attacks + behavior — DONE (see caveat).
  - Behavior: per-instance **aggression** (0..1) — aggressive husks crowd in close + strike often,
    cautious ones hang back at a wider ring + circle more. Compiled clean.
  - Attacks: Husk attack clip -> `HeavyCombo01A` swing; Brute -> `HeavyCombo01C`. Reads as a telegraph.
  - Weapons: Husk = goblin axe, Brute = large goblin axe, attached to `Hand_R` (child "EnemyWeapon").
  - **CAVEAT:** enemy weapon **grips are approximate** (lie along the forearm in T-pose). They're rigid on
    Hand_R (no prop bone). Needs a quick tuning pass — rotate/position the `EnemyWeapon` child in each
    prefab. Material imported as "Lit" (may want a `PolygonDungeon_*` atlas mat). Low effort to fix.

### Combo system reference (for tuning)
- AnimatorController `PlayerAnimator`: Base layer states Locomotion, L1/L2/L3 (LightCombo01 A/B/C),
  H1/H2/H3 (HeavyCombo01 A/B/C), JumpUp/Fall, Dodge. All attack states tagged "Attack".
- Tree: Loco --Light--> L1, --Heavy--> H1.  Each slot-state --Light--> next-Light, --Heavy--> next-Heavy.
  So L-L-L, L-H-L, L-L-H, H-H-H … 8 routes. Chain exit time = 0.5 (press within first ~half of a clip).
  Raise exit times if the chain window feels tight.
- Heavy 3rd hit (H3 reached via the controller's combo step) DETONATES Marks. Inputs: LMB light, RMB heavy,
  Space jump, LeftShift dodge, Q throw (pad: X/Y/A/B/LB).

## Assets we HAVE (discovered)
- Packs: AnimationBaseLocomotion, AnimationSwordCombat, PolygonDungeon (huge weapon set), PolygonDungeonRealms
  (hero), PolygonFantasyCharacters, PolygonFantasyRivals, PolygonWerewolf, SidekickCharacters, PolygonParticleFX.
- Weapons: axes, large hammers (2H), maces, spears, shields (Heater/Ornate), staffs — `PolygonDungeon/Models`.
  Daggers/knives: `SM_Wep_Knife_Small_01` (DungeonRealms), `SM_Prop_Dagger_01` (FantasyCharacters).
- Jump clips: `A_Jump_Idle/Running/Walking_Femn`, `A_InAir_FallShort/Large_Femn`, `A_Land_Idle{Soft,Medium,Hard}_Femn`.
- Heavy combo clips: `A_Attack_HeavyCombo01{A,B,C}_Sword`. Air attack: `A_Attack_LightLeaping01_Sword` (only one).

## Assets to ACQUIRE (outside current packs) — running list
- A dedicated **throw/cast** animation (none clean for throwing while sworded). Using no-anim throw for now.
- **Bow + bow-draw/shoot anims** for Archer (not in SwordCombat). Need a ranged-combat anim pack or custom.
- **Staff/cast anims** for Mage/Wizard (SwordCombat is melee-only). Need a magic/caster anim pack.
- **Dual-wield / shield-block / 2H** attack anim sets — SwordCombat is one-handed sword only. Knight/Rogue/
  Barbarian will reuse sword anims as placeholders until a matching anim pack is acquired.

## Progress log
- Fixed sword thrust earlier (prop bone). Then: discovery done.
- Wrote `PlayerController` (interchangeable combo + jump + remap) and `ThrowWeapon` (pooled dagger). **Compiled clean.**

---

# Day-2 follow-up (2026-06-23, with Daniel) — combat overhaul

Several rounds of feedback. All compiled clean; verified in play where possible (input-driven *feel* left to Daniel).

## Direction
**Pivoting away from the Vampire-Survivors auto-weapon model toward action / wushu combat.** So hit-reacts now fire on *every* hit (no cooldown) and combos are deliberate. **OPEN DECISION:** the `AutoWeapon` is still on the Player and is the current source of **Marks** for the heavy-combo detonator — needs a call on removal + how Marks get generated going forward.

## Player combat
- **Jump attacks (per SwordCombat doc §5):** the full leaping clip starts with a run-up+jump, so air attacks enter the *attacking part* via a transition `offset`. Light-in-air → `A_Attack_LightLeaping01_Sword` (offset 0.5); Heavy-in-air → `A_Attack_HeavyCombo01C_Sword` (`JumpAttackHeavy`, offset 0.45).
- **Combo rebuilt animator-driven.** Old bug: a fixed code timer (0.55s) outran the ~2s heavy clips and cut swings. Now the chain waits until the current swing has played `comboChainOpen` (0.75) of its clip, then chains on the buffered/pressed input — each swing animates fully. Chain transitions are trigger-only (no exit time); solo swings → Locomotion at 0.9.
- **Attack movement:** the forward lunge drives movement and overrides input mid-swing (only a slight `attackSteerSpeed` steer) — no more gliding.
- **Slash VFX:** `FX_SwordSlash_01` (light) + `FX_SwordStab_01` (heavy), spawned in front of the player (`slashLocalPos` tunable). Detonation explosion VFX **removed**.

## Enemies
- **Weapons on the Prop Bone socket** (Synty `Setup Prop Bones` tool): `Hand_R → Prop_R → Prop_R_Socket → weapon`, so the sword-combat attack clips drive the weapon. Husk rig = `Chr_Skeleton_01`, Brute = `Chr_Undead_Knight_01`; material `PolygonDungeon_01`.
- **Hit reactions:** every hit plays `A_Hit_F_React_Sword` + a brief movement stagger; re-triggers per hit (hitstun). **Brute has poise** — never flinches mid-slam.
- **Crowd separation:** kinematic push-apart (`separationRadius`/`separationForce`) so enemies don't pile on / clip through each other or the player.

## Still open / next
- Player **hit-react on taking damage** + **hitboxes matched to each hit reaction** (next task).
- `invincible` debug flag still ON on the Arena01 player.
- The AutoWeapon / Marks decision above; and combo/VFX/separation **feel** wants Daniel's eyes.
