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
        MilePost
    }

    /// <summary>
    /// Generates crisp retro roadside prop sprites (wooden telephone poles, fence posts,
    /// chevron corner warning signs, trees, and mileposts) matching the PS1 Road Rash highway aesthetic.
    /// </summary>
    public static class RoadRashPropSprites
    {
        private static Sprite _utilityPole;
        private static Sprite _fencePost;
        private static Sprite _chevronLeft;
        private static Sprite _chevronRight;
        private static Sprite _tree;
        private static Sprite _milePost;

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

            // Vertical pole
            FillRect(tex, 18, 0, 21, 93, wood);
            FillRect(tex, 18, 0, 18, 93, woodHi);
            FillRect(tex, 21, 0, 21, 93, woodDark);

            // Top crossarm
            FillRect(tex, 4, 80, 35, 83, wood);
            FillRect(tex, 4, 83, 35, 83, woodHi);
            FillRect(tex, 4, 80, 35, 80, woodDark);

            // Second lower crossarm
            FillRect(tex, 8, 69, 31, 71, wood);
            FillRect(tex, 8, 69, 31, 69, woodDark);

            // Diagonal brace struts
            for (int i = 0; i < 6; i++)
            {
                tex.SetPixel(13 + i, 74 + i, woodDark);
                tex.SetPixel(26 - i, 74 + i, woodDark);
            }

            // Insulators
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

            // Post
            FillRect(tex, 9, 0, 14, 31, wood);
            FillRect(tex, 9, 0, 10, 31, woodHi);
            FillRect(tex, 13, 0, 14, 31, woodDark);
            FillRect(tex, 10, 32, 13, 33, woodHi);

            // Horizontal rail stubs
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

            // Steel post
            FillRect(tex, 14, 0, 17, 25, metal);
            FillRect(tex, 17, 0, 17, 25, metalDark);

            // Sign board
            FillRect(tex, 3, 23, 28, 44, black);
            FillRect(tex, 5, 25, 26, 42, yellow);

            // Bold arrow chevron
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
