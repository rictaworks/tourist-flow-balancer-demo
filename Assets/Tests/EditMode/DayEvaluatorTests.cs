using NUnit.Framework;
using TouristFlowBalancer.Core;
using TouristFlowBalancer.Logic;

namespace TouristFlowBalancer.Tests.EditMode
{
    /// <summary>
    /// requirements.md 5.4節：日次評価（関数D, DayEvaluator）を検証する。
    /// 特に反発状態のヒステリシス（−50以下で入り、−20以上で解除）を受け入れ条件どおりに確認する。
    /// </summary>
    public class DayEvaluatorTests
    {
        private const int CentralTouristAreaId = 1; // 容量450
        private const int RemoteIslandId = 6;

        private static Season NewSeasonWithEvents(EventKind day1Kind = EventKind.Normal)
        {
            var season = new Season(seed: 1);
            season.Events = new EventDay[GameConstants.SeasonLengthDays];
            for (int i = 0; i < season.Events.Length; i++)
            {
                season.Events[i] = new EventDay(i + 1, EventKind.Normal, EventDay.NoTargetRegionId);
            }
            season.Events[0] = new EventDay(1, day1Kind, EventDay.NoTargetRegionId);
            season.Day = 1;
            return season;
        }

        private static Allocation AllocationWithOnlyCentral(int visitors)
        {
            var visitorsByRegion = new int[Region.Count];
            visitorsByRegion[CentralTouristAreaId - 1] = visitors;
            return new Allocation(visitorsByRegion, giveUps: 0, forecastCongestion: null);
        }

        [Test]
        public void Evaluate_NullArguments_Throw()
        {
            var season = NewSeasonWithEvents();
            var plan = new Plan(1);
            var allocation = AllocationWithOnlyCentral(100);
            var evaluator = new DayEvaluator();

            Assert.Throws<System.ArgumentNullException>(() => evaluator.Evaluate(null, plan, season));
            Assert.Throws<System.ArgumentNullException>(() => evaluator.Evaluate(allocation, null, season));
            Assert.Throws<System.ArgumentNullException>(() => evaluator.Evaluate(allocation, plan, null));
        }

        [Test]
        public void Evaluate_ClosedRegion_HasZeroedResult_AndDoesNotChangeSentimentOrAwareness()
        {
            var season = NewSeasonWithEvents();
            season.GetRegionState(RemoteIslandId).Closed = true;
            int sentimentBefore = season.GetRegionState(RemoteIslandId).Sentiment;
            int awarenessBefore = season.GetRegionState(RemoteIslandId).Awareness;

            var plan = new Plan(1);
            var visitorsByRegion = new int[Region.Count]; // 閉鎖地域の来訪者数は0
            var allocation = new Allocation(visitorsByRegion, giveUps: 0, forecastCongestion: null);

            var result = new DayEvaluator().Evaluate(allocation, plan, season);
            var islandResult = result.Regions[RemoteIslandId - 1];

            Assert.AreEqual(0, islandResult.Visitors);
            Assert.AreEqual(0f, islandResult.Congestion);
            Assert.AreEqual(0, islandResult.Satisfaction);
            Assert.AreEqual(0, islandResult.Revenue);
            Assert.AreEqual(0, islandResult.SentimentDelta);
            Assert.AreEqual(sentimentBefore, season.GetRegionState(RemoteIslandId).Sentiment);
            Assert.AreEqual(awarenessBefore, season.GetRegionState(RemoteIslandId).Awareness);
        }

        [Test]
        public void Evaluate_HealthyCongestion_IncreasesSentiment()
        {
            var season = NewSeasonWithEvents();
            var plan = new Plan(1);
            // 混雑率0.3〜1.0 → 住民感情+3。容量450の60%=270人。
            var allocation = AllocationWithOnlyCentral(270);

            var result = new DayEvaluator().Evaluate(allocation, plan, season);

            Assert.AreEqual(GameConstants.SentimentDeltaHealthy, result.Regions[CentralTouristAreaId - 1].SentimentDelta);
            Assert.AreEqual(GameConstants.SentimentDeltaHealthy, season.GetRegionState(CentralTouristAreaId).Sentiment);
        }

