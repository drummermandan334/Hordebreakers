# CLAUDE.md — Hordebreakers

Persistent context for Claude Code. Kept deliberately short — the full design lives in the docs linked below. Every line here is a rule that should change how you act; if something isn't, it gets cut.

## What this is
Hordebreakers is a **1–4 player online co-op 3D action game** — a beat-em-up crossed with a Vampire-Survivors-like. The player actively brawls while auto-weapons clear the swarm. Built for friends first; Steam-ready if it's fun.

- **Design source of truth → `Docs/HORDEBREAKERS_GDD.md`.** Read it before any design-affecting change. If a request conflicts with the GDD, flag it rather than silently diverging.
- **Tuning numbers → `Docs/HORDEBREAKERS_Tuning.xlsx`** (mirrored by the ScriptableObject assets in `Assets/_Game/Data/`).

## Stack
- **Unity 6.3 LTS** (`6000.3.x`), **URP** (Universal 3D). Do **not** change the Unity version or render pipeline without asking.
- C#; namespace `Hordebreakers` for all game code.
- Art: **Synty Polygon** (low-poly). Characters ship pre-rigged (humanoid / Mecanim).
- Planned, not yet in the project: the enemy horde moves to **DOTS/ECS** for scale; multiplayer is a **listen server (one player hosts) over Steam relay** using **Netcode for GameObjects**.

## Where things live
- `Assets/_Game/` — our code, scenes, prefabs, data (scripts under `Assets/_Game/Scripts/`).
- `Assets/Synty.../` (or wherever the pack imports) — third-party packs; leave them in their default folder so updates re-import cleanly.
- `Docs/` — GDD, tuning sheet, setup guides.
- Do not put art in `Resources/` (it's force-included in every build).

## Code conventions
- One MonoBehaviour per file; the filename matches the class.
- **Game values live in ScriptableObjects, never hardcoded.**
- Performance target is **60 FPS**. Always:
  - Cache component references in `Awake`/`Start` — no `GetComponent` in `Update`.
  - No per-frame allocations; use non-alloc physics queries (`Physics.OverlapSphereNonAlloc`, etc.).
  - **Object-pool** anything spawned in bulk (enemies, projectiles) — never `Instantiate`/`Destroy` in loops.
  - Use `CompareTag` over string compares, `FixedUpdate` for physics, and delta time for movement.

## Architecture rule (important)
The current prototype is **plain MonoBehaviour + object pooling — NOT ECS**, on purpose (fast iteration to prove the fun). The **enemy horde** migrates to DOTS/ECS *later*, once the loop is proven and counts must scale; players, camera, and UI stay GameObject-based. **Do not prematurely convert the prototype to ECS.**

## Current phase
Build-order step 1: the **single-player core prototype**. The one thing that must feel good is the **mark → detonate loop** amid a horde (auto-weapons stack Marks; the heavy attack detonates them for an AoE burst). Judge changes against that feel. Next phase is the vertical slice: level-up cards, a first telegraphed elite, and the wave → breather → boss director (GDD §6).

## Working in Unity via MCP
- Stop **Play mode** before scene or prefab edits.
- List the scene hierarchy / assets before referencing objects by name.
- Do not add new packages or third-party assets without asking first.
