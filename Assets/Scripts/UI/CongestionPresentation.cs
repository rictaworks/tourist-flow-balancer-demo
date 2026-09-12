using TouristFlowBalancer.Core;

namespace TouristFlowBalancer.UI
{
    /// <summary>
    /// requirements.md 13章・14.3節：「色のみで意味を区別しない＝混雑度は記号と数値を併記する」。
    /// 計画・流入・日次レポートの各画面が同じ基準で記号を出すよう、ここに1箇所へ集約する。
    /// 閾値は<see cref="GameConstants"/>の混雑ペナルティ・閑散の定義（5.10節）と揃える
    /// （記号専用の別の数値をハードコードしない）。
    /// </summary>
    internal static class CongestionPresentation
    {
        /// <summary>快適○・混雑△・過密×・閑散…・閉鎖休（13章）。</summary>
        public static string Symbol(bool closed, float congestion)
        {
            if (closed)
            {
                return "休";
            }
            if (congestion < GameConstants.DesertedThreshold)
            {
                return "…";
            }
            if (congestion <= GameConstants.CongestionCautionThreshold)
            {
                return "○";
            }
            if (congestion <= GameConstants.CongestionOvercrowdedThreshold)
            {
                return "△";
            }
            return "×";
        }

        /// <summary>記号に対応する短い説明（凡例・スクリーンリーダー代替のため数値と併記する）。</summary>
        public static string Label(bool closed, float congestion)
        {
            if (closed)
            {
                return "閉鎖";
            }
            if (congestion < GameConstants.DesertedThreshold)
            {
                return "閑散";
            }
            if (congestion <= GameConstants.CongestionCautionThreshold)
            {
                return "快適";
            }
            if (congestion <= GameConstants.CongestionOvercrowdedThreshold)
            {
                return "混雑";
            }
            return "過密";
        }

        public static string FormatPercent(float congestion)
        {
            return (congestion * 100f).ToString("0") + "%";
        }
    }
}
