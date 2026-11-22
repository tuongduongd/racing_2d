using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RacingGame
{
    public enum BikePaletteKind
    {
        XanhSM = 0,
        ShopeeFood = 1,
        Grab = 2,
        Ahamove = 3,
        Police = 4
    }

    /// <summary>
    /// Chuyên trách hoán đổi bảng màu (HSV/Color-range palette swap) từ bộ sprite
    /// Xanh SM góc nhìn sau lưng sang các hãng (Shopee Food, Grab, Ahamove) và Cảnh sát,
    /// kèm cache cố định để tránh cấp phát rác trong Update/FixedUpdate.
    /// </summary>
    public static class BikePaletteSwapper
    {
        private readonly struct PaletteCacheKey : IEquatable<PaletteCacheKey>
        {
            public readonly BikePaletteKind Palette;
            private readonly Sprite _upright;
            private readonly Sprite _leanLeft1;
            private readonly Sprite _leanLeft2;
            private readonly Sprite _leanRight1;
            private readonly Sprite _leanRight2;
            private readonly Sprite _kickLeft;
            private readonly Sprite _hitStagger;

            public PaletteCacheKey(
                BikePaletteKind palette,
                Sprite upright,
                Sprite leanLeft1,
                Sprite leanLeft2,
                Sprite leanRight1,
                Sprite leanRight2,
                Sprite kickLeft,
                Sprite hitStagger)
            {
                Palette = palette;
                _upright = upright;
                _leanLeft1 = leanLeft1;
                _leanLeft2 = leanLeft2;
                _leanRight1 = leanRight1;
                _leanRight2 = leanRight2;
                _kickLeft = kickLeft;
                _hitStagger = hitStagger;
            }

            public bool Equals(PaletteCacheKey other)
            {
                return Palette == other.Palette
                    && ReferenceEquals(_upright, other._upright)
                    && ReferenceEquals(_leanLeft1, other._leanLeft1)
                    && ReferenceEquals(_leanLeft2, other._leanLeft2)
                    && ReferenceEquals(_leanRight1, other._leanRight1)
                    && ReferenceEquals(_leanRight2, other._leanRight2)
                    && ReferenceEquals(_kickLeft, other._kickLeft)
                    && ReferenceEquals(_hitStagger, other._hitStagger);
            }

            public override bool Equals(object obj) => obj is PaletteCacheKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = (int)Palette;
                    hash = (hash * 397) ^ RuntimeHelpers.GetHashCode(_upright);
                    hash = (hash * 397) ^ RuntimeHelpers.GetHashCode(_leanLeft1);
                    hash = (hash * 397) ^ RuntimeHelpers.GetHashCode(_leanLeft2);
                    hash = (hash * 397) ^ RuntimeHelpers.GetHashCode(_leanRight1);
                    hash = (hash * 397) ^ RuntimeHelpers.GetHashCode(_leanRight2);
                    hash = (hash * 397) ^ RuntimeHelpers.GetHashCode(_kickLeft);
                    hash = (hash * 397) ^ RuntimeHelpers.GetHashCode(_hitStagger);
                    return hash;
                }
            }
        }

        private sealed class CachedPaletteEntry
        {
            public BikeSpriteSet SpriteSet;
            public List<Texture2D> OwnedTextures;
            public List<Sprite> OwnedSprites;
        }

        private static readonly Dictionary<PaletteCacheKey, CachedPaletteEntry> Cache =
            new Dictionary<PaletteCacheKey, CachedPaletteEntry>(8);

        public static int CachedEntryCount => Cache.Count;

        public static BikePaletteKind ToPaletteKind(DriverBrandSkin brand)
        {
            return brand switch
            {
                DriverBrandSkin.ShopeeFood => BikePaletteKind.ShopeeFood,
                DriverBrandSkin.Grab => BikePaletteKind.Grab,
                DriverBrandSkin.Ahamove => BikePaletteKind.Ahamove,
                _ => BikePaletteKind.XanhSM
            };
        }

        public static void ClearCache()
        {
            foreach (KeyValuePair<PaletteCacheKey, CachedPaletteEntry> kv in Cache)
            {
                DestroyOwnedAssets(kv.Value);
            }
            Cache.Clear();
        }

        public static void DisposeCache()
        {
            ClearCache();
        }

        public static BikeSpriteSet GetOrCreateSet(BikeSkinData skin)
        {
            if (skin == null)
            {
                return BuildProceduralFallback(BikePaletteKind.XanhSM, null, null, null);
            }

            BikePaletteKind palette = ToPaletteKind(skin.SkinId);
            return GetOrCreateSet(palette, skin);
        }

        public static BikeSpriteSet GetOrCreateSet(BikePaletteKind palette, BikeSkinData multiPoseSource)
        {
            if (multiPoseSource == null || !multiPoseSource.HasMultiPoseSprites)
            {
                return BuildProceduralFallback(
                    palette,
                    multiPoseSource != null ? multiPoseSource.PrimaryColor : (Color?)null,
                    multiPoseSource != null ? multiPoseSource.SecondaryColor : (Color?)null,
                    multiPoseSource != null ? multiPoseSource.HelmetColor : (Color?)null);
            }

            return GetOrCreateSet(
                palette,
                multiPoseSource.UprightSprite,
                multiPoseSource.LeanLeft1Sprite,
                multiPoseSource.LeanLeft2Sprite,
                multiPoseSource.LeanRight1Sprite,
                multiPoseSource.LeanRight2Sprite,
                multiPoseSource.KickLeftSprite,
                multiPoseSource.HitStaggerSprite,
                multiPoseSource.PrimaryColor,
                multiPoseSource.SecondaryColor,
                multiPoseSource.HelmetColor);
        }

        public static BikeSpriteSet GetOrCreateSet(
            BikePaletteKind palette,
            Sprite upright,
            Sprite leanLeft1,
            Sprite leanLeft2,
            Sprite leanRight1,
            Sprite leanRight2,
            Sprite kickLeft,
            Sprite hitStagger,
            Color? fallbackPrimary = null,
            Color? fallbackSecondary = null,
            Color? fallbackHelmet = null)
        {
            if (upright == null || upright.texture == null || !upright.texture.isReadable)
            {
                return BuildProceduralFallback(palette, fallbackPrimary, fallbackSecondary, fallbackHelmet);
            }

            Sprite resolvedLeanLeft1 = IsUsableReadableSprite(leanLeft1) ? leanLeft1 : upright;
            Sprite resolvedLeanLeft2 = IsUsableReadableSprite(leanLeft2) ? leanLeft2 : resolvedLeanLeft1;
            Sprite resolvedLeanRight1 = IsUsableReadableSprite(leanRight1) ? leanRight1 : upright;
            Sprite resolvedLeanRight2 = IsUsableReadableSprite(leanRight2) ? leanRight2 : resolvedLeanRight1;
            Sprite resolvedKickLeft = IsUsableReadableSprite(kickLeft) ? kickLeft : upright;
            Sprite resolvedHitStagger = IsUsableReadableSprite(hitStagger)
                ? hitStagger
                : (IsUsableReadableSprite(leanRight2) ? leanRight2 : resolvedLeanLeft2);

            PaletteCacheKey key = new PaletteCacheKey(
                palette,
                upright,
                resolvedLeanLeft1,
                resolvedLeanLeft2,
                resolvedLeanRight1,
                resolvedLeanRight2,
                resolvedKickLeft,
                resolvedHitStagger);

            if (Cache.TryGetValue(key, out CachedPaletteEntry existing))
            {
                if (IsCacheEntryValid(existing))
                {
                    return existing.SpriteSet;
                }

                DestroyOwnedAssets(existing);
                Cache.Remove(key);
            }

            CachedPaletteEntry created = CreateEntry(
                palette,
                upright,
                resolvedLeanLeft1,
                resolvedLeanLeft2,
                resolvedLeanRight1,
                resolvedLeanRight2,
                resolvedKickLeft,
                resolvedHitStagger);

            Cache[key] = created;
            return created.SpriteSet;
        }

        private static bool IsUsableReadableSprite(Sprite sprite)
        {
            return sprite != null && sprite.texture != null && sprite.texture.isReadable;
        }

        private static bool IsCacheEntryValid(CachedPaletteEntry entry)
        {
            return entry != null
                && entry.SpriteSet != null
                && entry.SpriteSet.Upright0 != null
                && entry.SpriteSet.Upright0.texture != null;
        }

        private static CachedPaletteEntry CreateEntry(
            BikePaletteKind palette,
            Sprite upright,
            Sprite leanLeft1,
            Sprite leanLeft2,
            Sprite leanRight1,
            Sprite leanRight2,
            Sprite kickLeft,
            Sprite hitStagger)
        {
            if (palette == BikePaletteKind.XanhSM)
            {
                BikeSpriteSet baseSet = new BikeSpriteSet
                {
                    Upright0 = upright,
                    Upright1 = upright,
                    LeanLeft1 = leanLeft1,
                    LeanLeft2 = leanLeft2,
                    LeanRight1 = leanRight1,
                    LeanRight2 = leanRight2,
                    KickLeft = kickLeft,
                    KickRight = kickLeft,
                    HitStagger = hitStagger,
                    FlipKickRight = true
                };

                return new CachedPaletteEntry
                {
                    SpriteSet = baseSet,
                    OwnedTextures = new List<Texture2D>(0),
                    OwnedSprites = new List<Sprite>(0)
                };
            }

            List<Texture2D> ownedTextures = new List<Texture2D>(7);
            List<Sprite> ownedSprites = new List<Sprite>(7);
            Dictionary<Sprite, Sprite> localDedup = new Dictionary<Sprite, Sprite>(7);

            Sprite GetSwapped(Sprite src)
            {
                if (localDedup.TryGetValue(src, out Sprite cached))
                {
                    return cached;
                }

                Sprite swapped = SwapSingleSprite(src, palette, out Texture2D tex);
                localDedup[src] = swapped;
                ownedTextures.Add(tex);
                ownedSprites.Add(swapped);
                return swapped;
            }

            Sprite swappedUpright = GetSwapped(upright);
            Sprite swappedKickLeft = GetSwapped(kickLeft);

            BikeSpriteSet set = new BikeSpriteSet
            {
                Upright0 = swappedUpright,
                Upright1 = swappedUpright,
                LeanLeft1 = GetSwapped(leanLeft1),
                LeanLeft2 = GetSwapped(leanLeft2),
                LeanRight1 = GetSwapped(leanRight1),
                LeanRight2 = GetSwapped(leanRight2),
                KickLeft = swappedKickLeft,
                KickRight = swappedKickLeft,
                HitStagger = GetSwapped(hitStagger),
                FlipKickRight = true
            };

            return new CachedPaletteEntry
            {
                SpriteSet = set,
                OwnedTextures = ownedTextures,
                OwnedSprites = ownedSprites
            };
        }

        private static Sprite SwapSingleSprite(Sprite src, BikePaletteKind palette, out Texture2D outTexture)
        {
            Texture2D srcTex = src.texture;
            Rect rect = src.rect;
            int rx = Mathf.Clamp(Mathf.RoundToInt(rect.x), 0, srcTex.width - 1);
            int ry = Mathf.Clamp(Mathf.RoundToInt(rect.y), 0, srcTex.height - 1);
            int rw = Mathf.Clamp(Mathf.RoundToInt(rect.width), 1, srcTex.width - rx);
            int rh = Mathf.Clamp(Mathf.RoundToInt(rect.height), 1, srcTex.height - ry);

            Color[] pixels = srcTex.GetPixels(rx, ry, rw, rh);
            for (int i = 0; i < pixels.Length; i++)
            {
                Color c = pixels[i];
                if (c.a <= 0.004f) continue;
                pixels[i] = RemapColor(c, palette);
            }

            outTexture = new Texture2D(rw, rh, TextureFormat.RGBA32, false)
            {
                name = $"{src.name}_{palette}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            outTexture.SetPixels(pixels);
            outTexture.Apply(false, false);

            Vector2 normalizedPivot = new Vector2(
                src.pivot.x / Mathf.Max(1f, rect.width),
                src.pivot.y / Mathf.Max(1f, rect.height));

            Sprite result = Sprite.Create(
                outTexture,
                new Rect(0f, 0f, rw, rh),
                normalizedPivot,
                src.pixelsPerUnit);
            result.name = $"{src.name}_{palette}";
            return result;
        }

        private static Color RemapColor(Color srcColor, BikePaletteKind palette)
        {
            Color.RGBToHSV(srcColor, out float h, out float s, out float v);
            float hDeg = h * 360f;

            // Preserve dark outlines, tires, chrome exhausts, license plates, and non-brand regions
            if (s < 0.30f || v < 0.24f || hDeg < 150f || hDeg > 238f)
            {
                return srcColor;
            }

            // Secondary mid-blue region (224..238) only nudged for Ahamove navy pants/accents
            if (hDeg > 224f)
            {
                if (palette == BikePaletteKind.Ahamove && s >= 0.35f)
                {
                    Color navy = Color.HSVToRGB(216f / 360f, Mathf.Clamp(s * 0.95f, 0.45f, 0.85f), v * 0.78f);
                    navy.a = srcColor.a;
                    return navy;
                }
                return srcColor;
            }

            float weight = hDeg <= 208f
                ? 1f
                : (s >= 0.35f && v >= 0.28f ? Mathf.InverseLerp(224f, 208f, hDeg) : 0f);

            if (weight <= 0.001f)
            {
                return srcColor;
            }

            float targetH = h;
            float targetS = s;
            float targetV = v;

            switch (palette)
            {
                case BikePaletteKind.ShopeeFood:
                    targetH = 12f / 360f;
                    targetS = Mathf.Clamp01(s * 1.10f);
                    targetV = Mathf.Clamp01(v * 0.98f);
                    break;

                case BikePaletteKind.Grab:
                    targetH = 145f / 360f;
                    targetS = Mathf.Clamp01(s * 1.05f);
                    targetV = Mathf.Clamp01(v * 0.94f);
                    break;

                case BikePaletteKind.Ahamove:
                    if (hDeg > 208f)
                    {
                        targetH = 216f / 360f;
                        targetS = Mathf.Clamp(s * 0.95f, 0.45f, 0.85f);
                        targetV = v * 0.82f;
                    }
                    else
                    {
                        targetH = 27f / 360f;
                        targetS = Mathf.Clamp01(s * 1.08f);
                        targetV = Mathf.Clamp01(v * 1.00f);
                    }
                    break;

                case BikePaletteKind.Police:
                    if (v >= 0.78f)
                    {
                        targetH = 215f / 360f;
                        targetS = s * 0.14f;
                        targetV = Mathf.Clamp01(v * 1.06f);
                    }
                    else
                    {
                        targetH = 222f / 360f;
                        targetS = Mathf.Clamp(s * 1.02f, 0.45f, 0.92f);
                        targetV = v * 0.64f;
                    }
                    break;
            }

            Color targetRgb = Color.HSVToRGB(targetH, targetS, targetV);
            Color blended = Color.Lerp(srcColor, targetRgb, weight);
            blended.a = srcColor.a;
            return blended;
        }

        private static BikeSpriteSet BuildProceduralFallback(
            BikePaletteKind palette,
            Color? fallbackPrimary,
            Color? fallbackSecondary,
            Color? fallbackHelmet)
        {
            if (fallbackPrimary.HasValue && fallbackSecondary.HasValue && fallbackHelmet.HasValue)
            {
                return RoadRashBikeSprites.GetOrCreateSet(
                    fallbackPrimary.Value,
                    fallbackSecondary.Value,
                    fallbackHelmet.Value);
            }

            switch (palette)
            {
                case BikePaletteKind.ShopeeFood:
                    return RoadRashBikeSprites.GetOrCreateSet(
                        new Color(0.93f, 0.30f, 0.18f),
                        new Color(1.00f, 0.93f, 0.88f),
                        new Color(0.93f, 0.30f, 0.18f));
                case BikePaletteKind.Grab:
                    return RoadRashBikeSprites.GetOrCreateSet(
                        new Color(0.00f, 0.69f, 0.31f),
                        new Color(0.90f, 0.98f, 0.92f),
                        new Color(0.00f, 0.58f, 0.25f));
                case BikePaletteKind.Ahamove:
                    return RoadRashBikeSprites.GetOrCreateSet(
                        new Color(0.96f, 0.50f, 0.13f),
                        new Color(0.11f, 0.21f, 0.36f),
                        new Color(0.11f, 0.21f, 0.36f));
                case BikePaletteKind.Police:
                    return RoadRashBikeSprites.GetOrCreateSet(
                        new Color(0.10f, 0.20f, 0.56f),
                        new Color(0.95f, 0.97f, 1.00f),
                        new Color(0.95f, 0.97f, 1.00f));
                default:
                    return RoadRashBikeSprites.GetOrCreateSet(
                        new Color(0.00f, 0.72f, 0.71f),
                        new Color(0.90f, 0.99f, 0.99f),
                        new Color(0.00f, 0.62f, 0.64f));
            }
        }

        private static void DestroyOwnedAssets(CachedPaletteEntry entry)
        {
            if (entry == null) return;

            if (entry.OwnedSprites != null)
            {
                for (int i = 0; i < entry.OwnedSprites.Count; i++)
                {
                    DestroyUnityObject(entry.OwnedSprites[i]);
                }
                entry.OwnedSprites.Clear();
            }

            if (entry.OwnedTextures != null)
            {
                for (int i = 0; i < entry.OwnedTextures.Count; i++)
                {
                    DestroyUnityObject(entry.OwnedTextures[i]);
                }
                entry.OwnedTextures.Clear();
            }
        }

        private static void DestroyUnityObject(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying)
            {
                Object.Destroy(obj);
            }
            else
            {
                Object.DestroyImmediate(obj);
            }
        }
    }
}
