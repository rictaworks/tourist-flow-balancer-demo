using System;
using System.Collections.Generic;
using TouristFlowBalancer.Core;

namespace TouristFlowBalancer.Logic
{
    /// <summary>
    /// 配分計算（関数C allocateVisitors、requirements.md 4章・5.3節・10章）。
    /// セグメント別スコア（4.1節）→シェアと人数・最大剰余法（4.2節）→二段階の混雑予測（4.3節）
    /// →入場制限のウォーターフォール（4.4節）の順に計算する。
    ///
    /// 14.1節の不変条件：配分予測（揺らぎなし基準来訪数）と確定配分（実際の来訪数）は必ず
    /// <see cref="Allocate"/> という同一の関数を呼ぶこと。予測用・確定用に別ロジックへ分岐させない。
    /// このクラス自身は乱数を持たず、同一の入力に対し常に同一の結果を返す（決定的なルールベース）。
    /// </summary>
    public sealed class Allocator
    {
        /// <summary>
        /// 来訪数をセグメント別スコアに基づいて地域へ配分する（関数C）。
        /// </summary>
        /// <param name="arrivals">
        /// 来訪数。配分予測（F3）では基準来訪数（揺らぎなし）を、確定時は実際の来訪数を渡す。
        /// どちらも本メソッドを同一に呼ぶ（14.1節）。
        /// </param>
        /// <param name="plan">今日のプラン（施策と対象地域の集合）。</param>
        /// <param name="season">シーズン状態。各地域の認知度・反発・閉鎖・アクセスの参照に使う。</param>
        /// <param name="todayEvent">今日の出来事（雨のときのみ屋外地域の魅力に影響する。3.4節・4.1節）。</param>
        public Allocation Allocate(int arrivals, Plan plan, Season season, EventDay todayEvent)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }
            if (season == null)
            {
                throw new ArgumentNullException(nameof(season));
            }

            var candidates = new List<Region>();
            foreach (var region in Region.All)
            {
                if (!season.GetRegionState(region.Id).Closed)
                {
                    candidates.Add(region);
                }
            }

            // 5.3節手順1：閉鎖中の地域を候補から外す。候補が空なら全員を断念者として返す
            // （現行の出来事定義では起きないが、境界として定義する）。
            if (candidates.Count == 0)
            {
                return new Allocation(new int[Region.Count], arrivals, null);
            }

            // 一段階目：混雑回避係数をすべて1.0として配分する（forecastCongestion=null。5.3節手順2・3）。
            float[] stage1Raw = Spread(arrivals, candidates, season, plan, todayEvent, forecastCongestion: null);
            int[] stage1Visitors = LargestRemainder(stage1Raw, arrivals);

            bool crowdInfoPosted = plan.Has(MeasureKind.CrowdInfo, Measure.AllRegionsTarget);

            int[] finalVisitors;
            float[] returnedForecast;

            if (crowdInfoPosted)
            {
                // 混雑予測＝一段階目の混雑率（4.3節）。前日実績は使わない。
                var forecastCongestion = new float[Region.Count];
                foreach (var region in Region.All)
                {
                    int idx = region.Id - 1;
                    forecastCongestion[idx] = (float)stage1Visitors[idx] / region.Capacity;
                }

                // 二段階目：混雑予測から混雑回避係数を求め、配分をやり直す（5.3節手順4）。これを最終配分とする。
                float[] stage2Raw = Spread(arrivals, candidates, season, plan, todayEvent, forecastCongestion);
                finalVisitors = LargestRemainder(stage2Raw, arrivals);
                returnedForecast = forecastCongestion;
            }
            else
            {
                // 掲示がなければ一段階目の結果を配分とする（5.3節手順4）。
                finalVisitors = stage1Visitors;
                returnedForecast = null;
            }

            // 入場制限のウォーターフォール（4.4節・5.3節手順6）。
            var waterfallResult = Waterfall(finalVisitors, plan, season);

