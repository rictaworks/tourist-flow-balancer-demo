using NUnit.Framework;
using TouristFlowBalancer.Core;
using TouristFlowBalancer.Logic;

namespace TouristFlowBalancer.Tests.EditMode
{
    /// <summary>
    /// requirements.md 4章・5.3節・14.1節：配分計算（関数C, Allocator）を検証する。
    /// 「混雑情報掲示あり/なし」「入場制限あり/なし」の代表的なケースと、
    /// 最大剰余法での合計一致・混雑回避係数の連続性というIssue #3の受け入れ条件を対象にする。
    /// </summary>
    public class AllocatorTests
    {
        private const int CentralTouristAreaId = 1; // 中央観光地。容量450・最高の基礎魅力/認知度/アクセス
        private const int RemoteIslandId = 6;        // 離島。容量150

        private static Season NewSeason()
        {
            return new Season(seed: 1);
        }

        private static EventDay NormalEvent(int day = 1)
        {
            return new EventDay(day, EventKind.Normal, EventDay.NoTargetRegionId);
        }

        private static int Sum(int[] values)
        {
            int total = 0;
            for (int i = 0; i < values.Length; i++)
            {
                total += values[i];
            }
            return total;
        }

        // --- 混雑情報の掲示なし ---

        [Test]
        public void NoMeasures_NoCrowdInfo_SumsToArrivals_AndNoForecast()
        {
            var season = NewSeason();
            var plan = new Plan(1);
            var allocation = new Allocator().Allocate(1200, plan, season, NormalEvent());

            Assert.AreEqual(6, allocation.VisitorsByRegion.Length);
            Assert.AreEqual(1200, Sum(allocation.VisitorsByRegion));
            Assert.AreEqual(0, allocation.GiveUps);
            Assert.IsNull(allocation.ForecastCongestion, "混雑情報の掲示がない日はForecastCongestionを持たない（5.3節出力）。");
        }

        [Test]
        public void NoMeasures_VisitorCountsAreNonNegative()
        {
            var season = NewSeason();
            var plan = new Plan(1);
            var allocation = new Allocator().Allocate(1800, plan, season, NormalEvent());

            foreach (int visitors in allocation.VisitorsByRegion)
            {
                Assert.GreaterOrEqual(visitors, 0);
            }
        }

        // --- 混雑情報の掲示あり ---

        [Test]
        public void CrowdInfoPosted_ReturnsForecastCongestion_AndSumsToArrivals()
        {
            var season = NewSeason();
            var plan = new Plan(1);
            plan.Measures.Add(new Measure(MeasureKind.CrowdInfo, Measure.AllRegionsTarget));

            var allocation = new Allocator().Allocate(1800, plan, season, NormalEvent());

            Assert.IsNotNull(allocation.ForecastCongestion, "混雑情報の掲示がある日はForecastCongestionを持つ（5.3節出力）。");
            Assert.AreEqual(6, allocation.ForecastCongestion.Length);
            Assert.AreEqual(1800, Sum(allocation.VisitorsByRegion));
            Assert.AreEqual(0, allocation.GiveUps);
        }

        [Test]
        public void CrowdInfoPosted_ReducesShareOfCongestedRegion_ComparedToNoCrowdInfo()
        {
            // 中央観光地は基礎魅力・認知度・アクセスのいずれも最良であるため、大きな来訪数では
            // 一段階目の混雑予測が0.9を超えやすい。掲示があると混雑回避するセグメント（家族・シニア）が
            // 中央観光地を避けるため、掲示なしのときより中央観光地の人数が増えないはずである。
            const int arrivals = 1800;
            int centralIdx = CentralTouristAreaId - 1;

            var withoutCrowdInfo = new Allocator().Allocate(
                arrivals, new Plan(1), NewSeason(), NormalEvent());

            var planWithCrowdInfo = new Plan(1);
            planWithCrowdInfo.Measures.Add(new Measure(MeasureKind.CrowdInfo, Measure.AllRegionsTarget));
            var withCrowdInfo = new Allocator().Allocate(
                arrivals, planWithCrowdInfo, NewSeason(), NormalEvent());

            Assert.LessOrEqual(
                withCrowdInfo.VisitorsByRegion[centralIdx],
                withoutCrowdInfo.VisitorsByRegion[centralIdx],
                "混雑情報の掲示により、混雑回避するセグメントが混雑地域を避けるはずである。");
        }

        // --- 混雑回避係数の連続性（14.1節・受け入れ条件） ---

