using System;

namespace TouristFlowBalancer.Core
{
    /// <summary>
    /// シーズンを通じた集計値（requirements.md 3.7節・6章 STATS・10章）。
    /// 地域別の配列は <see cref="Region.Count"/> 件、インデックスは地域ID-1に対応する。
    /// 施策別の配列は <see cref="MeasureKindCount"/> 件、インデックスは <see cref="MeasureKind"/> の値に対応する。
    /// </summary>
    [Serializable]
    public sealed class Stats
    {
        public static readonly int MeasureKindCount = Enum.GetValues(typeof(MeasureKind)).Length;

        public int SatisfiedTotal;     // 満足延べ人数の累計
        public int GiveUpTotal;        // 断念者の累計
        public int[] VisitorsByRegion; // 地域別の累計来訪者
        public int[] RevenueByRegion;  // 地域別の累計収益
        public int[] OvercrowdedDays;  // 地域別の過密日数（混雑率1.2超）
        public int[] DesertedDays;     // 地域別の閑散日数（混雑率0.3未満）
        public int[] UsesByMeasure;    // 施策別の使用回数

        public Stats()
        {
            SatisfiedTotal = 0;
            GiveUpTotal = 0;
            VisitorsByRegion = new int[Region.Count];
            RevenueByRegion = new int[Region.Count];
            OvercrowdedDays = new int[Region.Count];
            DesertedDays = new int[Region.Count];
            UsesByMeasure = new int[MeasureKindCount];
        }
    }
}
