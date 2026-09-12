using TouristFlowBalancer.Logic;
using UnityEngine;

namespace TouristFlowBalancer.UI
{
    /// <summary>
    /// requirements.md 10章 classDiagram の UIPresenter。<see cref="GameFlow"/>の状態変化を購読し、
    /// 5画面（タイトル・計画・流入・日次レポート・シーズン結果）のうち現在の状態に対応する1つだけを
    /// 表示する。画面自体の構築・更新はUIPresenterが直接行わず、各画面クラス（<see cref="TitleScreen"/>等）に委譲する。
    /// </summary>
    public sealed class UIPresenter : MonoBehaviour
    {
        private GameFlow _flow;
        private TitleScreen _title;
        private PlanScreen _plan;
        private InflowScreen _inflow;
        private ReportScreen _report;
        private ResultScreen _result;

        public void Initialize(Transform screenParent, GameFlow flow)
        {
            _flow = flow;

            _title = new TitleScreen(screenParent);
            _plan = new PlanScreen(screenParent, flow);
            _inflow = InflowScreen.Create(screenParent, flow);
            _report = new ReportScreen(screenParent, flow);
            _result = new ResultScreen(screenParent, flow);

            _title.StartNewRequested += () => _flow.StartNewSeason();
            _title.RetrySameSeedRequested += () => _flow.RetrySameSeed();
            _title.ContinueRequested += () => _flow.ContinueSavedSeason();

            _result.RetryRequested += () => _flow.RetryFromResult();
            _result.ToTitleRequested += () => _flow.GoToTitle();

            _flow.StateChanged += OnStateChanged;

            _flow.Initialize(); // LOADING→TITLE（9.3節：リセット判定を経てタイトルへ）
        }

        private void OnStateChanged(GameFlowState state)
        {
            if (state == GameFlowState.Title)
            {
                _title.Refresh(_flow.BestScore, _flow.LastSeed, _flow.HasSavedSeason);
            }

            _title.SetVisible(state == GameFlowState.Title);
            _plan.SetVisible(state == GameFlowState.Planning);
            _inflow.SetVisible(state == GameFlowState.Inflow);
            _report.SetVisible(state == GameFlowState.Report);
            _result.SetVisible(state == GameFlowState.Result);
        }

        private void OnDestroy()
        {
            if (_flow != null)
            {
                _flow.StateChanged -= OnStateChanged;
            }
        }
    }
}
