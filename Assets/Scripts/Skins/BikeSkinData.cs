using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// 4 chủ đề skin thương hiệu tài xế công nghệ trong game.
    /// </summary>
    public enum DriverBrandSkin
    {
        XanhSM = 0,
        ShopeeFood = 1,
        Grab = 2,
        Ahamove = 3
    }

    /// <summary>
    /// ScriptableObject lưu trữ dữ liệu của một Skin xe/tài xế (Xanh SM, Shopee Food, Grab, Ahamove).
    /// Cho phép kéo thả Material, Texture2D, hoặc Sprite trực tiếp trong Unity Inspector.
    /// </summary>
    [CreateAssetMenu(fileName = "BikeSkinData", menuName = "RacingGame/Bike Skin Data")]
    public class BikeSkinData : ScriptableObject
    {
        [Header("Định danh thương hiệu")]
        [SerializeField] private DriverBrandSkin skinId = DriverBrandSkin.XanhSM;
        [SerializeField] private string displayName = "Xanh SM";
        [SerializeField] private string tagline = "Thuần điện Xanh Cyan";

        [Header("Bảng màu chủ đạo (Áo khoác / Thân xe / Mũ bảo hiểm)")]
        [SerializeField] private Color primaryColor = new Color(0.00f, 0.72f, 0.71f);
        [SerializeField] private Color secondaryColor = new Color(0.92f, 0.98f, 0.98f);
        [SerializeField] private Color helmetColor = new Color(0.00f, 0.64f, 0.65f);

        [Header("Tài nguyên đồ họa (Gắn Material / Texture / Sprite tại đây)")]
        [Tooltip("Material tùy chỉnh cho xe/nhân vật (để trống nếu dùng material mặc định).")]
        [SerializeField] private Material customMaterial;

        [Tooltip("Texture2D tùy chỉnh cho xe/nhân vật (tự động tạo Sprite nếu chưa gắn Custom Sprite).")]
        [SerializeField] private Texture2D customTexture;

        [Tooltip("Sprite tùy chỉnh hiển thị cho xe trong game và ở sảnh chờ.")]
        [SerializeField] private Sprite customSprite;

        [Tooltip("Sprite icon/logo thương hiệu hiển thị trên thẻ chọn skin ở sảnh chờ.")]
        [SerializeField] private Sprite customLogoSprite;

        private Sprite _runtimeTextureSprite;
        private Texture2D _cachedTextureSource;

        public DriverBrandSkin SkinId => skinId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? skinId.ToString() : displayName;
        public string Tagline => tagline ?? string.Empty;
        public Color PrimaryColor => primaryColor;
        public Color SecondaryColor => secondaryColor;
        public Color HelmetColor => helmetColor;
        public Material CustomMaterial => customMaterial;
        public Texture2D CustomTexture => customTexture;
        public Sprite CustomSprite => customSprite;
        public Sprite CustomLogoSprite => customLogoSprite;

        public void ConfigureValues(
            DriverBrandSkin id,
            string nameText,
            string taglineText,
            Color primary,
            Color secondary,
            Color helmet,
            Material material = null,
            Texture2D texture = null,
            Sprite sprite = null,
            Sprite logoSprite = null)
        {
            skinId = id;
            displayName = nameText;
            tagline = taglineText;
            primaryColor = primary;
            secondaryColor = secondary;
            helmetColor = helmet;
            customMaterial = material;
            customTexture = texture;
            customSprite = sprite;
            customLogoSprite = logoSprite;
            _cachedTextureSource = null;
            _runtimeTextureSprite = null;
        }

        public void SetCustomAssets(Material material, Texture2D texture, Sprite sprite = null)
        {
            customMaterial = material;
            customTexture = texture;
            customSprite = sprite;
            _cachedTextureSource = null;
            _runtimeTextureSprite = null;
        }

        /// <summary>
        /// Trả về Sprite tùy chỉnh nếu người dùng đã gắn Custom Sprite hoặc Custom Texture2D;
        /// trả về null nếu dùng bộ sprite procedural theo màu thương hiệu.
        /// </summary>
        public Sprite GetOverrideSprite()
        {
            if (customSprite != null)
            {
                return customSprite;
            }

            if (customTexture != null)
            {
                if (_runtimeTextureSprite == null || _cachedTextureSource != customTexture)
                {
                    _cachedTextureSource = customTexture;
                    _runtimeTextureSprite = Sprite.Create(
                        customTexture,
                        new Rect(0f, 0f, customTexture.width, customTexture.height),
                        new Vector2(0.5f, 0.08f),
                        Mathf.Max(16f, customTexture.width * 0.5f));
                }
                return _runtimeTextureSprite;
            }

            return null;
        }

        /// <summary>
        /// Khởi tạo dữ liệu chuẩn cho 4 thương hiệu: Xanh SM, Shopee Food, Grab, Ahamove.
        /// </summary>
        public static BikeSkinData CreateDefault(DriverBrandSkin brand)
        {
            BikeSkinData data = CreateInstance<BikeSkinData>();
            data.name = $"Skin_{brand}";

            switch (brand)
            {
                case DriverBrandSkin.XanhSM:
                    data.ConfigureValues(
                        DriverBrandSkin.XanhSM,
                        "Xanh SM",
                        "Thuần điện Xanh Cyan",
                        primary: new Color(0.00f, 0.72f, 0.71f),   // #00B8B5 Cyan đặc trưng
                        secondary: new Color(0.90f, 0.99f, 0.99f), // Trắng xanh sáng
                        helmet: new Color(0.00f, 0.62f, 0.64f));
                    break;

                case DriverBrandSkin.ShopeeFood:
                    data.ConfigureValues(
                        DriverBrandSkin.ShopeeFood,
                        "Shopee Food",
                        "Chiến thần đơn cam",
                        primary: new Color(0.93f, 0.30f, 0.18f),   // #EE4D2D Cam đỏ ShopeeFood
                        secondary: new Color(1.00f, 0.93f, 0.88f), // Trắng kem cam
                        helmet: new Color(0.93f, 0.30f, 0.18f));
                    break;

                case DriverBrandSkin.Grab:
                    data.ConfigureValues(
                        DriverBrandSkin.Grab,
                        "Grab",
                        "Tia chớp xanh lá",
                        primary: new Color(0.00f, 0.69f, 0.31f),   // #00B14F Xanh lá Grab
                        secondary: new Color(0.90f, 0.98f, 0.92f), // Trắng xanh lá
                        helmet: new Color(0.00f, 0.58f, 0.25f));
                    break;

                case DriverBrandSkin.Ahamove:
                    data.ConfigureValues(
                        DriverBrandSkin.Ahamove,
                        "Ahamove",
                        "Biệt đội siêu tốc",
                        primary: new Color(0.96f, 0.50f, 0.13f),   // #F57E20 Cam Ahamove
                        secondary: new Color(0.11f, 0.21f, 0.36f), // #1B365D Xanh Navy Ahamove
                        helmet: new Color(0.11f, 0.21f, 0.36f));
                    break;
            }

            return data;
        }
    }
}
