using UnityEditor;
using UnityEngine;

namespace Hordebreakers.EditorTools
{
    /// <summary>
    /// Batch-fix orientation/root-motion settings on animation clips. The spear pack's attack .anim clips ship with
    /// "Bake Into Pose" ON for rotation AND XZ position — baked rotation twists the model off gameplay-forward, and
    /// baked XZ leaves zero root deltas for the root-motion attack system.
    ///
    /// Select the .anim clip assets in the Project window and apply:
    /// - FIX ORIENTATION: Bake-Off (rotation) + Body Orientation → root turns become root-motion deltas, which the
    ///   controller discards — the body holds gameplay-forward through the swing.
    /// - FIX POSITION XZ: Bake-Off (XZ) + Body/CoM basis → the clip's authored travel becomes root-motion deltas,
    ///   which RootMotionRelay feeds to the CharacterController on useRootMotion slots.
    /// - Offset: per-clip Y correction (deg) for residual lean.
    /// - REVERT: the pack's shipped settings, for A/B.
    /// Works via AnimationUtility.Get/SetAnimationClipSettings — same fields as the FBX importer's clip panel.
    /// </summary>
    public sealed class SpearClipSettingsTool : EditorWindow
    {
        private float _offsetY;

        [MenuItem("Hordebreakers/Spear Clip Settings Tool")]
        private static void Open() => GetWindow<SpearClipSettingsTool>("Spear Clips");

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Select .anim clip assets in the Project window, then apply.\n" +
                "ROOT-MOTION ATTACK CLIPS need BOTH fixes: Fix Orientation + Fix Position XZ.\n" +
                "If a clip still leans afterwards, set Offset Y and Apply Offset to it.",
                MessageType.Info);

            int n = CountSelectedClips();
            EditorGUILayout.LabelField($"Selected clips: {n}");
            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(n == 0))
            {
                if (GUILayout.Button("FIX ORIENTATION  (rotation Bake-Off + Body Orientation, Offset 0)"))
                    Apply(s => { s.loopBlendOrientation = false; s.keepOriginalOrientation = false; s.orientationOffsetY = 0f; });

                if (GUILayout.Button("FIX POSITION XZ  (XZ Bake-Off → real root-motion travel)"))
                    Apply(s => { s.loopBlendPositionXZ = false; s.keepOriginalPositionXZ = false; });

                EditorGUILayout.Space();
                _offsetY = EditorGUILayout.Slider("Offset Y (deg)", _offsetY, -45f, 45f);
                if (GUILayout.Button("Apply Offset only (keep current bake/basis)"))
                {
                    float o = _offsetY;
                    Apply(s => s.orientationOffsetY = o);
                }

                EditorGUILayout.Space();
                if (GUILayout.Button("REVERT selected  (pack defaults: rotation+XZ baked, Original basis)"))
                    Apply(s =>
                    {
                        s.loopBlendOrientation = true; s.keepOriginalOrientation = true; s.orientationOffsetY = 0f;
                        s.loopBlendPositionXZ = true; s.keepOriginalPositionXZ = true;
                    });
            }
        }

        private static int CountSelectedClips()
        {
            int n = 0;
            foreach (Object o in Selection.objects) if (o is AnimationClip) n++;
            return n;
        }

        private static void Apply(System.Action<AnimationClipSettings> mutate)
        {
            foreach (Object o in Selection.objects)
            {
                if (o is not AnimationClip clip) continue;
                AnimationClipSettings s = AnimationUtility.GetAnimationClipSettings(clip);
                mutate(s);
                AnimationUtility.SetAnimationClipSettings(clip, s);
                EditorUtility.SetDirty(clip);
                Debug.Log($"[SpearClipSettingsTool] {clip.name}: rotBake={s.loopBlendOrientation} rotOriginal={s.keepOriginalOrientation} " +
                          $"offsetY={s.orientationOffsetY:0.#} xzBake={s.loopBlendPositionXZ} xzOriginal={s.keepOriginalPositionXZ}");
            }
            AssetDatabase.SaveAssets();
        }
    }
}
