using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RacingGame
{
    public sealed class UpgradeCardWidgets
    {
        public UpgradeType Type;
        public Image CardBg;
        public Image TopAccentStrip;
        public TMP_Text TitleText;
        public TMP_Text LevelBadgeText;
        public Image LevelBadgeBg;
        public TMP_Text EffectText;
        public TMP_Text CostText;
        public Button BuyButton;
        public Image ButtonImg;
        public TMP_Text ButtonLabel;
        public readonly List<Image> LevelPips = new List<Image>(CarUpgradeSystem.MaxUpgradeLevel);
    }

    public sealed class GaragePanelWidgets
    {
        public GameObject RootPanel;
        public TMP_Text MoneyText;
        public TMP_Text BikeBrandTitleText;
        public TMP_Text BikeTaglineText;
        public Image BikePreviewImage;
        public Image BikeAccentBar;
        public TMP_Text StatsSummaryText;
        public Image SpeedBarFill;
        public Image PowerBarFill;
        public Image BrakeBarFill;
        public Image GripBarFill;
        public TMP_Text SpeedValueText;
        public TMP_Text PowerValueText;
        public TMP_Text BrakeValueText;
        public TMP_Text GripValueText;
        public TMP_Text FeedbackText;
        public Image FeedbackPanelBg;
        public readonly List<UpgradeCardWidgets> UpgradeCards = new List<UpgradeCardWidgets>(4);
    }

    /// <summary>
    /// Dedicated SRP builder for the Cyber Courier Velocity Garage / Workshop Tuning screen
    /// (uGUI + TextMeshPro + LayoutGroups + AutoSizing to prevent text overlap).
    /// </summary>
    public static class GarageUiBuilder
    {
        public static GaragePanelWidgets BuildGaragePanel(
            Transform canvasTransform,
            UpgradeType[] upgradeTypes,
            Action onBackToLobby,
            Action onStartRace,
            Action onOpenSkinSelector,
            Action<UpgradeType> onBuyUpgrade,
            TMP_FontAsset fontOverride = null)
        {
            GameObject root = new GameObject("GaragePanel");
            root.transform.SetParent(canvasTransform, false);
            RectTransform rootRt = root.AddComponent<RectTransform>();
            LobbyUiBuilder.StretchFill(rootRt);

            Image backdrop = root.AddComponent<Image>();
            backdrop.color = new Color(0.03f, 0.04f, 0.06f, 0.96f);

            var widgets = new GaragePanelWidgets
            {
                RootPanel = root
            };

            BuildHeaderBar(root.transform, widgets, onBackToLobby, onStartRace, fontOverride);
            BuildTelemetryColumn(root.transform, widgets, onOpenSkinSelector, fontOverride);
            BuildUpgradeDeckColumn(root.transform, widgets, upgradeTypes, onBuyUpgrade, fontOverride);

            root.SetActive(false);
            return widgets;
        }

        private static void BuildHeaderBar(
            Transform parent,
            GaragePanelWidgets widgets,
            Action onBackToLobby,
            Action onStartRace,
            TMP_FontAsset fontOverride)
        {
            Image headerBar = LobbyUiBuilder.CreateCyberPanel(
                parent, "GarageHeaderBar", LobbyUiBuilder.SurfaceLowest, LobbyUiBuilder.ElectricCyan);
            LobbyUiBuilder.PlaceTop(headerBar.rectTransform, 0f, 1280f, 58f);
            headerBar.rectTransform.anchorMin = new Vector2(0f, 1f);
            headerBar.rectTransform.anchorMax = new Vector2(1f, 1f);
            headerBar.rectTransform.sizeDelta = new Vector2(0f, 58f);

            Button backBtn = LobbyUiBuilder.CreateCyberButton(
                headerBar.transform, "BackBtn", "[ESC] VỀ SẢNH CHÍNH",
                LobbyUiBuilder.SurfaceHigh, LobbyUiBuilder.TextPrimary, 12f, null, fontOverride);
            LobbyUiBuilder.PlaceTopLeft(backBtn.GetComponent<RectTransform>(), 24f, -10f, 182f, 38f);
            backBtn.onClick.AddListener(() => onBackToLobby?.Invoke());

            // Two stacked non-overlapping lines for Workshop title + subtitle
            TMP_Text headerTitle = LobbyUiBuilder.CreateTmpText(
                headerBar.transform, "GarageTitle", "GARAGE ĐỘ XE // WORKSHOP TUNING",
                20f, LobbyUiBuilder.ElectricCyan, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, fontOverride);
            headerTitle.enableAutoSizing = true;
            headerTitle.fontSizeMin = 14f;
            headerTitle.fontSizeMax = 20f;
            LobbyUiBuilder.PlaceTopLeft(headerTitle.rectTransform, 220f, -6f, 520f, 26f);

            TMP_Text headerSub = LobbyUiBuilder.CreateTmpText(
                headerBar.transform, "GarageSubtitle", "HỆ THỐNG NÂNG CẤP MÔ-ĐUN TÁC CHIẾN ĐƯỜNG PHỐ",
                10f, LobbyUiBuilder.SafetyAmber, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, fontOverride);
            headerSub.enableAutoSizing = true;
            headerSub.fontSizeMin = 8.5f;
            headerSub.fontSizeMax = 10.5f;
            LobbyUiBuilder.PlaceTopLeft(headerSub.rectTransform, 220f, -33f, 520f, 18f);

            Image walletPill = LobbyUiBuilder.CreateCyberPanel(
                headerBar.transform, "GarageWalletPill", LobbyUiBuilder.SurfaceHigh);
            LobbyUiBuilder.PlaceTopRight(walletPill.rectTransform, -244f, -10f, 212f, 38f);

            TMP_Text moneyTxt = LobbyUiBuilder.CreateTmpText(
                walletPill.transform, "GarageMoney", "200 XU",
                14f, LobbyUiBuilder.SafetyAmber, TextAlignmentOptions.Center, FontStyles.Bold, fontOverride);
            moneyTxt.enableAutoSizing = true;
            moneyTxt.fontSizeMin = 11f;
            moneyTxt.fontSizeMax = 15f;
            LobbyUiBuilder.StretchFill(moneyTxt.rectTransform, 10f, 2f);
            widgets.MoneyText = moneyTxt;

            Button raceBtn = LobbyUiBuilder.CreateCyberButton(
                headerBar.transform, "RaceNowBtn", "▶ VÀO ĐUA NGAY",
                LobbyUiBuilder.ElectricCyan, LobbyUiBuilder.OnCyan, 13f, null, fontOverride);
            LobbyUiBuilder.PlaceTopRight(raceBtn.GetComponent<RectTransform>(), -24f, -10f, 206f, 38f);
            raceBtn.onClick.AddListener(() => onStartRace?.Invoke());
        }

        private static void BuildTelemetryColumn(
            Transform parent,
            GaragePanelWidgets widgets,
            Action onOpenSkinSelector,
            TMP_FontAsset fontOverride)
        {
            Image leftCol = LobbyUiBuilder.CreateCyberPanel(
                parent, "GarageTelemetryCol", LobbyUiBuilder.SurfaceLow, LobbyUiBuilder.ElectricCyan);
            LobbyUiBuilder.PlaceTopLeft(leftCol.rectTransform, 28f, -74f, 484f, 618f);

            TMP_Text colHeader = LobbyUiBuilder.CreateTmpText(
                leftCol.transform, "TelemetryHeader", "● TELEMETRY CHIẾN MÃ",
                11f, LobbyUiBuilder.ElectricCyan, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, fontOverride);
            LobbyUiBuilder.PlaceTopLeft(colHeader.rectTransform, 18f, -12f, 280f, 20f);

            Button swapSkinBtn = LobbyUiBuilder.CreateCyberButton(
                leftCol.transform, "GarageSwapSkinBtn", "ĐỔI SKIN ⇄",
                LobbyUiBuilder.SurfaceHigh, LobbyUiBuilder.TextPrimary, 11f, null, fontOverride);
            LobbyUiBuilder.PlaceTopRight(swapSkinBtn.GetComponent<RectTransform>(), -18f, -10f, 118f, 28f);
            swapSkinBtn.onClick.AddListener(() => onOpenSkinSelector?.Invoke());

            TMP_Text brandTitle = LobbyUiBuilder.CreateTmpText(
                leftCol.transform, "GarageBikeTitle", "XANH SM",
                22f, LobbyUiBuilder.ElectricCyan, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, fontOverride);
            brandTitle.enableAutoSizing = true;
            brandTitle.fontSizeMin = 15f;
            brandTitle.fontSizeMax = 22f;
            LobbyUiBuilder.PlaceTopLeft(brandTitle.rectTransform, 18f, -38f, 448f, 28f);
            widgets.BikeBrandTitleText = brandTitle;

            TMP_Text brandTagline = LobbyUiBuilder.CreateTmpText(
                leftCol.transform, "GarageBikeTagline", "THUẦN ĐIỆN XANH CYAN",
                11f, LobbyUiBuilder.TextMuted, TextAlignmentOptions.MidlineLeft, FontStyles.Normal, fontOverride);
            LobbyUiBuilder.PlaceTopLeft(brandTagline.rectTransform, 18f, -66f, 448f, 18f);
            widgets.BikeTaglineText = brandTagline;

            Image previewBox = LobbyUiBuilder.CreateCyberPanel(
                leftCol.transform, "GarageBikePreviewBox", LobbyUiBuilder.SurfaceLowest);
            LobbyUiBuilder.PlaceTopLeft(previewBox.rectTransform, 18f, -92f, 448f, 196f);

            GameObject previewImgObj = new GameObject("GarageBikePreviewImg");
            previewImgObj.transform.SetParent(previewBox.transform, false);
            Image previewImg = previewImgObj.AddComponent<Image>();
            previewImg.preserveAspect = true;
            previewImg.raycastTarget = false;
            LobbyUiBuilder.StretchFill(previewImg.rectTransform, 4f, 4f);
            widgets.BikePreviewImage = previewImg;

            GameObject accentObj = new GameObject("GarageBikeAccentBar");
            accentObj.transform.SetParent(previewBox.transform, false);
            Image accentBar = accentObj.AddComponent<Image>();
            accentBar.raycastTarget = false;
            accentBar.color = LobbyUiBuilder.ElectricCyan;
            LobbyUiBuilder.PlaceBottomStretch(accentBar.rectTransform, 4f);
            widgets.BikeAccentBar = accentBar;

            // VerticalLayoutGroup for 4 telemetry stat bars (zero overlap guaranteed)
            GameObject statsContainer = new GameObject("StatBarsContainer");
            statsContainer.transform.SetParent(leftCol.transform, false);
            RectTransform statsRt = statsContainer.AddComponent<RectTransform>();
            LobbyUiBuilder.PlaceTopLeft(statsRt, 18f, -300f, 448f, 260f);

            VerticalLayoutGroup vlg = statsContainer.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 10f;
            vlg.padding = new RectOffset(0, 0, 4, 4);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = true;

            widgets.SpeedBarFill = CreateStatBarRow(
                statsContainer.transform, "MAX_SPD", "TỐC ĐỘ TỐI ĐA (MAX SPD)",
                LobbyUiBuilder.ElectricCyan, fontOverride, out TMP_Text spdVal);
            widgets.SpeedValueText = spdVal;

            widgets.PowerBarFill = CreateStatBarRow(
                statsContainer.transform, "POWER", "CÔNG SUẤT ĐỘNG CƠ (POWER)",
                LobbyUiBuilder.SafetyAmber, fontOverride, out TMP_Text pwrVal);
            widgets.PowerValueText = pwrVal;

            widgets.BrakeBarFill = CreateStatBarRow(
                statsContainer.transform, "BRAKE", "LỰC PHANH (BRAKE)",
                new Color(0.28f, 0.78f, 0.96f, 1f), fontOverride, out TMP_Text brkVal);
            widgets.BrakeValueText = brkVal;

            widgets.GripBarFill = CreateStatBarRow(
                statsContainer.transform, "GRIP", "ĐỘ BÁM ĐƯỜNG (GRIP)",
                LobbyUiBuilder.SuccessGreen, fontOverride, out TMP_Text grpVal);
            widgets.GripValueText = grpVal;

            Image summaryBox = LobbyUiBuilder.CreateCyberPanel(
                leftCol.transform, "StatsSummaryBox", LobbyUiBuilder.SurfaceLowest);
            LobbyUiBuilder.PlaceTopLeft(summaryBox.rectTransform, 18f, -572f, 448f, 32f);

            TMP_Text summaryTxt = LobbyUiBuilder.CreateTmpText(
                summaryBox.transform, "GarageStats", "",
                11f, LobbyUiBuilder.TextMuted, TextAlignmentOptions.Center, FontStyles.Bold, fontOverride);
            summaryTxt.enableAutoSizing = true;
            summaryTxt.fontSizeMin = 9f;
            summaryTxt.fontSizeMax = 11.5f;
            LobbyUiBuilder.StretchFill(summaryTxt.rectTransform, 8f, 2f);
            widgets.StatsSummaryText = summaryTxt;
        }

        private static Image CreateStatBarRow(
            Transform parent,
            string key,
            string displayLabel,
            Color fillColor,
            TMP_FontAsset fontOverride,
            out TMP_Text valueText)
        {
            GameObject rowObj = new GameObject($"StatRow_{key}");
            rowObj.transform.SetParent(parent, false);
            rowObj.AddComponent<RectTransform>();

            Image rowBg = LobbyUiBuilder.CreateCyberPanel(
                rowObj.transform, "RowBg", LobbyUiBuilder.SurfaceContainer);
            LobbyUiBuilder.StretchFill(rowBg.rectTransform);

            TMP_Text lbl = LobbyUiBuilder.CreateTmpText(
                rowObj.transform, $"Lbl_{key}", displayLabel,
                11f, LobbyUiBuilder.TextPrimary, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, fontOverride);
            lbl.enableAutoSizing = true;
            lbl.fontSizeMin = 9f;
            lbl.fontSizeMax = 11.5f;
            RectTransform lrt = lbl.rectTransform;
            lrt.anchorMin = new Vector2(0f, 0.45f);
            lrt.anchorMax = new Vector2(0.68f, 0.95f);
            lrt.offsetMin = new Vector2(10f, 0f);
            lrt.offsetMax = new Vector2(-4f, -2f);

            valueText = LobbyUiBuilder.CreateTmpText(
                rowObj.transform, $"Val_{key}", "0",
                12f, fillColor, TextAlignmentOptions.MidlineRight, FontStyles.Bold, fontOverride);
            valueText.enableAutoSizing = true;
            valueText.fontSizeMin = 10f;
            valueText.fontSizeMax = 13f;
            RectTransform vrt = valueText.rectTransform;
            vrt.anchorMin = new Vector2(0.68f, 0.45f);
            vrt.anchorMax = new Vector2(1f, 0.95f);
            vrt.offsetMin = new Vector2(4f, 0f);
            vrt.offsetMax = new Vector2(-10f, -2f);

            GameObject track = new GameObject($"Bar_{key}");
            track.transform.SetParent(rowObj.transform, false);
            Image trackImg = track.AddComponent<Image>();
            trackImg.color = LobbyUiBuilder.SurfaceLowest;
            trackImg.raycastTarget = false;
            RectTransform trt = trackImg.rectTransform;
            trt.anchorMin = new Vector2(0f, 0.14f);
            trt.anchorMax = new Vector2(1f, 0.40f);
            trt.offsetMin = new Vector2(10f, 0f);
            trt.offsetMax = new Vector2(-10f, 0f);

            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(track.transform, false);
            Image fillImg = fill.AddComponent<Image>();
            fillImg.color = fillColor;
            fillImg.raycastTarget = false;
            RectTransform frt = fillImg.rectTransform;
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = new Vector2(0.5f, 1f);
            frt.offsetMin = new Vector2(1f, 1f);
            frt.offsetMax = new Vector2(-1f, -1f);
            return fillImg;
        }

        private static void BuildUpgradeDeckColumn(
            Transform parent,
            GaragePanelWidgets widgets,
            UpgradeType[] upgradeTypes,
            Action<UpgradeType> onBuyUpgrade,
            TMP_FontAsset fontOverride)
        {
            GameObject rightCol = new GameObject("GarageUpgradeDeckCol");
            rightCol.transform.SetParent(parent, false);
            RectTransform rightRt = rightCol.AddComponent<RectTransform>();
            LobbyUiBuilder.PlaceTopLeft(rightRt, 528f, -74f, 724f, 618f);

            Image deckHeader = LobbyUiBuilder.CreateCyberPanel(
                rightCol.transform, "DeckHeader", LobbyUiBuilder.SurfaceLow, LobbyUiBuilder.SafetyAmber);
            LobbyUiBuilder.PlaceTopLeft(deckHeader.rectTransform, 0f, 0f, 724f, 44f);

            TMP_Text deckTitle = LobbyUiBuilder.CreateTmpText(
                deckHeader.transform, "DeckTitle", "HỆ THỐNG MÔ-ĐUN NÂNG CẤP (4 HẠNG MỤC)",
                13f, LobbyUiBuilder.ElectricCyan, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, fontOverride);
            deckTitle.enableAutoSizing = true;
            deckTitle.fontSizeMin = 10f;
            deckTitle.fontSizeMax = 13.5f;
            LobbyUiBuilder.PlaceTopLeft(deckTitle.rectTransform, 16f, -8f, 340f, 28f);

            Image feedbackBg = LobbyUiBuilder.CreateCyberPanel(
                deckHeader.transform, "FeedbackBanner", LobbyUiBuilder.SurfaceLowest);
            LobbyUiBuilder.PlaceTopRight(feedbackBg.rectTransform, -12f, -7f, 352f, 30f);

            TMP_Text feedbackTxt = LobbyUiBuilder.CreateTmpText(
                feedbackBg.transform, "Feedback", "",
                11f, LobbyUiBuilder.SafetyAmber, TextAlignmentOptions.Center, FontStyles.Bold, fontOverride);
            feedbackTxt.enableAutoSizing = true;
            feedbackTxt.fontSizeMin = 9f;
            feedbackTxt.fontSizeMax = 12f;
            LobbyUiBuilder.StretchFill(feedbackTxt.rectTransform, 8f, 2f);
            feedbackBg.gameObject.SetActive(false);

            widgets.FeedbackPanelBg = feedbackBg;
            widgets.FeedbackText = feedbackTxt;

            // 2x2 GridLayoutGroup preventing any overlap between the 4 modular cards
            GameObject gridObj = new GameObject("UpgradeCardsGrid");
            gridObj.transform.SetParent(rightCol.transform, false);
            RectTransform gridRt = gridObj.AddComponent<RectTransform>();
            LobbyUiBuilder.PlaceTopLeft(gridRt, 0f, -56f, 724f, 562f);

            GridLayoutGroup grid = gridObj.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(354f, 273f);
            grid.spacing = new Vector2(16f, 16f);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;

            for (int i = 0; i < upgradeTypes.Length; i++)
            {
                UpgradeCardWidgets card = BuildUpgradeCard(
                    gridObj.transform,
                    upgradeTypes[i],
                    onBuyUpgrade,
                    fontOverride);
                widgets.UpgradeCards.Add(card);
            }
        }

        private static UpgradeCardWidgets BuildUpgradeCard(
            Transform parent,
            UpgradeType type,
            Action<UpgradeType> onBuyUpgrade,
            TMP_FontAsset fontOverride)
        {
            Color accentColor = ResolveUpgradeAccent(type);
            Image cardBg = LobbyUiBuilder.CreateCyberPanel(
                parent, $"Card_{type}", LobbyUiBuilder.SurfaceContainer, accentColor);

            // Header row: Left title (NoWrap + AutoSize) + Right Level Badge (zero overlap)
            TMP_Text cardTitle = LobbyUiBuilder.CreateTmpText(
                cardBg.transform, "CardTitle", type.ToString().ToUpperInvariant(),
                17f, Color.white, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, fontOverride, wrap: false);
            cardTitle.enableAutoSizing = true;
            cardTitle.fontSizeMin = 12f;
            cardTitle.fontSizeMax = 17f;
            LobbyUiBuilder.PlaceTopLeft(cardTitle.rectTransform, 16f, -14f, 236f, 28f);

            Image lvBadgeBg = LobbyUiBuilder.CreateCyberPanel(
                cardBg.transform, "LevelBadge", LobbyUiBuilder.SurfaceHigh);
            LobbyUiBuilder.PlaceTopRight(lvBadgeBg.rectTransform, -16f, -15f, 76f, 26f);

            TMP_Text lvBadgeTxt = LobbyUiBuilder.CreateTmpText(
                lvBadgeBg.transform, "LevelBadgeTxt", "LV 0/5",
                11f, LobbyUiBuilder.SafetyAmber, TextAlignmentOptions.Center, FontStyles.Bold, fontOverride);
            lvBadgeTxt.enableAutoSizing = true;
            lvBadgeTxt.fontSizeMin = 9f;
            lvBadgeTxt.fontSizeMax = 11.5f;
            LobbyUiBuilder.StretchFill(lvBadgeTxt.rectTransform, 4f, 2f);

            TMP_Text tagSub = LobbyUiBuilder.CreateTmpText(
                cardBg.transform, "ModuleSlotTag", $"MÔ-ĐUN // {type.ToString().ToUpperInvariant()}",
                9.5f, accentColor, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, fontOverride);
            LobbyUiBuilder.PlaceTopLeft(tagSub.rectTransform, 16f, -44f, 322f, 16f);

            // Dedicated description box separated below subtitle
            TMP_Text cardEffect = LobbyUiBuilder.CreateTmpText(
                cardBg.transform, "CardEffect", "",
                12.5f, LobbyUiBuilder.TextMuted, TextAlignmentOptions.TopLeft, FontStyles.Normal, fontOverride, wrap: true);
            cardEffect.enableAutoSizing = true;
            cardEffect.fontSizeMin = 10f;
            cardEffect.fontSizeMax = 13f;
            LobbyUiBuilder.PlaceTopLeft(cardEffect.rectTransform, 16f, -66f, 322f, 58f);

            // 5 LED Level Pips inside HorizontalLayoutGroup
            GameObject pipsRow = new GameObject("LevelPipsRow");
            pipsRow.transform.SetParent(cardBg.transform, false);
            RectTransform pipsRt = pipsRow.AddComponent<RectTransform>();
            LobbyUiBuilder.PlaceTopLeft(pipsRt, 16f, -134f, 322f, 18f);

            HorizontalLayoutGroup hlg = pipsRow.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8f;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            UpgradeCardWidgets cardWidgets = new UpgradeCardWidgets
            {
                Type = type,
                CardBg = cardBg,
                TitleText = cardTitle,
                LevelBadgeBg = lvBadgeBg,
                LevelBadgeText = lvBadgeTxt,
                EffectText = cardEffect
            };

            for (int p = 0; p < CarUpgradeSystem.MaxUpgradeLevel; p++)
            {
                GameObject pip = new GameObject($"Pip_{p}");
                pip.transform.SetParent(pipsRow.transform, false);
                Image pipImg = pip.AddComponent<Image>();
                pipImg.raycastTarget = false;
                pipImg.color = LobbyUiBuilder.SurfaceLowest;
                cardWidgets.LevelPips.Add(pipImg);
            }

            // Divider line
            GameObject divObj = new GameObject("Divider");
            divObj.transform.SetParent(cardBg.transform, false);
            Image divImg = divObj.AddComponent<Image>();
            divImg.raycastTarget = false;
            divImg.color = LobbyUiBuilder.OutlineVariant;
            LobbyUiBuilder.PlaceTopLeft(divImg.rectTransform, 16f, -168f, 322f, 2f);

            // Bottom purchase row: Left Cost Box + Right Buy Button
            Image costBox = LobbyUiBuilder.CreateCyberPanel(
                cardBg.transform, "CostBox", LobbyUiBuilder.SurfaceLowest);
            LobbyUiBuilder.PlaceTopLeft(costBox.rectTransform, 16f, -186f, 122f, 54f);

            TMP_Text costHeader = LobbyUiBuilder.CreateTmpText(
                costBox.transform, "CostLabel", "CHI PHÍ",
                9f, LobbyUiBuilder.TextMuted, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, fontOverride);
            LobbyUiBuilder.PlaceTopLeft(costHeader.rectTransform, 10f, -6f, 102f, 14f);

            TMP_Text costTxt = LobbyUiBuilder.CreateTmpText(
                costBox.transform, "CostValue", "150 XU",
                14f, LobbyUiBuilder.SafetyAmber, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, fontOverride);
            costTxt.enableAutoSizing = true;
            costTxt.fontSizeMin = 10.5f;
            costTxt.fontSizeMax = 15f;
            LobbyUiBuilder.PlaceTopLeft(costTxt.rectTransform, 10f, -22f, 102f, 26f);
            cardWidgets.CostText = costTxt;

            Button buyBtn = LobbyUiBuilder.CreateCyberButton(
                cardBg.transform, "BuyBtn", "NÂNG CẤP",
                LobbyUiBuilder.ElectricCyan, LobbyUiBuilder.OnCyan, 13f, null, fontOverride);
            TMP_Text buyLabel = LobbyUiBuilder.GetPrimaryLabel(buyBtn);
            if (buyLabel != null)
            {
                buyLabel.enableAutoSizing = true;
                buyLabel.fontSizeMin = 10f;
                buyLabel.fontSizeMax = 13.5f;
            }
            LobbyUiBuilder.PlaceTopRight(buyBtn.GetComponent<RectTransform>(), -16f, -186f, 186f, 54f);
            buyBtn.onClick.AddListener(() => onBuyUpgrade?.Invoke(type));

            cardWidgets.BuyButton = buyBtn;
            cardWidgets.ButtonImg = buyBtn.GetComponent<Image>();
            cardWidgets.ButtonLabel = buyLabel;
            return cardWidgets;
        }

        private static Color ResolveUpgradeAccent(UpgradeType type)
        {
            return type switch
            {
                UpgradeType.Engine => LobbyUiBuilder.ElectricCyan,
                UpgradeType.Brakes => new Color(0.28f, 0.78f, 0.96f, 1f),
                UpgradeType.Grip => LobbyUiBuilder.SuccessGreen,
                UpgradeType.WeightReduction => LobbyUiBuilder.SafetyAmber,
                _ => LobbyUiBuilder.ElectricCyan
            };
        }
    }
}
