using System;

namespace TouristFlowBalancer.Core
{
    /// <summary>
    /// ある日に発生する出来事（requirements.md 3.4節・6章 EVENT_DAY・10章）。
    /// シードから系列E（種類）・系列T（対象地域）で決まる（5.1節手順1・2）。
    /// </summary>
    [Serializable]
    public sealed class EventDay
    {
        /// <summary>対象地域を持たない出来事（平常・雨）でのターゲットID。</summary>
        public const int NoTargetRegionId = 0;

        public int Day;
        public EventKind Kind;
        public int TargetRegionId; // SNSバズ・交通障害のときのみ有効。中央観光地(id=1)は対象にならない（3.4節）

        public EventDay(int day, EventKind kind, int targetRegionId)
        {
            Day = day;
            Kind = kind;
            TargetRegionId = targetRegionId;
        }
    }
}
