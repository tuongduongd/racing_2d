using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace RacingGame
{
    /// <summary>
    /// Generates retro PS1 Road Rash UI sprites (brushed-metal dashboard housing, analog dial,
    /// red needle, LCD bezel, beveled buttons, minimap track texture) and shared uGUI builders.
    /// </summary>
    public static class RoadRashUiSprites
    {
        private static Sprite _panelSprite;
        private static Sprite _buttonSprite;
        private static Sprite _dashboardSprite;
        private static Sprite _gaugeDialSprite;
        private static Sprite _needleSprite;
        private static Sprite _dotSprite;
        private static Sprite _triangleSprite;
        private static Font _uiFont;

        public static Font GetUiFont()
        {
            if (_uiFont != null) return _uiFont;
            _uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_uiFont == null)
            {
                _uiFont = Font.CreateDynamicFontFromOSFont("Consolas", 16);
            }
            return _uiFont;
        }

        public static void EnsureEventSystem()
        {
            EventSystem es = Object.FindAnyObjectByType<EventSystem>();
            if (es == null)
            {
                GameObject esObj = new GameObject("EventSystem");
                es = esObj.AddComponent<EventSystem>();
            }

            if (es.GetComponent<InputSystemUIInputModule>() == null)
            {
                StandaloneInputModule legacy = es.GetComponent<StandaloneInputModule>();
                if (legacy != null) Object.Destroy(legacy);
                es.gameObject.AddComponent<InputSystemUIInputModule>();
            }
        }

        public static Canvas CreateOverlayCanvas(string name, int sortingOrder)
        {
            EnsureEventSystem();

            GameObject canvasObj = new GameObject(name);
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static Text CreateStyledText(
            Transform parent, string name, string content, int fontSize, Color color,
            TextAnchor alignment = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Bold, bool withShadow = true)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Text txt = go.AddComponent<Text>();
            txt.font = GetUiFont();
            txt.text = content;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = alignment;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.raycastTarget = false;

            if (withShadow)
            {
                Shadow sh = go.AddComponent<Shadow>();
                sh.effectColor = RoadRashVisualTheme.HudShadow;
                sh.effectDistance = new Vector2(2f, -2f);
            }
            return txt;
        }

        public static Button CreateBevelButton(Transform parent, string name, string label, Color tint, int fontSize = 15)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Image img = go.AddComponent<Image>();
            img.sprite = GetButtonSprite();
            img.type = Image.Type.Sliced;
            img.color = tint;

            Button btn = go.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.16f, 1.16f, 1.05f, 1f);
            cb.pressedColor = new Color(0.76f, 0.76f, 0.76f, 1f);
            cb.selectedColor = cb.highlightedColor;
            btn.colors = cb;

            Text txt = CreateStyledText(go.transform, "Label", label, fontSize, Color.white, TextAnchor.MiddleCenter);
            StretchFill(txt.rectTransform, 6f, 2f);
            return btn;
        }

        public static void StretchFill(RectTransform rt, float padX = 0f, float padY = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padX, padY);
            rt.offsetMax = new Vector2(-padX, -padY);
        }

        public static Sprite GetPanelSprite()
        {
            if (_panelSprite != null) return _panelSprite;
            const int s = 24;
            Texture2D tex = NewTex(s, s, FilterMode.Point);
            Color fill = new Color(1f, 1f, 1f, 0.94f);
            Color hi = new Color(0.86f, 0.89f, 0.95f, 1f);
            Color lo = new Color(0.28f, 0.30f, 0.36f, 1f);

            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    bool border = x == 0 || y == 0 || x == s - 1 || y == s - 1;
                    bool innerHi = (x == 1 || y == s - 2) && !border;
                    bool innerLo = (x == s - 2 || y == 1) && !border;
                    tex.SetPixel(x, y, border ? lo : (innerHi ? hi : (innerLo ? lo : fill)));
                }
            }
            tex.Apply();
            _panelSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect, new Vector4(4, 4, 4, 4));
            return _panelSprite;
        }

        public static Sprite GetButtonSprite()
        {
            if (_buttonSprite != null) return _buttonSprite;
            const int s = 24;
            Texture2D tex = NewTex(s, s, FilterMode.Point);
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    if (x == 0 || y == 0 || x == s - 1 || y == s - 1)
                    {
                        tex.SetPixel(x, y, new Color(0.10f, 0.12f, 0.15f, 1f));
                    }
                    else if (y >= s - 3 || x <= 2)
                    {
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, 1f));
                    }
                    else if (y <= 2 || x >= s - 3)
                    {
                        tex.SetPixel(x, y, new Color(0.62f, 0.65f, 0.70f, 1f));
                    }
                    else
                    {
                        float grad = Mathf.Lerp(0.78f, 0.96f, y / (float)s);
                        tex.SetPixel(x, y, new Color(grad, grad, grad, 1f));
                    }
                }
            }
            tex.Apply();
            _buttonSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect, new Vector4(5, 5, 5, 5));
            return _buttonSprite;
        }

        public static Sprite GetDashboardSprite()
        {
            if (_dashboardSprite != null) return _dashboardSprite;
            const int w = 260;
            const int h = 104;
            Texture2D tex = NewTex(w, h, FilterMode.Point);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float grad = Mathf.Lerp(0.42f, 0.76f, y / (float)h);
                    float grain = ((x * 13 + y * 7) % 11) * 0.008f;
                    Color c = new Color(grad + grain, grad + grain + 0.01f, grad + grain + 0.03f, 0.96f);

                    // Top bevel & dark outer rim
                    if (x < 2 || y < 2 || x >= w - 2 || y >= h - 2)
                        c = RoadRashVisualTheme.MetalDark;
                    else if (y >= h - 4 || x <= 3)
                        c = RoadRashVisualTheme.MetalLight;

                    // Cutout silhouette shape on top-right like classic Road Rash dash
                    if (x > 106 && y > 82)
                    {
                        c = new Color(0f, 0f, 0f, 0f);
                        if (y == 83 && x > 106) c = RoadRashVisualTheme.MetalLight;
                    }
                    else if (x == 106 && y > 82)
                    {
                        c = RoadRashVisualTheme.MetalDark;
                    }

                    // Recessed LCD box on right side (x: 118..244, y: 14..68)
                    if (x >= 118 && x <= 244 && y >= 14 && y <= 68)
                    {
                        bool rim = x < 121 || x > 241 || y < 17 || y > 65;
                        c = rim ? RoadRashVisualTheme.MetalDark : RoadRashVisualTheme.LcdBackground;
                    }

                    // Top dark label strip above LCD (x: 118..244, y: 71..80)
                    if (x >= 118 && x <= 244 && y >= 71 && y <= 79)
                    {
                        c = new Color(0.10f, 0.12f, 0.14f, 1f);
                    }

                    tex.SetPixel(x, y, c);
                }
            }

            tex.Apply();
            _dashboardSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            return _dashboardSprite;
        }

        public static Sprite GetGaugeDialSprite()
        {
            if (_gaugeDialSprite != null) return _gaugeDialSprite;
            const int s = 96;
            Texture2D tex = NewTex(s, s, FilterMode.Bilinear);
            float cxy = (s - 1) * 0.5f;

            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float dx = x - cxy;
                    float dy = y - cxy;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    Color col = new Color(0f, 0f, 0f, 0f);

                    if (r <= 46f)
                    {
                        if (r >= 42f) col = RoadRashVisualTheme.MetalDark;
                        else if (r >= 39f) col = RoadRashVisualTheme.MetalLight;
                        else col = new Color(0.18f, 0.20f, 0.23f, 1f);
                    }
                    tex.SetPixel(x, y, col);
                }
            }

            // Draw tick marks from 220 deg clockwise to -40 deg
            for (int t = 0; t <= 12; t++)
            {
                float frac = t / 12f;
                float deg = Mathf.Lerp(220f, -40f, frac);
                float rad = deg * Mathf.Deg2Rad;
                Color tickCol = t >= 10 ? RoadRashVisualTheme.DangerRed : Color.white;
                float rIn = (t % 2 == 0) ? 28f : 32f;
                float rOut = 37f;

                for (float rr = rIn; rr <= rOut; rr += 0.5f)
                {
                    int px = Mathf.RoundToInt(cxy + Mathf.Cos(rad) * rr);
                    int py = Mathf.RoundToInt(cxy + Mathf.Sin(rad) * rr);
                    if (px >= 0 && px < s && py >= 0 && py < s)
                    {
                        tex.SetPixel(px, py, tickCol);
                        if (px + 1 < s) tex.SetPixel(px + 1, py, tickCol);
                    }
                }
            }

            tex.Apply();
            _gaugeDialSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
            return _gaugeDialSprite;
        }

        public static Sprite GetNeedleSprite()
        {
            if (_needleSprite != null) return _needleSprite;
            const int w = 8;
            const int h = 44;
            Texture2D tex = NewTex(w, h, FilterMode.Bilinear);
            Color needleRed = new Color(0.96f, 0.18f, 0.14f, 1f);

            for (int y = 0; y < h; y++)
            {
                float halfW = Mathf.Lerp(2.4f, 0.6f, y / (float)h);
                for (int x = 0; x < w; x++)
                {
                    float dx = Mathf.Abs(x - 3.5f);
                    if (y >= 4 && dx <= halfW)
                    {
                        tex.SetPixel(x, y, needleRed);
                    }
                    else
                    {
                        tex.SetPixel(x, y, new Color(0f, 0f, 0f, 0f));
                    }
                }
            }

            tex.Apply();
            // Pivot near bottom center of the needle
            _needleSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.14f), 100f);
            return _needleSprite;
        }

        public static Sprite GetDotSprite()
        {
            if (_dotSprite != null) return _dotSprite;
            const int s = 16;
            Texture2D tex = NewTex(s, s, FilterMode.Bilinear);
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float dx = (x - 7.5f) / 6.5f;
                    float dy = (y - 7.5f) / 6.5f;
                    float d2 = dx * dx + dy * dy;
                    Color c = d2 <= 1f ? Color.white : (d2 <= 1.35f ? new Color(0f, 0f, 0f, 0.9f) : new Color(0f, 0f, 0f, 0f));
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            _dotSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 16f);
            return _dotSprite;
        }

        public static Sprite GetTriangleSprite()
        {
            if (_triangleSprite != null) return _triangleSprite;
            const int s = 16;
            Texture2D tex = NewTex(s, s, FilterMode.Point);
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float half = (s - 1 - y) * 0.45f;
                    bool inside = y >= 2 && y <= 14 && Mathf.Abs(x - 7.5f) <= half;
                    tex.SetPixel(x, y, inside ? Color.white : new Color(0f, 0f, 0f, 0f));
                }
            }
            tex.Apply();
            _triangleSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 16f);
            return _triangleSprite;
        }

        public static Texture2D BuildTrackMinimapTexture(
            WaypointTrack track, int size, out Vector2 minBounds, out float maxDim)
        {
            Texture2D tex = NewTex(size, size, FilterMode.Bilinear);
            Color bg = new Color(0.06f, 0.08f, 0.12f, 0.72f);
            Color border = RoadRashVisualTheme.MetalLight;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isBorder = x < 2 || y < 2 || x >= size - 2 || y >= size - 2;
                    tex.SetPixel(x, y, isBorder ? border : bg);
                }
            }

            IReadOnlyList<Vector2> wps = track != null ? track.Waypoints : null;
            if (wps == null || wps.Count < 2)
            {
                minBounds = Vector2.zero;
                maxDim = 1f;
                tex.Apply();
                return tex;
            }

            minBounds = wps[0];
            Vector2 max = wps[0];
            for (int i = 1; i < wps.Count; i++)
            {
                minBounds = Vector2.Min(minBounds, wps[i]);
                max = Vector2.Max(max, wps[i]);
            }

            Vector2 span = max - minBounds;
            maxDim = Mathf.Max(1f, Mathf.Max(span.x, span.y));

            Vector2 origin = minBounds;
            float spanDim = maxDim;
            float pad = size * 0.12f;
            float usable = size - pad * 2f;

            Vector2 ToPx(Vector2 world)
            {
                Vector2 norm = (world - origin) / spanDim;
                return new Vector2(pad + norm.x * usable, pad + norm.y * usable);
            }

            void DrawThickLine(Vector2 a, Vector2 b, float radius, Color col)
            {
                int steps = Mathf.CeilToInt(Vector2.Distance(a, b) * 1.5f);
                for (int s = 0; s <= steps; s++)
                {
                    Vector2 p = Vector2.Lerp(a, b, steps == 0 ? 0f : s / (float)steps);
                    int rInt = Mathf.CeilToInt(radius);
                    for (int dy = -rInt; dy <= rInt; dy++)
                    {
                        for (int dx = -rInt; dx <= rInt; dx++)
                        {
                            if (dx * dx + dy * dy <= radius * radius)
                            {
                                int px = Mathf.RoundToInt(p.x + dx);
                                int py = Mathf.RoundToInt(p.y + dy);
                                if (px >= 2 && px < size - 2 && py >= 2 && py < size - 2)
                                {
                                    tex.SetPixel(px, py, col);
                                }
                            }
                        }
                    }
                }
            }

            for (int i = 0; i < wps.Count; i++)
            {
                Vector2 a = ToPx(wps[i]);
                Vector2 b = ToPx(wps[(i + 1) % wps.Count]);
                DrawThickLine(a, b, 3.2f, new Color(0.24f, 0.27f, 0.34f, 1f));
            }
            for (int i = 0; i < wps.Count; i++)
            {
                Vector2 a = ToPx(wps[i]);
                Vector2 b = ToPx(wps[(i + 1) % wps.Count]);
                DrawThickLine(a, b, 1.8f, new Color(0.84f, 0.88f, 0.94f, 1f));
            }

            // Start/finish mark
            Vector2 startPx = ToPx(wps[0]);
            DrawThickLine(startPx - new Vector2(3f, 0f), startPx + new Vector2(3f, 0f), 2.2f, RoadRashVisualTheme.HudYellow);

            tex.Apply();
            return tex;
        }

        private static Texture2D NewTex(int w, int h, FilterMode filter)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = filter,
                wrapMode = TextureWrapMode.Clamp
            };
        }
    }
}
