namespace TouristFlowBalancer.Core
{
    /// <summary>
    /// 地域の特色タグ（requirements.md 3.1節）。
    /// </summary>
    public enum Tag
    {
        History,    // 歴史
        Gourmet,    // グルメ
        Healing,    // 癒し
        Scenery,    // 景観
        Nature,     // 自然
        Experience, // 体験
    }

    /// <summary>
    /// 観光客セグメントの価格感度（requirements.md 3.2節）。
    /// </summary>
    public enum PriceSensitivity
    {
        High, // 高
        Low,  // 低
    }

    /// <summary>
    /// 毎日1つシードから決まる出来事の種類（requirements.md 3.4節）。
    /// </summary>
    public enum EventKind
    {
        Normal,             // 平常
        Rain,               // 雨
        SnsBuzz,            // SNSバズ
        TrafficDisruption,  // 交通障害
    }

    /// <summary>
    /// プレイヤーが予算で打つ施策の種類（requirements.md 3.5節、L1〜L6）。
    /// </summary>
    public enum MeasureKind
    {
        CrowdInfo,    // L1 混雑情報の掲示（全地域）
        Coupon,       // L2 クーポン
        Promotion,    // L3 プロモーション
        ShuttleBus,   // L4 臨時バス
        EventHosting, // L5 イベント開催
        EntryLimit,   // L6 入場制限
    }

    /// <summary>
    /// シーズンの進行状態（requirements.md 6章）。
    /// LOADING・TITLE はゲーム全体（GameFlow）の状態であり、シーズン自体の状態には含めない（11.1節）。
    /// </summary>
    public enum SeasonState
    {
        Planning,  // 計画中
        Inflow,    // 流入中
        Report,    // レポート
        Finalized, // 精算
    }
}
