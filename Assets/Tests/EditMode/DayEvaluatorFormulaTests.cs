using System;
using NUnit.Framework;
using TouristFlowBalancer.Core;
using TouristFlowBalancer.Logic;

namespace TouristFlowBalancer.Tests.EditMode
{
    /// <summary>
    /// requirements.md 5.4節：日次評価（関数D, DayEvaluator）の手順2〜4（混雑ペナルティ・満足度・収益の
    /// 各計算式）を検証する。DayEvaluatorTests.csは住民感情・反発・口コミ・集計値を検証しているが、
    /// Congestion/Satisfaction/Revenueの数値そのものを式どおりに検証するテストが無かったため補完する。
    /// 実装（DayEvaluator.CongestionPenalty・満足度式・収益式）を実際に呼び出し、
    /// requirements.md 5.4節の式から手計算した期待値と比較する（モックなし）。
    /// </summary>
    public class DayEvaluatorFormulaTests
    {
        private const int CentralTouristAreaId = 1; // 屋内・容量450・基礎魅力90・客単価10
        private const int MountainVillageId = 4;    // 屋外・容量200・基礎魅力60・客単価8

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

        private static Allocation AllocationFor(int regionId, int visitors)
        {
            var visitorsByRegion = new int[Region.Count];
            visitorsByRegion[regionId - 1] = visitors;
            return new Allocation(visitorsByRegion, giveUps: 0, forecastCongestion: null);
        }

        private static int ExpectedSatisfaction(
            int baseAttraction, float attractionFactor, int eventBonus, float penalty,
            bool backlash, bool deserted)
        {
            float effectiveAttraction = baseAttraction * attractionFactor;
            float raw = 60f + (effectiveAttraction - 60f) / 2f
                + eventBonus
                - penalty
                - (backlash ? GameConstants.BacklashSatisfactionPenalty : 0)
                - (deserted ? GameConstants.DesertedSatisfactionPenalty : 0);
            int satisfaction = (int)Math.Round(raw, MidpointRounding.AwayFromZero);
            return Math.Max(0, Math.Min(100, satisfaction));
        }

        private static int ExpectedRevenue(int visitors, int spendPerVisitor, int satisfaction)
        {
            float factor = GameConstants.RevenueCoefficientBase + satisfaction / GameConstants.RevenueCoefficientSatisfactionDivisor;
            return (int)Math.Round(visitors * spendPerVisitor * factor, MidpointRounding.AwayFromZero);
        }

        [Test]
        public void CongestionPenalty_IsZero_AtOrBelowCautionThreshold()
        {
            var season = NewSeasonWithEvents();
            var plan = new Plan(1);
            var region = Region.ById(CentralTouristAreaId);
            int visitors = (int)(region.Capacity * GameConstants.CongestionCautionThreshold); // c = 0.9 ちょうど

            var result = new DayEvaluator().Evaluate(AllocationFor(CentralTouristAreaId, visitors), plan, season);
            var regionResult = result.Regions[CentralTouristAreaId - 1];

            int expectedSatisfaction = ExpectedSatisfaction(region.BaseAttraction, 1f, 0, penalty: 0f, backlash: false, deserted: false);
            int expectedRevenue = ExpectedRevenue(visitors, region.SpendPerVisitor, expectedSatisfaction);

            Assert.AreEqual(expectedSatisfaction, regionResult.Satisfaction, "c<=0.9では混雑ペナルティが0のはずである（5.4節手順2）。");
            Assert.AreEqual(expectedRevenue, regionResult.Revenue);
        }

        [Test]
        public void CongestionPenalty_IsLinear_BetweenCautionAndOvercrowdedThresholds()
        {
            var season = NewSeasonWithEvents();
            var plan = new Plan(1);
            var region = Region.ById(CentralTouristAreaId);
            int visitors = region.Capacity; // c = 1.0

            var result = new DayEvaluator().Evaluate(AllocationFor(CentralTouristAreaId, visitors), plan, season);
            var regionResult = result.Regions[CentralTouristAreaId - 1];

            float expectedPenalty = (1.0f - GameConstants.CongestionCautionThreshold) * 100f; // = 10
            int expectedSatisfaction = ExpectedSatisfaction(region.BaseAttraction, 1f, 0, expectedPenalty, backlash: false, deserted: false);
            int expectedRevenue = ExpectedRevenue(visitors, region.SpendPerVisitor, expectedSatisfaction);

            Assert.AreEqual(expectedSatisfaction, regionResult.Satisfaction, "0.9<c<=1.2では(c-0.9)*100が満足度から引かれるはずである（5.4節手順2）。");
            Assert.AreEqual(expectedRevenue, regionResult.Revenue);
        }

