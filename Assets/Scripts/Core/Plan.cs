using System;
using System.Collections.Generic;

namespace TouristFlowBalancer.Core
{
    /// <summary>
    /// ある日に打つ施策と対象地域の集合（requirements.md 3.5節・6章 PLAN・10章）。
    /// 編集中のプランは保存しない（5.8節）——復元時は空のプランから始まる。
    /// </summary>
    [Serializable]
    public sealed class Plan
    {
        public int Day;
        public List<Measure> Measures;

        public Plan(int day)
        {
            Day = day;
            Measures = new List<Measure>();
        }

        public int TotalCost()
        {
            int total = 0;
            for (int i = 0; i < Measures.Count; i++)
            {
                total += Measures[i].Cost;
            }
            return total;
        }

        /// <summary>指定した種類・地域の施策が既にプランに含まれているか（同一施策の重複判定に使う）。</summary>
        public bool Has(MeasureKind kind, int regionId)
        {
            for (int i = 0; i < Measures.Count; i++)
            {
                var measure = Measures[i];
                if (measure.Kind == kind && measure.RegionId == regionId)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
