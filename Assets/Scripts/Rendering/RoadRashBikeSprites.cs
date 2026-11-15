using System.Collections.Generic;
using UnityEngine;

namespace RacingGame
{
    public enum BikePose
    {
        Upright,
        LeanLeftLight,
        LeanLeftDeep,
        LeanRightLight,
        LeanRightDeep,
        KickLeft,
        KickRight,
        HitStagger
    }

    public sealed class BikeSpriteSet
    {
        public Sprite Upright0;
        public Sprite Upright1;
        public Sprite LeanLeft1;
        public Sprite LeanLeft2;
        public Sprite LeanRight1;
        public Sprite LeanRight2;
        public Sprite KickLeft;
        public Sprite KickRight;
        public Sprite HitStagger;
    }

    /// <summary>
    /// Procedurally generates crisp 64x64 PS1 Road Rash-inspired behind-the-bike sprites
    /// with distinct rider helmet, shaded jacket/fairing, boots, fat rear tire, and dual chrome exhausts.
    /// Caches one BikeSpriteSet per jacket/fairing color so helmet, tire, and chrome stay true-colored.
    /// </summary>
    public static class RoadRashBikeSprites
    {
        private const int Size = 64;
        private static readonly Dictionary<ulong, BikeSpriteSet> Cache = new Dictionary<ulong, BikeSpriteSet>();
        private static Sprite _shadowSprite;
        private static Sprite _tailLightSprite;

        public static Sprite GetShadowSprite()
        {
            if (_shadowSprite != null) return _shadowSprite;

            const int w = 32;
            const int h = 16;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = (x - (w - 1) * 0.5f) / (w * 0.48f);
                    float dy = (y - (h - 1) * 0.5f) / (h * 0.48f);
                    float d2 = dx * dx + dy * dy;
                    float alpha = Mathf.Clamp01(1f - d2);
                    alpha = alpha * alpha * 0.85f;
                    tex.SetPixel(x, y, new Color(0.03f, 0.04f, 0.06f, alpha));
                }
            }

