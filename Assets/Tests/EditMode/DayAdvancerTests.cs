using NUnit.Framework;
using TouristFlowBalancer.Core;
using TouristFlowBalancer.Logic;

namespace TouristFlowBalancer.Tests.EditMode
{
    /// <summary>
    /// requirements.md 5.5節：日次更新（関数E, DayAdvancer）を検証する。
    /// 手順の順序（認知度減衰→予算繰越→当日効果解除→翌日公開）が入れ替わっていないことを重点的に確認する。
    /// </summary>
    public class DayAdvancerTests
    {
        private const int CentralTouristAreaId = 1; // BaseAwareness=100
        private const int OnsenId = 2;               // BaseAwareness=45
        private const int RemoteIslandId = 6;

        private static Season NewSeasonWithEmptyEvents()
        {
            var season = new Season(seed: 1);
            season.Events = new EventDay[GameConstants.SeasonLengthDays];
            for (int i = 0; i < season.Events.Length; i++)
            {
                season.Events[i] = new EventDay(i + 1, EventKind.Normal, EventDay.NoTargetRegionId);
            }
            season.Arrivals = new int[GameConstants.SeasonLengthDays];
            return season;
        }

        [Test]
        public void Advance_NullSeason_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => new DayAdvancer().Advance(null));
        }

        [Test]
        public void Advance_DecaysAwarenessTowardBase_ByThreePerDay()
        {
            var season = NewSeasonWithEmptyEvents();
            var state = season.GetRegionState(OnsenId); // base=45
            state.Awareness = 60; // プロモーション等で上がっていた状態を模す

            new DayAdvancer().Advance(season);

            Assert.AreEqual(57, state.Awareness, "上振れ側は基礎値へ向けて-3されるはずである。");
        }

        [Test]
        public void Advance_DecaysAwareness_SnapsToBase_WhenDiffLessThanThree()
        {
            var season = NewSeasonWithEmptyEvents();
            var state = season.GetRegionState(OnsenId); // base=45
            state.Awareness = 46;

            new DayAdvancer().Advance(season);

            Assert.AreEqual(45, state.Awareness, "差が3未満なら基礎値にちょうど一致するはずである。");
        }

        [Test]
        public void Advance_ClampsAwarenessToMaximumOneHundred_WhenStillAboveRangeAfterDecay()
        {
            // DayEvaluator（関数D）の口コミ・プロモーション加算は5〜100へのクランプを行わない設計
            // （クランプは関数Eの責務）ため、前日の状態が一時的に範囲外(>100)になりうる。
            // 基礎認知度100の中央観光地で105からの減衰(-3)後も102と範囲外のままなので、
            // 5.5節手順2の範囲(5〜100)への収束が働くことを確認する。
            var season = NewSeasonWithEmptyEvents();
            var state = season.GetRegionState(CentralTouristAreaId); // base=100
            state.Awareness = 105;

            new DayAdvancer().Advance(season);

            Assert.AreEqual(100, state.Awareness);
        }

        [Test]
        public void Advance_RolloverBudget_KeepsUpToOneHundredLeftover_AndAddsHundred()
        {
            var season = NewSeasonWithEmptyEvents();
            season.Budget = 40; // 使い残し40 <= 100

            new DayAdvancer().Advance(season);

            Assert.AreEqual(140, season.Budget); // 40 + 100
        }

        [Test]
        public void Advance_RolloverBudget_CapsLeftoverAtOneHundred()
        {
            var season = NewSeasonWithEmptyEvents();
            season.Budget = 180; // 使い残し180 > 100上限 → 100として繰越

            new DayAdvancer().Advance(season);

            Assert.AreEqual(200, season.Budget); // min(180,100) + 100 = 200（所持上限にも一致）
        }

        [Test]
        public void Advance_RolloverBudget_CapsHoldingAtTwoHundred()
        {
            var season = NewSeasonWithEmptyEvents();
            season.Budget = 100; // 使い残し100 → +100 = 200（上限ちょうど）

            new DayAdvancer().Advance(season);

            Assert.AreEqual(200, season.Budget);
            Assert.LessOrEqual(season.Budget, GameConstants.BudgetHoldingCap);
        }

        [Test]
        public void Advance_ClearsAccessPenaltyAndClosed()
        {
            var season = NewSeasonWithEmptyEvents();
            var state = season.GetRegionState(OnsenId);
            state.AccessPenalty = 2;
            state.Closed = true;

            new DayAdvancer().Advance(season);

            Assert.AreEqual(0, state.AccessPenalty);
            Assert.IsFalse(state.Closed);
        }

        [Test]
        public void Advance_BeforeLastDay_AdvancesDayAndSetsPlanning()
        {
            var season = NewSeasonWithEmptyEvents();
            season.Day = 5;
            season.State = SeasonState.Report;

            bool seasonOver = new DayAdvancer().Advance(season);

            Assert.IsFalse(seasonOver);
            Assert.AreEqual(6, season.Day);
            Assert.AreEqual(SeasonState.Planning, season.State);
        }

        [Test]
        public void Advance_OnLastDay_ReturnsTrue_AndDoesNotAdvanceDayOrChangeState()
        {
            var season = NewSeasonWithEmptyEvents();
            season.Day = GameConstants.SeasonLengthDays;
            season.State = SeasonState.Report;

            bool seasonOver = new DayAdvancer().Advance(season);

            Assert.IsTrue(seasonOver);
            Assert.AreEqual(GameConstants.SeasonLengthDays, season.Day,
                "14日目はシーズン終了のため、翌日公開（Dayのインクリメント）は行わないはずである。");
            Assert.AreEqual(SeasonState.Report, season.State,
                "14日目は「計画中」へ遷移せず、関数Fに委ねるはずである。");
        }

        [Test]
        public void Advance_PublishesNextDaySnsBuzz_AndAppliesAwarenessBonusImmediately()
        {
            var season = NewSeasonWithEmptyEvents();
            season.Day = 1;
            season.Events[1] = new EventDay(2, EventKind.SnsBuzz, OnsenId); // 2日目にSNSバズ(温泉郷)
            int baseAwareness = season.GetRegionState(OnsenId).Awareness;

            new DayAdvancer().Advance(season);

            Assert.AreEqual(2, season.Day);
            Assert.AreEqual(baseAwareness + GameConstants.SnsBuzzAwarenessBonus, season.GetRegionState(OnsenId).Awareness);
        }

        [Test]
        public void Advance_PublishesNextDayTrafficDisruption_OnNonIsland_AddsAccessPenalty()
        {
            var season = NewSeasonWithEmptyEvents();
            season.Day = 1;
            season.Events[1] = new EventDay(2, EventKind.TrafficDisruption, OnsenId);

            new DayAdvancer().Advance(season);

            Assert.AreEqual(GameConstants.TrafficDisruptionAccessPenalty, season.GetRegionState(OnsenId).AccessPenalty);
            Assert.IsFalse(season.GetRegionState(OnsenId).Closed);
        }

        [Test]
        public void Advance_PublishesNextDayTrafficDisruption_OnIsland_ClosesIsland()
        {
            var season = NewSeasonWithEmptyEvents();
            season.Day = 1;
            season.Events[1] = new EventDay(2, EventKind.TrafficDisruption, RemoteIslandId);

            new DayAdvancer().Advance(season);

            Assert.IsTrue(season.GetRegionState(RemoteIslandId).Closed);
            Assert.AreEqual(0, season.GetRegionState(RemoteIslandId).AccessPenalty);
        }

        [Test]
        public void Advance_WithoutPersistence_DoesNotThrow()
        {
            var season = NewSeasonWithEmptyEvents();
            Assert.DoesNotThrow(() => new DayAdvancer().Advance(season, persistence: null));
        }

        [Test]
        public void Advance_StepOrder_ClearsClosedBeforeCheckingSeasonEnd_ButAfterAwarenessAndBudget()
        {
            // 「手順の順序が固定されている」ことの直接確認：認知度減衰・予算繰越・当日効果解除の
            // すべてが、14日目の判定・翌日公開より前に実行されていることを、それぞれの副作用の
            // 有無で確認する（14日目なら翌日公開系の副作用が一切起きないはずである）。
            var season = NewSeasonWithEmptyEvents();
            season.Day = GameConstants.SeasonLengthDays;
            var onsenState = season.GetRegionState(OnsenId);
            onsenState.Awareness = 60; // 減衰対象
            onsenState.AccessPenalty = 2;
            onsenState.Closed = true;
            season.Budget = 40;

            bool seasonOver = new DayAdvancer().Advance(season);

            Assert.IsTrue(seasonOver);
            // 減衰・繰越・当日効果解除は14日目でも実行される（手順1〜4は常に実行、手順5で分岐するため）。
            Assert.AreEqual(57, onsenState.Awareness);
            Assert.AreEqual(140, season.Budget);
            Assert.AreEqual(0, onsenState.AccessPenalty);
            Assert.IsFalse(onsenState.Closed);
        }
    }
}