        [Test]
        public void CongestionPenalty_CapsAtSixty_EvenForExtremeCongestion()
        {
            var season = NewSeasonWithEvents();
            var region = Region.ById(CentralTouristAreaId);

            // c=1.5：cap未満で理論値どおり60（30+(1.5-1.2)*100=60）。
            int visitorsAtCap = (int)(region.Capacity * 1.5f);
            var atCapResult = new DayEvaluator().Evaluate(AllocationFor(CentralTouristAreaId, visitorsAtCap), new Plan(1), season);

            // c=2.0：cap無しなら30+(2.0-1.2)*100=110になるはずだが、上限60でクランプされる。
            var season2 = NewSeasonWithEvents();
            int visitorsBeyondCap = (int)(region.Capacity * 2.0f);
            var beyondCapResult = new DayEvaluator().Evaluate(AllocationFor(CentralTouristAreaId, visitorsBeyondCap), new Plan(1), season2);

            int expectedSatisfactionAtCap = ExpectedSatisfaction(region.BaseAttraction, 1f, 0, GameConstants.CongestionPenaltyCap, false, false);

            Assert.AreEqual(expectedSatisfactionAtCap, atCapResult.Regions[CentralTouristAreaId - 1].Satisfaction);
            Assert.AreEqual(expectedSatisfactionAtCap, beyondCapResult.Regions[CentralTouristAreaId - 1].Satisfaction,
                "c=2.0でも、ペナルティは上限60でクランプされ、c=1.5と同じ満足度になるはずである（5.4節手順2の上限60）。" +
                "上限が効いていなければ満足度はここより大幅に低くなる（クランプされて0になる等）。");
        }

        [Test]
        public void EventHosting_MultipliesAttraction_AndAddsSatisfactionBonus()
        {
            var season = NewSeasonWithEvents();
            var plan = new Plan(1);
            plan.Measures.Add(new Measure(MeasureKind.EventHosting, CentralTouristAreaId));
            var region = Region.ById(CentralTouristAreaId);
            int visitors = (int)(region.Capacity * 0.6f); // c<0.9なのでペナルティ0

            var result = new DayEvaluator().Evaluate(AllocationFor(CentralTouristAreaId, visitors), plan, season);
            var regionResult = result.Regions[CentralTouristAreaId - 1];

            int expectedSatisfaction = ExpectedSatisfaction(
                region.BaseAttraction, GameConstants.EventAttractionFactor, GameConstants.EventSatisfactionBonus,
                penalty: 0f, backlash: false, deserted: false);
            int expectedRevenue = ExpectedRevenue(visitors, region.SpendPerVisitor, expectedSatisfaction);

            Assert.AreEqual(expectedSatisfaction, regionResult.Satisfaction,
                "イベント開催中は実効魅力が×1.4され、満足度に+5されるはずである（4.1節・5.4節手順3）。");
            Assert.AreEqual(expectedRevenue, regionResult.Revenue);
        }

        [Test]
        public void Rain_ReducesAttraction_ForOutdoorRegion_ButHasNoEffect_OnIndoorRegion()
        {
            var outdoorRegion = Region.ById(MountainVillageId);
            Assume.That(outdoorRegion.Outdoor, Is.True);
            var indoorRegion = Region.ById(CentralTouristAreaId);
            Assume.That(indoorRegion.Outdoor, Is.False);

            // 屋外地域：雨で魅力×0.6が満足度に反映される。
            var rainySeason = NewSeasonWithEvents(EventKind.Rain);
            int outdoorVisitors = (int)(outdoorRegion.Capacity * 0.5f); // c<0.9のためペナルティ0、c>=0.3のため閑散でもない
            var outdoorResult = new DayEvaluator().Evaluate(
                AllocationFor(MountainVillageId, outdoorVisitors), new Plan(1), rainySeason);
            int expectedOutdoorSatisfaction = ExpectedSatisfaction(
                outdoorRegion.BaseAttraction, GameConstants.RainOutdoorAttractionFactor, 0, 0f, false, false);
            Assert.AreEqual(expectedOutdoorSatisfaction, outdoorResult.Regions[MountainVillageId - 1].Satisfaction,
                "屋外地域は雨の日に実効魅力が×0.6されるはずである（4.1節）。");

            // 屋内地域：雨の日でも魅力は変化しない（晴天時と同じ満足度になる）。
            var rainySeason2 = NewSeasonWithEvents(EventKind.Rain);
            int indoorVisitors = (int)(indoorRegion.Capacity * 0.6f);
            var indoorRainyResult = new DayEvaluator().Evaluate(
                AllocationFor(CentralTouristAreaId, indoorVisitors), new Plan(1), rainySeason2);

            var clearSeason = NewSeasonWithEvents(EventKind.Normal);
            var indoorClearResult = new DayEvaluator().Evaluate(
                AllocationFor(CentralTouristAreaId, indoorVisitors), new Plan(1), clearSeason);

            Assert.AreEqual(indoorClearResult.Regions[CentralTouristAreaId - 1].Satisfaction,
                indoorRainyResult.Regions[CentralTouristAreaId - 1].Satisfaction,
                "屋内地域は雨の日でも実効魅力の低下が無いはずである（4.1節：雨は屋外型のみ）。");
        }

