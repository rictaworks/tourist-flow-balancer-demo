using System;

namespace TouristFlowBalancer.Core
{
    /// <summary>
    /// シーズン状態を束ねるルートオブジェクト（requirements.md 6章 SEASON・10章）。
    /// シード・日・予算・状態・地域別可変状態・出来事表・来訪数表・集計値を保持し、
    /// PlayerPrefsへ1つのJSON文字列として保存される（5.8節）。
    ///
    /// このクラスは基盤のデータモデルのみを提供する。出来事表・来訪数表の実際の生成
    /// （関数A initSeason の手順2・3。<see cref="TouristFlowBalancer.Infra.SplitRng"/> の3系列から
    /// 出来事の種類・対象地域・来訪数を確率分布に従って決める処理）は、配分ロジック層と合わせて
    /// 別issueで実装する。ここでは初期状態（日1・地域は基礎認知度・空の出来事表と来訪数表）のみを組む。
    /// </summary>
    [Serializable]
    public sealed class Season
    {
        public int Seed;
        public int Day;
        public int Budget;
        public SeasonState State;
        public RegionState[] Regions;
        public EventDay[] Events;
        public int[] Arrivals;
        public Stats Stats;

        public Season(int seed)
        {
            Seed = seed;
            Day = 1;
            Budget = GameConstants.InitialBudget;
            State = SeasonState.Planning;

            var regionMasters = Region.All;
            Regions = new RegionState[regionMasters.Count];
            for (int i = 0; i < regionMasters.Count; i++)
            {
                var region = regionMasters[i];
                Regions[i] = new RegionState(region.Id, region.BaseAwareness);
            }

            Events = new EventDay[GameConstants.SeasonLengthDays];
            Arrivals = new int[GameConstants.SeasonLengthDays];
            Stats = new Stats();
        }

        /// <summary>指定した日が週末（6・7・13・14日目）か（3.3節・5.10節）。</summary>
        public bool IsWeekend(int day)
        {
            var weekendDays = GameConstants.WeekendDays;
            for (int i = 0; i < weekendDays.Length; i++)
            {
                if (weekendDays[i] == day)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>指定した日の基準来訪数（揺らぎなし。平日1,200／週末1,800。3.3節）。</summary>
        public int BaseArrivals(int day)
        {
            return IsWeekend(day) ? GameConstants.BaseArrivalsWeekend : GameConstants.BaseArrivalsWeekday;
        }

        public RegionState GetRegionState(int regionId)
        {
            for (int i = 0; i < Regions.Length; i++)
            {
                if (Regions[i].RegionId == regionId)
                {
                    return Regions[i];
                }
            }
            throw new ArgumentOutOfRangeException(nameof(regionId), regionId, "存在しない地域IDです。");
        }
    }
}
