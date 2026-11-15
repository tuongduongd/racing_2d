using UnityEngine;

namespace RacingGame
{
    public enum RoadsidePropKind
    {
        UtilityPole,
        FencePost,
        ChevronLeft,
        ChevronRight,
        Tree,
        MilePost,
        StreetLamp,
        Billboard,
        PalmTree,
        TrafficCone,
        OilBarrel,
        RoadBarrier,
        OilSlick,
        PoliceSirenBeacon
    }

    /// <summary>
    /// Generates crisp retro roadside prop sprites, urban environment decorations,
    /// road obstacles (cones, barrels, barriers, oil slicks), and police beacon lights.
    /// </summary>
    public static class RoadRashPropSprites
    {
        private static Sprite _utilityPole;
        private static Sprite _fencePost;
        private static Sprite _chevronLeft;
        private static Sprite _chevronRight;
        private static Sprite _tree;
        private static Sprite _milePost;
        private static Sprite _streetLamp;
        private static Sprite _billboard;
        private static Sprite _palmTree;
        private static Sprite _trafficCone;
        private static Sprite _oilBarrel;
        private static Sprite _roadBarrier;
        private static Sprite _oilSlick;
        private static Sprite _policeSirenBeacon;

        public static Sprite GetPropSprite(RoadsidePropKind kind)
        {
            switch (kind)
            {
                case RoadsidePropKind.UtilityPole:
                    if (_utilityPole == null) _utilityPole = CreateUtilityPoleSprite();
                    return _utilityPole;
                case RoadsidePropKind.FencePost:
                    if (_fencePost == null) _fencePost = CreateFencePostSprite();
                    return _fencePost;
                case RoadsidePropKind.ChevronLeft:
                    if (_chevronLeft == null) _chevronLeft = CreateChevronSprite(-1);
                    return _chevronLeft;
                case RoadsidePropKind.ChevronRight:
                    if (_chevronRight == null) _chevronRight = CreateChevronSprite(1);
                    return _chevronRight;
                case RoadsidePropKind.Tree:
                    if (_tree == null) _tree = CreateTreeSprite();
                    return _tree;
                case RoadsidePropKind.StreetLamp:
                    if (_streetLamp == null) _streetLamp = CreateStreetLampSprite();
                    return _streetLamp;
                case RoadsidePropKind.Billboard:
                    if (_billboard == null) _billboard = CreateBillboardSprite();
                    return _billboard;
                case RoadsidePropKind.PalmTree:
                    if (_palmTree == null) _palmTree = CreatePalmTreeSprite();
                    return _palmTree;
                case RoadsidePropKind.TrafficCone:
                    if (_trafficCone == null) _trafficCone = CreateTrafficConeSprite();
                    return _trafficCone;
                case RoadsidePropKind.OilBarrel:
                    if (_oilBarrel == null) _oilBarrel = CreateOilBarrelSprite();
                    return _oilBarrel;
                case RoadsidePropKind.RoadBarrier:
                    if (_roadBarrier == null) _roadBarrier = CreateRoadBarrierSprite();
                    return _roadBarrier;
                case RoadsidePropKind.OilSlick:
                    if (_oilSlick == null) _oilSlick = CreateOilSlickSprite();
                    return _oilSlick;
                case RoadsidePropKind.PoliceSirenBeacon:
                    if (_policeSirenBeacon == null) _policeSirenBeacon = CreatePoliceBeaconSprite();
                    return _policeSirenBeacon;
                default:
                    if (_milePost == null) _milePost = CreateMilePostSprite();
                    return _milePost;
            }
        }

        private static Sprite CreateUtilityPoleSprite()
        {
            const int w = 40;
            const int h = 96;
            Texture2D tex = NewPointTex(w, h);
            Color wood = new Color(0.32f, 0.21f, 0.12f);
            Color woodHi = new Color(0.45f, 0.31f, 0.19f);
            Color woodDark = new Color(0.18f, 0.11f, 0.06f);
            Color insulator = new Color(0.76f, 0.84f, 0.88f);

            FillRect(tex, 18, 0, 21, 93, wood);
            FillRect(tex, 18, 0, 18, 93, woodHi);
            FillRect(tex, 21, 0, 21, 93, woodDark);

            FillRect(tex, 4, 80, 35, 83, wood);
            FillRect(tex, 4, 83, 35, 83, woodHi);
            FillRect(tex, 4, 80, 35, 80, woodDark);

            FillRect(tex, 8, 69, 31, 71, wood);
            FillRect(tex, 8, 69, 31, 69, woodDark);

            for (int i = 0; i < 6; i++)
            {
                tex.SetPixel(13 + i, 74 + i, woodDark);
                tex.SetPixel(26 - i, 74 + i, woodDark);
            }

            FillRect(tex, 6, 84, 7, 86, insulator);
            FillRect(tex, 12, 84, 13, 86, insulator);
            FillRect(tex, 26, 84, 27, 86, insulator);
            FillRect(tex, 32, 84, 33, 86, insulator);

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 28f);
        }

