using UnityEngine;
using UnityEngine.UI;

namespace IronDistrict
{
    /// <summary>Fully code-built HUD (uGUI): vitals, ammo, crosshair, wave banners, pause + death screens.</summary>
    public class HUD : MonoBehaviour
    {
        Canvas canvas;
        Text hpText, ammoText, reserveText, scoreText, killsText, enemiesText, waveText, toastText;
        Text deathStats, hintsText;
        RectTransform hpFill;
        Image hitmarker;
        Image vignette;
        RectTransform[] crossParts;
        GameObject waveBanner;
        Text waveBannerText;
        GameObject pausePanel;
        GameObject deathPanel;
        Text pauseStats;

        float toastTimer;
        float bannerTimer;
        float hitTimer;
        float vignetteAlpha;
        Sprite radialSprite;

        // ------------------------------------------------------------------ build
        public static HUD Create(Transform parent)
        {
            var go = new GameObject("HUD");
            go.transform.SetParent(parent, false);
            var hud = go.AddComponent<HUD>();
            hud.Build();
            return hud;
        }

        void Build()
        {
            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var font = UIFont.Get();
            radialSprite = MakeRadialSprite();

            // ---------- damage vignette (behind everything else)
            vignette = MakeImage("Vignette", canvas.transform, new Color(0.7f, 0.02f, 0.02f, 0f));
            Stretch(vignette.rectTransform);
            vignette.sprite = radialSprite;
            vignette.type = Image.Type.Simple;
            vignette.raycastTarget = false;

            // ---------- crosshair
            var crossRoot = new GameObject("Crosshair", typeof(RectTransform));
            crossRoot.transform.SetParent(canvas.transform, false);
            var crt = crossRoot.GetComponent<RectTransform>();
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = Vector2.zero;

            crossParts = new RectTransform[5];
            for (int i = 0; i < 5; i++)
            {
                bool horizontal = i < 2 || i == 4;
                var img = MakeImage("part" + i, crossRoot.transform, new Color(0.95f, 0.95f, 0.92f, 0.85f));
                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = (i == 4) ? new Vector2(2.5f, 2.5f) : (horizontal ? new Vector2(2.6f, 11f) : new Vector2(11f, 2.6f));
                img.raycastTarget = false;
                crossParts[i] = rt;
            }

            // ---------- hitmarker
            hitmarker = MakeImage("Hitmarker", canvas.transform, new Color(1f, 1f, 1f, 0f));
            Stretch(hitmarker.rectTransform);
            hitmarker.sprite = MakeHitmarkerSprite();
            hitmarker.raycastTarget = false;

            // ---------- top-left: vitals
            var vitals = MakePanel("Vitals", canvas.transform, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(46, -46), new Vector2(360, 96));
            hpText = MakeText("HPText", vitals.transform, "100", 34, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.95f), FontStyle.Bold);
            SetRect(hpText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -8), new Vector2(150, 40));
            var hpLabel = MakeText("HPLabel", vitals.transform, "ЗДОРОВЬЕ", 13, TextAnchor.UpperLeft, Palette.TextDim);
            SetRect(hpLabel.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(19, -48), new Vector2(200, 20));

            var barBg = MakeImage("BarBG", vitals.transform, new Color(0f, 0f, 0f, 0.55f));
            SetRect(barBg.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(18, 12), new Vector2(250, 12));
            var fill = MakeImage("BarFill", barBg.transform, Palette.Danger);
            fill.rectTransform.anchorMin = new Vector2(0, 0);
            fill.rectTransform.anchorMax = new Vector2(1, 1);
            fill.rectTransform.offsetMin = new Vector2(2, 2);
            fill.rectTransform.offsetMax = new Vector2(-2, -2);
            hpFill = fill.rectTransform;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;

