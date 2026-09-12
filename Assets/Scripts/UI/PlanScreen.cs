using System.Text;
using TouristFlowBalancer.Core;
using TouristFlowBalancer.Logic;
using UnityEngine;
using UnityEngine.UI;

namespace TouristFlowBalancer.UI
{
    /// <summary>
    /// requirements.md 13章：計画（地図）画面。中央ハブと6地域の簡易地図、上部の状況バー、
    /// 右側の施策パネル（施策×地域の選択・費用・合計・検証結果）、下部の「おまかせ」「確定」。
    /// 施策の追加・削除のたびに<see cref="GameFlow.PlanUpdated"/>を購読して即時再描画する（F3・F4）。
    /// </summary>
    internal sealed class PlanScreen
    {
        // 施策パネルのグリッドに並べる、地域単位で選ぶ5施策（L1混雑情報の掲示は全地域対象のため別行）。
        private static readonly MeasureKind[] PerRegionMeasures =
        {
            MeasureKind.Coupon,
            MeasureKind.Promotion,
            MeasureKind.ShuttleBus,
            MeasureKind.EventHosting,
            MeasureKind.EntryLimit,
        };

        private readonly GameFlow _flow;

        public RectTransform Root { get; }

        // 上部情報バー
        private readonly Text _dayText;
        private readonly Text _calendarText;
        private readonly Text _todayEventText;
        private readonly Text _tomorrowEventText;
        private readonly Text _budgetText;

        // 地図（地域ノード）
        private readonly RegionNodeView[] _regionNodes;

        // 施策パネル
        private readonly Text _costSummaryText;
        private readonly Text _crowdInfoCellText;
        private readonly Text _validationText;
        private readonly Text[,] _gridCells; // [measureIndex, regionIndex]

        private sealed class RegionNodeView
        {
            public Text Name;
            public Text Visitors;
            public Text Congestion;
            public Text Awareness;
            public Text Sentiment;
        }

        public PlanScreen(Transform parent, GameFlow flow)
        {
            _flow = flow;

            Root = UIFactory.CreatePanel(parent, "PlanScreen", new Color(0.12f, 0.14f, 0.10f, 1f));
            UIFactory.Stretch(Root);

            var topBar = UIFactory.CreatePanel(Root, "TopBar", new Color(0.05f, 0.05f, 0.05f, 0.6f));
            UIFactory.AnchorFraction(topBar, 0f, 0.90f, 1f, 1f);
            _dayText = AddTopBarField(topBar, "DayText", 0.00f, 0.20f);
            _calendarText = AddTopBarField(topBar, "CalendarText", 0.20f, 0.38f);
            _todayEventText = AddTopBarField(topBar, "TodayEventText", 0.38f, 0.62f);
            _tomorrowEventText = AddTopBarField(topBar, "TomorrowEventText", 0.62f, 0.86f);
            _budgetText = AddTopBarField(topBar, "BudgetText", 0.86f, 1.00f);

            var mapPanel = UIFactory.CreatePanel(Root, "MapPanel", new Color(0.10f, 0.16f, 0.12f, 1f));
            UIFactory.AnchorFraction(mapPanel, 0.00f, 0.08f, 0.62f, 0.90f);
            BuildHub(mapPanel);
            _regionNodes = BuildRegionNodes(mapPanel);

            var measurePanel = UIFactory.CreatePanel(Root, "MeasurePanel", new Color(0.08f, 0.10f, 0.16f, 1f));
            UIFactory.AnchorFraction(measurePanel, 0.64f, 0.08f, 1.00f, 0.90f);
            _costSummaryText = BuildMeasureHeader(measurePanel);
            _crowdInfoCellText = BuildCrowdInfoRow(measurePanel);
            _validationText = BuildValidationArea(measurePanel);
            _gridCells = BuildMeasureGrid(measurePanel);

            var bottomBar = UIFactory.CreatePanel(Root, "BottomBar", new Color(0.05f, 0.05f, 0.05f, 0.6f));
            UIFactory.AnchorFraction(bottomBar, 0f, 0f, 1f, 0.08f);
            var autoButton = UIFactory.CreateButton(bottomBar, "AutoPlanButton", "おまかせ", new Color(0.45f, 0.45f, 0.20f), Color.white);
            UIFactory.AnchorFraction(autoButton.GetComponent<RectTransform>(), 0.30f, 0.15f, 0.48f, 0.85f);
            autoButton.onClick.AddListener(() => _flow.ApplyAutoPlan());

            var confirmButton = UIFactory.CreateButton(bottomBar, "ConfirmButton", "確定", new Color(0.20f, 0.45f, 0.25f), Color.white);
            UIFactory.AnchorFraction(confirmButton.GetComponent<RectTransform>(), 0.52f, 0.15f, 0.70f, 0.85f);
            confirmButton.onClick.AddListener(() => _flow.Confirm());

            _flow.PlanUpdated += Refresh;
        }

