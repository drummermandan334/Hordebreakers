using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Hordebreakers
{
    /// <summary>
    /// Directional "incoming attack" warning on the HUD. When an enemy commits a telegraphed attack at the player, a red
    /// arrow flares around the player's screen position pointing in the direction the strike comes from — and crescendos
    /// as the hit nears (the same read as the enemy's wind-up glow, but on the PLAYER, so you know to dodge even when the
    /// attacker is off-screen or behind you). The direction is computed camera-relative (not via raw WorldToScreenPoint),
    /// so a behind-the-camera attacker still reads correctly.
    ///
    /// Static facade over a single instance (mirrors <see cref="CombatVfx"/> / <see cref="CombatAudio"/> /
    /// <see cref="PlayerCameraRig"/>): enemies call <see cref="Begin"/> on telegraph start and <see cref="Cancel"/> when
    /// it's interrupted/landed; both no-op if no instance is present. SELF-BOOTSTRAPPING — it builds its own pooled arrow
    /// Images + a procedural triangle sprite under the HUD canvas in Awake, so it needs no manual Unity wiring beyond
    /// dropping this component on a GameObject under (or near) the HUDCanvas. Zero per-frame heap allocation.
    /// </summary>
    public sealed class IncomingAttackWarning : MonoBehaviour
    {
        public static IncomingAttackWarning Instance { get; private set; }

        [Header("Look")]
        [Tooltip("Arrow tint (alpha is driven by the crescendo/pulse at runtime).")]
        [SerializeField] private Color warningColor = new Color(1f, 0.16f, 0.12f, 1f);
        [Tooltip("Ring radius the arrows sit at around the player's screen position (px @1080p; scales with resolution).")]
        [SerializeField] private float radius = 150f;
        [Tooltip("Arrow size (px @1080p reference; scaled by the canvas scaler).")]
        [SerializeField] private float arrowSize = 72f;
        [Tooltip("Throb speed (pulses/sec) — matches the enemy telegraph feel.")]
        [SerializeField] private float pulseHz = 5f;
        [Tooltip("Max simultaneous warnings shown (arrow pool size). The attack-budget caps committers well below this.")]
        [SerializeField] private int maxWarnings = 8;
        [Tooltip("Height (m) above the player's feet used as the on-screen anchor point (~chest).")]
        [SerializeField] private float anchorHeight = 1f;
        [Tooltip("Fallback anchor Y (fraction of screen height) when the player can't be projected.")]
        [SerializeField] private float fallbackAnchorY = 0.42f;

        private struct Warning { public Transform Attacker; public float Timer; public float Duration; }

        private List<Warning> _warnings = new List<Warning>(8);   // non-readonly: a domain reload mid-play can null it (re-created in LateUpdate)
        private Image[] _arrows;
        private RectTransform _container;
        private Sprite _arrowSprite;
        private Camera _cam;
        private Transform _player;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            Build();
            _cam = Camera.main;   // cache once (persistent Cinemachine brain camera); re-fetched lazily only if it goes null
        }

        private void OnDestroy()
        {
            if (Instance != this) return;   // a rejected duplicate built nothing — leave the real instance's resources alone
            Instance = null;
            if (_container != null) Destroy(_container.gameObject);   // the container lives under the Canvas, not this GO
            if (_arrowSprite != null)
            {
                Texture2D tex = _arrowSprite.texture;   // capture before destroying the sprite
                Destroy(_arrowSprite);
                if (tex != null) Destroy(tex);           // free the procedurally-baked texture (not GC-managed)
            }
        }

        // ---------------- static facade ----------------

        /// <summary>Raise (or refresh) a directional warning for <paramref name="attacker"/> lasting its wind-up.</summary>
        public static void Begin(Transform attacker, float duration)
        {
            if (Instance == null || attacker == null) return;
            Instance.BeginInternal(attacker, duration);
        }

        /// <summary>Clear the warning for <paramref name="attacker"/> (tell interrupted, strike landed, or it died).</summary>
        public static void Cancel(Transform attacker)
        {
            if (Instance == null || attacker == null) return;
            Instance.CancelInternal(attacker);
        }

        private void BeginInternal(Transform attacker, float duration)
        {
            duration = Mathf.Max(0.05f, duration);
            for (int i = 0; i < _warnings.Count; i++)
            {
                if (_warnings[i].Attacker == attacker)
                {
                    Warning refreshed = _warnings[i];
                    refreshed.Timer = duration;
                    refreshed.Duration = duration;
                    _warnings[i] = refreshed;
                    return;
                }
            }
            if (_warnings.Count >= maxWarnings) return;   // pool full (extremely unlikely given the attack budget)
            _warnings.Add(new Warning { Attacker = attacker, Timer = duration, Duration = duration });
        }

        private void CancelInternal(Transform attacker)
        {
            for (int i = 0; i < _warnings.Count; i++)
            {
                if (_warnings[i].Attacker == attacker)
                {
                    _warnings[i] = _warnings[_warnings.Count - 1];   // swap-remove
                    _warnings.RemoveAt(_warnings.Count - 1);
                    return;
                }
            }
        }

        // ---------------- per-frame placement ----------------

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (_warnings == null) _warnings = new List<Warning>(8);   // survive a mid-play domain reload (non-serializable list)

            // Tick + cull expired / destroyed / pooled-away attackers.
            for (int i = _warnings.Count - 1; i >= 0; i--)
            {
                Warning w = _warnings[i];
                w.Timer -= dt;
                if (w.Timer <= 0f || w.Attacker == null || !w.Attacker.gameObject.activeInHierarchy)
                {
                    _warnings[i] = _warnings[_warnings.Count - 1];
                    _warnings.RemoveAt(_warnings.Count - 1);
                    continue;
                }
                _warnings[i] = w;
            }

            if (_arrows == null) return;
            if (_warnings.Count == 0)
            {
                for (int i = 0; i < _arrows.Length; i++)
                {
                    if (_arrows[i].gameObject.activeSelf) _arrows[i].gameObject.SetActive(false);
                }
                return;   // nothing incoming → skip the per-frame camera/player lookups entirely
            }
            if (_cam == null) _cam = Camera.main;
            if (_player == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) _player = p.transform;
            }

            // Anchor the ring on the player's screen position (fallback to lower-centre), and capture the camera's
            // flattened forward/right so we can map a world bearing onto a screen bearing (robust behind the camera).
            Vector2 anchor = new Vector2(Screen.width * 0.5f, Screen.height * fallbackAnchorY);
            Vector3 camFwd = Vector3.forward;
            Vector3 camRight = Vector3.right;
            if (_cam != null)
            {
                if (_player != null)
                {
                    Vector3 sp = _cam.WorldToScreenPoint(_player.position + Vector3.up * anchorHeight);
                    if (sp.z > 0f) anchor = new Vector2(sp.x, sp.y);
                }
                camFwd = _cam.transform.forward; camFwd.y = 0f;
                camFwd = camFwd.sqrMagnitude > 1e-4f ? camFwd.normalized : Vector3.forward;
                camRight = _cam.transform.right; camRight.y = 0f;
                camRight = camRight.sqrMagnitude > 1e-4f ? camRight.normalized : Vector3.right;
            }

            Vector3 playerPos = _player != null ? _player.position : Vector3.zero;
            float ringPx = radius * (Screen.height / 1080f);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseHz * (Mathf.PI * 2f));

            int shown = 0;
            for (int i = 0; i < _warnings.Count && shown < _arrows.Length; i++)
            {
                Warning w = _warnings[i];
                Vector3 to = w.Attacker.position - playerPos; to.y = 0f;
                if (to.sqrMagnitude < 1e-4f) continue;

                float sx = Vector3.Dot(to, camRight);          // screen-right component
                float sy = Vector3.Dot(to, camFwd);            // screen-up (into the screen) component
                float ang = Mathf.Atan2(sx, sy);               // 0 = straight ahead (screen up), clockwise +
                float dirX = Mathf.Sin(ang);
                float dirY = Mathf.Cos(ang);

                Image arrow = _arrows[shown];
                RectTransform rt = arrow.rectTransform;
                rt.position = new Vector3(anchor.x + dirX * ringPx, anchor.y + dirY * ringPx, 0f);
                rt.localRotation = Quaternion.Euler(0f, 0f, -ang * Mathf.Rad2Deg);   // point the up-arrow outward

                float crescendo = 1f - Mathf.Clamp01(w.Timer / Mathf.Max(0.0001f, w.Duration));
                float k = (0.35f + 0.65f * crescendo) * (0.6f + 0.4f * pulse);
                Color c = warningColor;
                c.a = warningColor.a * Mathf.Clamp01(k);
                arrow.color = c;
                if (!arrow.gameObject.activeSelf) arrow.gameObject.SetActive(true);
                shown++;
            }
            for (int i = shown; i < _arrows.Length; i++)
            {
                if (_arrows[i].gameObject.activeSelf) _arrows[i].gameObject.SetActive(false);
            }
        }

        // ---------------- self-bootstrap (build the arrow pool + sprite) ----------------

        private void Build()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                GameObject hud = GameObject.Find("HUDCanvas");
                if (hud != null) canvas = hud.GetComponent<Canvas>();
                if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
            }
            Transform parent = canvas != null ? canvas.transform : transform;

            _arrowSprite = BuildArrowSprite();

            GameObject containerGo = new GameObject("IncomingAttackArrows", typeof(RectTransform), typeof(Canvas));
            _container = containerGo.GetComponent<RectTransform>();
            _container.SetParent(parent, false);
            _container.anchorMin = _container.anchorMax = new Vector2(0f, 0f);
            _container.pivot = new Vector2(0.5f, 0.5f);
            _container.anchoredPosition = Vector2.zero;
            _container.localScale = Vector3.one;
            // Own nested sub-canvas so moving/recoloring arrows every frame doesn't re-batch the whole HUD mesh
            // (perf rule: split canvases by update frequency). overrideSorting keeps it drawn above the HUD.
            Canvas sub = containerGo.GetComponent<Canvas>();
            sub.overrideSorting = true;
            sub.sortingOrder = (canvas != null ? canvas.sortingOrder : 0) + 50;

            int n = Mathf.Max(1, maxWarnings);
            _arrows = new Image[n];
            for (int i = 0; i < n; i++)
            {
                GameObject go = new GameObject("Arrow" + i, typeof(RectTransform), typeof(Image));
                RectTransform rt = go.GetComponent<RectTransform>();
                rt.SetParent(_container, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(arrowSize, arrowSize);
                Image img = go.GetComponent<Image>();
                img.sprite = _arrowSprite;
                img.raycastTarget = false;
                img.color = warningColor;
                go.SetActive(false);
                _arrows[i] = img;
            }
        }

        /// <summary>Procedurally bake a white, soft-edged filled triangle (apex up) so no sprite asset is needed.</summary>
        private static Sprite BuildArrowSprite()
        {
            const int S = 64;
            Texture2D tex = new Texture2D(S, S, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            Color32[] px = new Color32[S * S];
            for (int y = 0; y < S; y++)
            {
                float v = y / (float)(S - 1);              // 0 bottom .. 1 top
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)(S - 1);          // 0 left .. 1 right
                    byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(TriangleAlpha(u, v)) * 255f);
                    px[y * S + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, S, S), new Vector2(0.5f, 0.5f), 100f);
        }

        private static float TriangleAlpha(float u, float v)
        {
            const float baseY = 0.16f, apexY = 0.94f, halfBase = 0.44f, feather = 0.035f;
            if (v < baseY - feather || v > apexY) return 0f;
            float t = Mathf.InverseLerp(baseY, apexY, v);          // 0 base .. 1 apex
            float halfW = halfBase * (1f - t);                     // shrinks to a point at the apex
            float d = Mathf.Abs(u - 0.5f);
            float side = Mathf.Clamp01((halfW - d) / feather + 0.5f);
            float bottom = Mathf.Clamp01((v - baseY) / feather + 0.5f);
            return side * bottom;
        }
    }
}
