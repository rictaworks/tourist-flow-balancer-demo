using System;
using System.Text;
using TouristFlowBalancer.Core;
using TouristFlowBalancer.Logic;
using UnityEngine;
using UnityEngine.UI;

namespace TouristFlowBalancer.UI
{
    /// <summary>
    /// requirements.md 13章：シーズン結果画面。スコア、地域別の累計来訪者・累計収益・過密日数・閑散日数・
    /// 住民感情の最終値、施策別の使用回数、シード値、「同じシードで再挑戦」「タイトルへ」を表示する。
    /// </summary>
    internal sealed class ResultScreen
    {
        private static readonly string[] ColumnHeaders = { "地域", "累計来訪者", "累計収益", "過密日数", "閑散日数", "住民感情" };
        private static readonly float[] ColumnStops = { 0f, 0.20f, 0.40f, 0.58f, 0.74f, 0.90f, 1f };

        private readonly GameFlow _flow;

        public RectTransform Root { get; }

        public event Action RetryRequested;
        public event Action ToTitleRequested;

        private readonly Text _scoreText;
        private readonly Text[,] _cells;
        private readonly Text _measureUsageText;

        public ResultScreen(Transform parent, GameFlow flow)
        {
            _flow = flow;

            Root = UIFactory.CreatePanel(parent, "ResultScreen", new Color(0.12f, 0.10f, 0.08f, 1f));
            UIFactory.Stretch(Root);

            var title = UIFactory.CreateText(Root, "Title", "シーズン結果", 28, Color.white);
            UIFactory.AnchorFraction(title.rectTransform, 0.05f, 0.90f, 0.95f, 0.98f);

            _scoreText = UIFactory.CreateText(Root, "Score", string.Empty, 18, Color.white, TextAnchor.MiddleLeft);
            UIFactory.AnchorFraction(_scoreText.rectTransform, 0.05f, 0.80f, 0.95f, 0.89f);

            var headerRow = UIFactory.CreatePanel(Root, "HeaderRow", new Color(0.24f, 0.2f, 0.16f));
            UIFactory.AnchorFraction(headerRow, 0.05f, 0.70f, 0.95f, 0.77f);
            for (int c = 0; c < ColumnHeaders.Length; c++)
            {
                var head = UIFactory.CreateText(headerRow, "Head" + c, ColumnHeaders[c], 14, Color.white);
                UIFactory.AnchorFraction(head.rectTransform, ColumnStops[c], 0f, ColumnStops[c + 1], 1f);
            }

            _cells = new Text[Region.Count, ColumnHeaders.Length];
            float rowHeight = 0.46f / Region.Count;
            for (int r = 0; r < Region.Count; r++)
            {
                float yMax = 0.70f - r * rowHeight;
                float yMin = yMax - rowHeight;
                var rowPanel = UIFactory.CreatePanel(Root, "Row" + r, r % 2 == 0
                    ? new Color(0.18f, 0.15f, 0.12f)
                    : new Color(0.21f, 0.18f, 0.15f));
                UIFactory.AnchorFraction(rowPanel, 0.05f, yMin, 0.95f, yMax);

                for (int c = 0; c < ColumnHeaders.Length; c++)
                {
                    var cell = UIFactory.CreateText(rowPanel, "Cell" + c, string.Empty, 13, Color.white);
                    UIFactory.AnchorFraction(cell.rectTransform, ColumnStops[c], 0f, ColumnStops[c + 1], 1f);
                    _cells[r, c] = cell;
                }
            }

            _measureUsageText = UIFactory.CreateText(Root, "MeasureUsage", string.Empty, 12, Color.white, TextAnchor.UpperLeft);
            UIFactory.AnchorFraction(_measureUsageText.rectTransform, 0.05f, 0.14f, 0.95f, 0.22f);

            var retryButton = UIFactory.CreateButton(Root, "RetryButton", "同じシードで再挑戦", new Color(0.2f, 0.4f, 0.6f), Color.white);
            UIFactory.AnchorFraction(retryButton.GetComponent<RectTransform>(), 0.20f, 0.03f, 0.48f, 0.12f);
            retryButton.onClick.AddListener(() => RetryRequested?.Invoke());

            var titleButton = UIFactory.CreateButton(Root, "ToTitleButton", "タイトルへ", new Color(0.4f, 0.4f, 0.4f), Color.white);
            UIFactory.AnchorFraction(titleButton.GetComponent<RectTransform>(), 0.52f, 0.03f, 0.80f, 0.12f);
            titleButton.onClick.AddListener(() => ToTitleRequested?.Invoke());
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
            SeasonResult result = _flow.LastSeasonResult;
            if (result == null)
            {
                return;
            }

            _scoreText.text =
                $"スコア: {result.Score}（満足延べ人数 {result.SatisfiedTotal} − 断念者 {result.GiveUpTotal}） " +
                $"／ ベストスコア: {result.BestScore} ／ シード: {result.Seed}";

            foreach (var region in Region.All)
            {
                int idx = region.Id - 1;
                _cells[idx, 0].text = region.Name;
                _cells[idx, 1].text = result.VisitorsByRegion[idx].ToString();
                _cells[idx, 2].text = result.RevenueByRegion[idx].ToString();
                _cells[idx, 3].text = result.OvercrowdedDays[idx].ToString();
                _cells[idx, 4].text = result.DesertedDays[idx].ToString();
                _cells[idx, 5].text = result.FinalSentimentByRegion[idx].ToString();
            }

            var sb = new StringBuilder("施策別の使用回数: ");
            foreach (MeasureKind kind in Enum.GetValues(typeof(MeasureKind)))
            {
                sb.Append(Labels.MeasureShortName(kind)).Append('=').Append(result.UsesByMeasure[(int)kind]).Append("回  ");
            }
            _measureUsageText.text = sb.ToString();
        }
    }
}