        public void SetVisible(bool visible)
        {
            Root.gameObject.SetActive(visible);
        }

        private static Text AddTopBarField(Transform parent, string name, float xMin, float xMax)
        {
            var text = UIFactory.CreateText(parent, name, string.Empty, 15, Color.white, TextAnchor.MiddleLeft);
            UIFactory.AnchorFraction(text.rectTransform, xMin, 0f, xMax, 1f);
            return text;
        }

        private static void BuildHub(Transform mapPanel)
        {
            var hub = UIFactory.CreatePanel(mapPanel, "Hub", new Color(0.7f, 0.7f, 0.2f));
            UIFactory.AnchorPointBox(hub, MapLayout.Hub, 0.10f, 0.09f);
            var label = UIFactory.CreateText(hub, "HubLabel", "ハブ", 14, Color.black);
            UIFactory.Stretch(label.rectTransform);
        }

        private static RegionNodeView[] BuildRegionNodes(Transform mapPanel)
        {
            var nodes = new RegionNodeView[Region.Count];
            foreach (var region in Region.All)
            {
                int idx = region.Id - 1;
                var panel = UIFactory.CreatePanel(mapPanel, "Region" + region.Id, new Color(0.2f, 0.3f, 0.22f));
                UIFactory.AnchorPointBox(panel, MapLayout.RegionPosition(region.Id), 0.22f, 0.20f);

                var view = new RegionNodeView
                {
                    Name = CreateRow(panel, "Name", region.Name, 0.80f, 1.00f),
                    Visitors = CreateRow(panel, "Visitors", string.Empty, 0.58f, 0.80f),
                    Congestion = CreateRow(panel, "Congestion", string.Empty, 0.36f, 0.58f),
                    Awareness = CreateRow(panel, "Awareness", string.Empty, 0.18f, 0.36f),
                    Sentiment = CreateRow(panel, "Sentiment", string.Empty, 0.00f, 0.18f),
                };
                nodes[idx] = view;
            }
            return nodes;
        }

        private static Text CreateRow(Transform parent, string name, string content, float yMin, float yMax)
        {
            var text = UIFactory.CreateText(parent, name, content, 12, Color.white);
            UIFactory.AnchorFraction(text.rectTransform, 0.02f, yMin, 0.98f, yMax);
            return text;
        }

        private static Text BuildMeasureHeader(Transform measurePanel)
        {
            var title = UIFactory.CreateText(measurePanel, "MeasureTitle", "施策", 18, Color.white);
            UIFactory.AnchorFraction(title.rectTransform, 0.02f, 0.94f, 0.5f, 1.0f);

            var cost = UIFactory.CreateText(measurePanel, "CostSummary", string.Empty, 14, Color.white, TextAnchor.MiddleRight);
            UIFactory.AnchorFraction(cost.rectTransform, 0.5f, 0.94f, 0.98f, 1.0f);
            return cost;
        }

