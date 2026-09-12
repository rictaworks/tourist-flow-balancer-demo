using System;
using NUnit.Framework;
using TouristFlowBalancer.Core;
using TouristFlowBalancer.Infra;
using TouristFlowBalancer.Logic;
using UnityEngine;

namespace TouristFlowBalancer.Tests.EditMode
{
    /// <summary>
    /// requirements.md 11.1節（状態遷移）・9.1〜9.3節（シーケンス）・Issue #6 受け入れ条件の検証。
    /// GameFlowが呼ぶべきメソッド呼び出しと状態遷移そのものに絞り、UI要素の描画は検証しない
    /// （UI要素そのもののレンダリングテストは困難なため。Issue #6の指示どおり）。
    ///
    /// PlayerPrefsのクリーンアップ方針はPersistenceTests.csに準じる（owner_idキーの前後削除）。
    /// </summary>
    public class GameFlowTests
    {
        private string _ownerIdUsedInTest;

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey("owner_id");
            _ownerIdUsedInTest = null;
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey("owner_id");
            if (!string.IsNullOrEmpty(_ownerIdUsedInTest))
            {
                PlayerPrefs.DeleteKey(_ownerIdUsedInTest + ":last_reset_at");
                PlayerPrefs.DeleteKey(_ownerIdUsedInTest + ":season");
                PlayerPrefs.DeleteKey(_ownerIdUsedInTest + ":last_seed");
                PlayerPrefs.DeleteKey(_ownerIdUsedInTest + ":best_score");
            }
            PlayerPrefs.Save();
        }

        private GameFlow NewFlowAndTrack()
        {
            var persistence = new Persistence();
            _ownerIdUsedInTest = persistence.OwnerId;
            return new GameFlow(persistence);
        }

        // --- LOADING→TITLE ---

        [Test]
        public void Initialize_TransitionsLoadingToTitle()
        {
            var flow = NewFlowAndTrack();
            Assert.AreEqual(GameFlowState.Loading, flow.State);

            flow.Initialize();

            Assert.AreEqual(GameFlowState.Title, flow.State);
        }

        [Test]
        public void Title_WithoutAnyPriorData_HasNoRetryAndNoContinueOption()
        {
            var flow = NewFlowAndTrack();
            flow.Initialize();

            Assert.IsNull(flow.LastSeed);
            Assert.IsFalse(flow.HasSavedSeason);
            Assert.AreEqual(0, flow.BestScore);
        }

        // --- TITLE→PLANNING ---

        [Test]
        public void StartNewSeason_TransitionsToPlanning_WithDayOneAndEmptyPlan()
        {
            var flow = NewFlowAndTrack();
            flow.Initialize();

            flow.StartNewSeason();

            Assert.AreEqual(GameFlowState.Planning, flow.State);
            Assert.IsNotNull(flow.Season);
            Assert.AreEqual(1, flow.Season.Day);
            Assert.AreEqual(0, flow.CurrentPlan.Measures.Count);
            Assert.IsNotNull(flow.CurrentForecast, "PLANNING開始時点で配分予測が計算済みであるはずである。");
            Assert.IsNotNull(flow.CurrentValidation);
        }

        [Test]
        public void RetrySameSeed_WithoutPriorSeed_ReturnsFalse_AndStaysAtTitle()
        {
            var flow = NewFlowAndTrack();
            flow.Initialize();

            bool result = flow.RetrySameSeed();

            Assert.IsFalse(result);
            Assert.AreEqual(GameFlowState.Title, flow.State);
        }

        [Test]
        public void ContinueSavedSeason_WithoutSavedData_ReturnsFalse_AndStaysAtTitle()
        {
            var flow = NewFlowAndTrack();
            flow.Initialize();

            bool result = flow.ContinueSavedSeason();

            Assert.IsFalse(result);
            Assert.AreEqual(GameFlowState.Title, flow.State);
        }

        // --- PLANNING：施策の追加・削除は即時にプラン更新（F3・F4）---

        [Test]
        public void AddMeasure_RecomputesForecastAndValidation_AndRaisesPlanUpdated()
        {
            var flow = NewFlowAndTrack();
            flow.Initialize();
            flow.StartNewSeason();

            int planUpdatedCount = 0;
            flow.PlanUpdated += () => planUpdatedCount++;

            flow.AddMeasure(MeasureKind.CrowdInfo, Measure.AllRegionsTarget);

            Assert.AreEqual(1, planUpdatedCount);
            Assert.IsTrue(flow.HasMeasure(MeasureKind.CrowdInfo, Measure.AllRegionsTarget));
            Assert.IsTrue(flow.CurrentForecast.ForecastCongestion != null, "混雑情報の掲示を追加すると混雑予測が計算されるはずである（4.3節）。");
        }

