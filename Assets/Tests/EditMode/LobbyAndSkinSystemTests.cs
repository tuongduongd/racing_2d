using NUnit.Framework;
using UnityEngine;

namespace RacingGame.Tests
{
    public class LobbyAndSkinSystemTests
    {
        [Test]
        public void SkinManager_InitializesAllFourDeliveryBrandsAndAppliesToCar()
        {
            var go = new GameObject("TestSkinManager");
            var carGo = new GameObject("TestPlayerCar");
            try
            {
                var skinMgr = go.AddComponent<SkinManager>();
                var car = carGo.AddComponent<Car>();

                Assert.AreEqual(4, skinMgr.SkinCount);

                BikeSkinData xanhSm = skinMgr.GetSkin(DriverBrandSkin.XanhSM);
                BikeSkinData shopee = skinMgr.GetSkin(DriverBrandSkin.ShopeeFood);
                BikeSkinData grab = skinMgr.GetSkin(DriverBrandSkin.Grab);
                BikeSkinData ahamove = skinMgr.GetSkin(DriverBrandSkin.Ahamove);

                Assert.IsNotNull(xanhSm);
                Assert.IsNotNull(shopee);
                Assert.IsNotNull(grab);
                Assert.IsNotNull(ahamove);

                Assert.AreEqual("Xanh SM", xanhSm.DisplayName);
                Assert.AreEqual("Shopee Food", shopee.DisplayName);
                Assert.AreEqual("Grab", grab.DisplayName);
                Assert.AreEqual("Ahamove", ahamove.DisplayName);

                DriverBrandSkin[] brands =
                {
                    DriverBrandSkin.XanhSM,
                    DriverBrandSkin.ShopeeFood,
                    DriverBrandSkin.Grab,
                    DriverBrandSkin.Ahamove
                };

                for (int i = 0; i < brands.Length; i++)
                {
                    bool ok = skinMgr.SelectSkin(brands[i], car);
                    Assert.IsTrue(ok);
                    Assert.AreEqual(brands[i], skinMgr.SelectedBrand);
                    Assert.IsNotNull(car.ActiveSkin);
                    Assert.AreEqual(brands[i], car.ActiveSkin.SkinId);
                    Assert.AreEqual(car.ActiveSkin.PrimaryColor, car.BikeColor);

                    BikeSpriteSet spriteSet = RoadRashBikeSprites.GetOrCreateSet(car.ActiveSkin);
                    Assert.IsNotNull(spriteSet);
                    Assert.IsNotNull(spriteSet.Upright0);
                }
            }
            finally
            {
                Object.DestroyImmediate(carGo);
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void BikeSkinData_SupportsCustomTextureAndMaterialAssignment()
        {
            BikeSkinData skin = BikeSkinData.CreateDefault(DriverBrandSkin.Grab);
            Texture2D tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            Shader shader = Shader.Find("Sprites/Default");
            Material mat = shader != null ? new Material(shader) : null;

            try
            {
                Assert.IsNull(skin.GetOverrideSprite(), "Default skin without custom texture/sprite uses procedural set.");

                skin.SetCustomAssets(mat, tex);
                Sprite overrideSprite = skin.GetOverrideSprite();

                Assert.IsNotNull(overrideSprite, "Assigning a custom Texture2D must auto-create an override Sprite.");
                Assert.AreEqual(tex, overrideSprite.texture);
                Assert.AreEqual(mat, skin.CustomMaterial);
            }
            finally
            {
                if (mat != null) Object.DestroyImmediate(mat);
                Object.DestroyImmediate(tex);
                Object.DestroyImmediate(skin);
            }
        }

        [Test]
        public void GameMenuUI_BuildsBannerOuterQuitButtonAndFourSkinCards()
        {
            var gmGo = new GameObject("TestLobbyGM");
            try
            {
                var gm = gmGo.AddComponent<GameManager>();
                var skinMgr = gmGo.AddComponent<SkinManager>();
                var menuUi = gmGo.AddComponent<GameMenuUI>();

                menuUi.Bind(gm);

                Assert.IsNotNull(menuUi.MenuCanvas, "Lobby Canvas should be created.");
                Assert.IsNotNull(menuUi.OuterQuitButton, "Outer 'X' Quit button must exist in the lobby.");
                Assert.AreEqual(4, menuUi.SkinCardCount, "Lobby must build 4 skin selection cards (Xanh SM, Shopee Food, Grab, Ahamove).");

                Assert.IsTrue(menuUi.SelectSkinFromLobby(DriverBrandSkin.ShopeeFood));
                Assert.AreEqual(DriverBrandSkin.ShopeeFood, skinMgr.SelectedBrand);

                Assert.IsTrue(menuUi.SelectSkinFromLobby(DriverBrandSkin.Ahamove));
                Assert.AreEqual(DriverBrandSkin.Ahamove, skinMgr.SelectedBrand);

                if (menuUi.MenuCanvas != null)
                {
                    Object.DestroyImmediate(menuUi.MenuCanvas.gameObject);
                }
            }
            finally
            {
                Object.DestroyImmediate(gmGo);
            }
        }
    }
}