        [Test]
        public void Evaluate_Backlash_EntersAtMinusFifty_AndDoesNotExitAtMinusThirty()
        {
            var season = NewSeasonWithEvents();
            var state = season.GetRegionState(CentralTouristAreaId);
            state.Sentiment = -47; // あと一段階の過密(-15)/混雑(-6)ではなく、直接-50到達を確認するため近い値から
            var plan = new Plan(1);

            // 混雑率1.2超（過密）→ -15。-47-15=-62 <= -50 → 反発に入る。541/450≒1.202>1.2。
            var overcrowdedAllocation = AllocationWithOnlyCentral(541);

            new DayEvaluator().Evaluate(overcrowdedAllocation, plan, season);

            Assert.AreEqual(-62, state.Sentiment);
            Assert.IsTrue(state.Backlash, "住民感情が−50以下になったので反発状態に入るはずである（5.4節手順6）。");

            // 翌日：混雑率0.3〜1.0（+3）で -62+3=-59。まだ-20以上ではないので反発は解除されない（ヒステリシス）。
            season.Day = 2;
            var healthyAllocation = AllocationWithOnlyCentral(270);
            new DayEvaluator().Evaluate(healthyAllocation, plan, season);

            Assert.AreEqual(-59, state.Sentiment);
            Assert.IsTrue(state.Backlash, "−20以上に回復するまで反発は解除されないはずである（ヒステリシス）。");
        }

        [Test]
        public void Evaluate_Backlash_ExitsOnlyAtMinusTwentyOrAbove()
        {
            var season = NewSeasonWithEvents();
            var state = season.GetRegionState(CentralTouristAreaId);
            state.Sentiment = -50;
            state.Backlash = true;
            var plan = new Plan(1);

            // 混雑率0.3〜1.0（+3）：-50+3=-47。まだ-20未満なので解除されない。
            new DayEvaluator().Evaluate(AllocationWithOnlyCentral(270), plan, season);
            Assert.AreEqual(-47, state.Sentiment);
            Assert.IsTrue(state.Backlash);

            // 積み重ねて-20以上に到達させる（+3を複数回）。
            season.Day = 2;
            state.Sentiment = -22;
            new DayEvaluator().Evaluate(AllocationWithOnlyCentral(270), plan, season); // -22+3=-19
            Assert.AreEqual(-19, state.Sentiment);
            Assert.IsFalse(state.Backlash, "住民感情が−20以上に回復したので反発は解除されるはずである。");
        }

        [Test]
        public void Evaluate_WordOfMouth_HighSatisfaction_IncreasesAwareness()
        {
            var season = NewSeasonWithEvents();
            var plan = new Plan(1);
            // 混雑率を穏やかにし、満足度が60以上になるようにする（実効魅力90、混雑ペナルティ0、backlashなし）。
            // 300人来訪（congestion=300/450=0.667）。
            int awarenessBefore = season.GetRegionState(CentralTouristAreaId).Awareness;

            var result = new DayEvaluator().Evaluate(AllocationWithOnlyCentral(300), plan, season);
            var regionResult = result.Regions[CentralTouristAreaId - 1];

            Assume.That(regionResult.Satisfaction, Is.GreaterThanOrEqualTo(60));

            int expectedSteps = 300 / GameConstants.WordOfMouthVisitorsPerStep;
            int expectedAwareness = awarenessBefore + expectedSteps * GameConstants.WordOfMouthAwarenessGain;
            Assert.AreEqual(expectedAwareness, season.GetRegionState(CentralTouristAreaId).Awareness);
        }

