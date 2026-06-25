using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Hordebreakers
{
    /// <summary>
    /// At a breather (wave/arena cleared), resolves any banked level-ups by presenting, one at a time, 3
    /// augment choices drawn from the pool: class-gated to the player's character, filtered to what's
    /// offerable given the current build, and rolled weighted by rarity. The run is paused by the
    /// GameManager while picks resolve; this view just draws cards and applies the chosen augment.
    /// </summary>
    public class UpgradeCardUI : MonoBehaviour
    {
        [SerializeField] private List<Augment> pool = new List<Augment>();
        [SerializeField] private PlayerController player;
        [Tooltip("Optional. Designer-tunable rarity draw weights; falls back to sane defaults if unset.")]
        [SerializeField] private RarityWeightTable rarityWeights;

        private GameObject _panel;
        private Button[] _btns = new Button[3];
        private Text[] _titles = new Text[3];
        private Text[] _descs = new Text[3];
        private readonly List<Augment> _current = new List<Augment>(3);
        private readonly List<Augment> _eligible = new List<Augment>(16);
        private Font _font;

        private void Start()
        {
            if (player == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) player = p.GetComponent<PlayerController>();
            }
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildUI();
            _panel.SetActive(false);
            if (GameManager.Instance != null) GameManager.Instance.OnDraftRequested += HandleDraftRequested;
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null) GameManager.Instance.OnDraftRequested -= HandleDraftRequested;
        }

        private void HandleDraftRequested() => ShowNext();

        /// <summary>Present the next banked pick, or close when all are resolved. Drains picks that have nothing
        /// offerable (e.g. every eligible augment is maxed) so the run never soft-locks at a breather.</summary>
        private void ShowNext()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null) { _panel.SetActive(false); return; }

            while (gm.PendingLevelUps > 0)
            {
                BuildEligible();
                if (_eligible.Count == 0) { gm.ConsumeOnePendingLevelUp(); continue; }
                DrawAndDisplay();
                return;   // wait for the player's pick
            }
            _panel.SetActive(false);
        }

        private void BuildEligible()
        {
            _eligible.Clear();
            PlayerLoadout loadout = player != null ? player.Loadout : null;
            for (int i = 0; i < pool.Count; i++)
            {
                Augment a = pool[i];
                if (a != null && (loadout == null || a.CanOffer(loadout))) _eligible.Add(a);
            }
        }

        private void DrawAndDisplay()
        {
            _current.Clear();
            for (int n = 0; n < 3 && _eligible.Count > 0; n++)
            {
                int idx = WeightedPick(_eligible);
                _current.Add(_eligible[idx]);
                _eligible.RemoveAt(idx);
            }
            for (int i = 0; i < _btns.Length; i++)
            {
                bool has = i < _current.Count;
                _btns[i].gameObject.SetActive(has);
                if (has)
                {
                    Augment a = _current[i];
                    _titles[i].text = a.title;
                    _titles[i].color = RarityColor(a.rarity);
                    _descs[i].text = a.description;
                }
            }
            _panel.SetActive(true);
        }

        private void Pick(int i)
        {
            if (i < _current.Count && player != null) player.ApplyAugment(_current[i]);
            if (GameManager.Instance != null) GameManager.Instance.ConsumeOnePendingLevelUp();
            ShowNext();   // next banked pick, or close
        }

        // ---------- Draft weighting ----------
        private int WeightedPick(List<Augment> list)
        {
            float total = 0f;
            for (int i = 0; i < list.Count; i++) total += Weight(list[i].rarity);
            if (total <= 0f) return Random.Range(0, list.Count);

            float roll = Random.value * total;
            for (int i = 0; i < list.Count; i++)
            {
                roll -= Weight(list[i].rarity);
                if (roll <= 0f) return i;
            }
            return list.Count - 1;
        }

        private float Weight(Rarity rarity)
        {
            return rarityWeights != null ? rarityWeights.Weight(rarity) : DefaultWeight(rarity);
        }

        private static float DefaultWeight(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Common: return 100f;
                case Rarity.Uncommon: return 55f;
                case Rarity.Rare: return 25f;
                case Rarity.Epic: return 10f;
                case Rarity.Legendary: return 3f;
                default: return 1f;
            }
        }

        private static Color RarityColor(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Common: return new Color(0.80f, 0.85f, 0.90f);
                case Rarity.Uncommon: return new Color(0.45f, 0.85f, 0.50f);
                case Rarity.Rare: return new Color(0.40f, 0.65f, 1.00f);
                case Rarity.Epic: return new Color(0.75f, 0.45f, 0.95f);
                case Rarity.Legendary: return new Color(1.00f, 0.70f, 0.25f);
                default: return Color.white;
            }
        }

        // ---------- UI construction ----------
        private void BuildUI()
        {
            var canvasGO = new GameObject("LevelUpCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            _panel = NewImage("Dim", canvasGO.transform, new Color(0f, 0f, 0f, 0.78f)).gameObject;
            var pr = _panel.GetComponent<RectTransform>();
            pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one; pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;

            Text header = NewText("Header", _panel.transform, "LEVEL UP — choose an augment", 44, TextAnchor.MiddleCenter);
            var hr = header.rectTransform;
            hr.anchorMin = new Vector2(0.5f, 1f); hr.anchorMax = new Vector2(0.5f, 1f); hr.pivot = new Vector2(0.5f, 1f);
            hr.anchoredPosition = new Vector2(0, -120); hr.sizeDelta = new Vector2(1200, 80);

            float cardW = 380, cardH = 460, gap = 60;
            float totalW = cardW * 3 + gap * 2;
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                var card = NewImage("Card" + i, _panel.transform, new Color(0.12f, 0.13f, 0.18f, 0.98f));
                var cr = card.rectTransform;
                cr.anchorMin = new Vector2(0.5f, 0.5f); cr.anchorMax = new Vector2(0.5f, 0.5f); cr.pivot = new Vector2(0.5f, 0.5f);
                cr.sizeDelta = new Vector2(cardW, cardH);
                cr.anchoredPosition = new Vector2(-totalW / 2f + cardW / 2f + i * (cardW + gap), -20);

                var btn = card.gameObject.AddComponent<Button>();
                btn.targetGraphic = card;
                var colors = btn.colors; colors.highlightedColor = new Color(0.22f, 0.45f, 0.7f, 1f); colors.pressedColor = new Color(0.3f, 0.6f, 0.9f, 1f); btn.colors = colors;
                btn.onClick.AddListener(() => Pick(idx));

                Text t = NewText("Title", card.transform, "Title", 30, TextAnchor.UpperCenter);
                var tr = t.rectTransform; tr.anchorMin = new Vector2(0, 1); tr.anchorMax = new Vector2(1, 1); tr.pivot = new Vector2(0.5f, 1f);
                tr.anchoredPosition = new Vector2(0, -28); tr.sizeDelta = new Vector2(-30, 70);
                t.color = new Color(0.6f, 0.85f, 1f);

                Text d = NewText("Desc", card.transform, "Desc", 24, TextAnchor.UpperCenter);
                var dr = d.rectTransform; dr.anchorMin = new Vector2(0, 0); dr.anchorMax = new Vector2(1, 1); dr.pivot = new Vector2(0.5f, 0.5f);
                dr.offsetMin = new Vector2(24, 24); dr.offsetMax = new Vector2(-24, -110);

                _btns[i] = btn; _titles[i] = t; _descs[i] = d;
            }
        }

        private Image NewImage(string name, Transform parent, Color col)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = col;
            return img;
        }

        private Text NewText(string name, Transform parent, string content, int size, TextAnchor anchor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var txt = go.AddComponent<Text>();
            txt.font = _font;
            txt.text = content;
            txt.fontSize = size;
            txt.color = Color.white;
            txt.alignment = anchor;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            return txt;
        }
    }
}
