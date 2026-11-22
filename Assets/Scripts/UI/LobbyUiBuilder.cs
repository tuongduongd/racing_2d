using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RacingGame
{
    public sealed class MapTabWidgets
    {
        public string MapId;
        public Button Button;
        public Image Bg;
        public TMP_Text Label;
    }

    public sealed class SkinCardWidgets
    {
        public DriverBrandSkin Brand;
        public Image CardBg;
        public Image HeaderStrip;
        public Image PreviewIcon;
        public TMP_Text NameText;
        public TMP_Text TaglineText;
        public TMP_Text SelectBtnLabel;
        public Image SelectBtnImg;
        public Button SelectBtn;
    }

    /// <summary>
    /// Shared Cyber Courier Velocity UI builder (uGUI + TextMeshPro) for Lobby,
    /// Skin Selector, and Map Carousel. Keeps visual construction out of GameMenuUI.
    /// </summary>
    public static class LobbyUiBuilder
    {
        public static readonly Color BgAsphalt = new Color(0.063f, 0.078f, 0.102f, 1.00f);
        public static readonly Color SurfaceLowest = new Color(0.039f, 0.055f, 0.078f, 0.96f);
        public static readonly Color SurfaceLow = new Color(0.094f, 0.110f, 0.133f, 0.96f);
        public static readonly Color SurfaceContainer = new Color(0.110f, 0.125f, 0.149f, 0.96f);
        public static readonly Color SurfaceHigh = new Color(0.149f, 0.165f, 0.192f, 0.96f);
        public static readonly Color OutlineVariant = new Color(0.231f, 0.286f, 0.294f, 1.00f);
        public static readonly Color ElectricCyan = new Color(0.000f, 0.941f, 1.000f, 1.00f);
        public static readonly Color OnCyan = new Color(0.000f, 0.125f, 0.133f, 1.00f);
        public static readonly Color SafetyAmber = new Color(1.000f, 0.718f, 0.012f, 1.00f);
        public static readonly Color OnAmber = new Color(0.153f, 0.098f, 0.000f, 1.00f);
        public static readonly Color TextPrimary = new Color(0.875f, 0.886f, 0.922f, 1.00f);
        public static readonly Color TextMuted = new Color(0.725f, 0.792f, 0.796f, 1.00f);
        public static readonly Color DangerRed = new Color(0.851f, 0.220f, 0.161f, 1.00f);
        public static readonly Color SuccessGreen = new Color(0.000f, 0.824f, 0.522f, 1.00f);

        private static Sprite _cyberPanelSprite;
        private static Sprite _cyberButtonSprite;
        private static Sprite _softDarkScrimSprite;
        private static TMP_FontAsset _cachedTmpFont;

        public static TMP_FontAsset GetTmpFont(TMP_FontAsset customOverride = null)
        {
            if (customOverride != null) return customOverride;
            if (_cachedTmpFont != null) return _cachedTmpFont;

            try
            {
                string[] preferredFamilies = { "Segoe UI", "Arial", "Tahoma" };
                for (int i = 0; i < preferredFamilies.Length && _cachedTmpFont == null; i++)
                {
                    _cachedTmpFont = TMP_FontAsset.CreateFontAsset(preferredFamilies[i], "Regular", 48);
                }

                if (_cachedTmpFont != null)
                {
                    _cachedTmpFont.name = "Runtime_CyberLobby_TMPFont";
                }
            }
            catch
            {
                _cachedTmpFont = null;
            }

            if (_cachedTmpFont == null)
            {
                try
                {
                    if (TMP_Settings.instance != null)
                    {
                        _cachedTmpFont = TMP_Settings.defaultFontAsset;
                    }
                }
                catch
                {
                    _cachedTmpFont = null;
                }
            }
            return _cachedTmpFont;
        }

        public static Sprite GetCyberPanelSprite()
        {
            if (_cyberPanelSprite != null && _cyberPanelSprite.texture != null)
            {
                return _cyberPanelSprite;
            }

            const int size = 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Color[] px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool outerBorder = x == 0 || y == 0 || x == size - 1 || y == size - 1;
                    bool innerLine = x == 1 || y == 1 || x == size - 2 || y == size - 2;
                    bool topHighlight = y == size - 3 && x >= 2 && x <= size - 3;

                    if (outerBorder)
                    {
                        px[y * size + x] = new Color(0.24f, 0.30f, 0.34f, 1f);
                    }
                    else if (innerLine)
                    {
                        px[y * size + x] = new Color(0.75f, 0.80f, 0.84f, 0.35f);
                    }
                    else if (topHighlight)
                    {
                        px[y * size + x] = new Color(1f, 1f, 1f, 0.16f);
                    }
                    else
                    {
                        px[y * size + x] = Color.white;
                    }
                }
            }

            tex.SetPixels(px);
            tex.Apply();
            _cyberPanelSprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                32f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(4f, 4f, 4f, 4f));
            return _cyberPanelSprite;
        }

        public static Sprite GetCyberButtonSprite()
        {
            if (_cyberButtonSprite != null && _cyberButtonSprite.texture != null)
            {
                return _cyberButtonSprite;
            }

            const int size = 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Color[] px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool edge = x == 0 || y == 0 || x == size - 1 || y == size - 1;
                    bool topBevel = y == size - 2 && x > 0 && x < size - 1;
                    bool bottomShadow = y == 1 && x > 0 && x < size - 1;

                    if (edge)
                    {
                        px[y * size + x] = new Color(0.14f, 0.18f, 0.22f, 0.95f);
                    }
                    else if (topBevel)
                    {
                        px[y * size + x] = new Color(1f, 1f, 1f, 0.92f);
                    }
                    else if (bottomShadow)
                    {
                        px[y * size + x] = new Color(0.68f, 0.72f, 0.76f, 0.95f);
                    }
                    else
                    {
                        float shade = Mathf.Lerp(0.92f, 1.0f, y / (float)(size - 1));
                        px[y * size + x] = new Color(shade, shade, shade, 1f);
                    }
                }
            }

            tex.SetPixels(px);
            tex.Apply();
            _cyberButtonSprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                32f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(3f, 3f, 3f, 3f));
            return _cyberButtonSprite;
        }

        public static Sprite GetSoftDarkScrimSprite()
        {
            if (_softDarkScrimSprite != null && _softDarkScrimSprite.texture != null)
            {
                return _softDarkScrimSprite;
            }

            const int w = 64;
            const int h = 32;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Color[] px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = y / (float)(h - 1); // 0 bottom, 1 top
                float vertDark = Mathf.Lerp(0.92f, 0.58f, v);
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)(w - 1); // 0 left, 1 right
                    float horizDark = Mathf.Lerp(0.94f, 0.68f, u);
                    float a = Mathf.Clamp01(Mathf.Max(vertDark, horizDark * 0.9f));
                    px[y * w + x] = new Color(0.039f, 0.055f, 0.078f, a);
                }
            }

            tex.SetPixels(px);
            tex.Apply();
            _softDarkScrimSprite = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 64f);
            return _softDarkScrimSprite;
        }

        public static TMP_Text CreateTmpText(
            Transform parent,
            string name,
            string content,
            float fontSize,
            Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft,
            FontStyles style = FontStyles.Bold,
            TMP_FontAsset fontOverride = null,
            bool wrap = false)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset resolvedFont = GetTmpFont(fontOverride);
            if (resolvedFont != null)
            {
                tmp.font = resolvedFont;
            }
            tmp.text = content ?? string.Empty;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.raycastTarget = false;
            return tmp;
        }

        public static Image CreateCyberPanel(
            Transform parent,
            string name,
            Color fillColor,
            Color? accentTopColor = null)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Image img = go.AddComponent<Image>();
            img.sprite = GetCyberPanelSprite();
            img.type = Image.Type.Sliced;
            img.color = fillColor;

            if (accentTopColor.HasValue)
            {
                GameObject accent = new GameObject("TopAccentStrip");
                accent.transform.SetParent(go.transform, false);
                Image strip = accent.AddComponent<Image>();
                strip.color = accentTopColor.Value;
                strip.raycastTarget = false;
                RectTransform srt = strip.rectTransform;
                srt.anchorMin = new Vector2(0f, 1f);
                srt.anchorMax = new Vector2(1f, 1f);
                srt.pivot = new Vector2(0.5f, 1f);
                srt.offsetMin = new Vector2(2f, -3f);
                srt.offsetMax = new Vector2(-2f, 0f);
            }

            return img;
        }

        public static Button CreateCyberButton(
            Transform parent,
            string name,
            string label,
            Color bgColor,
            Color labelColor,
            float fontSize,
            string subtitle = null,
            TMP_FontAsset fontOverride = null)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Image img = go.AddComponent<Image>();
            img.sprite = GetCyberButtonSprite();
            img.type = Image.Type.Sliced;
            img.color = bgColor;

            Button btn = go.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.14f, 1.14f, 1.08f, 1f);
            cb.pressedColor = new Color(0.76f, 0.78f, 0.82f, 1f);
            cb.selectedColor = cb.highlightedColor;
            btn.colors = cb;

            if (string.IsNullOrEmpty(subtitle))
            {
                TMP_Text mainLbl = CreateTmpText(
                    go.transform,
                    "Label",
                    label,
                    fontSize,
                    labelColor,
                    TextAlignmentOptions.Center,
                    FontStyles.Bold,
                    fontOverride);
                StretchFill(mainLbl.rectTransform, 6f, 2f);
            }
            else
            {
                TMP_Text mainLbl = CreateTmpText(
                    go.transform,
                    "Label",
                    label,
                    fontSize,
                    labelColor,
                    TextAlignmentOptions.MidlineLeft,
                    FontStyles.Bold,
                    fontOverride);
                RectTransform mrt = mainLbl.rectTransform;
                mrt.anchorMin = new Vector2(0f, 0.42f);
                mrt.anchorMax = new Vector2(1f, 1f);
                mrt.offsetMin = new Vector2(14f, 0f);
                mrt.offsetMax = new Vector2(-12f, -4f);

                Color subCol = new Color(labelColor.r, labelColor.g, labelColor.b, 0.78f);
                TMP_Text subLbl = CreateTmpText(
                    go.transform,
                    "Subtitle",
                    subtitle,
                    Mathf.Max(9f, fontSize - 7f),
                    subCol,
                    TextAlignmentOptions.MidlineLeft,
                    FontStyles.Normal,
                    fontOverride);
                RectTransform srt = subLbl.rectTransform;
                srt.anchorMin = new Vector2(0f, 0f);
                srt.anchorMax = new Vector2(1f, 0.44f);
                srt.offsetMin = new Vector2(14f, 4f);
                srt.offsetMax = new Vector2(-12f, 0f);
            }

            return btn;
        }

        public static TMP_Text GetPrimaryLabel(Button btn)
        {
            if (btn == null) return null;
            Transform lblTr = btn.transform.Find("Label");
            if (lblTr != null)
            {
                TMP_Text direct = lblTr.GetComponent<TMP_Text>();
                if (direct != null) return direct;
            }
            return btn.GetComponentInChildren<TMP_Text>(true);
        }

        public static SkinCardWidgets CreateSkinCard(
            Transform parent,
            DriverBrandSkin brand,
            float x,
            float y,
            float w,
            float h,
            Action<DriverBrandSkin> onSelect,
            TMP_FontAsset fontOverride = null)
        {
            Image cardBg = CreateCyberPanel(parent, $"SkinCard_{brand}", SurfaceContainer);
            PlaceTopLeft(cardBg.rectTransform, x, y, w, h);

            // Left vertical brand accent bar
            GameObject stripObj = new GameObject("BrandColorStrip");
            stripObj.transform.SetParent(cardBg.transform, false);
            Image stripImg = stripObj.AddComponent<Image>();
            stripImg.raycastTarget = false;
            PlaceTopLeft(stripImg.rectTransform, 3f, -3f, 6f, h - 6f);

            // 16:9 thumbnail frame on left side of horizontal card
            float thumbH = h - 18f;
            float thumbW = Mathf.Round(thumbH * 1.65f);
            Image thumbFrame = CreateCyberPanel(cardBg.transform, "PreviewFrame", SurfaceLowest);
            PlaceTopLeft(thumbFrame.rectTransform, 16f, -9f, thumbW, thumbH);

            GameObject iconObj = new GameObject("BikePreviewIcon");
            iconObj.transform.SetParent(thumbFrame.transform, false);
            Image iconImg = iconObj.AddComponent<Image>();
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            StretchFill(iconImg.rectTransform, 2f, 2f);

            float textX = 16f + thumbW + 14f;
            float textW = Mathf.Max(140f, w - textX - 150f);

            TMP_Text nameTxt = CreateTmpText(
                cardBg.transform, "BrandName", brand.ToString(), 20f, TextPrimary,
                TextAlignmentOptions.MidlineLeft, FontStyles.Bold, fontOverride);
            PlaceTopLeft(nameTxt.rectTransform, textX, -14f, textW, 28f);

            TMP_Text tagTxt = CreateTmpText(
                cardBg.transform, "BrandTagline", "", 13f, TextMuted,
                TextAlignmentOptions.TopLeft, FontStyles.Normal, fontOverride, wrap: true);
            PlaceTopLeft(tagTxt.rectTransform, textX, -46f, textW, h - 54f);

            Button selectBtn = CreateCyberButton(
                cardBg.transform, $"SelectSkinBtn_{brand}", "CHỌN SKIN",
                SurfaceHigh, TextPrimary, 13f, null, fontOverride);
            PlaceTopRight(selectBtn.GetComponent<RectTransform>(), -14f, -(h - 40f) * 0.5f, 126f, 40f);
            selectBtn.onClick.AddListener(() => onSelect?.Invoke(brand));

            return new SkinCardWidgets
            {
                Brand = brand,
                CardBg = cardBg,
                HeaderStrip = stripImg,
                PreviewIcon = iconImg,
                NameText = nameTxt,
                TaglineText = tagTxt,
                SelectBtnLabel = GetPrimaryLabel(selectBtn),
                SelectBtnImg = selectBtn.GetComponent<Image>(),
                SelectBtn = selectBtn
            };
        }

        public static MapTabWidgets CreateMapTab(
            Transform parent,
            int index,
            string mapId,
            string displayName,
            float x,
            float w,
            float h,
            Action<int> onClick,
            TMP_FontAsset fontOverride = null)
        {
            Button tabBtn = CreateCyberButton(
                parent, $"MapTab_{index}", displayName, SurfaceHigh, TextPrimary, 11f, null, fontOverride);
            PlaceTopLeft(tabBtn.GetComponent<RectTransform>(), x, 0f, w, h);
            tabBtn.onClick.AddListener(() => onClick?.Invoke(index));

            return new MapTabWidgets
            {
                MapId = mapId,
                Button = tabBtn,
                Bg = tabBtn.GetComponent<Image>(),
                Label = GetPrimaryLabel(tabBtn)
            };
        }

        public static void StretchFill(RectTransform rt, float padX = 0f, float padY = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padX, padY);
            rt.offsetMax = new Vector2(-padX, -padY);
        }

        public static void PlaceTop(RectTransform rt, float yOffset, float w, float h)
        {
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, yOffset);
            rt.sizeDelta = new Vector2(w, h);
        }

        public static void PlaceTopLeft(RectTransform rt, float x, float yOffset, float w, float h)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, yOffset);
            rt.sizeDelta = new Vector2(w, h);
        }

        public static void PlaceTopRight(RectTransform rt, float xOffset, float yOffset, float w, float h)
        {
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(xOffset, yOffset);
            rt.sizeDelta = new Vector2(w, h);
        }

        public static void PlaceBottomStretch(RectTransform rt, float height)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = new Vector2(0f, height);
        }
    }
}
