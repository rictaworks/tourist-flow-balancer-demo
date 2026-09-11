using NUnit.Framework;
using TouristFlowBalancer.Core;
using TouristFlowBalancer.Logic;

namespace TouristFlowBalancer.Tests.EditMode
{
    /// <summary>
    /// requirements.md 5.2節：プラン検証（関数B, PlanValidator）の各エラー・警告・助言パターンを検証する。
    /// </summary>
    public class PlanValidatorTests
    {
        private const int CentralTouristAreaId = 1;
        private const int RemoteIslandId = 6;

        private static Season NewSeason(int budget = 100)
        {
            var season = new Season(seed: 1) { Budget = budget };
            return season;
        }

        private static EventDay NormalEvent(int day = 1)
        {
            return new EventDay(day, EventKind.Normal, EventDay.NoTargetRegionId);
        }

        [Test]
        public void OverBudget_IsError_AndCannotConfirm()
        {
            var season = NewSeason(budget: 10);
            var plan = new Plan(1);
            plan.Measures.Add(new Measure(MeasureKind.EventHosting, CentralTouristAreaId)); // cost 60

            var forecast = new Allocator().Allocate(1200, plan, season, NormalEvent());
            var report = new PlanValidator().Validate(plan, season, forecast);

            Assert.IsNotEmpty(report.Errors);
            Assert.IsFalse(report.CanConfirm());
        }

        [Test]
        public void WithinBudget_NoOverBudgetError()
        {
            var season = NewSeason(budget: 100);
            var plan = new Plan(1);
            plan.Measures.Add(new Measure(MeasureKind.CrowdInfo, Measure.AllRegionsTarget)); // cost 20

            var forecast = new Allocator().Allocate(1200, plan, season, NormalEvent());
            var report = new PlanValidator().Validate(plan, season, forecast);

            Assert.IsTrue(report.CanConfirm());
        }

        [Test]
        public void DuplicateMeasure_IsError()
        {
            var season = NewSeason();
            var plan = new Plan(1);
            plan.Measures.Add(new Measure(MeasureKind.Coupon, CentralTouristAreaId));
            plan.Measures.Add(new Measure(MeasureKind.Coupon, CentralTouristAreaId));

            var forecast = new Allocator().Allocate(1200, plan, season, NormalEvent());
            var report = new PlanValidator().Validate(plan, season, forecast);

            Assert.IsNotEmpty(report.Errors);
            Assert.IsFalse(report.CanConfirm());
        }

        [Test]
        public void MeasureOnClosedRegion_IsWarning_ButCanStillConfirm()
        {
            var season = NewSeason();
            season.GetRegionState(RemoteIslandId).Closed = true;
            var plan = new Plan(1);
            plan.Measures.Add(new Measure(MeasureKind.Promotion, RemoteIslandId));

            var forecast = new Allocator().Allocate(1200, plan, season, NormalEvent());
            var report = new PlanValidator().Validate(plan, season, forecast);

            Assert.IsNotEmpty(report.Warnings);
            Assert.IsTrue(report.CanConfirm(), "警告のみでは確定を妨げないはずである。");
        }

        [Test]
        public void CrowdInfoOnAllRegions_IsNotFlaggedAsTargetingClosedRegion()
        {
            // CrowdInfoはAllRegionsTarget(0)を使うため、閉鎖判定の対象外であるべき。
            var season = NewSeason();
            season.GetRegionState(RemoteIslandId).Closed = true;
            var plan = new Plan(1);
            plan.Measures.Add(new Measure(MeasureKind.CrowdInfo, Measure.AllRegionsTarget));

            var forecast = new Allocator().Allocate(1200, plan, season, NormalEvent());
            var report = new PlanValidator().Validate(plan, season, forecast);

            Assert.IsEmpty(report.Warnings);
        }

