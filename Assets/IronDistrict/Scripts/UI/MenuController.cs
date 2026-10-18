using UnityEngine;
using UnityEngine.UI;

namespace IronDistrict
{
    /// <summary>Main menu built from code: start, settings cycle buttons, controls.</summary>
    public class MenuController : MonoBehaviour
    {
        Canvas canvas;
        Text statusText, bestText, sensText, volText, hintText;
        GameObject root;
        float t;

        public static MenuController Create(Transform parent)
        {
            var go = new GameObject("Menu");
            go.transform.SetParent(parent, false);
            var m = go.AddComponent<MenuController>();
            m.Build();
            return m;
        }

        void Build()
        {
            var canvasGo = new GameObject("MenuCanvas");
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            root = canvasGo;

            // left dark gradient-ish panel
            var shade = Img("Shade", canvasGo.transform, new Color(0.02f, 0.025f, 0.03f, 0.82f));
            shade.rectTransform.anchorMin = new Vector2(0, 0);
            shade.rectTransform.anchorMax = new Vector2(0.46f, 1);
            shade.rectTransform.offsetMin = Vector2.zero;
            shade.rectTransform.offsetMax = Vector2.zero;

            var accentLine = Img("AccentLine", canvasGo.transform, Palette.Accent);
            accentLine.rectTransform.anchorMin = new Vector2(0.46f, 0);
            accentLine.rectTransform.anchorMax = new Vector2(0.463f, 1);
            accentLine.rectTransform.offsetMin = Vector2.zero;
            accentLine.rectTransform.offsetMax = Vector2.zero;

            var title = Txt("Title", canvasGo.transform, "IRON", 128, TextAnchor.MiddleLeft, Palette.Text, FontStyle.Bold);
            SetRect(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(100, -190), new Vector2(700, 140));
            var title2 = Txt("Title2", canvasGo.transform, "DISTRICT", 128, TextAnchor.MiddleLeft, Palette.Accent, FontStyle.Bold);
            SetRect(title2.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(100, -300), new Vector2(700, 140));

            var sub = Txt("Sub", canvasGo.transform,
                "ПРОЦЕДУРНЫЙ ШУТЕР С ОТКРЫТЫМ МИРОМ", 20, TextAnchor.MiddleLeft, Palette.TextDim);
            SetRect(sub.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(103, -372), new Vector2(700, 30));

            // buttons
            float y = -470;
            MakeButton("НАЧАТЬ ИГРУ   [ENTER]", ref y, () => GameRoot.I.StartGame(), true);
            sensText = MakeButton("ЧУВСТВИТЕЛЬНОСТЬ: 1.0", ref y, CycleSensitivity, false);
            volText = MakeButton("ГРОМКОСТЬ: 90%", ref y, CycleVolume, false);
            MakeButton("НОВЫЙ МИР (СБРОС СИДА)", ref y, NewWorld, false);

            bestText = Txt("Best", canvasGo.transform, "", 20, TextAnchor.MiddleLeft, Palette.TextDim);
            SetRect(bestText.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(103, 132), new Vector2(700, 30));

            hintText = Txt("Controls", canvasGo.transform,
                "WASD — движение · SHIFT — бег · ПРОБЕЛ — прыжок\nЛКМ — огонь · ПКМ — прицел · R — перезарядка · ESC — пауза",
                16, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.45f));
            SetRect(hintText.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(103, 84), new Vector2(900, 44));

            statusText = Txt("Status", canvasGo.transform, "", 18, TextAnchor.MiddleLeft, Palette.Accent, FontStyle.Bold);
            SetRect(statusText.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(103, 34), new Vector2(900, 30));

            var ver = Txt("Ver", canvasGo.transform, "v1.0 · procedural · IronDistrict", 14, TextAnchor.LowerRight, new Color(1f, 1f, 1f, 0.35f));
            SetRect(ver.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-24, 18), new Vector2(600, 24));