        private Text BuildCrowdInfoRow(Transform measurePanel)
        {
            var row = UIFactory.CreatePanel(measurePanel, "CrowdInfoRow", new Color(0.16f, 0.20f, 0.30f));
            UIFactory.AnchorFraction(row, 0.02f, 0.86f, 0.98f, 0.93f);

            var label = UIFactory.CreateText(row, "Label", Labels.MeasureName(MeasureKind.CrowdInfo) + $"（全地域, {MeasureCatalog.CostOf(MeasureKind.CrowdInfo)}）", 13, Color.white, TextAnchor.MiddleLeft);
            UIFactory.AnchorFraction(label.rectTransform, 0.02f, 0f, 0.72f, 1f);

            var cellText = UIFactory.CreateText(row, "State", string.Empty, 14, Color.yellow);
            UIFactory.AnchorFraction(cellText.rectTransform, 0.72f, 0f, 1f, 1f);

            var button = UIFactory.AddButtonBehaviour(row);
            button.onClick.AddListener(ToggleCrowdInfo);
            return cellText;
        }

        private Text BuildValidationArea(Transform measurePanel)
        {
            var panel = UIFactory.CreatePanel(measurePanel, "ValidationArea", new Color(0.05f, 0.05f, 0.05f, 0.4f));
            UIFactory.AnchorFraction(panel, 0.02f, 0.50f, 0.98f, 0.85f);
            var text = UIFactory.CreateText(panel, "ValidationText", string.Empty, 11, Color.white, TextAnchor.UpperLeft);
            UIFactory.Stretch(text.rectTransform);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private Text[,] BuildMeasureGrid(Transform measurePanel)
        {
            var header = UIFactory.CreatePanel(measurePanel, "GridHeader", new Color(0, 0, 0, 0));
            UIFactory.AnchorFraction(header, 0.02f, 0.44f, 0.98f, 0.49f);
            for (int i = 0; i < Region.Count; i++)
            {
                var head = UIFactory.CreateText(header, "Head" + (i + 1), (i + 1).ToString(), 12, Color.white);
                float xMin = 0.34f + i * (0.66f / Region.Count);
                float xMax = 0.34f + (i + 1) * (0.66f / Region.Count);
                UIFactory.AnchorFraction(head.rectTransform, xMin, 0f, xMax, 1f);
            }

            var cells = new Text[PerRegionMeasures.Length, Region.Count];
            float rowHeight = 0.44f / PerRegionMeasures.Length;

            for (int m = 0; m < PerRegionMeasures.Length; m++)
            {
                MeasureKind kind = PerRegionMeasures[m];
                float yMax = 0.44f - m * rowHeight;
                float yMin = yMax - rowHeight;

                var rowPanel = UIFactory.CreatePanel(measurePanel, "Row" + kind, new Color(0, 0, 0, 0));
                UIFactory.AnchorFraction(rowPanel, 0.02f, yMin, 0.98f, yMax);

                var label = UIFactory.CreateText(rowPanel, "RowLabel", Labels.MeasureShortName(kind) + $"({MeasureCatalog.CostOf(kind)})", 11, Color.white, TextAnchor.MiddleLeft);
                UIFactory.AnchorFraction(label.rectTransform, 0f, 0f, 0.34f, 1f);

                for (int r = 0; r < Region.Count; r++)
                {
                    int regionId = r + 1;
                    float xMin = 0.34f + r * (0.66f / Region.Count);
                    float xMax = 0.34f + (r + 1) * (0.66f / Region.Count);

                    var cellPanel = UIFactory.CreatePanel(rowPanel, "Cell" + regionId, new Color(0.2f, 0.2f, 0.2f));
                    UIFactory.AnchorFraction(cellPanel, xMin + 0.005f, 0.08f, xMax - 0.005f, 0.92f);
                    var cellButton = UIFactory.AddButtonBehaviour(cellPanel);

                    var cellText = UIFactory.CreateText(cellPanel, "State", string.Empty, 12, Color.white);
                    UIFactory.Stretch(cellText.rectTransform);

                    MeasureKind capturedKind = kind;
                    int capturedRegionId = regionId;
                    cellButton.onClick.AddListener(() => ToggleMeasure(capturedKind, capturedRegionId));

                    cells[m, r] = cellText;
                }
            }

            return cells;
        }

        private void ToggleMeasure(MeasureKind kind, int regionId)
        {
            if (_flow.HasMeasure(kind, regionId))
            {
                _flow.RemoveMeasure(kind, regionId);
            }
            else
            {
                _flow.AddMeasure(kind, regionId);
            }
        }

        private void ToggleCrowdInfo()
        {
            if (_flow.HasMeasure(MeasureKind.CrowdInfo, Measure.AllRegionsTarget))
            {
                _flow.RemoveMeasure(MeasureKind.CrowdInfo, Measure.AllRegionsTarget);
            }
            else
            {
                _flow.AddMeasure(MeasureKind.CrowdInfo, Measure.AllRegionsTarget);
            }
        }

        private void Refresh()
        {
            Season season = _flow.Season;
            if (season == null)
            {
                return;
            }

            _dayText.text = $"{season.Day}日目 / {GameConstants.SeasonLengthDays}";
            _calendarText.text = season.IsWeekend(season.Day) ? "週末" : "平日";
            _todayEventText.text = "今日: " + Labels.EventName(_flow.TodayEvent.Kind);
            _tomorrowEventText.text = _flow.TomorrowEvent != null
                ? "明日: " + Labels.EventName(_flow.TomorrowEvent.Kind)
                : "明日: -（最終日）";
            _budgetText.text = "予算: " + season.Budget;

            Allocation forecast = _flow.CurrentForecast;
            foreach (var region in Region.All)
            {
                int idx = region.Id - 1;
                var state = season.GetRegionState(region.Id);
                var view = _regionNodes[idx];

                int predicted = forecast != null ? forecast.VisitorsByRegion[idx] : 0;
                float congestion = state.Closed ? 0f : (float)predicted / region.Capacity;

                view.Visitors.text = $"予測 {predicted}/{region.Capacity}";
                view.Congestion.text = state.Closed
                    ? "休止中"
                    : $"混雑 {CongestionPresentation.FormatPercent(congestion)} {CongestionPresentation.Symbol(false, congestion)}({CongestionPresentation.Label(false, congestion)})";
                view.Awareness.text = "認知度 " + state.Awareness;
                view.Sentiment.text = "住民感情 " + state.Sentiment + (state.Backlash ? "（反発）" : string.Empty);
            }

            bool crowdInfoOn = _flow.HasMeasure(MeasureKind.CrowdInfo, Measure.AllRegionsTarget);
            _crowdInfoCellText.text = crowdInfoOn ? "済" : "-";

            for (int m = 0; m < PerRegionMeasures.Length; m++)
            {
                MeasureKind kind = PerRegionMeasures[m];
                for (int r = 0; r < Region.Count; r++)
                {
                    int regionId = r + 1;
                    bool on = _flow.HasMeasure(kind, regionId);
                    _gridCells[m, r].text = on ? "✓" : "-";
                }
            }

            int totalCost = _flow.CurrentPlan.TotalCost();
            _costSummaryText.text = $"合計費用: {totalCost} / 予算 {season.Budget}";

            _validationText.text = BuildValidationText(_flow.CurrentValidation);
        }

        private static string BuildValidationText(ValidationReport report)
        {
            if (report == null)
            {
                return string.Empty;
            }
            if (report.Errors.Count == 0 && report.Warnings.Count == 0 && report.Advices.Count == 0)
            {
                return "問題なし。確定できます。";
            }

            var sb = new StringBuilder();
            foreach (var error in report.Errors)
            {
                sb.AppendLine("[エラー] " + error);
            }
            foreach (var warning in report.Warnings)
            {
                sb.AppendLine("[警告] " + warning);
            }
            foreach (var advice in report.Advices)
            {
                sb.AppendLine("[助言] " + advice);
            }
            return sb.ToString();
        }
    }
}
