using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Hordebreakers
{
    /// <summary>
    /// On level-up, pauses the run and presents 3 random upgrade cards. Builds its own
    /// canvas/buttons in code; applies the chosen card to the player and resumes.
    /// </summary>
    public class UpgradeCardUI : MonoBehaviour
    {
        [SerializeField] private List<UpgradeCard> pool = new List<UpgradeCard>();
        [SerializeField] private PlayerController player;

        private GameObject _panel;
        private Button[] _btns = new Button[3];
        private Text[] _titles = new Text[3];
        private Text[] _descs = new Text[3];
        private readonly List<UpgradeCard> _current = new List<UpgradeCard>(3);
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
            if (GameManager.Instance != null) GameManager.Instance.OnLevelUp += HandleLevelUp;
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null) GameManager.Instance.OnLevelUp -= HandleLevelUp;
        }

        private void HandleLevelUp(int level) => Show();

        private void Show()
        {
            _current.Clear();
            List<UpgradeCard> temp = new List<UpgradeCard>(pool);
            for (int i = 0; i < 3 && temp.Count > 0; i++)
            {
                int r = Random.Range(0, temp.Count);
                _current.Add(temp[r]);
                temp.RemoveAt(r);
            }
            for (int i = 0; i < _btns.Length; i++)
            {
                bool has = i < _current.Count;
                _btns[i].gameObject.SetActive(has);
                if (has)
                {
                    _titles[i].text = _current[i].title;
                    _descs[i].text = _current[i].description;
                }
            }
            _panel.SetActive(true);
            Time.timeScale = 0f;
        }

        private void Pick(int i)
        {
            if (i < _current.Count && player != null) player.ApplyUpgrade(_current[i]);
            _panel.SetActive(false);
            if (GameManager.Instance != null) GameManager.Instance.ClearLevelUpPending();
            else Time.timeScale = 1f;
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

            Text header = NewText("Header", _panel.transform, "LEVEL UP — choose an upgrade", 44, TextAnchor.MiddleCenter);
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
