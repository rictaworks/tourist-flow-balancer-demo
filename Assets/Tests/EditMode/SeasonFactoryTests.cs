using NUnit.Framework;
using TouristFlowBalancer.Core;
using TouristFlowBalancer.Logic;

namespace TouristFlowBalancer.Tests.EditMode
{
    /// <summary>
    /// requirements.md 5.1節：シーズン初期化（関数A, SeasonFactory）を検証する。
    /// </summary>
    public class SeasonFactoryTests
    {
        [Test]
        public void Create_GeneratesFourteenDaysOfEventsAndArrivals()
        {
            var season = SeasonFactory.Create(seed: 12345);

            Assert.AreEqual(GameConstants.SeasonLengthDays, season.Events.Length);
            Assert.AreEqual(GameConstants.SeasonLengthDays, season.Arrivals.Length);
            for (int i = 0; i < season.Events.Length; i++)
            {
                Assert.IsNotNull(season.Events[i], $"day {i + 1}");
                Assert.AreEqual(i + 1, season.Events[i].Day);
                Assert.Greater(season.Arrivals[i], 0, $"day {i + 1}");
            }
        }

        [Test]
        public void Create_IsDeterministic_ForSameSeed()
        {
            var first = SeasonFactory.Create(seed: 777);
            var second = SeasonFactory.Create(seed: 777);

            for (int i = 0; i < GameConstants.SeasonLengthDays; i++)
            {
                Assert.AreEqual(first.Events[i].Kind, second.Events[i].Kind, $"day {i + 1} kind");
                Assert.AreEqual(first.Events[i].TargetRegionId, second.Events[i].TargetRegionId, $"day {i + 1} target");
                Assert.AreEqual(first.Arrivals[i], second.Arrivals[i], $"day {i + 1} arrivals");
            }
        }

        [Test]
        public void Create_DifferentSeeds_CanProduceDifferentEventSequences()
        {
            // 決定性の裏返し：異なるシードは（極めて高い確率で）異なる出来事系列を生成する。
            // 統計的な保証ではなく、境界的な「同一実装のコピペで乱数が固定化されていないか」の検知が目的。
            bool anyDifference = false;
            var a = SeasonFactory.Create(seed: 1);
            var b = SeasonFactory.Create(seed: 2);
            for (int i = 0; i < GameConstants.SeasonLengthDays; i++)
            {
                if (a.Events[i].Kind != b.Events[i].Kind || a.Arrivals[i] != b.Arrivals[i])
                {
                    anyDifference = true;
                    break;
                }
            }
            Assert.IsTrue(anyDifference, "異なるシードなのに全14日が完全に一致した（乱数系列が固定されている疑いがある）。");
        }

        [Test]
        public void Create_EventTargets_AreNeverCentralTouristArea()
        {
            const int centralTouristAreaId = 1;
            for (int seed = 0; seed < 50; seed++)
            {
                var season = SeasonFactory.Create(seed);
                foreach (var eventDay in season.Events)
                {
                    if (eventDay.Kind == EventKind.SnsBuzz || eventDay.Kind == EventKind.TrafficDisruption)
                    {
                        Assert.AreNotEqual(centralTouristAreaId, eventDay.TargetRegionId,
                            $"seed={seed} day={eventDay.Day}: 中央観光地はSNSバズ・交通障害の対象にならない（3.4節）。");
                    }
                }
            }
        }

        [Test]
        public void Create_Day1SnsBuzz_AppliesAwarenessBonusImmediately()
        {
            // SNSバズが1日目に出るシードを探す（決定的なので見つかれば以後も再現する）。
            for (int seed = 0; seed < 500; seed++)
            {
                var season = SeasonFactory.Create(seed);
                if (season.Events[0].Kind != EventKind.SnsBuzz)
                {
                    continue;
                }

                int targetId = season.Events[0].TargetRegionId;
                var state = season.GetRegionState(targetId);
                int expectedAwareness = Region.ById(targetId).BaseAwareness + GameConstants.SnsBuzzAwarenessBonus;
                if (expectedAwareness > GameConstants.AwarenessMax)
                {
                    expectedAwareness = GameConstants.AwarenessMax;
                }

                Assert.AreEqual(expectedAwareness, state.Awareness,
                    $"seed={seed}: 1日目にSNSバズが出た地域の認知度へ即時+30が反映されているはずである（5.1節手順6）。");
                return;
            }

            Assert.Inconclusive("シード0〜499の範囲で1日目にSNSバズが出るケースが見つからなかった。");
        }

        [Test]
        public void Create_Day1TrafficDisruptionOnIsland_ClosesIslandImmediately()
        {
            const int islandRegionId = 6;
            for (int seed = 0; seed < 500; seed++)
            {
                var season = SeasonFactory.Create(seed);
                if (season.Events[0].Kind == EventKind.TrafficDisruption && season.Events[0].TargetRegionId == islandRegionId)
                {
                    Assert.IsTrue(season.GetRegionState(islandRegionId).Closed,
                        $"seed={seed}: 1日目に離島への交通障害が出た場合、初日から欠航（閉鎖）が反映されているはずである。");
                    return;
                }
            }

            Assert.Inconclusive("シード0〜499の範囲で1日目に離島の交通障害が出るケースが見つからなかった。");
        }

        [Test]
        public void Create_RainDay_ReducesBaseArrivalsByFactor()
        {
            for (int seed = 0; seed < 500; seed++)
            {
                var season = SeasonFactory.Create(seed);
                for (int day = 1; day <= GameConstants.SeasonLengthDays; day++)
                {
                    if (season.Events[day - 1].Kind != EventKind.Rain)
                    {
                        continue;
                    }

                    int weekdayOrWeekendBase = season.IsWeekend(day)
                        ? GameConstants.BaseArrivalsWeekend
                        : GameConstants.BaseArrivalsWeekday;
                    int expected = (int)(weekdayOrWeekendBase * GameConstants.RainArrivalsFactor);

                    Assert.AreEqual(expected, season.BaseArrivals(day),
                        $"seed={seed} day={day}: 雨の日はBaseArrivalsに×0.85が適用されているはずである（3.3節）。");
                    return;
                }
            }

            Assert.Inconclusive("シード0〜499の範囲で雨の日が見つからなかった。");
        }

        [Test]
        public void Create_RegionsStartAtBaseAwareness_AndZeroSentiment_AndNoBacklash()
        {
            var season = SeasonFactory.Create(seed: 42);

            foreach (var region in Region.All)
            {
                var state = season.GetRegionState(region.Id);
                bool isSnsBuzzDay1Target =
                    season.Events[0].Kind == EventKind.SnsBuzz && season.Events[0].TargetRegionId == region.Id;

                if (!isSnsBuzzDay1Target)
                {
                    Assert.AreEqual(region.BaseAwareness, state.Awareness, $"region {region.Id}");
                }
                Assert.AreEqual(0, state.Sentiment, $"region {region.Id}");
                Assert.IsFalse(state.Backlash, $"region {region.Id}");
            }
        }

        [Test]
        public void Create_InitialBudgetAndDayAndState()
        {
            var season = SeasonFactory.Create(seed: 99);

            Assert.AreEqual(1, season.Day);
            Assert.AreEqual(GameConstants.InitialBudget, season.Budget);
            Assert.AreEqual(SeasonState.Planning, season.State);
        }
    }
}