        [Test]
        public void AddMeasure_SameKindAndRegionTwice_DoesNotDuplicate()
        {
            var flow = NewFlowAndTrack();
            flow.Initialize();
            flow.StartNewSeason();

            flow.AddMeasure(MeasureKind.Promotion, 2);
            flow.AddMeasure(MeasureKind.Promotion, 2);

            int count = 0;
            foreach (var measure in flow.CurrentPlan.Measures)
            {
                if (measure.Kind == MeasureKind.Promotion && measure.RegionId == 2)
                {
                    count++;
                }
            }
            Assert.AreEqual(1, count, "同じ施策・同じ地域の重複追加は無視されるはずである（5.2節手順2のエラーを未然に防ぐ）。");
        }

        [Test]
        public void RemoveMeasure_RemovesFromPlan_AndRecomputesForecast()
        {
            var flow = NewFlowAndTrack();
            flow.Initialize();
            flow.StartNewSeason();
            flow.AddMeasure(MeasureKind.Coupon, 3);

            flow.RemoveMeasure(MeasureKind.Coupon, 3);

            Assert.IsFalse(flow.HasMeasure(MeasureKind.Coupon, 3));
        }

        [Test]
        public void AddMeasure_WhileNotPlanning_Throws()
        {
            var flow = NewFlowAndTrack();
            flow.Initialize();
            flow.StartNewSeason();
            flow.Confirm(); // PLANNING→INFLOW

            Assert.Throws<InvalidOperationException>(() => flow.AddMeasure(MeasureKind.CrowdInfo, Measure.AllRegionsTarget));
        }

        [Test]
        public void ApplyAutoPlan_ReplacesCurrentPlan_WithConfirmablePlan()
        {
            var flow = NewFlowAndTrack();
            flow.Initialize();
            flow.StartNewSeason();

            flow.ApplyAutoPlan();

            Assert.IsTrue(flow.CurrentValidation.CanConfirm(), "おまかせプランはエラーなしで確定できるはずである（5.7節）。");
        }

        // --- PLANNING→INFLOW（確定）---

        [Test]
        public void Confirm_WithValidPlan_TransitionsToInflow_AndComputesPendingAllocation()
        {
            var flow = NewFlowAndTrack();
            flow.Initialize();
            flow.StartNewSeason();

            bool confirmed = flow.Confirm();

            Assert.IsTrue(confirmed);
            Assert.AreEqual(GameFlowState.Inflow, flow.State);
            Assert.IsNotNull(flow.PendingAllocation);
            Assert.AreEqual(Region.Count, flow.PendingAllocation.VisitorsByRegion.Length);
        }

        [Test]
        public void Confirm_WithBudgetExceedingPlan_ReturnsFalse_AndStaysInPlanning()
        {
            var flow = NewFlowAndTrack();
            flow.Initialize();
            flow.StartNewSeason();

            // 初期予算100を超える施策を積み増し、エラーで確定不可にする（L5イベント開催=60を2地域分＝120）。
            flow.AddMeasure(MeasureKind.EventHosting, 1);
            flow.AddMeasure(MeasureKind.EventHosting, 2);

            bool confirmed = flow.Confirm();

            Assert.IsFalse(confirmed);
            Assert.AreEqual(GameFlowState.Planning, flow.State);
            Assert.IsFalse(flow.CurrentValidation.CanConfirm());
            Assert.Greater(flow.CurrentValidation.Errors.Count, 0);
        }

        // --- INFLOW→REPORT ---

        [Test]
        public void CompleteInflow_TransitionsToReport_AndProducesDayResult()
        {
            var flow = NewFlowAndTrack();
            flow.Initialize();
            flow.StartNewSeason();
            flow.Confirm();

            flow.CompleteInflow();

            Assert.AreEqual(GameFlowState.Report, flow.State);
            Assert.IsNotNull(flow.LastDayResult);
            Assert.AreEqual(1, flow.LastDayResult.Day);
            Assert.AreEqual(Region.Count, flow.LastDayResult.Regions.Length);
        }

        [Test]
        public void CompleteInflow_WhileNotInflow_IsNoOp()
        {
            var flow = NewFlowAndTrack();
            flow.Initialize();
            flow.StartNewSeason();

            flow.CompleteInflow(); // PLANNING中に呼んでも何もしない

            Assert.AreEqual(GameFlowState.Planning, flow.State);
            Assert.IsNull(flow.LastDayResult);
        }

