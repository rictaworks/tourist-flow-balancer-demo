using System;
using UnityEngine;
using UnityEngine.UI;

namespace TouristFlowBalancer.UI
{
    /// <summary>
    /// requirements.md 13章：タイトル画面。新規開始／同じシードで再挑戦（直前シードがあるとき）／
    /// 続きから（進行中データがあるとき）。ベストスコアとシード値を表示する。
    /// </summary>
    internal sealed class TitleScreen
    {
        public RectTransform Root { get; }

        public event Action StartNewRequested;
        public event Action RetrySameSeedRequested;
        public event Action ContinueRequested;

        private readonly Text _bestScoreText;
        private readonly Text _seedText;
        private readonly GameObject _retryButtonObject;
        private readonly GameObject _continueButtonObject;

        public TitleScreen(Transform parent)
        {
            Root = UIFactory.CreatePanel(parent, "TitleScreen", new Color(0.09f, 0.12f, 0.18f, 1f));
            UIFactory.Stretch(Root);

            var titleText = UIFactory.CreateText(
                Root, "GameTitle",
                "観光客を複数地域へ分散させるゲーム（デモ版）",
                32, Color.white);
            UIFactory.AnchorFraction(titleText.rectTransform, 0.1f, 0.72f, 0.9f, 0.92f);

            var subtitleText = UIFactory.CreateText(
                Root, "Subtitle",
                "14日間の観光客の流れを、予算内の施策で6地域へ分散させよう。",
                18, new Color(0.85f, 0.85f, 0.85f));
            UIFactory.AnchorFraction(subtitleText.rectTransform, 0.1f, 0.62f, 0.9f, 0.72f);

            _bestScoreText = UIFactory.CreateText(Root, "BestScore", "ベストスコア: -", 20, Color.white);
            UIFactory.AnchorFraction(_bestScoreText.rectTransform, 0.1f, 0.52f, 0.5f, 0.60f);

            _seedText = UIFactory.CreateText(Root, "SeedInfo", "直前のシード: なし", 20, Color.white);
            UIFactory.AnchorFraction(_seedText.rectTransform, 0.5f, 0.52f, 0.9f, 0.60f);

            var startButton = UIFactory.CreateButton(Root, "StartNewButton", "新規開始", new Color(0.2f, 0.55f, 0.3f), Color.white);
            UIFactory.AnchorFraction(startButton.GetComponent<RectTransform>(), 0.30f, 0.36f, 0.70f, 0.46f);
            startButton.onClick.AddListener(() => StartNewRequested?.Invoke());

            var retryButton = UIFactory.CreateButton(Root, "RetrySameSeedButton", "同じシードで再挑戦", new Color(0.2f, 0.4f, 0.6f), Color.white);
            UIFactory.AnchorFraction(retryButton.GetComponent<RectTransform>(), 0.30f, 0.24f, 0.70f, 0.34f);
            retryButton.onClick.AddListener(() => RetrySameSeedRequested?.Invoke());
            _retryButtonObject = retryButton.gameObject;

            var continueButton = UIFactory.CreateButton(Root, "ContinueButton", "続きから", new Color(0.55f, 0.45f, 0.15f), Color.white);
            UIFactory.AnchorFraction(continueButton.GetComponent<RectTransform>(), 0.30f, 0.12f, 0.70f, 0.22f);
            continueButton.onClick.AddListener(() => ContinueRequested?.Invoke());
            _continueButtonObject = continueButton.gameObject;
        }

        public void Refresh(int bestScore, int? lastSeed, bool hasSavedSeason)
        {
            _bestScoreText.text = "ベストスコア: " + bestScore;
            _seedText.text = lastSeed.HasValue ? "直前のシード: " + lastSeed.Value : "直前のシード: なし";
            _retryButtonObject.SetActive(lastSeed.HasValue);
            _continueButtonObject.SetActive(hasSavedSeason);
        }

        public void SetVisible(bool visible)
        {
            Root.gameObject.SetActive(visible);
        }
    }
}
