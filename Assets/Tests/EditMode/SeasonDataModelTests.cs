using NUnit.Framework;
using TouristFlowBalancer.Core;

namespace TouristFlowBalancer.Tests.EditMode
{
    /// <summary>
    /// requirements.md 6章のデータ仕様どおりのフィールドをSeason・Plan・Stats等が持つことを検証する。
    /// </summary>
    public class SeasonDataModelTests
    {
        [Test]
        public void NewSeason_HasExpectedInitialState()
        {
            var season = new Season(seed: 2026);

            Assert.AreEqual(2026, season.Seed);
            Assert.AreEqual(1, season.Day);
            Assert.AreEqual(GameConstants.InitialBudget, season.Budget);
            Assert.AreEqual(SeasonState.Planning, season.State);
            Assert.AreEqual(Region.Count, season.Regions.Length);
            Assert.AreEqual(GameConstants.SeasonLengthDays, season.Events.Length);
            Assert.AreEqual(GameConstants.SeasonLengthDays, season.Arrivals.Length);
            Assert.IsNotNull(season.Stats);
        }

        [Test]
        public void NewSeason_RegionStates_StartAtBaseAwarenessAndZeroSentiment()
        {
            var season = new Season(seed: 1);

            foreach (var region in Region.All)
            {
                var state = season.GetRegionState(region.Id);
                Assert.AreEqual(region.BaseAwareness, state.Awareness);
                Assert.AreEqual(0, state.Sentiment);
                Assert.IsFalse(state.Backlash);
                Assert.IsFalse(state.Closed);
            }
        }

        [TestCase(1, false)]
        [TestCase(5, false)]
        [TestCase(6, true)]
        [TestCase(7, true)]
        [TestCase(12, false)]
        [TestCase(13, true)]
        [TestCase(14, true)]
        public void IsWeekend_MatchesCalendar(int day, bool expectedWeekend)
        {
            var season = new Season(seed: 1);
            Assert.AreEqual(expectedWeekend, season.IsWeekend(day));
        }

        [Test]
        public void BaseArrivals_WeekdayIs1200_WeekendIs1800()
        {
            var season = new Season(seed: 1);
            Assert.AreEqual(1200, season.BaseArrivals(1));
            Assert.AreEqual(1800, season.BaseArrivals(6));
        }

        [Test]
        public void Plan_TotalCost_SumsMeasureCosts()
        {
            var plan = new Plan(day: 1);
            plan.Measures.Add(new Measure(MeasureKind.CrowdInfo, Measure.AllRegionsTarget));
            plan.Measures.Add(new Measure(MeasureKind.Promotion, regionId: 5));

            Assert.AreEqual(20 + 40, plan.TotalCost());
        }

        [Test]
        public void Plan_Has_DetectsDuplicateMeasure()
        {
            var plan = new Plan(day: 1);
            plan.Measures.Add(new Measure(MeasureKind.Promotion, regionId: 5));

            Assert.IsTrue(plan.Has(MeasureKind.Promotion, 5));
            Assert.IsFalse(plan.Has(MeasureKind.Promotion, 6));
        }

        [Test]
        public void RegionState_EffectiveAccess_ShuttleBusReducesByOneWithLowerBoundOne()
        {
            var region = Region.ById(1); // accessSteps = 1
            var state = new RegionState(region.Id, region.BaseAwareness);
            var plan = new Plan(day: 1);
            plan.Measures.Add(new Measure(MeasureKind.ShuttleBus, region.Id));

            Assert.AreEqual(1, state.EffectiveAccess(region, plan)); // 下限1のため1のまま
        }

        [Test]
        public void RegionState_EffectiveAccess_TrafficDisruptionAddsPenalty()
        {
            var region = Region.ById(4); // accessSteps = 3
            var state = new RegionState(region.Id, region.BaseAwareness)
            {
                AccessPenalty = GameConstants.TrafficDisruptionAccessPenalty,
            };

            Assert.AreEqual(5, state.EffectiveAccess(region, plan: null));
        }

        [Test]
        public void Stats_ArraysAreSizedPerRegionAndMeasureKind()
        {
            var stats = new Stats();

            Assert.AreEqual(Region.Count, stats.VisitorsByRegion.Length);
            Assert.AreEqual(Region.Count, stats.RevenueByRegion.Length);
            Assert.AreEqual(Region.Count, stats.OvercrowdedDays.Length);
            Assert.AreEqual(Region.Count, stats.DesertedDays.Length);
            Assert.AreEqual(Stats.MeasureKindCount, stats.UsesByMeasure.Length);
            Assert.AreEqual(0, stats.SatisfiedTotal);
            Assert.AreEqual(0, stats.GiveUpTotal);
        }

        [Test]
        public void MeasureCatalog_CostsMatchSpec()
        {
            Assert.AreEqual(20, MeasureCatalog.CostOf(MeasureKind.CrowdInfo));
            Assert.AreEqual(30, MeasureCatalog.CostOf(MeasureKind.Coupon));
            Assert.AreEqual(40, MeasureCatalog.CostOf(MeasureKind.Promotion));
            Assert.AreEqual(50, MeasureCatalog.CostOf(MeasureKind.ShuttleBus));
            Assert.AreEqual(60, MeasureCatalog.CostOf(MeasureKind.EventHosting));
            Assert.AreEqual(0, MeasureCatalog.CostOf(MeasureKind.EntryLimit));
        }
    }
}
