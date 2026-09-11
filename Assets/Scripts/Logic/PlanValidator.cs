using System;
using System.Collections.Generic;
using TouristFlowBalancer.Core;

namespace TouristFlowBalancer.Logic
{
    /// <summary>
    /// プラン検証（関数B validatePlan）の結果（requirements.md 5.2節・10章）。
    /// エラーが1つ以上あれば確定不可（<see cref="CanConfirm"/>）。警告・助言は確定を妨げない。
    /// </summary>
    public sealed class ValidationReport
    {
        public List<string> Errors;
        public List<string> Warnings;
        public List<string> Advices;

        public ValidationReport()
        {
            Errors = new List<string>();
            Warnings = new List<string>();
            Advices = new List<string>();
        }

        public bool CanConfirm()
        {
            return Errors.Count == 0;
        }
    }

    /// <summary>
    /// プラン検証（関数B validatePlan、requirements.md 5.2節）。
    /// 予算超過・施策重複はエラー、閉鎖地域への施策・逆効果になり得る施策は警告、
    /// 過密見込み・断念者見込みは助言とする。
    /// </summary>
    public sealed class PlanValidator
    {
        public ValidationReport Validate(Plan plan, Season season, Allocation forecast)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }
            if (season == null)
            {
                throw new ArgumentNullException(nameof(season));
            }
            if (forecast == null)
            {
                throw new ArgumentNullException(nameof(forecast));
            }

            var report = new ValidationReport();

            // 手順1：施策費用の合計が予算を超えていればエラー。
            int totalCost = plan.TotalCost();
            if (totalCost > season.Budget)
            {
                report.Errors.Add($"施策費用の合計（{totalCost}）が予算（{season.Budget}）を超えています。");
            }

            // 手順2：同じ施策・同じ地域の組が重複していればエラー。
            var seen = new HashSet<(MeasureKind Kind, int RegionId)>();
            foreach (var measure in plan.Measures)
            {
                var key = (measure.Kind, measure.RegionId);
                if (!seen.Add(key))
                {
                    report.Errors.Add($"施策「{measure.Kind}」が地域{measure.RegionId}に重複しています。");
                }
            }

            // 手順3：閉鎖中（欠航）の地域を対象とする施策があれば警告（費用は掛かるが効果がない）。
            foreach (var measure in plan.Measures)
            {
                if (measure.RegionId == Measure.AllRegionsTarget)
                {
                    continue; // 全地域対象の施策（混雑情報の掲示）は個別の閉鎖判定の対象外。
                }
                if (season.GetRegionState(measure.RegionId).Closed)
                {
                    report.Warnings.Add($"地域{measure.RegionId}は本日欠航中のため、施策「{measure.Kind}」の効果がありません。");
                }
            }

            bool crowdInfoPosted = plan.Has(MeasureKind.CrowdInfo, Measure.AllRegionsTarget);
            bool entryLimitOnAllRegions = true;

            foreach (var region in Region.All)
            {
                int idx = region.Id - 1;
                float congestion = (float)forecast.VisitorsByRegion[idx] / region.Capacity;
                bool hasEntryLimit = plan.Has(MeasureKind.EntryLimit, region.Id);
                if (!hasEntryLimit)
                {
                    entryLimitOnAllRegions = false;
                }

                // 手順4：配分予測で混雑率が1.0を超える地域に、逆効果になり得る施策があれば警告。
                if (congestion > 1.0f)
                {
                    bool worsensCongestion =
                        plan.Has(MeasureKind.Coupon, region.Id) ||
                        plan.Has(MeasureKind.Promotion, region.Id) ||
                        plan.Has(MeasureKind.ShuttleBus, region.Id) ||
                        plan.Has(MeasureKind.EventHosting, region.Id);
                    if (worsensCongestion)
                    {
                        report.Warnings.Add($"地域{region.Id}は混雑が見込まれるため、その地域への施策は混雑を強める可能性があります。");
                    }
                }

                // 手順5：混雑率が1.2を超える地域があり、混雑情報の掲示も入場制限もなければ助言。
                if (congestion > 1.2f && !crowdInfoPosted && !hasEntryLimit)
                {
                    report.Advices.Add($"地域{region.Id}は過密が見込まれます。混雑情報の掲示や入場制限の検討をおすすめします。");
                }
            }

            // 手順6：入場制限が全地域に掛かっており、来訪数が総収容人数を超える見込みなら助言（断念者が出る）。
            if (entryLimitOnAllRegions && forecast.GiveUps > 0)
            {
                report.Advices.Add("全地域に入場制限を掛けても、断念者が発生する見込みです。");
            }

            return report;
        }
    }
}
