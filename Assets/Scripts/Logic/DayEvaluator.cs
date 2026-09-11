using System;
using TouristFlowBalancer.Core;

namespace TouristFlowBalancer.Logic
{
    /// <summary>地域別の日次評価結果（requirements.md 5.4節・10章 RegionResult）。</summary>
    public sealed class RegionResult
    {
        public int RegionId;
        public int Visitors;
        public float Congestion;
        public int Satisfaction;
        public int Revenue;
        public int SentimentDelta;

        public RegionResult(int regionId, int visitors, float congestion, int satisfaction, int revenue, int sentimentDelta)
        {
            RegionId = regionId;
            Visitors = visitors;
            Congestion = congestion;
            Satisfaction = satisfaction;
            Revenue = revenue;
            SentimentDelta = sentimentDelta;
        }
    }

    /// <summary>日次レポート（requirements.md 5.4節手順10・10章 DayResult）。</summary>
    public sealed class DayResult
    {
        public int Day;
        public RegionResult[] Regions;
        public int GiveUps;
        public int SatisfiedTotal;

        public DayResult(int day, RegionResult[] regions, int giveUps, int satisfiedTotal)
        {
            Day = day;
            Regions = regions;
            GiveUps = giveUps;
            SatisfiedTotal = satisfiedTotal;
        }
    }

    /// <summary>
    /// 日次評価（関数D evaluateDay、requirements.md 5.4節）。
    /// 地域を番号順（<see cref="Region.All"/>の順）に処理し、混雑率・満足度・収益を算出しつつ、
    /// 住民感情・反発状態・認知度（プロモーションの永続反映・口コミ）をシーズン状態へ反映する。
    /// 予算の消費（施策費用の差し引き）もここで行う——確定操作時点ではまだBudgetから差し引かれて
    /// おらず、関数E（<see cref="DayAdvancer"/>）の予算繰越（5.5節手順3）は「使い残し」を前提とするため、
    /// その日のプランを消費するこの関数で差し引く（requirements.mdは差し引きの主体を明記していないが、
    /// Planを受け取り集計値へ反映する本関数が最も自然な実施箇所であるため、ここに実装する）。
    /// </summary>
    public sealed class DayEvaluator
    {
        public DayResult Evaluate(Allocation allocation, Plan plan, Season season)
        {
            if (allocation == null)
            {
                throw new ArgumentNullException(nameof(allocation));
            }
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }
            if (season == null)
            {
                throw new ArgumentNullException(nameof(season));
            }

            EventDay todayEvent = season.Events[season.Day - 1];
            var regionResults = new RegionResult[Region.Count];
            double satisfiedTotalRaw = 0.0;

            foreach (var region in Region.All)
            {
                int idx = region.Id - 1;
                var state = season.GetRegionState(region.Id);
                int visitors = allocation.VisitorsByRegion[idx];

                if (state.Closed)
                {
                    // 閉鎖中は満足度・収益を算出せず、住民感情・口コミも変化させない（5.4節手順1・11.2節）。
                    regionResults[idx] = new RegionResult(region.Id, visitors, 0f, 0, 0, 0);
                    continue;
                }

                float congestion = (float)visitors / region.Capacity;
                float penalty = CongestionPenalty(congestion);
                float effectiveAttraction = EffectiveAttraction(region, state, plan, todayEvent);

                float rawSatisfaction = 60f + (effectiveAttraction - 60f) / 2f
                    + (plan.Has(MeasureKind.EventHosting, region.Id) ? GameConstants.EventSatisfactionBonus : 0)
                    - penalty
                    - (state.Backlash ? GameConstants.BacklashSatisfactionPenalty : 0)
                    - (congestion < GameConstants.DesertedThreshold ? GameConstants.DesertedSatisfactionPenalty : 0);
                int satisfaction = ClampInt(
                    (int)Math.Round(rawSatisfaction, MidpointRounding.AwayFromZero), 0, 100);

                float revenueFactor = GameConstants.RevenueCoefficientBase
                    + satisfaction / GameConstants.RevenueCoefficientSatisfactionDivisor;
                int revenue = (int)Math.Round(visitors * region.SpendPerVisitor * revenueFactor, MidpointRounding.AwayFromZero);

                int sentimentDelta = SentimentDelta(congestion);
                state.Sentiment = ClampInt(state.Sentiment + sentimentDelta, GameConstants.SentimentMin, GameConstants.SentimentMax);

                // 反発のヒステリシス：−50以下で入り、−20以上に戻るまで解除されない（5.4節手順6）。
                if (state.Sentiment <= GameConstants.BacklashEnterThreshold)
                {
                    state.Backlash = true;
                }
                else if (state.Backlash && state.Sentiment >= GameConstants.BacklashExitThreshold)
                {
                    state.Backlash = false;
                }

                // プロモーションの永続効果：認知度として残り、日々減衰する（3.5節L3）。
                // Allocator（関数C）は当日の配分計算のみでこのボーナスを一時的に加味しており、
                // RegionState自体は書き換えない。恒常反映はここ（関数D）で行う。
                if (plan.Has(MeasureKind.Promotion, region.Id))
                {
                    state.Awareness += (int)GameConstants.PromotionAwarenessBonus;
                }

                // 口コミによる認知度変化：来訪者数100人ごとに、満足度60以上なら+1、40未満なら-1（5.4節手順7）。
                int steps = visitors / GameConstants.WordOfMouthVisitorsPerStep;
                if (steps > 0)
                {
                    if (satisfaction >= GameConstants.WordOfMouthSatisfactionHighThreshold)
                    {
                        state.Awareness += steps * GameConstants.WordOfMouthAwarenessGain;
                    }
                    else if (satisfaction < GameConstants.WordOfMouthSatisfactionLowThreshold)
                    {
                        state.Awareness += steps * GameConstants.WordOfMouthAwarenessLoss;
                    }
                }

                regionResults[idx] = new RegionResult(region.Id, visitors, congestion, satisfaction, revenue, sentimentDelta);
                satisfiedTotalRaw += visitors * satisfaction / 100.0;

                if (congestion > GameConstants.CongestionOvercrowdedThreshold)
                {
                    season.Stats.OvercrowdedDays[idx]++;
                }
                else if (congestion < GameConstants.DesertedThreshold)
                {
                    season.Stats.DesertedDays[idx]++;
                }

                season.Stats.VisitorsByRegion[idx] += visitors;
                season.Stats.RevenueByRegion[idx] += revenue;
            }

