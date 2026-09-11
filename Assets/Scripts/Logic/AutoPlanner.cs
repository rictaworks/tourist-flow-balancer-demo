using System;
using TouristFlowBalancer.Core;

namespace TouristFlowBalancer.Logic
{
    /// <summary>
    /// おまかせプラン（関数H autoPlan、requirements.md 5.7節）。
    /// 予算が尽きた時点で打ち切り、各手順は施策を1つ追加するごとに配分予測（<see cref="Allocator"/>）を
    /// 現在のプランで再計算する。最善手ではなく、施策を1つも選ばない来場者にも成立させるための土台。
    /// </summary>
    public sealed class AutoPlanner
    {
        // 手順4〜6が対象とする「閑散」の閾値（混雑率0.5未満）。5.7節固有の値で、GameConstantsの
        // 混雑ペナルティ・閑散(0.3)とは別物のためここに閉じ込める。
        private const float LowCongestionThreshold = 0.5f;
        private const float SevereCongestionThreshold = 1.5f;
        private const float OvercrowdedForecastThreshold = 1.2f;

        private const int ShuttleBusBudgetFloor = 50;
        private const int CouponBudgetFloor = 30;

        public Plan Build(Season season, EventDay todayEvent, int budget)
        {
            if (season == null)
            {
                throw new ArgumentNullException(nameof(season));
            }

            var plan = new Plan(season.Day);
            var allocator = new Allocator();
            int remainingBudget = budget;

            // 手順1：空のプランで配分予測を計算する。
            Allocation forecast = Forecast(allocator, season, plan, todayEvent);

            // 手順2：混雑率1.2超の地域があれば、混雑情報の掲示を追加する。
            int crowdInfoCost = MeasureCatalog.CostOf(MeasureKind.CrowdInfo);
            if (remainingBudget >= crowdInfoCost && AnyCongestionAbove(forecast, OvercrowdedForecastThreshold))
            {
                plan.Measures.Add(new Measure(MeasureKind.CrowdInfo, Measure.AllRegionsTarget, crowdInfoCost));
                remainingBudget -= crowdInfoCost;
                forecast = Forecast(allocator, season, plan, todayEvent);
            }

            // 手順3：混雑率1.5超の地域があれば、その地域に入場制限を追加する（費用0）。
            foreach (var region in Region.All)
            {
                if (season.GetRegionState(region.Id).Closed)
                {
                    continue;
                }
                if (CongestionOf(forecast, region) > SevereCongestionThreshold && !plan.Has(MeasureKind.EntryLimit, region.Id))
                {
                    plan.Measures.Add(new Measure(MeasureKind.EntryLimit, region.Id));
                    forecast = Forecast(allocator, season, plan, todayEvent);
                }
            }

            // 手順4：混雑率0.5未満かつ閉鎖中でない地域のうち認知度が最も低い地域に、プロモーションを追加する。
            int promotionCost = MeasureCatalog.CostOf(MeasureKind.Promotion);
            if (remainingBudget >= promotionCost)
            {
                Region target = PickLowestAwareness(forecast, season);
                if (target != null && !plan.Has(MeasureKind.Promotion, target.Id))
                {
                    plan.Measures.Add(new Measure(MeasureKind.Promotion, target.Id, promotionCost));
                    remainingBudget -= promotionCost;
                    forecast = Forecast(allocator, season, plan, todayEvent);
                }
            }

            // 手順5：予算が50以上残っていれば、混雑率0.5未満の地域のうちアクセス段数が最も大きい地域に臨時バスを追加する。
            if (remainingBudget >= ShuttleBusBudgetFloor)
            {
                Region target = PickHighestEffectiveAccess(forecast, season, plan);
                if (target != null && !plan.Has(MeasureKind.ShuttleBus, target.Id))
                {
                    int cost = MeasureCatalog.CostOf(MeasureKind.ShuttleBus);
                    plan.Measures.Add(new Measure(MeasureKind.ShuttleBus, target.Id, cost));
                    remainingBudget -= cost;
                    forecast = Forecast(allocator, season, plan, todayEvent);
                }
            }

            // 手順6：予算が30以上残っていれば、混雑率0.5未満の地域のうち収容人数が最も大きい地域にクーポンを追加する。
            if (remainingBudget >= CouponBudgetFloor)
            {
                Region target = PickLargestCapacity(forecast, season);
                if (target != null && !plan.Has(MeasureKind.Coupon, target.Id))
                {
                    int cost = MeasureCatalog.CostOf(MeasureKind.Coupon);
                    plan.Measures.Add(new Measure(MeasureKind.Coupon, target.Id, cost));
                    remainingBudget -= cost;
                }
            }

            // 手順7：プランを関数Bで検証し、エラーがあれば最後に追加した施策から順に外す。
            var validator = new PlanValidator();
            while (plan.Measures.Count > 0)
            {
                Allocation validationForecast = Forecast(allocator, season, plan, todayEvent);
                ValidationReport report = validator.Validate(plan, season, validationForecast);
                if (report.CanConfirm())
                {
                    break;
                }
                plan.Measures.RemoveAt(plan.Measures.Count - 1);
            }

            return plan;
        }

