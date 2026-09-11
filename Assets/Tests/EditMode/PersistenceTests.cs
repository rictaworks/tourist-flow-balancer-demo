using System;
using NUnit.Framework;
using TouristFlowBalancer.Core;
using TouristFlowBalancer.Infra;
using UnityEngine;

namespace TouristFlowBalancer.Tests.EditMode
{
    /// <summary>
    /// requirements.md 5.8節・6章・10章：Persistence（関数G persist / resetIfNeeded）の検証。
    ///
    /// PlayerPrefsはEditModeテストでも実際に書き込まれる共有ストアであり、"owner_id" は
    /// オーナーIDの唯一の接頭辞なしキーとしてテスト間でも共有されてしまうため、
    /// 各テストの前後で明示的にキーを削除し、他のテストと状態が混ざらないようにする
    /// （ファイル削除コマンドではなく、PlayerPrefsのAPI呼び出しであることに注意）。
    /// </summary>
    public class PersistenceTests
    {
        private string _ownerIdUsedInTest;

        [SetUp]
        public void SetUp()
        {
            // 前のテスト（や他のテスト実行）の残骸が無いことを保証してから開始する。
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

        // --- オーナーID ---

        [Test]
        public void OwnerId_GeneratedOnFirstAccess_IsNotEmpty()
        {
            var persistence = NewPersistenceAndTrack();
            Assert.IsFalse(string.IsNullOrEmpty(persistence.OwnerId));
        }

        [Test]
        public void OwnerId_ReusedAcrossInstances_AfterFirstGeneration()
        {
            var first = NewPersistenceAndTrack();
            string generated = first.OwnerId;

            var second = new Persistence();

            Assert.AreEqual(generated, second.OwnerId, "2回目以降はPlayerPrefsに保存済みのオーナーIDを読み込むはずである。");
        }

        // --- 保存キーの接頭辞 ---

        [Test]
        public void AllSavedKeys_ExceptOwnerId_HaveOwnerIdPrefix()
        {
            var persistence = NewPersistenceAndTrack();
            var season = new Season(seed: 10);

            // ResetIfNeeded を先に実行して last_reset_at を確立する。Save/SaveResultの後に呼ぶと
            // （このテストでは前回リセット日時が未保存のため）リセット判定でこれらのキーが
            // 削除されてしまい、このテストが検証したい「保存キーの接頭辞」の確認ができなくなる。
            persistence.ResetIfNeeded(new DateTimeOffset(2026, 1, 1, 3, 0, 0, TimeSpan.FromHours(9)));
            persistence.Save(season);
            persistence.SaveResult(seed: 10, score: 5);

            // owner_id は接頭辞なしの唯一のキー。
            Assert.IsTrue(PlayerPrefs.HasKey("owner_id"));

            // last_seed・best_score・last_reset_at は接頭辞付きで保存されている。
            Assert.IsTrue(PlayerPrefs.HasKey(persistence.OwnerId + ":last_seed"));
            Assert.IsTrue(PlayerPrefs.HasKey(persistence.OwnerId + ":best_score"));
            Assert.IsTrue(PlayerPrefs.HasKey(persistence.OwnerId + ":last_reset_at"));

            // 接頭辞なしの同名キーは存在しない。
            Assert.IsFalse(PlayerPrefs.HasKey("last_seed"));
            Assert.IsFalse(PlayerPrefs.HasKey("best_score"));
            Assert.IsFalse(PlayerPrefs.HasKey("last_reset_at"));
            Assert.IsFalse(PlayerPrefs.HasKey("season"));
        }

        // --- 保存・復元（save / load） ---

        [Test]
        public void Load_WithoutPriorSave_ReturnsNull()
        {
            var persistence = NewPersistenceAndTrack();
            Assert.IsNull(persistence.Load());
        }

        [Test]
        public void SaveThenLoad_RestoresSeasonContent()
        {
            var persistence = NewPersistenceAndTrack();
            var season = new Season(seed: 2026)
            {
                Day = 5,
                Budget = 150,
            };
            season.GetRegionState(3).Awareness = 77;
            season.GetRegionState(3).Sentiment = -12;
            season.Stats.SatisfiedTotal = 999;

            persistence.Save(season);
            Season restored = persistence.Load();

            Assert.IsNotNull(restored);
            Assert.AreEqual(season.Seed, restored.Seed);
            Assert.AreEqual(season.Day, restored.Day);
            Assert.AreEqual(season.Budget, restored.Budget);
            Assert.AreEqual(season.State, restored.State);
            Assert.AreEqual(Region.Count, restored.Regions.Length);
            Assert.AreEqual(77, restored.GetRegionState(3).Awareness);
            Assert.AreEqual(-12, restored.GetRegionState(3).Sentiment);
            Assert.AreEqual(999, restored.Stats.SatisfiedTotal);
        }

        [Test]
        public void HasSavedSeason_ReflectsPresenceOfSeasonKey()
        {
            var persistence = NewPersistenceAndTrack();
            Assert.IsFalse(persistence.HasSavedSeason());

            persistence.Save(new Season(seed: 1));
            Assert.IsTrue(persistence.HasSavedSeason());
        }

        // --- シーズン精算結果の保存（saveResult） ---

        [Test]
        public void SaveResult_StoresLastSeed_AndClearsInProgressSeason()
        {
            var persistence = NewPersistenceAndTrack();
            persistence.Save(new Season(seed: 42));
            Assert.IsTrue(persistence.HasSavedSeason());

            persistence.SaveResult(seed: 42, score: 100);

            Assert.AreEqual(42, persistence.LoadLastSeed());
            Assert.IsFalse(persistence.HasSavedSeason(), "精算後は進行中の保存データを削除する（5.6節手順5）。");
        }

        [Test]
        public void SaveResult_UpdatesBestScore_OnlyWhenHigher()
        {
            var persistence = NewPersistenceAndTrack();
            Assert.AreEqual(0, persistence.LoadBestScore());

            persistence.SaveResult(seed: 1, score: 100);
            Assert.AreEqual(100, persistence.LoadBestScore());

            persistence.SaveResult(seed: 2, score: 50);
            Assert.AreEqual(100, persistence.LoadBestScore(), "より低いスコアではベストスコアを更新しない。");

            persistence.SaveResult(seed: 3, score: 150);
            Assert.AreEqual(150, persistence.LoadBestScore(), "より高いスコアではベストスコアを更新する。");
        }

        [Test]
        public void LoadLastSeed_WithoutPriorSaveResult_ReturnsNull()
        {
            var persistence = NewPersistenceAndTrack();
            Assert.IsNull(persistence.LoadLastSeed());
        }

        // --- 日次リセット境界（JST 03:00） ---

        [Test]
        public void ResetIfNeeded_LastResetJustBeforeBoundary_NowJustAfterBoundary_Resets()
        {
            var persistence = NewPersistenceAndTrack();
            SetLastResetAt(persistence, new DateTimeOffset(2026, 9, 10, 2, 59, 0, TimeSpan.FromHours(9)));

            bool didReset = persistence.ResetIfNeeded(new DateTimeOffset(2026, 9, 10, 3, 1, 0, TimeSpan.FromHours(9)));

            Assert.IsTrue(didReset, "前回リセット(02:59)が直近の境界(03:00)より前で、現在時刻がその境界を越えているためリセットするはずである。");
        }

        [Test]
        public void ResetIfNeeded_LastResetJustAfterBoundary_NowSameDayLater_DoesNotReset()
        {
            var persistence = NewPersistenceAndTrack();
            SetLastResetAt(persistence, new DateTimeOffset(2026, 9, 10, 3, 1, 0, TimeSpan.FromHours(9)));

            bool didReset = persistence.ResetIfNeeded(new DateTimeOffset(2026, 9, 10, 3, 5, 0, TimeSpan.FromHours(9)));

            Assert.IsFalse(didReset, "前回リセット(03:01)は直近の境界(03:00)より後であり、まだ境界を越えていないためリセットしないはずである。");
        }

        [Test]
        public void ResetIfNeeded_LastResetAtPreviousBoundary_NowBeforeTodayBoundary_DoesNotReset()
        {
            var persistence = NewPersistenceAndTrack();
            SetLastResetAt(persistence, new DateTimeOffset(2026, 9, 9, 3, 0, 0, TimeSpan.FromHours(9)));

            bool didReset = persistence.ResetIfNeeded(new DateTimeOffset(2026, 9, 10, 2, 59, 59, TimeSpan.FromHours(9)));

            Assert.IsFalse(didReset, "現在時刻がまだ今日の境界(03:00)に達していないため、前日の境界のままでリセットしないはずである。");
        }

        [Test]
        public void ResetIfNeeded_LastResetAtPreviousBoundary_NowAtTodayBoundary_Resets()
        {
            var persistence = NewPersistenceAndTrack();
            SetLastResetAt(persistence, new DateTimeOffset(2026, 9, 9, 3, 0, 0, TimeSpan.FromHours(9)));

            bool didReset = persistence.ResetIfNeeded(new DateTimeOffset(2026, 9, 10, 3, 0, 0, TimeSpan.FromHours(9)));

            Assert.IsTrue(didReset, "現在時刻が今日の境界(03:00)にちょうど達したため、新しい境界を越えたとしてリセットするはずである。");
        }

        [Test]
        public void ResetIfNeeded_FirstEverRun_WithoutPriorLastResetAt_Resets()
        {
            var persistence = NewPersistenceAndTrack();

            bool didReset = persistence.ResetIfNeeded(new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.FromHours(9)));

            Assert.IsTrue(didReset, "前回リセット日時が未保存（初回起動）の場合はリセット手順を通す。");
        }

