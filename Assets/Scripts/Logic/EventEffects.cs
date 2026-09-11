using TouristFlowBalancer.Core;

namespace TouristFlowBalancer.Logic
{
    /// <summary>
    /// SNSバズ・交通障害の即時効果適用（requirements.md 3.4節・5.1節手順6・5.5節手順6）。
    ///
    /// requirements.md 5.1節手順6はSNSバズの即時反映のみ明記するが、5.5節手順6（翌日公開）は
    /// SNSバズ・交通障害の両方を同時に反映しており、1日目だけ交通障害の効果（アクセス加算／欠航）が
    /// 欠落すると、1日目の配分計算にその日の出来事が正しく反映されない不整合が生じる。
    /// そのため<see cref="SeasonFactory"/>（関数A、1日目）と<see cref="DayAdvancer"/>（関数E、2〜14日目）の
    /// 両方から本メソッドを呼び、出来事の即時効果を同一ロジックで適用する。
    /// </summary>
    internal static class EventEffects
    {
        // 離島（requirements.md 3.1節、id=6）。交通障害の対象が離島の場合のみ欠航として扱う（3.4節）。
        private const int IslandRegionId = 6;

        public static void ApplyImmediateEffects(EventDay eventDay, Season season)
        {
            if (eventDay == null)
            {
                return;
            }

            if (eventDay.Kind == EventKind.SnsBuzz)
            {
                var state = season.GetRegionState(eventDay.TargetRegionId);
                state.Awareness = ClampInt(
                    state.Awareness + GameConstants.SnsBuzzAwarenessBonus,
                    GameConstants.AwarenessMin,
                    GameConstants.AwarenessMax);
            }
            else if (eventDay.Kind == EventKind.TrafficDisruption)
            {
                var state = season.GetRegionState(eventDay.TargetRegionId);
                if (eventDay.TargetRegionId == IslandRegionId)
                {
                    // 離島は欠航：当日閉鎖（来訪0、選択肢から除外。3.4節）。
                    state.Closed = true;
                }
                else
                {
                    state.AccessPenalty += GameConstants.TrafficDisruptionAccessPenalty;
                }
            }
        }

        private static int ClampInt(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }
            if (value > max)
            {
                return max;
            }
            return value;
        }
    }
}