        private static Sprite CreateFencePostSprite()
        {
            const int w = 24;
            const int h = 36;
            Texture2D tex = NewPointTex(w, h);
            Color wood = new Color(0.46f, 0.33f, 0.20f);
            Color woodHi = new Color(0.60f, 0.45f, 0.28f);
            Color woodDark = new Color(0.26f, 0.18f, 0.10f);

            FillRect(tex, 9, 0, 14, 31, wood);
            FillRect(tex, 9, 0, 10, 31, woodHi);
            FillRect(tex, 13, 0, 14, 31, woodDark);
            FillRect(tex, 10, 32, 13, 33, woodHi);

            FillRect(tex, 0, 21, 23, 24, wood);
            FillRect(tex, 0, 24, 23, 24, woodHi);
            FillRect(tex, 0, 11, 23, 14, wood);
            FillRect(tex, 0, 11, 23, 11, woodDark);

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 32f);
        }

        private static Sprite CreateChevronSprite(int dir)
        {
            const int w = 32;
            const int h = 48;
            Texture2D tex = NewPointTex(w, h);
            Color metal = new Color(0.48f, 0.52f, 0.56f);
            Color metalDark = new Color(0.24f, 0.26f, 0.30f);
            Color yellow = new Color(0.96f, 0.78f, 0.12f);
            Color black = new Color(0.10f, 0.11f, 0.13f);

            FillRect(tex, 14, 0, 17, 25, metal);
            FillRect(tex, 17, 0, 17, 25, metalDark);

            FillRect(tex, 3, 23, 28, 44, black);
            FillRect(tex, 5, 25, 26, 42, yellow);

            for (int y = 27; y <= 40; y++)
            {
                int dy = Mathf.Abs(y - 33);
                int tipX = dir > 0 ? (22 - dy) : (9 + dy);
                for (int k = 0; k < 4; k++)
                {
                    int x = dir > 0 ? tipX - k : tipX + k;
                    if (x >= 6 && x <= 25) tex.SetPixel(x, y, black);
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 30f);
        }

        private static Sprite CreateTreeSprite()
        {
            const int w = 48;
            const int h = 64;
            Texture2D tex = NewPointTex(w, h);
            Color trunk = new Color(0.35f, 0.22f, 0.12f);
            Color trunkDark = new Color(0.21f, 0.13f, 0.07f);
            Color leafDark = new Color(0.15f, 0.30f, 0.12f);
            Color leafMid = new Color(0.25f, 0.44f, 0.17f);
            Color leafHi = new Color(0.40f, 0.58f, 0.23f);

            FillRect(tex, 20, 0, 27, 22, trunk);
            FillRect(tex, 25, 0, 27, 22, trunkDark);

            void CanopyBlob(float cx, float cy, float rx, float ry)
            {
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        float dx = (x - cx) / rx;
                        float dy = (y - cy) / ry;
                        float d2 = dx * dx + dy * dy;
                        if (d2 <= 1f)
                        {
                            Color c = (dx < -0.15f && dy > 0.1f) ? leafHi : (dx > 0.25f || dy < -0.25f ? leafDark : leafMid);
                            tex.SetPixel(x, y, c);
                        }
                    }
                }
            }

            CanopyBlob(24f, 34f, 18f, 14f);
            CanopyBlob(18f, 41f, 13f, 11f);
            CanopyBlob(30f, 40f, 13f, 11f);
            CanopyBlob(24f, 49f, 12f, 10f);

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 26f);
        }

        private static Sprite CreateMilePostSprite()
        {
            const int w = 16;
            const int h = 32;
            Texture2D tex = NewPointTex(w, h);
            Color post = new Color(0.84f, 0.86f, 0.88f);
            Color postShade = new Color(0.58f, 0.61f, 0.65f);
            Color reflector = new Color(0.95f, 0.42f, 0.10f);

            FillRect(tex, 5, 0, 10, 28, post);
            FillRect(tex, 9, 0, 10, 28, postShade);
            FillRect(tex, 4, 21, 11, 25, reflector);

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 32f);
        }

        private static Sprite CreateStreetLampSprite()
        {
            const int w = 44;
            const int h = 96;
            Texture2D tex = NewPointTex(w, h);
            Color steel = new Color(0.36f, 0.40f, 0.45f);
            Color steelHi = new Color(0.56f, 0.62f, 0.68f);
            Color steelDark = new Color(0.20f, 0.22f, 0.26f);
            Color lampGlow = new Color(1.00f, 0.92f, 0.55f);
            Color lampCore = new Color(1.00f, 0.98f, 0.86f);

            // Base & vertical shaft
            FillRect(tex, 16, 0, 25, 10, steelDark);
            FillRect(tex, 19, 10, 22, 86, steel);
            FillRect(tex, 19, 10, 19, 86, steelHi);

            // Curved overhang arm toward road
            FillRect(tex, 19, 84, 37, 87, steel);
            FillRect(tex, 33, 81, 41, 84, steelDark);
            FillRect(tex, 34, 78, 40, 81, lampGlow);
            FillRect(tex, 35, 79, 39, 80, lampCore);

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 28f);
        }

        private static Sprite CreateBillboardSprite()
        {
            const int w = 64;
            const int h = 64;
            Texture2D tex = NewPointTex(w, h);
            Color pillar = new Color(0.28f, 0.31f, 0.36f);
            Color frame = new Color(0.14f, 0.16f, 0.20f);
            Color cyan = new Color(0.00f, 0.72f, 0.71f);
            Color orange = new Color(0.93f, 0.30f, 0.18f);
            Color green = new Color(0.00f, 0.69f, 0.31f);
            Color yellow = new Color(0.96f, 0.78f, 0.18f);

            // Two support legs
            FillRect(tex, 14, 0, 18, 26, pillar);
            FillRect(tex, 45, 0, 49, 26, pillar);

            // Billboard frame
            FillRect(tex, 4, 24, 59, 58, frame);
            FillRect(tex, 7, 27, 29, 41, cyan);
            FillRect(tex, 30, 27, 56, 41, orange);
            FillRect(tex, 7, 42, 33, 55, green);
            FillRect(tex, 34, 42, 56, 55, yellow);

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 26f);
        }

        private static Sprite CreatePalmTreeSprite()
        {
            const int w = 52;
            const int h = 76;
            Texture2D tex = NewPointTex(w, h);
            Color trunk = new Color(0.44f, 0.31f, 0.18f);
            Color trunkDark = new Color(0.28f, 0.19f, 0.10f);
            Color frond = new Color(0.18f, 0.52f, 0.22f);
            Color frondHi = new Color(0.32f, 0.68f, 0.30f);

            for (int y = 0; y < 48; y++)
            {
                int cx = 26 + Mathf.RoundToInt(Mathf.Sin(y * 0.06f) * 3f);
                FillRect(tex, cx - 2, y, cx + 2, y, (y % 4 == 0) ? trunkDark : trunk);
            }

            // Palm fronds radiating from crown (28, 50)
            Vector2 crown = new Vector2(28f, 50f);
            Vector2[] tips =
            {
                new Vector2(6f, 44f),
                new Vector2(8f, 58f),
                new Vector2(20f, 70f),
                new Vector2(36f, 70f),
                new Vector2(47f, 58f),
                new Vector2(48f, 43f)
            };

            for (int i = 0; i < tips.Length; i++)
            {
                for (int s = 0; s <= 24; s++)
                {
                    float t = s / 24f;
                    Vector2 p = Vector2.Lerp(crown, tips[i], t);
                    p.y += Mathf.Sin(t * Mathf.PI) * 4f;
                    FillRect(tex, Mathf.RoundToInt(p.x) - 1, Mathf.RoundToInt(p.y) - 1,
                        Mathf.RoundToInt(p.x) + 1, Mathf.RoundToInt(p.y) + 1,
                        t < 0.6f ? frondHi : frond);
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 26f);
        }

        private static Sprite CreateTrafficConeSprite()
        {
            const int w = 28;
            const int h = 32;
            Texture2D tex = NewPointTex(w, h);
            Color baseDark = new Color(0.16f, 0.17f, 0.20f);
            Color orange = new Color(0.98f, 0.42f, 0.08f);
            Color orangeShade = new Color(0.76f, 0.28f, 0.04f);
            Color whiteStripe = new Color(0.96f, 0.97f, 0.99f);

            FillRect(tex, 4, 0, 23, 4, baseDark);

            for (int y = 5; y <= 27; y++)
            {
                float t = (y - 5f) / 22f;
                int halfW = Mathf.RoundToInt(Mathf.Lerp(7f, 2f, t));
                bool isReflective = (y >= 11 && y <= 14) || (y >= 19 && y <= 21);
                for (int x = 14 - halfW; x <= 14 + halfW; x++)
                {
                    Color c = isReflective ? whiteStripe : (x > 14 ? orangeShade : orange);
                    tex.SetPixel(x, y, c);
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 32f);
        }

        private static Sprite CreateOilBarrelSprite()
        {
            const int w = 32;
            const int h = 40;
            Texture2D tex = NewPointTex(w, h);
            Color red = new Color(0.84f, 0.18f, 0.14f);
            Color redDark = new Color(0.54f, 0.10f, 0.08f);
            Color yellowBand = new Color(0.95f, 0.78f, 0.14f);
            Color rim = new Color(0.24f, 0.26f, 0.30f);

            FillRect(tex, 6, 2, 25, 36, red);
            FillRect(tex, 19, 2, 25, 36, redDark);
            FillRect(tex, 5, 13, 26, 24, yellowBand);
            FillRect(tex, 5, 0, 26, 2, rim);
            FillRect(tex, 5, 36, 26, 38, rim);

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 32f);
        }

        private static Sprite CreateRoadBarrierSprite()
        {
            const int w = 48;
            const int h = 32;
            Texture2D tex = NewPointTex(w, h);
            Color leg = new Color(0.26f, 0.28f, 0.32f);
            Color yellow = new Color(0.96f, 0.78f, 0.12f);
            Color black = new Color(0.12f, 0.13f, 0.16f);

            FillRect(tex, 7, 0, 11, 14, leg);
            FillRect(tex, 36, 0, 40, 14, leg);

            for (int y = 14; y <= 27; y++)
            {
                for (int x = 2; x <= 45; x++)
                {
                    bool stripe = (((x + y) / 6) % 2) == 0;
                    tex.SetPixel(x, y, stripe ? yellow : black);
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 32f);
        }

        private static Sprite CreateOilSlickSprite()
        {
            const int w = 48;
            const int h = 20;
            Texture2D tex = NewPointTex(w, h);
            Color oilDark = new Color(0.06f, 0.07f, 0.10f, 0.92f);
            Color oilSheen = new Color(0.24f, 0.18f, 0.36f, 0.92f);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float nx = (x - 24f) / 21f;
                    float ny = (y - 10f) / 8f;
                    float d = nx * nx + ny * ny + Mathf.Sin(x * 0.35f) * 0.08f;
                    if (d <= 1.0f)
                    {
                        tex.SetPixel(x, y, (d > 0.45f && d < 0.72f) ? oilSheen : oilDark);
                    }
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.25f), 32f);
        }

        private static Sprite CreatePoliceBeaconSprite()
        {
            const int w = 24;
            const int h = 12;
            Texture2D tex = NewPointTex(w, h);
            Color red = new Color(1.0f, 0.15f, 0.15f, 1f);
            Color blue = new Color(0.15f, 0.55f, 1.0f, 1f);
            Color housing = new Color(0.15f, 0.17f, 0.20f, 1f);

            FillRect(tex, 2, 2, 21, 9, housing);
            FillRect(tex, 3, 3, 10, 8, red);
            FillRect(tex, 13, 3, 20, 8, blue);

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 24f);
        }

        private static Texture2D NewPointTex(int w, int h)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            Color clear = new Color(0f, 0f, 0f, 0f);
            Color[] px = new Color[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = clear;
            tex.SetPixels(px);
            return tex;
        }

        private static void FillRect(Texture2D tex, int x0, int y0, int x1, int y1, Color c)
        {
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    if (x >= 0 && x < tex.width && y >= 0 && y < tex.height)
                    {
                        tex.SetPixel(x, y, c);
                    }
                }
            }
        }
    }
}