        [Test]
        public void CrowdAversionFactor_IsContinuousAcrossThreshold()
        {
            float justBelow = Allocator.CrowdAversionFactor(0.899999f);
            float atThreshold = Allocator.CrowdAversionFactor(0.9f);
            float justAbove = Allocator.CrowdAversionFactor(0.900001f);

            Assert.AreEqual(justBelow, atThreshold, 1e-3f, "閾値0.9の直前で不連続に変化してはならない。");
            Assert.AreEqual(atThreshold, justAbove, 1e-3f, "閾値0.9の直後で不連続に変化してはならない。");
        }

        [TestCase(0.0f, 1.0f)]   // raw = 1-(0-0.9) = 1.9 -> 上限1.0にクランプ
        [TestCase(0.9f, 1.0f)]  // raw = 1.0（クランプなし境界）
        [TestCase(1.5f, 0.4f)]  // raw = 1-(1.5-0.9) = 0.4（下限にちょうど一致）
        [TestCase(3.0f, 0.4f)]  // raw = 1-(3.0-0.9) = -1.1 -> 下限0.4にクランプ
        public void CrowdAversionFactor_MatchesFormulaAndClampsToRange(float congestion, float expected)
        {
            float factor = Allocator.CrowdAversionFactor(congestion);
            Assert.AreEqual(expected, factor, 1e-6f);
        }

        [Test]
        public void CrowdAversionFactor_AlwaysWithinClampRange()
        {
            for (float c = -1f; c <= 4f; c += 0.1f)
            {
                float factor = Allocator.CrowdAversionFactor(c);
                Assert.GreaterOrEqual(factor, 0.4f);
                Assert.LessOrEqual(factor, 1.0f);
            }
        }

        // --- 入場制限なし：最大剰余法で合計が一致する ---

        [TestCase(1)]
        [TestCase(7)]
        [TestCase(1199)]
        [TestCase(1200)]
        [TestCase(1201)]
        [TestCase(1800)]
        [TestCase(1801)]
        public void NoEntryLimit_SumOfVisitors_AlwaysEqualsArrivals(int arrivals)
        {
            var season = NewSeason();
            var plan = new Plan(1);
            var allocation = new Allocator().Allocate(arrivals, plan, season, NormalEvent());

            Assert.AreEqual(arrivals, Sum(allocation.VisitorsByRegion));
            Assert.AreEqual(0, allocation.GiveUps);
        }

        // --- 入場制限あり：ウォーターフォール ---

        [Test]
        public void EntryLimit_WithoutOverflow_LeavesAllocationUnchanged_NoGiveUps()
        {
            var season = NewSeason();
            var plan = new Plan(1);
            plan.Measures.Add(new Measure(MeasureKind.EntryLimit, CentralTouristAreaId));

            // 来訪数がどの地域の容量よりも小さければ、入場制限の対象でも収容人数を超えない。
            var allocation = new Allocator().Allocate(100, plan, season, NormalEvent());

            Assert.AreEqual(0, allocation.GiveUps);
            Assert.AreEqual(100, Sum(allocation.VisitorsByRegion));
            Assert.LessOrEqual(allocation.VisitorsByRegion[CentralTouristAreaId - 1], 450);
        }

        [Test]
        public void EntryLimit_SingleRegionOverflow_CapsRegion_AndRedistributesWithoutGiveUps()
        {
            var season = NewSeason();
            var plan = new Plan(1);
            plan.Measures.Add(new Measure(MeasureKind.EntryLimit, CentralTouristAreaId));

            // 総収容人数1,700人に対し来訪数1,200人。中央観光地(450)だけに制限を掛けても、
            // 他5地域の残り容量（合計1,250人）で十分に受け止められ、断念者は出ないはずである。
            var allocation = new Allocator().Allocate(1200, plan, season, NormalEvent());

            Assert.LessOrEqual(allocation.VisitorsByRegion[CentralTouristAreaId - 1], 450);
            Assert.AreEqual(0, allocation.GiveUps);
            Assert.AreEqual(1200, Sum(allocation.VisitorsByRegion));
        }

        [Test]
        public void EntryLimit_AllRegions_ExceedingTotalCapacity_ProducesGiveUps_AndRespectsEachCapacity()
        {
            var season = NewSeason();
            var plan = new Plan(1);
            foreach (var region in Region.All)
            {
                plan.Measures.Add(new Measure(MeasureKind.EntryLimit, region.Id));
            }

            // 総収容人数は1,700人。来訪数2,000人・全地域に入場制限を掛ければ、
            // 誰も収容人数を超えられず、300人が断念者になるはずである。
            const int arrivals = 2000;
            var allocation = new Allocator().Allocate(arrivals, plan, season, NormalEvent());

            foreach (var region in Region.All)
            {
                Assert.LessOrEqual(allocation.VisitorsByRegion[region.Id - 1], region.Capacity,
                    $"地域{region.Id}は入場制限があるため収容人数を超えてはならない。");
            }

            Assert.AreEqual(arrivals, Sum(allocation.VisitorsByRegion) + allocation.GiveUps,
                "地域別人数と断念者数の合計は必ず来訪数に一致する。");
            Assert.Greater(allocation.GiveUps, 0, "総収容人数(1,700)を上回る来訪数(2,000)なら断念者が出るはずである。");
            Assert.LessOrEqual(Sum(allocation.VisitorsByRegion), 1700);
        }