        [Test]
        public void MeasureWorseningCongestion_AboveOne_IsWarning()
        {
            var season = NewSeason();
            var plan = new Plan(1);
            // 中央観光地は基礎値が最も強く、来訪数1800なら一段階目で混雑率1.0超になりやすい。
            plan.Measures.Add(new Measure(MeasureKind.EventHosting, CentralTouristAreaId));

            var forecast = new Allocator().Allocate(1800, plan, season, NormalEvent());
            float congestion = (float)forecast.VisitorsByRegion[CentralTouristAreaId - 1] / Region.ById(CentralTouristAreaId).Capacity;
            Assume.That(congestion > 1.0f, "前提：このテストケースでは中央観光地の混雑率が1.0を超えている必要がある。");

            var report = new PlanValidator().Validate(plan, season, forecast);

            Assert.IsNotEmpty(report.Warnings);
        }

        [Test]
        public void OvercrowdedForecast_WithoutCrowdInfoOrEntryLimit_IsAdvice()
        {
            var season = NewSeason();
            var plan = new Plan(1); // 何も打たない

            var forecast = new Allocator().Allocate(1800, plan, season, NormalEvent());
            float congestion = (float)forecast.VisitorsByRegion[CentralTouristAreaId - 1] / Region.ById(CentralTouristAreaId).Capacity;
            Assume.That(congestion > 1.2f, "前提：何も施策を打たなければ中央観光地は週末に過密（混雑率1.2超）になる設計である（14.2節）。");

            var report = new PlanValidator().Validate(plan, season, forecast);

            Assert.IsNotEmpty(report.Advices);
        }

        [Test]
        public void OvercrowdedForecast_WithCrowdInfo_NoAdviceForThatReason()
        {
            var season = NewSeason();
            var plan = new Plan(1);
            plan.Measures.Add(new Measure(MeasureKind.CrowdInfo, Measure.AllRegionsTarget));

            var forecast = new Allocator().Allocate(1800, plan, season, NormalEvent());
            var report = new PlanValidator().Validate(plan, season, forecast);

            // 混雑情報の掲示があるため、手順5の「過密見込み」助言は出ないはずである
            // （手順4の「逆効果施策」警告はCrowdInfo自体には出ない——対象はクーポン等のみ）。
            foreach (string advice in report.Advices)
            {
                StringAssert.DoesNotContain("過密が見込まれます", advice);
            }
        }

        [Test]
        public void EntryLimitOnAllRegions_WithGiveUps_IsAdvice()
        {
            var season = NewSeason();
            var plan = new Plan(1);
            foreach (var region in Region.All)
            {
                plan.Measures.Add(new Measure(MeasureKind.EntryLimit, region.Id));
            }

            // 総収容人数1,700人を超える来訪数で断念者を発生させる。
            var forecast = new Allocator().Allocate(2000, plan, season, NormalEvent());
            Assume.That(forecast.GiveUps, Is.GreaterThan(0));

            var report = new PlanValidator().Validate(plan, season, forecast);

            Assert.IsNotEmpty(report.Advices);
        }

        [Test]
        public void EntryLimitNotOnAllRegions_NoGiveUpAdvice_EvenIfGiveUpsOccur()
        {
            var season = NewSeason();
            var plan = new Plan(1);
            plan.Measures.Add(new Measure(MeasureKind.EntryLimit, CentralTouristAreaId));

            var forecast = new Allocator().Allocate(2000, plan, season, NormalEvent());
            var report = new PlanValidator().Validate(plan, season, forecast);

            foreach (string advice in report.Advices)
            {
                StringAssert.DoesNotContain("断念者が発生する見込み", advice);
            }
        }

        [Test]
        public void EmptyPlan_LowArrivals_HasNoIssues()
        {
            var season = NewSeason();
            var plan = new Plan(1);

            var forecast = new Allocator().Allocate(100, plan, season, NormalEvent());
            var report = new PlanValidator().Validate(plan, season, forecast);

            Assert.IsEmpty(report.Errors);
            Assert.IsTrue(report.CanConfirm());
        }

        [Test]
        public void Validate_NullArguments_Throw()
        {
            var season = NewSeason();
            var plan = new Plan(1);
            var forecast = new Allocator().Allocate(100, plan, season, NormalEvent());
            var validator = new PlanValidator();

            Assert.Throws<System.ArgumentNullException>(() => validator.Validate(null, season, forecast));
            Assert.Throws<System.ArgumentNullException>(() => validator.Validate(plan, null, forecast));
            Assert.Throws<System.ArgumentNullException>(() => validator.Validate(plan, season, null));
        }
    }
}
