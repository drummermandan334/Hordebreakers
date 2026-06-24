# HORDEBREAKERS — Prototype Setup

The single-player core prototype from GDD §11, step 1. Goal: **answer the fun hypothesis** — does melee + mark/detonate amid a horde feel good? Everything here is built for fast iteration, not for scale.

> **Architecture note:** This is plain **MonoBehaviour + object pooling**, *not* DOTS/ECS. That's deliberate — ECS authoring overhead would slow the fun-test loop, and pooled MonoBehaviours handle a few hundred enemies at 60fps fine for a prototype. Once the loop is proven fun and you need thousands of enemies, the *enemy horde* migrates to DOTS/ECS (players/camera/UI stay GameObject-based, per GDD §9). The data lives in ScriptableObjects, so tuning carries over.

---

## 1. Requirements

- **Unity 6 LTS**, URP recommended (Synty packs are URP-friendly and low-poly = cheap hordes).
- **Active Input Handling:** the scripts use the legacy `Input` API. Go to **Project Settings → Player → Active Input Handling** and set it to **Both** (or *Input Manager (Old)*). If you see `InvalidOperationException: ... Input System package`, this is why.
- Drop the `Assets/_Game/Scripts` folder from this package into your project's `Assets/`.

## 2. The scripts

| Script | Folder | Role |
|---|---|---|
| `IDamageable` | Core | Interface for anything that takes damage |
| `ObjectPool<T>` | Core | Runtime pool (no per-frame allocation) |
| `ThirdPersonCamera` | Camera | Fixed elevated 3/4 follow cam (tune pitch/distance) |
| `PlayerController` | Player | Camera-relative movement, dodge + i-frames, light combo, **heavy = detonator** |
| `AutoWeapon` | Combat | VS-style auto-fire at nearest enemy; applies **Marks** |
| `Projectile` | Combat | Pooled shot; SphereCast hit → mark + damage |
| `Markable` | Combat | Stacking marks + decay; optional emissive glow |
| `Enemy` | Enemies | Pooled chaff: seek player, contact damage, die→pool |
| `Spawner` | Enemies | Pools + ring-spawns enemies, ramps rate over time |
| `PlayerCombatData` / `WeaponData` / `EnemyData` | Data | ScriptableObject tuning (mirror the .xlsx) |

**The synergy loop, in code:** `AutoWeapon` → `Projectile` hit → `Markable.AddMark()` stacks on enemies. Your heavy attack (`PlayerController.ResolveHeavyHit`) calls `Detonate()`, which consumes each nearby enemy's marks and deals `flat + stacks × perStack`. More autos → more marks → bigger detonations.

## 3. Project setup (one time)

1. **Layer:** add a layer named **`Enemy`** (Project Settings → Tags and Layers).
2. **Tag:** the **`Player`** tag already exists in Unity — you'll assign it to the player object.

## 4. Create the data assets

Right-click in `Assets/_Game/Data` → **Create → Hordebreakers →** and make:
- **Player Combat Data** (defaults are good to start)
- **Weapon Data** → name it `ThrowingKnives`
- **Enemy Data** → name it `Husk`

Defaults already match the tuning spreadsheet. Tune there, then copy values across (or just edit the assets directly while prototyping).

## 5. Build the prefabs

**Projectile** (`Assets/_Game/Prefabs`)
- Small sphere, scale ~0.3. Add **`Projectile`**. *No collider, no Rigidbody* (it SphereCasts).
- Assign this prefab to `ThrowingKnives → projectilePrefab`.

**Player**
- Root empty (or a Synty character root). Add **`CharacterController`** sized to the body (radius ~0.4, height ~1.8, center y ~0.9).
- Make the Synty character mesh a **child** of the root.
- Add **`PlayerController`**: assign `PlayerCombatData`, set **enemyMask → Enemy**, set `modelRoot` to the mesh child.
- Add **`AutoWeapon`**: assign `ThrowingKnives`, set **enemyMask → Enemy**.
- Set the root GameObject's **tag → Player**.

**Husk enemy**
- Root on the **Enemy layer**. Add a **`CapsuleCollider`** sized to the body.
- Synty zombie/skeleton mesh as a **child**.
- Add **`Markable`**: (optional) drag the body `Renderer` into `markRenderer` for the glow — needs an emission-capable material (URP/Lit with Emission enabled).
- Add **`Enemy`**: set `modelRoot` to the mesh child. (It auto-uses the `Husk` data via the Spawner.)
- Save as a prefab.

## 6. Build the scene

1. **Ground:** a 30×30 plane at origin.
2. **Light:** a Directional Light.
3. **Player:** drag the Player prefab in at origin.
4. **Camera:** on Main Camera add **`ThirdPersonCamera`** (leave `target` empty — it finds the Player tag, or assign it).
5. **Spawner:** empty GameObject + **`Spawner`** → assign `enemyPrefab` (Husk prefab), `enemyData` (Husk data); `player` auto-finds by tag.

Press Play.

## 7. Controls

| Input | Action |
|---|---|
| **WASD** | Move (camera-relative) |
| **LMB** | Light attack (fast combo) |
| **RMB** | Heavy attack — **detonates marks** |
| **Space** | Dodge (i-frames at the start) |

## 8. What you're testing (the fun hypothesis)

Wade into the swarm. The autos should be peppering enemies with marks (the glow ramps up if you wired the emissive renderer). Time a heavy attack into a marked cluster — it should *pop*. Does that loop feel satisfying? Does dodging an incoming pile feel good? **That's the whole question.** If yes, proceed to the vertical slice. If no, tune before building anything else.

Fast tuning levers (edit the data assets live in Play mode):
- Detonation flat/per-stack and radius → how punchy the payoff is.
- `AutoWeapon` fire interval + marks/hit → how fast marks build.
- Spawner `startInterval` / `minInterval` / `maxAlive` → horde pressure.
- Heavy windup → risk/reward of committing to a detonate.

## 9. Known prototype simplifications (intentional)

- **No enemy avoidance/NavMesh** — they pile up and overlap (VS-like). Flow-field/avoidance is a later concern.
- **Death = disable** — reload the scene to retry. No game-over UI yet.
- **No waves/boss director yet** — the Spawner just ramps density. The full wave/breather/boss loop (GDD §6) comes in the vertical slice.
- **Mark glow** needs an emissive material; it's optional and null-safe.
- **One enemy tier, one weapon** — elites, bosses, more weapons, and the level-up card system are the next layers.

## 10. After the fun is proven

Vertical slice → co-op networking spike (listen server + Steam relay) → migrate the horde to DOTS/ECS for scale. See `UNITY_MCP_SETUP.md` to have Claude Code rig the prefabs and blockout arenas for you instead of doing §5–6 by hand.
