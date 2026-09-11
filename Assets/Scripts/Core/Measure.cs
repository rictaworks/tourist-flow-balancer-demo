using System;
using System.Collections.Generic;

namespace TouristFlowBalancer.Core
{
    /// <summary>
    /// ある日にある地域へ打つ施策（requirements.md 3.5節・6章 MEASURE・10章）。
    /// </summary>
    [Serializable]
    public sealed class Measure
    {
        /// <summary>L1「混雑情報の掲示」など、全地域を対象とする施策で使うRegionId。</summary>
        public const int AllRegionsTarget = 0;

        public MeasureKind Kind;
        public int RegionId; // 全地域対象の施策は AllRegionsTarget
        public int Cost;

        public Measure(MeasureKind kind, int regionId, int cost)
        {
            Kind = kind;
            RegionId = regionId;
            Cost = cost;
        }

        public Measure(MeasureKind kind, int regionId)
            : this(kind, regionId, MeasureCatalog.CostOf(kind))
        {
        }
    }

    /// <summary>
    /// 施策の費用マスタ（requirements.md 3.5節・5.10節）。数値はここへ1箇所に集約する。
    /// </summary>
    public static class MeasureCatalog
    {
        private static readonly Dictionary<MeasureKind, int> Costs = new Dictionary<MeasureKind, int>
        {
            { MeasureKind.CrowdInfo, 20 },
            { MeasureKind.Coupon, 30 },
            { MeasureKind.Promotion, 40 },
            { MeasureKind.ShuttleBus, 50 },
            { MeasureKind.EventHosting, 60 },
            { MeasureKind.EntryLimit, 0 },
        };

        public static int CostOf(MeasureKind kind)
        {
            return Costs[kind];
        }

        /// <summary>この施策が全地域を対象とするか（L1のみ）。</summary>
        public static bool TargetsAllRegions(MeasureKind kind)
        {
            return kind == MeasureKind.CrowdInfo;
        }
    }
}
