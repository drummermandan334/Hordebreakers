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

---

# Night 3 (2026-06-24, overnight) — VFX fix, VS-era teardown, hitboxes, camera, UI

5-task batch with advance permission. Working order (by dependency/risk): VFX fix → cleanup → hitboxes → camera → UI.

## Flags for the morning (read these)
- **GDD CONFLICT:** removed the **mark → detonate** loop (the "heavy explosion") per your instruction. `HORDEBREAKERS_GDD.md` still calls mark→detonate the *core loop* — it needs a rewrite to the action/wushu direction. Flagging rather than silently diverging.
- **Cinemachine** package added — the new camera task required it.
- **Kept the thrown dagger** (ThrowWeapon, Q) as a ranged option; it fed Marks but works as plain damage. Say the word if you'd rather cut it.
- Upgrade-card scaffolding (UpgradeCard/UI + the 2 generic cards) **kept** for the vertical slice; the 5 VS-specific cards (auto-weapon / detonation upgrades) removed.

## Task status (your numbering)
- [x] **1. VFX attach** — `FX_SwordStab_01` was World-sim, so the burst was left behind by the swing. `WeaponVfx.Spawn` now forces every spawned particle system to **Local**, so any effect rides the blade. Stab verified on the sword; the light-jump slash was already Local/attached (the big swipe just reads as floating mid-leap — `WeaponVfx.localPosition`/`scale` are tunable).
- [x] **2. UI** — DONE. Replaced the code HUD with the **Synty Fantasy Warrior HUD** art.
  - `HUDCanvas` now hosts: `HUD_FantasyWarrior_HealthBar_01` (top-left, heart icon + red fill), `_Parts/HUD_FantasyWarrior_XPBar_01`
    (bottom-center, cyan fill + "x / next" label), and a `Label_FantasyWarrior_Header` (Grenze font) reading "Lv x  Wave y  Kills z".
  - Both bars are Synty **Sliders** (driven by `Slider.value`); text is **TextMeshPro**. `HUDController` rewritten to drive
    `healthSlider`/`xpSlider`/`xpText`/`infoText` (was Image-fill + legacy Text). Deps already in project (InterfaceCore, Grenze SDF font).
  - Verified in play: dealt damage → health bar dropped; +kill/+xp → "Kills 1" and XP bar "2/5" cyan fill updated. Zero errors.
  - Chose individual widgets over the pre-made `_PreMadeHUDs/Screen_FantasyWarrior_HUD_ARPG_01` (73 nested instances — overkill).
  - Polish later if wanted: bar sizes/positions, font sizing, and the empty XP bar reads plain grey until XP accrues.

## Morning feedback round (2026-06-24, with Daniel)
- **Player wasn't hit-reacting** — root cause: the `invincible` debug flag was still **ON**, so `TakeDamage` returned
  before the flinch (no damage, no react). Turned **invincible OFF** on the Arena01 player. Verified in play: a frontal
  hit now drives `Locomotion → HitReactF`. (Enemies were reacting fine all along.)
- **Camera lowered** — `PlayerCameraRig.defaultPitch` 20→13 and OrbitalFollow `VerticalAxis` 20→13 + `TargetOffset.y` 1.4→1.1.
- **Camera shake OFF** — added `PlayerCameraRig.enableShake` toggle (set false on the scene rig) so hit-reactions read clearly.
- **STILL OPEN (Daniel: "problem for later today"):** melee **hitboxes + reaction times need tuning**; **auto-targeting
  (`SoftTargetFace`) feels terrible** — wants manual/aim-based targeting instead.

