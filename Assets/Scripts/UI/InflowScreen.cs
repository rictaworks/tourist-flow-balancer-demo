using System.Collections;
using System.Collections.Generic;
using TouristFlowBalancer.Core;
using TouristFlowBalancer.Logic;
using UnityEngine;
using UnityEngine.UI;

namespace TouristFlowBalancer.UI
{
    /// <summary>
    /// requirements.md 13章・5.9節・5.10節：流入画面。ハブから各地域へ人数に比例した数の点が移動する
    /// アニメーション（等速約8秒、スキップ可）。到着後、混雑率に応じた記号を表示してから日次レポートへ進む
    /// （<see cref="GameFlow.CompleteInflow"/>を呼ぶ）。
    ///
    /// 8秒のタイマーと点の移動はフレームごとの更新（Update）を要するため、他の画面と異なりMonoBehaviourとして
    /// 実装する（GameFlow自身はUnityEngineを参照できないLogicアセンブリにあるため、時間管理はUI側の責務とする）。
    /// </summary>
    internal sealed class InflowScreen : MonoBehaviour
    {
        private const float ArrivalHoldSeconds = 0.4f;
        private const int MaxDotsPerRegion = 24;
        private const float VisitorsPerDot = 40f;
        private const float JitterMagnitude = 0.025f;

        private GameFlow _flow;
        private RectTransform _mapPanel;
        private RectTransform _animationLayer;
        private Text _progressText;
        private RegionArrivalView[] _regionViews;
        private readonly List<Dot> _dots = new List<Dot>();

        private bool _skipRequested;

        private sealed class RegionArrivalView
        {
            public Text Name;
            public Text Status;
        }

        private struct Dot
        {
            public RectTransform Rect;
            public Vector2 Start;
            public Vector2 End;
        }

        public static InflowScreen Create(Transform parent, GameFlow flow)
        {
            var root = UIFactory.CreatePanel(parent, "InflowScreen", new Color(0.08f, 0.10f, 0.14f, 1f));
            UIFactory.Stretch(root);

            var screen = root.gameObject.AddComponent<InflowScreen>();
            screen.Initialize(flow, root);
            return screen;
        }

        private void Initialize(GameFlow flow, RectTransform root)
        {
            _flow = flow;

            var title = UIFactory.CreateText(root, "Title", "観光客が各地域へ向かっています…", 20, Color.white);
            UIFactory.AnchorFraction(title.rectTransform, 0.1f, 0.90f, 0.9f, 0.98f);

            _progressText = UIFactory.CreateText(root, "Progress", string.Empty, 16, Color.white);
            UIFactory.AnchorFraction(_progressText.rectTransform, 0.1f, 0.83f, 0.9f, 0.90f);

            _mapPanel = UIFactory.CreatePanel(root, "MapPanel", new Color(0.10f, 0.14f, 0.18f, 1f));
            UIFactory.AnchorFraction(_mapPanel, 0.10f, 0.14f, 0.90f, 0.80f);

            var hub = UIFactory.CreatePanel(_mapPanel, "Hub", new Color(0.7f, 0.7f, 0.2f));
            UIFactory.AnchorPointBox(hub, MapLayout.Hub, 0.08f, 0.07f);
            var hubLabel = UIFactory.CreateText(hub, "HubLabel", "ハブ", 12, Color.black);
            UIFactory.Stretch(hubLabel.rectTransform);

            _regionViews = BuildRegionViews(_mapPanel);

            _animationLayer = UIFactory.CreateRect(_mapPanel, "AnimationLayer");
            UIFactory.Stretch(_animationLayer);

            var skipButton = UIFactory.CreateButton(root, "SkipButton", "スキップ", new Color(0.4f, 0.2f, 0.2f), Color.white);
            UIFactory.AnchorFraction(skipButton.GetComponent<RectTransform>(), 0.40f, 0.03f, 0.60f, 0.11f);
            skipButton.onClick.AddListener(() => _skipRequested = true);
        }

