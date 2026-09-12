using TouristFlowBalancer.Core;

namespace TouristFlowBalancer.UI
{
    /// <summary>UI表示用の短い日本語ラベル（requirements.md 3.4節・3.5節）。表示専用で、ロジックには影響しない。</summary>
    internal static class Labels
    {
        public static string EventName(EventKind kind)
        {
            switch (kind)
            {
                case EventKind.Normal: return "平常";
                case EventKind.Rain: return "雨";
                case EventKind.SnsBuzz: return "SNSバズ";
                case EventKind.TrafficDisruption: return "交通障害";
                default: return kind.ToString();
            }
        }

        public static string MeasureName(MeasureKind kind)
        {
            switch (kind)
            {
                case MeasureKind.CrowdInfo: return "混雑情報の掲示";
                case MeasureKind.Coupon: return "クーポン";
                case MeasureKind.Promotion: return "プロモーション";
                case MeasureKind.ShuttleBus: return "臨時バス";
                case MeasureKind.EventHosting: return "イベント開催";
                case MeasureKind.EntryLimit: return "入場制限";
                default: return kind.ToString();
            }
        }

        public static string MeasureShortName(MeasureKind kind)
        {
            switch (kind)
            {
                case MeasureKind.CrowdInfo: return "掲示";
                case MeasureKind.Coupon: return "クーポン";
                case MeasureKind.Promotion: return "プロモ";
                case MeasureKind.ShuttleBus: return "バス";
                case MeasureKind.EventHosting: return "イベント";
                case MeasureKind.EntryLimit: return "入場制限";
                default: return kind.ToString();
            }
        }
    }
}
