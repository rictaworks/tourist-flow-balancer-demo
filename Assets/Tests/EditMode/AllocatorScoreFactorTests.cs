using NUnit.Framework;
using TouristFlowBalancer.Core;
using TouristFlowBalancer.Logic;

namespace TouristFlowBalancer.Tests.EditMode
{
    /// <summary>
    /// requirements.md 4.1節：セグメント別スコア＝6係数（魅力・タグ一致・認知度・アクセス・価格・混雑回避）の積を検証する。
    /// <see cref="AllocatorTests"/> は最大剰余法・二段階混雑予測・入場制限ウォーターフォール・
    /// 予測と確定の同一関数・混雑回避係数の連続性はカバーしているが、混雑回避係数以外の5係数
    /// （魅力・タグ一致・認知度・アクセス・価格）が実際に配分結果へ反映されているかは検証していなかった
    /// （Issue #3受け入れ条件「6要素の積」の一部が未カバーだったため追加）。
    ///
    /// 他4地域を閉鎖して候補を「中央観光地（id=1）」「山里（id=4）」の2地域だけに絞ることで、
    /// 各係数の効果をそれ以外の要因から切り分けて検証する。
    /// </summary>
    public class AllocatorScoreFactorTests
    {
        private const int CentralTouristAreaId = 1; // 中央観光地：基礎魅力90・基礎認知度100・アクセス段数1・タグ{History,Gourmet}・屋内
        private const int MountainVillageId = 4;     // 山里：基礎魅力60・基礎認知度25・アクセス段数3・タグ{Nature,Experience}・屋外

        private static Season TwoOpenRegions(int regionA, int regionB)
        {
            var season = new Season(seed: 1);
            foreach (var region in Region.All)
            {
                if (region.Id != regionA && region.Id != regionB)
                {
                    season.GetRegionState(region.Id).Closed = true;
                }
            }
            return season;
        }

        private static EventDay NormalEvent(int day = 1)
        {
            return new EventDay(day, EventKind.Normal, EventDay.NoTargetRegionId);
        }

        private static EventDay RainEvent(int day = 1)
        {
            return new EventDay(day, EventKind.Rain, EventDay.NoTargetRegionId);
        }

        // --- 魅力・タグ一致・認知度・アクセスの積を厳密値で検証（価格・混雑回避係数は1.0固定の条件） ---

        [Test]
        public void Score_AttractionTagAwarenessAccess_ProduceExactSplit_NoMeasuresNoCrowdInfo()
        {
            // 中央観光地(id=1)と山里(id=4)だけを開放し、施策なし・混雑情報なし・平常日で配分する。
            // 4セグメントそれぞれの score = 魅力×タグ一致×認知度係数×アクセス係数（価格係数・混雑回避係数は
            // いずれも1.0）を4.1節の定義どおりに手計算すると、中央観光地/山里のシェアは
            // 家族90/106・若年234/259・シニア351/376・訪日1521/1671 となる。
            // 来訪数1000人でこれを展開すると、中央観光地への生の配分合計は約894.508（残り約105.492が山里）となり、
            // 最大剰余法（余りの大きい順）で中央観光地に+1され、895／105 に確定するはずである。
            var season = TwoOpenRegions(CentralTouristAreaId, MountainVillageId);
            var plan = new Plan(1);

            var allocation = new Allocator().Allocate(1000, plan, season, NormalEvent());

            Assert.AreEqual(895, allocation.VisitorsByRegion[CentralTouristAreaId - 1],
                "魅力×タグ一致×認知度×アクセスの積が4.1節どおりであれば895人になるはずである。");
            Assert.AreEqual(105, allocation.VisitorsByRegion[MountainVillageId - 1]);
        }

        // --- 価格係数（クーポン。5.10節：価格感度高1.3／低1.1） ---

        [Test]
        public void Coupon_IncreasesTargetRegionShare()
        {
            var without = new Allocator().Allocate(
                1000, new Plan(1), TwoOpenRegions(CentralTouristAreaId, MountainVillageId), NormalEvent());

            var planWithCoupon = new Plan(1);
            planWithCoupon.Measures.Add(new Measure(MeasureKind.Coupon, MountainVillageId));
            var withCoupon = new Allocator().Allocate(
                1000, planWithCoupon, TwoOpenRegions(CentralTouristAreaId, MountainVillageId), NormalEvent());

            Assert.Greater(
                withCoupon.VisitorsByRegion[MountainVillageId - 1],
                without.VisitorsByRegion[MountainVillageId - 1],
                "クーポンの価格係数（高1.3／低1.1）はいずれも1.0超のため、対象地域の配分は増えるはずである。");
        }

        // --- 認知度係数（プロモーションの認知度加算+15。5.10節） ---