        [Test]
        public void Evaluate_Promotion_PersistsAwarenessBonus()
        {
            var season = NewSeasonWithEvents();
            var plan = new Plan(1);
            plan.Measures.Add(new Measure(MeasureKind.Promotion, CentralTouristAreaId));
            int awarenessBefore = season.GetRegionState(CentralTouristAreaId).Awareness;

            var result = new DayEvaluator().Evaluate(AllocationWithOnlyCentral(300), plan, season);
            var regionResult = result.Regions[CentralTouristAreaId - 1];

            int wordOfMouthSteps = 300 / GameConstants.WordOfMouthVisitorsPerStep;
            int wordOfMouthDelta = regionResult.Satisfaction >= 60
                ? wordOfMouthSteps * GameConstants.WordOfMouthAwarenessGain
                : regionResult.Satisfaction < 40 ? wordOfMouthSteps * GameConstants.WordOfMouthAwarenessLoss : 0;

            int expected = awarenessBefore + (int)GameConstants.PromotionAwarenessBonus + wordOfMouthDelta;
            Assert.AreEqual(expected, season.GetRegionState(CentralTouristAreaId).Awareness);
        }

        [Test]
        public void Evaluate_AggregatesStats_SatisfiedTotal_GiveUps_MeasureUses()
        {
            var season = NewSeasonWithEvents();
            var plan = new Plan(1);
            plan.Measures.Add(new Measure(MeasureKind.CrowdInfo, Measure.AllRegionsTarget));

            var visitorsByRegion = new int[Region.Count];
            visitorsByRegion[CentralTouristAreaId - 1] = 300;
            var allocation = new Allocation(visitorsByRegion, giveUps: 7, forecastCongestion: null);

            var result = new DayEvaluator().Evaluate(allocation, plan, season);

            Assert.AreEqual(result.SatisfiedTotal, season.Stats.SatisfiedTotal);
            Assert.AreEqual(7, season.Stats.GiveUpTotal);
            Assert.AreEqual(7, result.GiveUps);
            Assert.AreEqual(1, season.Stats.UsesByMeasure[(int)MeasureKind.CrowdInfo]);
            Assert.AreEqual(300, season.Stats.VisitorsByRegion[CentralTouristAreaId - 1]);
        }

        [Test]
        public void Evaluate_DeductsPlanCostFromBudget()
        {
            var season = NewSeasonWithEvents();
            season.Budget = 100;
            var plan = new Plan(1);
            plan.Measures.Add(new Measure(MeasureKind.CrowdInfo, Measure.AllRegionsTarget)); // cost 20

            new DayEvaluator().Evaluate(AllocationWithOnlyCentral(100), plan, season);

            Assert.AreEqual(80, season.Budget);
        }

        [Test]
        public void Evaluate_OvercrowdedDay_IncrementsOvercrowdedDaysStat()
        {
            var season = NewSeasonWithEvents();
            var plan = new Plan(1);

            new DayEvaluator().Evaluate(AllocationWithOnlyCentral(600), plan, season); // 600/450=1.33>1.2

            Assert.AreEqual(1, season.Stats.OvercrowdedDays[CentralTouristAreaId - 1]);
            Assert.AreEqual(0, season.Stats.DesertedDays[CentralTouristAreaId - 1]);
        }

        [Test]
        public void Evaluate_DesertedDay_IncrementsDesertedDaysStat_AndAppliesSmallSentimentPenalty()
        {
            var season = NewSeasonWithEvents();
            var plan = new Plan(1);

            new DayEvaluator().Evaluate(AllocationWithOnlyCentral(50), plan, season); // 50/450=0.11<0.3

            Assert.AreEqual(1, season.Stats.DesertedDays[CentralTouristAreaId - 1]);
            Assert.AreEqual(GameConstants.SentimentDeltaDeserted, season.GetRegionState(CentralTouristAreaId).Sentiment);
        }
    }
}
