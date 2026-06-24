# Connecting Claude Code to Unity (MCP) — Rig & Blockout Workflow

This wires **Claude Code → your running Unity Editor** so Code can create the ScriptableObjects, rig the player/enemy prefabs, wire the scene, and blockout arena 1 — instead of you doing §5–6 of the prototype setup by hand.

> **What "rig" means here:** Synty Polygon characters ship **pre-rigged** to a standard humanoid (Mecanim) skeleton, so nobody redoes bones or skin weights. "Rigging" below = the *gameplay* assembly: prefab + components + animator + references. That's squarely what MCP can do.

> **First:** drop `Assets/_Game/Scripts` into your project so Code (and Unity) can see the script and ScriptableObject types it'll reference by name.

---

## 1. Install a Unity MCP server

Pick one. Either exposes the Editor to Claude Code over MCP. (Pin exact versions when you commit — these move fast.)

**Option A — CoplayDev "MCP for Unity"** (free, MIT, mature; recommended to start)
1. In Unity: **Window → Package Manager → + → Add package from git URL**, paste:
   `https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main`
2. **Window → MCP for Unity → Configure All Detected Clients** (this auto-wires Claude Code).
3. In a terminal at your project folder, start `claude` and confirm the Unity tools are listed.

**Option B — Unity's official MCP server** (first-party, but **beta**; needs a Unity subscription, doesn't burn AI credits)
1. **Edit → Project Settings → AI → Unity MCP**; confirm **Unity Bridge** shows *Running* (green).
2. Expand **Integrations**, pick your client (Claude Code), and select **Configure**.
3. Treat it as exploratory for now — behavior changes between updates.

**Verify:** in Claude Code, ask *"What objects are in the current scene?"* If MCP is live, it'll query the hierarchy and answer.

## 2. Gotchas (save yourself the headaches)

- **Stop Play mode** before any scene/prefab edits — many operations no-op or fail in Play mode.
- **Reference by listing first.** An op that names a GameObject that doesn't exist will fail — tell Code to *list the hierarchy/assets* before editing.
- **Commit to git first.** MCP has full Editor access (create/modify/delete anything). A clean commit = easy undo.
- **Localhost only.** The bridge binds to 127.0.0.1; it's not reachable from other machines.
- **Batch big jobs.** Ask Code to batch multi-object operations — it's far faster than one call per object.
- **It can't judge feel.** Code can build and even enter Play mode to check for exceptions, but "is it fun" is yours.

## 3. The prompts (paste into Claude Code, in order)

Replace `[Synty character]`, `[Synty zombie]`, and `[pack path]` with your actual asset names/paths.

**1) Project setup**
> Create an `Enemy` layer if it doesn't exist. Confirm a `Player` tag exists.

**2) Data assets**
> In `Assets/_Game/Data`, create these ScriptableObjects via the Hordebreakers create menu, leaving default values: a `PlayerCombatData`, a `WeaponData` named `ThrowingKnives`, and an `EnemyData` named `Husk`.

**3) Projectile prefab**
> Create a `Projectile` prefab in `Assets/_Game/Prefabs`: a sphere scaled to 0.3 with the `Projectile` component, **no collider and no Rigidbody**. Then assign it to `ThrowingKnives.projectilePrefab`.

**4) Player prefab**
> Build a Player prefab from `[Synty character]` in `Assets/_Game/Prefabs`. Add a `CharacterController` sized to the body (radius ~0.4, height ~1.8, center y ~0.9). Add `PlayerController` and assign the `PlayerCombatData` asset, set its `enemyMask` to the `Enemy` layer, and set `modelRoot` to the character mesh child. Add `AutoWeapon`, assign `ThrowingKnives`, and set its `enemyMask` to `Enemy`. Set the root GameObject's tag to `Player`. List the resulting component setup so I can confirm.

**5) Enemy prefab**
> Build a `Husk` enemy prefab from `[Synty zombie]` in `Assets/_Game/Prefabs`. Put the root on the `Enemy` layer and add a `CapsuleCollider` sized to the body. Add `Markable` and assign the body Renderer to its `markRenderer`. Add `Enemy` and set `modelRoot` to the mesh child. Save the prefab.

**6) Scene wiring**
> Create a new scene `Arena01`. Add a 30×30 ground plane and a directional light. Place the Player prefab at the origin. On the Main Camera add `ThirdPersonCamera`. Add an empty `Spawner` GameObject with the `Spawner` component, assigning `enemyPrefab` = Husk prefab and `enemyData` = the Husk data asset.

**7) Blockout the arena**
> Blockout a bounded arena roughly 40×40 using the modular floor and wall pieces from `[pack path]`, enclosed so enemies funnel inward, with a few cover props for positioning. Keep it greybox — no fine art pass. When done, capture a Scene-view screenshot so I can review the layout.

**8) Iterate / verify**
> Read the Unity console and fix any compile or null-reference errors. Enter Play mode, confirm there are no exceptions for ~10 seconds, then stop.

## 4. The loop from here

Run it (`PROTOTYPE_SETUP.md` §8), feel it, then feed Code tuning changes ("make detonations punchier — bump per-stack to 9 and radius to 5") or layout changes ("widen the arena, add two chokepoints"). Code edits the assets/scene; you keep playtesting. That's the fast iteration loop — Code+MCP on assembly and tuning, you on feel.
