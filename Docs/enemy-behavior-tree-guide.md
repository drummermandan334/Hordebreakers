# Enemy Behavior Tree — authoring guide

The enemy-AI foundation is now on **Unity Behavior** (`com.unity.behavior` 1.0.16). The code half is done; the one
thing that can only be done in the editor — **wiring the graph** — is your part. It's a single small graph that drives
**every** archetype.

## How it's split
- **Logic → one Behavior graph** (this guide). Approach → telegraph → strike → recover, with the abort flow.
- **Numbers → EnemyData/EliteData** (the SO on each enemy). HP, speed, ranges, telegraph timings, damage, cooldowns,
  `turnSpeedDeg`. The nodes read these off the enemy automatically.
- **Archetype differences** (rusher rings & lunges, charger rushes & charges, brute walks & slams) live in the C# —
  each enemy implements the same `IEnemyBody` verbs differently. So **the same graph + the same nodes** work for all
  three; you author it **once**.
- **Race variants later** = a different EnemyData asset on the same archetype = no graph work. A genuine behavioral
  quirk = a small extra branch in the graph (rare).

There are **no blackboard variables to set up** — every node finds the enemy on its own GameObject. The graph is purely
structural.

## Part A — author the graph (you, in the Behavior editor)

1. **Create the asset:** Project window → right-click → **Create ▸ Behavior ▸ Behavior Graph**. Name it
   **`EnemyArchetypeGraph`**. Suggested location: `Assets/_Game/AI/`. Double-click to open the Behavior editor.

2. **Build this tree.** All custom nodes are in the Add-node search under **Action/Hordebreakers** and
   **Conditions/Hordebreakers**; the composites (Repeat, Selector, Sequence) are the built-ins.

   ```
   On Start
   └─ Repeat            (run forever — the AI loops)
      └─ Selector       (try each branch top→bottom; first that runs/succeeds wins)
         ├─ Sequence              ← ATTACK (only when in range AND ready)
         │   ├─ In Attack Range   (Condition)
         │   ├─ Off Cooldown      (Condition)
         │   ├─ Telegraph         (Action)
         │   ├─ Commit Attack     (Action)
         │   └─ Recover           (Action)
         ├─ Sequence              ← HOLD (in range but cooling down)
         │   ├─ In Attack Range   (Condition)
         │   └─ Reposition        (Action)
         └─ Approach              (Action)   ← default: close the distance
   ```

   Order matters — keep the three Selector branches **top to bottom** as shown (attack, then hold, then approach).

3. **No blackboard variables.** Leave the Blackboard empty. Save the graph (Ctrl+S).

That's it for authoring. The same `EnemyArchetypeGraph` will be assigned to Husk, Charger, and Brute.

## Part B — wiring + flipping the brain (me, via MCP, once the graph exists)
Tell me the graph is saved and I'll: add a **Behavior Agent** component to the Husk / Charger / Brute / Commander
prefabs, assign `EnemyArchetypeGraph` to each, and flip each enemy's **Brain** from `FSM` to `BehaviorTree`. (Until
then everything runs on the existing FSM, so the game stays playable.)

## How the pieces map (for reference)
- **Approach** → `IEnemyBody.ApproachStep` — rusher closes to its standoff ring, charger rushes in, brute walks in,
  dummy idles. Succeeds when `InAttackRange`.
- **Reposition** → `RepositionStep` — rusher strafes the ring; charger/brute keep closing. Succeeds (yields) the moment
  it's `OffCooldown` so the attack branch can fire.
- **Telegraph** → the wind-up tell (anim + glow). **Aborts** (fails the attack branch → falls back to Reposition/
  Approach) if the player dodges out of the commit window, or if we get hit. Brutes commit (hyperarmor — never abort).
- **Commit Attack** → the strike: rusher lunge, charger charge, brute AoE slam. Applies the hit. A hit interrupts the
  rusher/charger here (brute has poise).
- **Recover** → the recovery window, then the attack cooldown re-arms.

## Verify (together, in Play)
With the brains flipped, each archetype should: approach + hold a **real standoff ring** (no face-hugging) → telegraph →
strike → **abort the wind-up if you dodge clear** → recover → die → return to pool and behave fresh on reuse. Lock-on,
hit-feel (flash/shake/blood/SFX/telegraph glow), the spawner, and kill/XP-gem must all still work.

## After parity is confirmed (the fast-follow)
The interlinked feel fixes from the AI audit land next, separately: a **cap on simultaneous attackers** (the rest orbit
instead of all piling on), a **bigger standoff ring** (`attackRange`) with the **lunge reaching across it**, and
charger speed tuning. The turn-rate fix is already in (creatures pivot now, so you can flank them).