        private static RegionArrivalView[] BuildRegionViews(Transform mapPanel)
        {
            var views = new RegionArrivalView[Region.Count];
            foreach (var region in Region.All)
            {
                var panel = UIFactory.CreatePanel(mapPanel, "Arrival" + region.Id, new Color(0.2f, 0.24f, 0.28f));
                UIFactory.AnchorPointBox(panel, MapLayout.RegionPosition(region.Id), 0.20f, 0.16f);

                var name = UIFactory.CreateText(panel, "Name", region.Name, 12, Color.white);
                UIFactory.AnchorFraction(name.rectTransform, 0.02f, 0.55f, 0.98f, 1f);

                var status = UIFactory.CreateText(panel, "Status", "移動中…", 12, Color.white);
                UIFactory.AnchorFraction(status.rectTransform, 0.02f, 0f, 0.98f, 0.55f);

                views[region.Id - 1] = new RegionArrivalView { Name = name, Status = status };
            }
            return views;
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
            if (visible)
            {
                BeginAnimation();
            }
        }

        private void BeginAnimation()
        {
            ClearDots();
            foreach (var view in _regionViews)
            {
                view.Status.text = "移動中…";
            }

            Allocation allocation = _flow.PendingAllocation;
            if (allocation != null)
            {
                foreach (var region in Region.All)
                {
                    int visitors = allocation.VisitorsByRegion[region.Id - 1];
                    SpawnDots(region.Id, visitors);
                }
            }

            _skipRequested = false;
            StopAllCoroutines();
            StartCoroutine(RunAnimation());
        }

        private void SpawnDots(int regionId, int visitors)
        {
            if (visitors <= 0)
            {
                return;
            }

            int count = Mathf.Clamp(Mathf.RoundToInt(visitors / VisitorsPerDot), 1, MaxDotsPerRegion);
            Vector2 start = MapLayout.Hub;
            Vector2 end = MapLayout.RegionPosition(regionId);

            for (int i = 0; i < count; i++)
            {
                var dotPanel = UIFactory.CreatePanel(_animationLayer, "Dot", new Color(0.95f, 0.85f, 0.3f));
                Vector2 jitter = new Vector2(
                    Random.Range(-JitterMagnitude, JitterMagnitude),
                    Random.Range(-JitterMagnitude, JitterMagnitude));
                UIFactory.AnchorPoint(dotPanel, start + jitter, new Vector2(7f, 7f));

                _dots.Add(new Dot { Rect = dotPanel, Start = start + jitter, End = end + jitter });
            }
        }

        private void ClearDots()
        {
            foreach (var dot in _dots)
            {
                if (dot.Rect != null)
                {
                    Destroy(dot.Rect.gameObject);
                }
            }
            _dots.Clear();
        }

        private IEnumerator RunAnimation()
        {
            float elapsed = 0f;
            float duration = GameConstants.InflowAnimationSeconds;

            while (elapsed < duration && !_skipRequested)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                ApplyProgress(t);
                yield return null;
            }

            ApplyProgress(1f);
            ShowArrivalState();

            yield return new WaitForSeconds(ArrivalHoldSeconds);

            _flow.CompleteInflow();
        }

        private void ApplyProgress(float t)
        {
            foreach (var dot in _dots)
            {
                if (dot.Rect == null)
                {
                    continue;
                }
                Vector2 pos = Vector2.Lerp(dot.Start, dot.End, t);
                dot.Rect.anchorMin = pos;
                dot.Rect.anchorMax = pos;
            }

            float duration = GameConstants.InflowAnimationSeconds;
            _progressText.text = $"{(t * duration):0.0} / {duration:0.0} 秒（スキップ可）";
        }

        private void ShowArrivalState()
        {
            Allocation allocation = _flow.PendingAllocation;
            Season season = _flow.Season;
            if (allocation == null || season == null)
            {
                return;
            }

            foreach (var region in Region.All)
            {
                int idx = region.Id - 1;
                var state = season.GetRegionState(region.Id);
                int visitors = allocation.VisitorsByRegion[idx];
                float congestion = state.Closed ? 0f : (float)visitors / region.Capacity;
                string symbol = CongestionPresentation.Symbol(state.Closed, congestion);
                string label = CongestionPresentation.Label(state.Closed, congestion);

                _regionViews[idx].Status.text = state.Closed
                    ? $"休止 {symbol}"
                    : $"{visitors}人 {CongestionPresentation.FormatPercent(congestion)} {symbol}({label})";
            }
        }

        private void OnDestroy()
        {
            ClearDots();
        }
    }
}
