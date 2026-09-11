using System;
using TouristFlowBalancer.Core;
using TouristFlowBalancer.Infra;

namespace TouristFlowBalancer.Logic
{
    /// <summary>
    /// 日次更新（関数E advanceDay、requirements.md 5.5節・14.1節）。
    /// 手順の順序を固定する：認知度減衰→予算繰越→当日限りの効果解除→シーズン終了判定→
    /// 翌日公開→プラン初期化→自動保存。
    ///
    /// 「プランの初期化」（手順7の前半）はSeasonがPlanへの参照を一切持たない設計
    /// （<see cref="Season"/>にPlanフィールドが無いこと自体で「編集中のプランを保存しない」を
    /// 構造的に保証している）であるため、本クラスの責務は状態を「計画中」にすることのみで、
    /// 実際に空のPlanを新規作成するのは呼び出し側（将来のGameFlow）の役割とする。
    /// </summary>
    public sealed class DayAdvancer
    {
        /// <summary>
        /// 日次更新を実行する。<paramref name="persistence"/>を渡すと手順8（自動保存）でPlayerPrefsへ
        /// 保存する（省略時はテスト等でPlayerPrefsへの副作用を避けるため保存をスキップする）。
        /// </summary>
        /// <returns>今日が14日目で、シーズンが終了した（関数Fへ進むべき）場合はtrue。</returns>
        public bool Advance(Season season, Persistence persistence = null)
        {
            if (season == null)
            {
                throw new ArgumentNullException(nameof(season));
            }

            // 手順1・2：認知度の減衰と範囲(5〜100)への収束。
            DecayAwareness(season);

            // 手順3：予算の繰越（使い残しを最大100まで残し、100を加える。所持上限200）。
            RolloverBudget(season);

            // 手順4：当日限りの効果の解除（交通障害のアクセス加算・欠航）。
            // 施策（クーポン・臨時バス・イベント・混雑情報・入場制限）はPlanの再生成（手順7）で
            // 自然に消える——RegionStateには保持していないため、ここでの追加処理は不要。
            ClearDailyEffects(season);

            // 手順5：シーズン終了判定。
            if (season.Day >= GameConstants.SeasonLengthDays)
            {
                // 9.2節：14日目は翌日公開・プラン初期化・自動保存を行わず、関数Fへ進む。
                return true;
            }

            // 手順6・7前半：翌日の公開（出来事・予報。SNSバズ/交通障害の即時反映を含む）と「計画中」への遷移。
            PublishNextDay(season);
            season.State = SeasonState.Planning;

            // 手順8：自動保存。
            persistence?.Save(season);

            return false;
        }

        /// <summary>
        /// 手順1・2：各地域の認知度を基礎認知度に向けて3だけ近づけ、5〜100に収める（5.5節・5.10節）。
        /// </summary>
        private static void DecayAwareness(Season season)
        {
            foreach (var region in Region.All)
            {
                var state = season.GetRegionState(region.Id);
                int diff = region.BaseAwareness - state.Awareness;
                if (diff > 0)
                {
                    state.Awareness += Math.Min(GameConstants.AwarenessDecayPerDay, diff);
                }
                else if (diff < 0)
                {
                    state.Awareness -= Math.Min(GameConstants.AwarenessDecayPerDay, -diff);
                }

                if (state.Awareness < GameConstants.AwarenessMin)
                {
                    state.Awareness = GameConstants.AwarenessMin;
                }
                else if (state.Awareness > GameConstants.AwarenessMax)
                {
                    state.Awareness = GameConstants.AwarenessMax;
                }
            }
        }

        /// <summary>手順3：予算の繰越（使い残し最大100まで残し+100、所持上限200。5.10節）。</summary>
        private static void RolloverBudget(Season season)
        {
            int leftover = season.Budget;
            if (leftover < 0)
            {
                leftover = 0;
            }
            if (leftover > GameConstants.BudgetRolloverCap)
            {
                leftover = GameConstants.BudgetRolloverCap;
            }

            int newBudget = leftover + GameConstants.DailyBudget;
            if (newBudget > GameConstants.BudgetHoldingCap)
            {
                newBudget = GameConstants.BudgetHoldingCap;
            }

            season.Budget = newBudget;
        }

        /// <summary>手順4：当日限りの効果（交通障害のアクセス加算・欠航）を解除する。</summary>
        private static void ClearDailyEffects(Season season)
        {
            foreach (var region in Region.All)
            {
                var state = season.GetRegionState(region.Id);
                state.AccessPenalty = 0;
                state.Closed = false;
            }
        }

        /// <summary>手順6：日を1進め、その日の出来事の即時効果（SNSバズ・交通障害）を反映する。</summary>
        private static void PublishNextDay(Season season)
        {
            season.Day += 1;
            EventEffects.ApplyImmediateEffects(season.Events[season.Day - 1], season);
        }
    }
}
