# Spear Kit — Animator Wiring & Tuning Guide (S4)

> Companion to the spear-kit code drop (per-slot `AttackSlotTuning`, thrust capsule hitbox, per-state
> WeaponVfx fractions) and the tuned `PlayerCombatData.asset`. All clips are the pack's standalone
> Humanoid `.anim` files under `Assets/SpearAnimationPack/Animations/Humanoid/02_Attack/`. All 60fps.

## 1. Why this mapping

Chain cadence derived from the pack's own `_All` clips ((All − finisher) ÷ 3 chained steps = the
author's intended cut rhythm):

| Chain | Cadence | Read |
|---|---|---|
| Combo_01 | 0.69s/step | fastest opener (1.83s) — **the light kit** |
| Combo_02 | 0.58s/step | fastest chain — reserved (haste augment / 2nd kit) |
| Combo_03 | 0.94s/step | slowest, uniform ~2.5s steps — **the heavy kit** |
| Combo_04 | 0.83s/step | reserved |
| Combo_05 | 0.75s/step | reserved |
| Air      | 0.61s/step | jump attacks |

We use steps **1, 2, 4** of each chain (skip step 3): our combo is 3 slots, and step 4 is each
chain's true finisher (biggest motion). The `_04` clips ARE finishers — don't sub in `_03`.

## 2. Animator wiring (PlayerAnimator.controller)

| State | Motion (.anim) | Suggested state Speed |
|---|---|---|
| L1 | `01_Combo_Attack_01/Combo_Attack_01_01` | 1.15 |
| L2 | `01_Combo_Attack_01/Combo_Attack_01_02` | 1.15 |
| L3 | `01_Combo_Attack_01/Combo_Attack_01_04` | 1.1 |
| H1 | `03_Combo_Attack_03/Combo_Attack_03_01` | 1.0 |
| H2 | `03_Combo_Attack_03/Combo_Attack_03_02` | 1.0 |
| H3 | `03_Combo_Attack_03/Combo_Attack_03_04` | 1.0 |
| JumpAttack | `06_Combo_Attack_Air/Combo_Attack_Air_01` | 1.0 |
| JumpAttackHeavy | `06_Combo_Attack_Air/Combo_Attack_Air_04` | 1.0 |

- Keep every state's **Tag = "Attack"** (the whole combat loop keys off it).
- State **Speed** multipliers sharpen the light tempo without touching the augment dial
  (`attackSpeedMult` stays the AttackSpeed param driver on top).
- Transitions: keep your existing trigger routing (Light/Heavy per slot). Exit-time transitions back
  to locomotion can stay as-is — the per-slot `chainOpen` now governs when the NEXT input chains,
  not the state's exit time.
- Contact phases are normalized time, so they stay correct under any state Speed / AttackSpeed.

## 3. What's already tuned in PlayerCombatData.asset

| Slot | contact | chainOpen | step | shape |
|---|---|---|---|---|
| L1 | 0.28 | 0.40 | 3.2 m/s × 0.12s | Thrust |
| L2 | 0.26 | 0.37 | 3.2 × 0.12 | Thrust |
| L3 | 0.35 | 0.60 | 5.0 × 0.15 | Thrust, reach ×1.15 |
| H1 | 0.30 | 0.40 | 1.8 × 0.12 | Sweep |
| H2 | 0.30 | 0.40 | 1.8 × 0.12 | Sweep |
| H3 | 0.35 | 0.65 | 4.5 × 0.15 | Thrust (impale) |
| JumpL | 0.35 | — | 2.5 × 0.12 | Thrust |
| JumpH | 0.40 | — | 2.5 × 0.12 | Sweep |

Also set: `dodgeCancelPhase 0.45` (cancels open just after the chain point — snappier spear feel),
`thrustRadius 0.5`, `thrustArcDot 0.55`. Everything else (your dodge/HP/musou tuning) untouched.

## 4. WeaponVfx inspector (player prefab)

- `stabStates` = **L1, L2, L3, H3**
- `lightStrikeFractions` = **0.28, 0.26, 0.35** · `heavyStrikeFractions` = **0.3, 0.3, 0.35**
- `jumpLightStrikeFraction` = **0.35** · `jumpHeavyStrikeFraction` = **0.4**
- (These mirror the contact phases — if you retune a slot's contactPhase, change its fraction too.)

## 5. First playtest checklist (5 minutes)

1. **Thrust/Sweep sanity (per slot):** play each attack on the training dummy. Tip drives FORWARD =
   Thrust (correct); tip arcs SIDEWAYS = flip that slot's `hitShape` to Sweep (and vice versa).
   My Sweep guesses on H1/H2 and Thrusts on lights are length-based — 1 minute to verify visually.
2. **Contact moment:** hit lands when the spear visibly connects. Early → +0.03 contact; late → −0.03.
3. **Chain rhythm:** mash lights — should flow at ~0.6s/hit with no dead air and no clipped swings.
   Gaps → lower that slot's chainOpen by 0.05; clipping → raise it.
4. **Line identity:** stand between two dummies, thrust — only the one in FRONT should take the hit.
   Side dummy hit → lower `thrustRadius` (0.4) or raise `thrustArcDot` (0.65).
5. **Augments still work:** damage/attack-speed augments scale as before (slots are multipliers).

## 6. Editor notes / flags

- **Grip layers are sword-named** (`SwordArm`, `RightHandGrip`) and hold sword-grip poses — swap
  their clips for spear stance poses from the pack's Idle_Combat set (art task).
- The pack's **06_Dodge / 07_Roll / 08_Hit** sections can replace the Dodge/flinch clips for a
  fully spear-consistent kit (drop-in state motion swaps).

## 7. Backlog gold in this pack (future sprints)

| Clips | System it unlocks |
|---|---|
| `Run_Attack_01/02` (2.5s) | **Sprint attack** — we already have `_isSprinting`; one state + slot |
| `Parry_Counter_Attack_L/R` (3.25s) | Parry/counter on the block system |
| `Execution_01–03` (2.6–3.5s) | Kill-confirm executions on staggered elites |
| `Ultimate_Attack` Start/Loop/End (+Air) | Musou animation (currently animator-less trigger) |
| `Attack_Up_Floor_To_Air_02`, `Air_To_Air_03`, `Attack_Air_to_Floor_01–03` | Launcher → air combo → plunge system |
| `Skill_01–05`, `Buff` | Ability cast animations (slots 1–4) |
| `Combo_02/04/05` chains | 2nd weapon kit / haste-mode chain / enemy spear units |
| `Target_01–03` | Lock-on stance layer |