        [Test]
        public void Backlash_ReducesAttraction_AndAddsSatisfactionPenalty()
        {
            var season = NewSeasonWithEvents();
            var state = season.GetRegionState(CentralTouristAreaId);
            state.Backlash = true; // 反発状態そのものを直接与える（この日の計算への影響のみを見る）
            var region = Region.ById(CentralTouristAreaId);
            int visitors = (int)(region.Capacity * 0.6f); // c<0.9のためペナルティ0

            var result = new DayEvaluator().Evaluate(AllocationFor(CentralTouristAreaId, visitors), new Plan(1), season);
            var regionResult = result.Regions[CentralTouristAreaId - 1];

            int expectedSatisfaction = ExpectedSatisfaction(
                region.BaseAttraction, GameConstants.BacklashAttractionFactor, 0, 0f, backlash: true, deserted: false);

            Assert.AreEqual(expectedSatisfaction, regionResult.Satisfaction,
                "反発中は実効魅力が×0.8され、満足度から反発ペナルティ15が引かれるはずである（4.1節・5.4節手順3）。");
        }

        [Test]
        public void DesertedCongestion_AddsSatisfactionPenalty()
        {
            var season = NewSeasonWithEvents();
            var region = Region.ById(CentralTouristAreaId);
            int visitors = (int)(region.Capacity * 0.1f); // c<0.3（閑散）だがc<=0.9なので混雑ペナルティは0

            var result = new DayEvaluator().Evaluate(AllocationFor(CentralTouristAreaId, visitors), new Plan(1), season);
            var regionResult = result.Regions[CentralTouristAreaId - 1];

            int expectedSatisfaction = ExpectedSatisfaction(region.BaseAttraction, 1f, 0, 0f, backlash: false, deserted: true);
            int expectedRevenue = ExpectedRevenue(visitors, region.SpendPerVisitor, expectedSatisfaction);

            Assert.AreEqual(expectedSatisfaction, regionResult.Satisfaction,
                "混雑率0.3未満は閑散ペナルティ5が満足度から引かれるはずである（5.4節手順3）。");
            Assert.AreEqual(expectedRevenue, regionResult.Revenue);
        }

        [Test]
        public void Satisfaction_ClampsToZero_WhenCombinedPenaltiesWouldGoNegative()
        {
            var season = NewSeasonWithEvents(EventKind.Rain);
            var state = season.GetRegionState(MountainVillageId);
            state.Backlash = true;
            var region = Region.ById(MountainVillageId);
            int visitors = (int)(region.Capacity * 1.5f); // 過密（混雑ペナルティは上限60）

            var result = new DayEvaluator().Evaluate(AllocationFor(MountainVillageId, visitors), new Plan(1), season);
            var regionResult = result.Regions[MountainVillageId - 1];

            float combinedAttractionFactor = GameConstants.RainOutdoorAttractionFactor * GameConstants.BacklashAttractionFactor;
            float raw = 60f + (region.BaseAttraction * combinedAttractionFactor - 60f) / 2f
                - GameConstants.CongestionPenaltyCap
                - GameConstants.BacklashSatisfactionPenalty;
            Assume.That(raw, Is.LessThan(0), "前提：このケースでは素の満足度計算が負になっている必要がある。");

            Assert.AreEqual(0, regionResult.Satisfaction, "満足度は0未満にならないよう0にクランプされるはずである（5.4節手順3）。");
        }

        [Test]
        public void Revenue_UsesSatisfactionDependentCoefficient_NotJustVisitorsTimesSpend()
        {
            // 収益＝来訪者数×客単価×(0.5+満足度÷200)。満足度が高いほど係数が上がることを、
            // 同じ来訪者数・同じ地域で満足度だけが変わる2ケース（平常 vs イベント開催で満足度up）で確認する。
            var region = Region.ById(CentralTouristAreaId);
            int visitors = (int)(region.Capacity * 0.6f);

            var plainSeason = NewSeasonWithEvents();
            var plainResult = new DayEvaluator().Evaluate(AllocationFor(CentralTouristAreaId, visitors), new Plan(1), plainSeason);

            var eventSeason = NewSeasonWithEvents();
            var eventPlan = new Plan(1);
            eventPlan.Measures.Add(new Measure(MeasureKind.EventHosting, CentralTouristAreaId));
            var eventResult = new DayEvaluator().Evaluate(AllocationFor(CentralTouristAreaId, visitors), eventPlan, eventSeason);

            Assert.Greater(eventResult.Regions[CentralTouristAreaId - 1].Satisfaction,
                plainResult.Regions[CentralTouristAreaId - 1].Satisfaction);
            Assert.Greater(eventResult.Regions[CentralTouristAreaId - 1].Revenue,
                plainResult.Regions[CentralTouristAreaId - 1].Revenue,
                "満足度が上がれば収益係数(0.5+満足度/200)も上がり、同じ来訪者数でも収益が増えるはずである（5.4節手順4）。");
        }
    }
}