        [Test]
        public void ResetIfNeeded_DeletesSeasonAndResultKeys_ButKeepsOwnerId()
        {
            var persistence = NewPersistenceAndTrack();
            persistence.Save(new Season(seed: 1));
            persistence.SaveResult(seed: 1, score: 10);
            // SaveResult は season キーを消すため、リセット判定前に再度保存して存在させる。
            persistence.Save(new Season(seed: 1));
            SetLastResetAt(persistence, new DateTimeOffset(2026, 9, 9, 2, 0, 0, TimeSpan.FromHours(9)));

            persistence.ResetIfNeeded(new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.FromHours(9)));

            Assert.IsTrue(PlayerPrefs.HasKey("owner_id"), "owner_idは削除対象から除外される唯一のキーである。");
            Assert.IsFalse(persistence.HasSavedSeason());
            Assert.IsNull(persistence.LoadLastSeed());
            Assert.AreEqual(0, persistence.LoadBestScore());
        }

        [Test]
        public void ResetIfNeeded_ParameterlessOverload_UsesCurrentJstTime()
        {
            var persistence = NewPersistenceAndTrack();
            // 前回リセット日時を十分未来に設定し、現在時刻（実行環境の「今」）では境界を越えないことを確認する。
            SetLastResetAt(persistence, Persistence.NowJst().AddYears(1));

            bool didReset = persistence.ResetIfNeeded();

            Assert.IsFalse(didReset);
        }

        private static void SetLastResetAt(Persistence persistence, DateTimeOffset value)
        {
            PlayerPrefs.SetString(persistence.OwnerId + ":last_reset_at", value.ToString("o"));
            PlayerPrefs.Save();
        }
    }
}
