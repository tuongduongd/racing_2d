using System.Collections.Generic;
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
        PoliceSirenBeacon,
        SkyscraperTower,
        TrafficLight,
        NeonSign,
        ParkedBus,
        CityBuilding,
        HighwaySign,
        BusStop,
        FireHydrant,
        VendingMachine,
        Pothole
    }

    /// <summary>
    /// Generates crisp retro roadside prop sprites, urban environment decorations,
    /// road obstacles (cones, barrels, barriers, oil slicks), and police beacon lights.
    /// </summary>
    public static class RoadRashPropSprites
    {
        private static readonly Dictionary<RoadsidePropKind, Sprite> _propCache =
            new Dictionary<RoadsidePropKind, Sprite>();

        public static Sprite GetPropSprite(RoadsidePropKind kind)
        {
            if (_propCache.TryGetValue(kind, out Sprite cached) && cached != null && cached.texture != null)
            {
                return cached;
            }

            Sprite created = CreatePropSprite(kind);
            _propCache[kind] = created;
            return created;
        }

        private static Sprite CreatePropSprite(RoadsidePropKind kind)
        {
            return kind switch
            {
                RoadsidePropKind.UtilityPole => CreateUtilityPoleSprite(),
                RoadsidePropKind.FencePost => CreateFencePostSprite(),
                RoadsidePropKind.ChevronLeft => CreateChevronSprite(-1),
                RoadsidePropKind.ChevronRight => CreateChevronSprite(1),
                RoadsidePropKind.Tree => CreateTreeSprite(),
                RoadsidePropKind.StreetLamp => CreateStreetLampSprite(),
                RoadsidePropKind.Billboard => CreateBillboardSprite(),
                RoadsidePropKind.PalmTree => CreatePalmTreeSprite(),
                RoadsidePropKind.TrafficCone => CreateTrafficConeSprite(),
                RoadsidePropKind.OilBarrel => CreateOilBarrelSprite(),
                RoadsidePropKind.RoadBarrier => CreateRoadBarrierSprite(),
                RoadsidePropKind.OilSlick => CreateOilSlickSprite(),
                RoadsidePropKind.Pothole => CreatePotholeSprite(),
                RoadsidePropKind.PoliceSirenBeacon => CreatePoliceBeaconSprite(),
                RoadsidePropKind.SkyscraperTower => CreateSkyscraperSprite(),
                RoadsidePropKind.TrafficLight => CreateTrafficLightSprite(),
                RoadsidePropKind.NeonSign => CreateNeonSignSprite(),
                RoadsidePropKind.ParkedBus => CreateParkedBusSprite(),
                RoadsidePropKind.CityBuilding => CreateCityBuildingSprite(),
                RoadsidePropKind.HighwaySign => CreateHighwaySignSprite(),
                RoadsidePropKind.BusStop => CreateBusStopSprite(),
                RoadsidePropKind.FireHydrant => CreateFireHydrantSprite(),
                RoadsidePropKind.VendingMachine => CreateVendingMachineSprite(),
                _ => CreateMilePostSprite()
            };
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

            return FinalizeSprite(tex, 28f);
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

            return FinalizeSprite(tex, 32f);
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

            return FinalizeSprite(tex, 30f);
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

            return FinalizeSprite(tex, 26f);
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

            return FinalizeSprite(tex, 32f);
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

            return FinalizeSprite(tex, 28f);
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

            return FinalizeSprite(tex, 26f);
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

            return FinalizeSprite(tex, 26f);
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

            return FinalizeSprite(tex, 32f);
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

            return FinalizeSprite(tex, 32f);
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

            return FinalizeSprite(tex, 32f);
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

            return FinalizeSprite(tex, 32f, new Vector2(0.5f, 0.25f));
        }

        private static Sprite CreatePotholeSprite()
        {
            const int w = 64;
            const int h = 32;
            Texture2D tex = NewPointTex(w, h);
            Color rimCrack = new Color(0.28f, 0.30f, 0.34f, 0.95f);
            Color innerWall = new Color(0.11f, 0.12f, 0.16f, 0.98f);
            Color deepCore = new Color(0.03f, 0.03f, 0.05f, 1.0f);
            Color grimeTint = new Color(0.18f, 0.15f, 0.12f, 0.92f);

            for (int y = 2; y < h - 2; y++)
            {
                for (int x = 4; x < w - 4; x++)
                {
                    float nx = (x - 32f) / 24f;
                    float ny = (y - 16f) / 11f;
                    float jagged = Mathf.Sin(x * 0.45f) * 0.07f + Mathf.Cos(y * 0.65f) * 0.05f;
                    float d = nx * nx + ny * ny + jagged;
                    if (d <= 1.0f)
                    {
                        if (d < 0.42f)
                            tex.SetPixel(x, y, deepCore);
                        else if (d < 0.78f)
                            tex.SetPixel(x, y, ((x + y) % 5 == 0) ? grimeTint : innerWall);
                        else
                            tex.SetPixel(x, y, rimCrack);
                    }
                }
            }

            // Cracked edges radiating outward slightly inside safe margin
            for (int x = 10; x < 16; x++) tex.SetPixel(x, 16, rimCrack);
            for (int x = 48; x < 54; x++) tex.SetPixel(x, 15, rimCrack);

            return FinalizeSprite(tex, 32f);
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

            return FinalizeSprite(tex, 24f, new Vector2(0.5f, 0.5f));
        }

        private static Sprite CreateSkyscraperSprite()
        {
            const int w = 64;
            const int h = 128;
            Texture2D tex = NewPointTex(w, h);
            Color facade = new Color(0.10f, 0.12f, 0.22f);
            Color facadeHi = new Color(0.16f, 0.19f, 0.32f);
            Color facadeShade = new Color(0.06f, 0.07f, 0.14f);
            Color trimCyan = new Color(0.18f, 0.92f, 1.00f);
            Color beaconRed = new Color(1.00f, 0.24f, 0.28f);
            Color winGold = new Color(1.00f, 0.84f, 0.34f);
            Color winCyan = new Color(0.32f, 0.90f, 1.00f);
            Color winDark = new Color(0.05f, 0.06f, 0.11f);
            Color lobbyGlow = new Color(0.88f, 0.96f, 1.00f);

            // Main tower body, sunlit/moonlit left edge, and stepped penthouse
            FillRect(tex, 8, 0, 55, 114, facade);
            FillRect(tex, 8, 0, 10, 114, facadeHi);
            FillRect(tex, 43, 0, 55, 114, facadeShade);
            FillRect(tex, 16, 115, 47, 122, facade);
            FillRect(tex, 38, 115, 47, 122, facadeShade);

            // Rooftop cyan trim and spire with aviation warning beacon
            FillRect(tex, 8, 113, 55, 114, trimCyan);
            FillRect(tex, 16, 121, 47, 121, trimCyan);
            FillRect(tex, 30, 122, 33, 125, trimCyan);
            FillRect(tex, 30, 126, 33, 127, beaconRed);

            // Illuminated street-level lobby entrance
            FillRect(tex, 20, 0, 43, 9, facadeShade);
            FillRect(tex, 22, 0, 41, 7, winCyan);
            FillRect(tex, 28, 0, 35, 6, lobbyGlow);
            FillRect(tex, 18, 9, 45, 10, trimCyan);

            // Window grid with lit/unlit rhythm
            for (int row = 14; row <= 106; row += 8)
            {
                for (int col = 12; col <= 50; col += 7)
                {
                    if (((row * 3 + col) % 11) == 0)
                    {
                        FillRect(tex, col, row, col + 3, row + 4, winDark);
                        continue;
                    }
                    Color wc = ((row + col) % 5 == 0) ? winCyan : winGold;
                    FillRect(tex, col, row, col + 3, row + 4, wc);
                }
            }

            return FinalizeSprite(tex, 22f);
        }

        private static Sprite CreateTrafficLightSprite()
        {
            const int w = 36;
            const int h = 88;
            Texture2D tex = NewPointTex(w, h);
            Color pole = new Color(0.24f, 0.27f, 0.32f);
            Color poleHi = new Color(0.42f, 0.46f, 0.52f);
            Color poleDark = new Color(0.14f, 0.16f, 0.20f);
            Color box = new Color(0.08f, 0.09f, 0.11f);
            Color bezel = new Color(0.92f, 0.72f, 0.12f);
            Color redOn = new Color(1.00f, 0.22f, 0.16f);
            Color yellowOn = new Color(1.00f, 0.82f, 0.14f);
            Color greenOn = new Color(0.18f, 0.96f, 0.42f);
            Color coreHi = new Color(1.00f, 0.96f, 0.88f);

            // Plinth, vertical post, and cantilever arm
            FillRect(tex, 6, 0, 14, 6, poleDark);
            FillRect(tex, 8, 6, 11, 80, pole);
            FillRect(tex, 8, 6, 9, 80, poleHi);
            FillRect(tex, 8, 72, 31, 75, pole);
            FillRect(tex, 8, 75, 31, 75, poleHi);

            // Diagonal gusset brace
            for (int i = 0; i < 7; i++)
            {
                FillRect(tex, 11 + i, 65 + i, 12 + i, 66 + i, poleDark);
            }

            // Signal housing hanging from arm with yellow retro border & 3 hooded lamps
            FillRect(tex, 16, 37, 29, 71, bezel);
            FillRect(tex, 17, 38, 28, 70, box);

            // Red lamp
            FillRect(tex, 19, 67, 26, 68, poleDark);
            FillRect(tex, 20, 60, 25, 66, redOn);
            FillRect(tex, 21, 63, 22, 65, coreHi);

            // Yellow lamp
            FillRect(tex, 19, 57, 26, 58, poleDark);
            FillRect(tex, 20, 50, 25, 56, yellowOn);
            FillRect(tex, 21, 53, 22, 55, coreHi);

            // Green lamp
            FillRect(tex, 19, 47, 26, 48, poleDark);
            FillRect(tex, 20, 40, 25, 46, greenOn);
            FillRect(tex, 21, 43, 22, 45, coreHi);

            return FinalizeSprite(tex, 28f);
        }

        private static Sprite CreateNeonSignSprite()
        {
            const int w = 52;
            const int h = 76;
            Texture2D tex = NewPointTex(w, h);
            Color frame = new Color(0.08f, 0.08f, 0.14f);
            Color steel = new Color(0.26f, 0.28f, 0.34f);
            Color steelHi = new Color(0.42f, 0.45f, 0.52f);
            Color magenta = new Color(1.00f, 0.20f, 0.72f);
            Color cyan = new Color(0.14f, 0.96f, 1.00f);
            Color yellow = new Color(1.00f, 0.90f, 0.24f);
            Color hotCore = new Color(1.00f, 0.96f, 1.00f);

            // Support post & mounting plate
            FillRect(tex, 20, 0, 31, 3, steel);
            FillRect(tex, 23, 3, 28, 28, steel);
            FillRect(tex, 23, 3, 24, 28, steelHi);

            // Sign housing
            FillRect(tex, 3, 26, 48, 72, steel);
            FillRect(tex, 4, 27, 47, 71, frame);

            // Double neon tube frame
            FillRect(tex, 5, 28, 46, 30, cyan);
            FillRect(tex, 5, 68, 46, 70, cyan);
            FillRect(tex, 5, 28, 7, 70, magenta);
            FillRect(tex, 44, 28, 46, 70, magenta);
            FillRect(tex, 6, 29, 45, 29, hotCore);
            FillRect(tex, 6, 69, 45, 69, hotCore);

            // Stylized neon bolt/arrow + letter strips
            FillRect(tex, 11, 56, 39, 61, magenta);
            FillRect(tex, 13, 58, 37, 59, hotCore);
            FillRect(tex, 11, 45, 31, 50, cyan);
            FillRect(tex, 13, 47, 29, 48, hotCore);
            FillRect(tex, 11, 35, 40, 39, yellow);

            // Glowing chevron arrow on right side
            for (int i = 0; i < 5; i++)
            {
                FillRect(tex, 35 + i, 44 + i, 36 + i, 45 + i, yellow);
                FillRect(tex, 39 - i, 48 + i, 40 - i, 49 + i, yellow);
            }

            return FinalizeSprite(tex, 26f);
        }

        private static Sprite CreateParkedBusSprite()
        {
            const int w = 72;
            const int h = 64;
            Texture2D tex = NewPointTex(w, h);
            Color tire = new Color(0.08f, 0.08f, 0.10f);
            Color bumper = new Color(0.22f, 0.24f, 0.28f);
            Color body = new Color(0.92f, 0.62f, 0.12f);
            Color bodyShade = new Color(0.70f, 0.42f, 0.08f);
            Color roof = new Color(0.94f, 0.95f, 0.98f);
            Color glass = new Color(0.14f, 0.22f, 0.36f);
            Color glassSheen = new Color(0.28f, 0.42f, 0.60f);
            Color tailRed = new Color(1.00f, 0.20f, 0.16f);
            Color amber = new Color(1.00f, 0.86f, 0.28f);

            // Rear tires
            FillRect(tex, 8, 0, 18, 10, tire);
            FillRect(tex, 53, 0, 63, 10, tire);

            // Bus body, right-side shade, white roof & rooftop AC pod
            FillRect(tex, 4, 9, 67, 54, body);
            FillRect(tex, 52, 9, 67, 54, bodyShade);
            FillRect(tex, 6, 55, 65, 59, roof);
            FillRect(tex, 22, 60, 49, 62, bumper);

            // Rear windshield with pillar & diagonal reflection
            FillRect(tex, 9, 32, 62, 50, glass);
            FillRect(tex, 35, 32, 36, 50, bumper);
            for (int i = 0; i < 8; i++)
            {
                FillRect(tex, 14 + i, 35 + i * 2, 16 + i, 36 + i * 2, glassSheen);
            }

            // Side/waist stripe & LED route sign
            FillRect(tex, 6, 25, 65, 28, roof);
            FillRect(tex, 24, 51, 47, 53, bumper);
            FillRect(tex, 26, 52, 45, 52, amber);

            // Bumper, engine louvers, taillights & license plate
            FillRect(tex, 3, 7, 68, 10, bumper);
            FillRect(tex, 8, 14, 15, 20, tailRed);
            FillRect(tex, 16, 15, 19, 19, amber);
            FillRect(tex, 56, 14, 63, 20, tailRed);
            FillRect(tex, 52, 15, 55, 19, amber);
            FillRect(tex, 30, 14, 41, 19, roof);
            FillRect(tex, 22, 12, 26, 21, bodyShade);
            FillRect(tex, 45, 12, 49, 21, bodyShade);

            return FinalizeSprite(tex, 24f);
        }

        private static Sprite CreateCityBuildingSprite()
        {
            const int w = 64;
            const int h = 96;
            Texture2D tex = NewPointTex(w, h);
            Color brick = new Color(0.24f, 0.18f, 0.26f);
            Color brickHi = new Color(0.32f, 0.24f, 0.34f);
            Color brickShade = new Color(0.14f, 0.11f, 0.18f);
            Color trim = new Color(0.42f, 0.44f, 0.52f);
            Color awningRed = new Color(0.84f, 0.18f, 0.24f);
            Color awningCream = new Color(0.95f, 0.90f, 0.78f);
            Color shopGlow = new Color(1.00f, 0.88f, 0.46f);
            Color winCyan = new Color(0.30f, 0.88f, 1.00f);
            Color winGold = new Color(0.98f, 0.80f, 0.30f);
            Color winDark = new Color(0.08f, 0.08f, 0.13f);

            // Mid-rise building body & parapet
            FillRect(tex, 4, 0, 59, 89, brick);
            FillRect(tex, 4, 0, 7, 89, brickHi);
            FillRect(tex, 46, 0, 59, 89, brickShade);
            FillRect(tex, 2, 87, 61, 90, trim);
            FillRect(tex, 10, 91, 22, 95, trim);
            FillRect(tex, 36, 91, 46, 94, brickShade);

            // Ground-floor storefront & lit entrance
            FillRect(tex, 6, 0, 57, 22, brickShade);
            FillRect(tex, 8, 4, 37, 15, shopGlow);
            FillRect(tex, 42, 0, 53, 15, winCyan);

            // Striped storefront awning
            for (int x = 5; x <= 58; x += 4)
            {
                Color stripe = ((x / 4) % 2 == 0) ? awningRed : awningCream;
                FillRect(tex, x, 17, Mathf.Min(58, x + 3), 21, stripe);
            }

            // Three upper apartment stories
            for (int row = 28; row <= 76; row += 19)
            {
                FillRect(tex, 4, row - 2, 59, row - 1, trim);
                for (int i = 0; i < 4; i++)
                {
                    int wx = 10 + i * 12;
                    Color wc = ((row + i) % 3 == 0) ? winDark : (((row + i) % 2 == 0) ? winCyan : winGold);
                    FillRect(tex, wx - 1, row, wx + 8, row + 11, trim);
                    FillRect(tex, wx, row + 1, wx + 7, row + 10, wc);
                }
            }

            return FinalizeSprite(tex, 20f);
        }

        private static Sprite CreateHighwaySignSprite()
        {
            const int w = 64;
            const int h = 68;
            Texture2D tex = NewPointTex(w, h);
            Color steel = new Color(0.42f, 0.46f, 0.52f);
            Color steelDark = new Color(0.24f, 0.27f, 0.32f);
            Color greenSign = new Color(0.06f, 0.44f, 0.24f);
            Color greenShade = new Color(0.04f, 0.30f, 0.16f);
            Color white = new Color(0.95f, 0.97f, 0.99f);
            Color shieldBlue = new Color(0.14f, 0.36f, 0.82f);
            Color shieldRed = new Color(0.86f, 0.20f, 0.18f);

            // Twin support posts
            FillRect(tex, 16, 0, 19, 31, steel);
            FillRect(tex, 19, 0, 19, 31, steelDark);
            FillRect(tex, 44, 0, 47, 31, steel);
            FillRect(tex, 47, 0, 47, 31, steelDark);

            // Highway signboard with crisp white border
            FillRect(tex, 3, 30, 60, 64, greenSign);
            FillRect(tex, 3, 30, 60, 31, greenShade);
            FillRect(tex, 5, 32, 58, 33, white);
            FillRect(tex, 5, 61, 58, 62, white);
            FillRect(tex, 5, 32, 6, 62, white);
            FillRect(tex, 57, 32, 58, 62, white);

            // Interstate route shield
            FillRect(tex, 10, 45, 20, 56, white);
            FillRect(tex, 11, 52, 19, 55, shieldRed);
            FillRect(tex, 11, 46, 19, 51, shieldBlue);

            // Legend text bars
            FillRect(tex, 24, 53, 52, 56, white);
            FillRect(tex, 24, 46, 46, 49, white);

            // Exit directional arrow on bottom row
            FillRect(tex, 12, 37, 46, 40, white);
            for (int k = 0; k < 5; k++)
            {
                FillRect(tex, 46 + k, 35 + k, 46 + k, 42 - k, white);
            }

            return FinalizeSprite(tex, 24f);
        }

        private static Sprite CreateBusStopSprite()
        {
            const int w = 56;
            const int h = 52;
            Texture2D tex = NewPointTex(w, h);
            Color frame = new Color(0.18f, 0.22f, 0.28f);
            Color frameHi = new Color(0.36f, 0.42f, 0.50f);
            Color glass = new Color(0.24f, 0.56f, 0.72f, 0.85f);
            Color glassHi = new Color(0.56f, 0.88f, 0.98f, 0.90f);
            Color canopy = new Color(0.12f, 0.28f, 0.40f);
            Color neonCyan = new Color(0.18f, 0.94f, 1.00f);
            Color adGlow = new Color(1.00f, 0.84f, 0.42f);
            Color adAccent = new Color(0.96f, 0.24f, 0.62f);
            Color benchWood = new Color(0.62f, 0.40f, 0.18f);

            // Shelter posts & sloped canopy
            FillRect(tex, 5, 0, 7, 40, frame);
            FillRect(tex, 43, 0, 45, 40, frame);
            FillRect(tex, 2, 40, 48, 45, canopy);
            FillRect(tex, 2, 45, 48, 46, frameHi);
            FillRect(tex, 2, 39, 48, 39, neonCyan);

            // Translucent glass shelter back
            FillRect(tex, 8, 6, 32, 38, glass);
            for (int i = 0; i < 6; i++)
            {
                FillRect(tex, 12 + i, 12 + i * 3, 13 + i, 14 + i * 3, glassHi);
            }

            // Bench inside shelter
            FillRect(tex, 11, 0, 13, 9, frame);
            FillRect(tex, 27, 0, 29, 9, frame);
            FillRect(tex, 9, 9, 31, 13, benchWood);

            // Illuminated transit poster box
            FillRect(tex, 33, 3, 42, 38, frame);
            FillRect(tex, 35, 6, 40, 35, adGlow);
            FillRect(tex, 36, 20, 39, 30, adAccent);

            // Curbside bus-stop sign pole on right
            FillRect(tex, 50, 0, 51, 44, frameHi);
            FillRect(tex, 47, 43, 54, 50, neonCyan);
            FillRect(tex, 49, 45, 52, 48, frame);

            return FinalizeSprite(tex, 24f);
        }

        private static Sprite CreateFireHydrantSprite()
        {
            const int w = 24;
            const int h = 32;
            Texture2D tex = NewPointTex(w, h);
            Color curb = new Color(0.45f, 0.47f, 0.52f);
            Color red = new Color(0.90f, 0.16f, 0.14f);
            Color redHi = new Color(1.00f, 0.42f, 0.32f);
            Color redDark = new Color(0.56f, 0.08f, 0.08f);
            Color silver = new Color(0.84f, 0.88f, 0.92f);

            // Concrete pad
            FillRect(tex, 5, 0, 18, 3, curb);

            // Hydrant barrel, flanges & dome cap
            FillRect(tex, 7, 4, 16, 6, redDark);
            FillRect(tex, 8, 7, 15, 19, red);
            FillRect(tex, 8, 7, 9, 19, redHi);
            FillRect(tex, 14, 7, 15, 19, redDark);
            FillRect(tex, 7, 20, 16, 22, redDark);
            FillRect(tex, 8, 21, 14, 21, redHi);
            FillRect(tex, 9, 23, 14, 26, red);
            FillRect(tex, 10, 27, 13, 29, silver);

            // Side hose nozzles
            FillRect(tex, 4, 13, 7, 17, silver);
            FillRect(tex, 16, 13, 19, 17, silver);
            FillRect(tex, 10, 12, 13, 16, silver);

            return FinalizeSprite(tex, 32f);
        }

        private static Sprite CreateVendingMachineSprite()
        {
            const int w = 32;
            const int h = 48;
            Texture2D tex = NewPointTex(w, h);
            Color body = new Color(0.14f, 0.18f, 0.28f);
            Color bodyHi = new Color(0.24f, 0.30f, 0.44f);
            Color bodyShade = new Color(0.08f, 0.10f, 0.18f);
            Color windowBg = new Color(0.78f, 0.95f, 1.00f);
            Color neonPink = new Color(1.00f, 0.24f, 0.68f);
            Color neonCyan = new Color(0.18f, 0.94f, 1.00f);
            Color canRed = new Color(0.94f, 0.22f, 0.20f);
            Color canGold = new Color(0.98f, 0.80f, 0.18f);
            Color canGreen = new Color(0.20f, 0.84f, 0.38f);

            // Feet & cabinet
            FillRect(tex, 6, 0, 9, 2, bodyShade);
            FillRect(tex, 22, 0, 25, 2, bodyShade);
            FillRect(tex, 4, 2, 27, 45, body);
            FillRect(tex, 4, 2, 6, 45, bodyHi);
            FillRect(tex, 24, 2, 27, 45, bodyShade);

            // Illuminated header & display window
            FillRect(tex, 7, 40, 24, 43, neonPink);
            FillRect(tex, 9, 41, 22, 42, neonCyan);
            FillRect(tex, 7, 20, 21, 38, windowBg);

            // Drink rows inside lit display
            for (int row = 22; row <= 34; row += 6)
            {
                FillRect(tex, 7, row - 1, 21, row - 1, bodyShade);
                FillRect(tex, 8, row, 10, row + 3, canRed);
                FillRect(tex, 12, row, 14, row + 3, canGold);
                FillRect(tex, 16, row, 18, row + 3, canGreen);
                FillRect(tex, 19, row, 20, row + 3, neonCyan);
            }

            // Keypad & dispense slot
            FillRect(tex, 22, 12, 25, 19, neonCyan);
            FillRect(tex, 7, 5, 19, 11, bodyShade);

            return FinalizeSprite(tex, 28f);
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

        private static Sprite FinalizeSprite(Texture2D tex, float pixelsPerUnit, Vector2? pivot = null)
        {
            tex.Apply();
            return Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                pivot ?? new Vector2(0.5f, 0f),
                pixelsPerUnit);
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
