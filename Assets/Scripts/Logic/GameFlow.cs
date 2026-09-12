using System;
using TouristFlowBalancer.Core;
using TouristFlowBalancer.Infra;

namespace TouristFlowBalancer.Logic
{
    /// <summary>
    /// requirements.md 11.1節：ゲーム全体の状態遷移。
    /// シーズン自体の状態（<see cref="SeasonState"/>：計画中／流入中／レポート／精算）とは別に、
    /// LOADING・TITLE・RESULTを含むゲーム全体の進行を表す。
    /// </summary>
    public enum GameFlowState
    {
        Loading,
        Title,
        Planning,
        Inflow,
        Report,
        Result,
    }

    /// <summary>
    /// requirements.md 11.1節（状態遷移）・9.1〜9.3節（シーケンス）・10章 classDiagram の GameFlow。
    ///
    /// 配置についての注記：Issue #6 の編集範囲は本クラスを"Assets/Scripts/Infra/GameFlow.cs"に
    /// 置くことを想定しているが、実装するとInfra.asmdefがLogic.asmdefを参照する必要が生じ、
    /// 既にLogic.asmdefがInfra.asmdefを参照している（Allocator等がPersistenceを使うため）ため
    /// 循環参照になりUnityがビルドを拒否する。この問題は<see cref="SeasonFactory"/>が
    /// Core→Logicへ配置を変えた際と同型であり（同ファイルの配置コメント参照）、同じ理由で
    /// 本クラスもLogicアセンブリ（Assets/Scripts/Logic/）に配置する
    /// （Infra→Core、Logic→Core、Logic→Infraの単方向のみで循環なし）。
    ///
    /// UI層（<c>TouristFlowBalancer.UI</c>）はこのクラスの状態変化イベント（<see cref="StateChanged"/>）
    /// とプラン更新イベント（<see cref="PlanUpdated"/>）を購読し、公開プロパティ（<see cref="Season"/>・
    /// <see cref="CurrentPlan"/>・<see cref="CurrentForecast"/>・<see cref="CurrentValidation"/>・
    /// <see cref="LastDayResult"/>・<see cref="LastSeasonResult"/>）から表示内容を読み出す。
    /// UIはロジック層のクラス（Allocator・PlanValidator等）を直接呼ばず、必ず本クラスのメソッド経由で操作する。
    /// </summary>
    public sealed class GameFlow
    {
        private readonly Persistence _persistence;
        private readonly Allocator _allocator = new Allocator();
        private readonly PlanValidator _validator = new PlanValidator();
        private readonly DayEvaluator _evaluator = new DayEvaluator();
        private readonly DayAdvancer _advancer = new DayAdvancer();
        private readonly SeasonFinalizer _finalizer = new SeasonFinalizer();
        private readonly AutoPlanner _autoPlanner = new AutoPlanner();

        public GameFlowState State { get; private set; } = GameFlowState.Loading;
        public Season Season { get; private set; }
        public Plan CurrentPlan { get; private set; }

        /// <summary>
        /// 現在のプランでの配分予測（基準来訪数、揺らぎなし。関数C）。PLANNING中は施策の変更ごとに更新する。
        /// 確定（<see cref="Confirm"/>）の時点の値は、そのままINFLOW・REPORT中も保持し続け、
        /// 日次レポート画面が「予測と実績の差」（13章）を表示するのに使う（次のPLANNINGで上書きされる）。
        /// </summary>
        public Allocation CurrentForecast { get; private set; }

        public ValidationReport CurrentValidation { get; private set; }

        /// <summary>確定時に実際の来訪数で計算した配分（関数C、同一関数）。流入アニメーション・日次評価に使う。</summary>
        public Allocation PendingAllocation { get; private set; }

        public DayResult LastDayResult { get; private set; }
        public SeasonResult LastSeasonResult { get; private set; }

        /// <summary>ゲーム全体の状態が変わるたびに通知する。</summary>
        public event Action<GameFlowState> StateChanged;

        /// <summary>PLANNING中、プラン・配分予測・検証結果が更新されるたびに通知する。</summary>
        public event Action PlanUpdated;