            season.Stats.GiveUpTotal += allocation.GiveUps;

            foreach (var measure in plan.Measures)
            {
                season.Stats.UsesByMeasure[(int)measure.Kind]++;
            }

            int satisfiedTotal = (int)Math.Round(satisfiedTotalRaw, MidpointRounding.AwayFromZero);
            season.Stats.SatisfiedTotal += satisfiedTotal;

            // その日のプラン費用を予算から差し引く（クラス冒頭のコメント参照）。
            season.Budget -= plan.TotalCost();

            return new DayResult(season.Day, regionResults, allocation.GiveUps, satisfiedTotal);
        }

        /// <summary>
        /// 実効魅力（requirements.md 4.1節「魅力」）＝基礎魅力×出来事補正（雨は屋外型のみ×0.6）×
        /// イベント補正（開催なら×1.4）×反発補正（反発状態なら×0.8）。
        /// Allocator.Score()の魅力計算と同じ式（4.1節）を用いる。Allocator.csは本Issueの参照専用範囲
        /// のため、共通化のための公開メンバー追加を行わず、ここに同一の式を再実装する。
        /// </summary>
        private static float EffectiveAttraction(Region region, RegionState state, Plan plan, EventDay todayEvent)
        {
            float attraction = region.BaseAttraction;
            if (todayEvent != null && todayEvent.Kind == EventKind.Rain && region.Outdoor)
            {
                attraction *= GameConstants.RainOutdoorAttractionFactor;
            }
            if (plan.Has(MeasureKind.EventHosting, region.Id))
            {
                attraction *= GameConstants.EventAttractionFactor;
            }
            if (state.Backlash)
            {
                attraction *= GameConstants.BacklashAttractionFactor;
            }
            return attraction;
        }

        /// <summary>混雑ペナルティ（requirements.md 5.4節手順2）。</summary>
        private static float CongestionPenalty(float c)
        {
            if (c <= GameConstants.CongestionCautionThreshold)
            {
                return 0f;
            }
            if (c <= GameConstants.CongestionOvercrowdedThreshold)
            {
                return (c - GameConstants.CongestionCautionThreshold) * 100f;
            }
            float penalty = 30f + (c - GameConstants.CongestionOvercrowdedThreshold) * 100f;
            return penalty > GameConstants.CongestionPenaltyCap ? GameConstants.CongestionPenaltyCap : penalty;
        }

        /// <summary>
        /// 住民感情の変化（requirements.md 5.4節手順5・5.10節）。
        /// 境界の1.0（収容人数ちょうど）は混雑ペナルティの閾値（0.9・1.2）とは別の、
        /// 住民感情専用の境界であり、GameConstantsに対応する定数を持たないためリテラルで表す。
        /// </summary>
        private static int SentimentDelta(float c)
        {
            const float fullCapacityRatio = 1.0f;

            if (c > GameConstants.CongestionOvercrowdedThreshold)
            {
                return GameConstants.SentimentDeltaOvercrowded;
            }
            if (c > fullCapacityRatio)
            {
                return GameConstants.SentimentDeltaCongested;
            }
            if (c >= GameConstants.DesertedThreshold)
            {
                return GameConstants.SentimentDeltaHealthy;
            }
            return GameConstants.SentimentDeltaDeserted;
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
