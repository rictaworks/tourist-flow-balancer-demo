using System;
using System.Collections.Generic;

namespace TouristFlowBalancer.Core
{
    /// <summary>
    /// 地域マスタ（requirements.md 3.1節・10章）。6地域は不変のコード内定数であり、保存しない（6章）。
    /// 数値はすべて <see cref="Definitions"/> の1箇所に構造化してまとめ、他コードはこのクラスの
    /// static メンバー経由でのみ参照する（数値のハードコード禁止）。
    /// </summary>
    public sealed class Region
    {
        public const int Count = 6;

        public int Id { get; }
        public string Name { get; }
        public int Capacity { get; }
        public int BaseAttraction { get; }
        public int BaseAwareness { get; }
        public int AccessSteps { get; }
        public IReadOnlyList<Tag> Tags { get; }
        public bool Outdoor { get; }
        public int SpendPerVisitor { get; }

        private Region(
            int id,
            string name,
            int capacity,
            int baseAttraction,
            int baseAwareness,
            int accessSteps,
            Tag[] tags,
            bool outdoor,
            int spendPerVisitor)
        {
            Id = id;
            Name = name;
            Capacity = capacity;
            BaseAttraction = baseAttraction;
            BaseAwareness = baseAwareness;
            AccessSteps = accessSteps;
            Tags = tags;
            Outdoor = outdoor;
            SpendPerVisitor = spendPerVisitor;
        }

        public bool HasTag(Tag tag)
        {
            for (int i = 0; i < Tags.Count; i++)
            {
                if (Tags[i] == tag)
                {
                    return true;
                }
            }
            return false;
        }

        // requirements.md 3.1節の表を1箇所に構造化して定義する。
        // | 地域 | 収容人数 | 基礎魅力 | 基礎認知度 | アクセス段数 | 特色タグ | 屋外型 | 客単価 |
        private static readonly Region[] Definitions =
        {
            new Region(
                id: 1, name: "中央観光地",
                capacity: 450, baseAttraction: 90, baseAwareness: 100, accessSteps: 1,
                tags: new[] { Tag.History, Tag.Gourmet }, outdoor: false, spendPerVisitor: 10),
            new Region(
                id: 2, name: "温泉郷",
                capacity: 300, baseAttraction: 70, baseAwareness: 45, accessSteps: 2,
                tags: new[] { Tag.Healing, Tag.Gourmet }, outdoor: false, spendPerVisitor: 14),
            new Region(
                id: 3, name: "港町",
                capacity: 350, baseAttraction: 65, baseAwareness: 40, accessSteps: 2,
                tags: new[] { Tag.Gourmet, Tag.Scenery }, outdoor: false, spendPerVisitor: 9),
            new Region(
                id: 4, name: "山里",
                capacity: 200, baseAttraction: 60, baseAwareness: 25, accessSteps: 3,
                tags: new[] { Tag.Nature, Tag.Experience }, outdoor: true, spendPerVisitor: 8),
            new Region(
                id: 5, name: "古街",
                capacity: 250, baseAttraction: 70, baseAwareness: 35, accessSteps: 2,
                tags: new[] { Tag.History, Tag.Scenery }, outdoor: false, spendPerVisitor: 9),
            new Region(
                id: 6, name: "離島",
                capacity: 150, baseAttraction: 80, baseAwareness: 20, accessSteps: 4,
                tags: new[] { Tag.Nature, Tag.Scenery }, outdoor: true, spendPerVisitor: 12),
        };

        /// <summary>全6地域（地域ID昇順）。</summary>
        public static IReadOnlyList<Region> All => Definitions;

        public static Region ById(int id)
        {
            foreach (var region in Definitions)
            {
                if (region.Id == id)
                {
                    return region;
                }
            }
            throw new ArgumentOutOfRangeException(nameof(id), id, "存在しない地域IDです。");
        }
    }
}
