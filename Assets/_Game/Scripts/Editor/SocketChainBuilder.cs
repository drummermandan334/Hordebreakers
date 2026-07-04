using UnityEditor;
using UnityEngine;

namespace Hordebreakers.EditorTools
{
    /// <summary>
    /// Builds the DUMMY socket chain that lets the spear pack's animated weapon socket play on the SYNTY rig.
    /// The pack's clips carry generic transform curves for "root/pelvis/.../hand_r/Weapon_Actor_R"; generic curves
    /// bind by PATH relative to the Animator, so an empty-GameObject chain with exactly those names receives the
    /// authored socket motion on any rig. All chain nodes sit at identity; the socket node gets the 9CG-authored
    /// rest pose (baked below); a <see cref="WeaponSocketFollower"/> on the dummy "hand_r" pins it to the real
    /// Synty hand bone each frame. Parent the spear prop under the created "Weapon_Actor_R".
    ///
    /// Usage: select the GameObject that HAS the Animator (the Synty model root), then
    /// Hordebreakers → Build Spear Socket Chain. Idempotent — re-running finds existing nodes.
    /// </summary>
    public static class SocketChainBuilder
    {
        // Exact authored path (from the pack's .anim curve bindings), relative to the Animator's GameObject.
        private static readonly string[] Path =
        {
            "root", "pelvis", "spine_01", "spine_02", "spine_03", "spine_04", "spine_05",
            "clavicle_r", "upperarm_r", "lowerarm_r", "hand_r"
        };

        // Weapon_Actor_R rest pose as authored in PreFabs/9CG_Spear.prefab (local to hand_r).
        private static readonly Vector3 SocketRestPos = new Vector3(0.08045031f, -0.034351554f, 0.010839636f);
        private static readonly Quaternion SocketRestRot = new Quaternion(0.21179876f, 0.63712806f, 0.597078f, -0.43898398f);

        [MenuItem("Hordebreakers/Build Spear Socket Chain")]
        private static void Build()
        {
            GameObject sel = Selection.activeGameObject;
            if (sel == null || sel.GetComponent<Animator>() == null)
            {
                EditorUtility.DisplayDialog("Build Spear Socket Chain",
                    "Select the GameObject that has the character's Animator (the model root) first.", "OK");
                return;
            }

            Transform parent = sel.transform;
            foreach (string name in Path)
            {
                Transform next = parent.Find(name);
                if (next == null)
                {
                    var go = new GameObject(name);
                    Undo.RegisterCreatedObjectUndo(go, "Build Spear Socket Chain");
                    next = go.transform;
                    next.SetParent(parent, false);   // identity local — only the follower + curves move this chain
                }
                parent = next;
            }

            // parent is now the dummy hand_r → follower + the socket node with its authored rest pose.
            if (parent.GetComponent<WeaponSocketFollower>() == null)
                Undo.AddComponent<WeaponSocketFollower>(parent.gameObject);

            Transform socket = parent.Find("Weapon_Actor_R");
            if (socket == null)
            {
                var go = new GameObject("Weapon_Actor_R");
                Undo.RegisterCreatedObjectUndo(go, "Build Spear Socket Chain");
                socket = go.transform;
                socket.SetParent(parent, false);
            }
            socket.localPosition = SocketRestPos;
            socket.localRotation = SocketRestRot;
            socket.localScale = Vector3.one;

            Selection.activeTransform = socket;
            Debug.Log("[SocketChainBuilder] Chain ready. Parent the spear prop under the selected 'Weapon_Actor_R', " +
                      "point WeaponVfx.bladeSocket at the spear, then calibrate the WeaponSocketFollower offsets on 'hand_r' " +
                      "until the spear lies along the palm.");
        }
    }
}