            return new Allocation(waterfallResult.Visitors, waterfallResult.GiveUps, returnedForecast);
        }

        /// <summary>
        /// セグメント別スコア・シェア（4.1・4.2節）から、来訪数を地域別の生の（未整数化の）人数に広げる。
        /// 戻り値は長さ<see cref="Region.Count"/>で、候補外（閉鎖中）の地域は常に0。
        /// </summary>
        private static float[] Spread(
            int arrivals,
            List<Region> candidates,
            Season season,
            Plan plan,
            EventDay todayEvent,
            float[] forecastCongestion)
        {
            var rawByRegion = new float[Region.Count];

            foreach (var segment in Segment.All)
            {
                var scores = new float[candidates.Count];
                float sumScores = 0f;
                for (int i = 0; i < candidates.Count; i++)
                {
                    var region = candidates[i];
                    var state = season.GetRegionState(region.Id);
                    float score = Score(segment, region, state, plan, todayEvent, forecastCongestion);
                    scores[i] = score;
                    sumScores += score;
                }

                if (sumScores <= 0f)
                {
                    // このセグメントに選べる候補地域がない場合の防御。認知度係数の下限(0.1)等により
                    // 通常は起きないが、スコアが総じて0以下になった場合は当該セグメントの配分を0として続行する。
                    continue;
                }

                float segmentArrivals = arrivals * segment.Share;
                for (int i = 0; i < candidates.Count; i++)
                {
                    var region = candidates[i];
                    float share = scores[i] / sumScores;
                    rawByRegion[region.Id - 1] += segmentArrivals * share;
                }
            }

            return rawByRegion;
        }

        /// <summary>
        /// セグメントsが地域rを選ぶ度合い（スコア。requirements.md 4.1節）＝6係数の積。
        /// <paramref name="forecastCongestion"/>がnullのときは一段階目（混雑回避係数は常に1.0）。
        /// 非nullのときは二段階目で、混雑回避するセグメントに対して混雑予測から連続関数で係数を求める。
        /// </summary>
        private static float Score(
            Segment segment,
            Region region,
            RegionState state,
            Plan plan,
            EventDay todayEvent,
            float[] forecastCongestion)
        {
            // 魅力＝基礎魅力×出来事補正（雨×0.6は屋外型のみ）×イベント補正（開催なら1.4）×反発補正（反発なら0.8）
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

            // タグ一致＝1.0＋0.3×（好むタグと特色タグの一致数。0・1・2）
            int tagMatchCount = segment.CountMatchingTags(region);
            float tagFactor = 1f + GameConstants.TagMatchBonusPerTag * tagMatchCount;

            // 認知度係数＝認知度÷100（下限0.1）。プロモーションの認知度加算（+15）は「当日の配分から反映」
            // するため（3.5節L3・5.10節）、RegionStateの永続値は書き換えずスコア計算だけに一時的に加算する。
            // 実際の永続反映（残存・日々の減衰）は日次評価・日次更新（関数D・E、別issue）の責務とする。
            float effectiveAwareness = state.Awareness;
            if (plan.Has(MeasureKind.Promotion, region.Id))
            {
                effectiveAwareness += GameConstants.PromotionAwarenessBonus;
            }
            float awarenessCoef = effectiveAwareness / 100f;
            if (awarenessCoef < GameConstants.AwarenessCoefficientMin)
            {
                awarenessCoef = GameConstants.AwarenessCoefficientMin;
            }

            // アクセス係数＝1÷(1＋k×(実効アクセス段数−1))。実効段数はRegionStateが交通障害・臨時バスを込みで算出する。
            int effectiveAccess = state.EffectiveAccess(region, plan);
            float accessCoef = 1f / (1f + segment.DistanceAversion * (effectiveAccess - 1));

            // 価格係数：クーポンあり→価格感度高1.3／低1.1、なし→1.0
            float priceFactor = 1f;
            if (plan.Has(MeasureKind.Coupon, region.Id))
            {
                priceFactor = segment.Price == PriceSensitivity.High
                    ? GameConstants.CouponPriceFactorHigh
                    : GameConstants.CouponPriceFactorLow;
            }

            // 混雑回避係数：掲示があり、かつ混雑回避するセグメントのときのみ、混雑予測cから連続関数で求める。
            float crowdFactor = 1f;
            if (forecastCongestion != null && segment.CrowdAverse)
            {
                crowdFactor = CrowdAversionFactor(forecastCongestion[region.Id - 1]);
            }

            return attraction * tagFactor * awarenessCoef * accessCoef * priceFactor * crowdFactor;
        }

        /// <summary>
        /// 混雑回避係数（requirements.md 4.1節・5.10節）＝1−（混雑予測c−0.9）を0.4〜1.0に収めた値。
        /// clampのみで構成される連続関数であり、閾値（0.9）の前後で不連続に変化しない（14.1節）。
        /// public static とし、連続性をユニットテストから直接検証できるようにする。
        /// </summary>
        public static float CrowdAversionFactor(float forecastCongestionForRegion)
        {
            float raw = 1f - (forecastCongestionForRegion - GameConstants.CrowdAversionBase);
            return Clamp(raw, GameConstants.CrowdAversionMin, GameConstants.CrowdAversionMax);
        }

        /// <summary>
        /// 最大剰余法（4.2節）：生の（未整数化の）値の配列を、合計が<paramref name="total"/>に一致するよう
        /// 整数化する。各値を切り下げたのち、余りの大きい順（同率は添字の小さい順）に1ずつ配る。
        /// </summary>
        private static int[] LargestRemainder(float[] raw, int total)
        {
            int n = raw.Length;
            var result = new int[n];
            var remainder = new float[n];
            int assigned = 0;

            for (int i = 0; i < n; i++)
            {
                float value = raw[i] < 0f ? 0f : raw[i];
                int floorValue = (int)Math.Floor(value);
                result[i] = floorValue;
                remainder[i] = value - floorValue;
                assigned += floorValue;
            }

            int remaining = total - assigned;
            if (remaining < 0)
            {
                remaining = 0;
            }
            if (remaining > n)
            {
                // 浮動小数点誤差に対する安全策。raw の合計が total に十分近ければここには達しない。
                remaining = n;
            }

            var order = new int[n];
            for (int i = 0; i < n; i++)
            {
                order[i] = i;
            }
            Array.Sort(order, (a, b) =>
            {
                int cmp = remainder[b].CompareTo(remainder[a]);
                return cmp != 0 ? cmp : a.CompareTo(b);
            });

            for (int i = 0; i < remaining; i++)
            {
                result[order[i]] += 1;
            }

            return result;
        }

        private readonly struct WaterfallResult
        {
            public readonly int[] Visitors;
            public readonly int GiveUps;

            public WaterfallResult(int[] visitors, int giveUps)
            {
                Visitors = visitors;
                GiveUps = giveUps;
            }
        }

        /// <summary>
        /// 入場制限のウォーターフォール（4.4節）。満員地域を候補から外しながら、あふれを比例配分する。
        /// 繰り返しは地域数（<see cref="Region.Count"/>）を上限とする（14.1節）。
        /// 振り分け先が1つもなくなった時点で、残ったあふれを断念者として計上する。
        /// </summary>
        private static WaterfallResult Waterfall(int[] visitors, Plan plan, Season season)
        {
            var result = (int[])visitors.Clone();

            var closed = new bool[Region.Count];
            var entryLimited = new bool[Region.Count];
            bool anyEntryLimit = false;

            foreach (var region in Region.All)
            {
                int idx = region.Id - 1;
                closed[idx] = season.GetRegionState(region.Id).Closed;
                entryLimited[idx] = plan.Has(MeasureKind.EntryLimit, region.Id);
                anyEntryLimit |= entryLimited[idx];
            }

            if (!anyEntryLimit)
            {
                // 入場制限のない地域は収容人数を超えて受け入れる（過密を許容する。4.4節末尾）。
                return new WaterfallResult(result, 0);
            }

            var full = new bool[Region.Count];
            int giveUps = 0;

            for (int iteration = 0; iteration < Region.Count; iteration++)
            {
                // 手順1：入場制限が掛かった地域のうち収容人数超過分をあふれとして集め、満員の印を付ける。
                int overflow = 0;
                foreach (var region in Region.All)
                {
                    int idx = region.Id - 1;
                    if (closed[idx] || full[idx] || !entryLimited[idx])
                    {
                        continue;
                    }
                    if (result[idx] > region.Capacity)
                    {
                        overflow += result[idx] - region.Capacity;
                        result[idx] = region.Capacity;
                        full[idx] = true;
                    }
                }

                if (overflow == 0)
                {
                    break;
                }

                // 手順2：満員でも閉鎖でもない地域へ、その時点の人数に比例して上乗せする。
                var eligible = new List<int>();
                foreach (var region in Region.All)
                {
                    int idx = region.Id - 1;
                    if (!closed[idx] && !full[idx])
                    {
                        eligible.Add(idx);
                    }
                }

                // 手順4：振り分け先が1つもない場合、残ったあふれを断念者として計上する。
                if (eligible.Count == 0)
                {
                    giveUps += overflow;
                    break;
                }

                var weights = new float[eligible.Count];
                float sumWeights = 0f;
                for (int i = 0; i < eligible.Count; i++)
                {
                    weights[i] = result[eligible[i]];
                    sumWeights += weights[i];
                }
                if (sumWeights <= 0f)
                {
                    // 候補全員が0人（極端な入力）の場合の保険：等分して配る。
                    for (int i = 0; i < weights.Length; i++)
                    {
                        weights[i] = 1f;
                    }
                    sumWeights = weights.Length;
                }

                // weightsは「その時点の人数」であり、そのままでは合計がoverflowと一致しない。
                // LargestRemainderは「合計がtotalに一致するよう整数化する」関数なので、
                // 先にoverflowに比例した生の値へスケールしてから渡す（Spreadと同じ考え方）。
                var scaledOverflow = new float[eligible.Count];
                for (int i = 0; i < eligible.Count; i++)
                {
                    scaledOverflow[i] = weights[i] / sumWeights * overflow;
                }

                var additions = LargestRemainder(scaledOverflow, overflow);
                for (int i = 0; i < eligible.Count; i++)
                {
                    result[eligible[i]] += additions[i];
                }

                // 手順3：上乗せの結果、別の入場制限地域が収容人数を超えたら手順1に戻る（ループ先頭で再判定）。
            }

            return new WaterfallResult(result, giveUps);
        }

        private static float Clamp(float value, float min, float max)
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
