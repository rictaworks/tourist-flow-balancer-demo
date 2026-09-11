using System;
using TouristFlowBalancer.Core;
using TouristFlowBalancer.Infra;

namespace TouristFlowBalancer.Logic
{
    /// <summary>シーズン精算の結果（requirements.md 5.6節・10章）。</summary>
    public sealed class SeasonResult
    {
        public int Seed;
        public int Score;
        public int SatisfiedTotal;
        public int GiveUpTotal;
        public int[] VisitorsByRegion;
        public int[] RevenueByRegion;
        public int[] OvercrowdedDays;
        public int[] DesertedDays;
        public int[] FinalSentimentByRegion;
        public int[] UsesByMeasure;
        public int BestScore;
    }

    /// <summary>
    /// シーズン精算（関数F finalizeSeason、requirements.md 5.6節）。
    /// スコア＝満足延べ人数の累計−断念者の累計。地域別の累計値・住民感情の最終値・施策使用回数は
    /// 14日間、日次評価（<see cref="DayEvaluator"/>）が既に<see cref="Stats"/>へ積み上げてきたものを
    /// ここで読み出す。
    /// </summary>
    public sealed class SeasonFinalizer
    {
        /// <summary>
        /// <paramref name="persistence"/>を渡すとベストスコア・直前シードの保存と進行中データの削除
        /// （5.6節手順3〜5）を行う（省略時はテスト等でPlayerPrefsへの副作用を避けるためスキップし、
        /// ベストスコアは今回のスコアそのものとして返す）。
        /// </summary>
        public SeasonResult Finalize(Season season, Persistence persistence = null)
        {
            if (season == null)
            {
                throw new ArgumentNullException(nameof(season));
            }

            // 手順1：スコア＝満足延べ人数の累計−断念者の累計。
            int score = season.Stats.SatisfiedTotal - season.Stats.GiveUpTotal;

            // 手順2：地域別の累計・住民感情の最終値・施策別の使用回数を集計する。
            var finalSentiment = new int[Region.Count];
            foreach (var region in Region.All)
            {
                finalSentiment[region.Id - 1] = season.GetRegionState(region.Id).Sentiment;
            }

            // 手順3・4：ベストスコアより高ければ更新し、直前のシードを保存する。
            int bestScore = score;
            if (persistence != null)
            {
                persistence.SaveResult(season.Seed, score);
                bestScore = persistence.LoadBestScore();
            }

            // 手順5：ゲーム状態を「精算」とする（進行中の保存データの削除はPersistence.SaveResultが行う）。
            season.State = SeasonState.Finalized;

            var result = new SeasonResult
            {
                Seed = season.Seed,
                Score = score,
                SatisfiedTotal = season.Stats.SatisfiedTotal,
                GiveUpTotal = season.Stats.GiveUpTotal,
                VisitorsByRegion = (int[])season.Stats.VisitorsByRegion.Clone(),
                RevenueByRegion = (int[])season.Stats.RevenueByRegion.Clone(),
                OvercrowdedDays = (int[])season.Stats.OvercrowdedDays.Clone(),
                DesertedDays = (int[])season.Stats.DesertedDays.Clone(),
                FinalSentimentByRegion = finalSentiment,
                UsesByMeasure = (int[])season.Stats.UsesByMeasure.Clone(),
                BestScore = bestScore,
            };

            return result;
        }
    }
}
