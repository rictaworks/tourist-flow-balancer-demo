using TouristFlowBalancer.Core;
using TouristFlowBalancer.Logic;
using UnityEngine;
using UnityEngine.UI;

namespace TouristFlowBalancer.UI
{
    /// <summary>
    /// requirements.md 13章：日次レポート画面。地域別の来訪者数・混雑率・満足度・収益・住民感情の変化、
    /// 断念者数、今日の満足延べ人数を表示する。予測（確定前の配分予測）と実績の差を併記する。
    /// </summary>
    internal sealed class ReportScreen
    {
        private static readonly string[] ColumnHeaders = { "地域", "来訪者(予測差)", "混雑率", "満足度", "収益", "住民感情" };
        private static readonly float[] ColumnStops = { 0f, 0.20f, 0.44f, 0.62f, 0.76f, 0.90f, 1f };

        private readonly GameFlow _flow;

        public RectTransform Root { get; }

        private readonly Text _titleText;
        private readonly Text _summaryText;
        private readonly Text[,] _cells; // [region, column]

        public ReportScreen(Transform parent, GameFlow flow)
        {
            _flow = flow;

            Root = UIFactory.CreatePanel(parent, "ReportScreen", new Color(0.10f, 0.10f, 0.14f, 1f));
            UIFactory.Stretch(Root);

            _titleText = UIFactory.CreateText(Root, "Title", string.Empty, 24, Color.white);
            UIFactory.AnchorFraction(_titleText.rectTransform, 0.05f, 0.90f, 0.95f, 0.98f);

            var headerRow = UIFactory.CreatePanel(Root, "HeaderRow", new Color(0.2f, 0.2f, 0.24f));
            UIFactory.AnchorFraction(headerRow, 0.05f, 0.80f, 0.95f, 0.87f);
            for (int c = 0; c < ColumnHeaders.Length; c++)
            {
                var head = UIFactory.CreateText(headerRow, "Head" + c, ColumnHeaders[c], 14, Color.white);
                UIFactory.AnchorFraction(head.rectTransform, ColumnStops[c], 0f, ColumnStops[c + 1], 1f);
            }

            _cells = new Text[Region.Count, ColumnHeaders.Length];
            float rowHeight = 0.66f / Region.Count;
            for (int r = 0; r < Region.Count; r++)
            {
                float yMax = 0.80f - r * rowHeight;
                float yMin = yMax - rowHeight;
                var rowPanel = UIFactory.CreatePanel(Root, "Row" + r, r % 2 == 0
                    ? new Color(0.14f, 0.14f, 0.18f)
                    : new Color(0.17f, 0.17f, 0.21f));
                UIFactory.AnchorFraction(rowPanel, 0.05f, yMin, 0.95f, yMax);

                for (int c = 0; c < ColumnHeaders.Length; c++)
                {
                    var cell = UIFactory.CreateText(rowPanel, "Cell" + c, string.Empty, 13, Color.white);
                    UIFactory.AnchorFraction(cell.rectTransform, ColumnStops[c], 0f, ColumnStops[c + 1], 1f);
                    _cells[r, c] = cell;
                }
            }

            _summaryText = UIFactory.CreateText(Root, "Summary", string.Empty, 16, Color.white, TextAnchor.MiddleLeft);
            UIFactory.AnchorFraction(_summaryText.rectTransform, 0.05f, 0.08f, 0.70f, 0.14f);

            var closeButton = UIFactory.CreateButton(Root, "CloseButton", "閉じる（次へ）", new Color(0.2f, 0.45f, 0.25f), Color.white);
            UIFactory.AnchorFraction(closeButton.GetComponent<RectTransform>(), 0.72f, 0.03f, 0.95f, 0.14f);
            closeButton.onClick.AddListener(() => _flow.CloseReport());
        }

        public void SetVisible(bool visible)
        {
            Root.gameObject.SetActive(visible);
            if (visible)
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            DayResult result = _flow.LastDayResult;
            Allocation forecast = _flow.CurrentForecast;
            Season season = _flow.Season;
            if (result == null || season == null)
            {
                return;
            }

            _titleText.text = $"日次レポート - {result.Day}日目 / {GameConstants.SeasonLengthDays}";

            foreach (var region in Region.All)
            {
                int idx = region.Id - 1;
                RegionResult regionResult = result.Regions[idx];
                var state = season.GetRegionState(region.Id);
                bool closed = state.Closed;

                int predicted = forecast != null ? forecast.VisitorsByRegion[idx] : regionResult.Visitors;
                int diff = regionResult.Visitors - predicted;
                string diffText = diff == 0 ? "±0" : (diff > 0 ? "+" + diff : diff.ToString());

                _cells[idx, 0].text = region.Name;
                _cells[idx, 1].text = closed ? "0（休止）" : $"{regionResult.Visitors}人（予測比{diffText}）";
                _cells[idx, 2].text = closed
                    ? "休"
                    : $"{CongestionPresentation.FormatPercent(regionResult.Congestion)} {CongestionPresentation.Symbol(false, regionResult.Congestion)}";
                _cells[idx, 3].text = closed ? "-" : regionResult.Satisfaction.ToString();
                _cells[idx, 4].text = closed ? "0" : regionResult.Revenue.ToString();
                string sentimentSign = regionResult.SentimentDelta > 0 ? "+" : string.Empty;
                _cells[idx, 5].text = closed ? "±0" : sentimentSign + regionResult.SentimentDelta + (state.Backlash ? "(反発)" : string.Empty);
            }

            _summaryText.text =
                $"本日の満足延べ人数: {result.SatisfiedTotal} / 断念者: {result.GiveUps}";
        }
    }
}