### Combat-feel round 2 (2026-06-24) — after camera/reactions confirmed good
- **Targeting → MANUAL.** Removed `SoftTargetFace`/`FindNearest` + `PlayerCombatData.softTargetRange`. On a swing the
  player now faces `AimFace()` = the camera-relative movement direction (where you're pushing); no auto-snap to nearest,
  keeps current facing when there's no input. Hit arc follows facing, so you hit where you aim.
- **Melee hitbox now resolves at the swing's CONTACT phase, not on the press frame.** Was: `MeleeHit` ran in `AttackInput`
  *before* the forward lunge moved the player → lunging swings could whiff / damage didn't match the visible blade. Now the
  swing is "armed" on press and resolves once in `Update` at `meleeContactPhase` (0.35, Inspector) — after the lunge carries
  you in. One hit per swing (`_swingHitResolved`). Reach/arc/radius left as-is (didn't blind-tune; judge now that timing's fixed).
- **Reaction times NOT touched yet** — subjective, want Daniel's direction (enemy `hitReactTime` 0.3 / player `hitReactCooldown` 0.5).

### Combat backlog — NOTED, not yet done (Daniel, 2026-06-24)
1. **Player hits should knock the enemy back.** Add/strengthen knockback on the enemy when the player lands a hit (impact
   feel). Note: `Enemy.TakeDamage` already sets `_knockback` from the hit source via `EnemyData.knockback` — likely just
   too weak/unnoticeable, so bump it and/or make it read better (it currently decays via `knockbackDecayRate`).
2. **Improve the enemy attack — it closes too far before striking, and the strike needs forward movement.** The enemy should
   commit to its attack from FURTHER out and lunge INTO the player (a committed forward dash), instead of walking right up
   then doing a short poke. Levers in `Enemy.cs` / `EnemyData`: raise the attack-initiation distance (standoff ring /
   `lungeConnectReachMult`), and give the lunge more travel (`lungeSpeed` × `lungeTime`) so the strike carries forward.

### Next-up / process
- After the **UI/HUD** task, Daniel will **equip new plugins** to speed development along (TBD which) — expect a tooling change.
- [x] **3. Hitboxes** — DONE. Directional front/back/left/right hit-reactions for enemies + player.
  - Clips: `A_Hit_{F,B,L,R}_React_Sword` (SwordCombat pack). Added `HitReact{F,B,L,R}` states + `Hit`(trigger)/`HitDir`(int)
    params to all 3 controllers (`Husk`/`Brute`/`Player`) via editor scripting; `AnyState → HitReact{dir}` on `Hit && HitDir==n`,
    exit→Locomotion (exitTime 0.7). React states tagged `HitReact`.
  - `IDamageable` gained `TakeDamage(amount, Vector3 sourcePos)`; the old 1-arg version defaults to a frontal hit.
    All call sites pass the source: melee → player pos, projectile → its pos, enemy lunge / brute slam → their pos.
  - `HitReaction.Direction(...)` (new, `Combat/`) classifies F/B/L/R from the source relative to the victim's facing.
  - Enemies: every hit now reacts toward the hit; knockback recoils away from the **source** (was always away from the player).
    Brute keeps poise mid-slam. Player: flinch is **gated** — only when not mid-attack/dodge, on a `hitReactCooldown` (0.5s,
    Inspector) so a dense horde can't stunlock it; no movement lock. Grip/arm override layers blend off during the flinch
    (new `IsBaseInHitReact`) so the full-body react owns the arm.
  - **FEEL TO CHECK:** player flinch frequency/length, and whether B/L/R reads right vs. the camera. All knobs Inspector-exposed.
- [x] **4. Cleanup** — DONE. Detonation/Marks already stripped from Projectile/ThrowWeapon + PlayerController.ApplyUpgrade.
  Finished the teardown the morning after (comp restart had left it half-done with a compile error):
  - Deleted scripts: `AutoWeapon.cs`, `WeaponData.cs`, `Markable.cs`.
  - Removed their components: `AutoWeapon` off `Player.prefab` (was a missing-script ref), `Markable` off `Brute`/`Husk` prefabs.
  - Deleted assets: `ThrowingKnives.asset` (WeaponData instance) + the 5 VS cards (`Card_AutoDamage/FireRate/ExtraMark/DetRadius/DetPower`).
  - Trimmed `UpgradeType` enum to `{MoveSpeed, MaxHealth}`; repointed the 2 kept cards' `type` (5→0, 6→1) and trimmed the
    `LevelUpUI` (inactive) `UpgradeCardUI.pool` from 7→2 cards. Compiles clean, zero console errors/warnings.
  - NOTE: `Assets/_Recovery/*.unity` backups still hold dangling GUID refs to the deleted cards — they're unloaded backups, harmless.
- [x] **5. Camera** — DONE (you chose "convert to Cinemachine"). Installed **Cinemachine 3.1.7**.
  - Main Camera now has a `CinemachineBrain`; new `CM_PlayerCam` = `CinemachineCamera` + `CinemachineOrbitalFollow`
    (Sphere, **WorldSpace** binding, radius 8, target offset y1.4, pos-damping 0.3) + `CinemachineRotationComposer`
    (aim offset y1.3) + `CinemachineDeoccluder` (wall avoidance) + `CinemachineImpulseListener`/`Source`. Follow/LookAt = Player, FOV 65.
  - The musou feel is preserved by a thin **`PlayerCameraRig`** controller that feeds the orbit axes (HorizontalAxis = world
    yaw, VerticalAxis = pitch): stay-behind from actual velocity, anti-spin back-pedal cutoff, hold-Ctrl-mouse / right-stick orbit,
    ease-back-behind on release — i.e. all the old `ThirdPersonCamera` behaviour, now driving Cinemachine instead of the transform.
  - Camera-relative movement already worked (PlayerController reads `Camera.main`); unchanged. Old `ThirdPersonCamera.cs`
    **deleted** and its component removed from Main Camera. `Shake(amount,duration)` ported to a Cinemachine **Impulse**
    (static `PlayerCameraRig.Shake` facade; the 3 callers — hit-juice ×2, brute slam — repointed).
  - Verified in play: correct behind/above framing, player low-center, zero console errors (screenshot captured).
  - **FEEL TO CHECK / tune (all on `PlayerCameraRig` or the CM components):** `shakeForceScale` (8 — impulse↔old-offset mapping
    is approximate, may want bigger/smaller), orbit sensitivity, `autoFollowStrength`, pitch range, Deoccluder collision mask
    (set to Default layer; verify it ignores enemies). The bespoke anti-spin is ported but worth a back-pedal sanity check.
