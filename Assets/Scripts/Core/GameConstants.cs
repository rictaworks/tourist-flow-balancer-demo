namespace TouristFlowBalancer.Core
{
    /// <summary>
    /// requirements.md 5.10節「定数一覧（デモ版の既定値）」。
    /// 数値定数はすべてここへ集約し、他のクラスはこのクラスの定数を参照する
    /// （個別のクラスへ数値をハードコードしない）。
    /// </summary>
    public static class GameConstants
    {
        // --- 関数A：シーズン初期化 ---
        public const int SeasonLengthDays = 14;
        public static readonly int[] WeekendDays = { 6, 7, 13, 14 };

        public const int BaseArrivalsWeekday = 1200;
        public const int BaseArrivalsWeekend = 1800;
        public const float ArrivalsFluctuation = 0.10f; // ±10%

        // 出来事の出現割合（%）。合計100。
        public const int EventChanceNormal = 55;
        public const int EventChanceRain = 20;
        public const int EventChanceSnsBuzz = 15;
        public const int EventChanceTrafficDisruption = 10;

        public const int InitialBudget = 100;
        public const int DailyBudget = 100;
        public const int BudgetRolloverCap = 100;
        public const int BudgetHoldingCap = 200;

        // --- 関数C：配分計算 ---
        public const float RainArrivalsFactor = 0.85f;
        public const float RainOutdoorAttractionFactor = 0.6f;
        public const int SnsBuzzAwarenessBonus = 30;
        public const int TrafficDisruptionAccessPenalty = 2;

        public const float TagMatchBonusPerTag = 0.3f;
        public const float AwarenessCoefficientMin = 0.1f;
        public const float EventAttractionFactor = 1.4f;
        public const float BacklashAttractionFactor = 0.8f;
        public const float CouponPriceFactorHigh = 1.3f;
        public const float CouponPriceFactorLow = 1.1f;
        public const float PromotionAwarenessBonus = 15f;

        // 混雑回避係数：1-(c-0.9) を 0.4〜1.0 に収める
        public const float CrowdAversionBase = 0.9f;
        public const float CrowdAversionMin = 0.4f;
        public const float CrowdAversionMax = 1.0f;

        // --- 関数D：日次評価 ---
        public const float CongestionCautionThreshold = 0.9f;   // これを超えると満足度が下がり始める
        public const float CongestionOvercrowdedThreshold = 1.2f; // これを超えると過密
        public const int CongestionPenaltyCap = 60;
        public const float DesertedThreshold = 0.3f; // これ未満は閑散

        public const int EventSatisfactionBonus = 5;
        public const int BacklashSatisfactionPenalty = 15;
        public const int DesertedSatisfactionPenalty = 5;

        public const float RevenueCoefficientBase = 0.5f;
        public const float RevenueCoefficientSatisfactionDivisor = 200f;

        public const int SentimentDeltaOvercrowded = -15;  // c > 1.2
        public const int SentimentDeltaCongested = -6;      // 1.0 < c <= 1.2
        public const int SentimentDeltaHealthy = 3;          // 0.3 <= c <= 1.0
        public const int SentimentDeltaDeserted = -2;        // c < 0.3

        public const int SentimentMin = -100;
        public const int SentimentMax = 100;
        public const int BacklashEnterThreshold = -50;
        public const int BacklashExitThreshold = -20;

        public const int WordOfMouthVisitorsPerStep = 100;
        public const int WordOfMouthAwarenessGain = 1;  // 満足度60以上
        public const int WordOfMouthAwarenessLoss = -1; // 満足度40未満
        public const int WordOfMouthSatisfactionHighThreshold = 60;
        public const int WordOfMouthSatisfactionLowThreshold = 40;

        // --- 関数E：日次更新 ---
        public const int AwarenessDecayPerDay = 3;
        public const int AwarenessMin = 5;
        public const int AwarenessMax = 100;

        // --- 進行 ---
        public const float InflowAnimationSeconds = 8f;
    }
}