        [Test]
        public void EntryLimit_GiveUpsPlusVisitors_AlwaysEqualsArrivals_AcrossSeveralArrivalCounts()
        {
            int[] arrivalsToTest = { 1, 500, 1200, 1700, 1701, 2500 };

            foreach (int arrivals in arrivalsToTest)
            {
                var season = NewSeason();
                var plan = new Plan(1);
                plan.Measures.Add(new Measure(MeasureKind.EntryLimit, CentralTouristAreaId));
                plan.Measures.Add(new Measure(MeasureKind.EntryLimit, RemoteIslandId));

                var allocation = new Allocator().Allocate(arrivals, plan, season, NormalEvent());

                Assert.AreEqual(
                    arrivals,
                    Sum(allocation.VisitorsByRegion) + allocation.GiveUps,
                    $"arrivals={arrivals}");
            }
        }

        // --- 閉鎖中の地域（境界：候補が空） ---

        [Test]
        public void AllRegionsClosed_EveryoneGivesUp()
        {
            var season = NewSeason();
            foreach (var region in Region.All)
            {
                season.GetRegionState(region.Id).Closed = true;
            }
            var plan = new Plan(1);

            var allocation = new Allocator().Allocate(1000, plan, season, NormalEvent());

            Assert.AreEqual(1000, allocation.GiveUps);
            Assert.AreEqual(0, Sum(allocation.VisitorsByRegion));
            Assert.IsNull(allocation.ForecastCongestion);
        }

        [Test]
        public void OneRegionClosed_ExcludedFromAllocation()
        {
            var season = NewSeason();
            season.GetRegionState(RemoteIslandId).Closed = true;
            var plan = new Plan(1);

            var allocation = new Allocator().Allocate(1200, plan, season, NormalEvent());

            Assert.AreEqual(0, allocation.VisitorsByRegion[RemoteIslandId - 1]);
            Assert.AreEqual(1200, Sum(allocation.VisitorsByRegion));
            Assert.AreEqual(0, allocation.GiveUps);
        }

        // --- 決定性（同一入力なら常に同一結果。ゲームはルールベースで乱数を使わない） ---

        [Test]
        public void Allocate_IsDeterministic_ForSameInputs()
        {
            var plan = new Plan(1);
            plan.Measures.Add(new Measure(MeasureKind.CrowdInfo, Measure.AllRegionsTarget));
            plan.Measures.Add(new Measure(MeasureKind.EntryLimit, CentralTouristAreaId));

            var first = new Allocator().Allocate(1800, plan, NewSeason(), NormalEvent());
            var second = new Allocator().Allocate(1800, plan, NewSeason(), NormalEvent());

            Assert.AreEqual(first.GiveUps, second.GiveUps);
            CollectionAssert.AreEqual(first.VisitorsByRegion, second.VisitorsByRegion);
            CollectionAssert.AreEqual(first.ForecastCongestion, second.ForecastCongestion);
        }

        // --- 予測（揺らぎなし基準来訪数）と確定（実来訪数）は同一関数（14.1節の不変条件） ---

        [Test]
        public void ForecastAndFinalAllocation_UseTheSameFunction_DifferOnlyByArrivalsArgument()
        {
            var season = NewSeason();
            var plan = new Plan(1);
            plan.Measures.Add(new Measure(MeasureKind.CrowdInfo, Measure.AllRegionsTarget));
            var evt = NormalEvent();
            var allocator = new Allocator();

            // 予測（基準来訪数、揺らぎなし）
            int baseArrivals = season.BaseArrivals(season.Day);
            var forecast = allocator.Allocate(baseArrivals, plan, season, evt);

            // 確定（実際の来訪数。ここでは揺らぎを模して基準からずらす）
            int actualArrivals = baseArrivals + 37;
            var final = allocator.Allocate(actualArrivals, plan, season, evt);

            Assert.AreEqual(baseArrivals, Sum(forecast.VisitorsByRegion));
            Assert.AreEqual(actualArrivals, Sum(final.VisitorsByRegion));
        }

        // --- 引数の防御的検証 ---

        [Test]
        public void Allocate_NullPlan_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => new Allocator().Allocate(1200, null, NewSeason(), NormalEvent()));
        }

        [Test]
        public void Allocate_NullSeason_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => new Allocator().Allocate(1200, new Plan(1), null, NormalEvent()));
        }
    }
}
