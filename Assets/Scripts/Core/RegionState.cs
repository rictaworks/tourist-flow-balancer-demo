using System;

namespace TouristFlowBalancer.Core
{
    /// <summary>
    /// 地域の日々変化する可変状態（requirements.md 6章 REGION_STATE・10章）。
    /// マスタ値（<see cref="Region"/>）とは別に、認知度・住民感情・反発状態・当日限りの効果を持つ。
    /// PlayerPrefsへJSONとして保存されるため、JsonUtility互換の public フィールドで構成する。
    /// </summary>
    [Serializable]
    public sealed class RegionState
    {
        public int RegionId;
        public int Awareness;      // 認知度（5〜100）
        public int Sentiment;      // 住民感情（−100〜100）
        public bool Backlash;      // 反発状態
        public int AccessPenalty;  // 当日のアクセス加算（交通障害。欠航でない限り+2など）
        public bool Closed;        // 当日の閉鎖（交通障害＝離島の欠航）

        public RegionState(int regionId, int initialAwareness)
        {
            RegionId = regionId;
            Awareness = initialAwareness;
            Sentiment = 0; // 初期の住民感情はすべて0（3.1節）
            Backlash = false;
            AccessPenalty = 0;
            Closed = false;
        }

        /// <summary>
        /// 実効アクセス段数＝基礎段数＋交通障害の加算−臨時バスの短縮、下限1（4.1節）。
        /// </summary>
        public int EffectiveAccess(Region region, Plan plan)
        {
            int steps = region.AccessSteps + AccessPenalty;
            if (plan != null && plan.Has(MeasureKind.ShuttleBus, RegionId))
            {
                steps -= 1;
            }
            return steps < 1 ? 1 : steps;
        }
    }
}