        [Test]
        public void Promotion_IncreasesTargetRegionShare()
        {
            var without = new Allocator().Allocate(
                1000, new Plan(1), TwoOpenRegions(CentralTouristAreaId, MountainVillageId), NormalEvent());

            var planWithPromotion = new Plan(1);
            planWithPromotion.Measures.Add(new Measure(MeasureKind.Promotion, MountainVillageId));
            var withPromotion = new Allocator().Allocate(
                1000, planWithPromotion, TwoOpenRegions(CentralTouristAreaId, MountainVillageId), NormalEvent());

            Assert.Greater(
                withPromotion.VisitorsByRegion[MountainVillageId - 1],
                without.VisitorsByRegion[MountainVillageId - 1],
                "プロモーションは認知度係数の分子（有効認知度）を+15するため、対象地域の配分は増えるはずである。");
        }

        // --- 魅力係数（イベント開催×1.4。5.10節） ---

        [Test]
        public void EventHosting_IncreasesTargetRegionShare()
        {
            var without = new Allocator().Allocate(
                1000, new Plan(1), TwoOpenRegions(CentralTouristAreaId, MountainVillageId), NormalEvent());

            var planWithEvent = new Plan(1);
            planWithEvent.Measures.Add(new Measure(MeasureKind.EventHosting, MountainVillageId));
            var withEvent = new Allocator().Allocate(
                1000, planWithEvent, TwoOpenRegions(CentralTouristAreaId, MountainVillageId), NormalEvent());

            Assert.Greater(
                withEvent.VisitorsByRegion[MountainVillageId - 1],
                without.VisitorsByRegion[MountainVillageId - 1],
                "イベント開催は魅力係数を×1.4するため、対象地域の配分は増えるはずである。");
        }

        // --- アクセス係数（臨時バスによる実効段数-1。5.10節） ---

        [Test]
        public void ShuttleBus_IncreasesTargetRegionShare_WhenAccessStepsAboveMinimum()
        {
            // 山里(id=4)はアクセス段数3。臨時バスで実効段数が2になり、アクセス係数
            // （1/(1+k×(実効段数-1))）が上がるため、対象地域の配分は増えるはずである
            // （中央観光地はアクセス段数1のため臨時バスを掛けても下限1でクランプされ効果が出ない。
            //  そのため効果が出る山里側で検証する）。
            var without = new Allocator().Allocate(
                1000, new Plan(1), TwoOpenRegions(CentralTouristAreaId, MountainVillageId), NormalEvent());

            var planWithBus = new Plan(1);
            planWithBus.Measures.Add(new Measure(MeasureKind.ShuttleBus, MountainVillageId));
            var withBus = new Allocator().Allocate(
                1000, planWithBus, TwoOpenRegions(CentralTouristAreaId, MountainVillageId), NormalEvent());

            Assert.Greater(
                withBus.VisitorsByRegion[MountainVillageId - 1],
                without.VisitorsByRegion[MountainVillageId - 1],
                "臨時バスは実効アクセス段数を1減らしアクセス係数を上げるため、対象地域の配分は増えるはずである。");
        }

        // --- 魅力係数（雨×0.6は屋外型のみ。5.10節） ---

        [Test]
        public void Rain_DecreasesOutdoorRegionShare_ComparedToNormalEvent()
        {
            // 山里(id=4)は屋外型。雨の日は魅力が×0.6されるため、屋内型の中央観光地(id=1)と競合させた
            // ときのシェアが平常時より下がるはずである。
            var normal = new Allocator().Allocate(
                1000, new Plan(1), TwoOpenRegions(CentralTouristAreaId, MountainVillageId), NormalEvent());
            var rainy = new Allocator().Allocate(
                1000, new Plan(1), TwoOpenRegions(CentralTouristAreaId, MountainVillageId), RainEvent());

            Assert.Less(
                rainy.VisitorsByRegion[MountainVillageId - 1],
                normal.VisitorsByRegion[MountainVillageId - 1],
                "雨の日は屋外型地域（山里）の魅力が×0.6されるため、平常時より配分は減るはずである。");
        }

        // --- 魅力係数（反発状態×0.8。5.10節） ---

        [Test]
        public void Backlash_DecreasesRegionShare()
        {
            var withoutBacklash = new Allocator().Allocate(
                1000, new Plan(1), TwoOpenRegions(CentralTouristAreaId, MountainVillageId), NormalEvent());

            var backlashSeason = TwoOpenRegions(CentralTouristAreaId, MountainVillageId);
            backlashSeason.GetRegionState(MountainVillageId).Backlash = true;
            var withBacklash = new Allocator().Allocate(1000, new Plan(1), backlashSeason, NormalEvent());

            Assert.Less(
                withBacklash.VisitorsByRegion[MountainVillageId - 1],
                withoutBacklash.VisitorsByRegion[MountainVillageId - 1],
                "反発状態は魅力係数を×0.8するため、対象地域の配分は減るはずである。");
        }
    }
}
