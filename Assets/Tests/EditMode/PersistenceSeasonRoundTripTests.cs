using System;
using System.Reflection;
using NUnit.Framework;
using TouristFlowBalancer.Core;
using TouristFlowBalancer.Infra;
using UnityEngine;

namespace TouristFlowBalancer.Tests.EditMode
{
    /// <summary>
    /// requirements.md 5.8節・6章：PersistenceTests.cs（PR #8）が確認していない2点を補う。
    ///
    /// 1. Save/Load の往復で Stats の集計値（配列フィールドを含む）と複数地域の状態が
    ///    JsonUtility経由でも欠落しないこと（PersistenceTests.cs の SaveThenLoad_RestoresSeasonContent
    ///    は Stats.SatisfiedTotal と地域1件しか検証していない。JsonUtilityは配列や複数フィールドの
    ///    取りこぼしが起きやすいため、6章の「集計値」全体を往復させて確認する）。
    /// 2. Season型が Plan（編集中のプラン）を一切保持しないこと（受け入れ条件「編集中のプランは
    ///    保存しない」を、将来 Season に Plan 参照が追加されても検知できるよう反射で固定する）。
    ///
    /// PlayerPrefsの扱い・オーナーIDのクリーンアップ方針はPersistenceTests.csに準じる。
    /// </summary>
    public class PersistenceSeasonRoundTripTests
    {
        private string _ownerIdUsedInTest;

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey("owner_id");
            _ownerIdUsedInTest = null;
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey("owner_id");
            if (!string.IsNullOrEmpty(_ownerIdUsedInTest))
            {
                PlayerPrefs.DeleteKey(_ownerIdUsedInTest + ":last_reset_at");
                PlayerPrefs.DeleteKey(_ownerIdUsedInTest + ":season");
                PlayerPrefs.DeleteKey(_ownerIdUsedInTest + ":last_seed");
                PlayerPrefs.DeleteKey(_ownerIdUsedInTest + ":best_score");
            }
            PlayerPrefs.Save();
        }

        private Persistence NewPersistenceAndTrack()
        {
            var persistence = new Persistence();
            _ownerIdUsedInTest = persistence.OwnerId;
            return persistence;
        }

        [Test]
        public void SaveThenLoad_RestoresStatsAggregatesAndAllRegions()
        {
            var persistence = NewPersistenceAndTrack();
            var season = new Season(seed: 7)
            {
                Day = 9,
                Budget = 60,
            };

            // 6地域すべてに異なる値を入れ、特定の1件だけでなく全件が往復することを確認する。
            for (int i = 0; i < Region.Count; i++)
            {
                var region = Region.All[i];
                var state = season.GetRegionState(region.Id);
                state.Awareness = 10 + i;
                state.Sentiment = -50 + i;
                state.Backlash = i % 2 == 0;
                state.AccessPenalty = i;
                state.Closed = i == Region.Count - 1;
            }

            season.Stats.SatisfiedTotal = 321;
            season.Stats.GiveUpTotal = 12;
            for (int i = 0; i < Region.Count; i++)
            {
                season.Stats.VisitorsByRegion[i] = 100 + i;
                season.Stats.RevenueByRegion[i] = 1000 + i;
                season.Stats.OvercrowdedDays[i] = i;
                season.Stats.DesertedDays[i] = Region.Count - i;
            }
            for (int i = 0; i < Stats.MeasureKindCount; i++)
            {
                season.Stats.UsesByMeasure[i] = i + 1;
            }

            persistence.Save(season);
            Season restored = persistence.Load();

            Assert.IsNotNull(restored);

            for (int i = 0; i < Region.Count; i++)
            {
                var region = Region.All[i];
                var expected = season.GetRegionState(region.Id);
                var actual = restored.GetRegionState(region.Id);

                Assert.AreEqual(expected.Awareness, actual.Awareness, $"region {region.Id} Awareness");
                Assert.AreEqual(expected.Sentiment, actual.Sentiment, $"region {region.Id} Sentiment");
                Assert.AreEqual(expected.Backlash, actual.Backlash, $"region {region.Id} Backlash");
                Assert.AreEqual(expected.AccessPenalty, actual.AccessPenalty, $"region {region.Id} AccessPenalty");
                Assert.AreEqual(expected.Closed, actual.Closed, $"region {region.Id} Closed");
            }

            Assert.AreEqual(321, restored.Stats.SatisfiedTotal);
            Assert.AreEqual(12, restored.Stats.GiveUpTotal);
            Assert.AreEqual(Region.Count, restored.Stats.VisitorsByRegion.Length);
            Assert.AreEqual(Region.Count, restored.Stats.RevenueByRegion.Length);
            Assert.AreEqual(Region.Count, restored.Stats.OvercrowdedDays.Length);
            Assert.AreEqual(Region.Count, restored.Stats.DesertedDays.Length);
            Assert.AreEqual(Stats.MeasureKindCount, restored.Stats.UsesByMeasure.Length);

            for (int i = 0; i < Region.Count; i++)
            {
                Assert.AreEqual(100 + i, restored.Stats.VisitorsByRegion[i], $"VisitorsByRegion[{i}]");
                Assert.AreEqual(1000 + i, restored.Stats.RevenueByRegion[i], $"RevenueByRegion[{i}]");
                Assert.AreEqual(i, restored.Stats.OvercrowdedDays[i], $"OvercrowdedDays[{i}]");
                Assert.AreEqual(Region.Count - i, restored.Stats.DesertedDays[i], $"DesertedDays[{i}]");
            }
            for (int i = 0; i < Stats.MeasureKindCount; i++)
            {
                Assert.AreEqual(i + 1, restored.Stats.UsesByMeasure[i], $"UsesByMeasure[{i}]");
            }
        }

        [Test]
        public void Season_HasNoPlanField_SoEditingPlanCanNeverBePersisted()
        {
            // 受け入れ条件「編集中のプランは保存しない」は、現状はSeason型がPlanへの参照を
            // 一切持たないことで構造的に保証されている（PR #8の説明どおり）。
            // Season に将来 Plan 型（またはPlanと同名）のフィールド／プロパティが追加されると
            // JsonUtility経由で編集中プランがそのまま保存されてしまうため、その追加を検知する。
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Instance | BindingFlags.Static;

            foreach (FieldInfo field in typeof(Season).GetFields(flags))
            {
                Assert.AreNotEqual(
                    typeof(Plan),
                    field.FieldType,
                    $"Season.{field.Name} が Plan 型を保持している。編集中のプランは保存しない設計（5.8節）に反する。");
            }

            foreach (PropertyInfo property in typeof(Season).GetProperties(flags))
            {
                Assert.AreNotEqual(
                    typeof(Plan),
                    property.PropertyType,
                    $"Season.{property.Name} が Plan 型を保持している。編集中のプランは保存しない設計（5.8節）に反する。");
            }
        }
    }
}