            tex.Apply();
            _shadowSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f);
            return _shadowSprite;
        }

        public static Sprite GetTailLightSprite()
        {
            if (_tailLightSprite != null) return _tailLightSprite;

            const int w = 16;
            const int h = 16;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = (x - 7.5f) / 7.5f;
                    float dy = (y - 7.5f) / 7.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            }

            tex.Apply();
            _tailLightSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 16f);
            return _tailLightSprite;
        }

        public static BikeSpriteSet GetOrCreateSet(Color primaryColor)
        {
            return GetOrCreateSet(
                primaryColor,
                new Color(0.92f, 0.93f, 0.96f),
                new Color(0.96f, 0.97f, 0.99f));
        }

        public static BikeSpriteSet GetOrCreateSet(BikeSkinData skin)
        {
            if (skin == null)
            {
                return GetOrCreateSet(RoadRashVisualTheme.PlayerJacketBlue);
            }
            return GetOrCreateSet(skin.PrimaryColor, skin.SecondaryColor, skin.HelmetColor);
        }

        public static BikeSpriteSet GetOrCreateSet(Color primaryColor, Color secondaryColor, Color helmetColor)
        {
            Color32 p = primaryColor;
            Color32 s = secondaryColor;
            Color32 h = helmetColor;
            ulong key = ((ulong)p.r << 56) | ((ulong)p.g << 48) | ((ulong)p.b << 40)
                      | ((ulong)s.r << 32) | ((ulong)s.g << 24) | ((ulong)s.b << 16)
                      | ((ulong)h.r << 8) | h.b;

            if (Cache.TryGetValue(key, out BikeSpriteSet existing) && existing != null && existing.Upright0 != null)
            {
                return existing;
            }

            BikeSpriteSet set = new BikeSpriteSet
            {
                Upright0 = BuildBikeSprite(primaryColor, secondaryColor, helmetColor, 0f, 0, false, 0),
                Upright1 = BuildBikeSprite(primaryColor, secondaryColor, helmetColor, 0f, 0, true, 0),
                LeanLeft1 = BuildBikeSprite(primaryColor, secondaryColor, helmetColor, -0.45f, 0, false, 0),
                LeanLeft2 = BuildBikeSprite(primaryColor, secondaryColor, helmetColor, -0.95f, 0, true, 0),
                LeanRight1 = BuildBikeSprite(primaryColor, secondaryColor, helmetColor, 0.45f, 0, false, 0),
                LeanRight2 = BuildBikeSprite(primaryColor, secondaryColor, helmetColor, 0.95f, 0, true, 0),
                KickLeft = BuildBikeSprite(primaryColor, secondaryColor, helmetColor, 0.28f, -1, false, 0),
                KickRight = BuildBikeSprite(primaryColor, secondaryColor, helmetColor, -0.28f, 1, false, 0),
                HitStagger = BuildBikeSprite(primaryColor, secondaryColor, helmetColor, 0.55f, 0, false, 1)
            };

            Cache[key] = set;
            return set;
        }

        public static Sprite SelectPoseSprite(BikeSpriteSet set, BikePose pose, int animFrame)
        {
            if (set == null) return null;
            return pose switch
            {
                BikePose.LeanLeftLight => set.LeanLeft1,
                BikePose.LeanLeftDeep => set.LeanLeft2,
                BikePose.LeanRightLight => set.LeanRight1,
                BikePose.LeanRightDeep => set.LeanRight2,
                BikePose.KickLeft => set.KickLeft,
                BikePose.KickRight => set.KickRight,
                BikePose.HitStagger => set.HitStagger,
                _ => (animFrame & 1) == 0 ? set.Upright0 : set.Upright1
            };
        }

        private static Sprite BuildBikeSprite(Color primary, Color secondary, Color helmet, float leanAmount, int kickDir, bool altWheelFrame, int stagger)
        {
            Texture2D tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Color[] pixels = new Color[Size * Size];
            Color clear = new Color(0f, 0f, 0f, 0f);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

            Color outline = new Color(0.06f, 0.07f, 0.09f);
            Color tireDark = new Color(0.11f, 0.12f, 0.14f);
            Color tireMid = new Color(0.18f, 0.20f, 0.23f);
            Color tireTread = altWheelFrame ? new Color(0.24f, 0.26f, 0.29f) : new Color(0.14f, 0.15f, 0.17f);
            Color chromeHi = new Color(0.86f, 0.90f, 0.94f);
            Color chromeMid = new Color(0.60f, 0.64f, 0.70f);
            Color chromeDark = new Color(0.32f, 0.35f, 0.40f);

            Color suitBase = primary;
            Color suitLight = Color.Lerp(primary, Color.white, 0.28f);
            Color suitDark = Color.Lerp(primary, Color.black, 0.38f);
            Color whitePanel = secondary;
            Color whiteShade = Color.Lerp(secondary, Color.black, 0.24f);
            Color helmetWhite = helmet;
            Color helmetShade = Color.Lerp(helmet, Color.black, 0.24f);
            Color visorDark = new Color(0.12f, 0.15f, 0.21f);
            Color tailRed = new Color(0.95f, 0.16f, 0.12f);
            Color bootWhite = new Color(0.90f, 0.91f, 0.94f);
            Color bootSole = new Color(0.16f, 0.16f, 0.18f);

            float cx = 31.5f;

            void SetPx(int x, int y, Color c)
            {
                if (x >= 0 && x < Size && y >= 0 && y < Size)
                {
                    pixels[y * Size + x] = c;
                }
            }

            void DrawEllipse(float ex, float ey, float rx, float ry, Color fill, Color border, bool shadeSide = false, Color? shadeCol = null)
            {
                int minX = Mathf.FloorToInt(ex - rx - 1.5f);
                int maxX = Mathf.CeilToInt(ex + rx + 1.5f);
                int minY = Mathf.FloorToInt(ey - ry - 1.5f);
                int maxY = Mathf.CeilToInt(ey + ry + 1.5f);

                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        float nx = (x - ex) / rx;
                        float ny = (y - ey) / ry;
                        float d = nx * nx + ny * ny;
                        if (d <= 1.0f)
                        {
                            Color c = fill;
                            if (shadeSide && shadeCol.HasValue && (nx > 0.25f || ny < -0.35f))
                            {
                                c = shadeCol.Value;
                            }
                            SetPx(x, y, c);
                        }
                        else if (d <= 1.32f)
                        {
                            SetPx(x, y, border);
                        }
                    }
                }
            }

            void DrawCapsule(Vector2 a, Vector2 b, float radius, Color fill, Color border, Color? highlight = null)
            {
                int minX = Mathf.FloorToInt(Mathf.Min(a.x, b.x) - radius - 2f);
                int maxX = Mathf.CeilToInt(Mathf.Max(a.x, b.x) + radius + 2f);
                int minY = Mathf.FloorToInt(Mathf.Min(a.y, b.y) - radius - 2f);
                int maxY = Mathf.CeilToInt(Mathf.Max(a.y, b.y) + radius + 2f);

                Vector2 ab = b - a;
                float lenSq = Mathf.Max(0.0001f, ab.sqrMagnitude);

                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        Vector2 p = new Vector2(x, y);
                        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
                        Vector2 closest = a + ab * t;
                        float dist = Vector2.Distance(p, closest);
                        if (dist <= radius)
                        {
                            Color c = fill;
                            if (highlight.HasValue && (p.y > closest.y + radius * 0.2f || p.x < closest.x - radius * 0.2f))
                            {
                                c = highlight.Value;
                            }
                            SetPx(x, y, c);
                        }
                        else if (dist <= radius + 1.05f)
                        {
                            SetPx(x, y, border);
                        }
                    }
                }
            }

            float LeanX(float y)
            {
                float hNorm = Mathf.Clamp01((y - 6f) / 48f);
                return cx + leanAmount * hNorm * 11f + stagger * Mathf.Sin(y * 0.3f) * 1.4f;
            }

            // 1. Handlebars & Front Fairing Edges
            float barY = 39f;
            float barX = LeanX(barY);
            DrawCapsule(new Vector2(barX - 11f, barY), new Vector2(barX + 11f, barY), 1.3f, chromeMid, outline, chromeHi);
            DrawEllipse(barX - 11.5f, barY, 1.8f, 1.8f, tireDark, outline);
            DrawEllipse(barX + 11.5f, barY, 1.8f, 1.8f, tireDark, outline);

            // 2. Dual Chrome Exhaust Pipes on the right rear
            float exBaseX = LeanX(12f) + 6.2f;
            DrawCapsule(new Vector2(exBaseX, 8f), new Vector2(exBaseX + 1.4f, 19f), 1.7f, chromeMid, outline, chromeHi);
            DrawCapsule(new Vector2(exBaseX + 2.7f, 9f), new Vector2(exBaseX + 3.8f, 18f), 1.5f, chromeDark, outline, chromeMid);
            DrawEllipse(exBaseX + 0.2f, 7.5f, 1.6f, 1.2f, outline, outline);

            // Left swingarm/chain guard detail
            float leftArmX = LeanX(12f) - 5.6f;
            DrawCapsule(new Vector2(leftArmX, 9f), new Vector2(leftArmX - 0.8f, 18f), 1.4f, chromeDark, outline, chromeMid);

            // 3. Fat Rear Tire (like PS1 Road Rash)
            for (int y = 3; y <= 21; y++)
            {
                float tx = LeanX(y);
                float tNorm = (y - 3f) / 18f;
                float halfW = Mathf.Lerp(4.2f, 5.4f, Mathf.Sin(tNorm * Mathf.PI));
                for (int x = Mathf.FloorToInt(tx - halfW - 1.2f); x <= Mathf.CeilToInt(tx + halfW + 1.2f); x++)
                {
                    float dx = Mathf.Abs(x - tx);
                    if (dx <= halfW)
                    {
                        bool centerStrip = dx < 1.3f && ((y + (altWheelFrame ? 1 : 0)) % 3 == 0);
                        Color tc = centerStrip ? tireTread : (dx > halfW * 0.65f ? tireDark : tireMid);
                        SetPx(x, y, tc);
                    }
                    else if (dx <= halfW + 1.1f)
                    {
                        SetPx(x, y, outline);
                    }
                }
            }

            // 4. Rear Fender, Tail Fairing & Tail Light
            float tailX = LeanX(24f);
            DrawEllipse(tailX, 23.5f, 6.2f, 4.2f, suitBase, outline, true, suitDark);
            DrawEllipse(tailX, 24.8f, 4.4f, 2.2f, whitePanel, suitDark);
            // Red tail light block
            DrawEllipse(tailX, 20.8f, 3.2f, 1.6f, tailRed, outline);

            // 5. Legs & Boots (normal straddle OR extended Road Rash kick!)
            float hipY = 28f;
            float hipX = LeanX(hipY);

            // Left Leg
            if (kickDir < 0)
            {
                Vector2 knee = new Vector2(hipX - 11f, hipY - 2f);
                Vector2 foot = new Vector2(hipX - 21f, hipY - 4f);
                DrawCapsule(new Vector2(hipX - 3f, hipY), knee, 3.1f, suitBase, outline, suitLight);
                DrawCapsule(knee, foot, 2.5f, suitDark, outline, suitBase);
                DrawEllipse(foot.x - 1.5f, foot.y, 3.3f, 2.3f, bootWhite, outline, true, bootSole);
            }
            else
            {
                Vector2 knee = new Vector2(hipX - 8.2f, hipY - 3.5f);
                Vector2 foot = new Vector2(LeanX(14f) - 6.6f, 14f);
                DrawCapsule(new Vector2(hipX - 3f, hipY), knee, 3.1f, suitBase, outline, suitLight);
                DrawCapsule(knee, foot, 2.4f, suitDark, outline, suitBase);
                DrawEllipse(foot.x - 0.5f, foot.y - 1f, 2.5f, 2.1f, bootWhite, outline, true, bootSole);
            }

            // Right Leg
            if (kickDir > 0)
            {
                Vector2 knee = new Vector2(hipX + 11f, hipY - 2f);
                Vector2 foot = new Vector2(hipX + 21f, hipY - 4f);
                DrawCapsule(new Vector2(hipX + 3f, hipY), knee, 3.1f, suitBase, outline, suitLight);
                DrawCapsule(knee, foot, 2.5f, suitDark, outline, suitBase);
                DrawEllipse(foot.x + 1.5f, foot.y, 3.3f, 2.3f, bootWhite, outline, true, bootSole);
            }
            else
            {
                Vector2 knee = new Vector2(hipX + 8.2f, hipY - 3.5f);
                Vector2 foot = new Vector2(LeanX(14f) + 6.6f, 14f);
                DrawCapsule(new Vector2(hipX + 3f, hipY), knee, 3.1f, suitBase, outline, suitLight);
                DrawCapsule(knee, foot, 2.4f, suitDark, outline, suitBase);
                DrawEllipse(foot.x + 0.5f, foot.y - 1f, 2.5f, 2.1f, bootWhite, outline, true, bootSole);
            }

            // 6. Rider Torso / Racing Jacket (white back V-panel + colored shoulders)
            float waistX = LeanX(30f);
            float chestX = LeanX(40f);
            DrawCapsule(new Vector2(waistX, 29f), new Vector2(chestX, 41f), 7.6f, suitBase, outline, suitLight);
            // Shaded white back panel (signature PS1 Road Rash jacket detail)
            DrawEllipse(chestX, 37.5f, 4.8f, 5.0f, whitePanel, suitDark, true, whiteShade);
            // Dark belt / waist crease
            DrawCapsule(new Vector2(waistX - 4.5f, 29.5f), new Vector2(waistX + 4.5f, 29.5f), 1.1f, tireDark, outline);

            // 7. Rider Arms gripping handlebars
            float shoulderY = 41.5f;
            float shoulderX = LeanX(shoulderY);
            Vector2 lShoulder = new Vector2(shoulderX - 7.5f, shoulderY);
            Vector2 rShoulder = new Vector2(shoulderX + 7.5f, shoulderY);
            Vector2 lElbow = new Vector2(barX - 11.5f, barY + 1.5f);
            Vector2 rElbow = new Vector2(barX + 11.5f, barY + 1.5f);

            if (kickDir < 0) lElbow += new Vector2(-1.5f, 2.5f);
            if (kickDir > 0) rElbow += new Vector2(1.5f, 2.5f);

            DrawCapsule(lShoulder, lElbow, 2.6f, suitBase, outline, suitLight);
            DrawCapsule(rShoulder, rElbow, 2.6f, suitDark, outline, suitBase);
            DrawEllipse(lElbow.x, lElbow.y - 1f, 2.0f, 1.9f, tireDark, outline);
            DrawEllipse(rElbow.x, rElbow.y - 1f, 2.0f, 1.9f, tireDark, outline);

            // 8. White Full-Face Helmet
            float headY = 49.5f;
            float headX = LeanX(headY);
            DrawEllipse(headX, 44.5f, 3.0f, 2.0f, visorDark, outline); // neck collar
            DrawEllipse(headX, headY, 5.4f, 6.0f, helmetWhite, outline, true, helmetShade);
            // Subtle racing stripe & lower rear vent on helmet
            DrawCapsule(new Vector2(headX, headY + 2.5f), new Vector2(headX, headY + 5.0f), 0.7f, suitBase, suitDark);
            DrawCapsule(new Vector2(headX - 2.6f, headY - 3.6f), new Vector2(headX + 2.6f, headY - 3.6f), 0.8f, visorDark, outline);

            tex.SetPixels(pixels);
            tex.Apply();

            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.08f), 32f);
        }
    }
}
