# Player Character Classes — plan & asset map (task 4)

Researched overnight. **Key finding:** the **Sidekick Characters** pack installed only contains parts for the
**Knight** fantasy class (`FANT_KNGT`, 144 parts) plus a base human and a sci-fi civilian — there are **no
Barbarian / Mage / Wizard / Rogue / Archer Sidekick parts**. So "use the Sidekick tool" can only produce the
Knight today; the other classes need a different source. Good news: we already own pre-made, **Humanoid-rigged**
characters (verified: SkinnedMesh + Humanoid avatar + Hand_R) that can use our Sword Combat anims, combo tree,
and prop-bone weapon setup directly.

## Recommended source per class (what we HAVE vs. ACQUIRE)
| Class | Recommended character (owned) | Weapon (owned) | Status |
|---|---|---|---|
| **Battle Sorcerer** (current) | `Chr_Hero_Female_01` (DungeonRealms) | `SM_Wep_Sword_01` (prop bone ✓) | DONE — playable |
| **Knight** | Sidekick `FANT_KNGT` build, **or** `SM_Chr_AncientWarrior_01` (Rivals) | `SM_Wep_Sword_01` + `SM_Wep_Shield_Heater_01` | Ready to build |
| **Barbarian** | `SM_Chr_BR_BarbarianGiant_01` (Rivals) | 2H: `SM_Wep_Hammer_Large_Metal_01` or `SM_Wep_Ornate_GreatAxe_01` | Ready (needs 2H anims) |
| **Rogue** | `SM_Chr_Male_Rouge_01` (FantasyCharacters) | dual `SM_Wep_Knife_Small_01` | Ready (needs dual-wield anims) |
| **Mage** | `SM_Chr_Male_Sorcerer_01` or `SM_Chr_Female_Witch_01` | `SM_Prop_Sceptre_01` / `SM_Prop_SpellBook_01` | Ready (needs cast anims + spell system) |
| **Wizard** | `SM_Chr_Male_Wizard_01` (FantasyCharacters) | `SM_Prop_Druid_Staff_01` / staff | Ready (needs cast anims + spell system) |
| **Archer / Ranger** | **none owned** — closest reskin = Druid/Peasant | bow: **none owned** | **ACQUIRE** |

Bonus for the GDD's elite/boss work: `PolygonFantasyRivals` has 20+ monster characters (Troll, Ork, Golem,
Demon, Dwarf, PigButcher, Slayer, Medusa, ForestGuardian…) — ideal for the first telegraphed **elite** and the
**boss** (all Humanoid + Hand_R, so they take the enemy attack anims too).

## To ACQUIRE outside current Synty assets
1. **Archer character + a bow mesh + bow/draw/loose animations.** Nothing owned covers ranged-bow. (Synty has a
   ranged/archery anim pack; or repurpose the dagger-throw `ThrowWeapon` as a stopgap "ranged" class.)
2. **Caster (staff/wand) cast animations** for Mage/Wizard. Sword Combat is melee-only. Need a magic/caster anim
   set, plus a **spell/projectile system** (we have pooled `Projectile` + `AutoWeapon`/`ThrowWeapon` to build on).
3. **Two-handed** (Barbarian) and **dual-wield** (Rogue) attack anim sets. Sword Combat is one-handed only — using
   it as a placeholder works but the weapon won't match. Synty sells matching anim packs.
4. **Sidekick fantasy class part packs** (Barbarian/Mage/Rogue/etc.) IF you want to build them with the Sidekick
   tool instead of the pre-made characters.
5. **Shield + block:** we HAVE shields and Sword Combat HAS blocking/parry clips — Knight sword+shield is doable
   with owned assets (shield on `Hand_L`/`Prop_L`).

## Integration recipe (turn any owned Humanoid character into a playable class)
The combo system is **character-agnostic** — it only needs the right animator + a prop-bone weapon. Per class:
1. Swap the player's **Model** child for the class character prefab (keep the `Player` root, CharacterController,
   `PlayerController`, `AutoWeapon`, `ThrowWeapon`).
2. Assign **`PlayerAnimator`** (the combo/jump controller) to the new Model's Animator (Humanoid avatar retargets
   the Sword Combat clips automatically).
3. Run the **Prop Bone Binder** on the Model (`Synty/Tools/Animation/Setup Prop Bones`), build a fresh weapon mesh
   on `Prop_R_Socket` (see `weapon-attach-socket` notes / how `Sword_Prop` was made), add **`SwordPropRest`** +
   the `RightHandGrip`/`SwordArm` grip layers (copy from the hero).
4. Give the class its weapon; tune `PlayerCombatData` (reach/damage/speed) per class feel.
5. For **melee** classes (Knight/Barbarian/Rogue) the existing L/H combo tree works now. For **caster/ranged**
   classes, the "attack" should fire spells/arrows (build on `Projectile`) rather than the melee combo.

## Suggested build order (when you're ready)
Knight → Barbarian → Rogue (all melee, reuse the combo tree) first; then Mage/Wizard (need cast anims + spell
system); Archer last (needs the most acquisition). I did **not** auto-build these tonight — each needs weapon
placement + per-class tuning + (for casters/archer) systems we don't have yet, so it's better done with you
awake than half-built blind. Say the word and I'll wire up whichever you want first.