        private static Allocation Forecast(Allocator allocator, Season season, Plan plan, EventDay todayEvent)
        {
            return allocator.Allocate(season.BaseArrivals(season.Day), plan, season, todayEvent);
        }

        private static float CongestionOf(Allocation allocation, Region region)
        {
            return (float)allocation.VisitorsByRegion[region.Id - 1] / region.Capacity;
        }

        private static bool AnyCongestionAbove(Allocation allocation, float threshold)
        {
            foreach (var region in Region.All)
            {
                if (CongestionOf(allocation, region) > threshold)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>混雑率0.5未満かつ閉鎖中でない地域のうち、認知度が最も低い地域（同率は地域番号最小）。</summary>
        private static Region PickLowestAwareness(Allocation forecast, Season season)
        {
            Region best = null;
            int bestAwareness = int.MaxValue;
            foreach (var region in Region.All)
            {
                var state = season.GetRegionState(region.Id);
                if (state.Closed || CongestionOf(forecast, region) >= LowCongestionThreshold)
                {
                    continue;
                }
                if (state.Awareness < bestAwareness)
                {
                    bestAwareness = state.Awareness;
                    best = region;
                }
            }
            return best;
        }

        /// <summary>混雑率0.5未満かつ閉鎖中でない地域のうち、実効アクセス段数が最も大きい地域（同率は地域番号最小）。</summary>
        private static Region PickHighestEffectiveAccess(Allocation forecast, Season season, Plan plan)
        {
            Region best = null;
            int bestAccess = -1;
            foreach (var region in Region.All)
            {
                var state = season.GetRegionState(region.Id);
                if (state.Closed || CongestionOf(forecast, region) >= LowCongestionThreshold)
                {
                    continue;
                }
                int access = state.EffectiveAccess(region, plan);
                if (access > bestAccess)
                {
                    bestAccess = access;
                    best = region;
                }
            }
            return best;
        }

        /// <summary>混雑率0.5未満かつ閉鎖中でない地域のうち、収容人数が最も大きい地域（同率は地域番号最小）。</summary>
        private static Region PickLargestCapacity(Allocation forecast, Season season)
        {
            Region best = null;
            int bestCapacity = -1;
            foreach (var region in Region.All)
            {
                var state = season.GetRegionState(region.Id);
                if (state.Closed || CongestionOf(forecast, region) >= LowCongestionThreshold)
                {
                    continue;
                }
                if (region.Capacity > bestCapacity)
                {
                    bestCapacity = region.Capacity;
                    best = region;
                }
            }
            return best;
        }
    }
}
