using System;

namespace TouristFlowBalancer.Core
{
    /// <summary>
    /// 配分計算（関数C allocateVisitors、requirements.md 5.3節）の結果（10章 ALLOCATION／DAY_RESULTの一部）。
    /// <see cref="TouristFlowBalancer.Logic.Allocator"/>（別issue Issue #3で実装）の戻り値として使う。
    /// </summary>
    [Serializable]
    public sealed class Allocation
    {
        /// <summary>地域別の来訪者数。長さ<see cref="Region.Count"/>、インデックスは地域ID-1。</summary>
        public int[] VisitorsByRegion;

        /// <summary>断念者数（入場制限のウォーターフォールであふれ、行き先がなかった人数。4.4節）。</summary>
        public int GiveUps;

        /// <summary>
        /// 混雑予測（4.3節）。混雑情報の掲示がある日のみ、一段階目（混雑回避係数1.0）の混雑率を持つ。
        /// 掲示がない日はnull。長さ<see cref="Region.Count"/>、インデックスは地域ID-1。
        /// </summary>
        public float[] ForecastCongestion;

        public Allocation(int[] visitorsByRegion, int giveUps, float[] forecastCongestion)
        {
            VisitorsByRegion = visitorsByRegion;
            GiveUps = giveUps;
            ForecastCongestion = forecastCongestion;
        }
    }
}