        public GameFlow(Persistence persistence)
        {
            _persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        }

        public EventDay TodayEvent => Season != null ? Season.Events[Season.Day - 1] : null;

        /// <summary>明日の予報（最終日には存在しない。5.5節手順6「最終日の予報はなし」）。</summary>
        public EventDay TomorrowEvent =>
            Season != null && Season.Day < GameConstants.SeasonLengthDays
                ? Season.Events[Season.Day]
                : null;

        public int? LastSeed => _persistence.LoadLastSeed();
        public int BestScore => _persistence.LoadBestScore();
        public bool HasSavedSeason => _persistence.HasSavedSeason();

        /// <summary>
        /// LOADING→TITLE（requirements.md 9.3節）。ページロード時のリセット判定（関数G resetIfNeeded）を
        /// 行ってからタイトル画面へ遷移する。
        /// </summary>
        public void Initialize()
        {
            _persistence.ResetIfNeeded();
            TransitionTo(GameFlowState.Title);
        }

        /// <summary>新規シードでシーズンを開始する（TITLE→PLANNING、F1）。</summary>
        public void StartNewSeason()
        {
            BeginSeason(SeasonFactory.Create(GenerateFreshSeed()));
        }

        /// <summary>直前のシードでシーズンを開始する（F10）。直前シードが無ければ何もせずfalseを返す。</summary>
        public bool RetrySameSeed()
        {
            int? seed = _persistence.LoadLastSeed();
            if (seed == null)
            {
                return false;
            }
            BeginSeason(SeasonFactory.Create(seed.Value));
            return true;
        }

        /// <summary>
        /// 保存済みの進行中シーズンから復元する（F11）。復元は保存時点（その日の計画フェーズ）から始まり、
        /// 編集中のプランは保存されていないため空のプランから始める（5.8節「復元」）。
        /// </summary>
        public bool ContinueSavedSeason()
        {
            Season saved = _persistence.Load();
            if (saved == null)
            {
                return false;
            }

            Season = saved;
            Season.State = SeasonState.Planning;
            CurrentPlan = new Plan(Season.Day);
            LastDayResult = null;
            LastSeasonResult = null;
            RefreshForecast();
            TransitionTo(GameFlowState.Planning);
            return true;
        }

        private void BeginSeason(Season season)
        {
            Season = season;
            CurrentPlan = new Plan(Season.Day);
            LastDayResult = null;
            LastSeasonResult = null;
            RefreshForecast();
            TransitionTo(GameFlowState.Planning);
        }

        // ---- PLANNING ----

        /// <summary>
        /// 施策を追加する（PLANNING中のみ）。全地域対象の施策（混雑情報の掲示）は
        /// <paramref name="regionId"/>を無視して<see cref="Measure.AllRegionsTarget"/>を対象にする。
        /// 既に同じ施策・同じ地域がプランに含まれる場合は何もしない（5.2節手順2の重複エラーを未然に防ぐ）。
        /// </summary>
        public void AddMeasure(MeasureKind kind, int regionId)
        {
            EnsurePlanning();
            int targetId = MeasureCatalog.TargetsAllRegions(kind) ? Measure.AllRegionsTarget : regionId;
            if (CurrentPlan.Has(kind, targetId))
            {
                return;
            }
            CurrentPlan.Measures.Add(new Measure(kind, targetId));
            RefreshForecast();
        }

        /// <summary>施策を取り除く（PLANNING中のみ）。存在しなければ何もしない。</summary>
        public void RemoveMeasure(MeasureKind kind, int regionId)
        {
            EnsurePlanning();
            int targetId = MeasureCatalog.TargetsAllRegions(kind) ? Measure.AllRegionsTarget : regionId;
            for (int i = CurrentPlan.Measures.Count - 1; i >= 0; i--)
            {
                var measure = CurrentPlan.Measures[i];
                if (measure.Kind == kind && measure.RegionId == targetId)
                {
                    CurrentPlan.Measures.RemoveAt(i);
                }
            }
            RefreshForecast();
        }