            RefreshLabels();
        }

        Text MakeButton(string label, ref float y, UnityEngine.Events.UnityAction action, bool primary)
        {
            var img = Img("Btn_" + label, root.transform, primary ? new Color(0.85f, 0.42f, 0.1f, 0.95f) : new Color(1f, 1f, 1f, 0.08f));
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(103, y);
            rt.sizeDelta = new Vector2(520, 56);
            img.raycastTarget = true;

            var btn = img.gameObject.AddComponent<Button>();
            var cb = btn.colors;
            cb.normalColor = new Color(1f, 1f, 1f, 1f);
            cb.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            cb.fadeDuration = 0.08f;
            btn.colors = cb;
            btn.targetGraphic = img;
            btn.onClick.AddListener(() =>
            {
                Sfx.Play("sfx_hit", 0.4f, 1.3f);
                action();
            });

            var txt = Txt("Label", img.transform, label, 22, TextAnchor.MiddleLeft,
                primary ? new Color(0.08f, 0.05f, 0.03f) : Palette.Text, primary ? FontStyle.Bold : FontStyle.Normal);
            SetRect(txt.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(22, 0), new Vector2(480, 40));
            txt.raycastTarget = false;

            y -= 68;
            return txt;
        }

        void CycleSensitivity()
        {
            float[] steps = { 0.5f, 0.75f, 1f, 1.25f, 1.5f, 2f };
            float cur = InputService.Sensitivity;
            int idx = 0;
            for (int i = 0; i < steps.Length; i++) if (Mathf.Approximately(steps[i], cur)) idx = i;
            idx = (idx + 1) % steps.Length;
            InputService.Sensitivity = steps[idx];
            PlayerPrefs.SetFloat("IrD_Sens", InputService.Sensitivity);
            RefreshLabels();
        }

        void CycleVolume()
        {
            float cur = AudioListener.volume;
            cur = Mathf.Round(cur * 10f) / 10f;
            cur -= 0.1f;
            if (cur < 0.05f) cur = 1f;
            AudioListener.volume = cur;
            PlayerPrefs.SetFloat("IrD_Vol", cur);
            RefreshLabels();
        }

        void NewWorld()
        {
            if (GameRoot.I != null) GameRoot.I.RequestNewWorld();
            else
            {
                PlayerPrefs.SetInt("IrD_Seed", 0);
                PlayerPrefs.Save();
            }
            SetStatus("СИД СБРОШЕН — СЛЕДУЮЩИЙ МИР БУДЕТ НОВЫМ");
        }

        void RefreshLabels()
        {
            if (sensText != null) sensText.text = "ЧУВСТВИТЕЛЬНОСТЬ: " + InputService.Sensitivity.ToString("0.0#");
            if (volText != null) volText.text = "ГРОМКОСТЬ: " + Mathf.RoundToInt(AudioListener.volume * 100f) + "%";
            if (bestText != null) bestText.text = "РЕКОРД: " + GameManager.Best;
        }

        public void Show()
        {
            root.SetActive(true);
            RefreshLabels();
            SetStatus("");
        }

        public void Hide() => root.SetActive(false);

        public void SetStatus(string s)
        {
            if (statusText != null) statusText.text = s;
        }

        void Update()
        {
            t += Time.unscaledDeltaTime;
            if (statusText != null && !string.IsNullOrEmpty(statusText.text))
            {
                var c = statusText.color;
                c.a = 0.55f + 0.45f * Mathf.Sin(t * 4f);
                statusText.color = c;
            }
        }

        // helpers
        static Image Img(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        static Text Txt(string name, Transform parent, string content, int size, TextAnchor anchor, Color color,
            FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = UIFont.Get();
            t.text = content;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var sh = go.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, 0.55f);
            sh.effectDistance = new Vector2(2, -2);
            return t;
        }

        static void SetRect(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }
    }
}
