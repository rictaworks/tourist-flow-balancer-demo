using System;
using TouristFlowBalancer.Core;
using TouristFlowBalancer.Infra;

namespace TouristFlowBalancer.Logic
{
    /// <summary>
    /// シーズン初期化（関数A initSeason、requirements.md 5.1節・10章）。
    ///
    /// 配置についての注記：Issue #4の編集範囲は本クラスを"Assets/Scripts/Core/SeasonFactory.cs"に
    /// 置くことを想定しているが、実装するとCore.asmdefとInfra.asmdefが相互参照（循環参照）になり
    /// Unityがビルドを拒否する（Infra.asmdefは既にCoreを参照しており、Core.asmdefが逆にInfraを
    /// 参照すると循環する。Core.asmdefは references:[] のまま純粋なデータモデル専用に保つ設計が
    /// requirements.md 14.5節の意図に合致する）。
    /// 本クラスは出来事・来訪数の生成に<see cref="SplitRng"/>（Infra層）を必要とするため、
    /// 循環を作らないLogicアセンブリに配置し、Logic.asmdefにInfraへの参照を追加した
    /// （Infra→Core、Logic→Core、Logic→Infraの単方向のみで循環なし）。
    /// </summary>
    public static class SeasonFactory
    {
        // SNSバズ・交通障害の対象は「中央観光地以外」（3.4節）＝中央観光地(id=1)を除く5地域(id=2〜6)。
        private const int NonCentralRegionCount = Region.Count - 1;

        /// <summary>
        /// シードからシーズン状態を初期化する（関数A）。
        /// 手順1〜5（乱数系列の分離・地域初期状態・予算・集計値の初期化）は<see cref="Season"/>の
        /// コンストラクタで既に行われている。ここでは手順2・3（出来事表・来訪数表の生成）と
        /// 手順6（1日目の出来事公開・SNSバズの即時反映）を行う。
        /// </summary>
        public static Season Create(int seed)
        {
            var season = new Season(seed);
            var rng = new SplitRng(seed);

            // 手順1・2：系列E・Tで14日分の出来事（種類・対象地域）を生成する。
            season.Events = MakeEvents(rng);

            // 手順3：系列Nで14日分の来訪数を生成する（3.3節の基準×揺らぎ、整数）。
            // BaseArrivalsが雨補正のためEventsを参照するため、Events生成の後に行う。
            season.Arrivals = MakeArrivals(season, rng);

            // 手順6：1日目の出来事を公開し、即時効果（SNSバズ・交通障害）を反映する。
            EventEffects.ApplyImmediateEffects(season.Events[0], season);

            return season;
        }

        private static EventDay[] MakeEvents(SplitRng rng)
        {
            var events = new EventDay[GameConstants.SeasonLengthDays];
            for (int day = 1; day <= GameConstants.SeasonLengthDays; day++)
            {
                EventKind kind = RollEventKind(rng.EventKind);
                int targetRegionId = EventDay.NoTargetRegionId;
                if (kind == EventKind.SnsBuzz || kind == EventKind.TrafficDisruption)
                {
                    targetRegionId = RollNonCentralRegionId(rng.EventTarget);
                }
                events[day - 1] = new EventDay(day, kind, targetRegionId);
            }
            return events;
        }

        /// <summary>出来事の種類を出現割合（平常55／雨20／SNSバズ15／交通障害10（%）。5.10節）から決める。</summary>
        private static EventKind RollEventKind(Rng rng)
        {
            int roll = rng.NextPercent(); // [0,100)

            int rainThreshold = GameConstants.EventChanceNormal + GameConstants.EventChanceRain;
            int snsThreshold = rainThreshold + GameConstants.EventChanceSnsBuzz;

            if (roll < GameConstants.EventChanceNormal)
            {
                return EventKind.Normal;
            }
            if (roll < rainThreshold)
            {
                return EventKind.Rain;
            }
            if (roll < snsThreshold)
            {
                return EventKind.SnsBuzz;
            }
            return EventKind.TrafficDisruption;
        }

        private static int RollNonCentralRegionId(Rng rng)
        {
            int offset = rng.NextInt(0, NonCentralRegionCount);
            return offset + 2; // 中央観光地(id=1)を除くため、id=2から始まる。
        }

        /// <summary>14日分の来訪数を生成する（3.3節：基準×揺らぎ±10%、整数）。</summary>
        private static int[] MakeArrivals(Season season, SplitRng rng)
        {
            var arrivals = new int[GameConstants.SeasonLengthDays];
            for (int day = 1; day <= GameConstants.SeasonLengthDays; day++)
            {
                int baseArrivals = season.BaseArrivals(day); // 週末1.5倍・雨0.85倍込み（Events生成済みのため）
                double fluctuation = rng.Arrivals.NextFluctuation(GameConstants.ArrivalsFluctuation);
                int actual = (int)Math.Round(baseArrivals * fluctuation, MidpointRounding.AwayFromZero);
                arrivals[day - 1] = actual < 0 ? 0 : actual;
            }
            return arrivals;
        }
    }
}