        /// <summary>今のプランに指定した施策が入っているか（UIのトグル表示に使う）。</summary>
        public bool HasMeasure(MeasureKind kind, int regionId)
        {
            int targetId = MeasureCatalog.TargetsAllRegions(kind) ? Measure.AllRegionsTarget : regionId;
            return CurrentPlan != null && CurrentPlan.Has(kind, targetId);
        }

        /// <summary>おまかせプラン（関数H）で現在のプランを置き換える（F5）。</summary>
        public void ApplyAutoPlan()
        {
            EnsurePlanning();
            CurrentPlan = _autoPlanner.Build(Season, TodayEvent, Season.Budget);
            RefreshForecast();
        }

        private void RefreshForecast()
        {
            CurrentForecast = _allocator.Allocate(Season.BaseArrivals(Season.Day), CurrentPlan, Season, TodayEvent);
            CurrentValidation = _validator.Validate(CurrentPlan, Season, CurrentForecast);
            PlanUpdated?.Invoke();
        }

        /// <summary>
        /// 確定（PLANNING→INFLOW、requirements.md 9.1節）。最終検証でエラーがあれば遷移せずfalseを返す。
        /// </summary>
        public bool Confirm()
        {
            EnsurePlanning();
            RefreshForecast(); // 9.1節「UI->V: 最終検証」
            if (!CurrentValidation.CanConfirm())
            {
                return false;
            }

            int actualArrivals = Season.Arrivals[Season.Day - 1];
            PendingAllocation = _allocator.Allocate(actualArrivals, CurrentPlan, Season, TodayEvent);
            Season.State = SeasonState.Inflow;
            TransitionTo(GameFlowState.Inflow);
            return true;
        }

        /// <summary>
        /// INFLOW→REPORT（requirements.md 11.1節「アニメーション完了 / スキップ」）。
        /// 流入アニメーションの自然完了・スキップボタンのいずれからも同じくこのメソッドを呼ぶ。
        /// </summary>
        public void CompleteInflow()
        {
            if (State != GameFlowState.Inflow)
            {
                return;
            }

            LastDayResult = _evaluator.Evaluate(PendingAllocation, CurrentPlan, Season);
            Season.State = SeasonState.Report;
            TransitionTo(GameFlowState.Report);
        }

        /// <summary>
        /// レポートを閉じる（REPORT→PLANNING、13日目以前／REPORT→RESULT、14日目・精算。9.2節）。
        /// </summary>
        public void CloseReport()
        {
            if (State != GameFlowState.Report)
            {
                return;
            }

            bool seasonOver = _advancer.Advance(Season, _persistence);
            if (seasonOver)
            {
                LastSeasonResult = _finalizer.Finalize(Season, _persistence);
                TransitionTo(GameFlowState.Result);
                return;
            }

            CurrentPlan = new Plan(Season.Day);
            RefreshForecast();
            TransitionTo(GameFlowState.Planning);
        }

        /// <summary>RESULT→TITLE。</summary>
        public void GoToTitle()
        {
            TransitionTo(GameFlowState.Title);
        }

        /// <summary>RESULT→PLANNING（同じシードで再挑戦、F10・F11経路と同じ関数A呼び出し）。</summary>
        public bool RetryFromResult()
        {
            return RetrySameSeed();
        }

        private void EnsurePlanning()
        {
            if (State != GameFlowState.Planning)
            {
                throw new InvalidOperationException(
                    $"施策の変更・確定はPLANNING中のみ受け付ける（現在の状態: {State}）。");
            }
        }

        private void TransitionTo(GameFlowState next)
        {
            State = next;
            StateChanged?.Invoke(State);
        }

        /// <summary>
        /// 新規シード生成。UnityEngine.Randomを使わない（Logic.asmdefはnoEngineReferences=trueで
        /// UnityEngineを参照できないため）。デモ用途であり暗号学的な一意性は要求しない。
        /// </summary>
        private static int GenerateFreshSeed()
        {
            unchecked
            {
                return (int)DateTime.UtcNow.Ticks;
            }
        }
    }
}