            // ---------- bottom-right: ammo
            var ammoPanel = MakePanel("Ammo", canvas.transform, new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-46, 46), new Vector2(300, 120));
            ammoText = MakeText("AmmoText", ammoPanel.transform, "30", 60, TextAnchor.LowerRight, Palette.Text, FontStyle.Bold);
            SetRect(ammoText.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-130, 26), new Vector2(120, 70));
            reserveText = MakeText("ReserveText", ammoPanel.transform, "/ 150", 24, TextAnchor.LowerRight, Palette.TextDim);
            SetRect(reserveText.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-22, 32), new Vector2(120, 30));
            var weaponLabel = MakeText("WeaponLabel", ammoPanel.transform, "AR-7  ·  АВТО", 15, TextAnchor.LowerRight, Palette.TextDim);
            SetRect(weaponLabel.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-22, 100), new Vector2(240, 24));

            // ---------- top-right: score
            var scorePanel = MakePanel("Score", canvas.transform, new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-46, -46), new Vector2(330, 118));
            scoreText = MakeText("ScoreText", scorePanel.transform, "0", 30, TextAnchor.UpperRight, Palette.Text, FontStyle.Bold);
            SetRect(scoreText.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-18, -8), new Vector2(200, 40));
            var scoreLabel = MakeText("ScoreLabel", scorePanel.transform, "ОЧКИ", 13, TextAnchor.UpperRight, Palette.TextDim);
            SetRect(scoreLabel.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-18, -46), new Vector2(200, 20));
            killsText = MakeText("KillsText", scorePanel.transform, "УБИЙСТВА 0", 17, TextAnchor.UpperRight, Palette.TextDim);
            SetRect(killsText.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-18, -66), new Vector2(200, 24));
            enemiesText = MakeText("EnemiesText", scorePanel.transform, "ВРАГИ 0", 17, TextAnchor.UpperRight, Palette.Accent);
            SetRect(enemiesText.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-18, -88), new Vector2(200, 24));

            // ---------- top-center: wave
            waveText = MakeText("WaveText", canvas.transform, "ВОЛНА 1", 20, TextAnchor.UpperCenter, Palette.TextDim);
            SetRect(waveText.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(420, 30));

            waveBanner = MakeBanner(out waveBannerText);

            // ---------- bottom-center: toast + hints
            toastText = MakeText("Toast", canvas.transform, "", 22, TextAnchor.LowerCenter, Palette.Accent, FontStyle.Bold);
            SetRect(toastText.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 210), new Vector2(700, 34));

            hintsText = MakeText("Hints", canvas.transform,
                "WASD — движение · SHIFT — бег · ПРОБЕЛ — прыжок · СТРЕЛКИ — обзор\nЛКМ — огонь · ПКМ — прицел · R — перезарядка · ESC — пауза",
                16, TextAnchor.LowerLeft, new Color(1f, 1f, 1f, 0.55f));
            SetRect(hintsText.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(46, 42), new Vector2(760, 48));

            // ---------- pause panel
            pausePanel = MakeOverlay("Pause");
            var pauseTitle = MakeText("Title", pausePanel.transform, "ПАУЗА", 64, TextAnchor.MiddleCenter, Palette.Text, FontStyle.Bold);
            SetRect(pauseTitle.rectTransform, new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.62f), Vector2.zero, new Vector2(900, 90));
            pauseStats = MakeText("Stats", pausePanel.transform, "", 22, TextAnchor.MiddleCenter, Palette.TextDim);
            SetRect(pauseStats.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 120));
            var pauseHint = MakeText("Hint", pausePanel.transform,
                "ESC — продолжить    ENTER — начать заново    Q — выйти в меню", 20, TextAnchor.MiddleCenter, Palette.Accent);
            SetRect(pauseHint.rectTransform, new Vector2(0.5f, 0.36f), new Vector2(0.5f, 0.36f), Vector2.zero, new Vector2(1100, 40));
            pausePanel.SetActive(false);

            // ---------- death panel
            deathPanel = MakeOverlay("Death");
            var deathTitle = MakeText("Title", deathPanel.transform, "ВЫ УБИТЫ", 72, TextAnchor.MiddleCenter, Palette.Danger, FontStyle.Bold);
            SetRect(deathTitle.rectTransform, new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.62f), Vector2.zero, new Vector2(1000, 100));
            deathStats = MakeText("Stats", deathPanel.transform, "", 24, TextAnchor.MiddleCenter, Palette.Text);
            SetRect(deathStats.rectTransform, new Vector2(0.5f, 0.48f), new Vector2(0.5f, 0.48f), Vector2.zero, new Vector2(900, 160));
            var deathHint = MakeText("Hint", deathPanel.transform,
                "R — попробовать снова      ESC — в меню", 22, TextAnchor.MiddleCenter, Palette.Accent);
            SetRect(deathHint.rectTransform, new Vector2(0.5f, 0.3f), new Vector2(0.5f, 0.3f), Vector2.zero, new Vector2(900, 40));
            deathPanel.SetActive(false);
        }

        GameObject MakeOverlay(string name)
        {
            var panel = MakeImage(name, canvas.transform, new Color(0.02f, 0.02f, 0.03f, 0.78f));
            Stretch(panel.rectTransform);
            panel.raycastTarget = false;
            return panel.gameObject;
        }

        GameObject MakeBanner(out Text text)
        {
            var panel = MakeImage("WaveBanner", canvas.transform, new Color(0.03f, 0.03f, 0.04f, 0f));
            var rt = panel.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(760, 120);
            rt.anchoredPosition = new Vector2(0, 170);
            var t = MakeText("BannerText", panel.transform, "ВОЛНА 1", 54, TextAnchor.MiddleCenter, Palette.Text, FontStyle.Bold);
            Stretch(t.rectTransform);
            text = t;
            panel.raycastTarget = false;
            panel.gameObject.SetActive(false);
            return panel.gameObject;
        }

        // ------------------------------------------------------------------ helpers
        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void SetRect(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        Image MakePanel(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var img = MakeImage(name, parent, Palette.Panel);
            SetRect(img.rectTransform, anchor, pivot, pos, size);
            img.raycastTarget = false;
            return img;
        }

        static Image MakeImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        static Text MakeText(string name, Transform parent, string content, int size, TextAnchor anchor,
            Color color, FontStyle style = FontStyle.Normal)
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
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }

        Sprite MakeRadialSprite()
        {
            const int s = 128;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float dx = (x - s * 0.5f) / (s * 0.5f);
                    float dy = (y - s * 0.5f) / (s * 0.5f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((d - 0.45f) / 0.55f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
        }

        Sprite MakeHitmarkerSprite()
        {
            const int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float dx = Mathf.Abs(x - s * 0.5f);
                    float dy = Mathf.Abs(y - s * 0.5f);
                    bool diag = Mathf.Abs(dx - dy) < 2.2f && Mathf.Max(dx, dy) > 8f && Mathf.Max(dx, dy) < 26f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, diag ? 1f : 0f));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
        }

        // ------------------------------------------------------------------ events
        public void ShowHitmarker(bool headshot)
        {
            hitTimer = 0.14f;
            hitmarker.color = headshot ? new Color(1f, 0.42f, 0.2f, 0.95f) : new Color(1f, 1f, 1f, 0.95f);
            if (headshot) Sfx.Play("sfx_hit", 0.5f, 1.4f);
            else Sfx.Play("sfx_hit", 0.35f, 0.9f);
        }

        public void OnKill(bool headshot)
        {
            ShowToast(headshot ? "В ГОЛОВУ!  +150" : "ЦЕЛЬ УСТРАНЕНА  +100");
        }

        public void ShowToast(string msg)
        {
            toastText.text = msg;
            toastTimer = 1.6f;
        }

        public void ShowWave(int wave)
        {
            waveText.text = "ВОЛНА " + wave;
            if (wave > 1)
            {
                waveBannerText.text = "ВОЛНА " + wave;
                waveBanner.SetActive(true);
                var img = waveBanner.GetComponent<Image>();
                img.color = new Color(0.03f, 0.03f, 0.04f, 0.65f);
                bannerTimer = 2.6f;
            }
        }

        public void ShowWaveCleared()
        {
            waveBannerText.text = "ВОЛНА ЗАЧИЩЕНА";
            waveBanner.SetActive(true);
            var img = waveBanner.GetComponent<Image>();
            img.color = new Color(0.05f, 0.14f, 0.05f, 0.65f);
            bannerTimer = 3.4f;
            ShowToast("ПЕРЕДЫШКА...");
        }

        public void OnPlayerDamaged(Vector3 point, bool headshot)
        {
            vignetteAlpha = Mathf.Min(0.85f, vignetteAlpha + 0.5f);
        }

        public void ShowDeath(int kills, int score, int wave)
        {
            deathPanel.SetActive(true);
            pausePanel.SetActive(false);
            int best = GameManager.Best;
            deathStats.text = $"УБИЙСТВ К {kills}   ·   ВОЛНА {wave}\nОЧКИ {score}      РЕКОРД {best}";
        }

        public void ShowPause(bool visible, int kills, int score, int wave)
        {
            pausePanel.SetActive(visible);
            if (visible)
                pauseStats.text = $"УБИЙСТВ {kills}      ОЧКИ {score}      ВОЛНА {wave}";
        }

        public void HideDeath() => deathPanel.SetActive(false);

        // ------------------------------------------------------------------ update
        void Update()
        {
            var root = GameRoot.I;
            if (root == null || root.Player == null) return;
            var p = root.Player;

            // vitals
            float hp = p.Health.Health;
            hpText.text = Mathf.CeilToInt(hp).ToString();
            hpText.color = hp < 30f ? Palette.Danger : new Color(1f, 1f, 1f, 0.95f);
            hpFill.anchorMax = new Vector2(Mathf.Clamp01(hp / p.Health.MaxHealth), 1f);

            // ammo
            ammoText.text = p.Weapon.AmmoInMag.ToString();
            ammoText.color = p.Weapon.AmmoInMag <= 6 ? Palette.Danger : Palette.Text;
            reserveText.text = "/ " + p.Weapon.Reserve;
            if (p.Weapon.Reloading)
                ammoText.text = "…";

            // score
            var gm = GameManager.I;
            if (gm != null)
            {
                scoreText.text = gm.Score.ToString();
                killsText.text = "УБИЙСТВА " + gm.Kills;
                int alive = gm.Spawner != null ? gm.Spawner.WaveRemaining : 0;
                enemiesText.text = "ОСТАЛОСЬ " + alive;
            }

            // crosshair dynamic gap
            float speed01 = p.Speed01;
            bool ads = p.Weapon.IsAds;
            float targetGap = ads ? 9f : 22f + speed01 * 26f + (p.Weapon.Reloading ? 10f : 0f);
            float gap = Mathf.Lerp(crossParts[0].anchoredPosition.x, targetGap, 1f - Mathf.Exp(-10f * Time.deltaTime));
            if (ads)
            {
                for (int i = 0; i < 4; i++) crossParts[i].GetComponent<Image>().color = new Color(0.95f, 0.95f, 0.92f, 0.35f);
            }
            else
            {
                for (int i = 0; i < 4; i++) crossParts[i].GetComponent<Image>().color = new Color(0.95f, 0.95f, 0.92f, 0.85f);
            }
            crossParts[0].anchoredPosition = new Vector2(0, gap);
            crossParts[1].anchoredPosition = new Vector2(0, -gap);
            crossParts[2].anchoredPosition = new Vector2(-gap, 0);
            crossParts[3].anchoredPosition = new Vector2(gap, 0);

            // hitmarker
            if (hitTimer > 0f)
            {
                hitTimer -= Time.deltaTime;
                var c = hitmarker.color;
                c.a = Mathf.Clamp01(hitTimer / 0.14f) * 0.95f;
                hitmarker.color = c;
            }

            // vignette
            vignetteAlpha = Mathf.MoveTowards(vignetteAlpha, 0f, Time.deltaTime * 0.9f);
            var vc = vignette.color;
            vc.a = vignetteAlpha;
            vignette.color = vc;

            // toast
            if (toastTimer > 0f)
            {
                toastTimer -= Time.deltaTime;
                var c = toastText.color;
                c.a = Mathf.Clamp01(toastTimer / 0.5f);
                toastText.color = c;
            }

            // banner
            if (bannerTimer > 0f)
            {
                bannerTimer -= Time.deltaTime;
                var img = waveBanner.GetComponent<Image>();
                var c = img.color;
                c.a = Mathf.Clamp01(bannerTimer / 0.8f) * 0.68f;
                img.color = c;
                var tc = waveBannerText.color;
                tc.a = Mathf.Clamp01(bannerTimer / 0.8f);
                waveBannerText.color = tc;
                if (bannerTimer <= 0f) waveBanner.SetActive(false);
            }

            // hints fade after 25 s of play
            if (hintsText != null)
            {
                float elapsed = Time.timeSinceLevelLoad;
                var c = hintsText.color;
                c.a = Mathf.Lerp(0.55f, 0f, Mathf.Clamp01((elapsed - 22f) / 6f));
                hintsText.color = c;
            }
        }
    }
}
