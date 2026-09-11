using System.Collections.Generic;

namespace TouristFlowBalancer.Core
{
    /// <summary>
    /// 観光客セグメントマスタ（requirements.md 3.2節・10章）。4セグメントは不変のコード内定数であり、
    /// 保存しない（6章）。数値はすべて <see cref="Definitions"/> の1箇所に構造化してまとめる
    /// （数値のハードコード禁止）。
    /// </summary>
    public sealed class Segment
    {
        public const int Count = 4;

        public string Name { get; }
        public float Share { get; }
        public IReadOnlyList<Tag> PreferredTags { get; }
        public float DistanceAversion { get; } // 遠さの嫌がり度 k
        public PriceSensitivity Price { get; }
        public bool CrowdAverse { get; }

        private Segment(
            string name,
            float share,
            Tag[] preferredTags,
            float distanceAversion,
            PriceSensitivity price,
            bool crowdAverse)
        {
            Name = name;
            Share = share;
            PreferredTags = preferredTags;
            DistanceAversion = distanceAversion;
            Price = price;
            CrowdAverse = crowdAverse;
        }

        // requirements.md 3.2節の表を1箇所に構造化して定義する。
        // | セグメント | 構成比 | 好むタグ | 遠さの嫌がり度k | 価格感度 | 混雑回避 |
        private static readonly Segment[] Definitions =
        {
            new Segment(
                name: "家族", share: 0.30f,
                preferredTags: new[] { Tag.Experience, Tag.Nature },
                distanceAversion: 0.25f, price: PriceSensitivity.High, crowdAverse: true),
            new Segment(
                name: "若年", share: 0.30f,
                preferredTags: new[] { Tag.Gourmet, Tag.Scenery },
                distanceAversion: 0.10f, price: PriceSensitivity.High, crowdAverse: false),
            new Segment(
                name: "シニア", share: 0.20f,
                preferredTags: new[] { Tag.History, Tag.Healing },
                distanceAversion: 0.40f, price: PriceSensitivity.Low, crowdAverse: true),
            new Segment(
                name: "訪日", share: 0.20f,
                preferredTags: new[] { Tag.History, Tag.Scenery },
                distanceAversion: 0.15f, price: PriceSensitivity.Low, crowdAverse: false),
        };

        /// <summary>全4セグメント（3.2節の表の順）。</summary>
        public static IReadOnlyList<Segment> All => Definitions;

        /// <summary>セグメントの好むタグと地域の特色タグの一致数（0・1・2）。4.1節「タグ一致」で使う。</summary>
        public int CountMatchingTags(Region region)
        {
            int count = 0;
            for (int i = 0; i < PreferredTags.Count; i++)
            {
                if (region.HasTag(PreferredTags[i]))
                {
                    count++;
                }
            }
            return count;
        }
    }
}