        // --- REPORT→PLANNING（13日目以前）／REPORT→RESULT（14日目）---

        [Test]
        public void CloseReport_OnDayOne_ReturnsToPlanning_WithFreshEmptyPlan_ForDayTwo()
        {
            var flow = NewFlowAndTrack();
            flow.Initialize();
            flow.StartNewSeason();
            flow.AddMeasure(MeasureKind.CrowdInfo, Measure.AllRegionsTarget);
            flow.Confirm();
            flow.CompleteInflow();

            flow.CloseReport();

            Assert.AreEqual(GameFlowState.Planning, flow.State);
            Assert.AreEqual(2, flow.Season.Day);
            Assert.AreEqual(0, flow.CurrentPlan.Measures.Count, "5.8節：復元・翌日開始時のプランは空である。");
        }

        [Test]
        public void FullSeason_ThroughGameFlow_ReachesResult_OnDayFourteen()
        {
            var flow = NewFlowAndTrack();
            flow.Initialize();
            flow.StartNewSeason();

            for (int day = 1; day <= GameConstants.SeasonLengthDays; day++)
            {
                Assert.AreEqual(GameFlowState.Planning, flow.State, $"day {day}");
                flow.ApplyAutoPlan();
                bool confirmed = flow.Confirm();
                Assert.IsTrue(confirmed, $"day {day}: おまかせプランはエラーなしで確定できるはずである。");

                Assert.AreEqual(GameFlowState.Inflow, flow.State);
                flow.CompleteInflow();

                Assert.AreEqual(GameFlowState.Report, flow.State);
                flow.CloseReport();
            }

            Assert.AreEqual(GameFlowState.Result, flow.State);
            Assert.IsNotNull(flow.LastSeasonResult);
            Assert.AreEqual(SeasonState.Finalized, flow.Season.State);
            Assert.AreEqual(
                flow.LastSeasonResult.Score,
                flow.LastSeasonResult.SatisfiedTotal - flow.LastSeasonResult.GiveUpTotal);
        }

        // --- RESULT→TITLE／RESULT→PLANNING ---

        [Test]
        public void GoToTitle_FromResult_TransitionsToTitle()
        {
            var flow = NewFlowAndTrack();
            flow.Initialize();
            flow.StartNewSeason();
            RunFullSeason(flow);

            flow.GoToTitle();

            Assert.AreEqual(GameFlowState.Title, flow.State);
        }

        [Test]
        public void RetryFromResult_UsesSameSeed_AndReturnsToPlanning()
        {
            var flow = NewFlowAndTrack();
            flow.Initialize();
            flow.StartNewSeason();
            int originalSeed = flow.Season.Seed;
            RunFullSeason(flow);

            bool retried = flow.RetryFromResult();

            Assert.IsTrue(retried);
            Assert.AreEqual(GameFlowState.Planning, flow.State);
            Assert.AreEqual(originalSeed, flow.Season.Seed, "同じシードで再挑戦するはずである（F10）。");
        }

        // --- 保存・復元（TITLE→PLANNING、続きから）---

        [Test]
        public void ContinueSavedSeason_RestoresFromPlanningPhase_WithEmptyPlan()
        {
            var persistence = new Persistence();
            _ownerIdUsedInTest = persistence.OwnerId;
            var flow = new GameFlow(persistence);
            flow.Initialize();
            flow.StartNewSeason();
            flow.ApplyAutoPlan();
            flow.Confirm();
            flow.CompleteInflow();
            flow.CloseReport(); // ここでday2として保存される（関数E手順8）

            // 新しいGameFlowインスタンス（ページ再読込を模す）で復元する。
            var reloaded = new GameFlow(persistence);
            reloaded.Initialize();
            bool continued = reloaded.ContinueSavedSeason();

            Assert.IsTrue(continued);
            Assert.AreEqual(GameFlowState.Planning, reloaded.State);
            Assert.AreEqual(2, reloaded.Season.Day);
            Assert.AreEqual(0, reloaded.CurrentPlan.Measures.Count);
        }

        private static void RunFullSeason(GameFlow flow)
        {
            for (int day = 1; day <= GameConstants.SeasonLengthDays; day++)
            {
                flow.ApplyAutoPlan();
                flow.Confirm();
                flow.CompleteInflow();
                flow.CloseReport();
            }
        }
    }
}
