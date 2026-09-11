using NUnit.Framework;
using TouristFlowBalancer.Core;
using TouristFlowBalancer.Logic;

namespace TouristFlowBalancer.Tests.EditMode
{
    /// <summary>
    /// requirements.md 5.7節：おまかせプラン（関数H, AutoPlanner）を検証する。
    /// 予算を超えないこと・確定可能（エラーなし）なプランを返すことを主な受け入れ条件とする。
    /// </summary>
    public class AutoPlannerTests
    {
        private const int CentralTouristAreaId = 1;

        private static Season NewSeason(int seed = 1)
        {
            return new Season(seed);
        }

        private static EventDay NormalEvent(int day = 1)
        {
            return new EventDay(day, EventKind.Normal, EventDay.NoTargetRegionId);
        }

        [Test]
        public void Build_NullSeason_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => new AutoPlanner().Build(null, NormalEvent(), 100));
        }

        [Test]
        public void Build_NeverExceedsBudget()
        {
            int[] budgets = { 0, 10, 30, 50, 80, 100, 150, 200 };
            foreach (int budget in budgets)
            {
                var season = NewSeason();
                var plan = new AutoPlanner().Build(season, NormalEvent(), budget);

                Assert.LessOrEqual(plan.TotalCost(), budget, $"budget={budget}");
            }
        }

        [Test]
        public void Build_ProducesConfirmableValidPlan()
        {
            var season = NewSeason();
            var plan = new AutoPlanner().Build(season, NormalEvent(), 100);

            var forecast = new Allocator().Allocate(season.BaseArrivals(season.Day), plan, season, NormalEvent());
            var report = new PlanValidator().Validate(plan, season, forecast);

            Assert.IsTrue(report.CanConfirm(), string.Join("; ", report.Errors));
        }

        [Test]
        public void Build_ZeroBudget_StillProducesConfirmablePlan()
        {
            // 予算0でも、費用0の入場制限だけは追加されうる。おまかせは施策を選ばなくても成立する設計
            // （5.7節末尾）なので、空でも有効なプランを返せるはずである。
            var season = NewSeason();
            var plan = new AutoPlanner().Build(season, NormalEvent(), 0);

            var forecast = new Allocator().Allocate(season.BaseArrivals(season.Day), plan, season, NormalEvent());
            var report = new PlanValidator().Validate(plan, season, forecast);

            Assert.AreEqual(0, plan.TotalCost());
            Assert.IsTrue(report.CanConfirm());
        }

        [Test]
        public void Build_WeekendWithOvercrowdedForecast_AddsCrowdInfo()
        {
            var season = NewSeason();
            season.Day = 6; // 週末（6・7・13・14日目）。何も施策がなければ中央観光地は過密になる設計（14.2節）。

            var plan = new AutoPlanner().Build(season, NormalEvent(6), 100);

            Assert.IsTrue(plan.Has(MeasureKind.CrowdInfo, Measure.AllRegionsTarget),
                "週末は何もしなければ過密（混雑率1.2超）になる設計のため、混雑情報の掲示が追加されるはずである。");
        }

        [Test]
        public void Build_Deterministic_ForSameInputs()
        {
            var first = new AutoPlanner().Build(NewSeason(), NormalEvent(), 100);
            var second = new AutoPlanner().Build(NewSeason(), NormalEvent(), 100);

            Assert.AreEqual(first.Measures.Count, second.Measures.Count);
            for (int i = 0; i < first.Measures.Count; i++)
            {
                Assert.AreEqual(first.Measures[i].Kind, second.Measures[i].Kind, $"measure {i}");
                Assert.AreEqual(first.Measures[i].RegionId, second.Measures[i].RegionId, $"measure {i}");
            }
        }

        [Test]
        public void Build_NeverDuplicatesTheSameMeasureOnTheSameRegion()
        {
            var season = NewSeason();
            season.Day = 6; // 週末で複数の施策が追加されやすい条件
            var plan = new AutoPlanner().Build(season, NormalEvent(6), 200);

            var seen = new System.Collections.Generic.HashSet<(MeasureKind, int)>();
            foreach (var measure in plan.Measures)
            {
                Assert.IsTrue(seen.Add((measure.Kind, measure.RegionId)),
                    $"施策{measure.Kind}が地域{measure.RegionId}に重複している。");
            }
        }
    }
}
