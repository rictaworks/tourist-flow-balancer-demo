using System;
using System.Collections.Generic;
using NUnit.Framework;
using TouristFlowBalancer.Core;
using TouristFlowBalancer.Infra;
using TouristFlowBalancer.Logic;
using UnityEngine;

namespace TouristFlowBalancer.Tests.EditMode
{
    /// <summary>
    /// GameFlowTests.cs を補完する追加テスト（Issue #6・PR #11レビュー）。
    ///
    /// requirements.md 14.1節の不変条件「施策の変更は計画中（PLANNING状態）のみ受け付け、
    /// 確定後は当日の配分を変更しない」について、GameFlowTests.cs では AddMeasure の
    /// PLANNING外呼び出しのみ検証されていた。EnsurePlanning() は RemoveMeasure・ApplyAutoPlan・
    /// Confirm でも呼ばれているため、それぞれについても同じ不変条件を検証する。
    /// また、CloseReport の「REPORT以外では何もしない」という対称の振る舞い
    /// （CompleteInflowの同種テストはGameFlowTests.csに既存）と、
    /// 受け入れ条件に明記された StateChanged イベントの発火そのものを検証する
    /// テストが既存になかったため追加する。
    /// </summary>
    public class GameFlowStateGuardTests
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

        // --- PLANNING中のみ施策変更・確定を受け付ける（14.1節不変条件）。AddMeasureはGameFlowTests.csで検証済み。 ---

        [Test]
        public void RemoveMeasure_WhileNotPlanning_Throws()
        {
            var flow = NewFlowAndTrack();
            flow.Initialize();
            flow.StartNewSeason();
            flow.AddMeasure(MeasureKind.Coupon, 3);
            flow.Confirm(); // PLANNING→INFLOW

            Assert.Throws<InvalidOperationException>(() => flow.RemoveMeasure(MeasureKind.Coupon, 3));
        }

        [Test]
        public void ApplyAutoPlan_WhileNotPlanning_Throws()
        {
            var flow = NewFlowAndTrack();
            flow.Initialize();
            flow.StartNewSeason();
            flow.Confirm(); // PLANNING→INFLOW

            Assert.Throws<InvalidOperationException>(() => flow.ApplyAutoPlan());
        }

        [Test]
        public void Confirm_WhileNotPlanning_Throws()
        {
            var flow = NewFlowAndTrack();
            flow.Initialize();
            flow.StartNewSeason();
            flow.Confirm(); // PLANNING→INFLOW（1回目は成功する）

            Assert.Throws<InvalidOperationException>(() => flow.Confirm());
        }

        [Test]
        public void CloseReport_WhileNotReport_IsNoOp()
        {
            var flow = NewFlowAndTrack();
            flow.Initialize();
            flow.StartNewSeason();

            flow.CloseReport(); // PLANNING中に呼んでも何もしない

            Assert.AreEqual(GameFlowState.Planning, flow.State);
            Assert.IsNull(flow.LastSeasonResult);
        }

        // --- 受け入れ条件「GameFlowクラスが状態遷移イベントStateChangedを発火」の直接検証 ---

        [Test]
        public void StateChanged_FiresWithCorrectState_OnEachTransition()
        {
            var flow = NewFlowAndTrack();
            var observedStates = new List<GameFlowState>();
            flow.StateChanged += state => observedStates.Add(state);

            flow.Initialize();
            flow.StartNewSeason();
            flow.ApplyAutoPlan();
            flow.Confirm();
            flow.CompleteInflow();
            flow.CloseReport();

            CollectionAssert.AreEqual(
                new[]
                {
                    GameFlowState.Title,
                    GameFlowState.Planning,
                    GameFlowState.Inflow,
                    GameFlowState.Report,
                    GameFlowState.Planning,
                },
                observedStates,
                "StateChangedは各遷移で、遷移後の状態を引数として発火するはずである。");
        }

        [Test]
        public void RemoveMeasure_RaisesPlanUpdated()
        {
            var flow = NewFlowAndTrack();
            flow.Initialize();
            flow.StartNewSeason();
            flow.AddMeasure(MeasureKind.Coupon, 3);

            int planUpdatedCount = 0;
            flow.PlanUpdated += () => planUpdatedCount++;

            flow.RemoveMeasure(MeasureKind.Coupon, 3);

            Assert.AreEqual(1, planUpdatedCount, "施策削除でもPlanUpdatedが発火し配分予測・検証結果が更新されるはずである。");
        }
    }
}
