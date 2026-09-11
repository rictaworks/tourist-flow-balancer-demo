using NUnit.Framework;
using TouristFlowBalancer.Core;
using TouristFlowBalancer.Logic;

namespace TouristFlowBalancer.Tests.EditMode
{
    /// <summary>
    /// Issue #4 受け入れ条件：「ユニットテストで14日シーズンを最後まで進行できることを確認する」。
    /// SeasonFactory（関数A）→ 毎日 検証(関数B)・配分(関数C, 参照のみ)・評価(関数D)・更新(関数E) →
    /// 14日目に精算(関数F)、という一連の流れを通しで実行し、例外なく完走することを確認する。
    /// </summary>
    public class FullSeasonLoopTests
    {
        [Test]
        public void FullSeason_CompletesFourteenDays_WithAutoPlannerEachDay()
        {
            var season = SeasonFactory.Create(seed: 2024);
            var allocator = new Allocator();
            var evaluator = new DayEvaluator();
            var advancer = new DayAdvancer();
            var autoPlanner = new AutoPlanner();

            int daysProcessed = 0;
            bool seasonOver = false;

            for (int i = 0; i < GameConstants.SeasonLengthDays; i++)
            {
                Assert.AreEqual(SeasonState.Planning, season.State, $"day {season.Day}");
                EventDay todayEvent = season.Events[season.Day - 1];

                // おまかせプラン（関数H）で今日のプランを組む。
                var plan = autoPlanner.Build(season, todayEvent, season.Budget);

                // 確定前に関数Bで最終検証する（おまかせプランはエラーなしで確定できるはずである）。
                var forecastForValidation = allocator.Allocate(season.BaseArrivals(season.Day), plan, season, todayEvent);
                var report = new PlanValidator().Validate(plan, season, forecastForValidation);
                Assert.IsTrue(report.CanConfirm(), $"day {season.Day}: {string.Join("; ", report.Errors)}");

                // 確定：実際の来訪数で配分する（関数C、予測と同一関数）。
                int actualArrivals = season.Arrivals[season.Day - 1];
                var allocation = allocator.Allocate(actualArrivals, plan, season, todayEvent);

                // 日次評価（関数D）。
                var dayResult = evaluator.Evaluate(allocation, plan, season);
                Assert.AreEqual(season.Day, dayResult.Day);
                Assert.AreEqual(Region.Count, dayResult.Regions.Length);

                daysProcessed++;

                // 日次更新（関数E）。永続化はテストの副作用を避けるため渡さない。
                seasonOver = advancer.Advance(season, persistence: null);

                if (seasonOver)
                {
                    break;
                }
            }

            Assert.AreEqual(GameConstants.SeasonLengthDays, daysProcessed, "14日すべてを評価できているはずである。");
            Assert.IsTrue(seasonOver, "14日目の評価後、DayAdvancerはシーズン終了(true)を返すはずである。");

            // シーズン精算（関数F）。
            var result = new SeasonFinalizer().Finalize(season, persistence: null);

            Assert.AreEqual(SeasonState.Finalized, season.State);
            Assert.AreEqual(result.Score, result.SatisfiedTotal - result.GiveUpTotal);
            Assert.AreEqual(Region.Count, result.VisitorsByRegion.Length);
            Assert.AreEqual(Region.Count, result.FinalSentimentByRegion.Length);
        }

        [Test]
        public void FullSeason_WithEmptyPlanEveryDay_AlsoCompletesFourteenDays()
        {
            // 「おまかせプランだけで完走できる」だけでなく、施策を1つも選ばない来場者でも
            // 例外なく14日を完走できることを確認する（14.2節末尾）。
            var season = SeasonFactory.Create(seed: 555);
            var allocator = new Allocator();
            var evaluator = new DayEvaluator();
            var advancer = new DayAdvancer();

            int daysProcessed = 0;
            bool seasonOver = false;

            for (int i = 0; i < GameConstants.SeasonLengthDays; i++)
            {
                EventDay todayEvent = season.Events[season.Day - 1];
                var plan = new Plan(season.Day); // 空のプラン

                int actualArrivals = season.Arrivals[season.Day - 1];
                var allocation = allocator.Allocate(actualArrivals, plan, season, todayEvent);

                evaluator.Evaluate(allocation, plan, season);

                daysProcessed++;
                seasonOver = advancer.Advance(season, persistence: null);
                if (seasonOver)
                {
                    break;
                }
            }

            Assert.AreEqual(GameConstants.SeasonLengthDays, daysProcessed);
            Assert.IsTrue(seasonOver);

            var result = new SeasonFinalizer().Finalize(season, persistence: null);
            Assert.IsNotNull(result);
            Assert.GreaterOrEqual(season.Stats.GiveUpTotal, 0);
        }
    }
}
