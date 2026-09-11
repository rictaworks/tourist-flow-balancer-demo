using NUnit.Framework;
using TouristFlowBalancer.Core;
using TouristFlowBalancer.Infra;
using TouristFlowBalancer.Logic;
using UnityEngine;

namespace TouristFlowBalancer.Tests.EditMode
{
    /// <summary>
    /// requirements.md 5.6節：シーズン精算（関数F, SeasonFinalizer）を検証する。
    /// PlayerPrefsを伴うベストスコア更新は、PersistenceTests系と同様にオーナーIDを都度クリーンアップする。
    /// </summary>
    public class SeasonFinalizerTests
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
        public void Finalize_NullSeason_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() => new SeasonFinalizer().Finalize(null));
        }

        [Test]
        public void Finalize_ScoreEqualsSatisfiedMinusGiveUps()
        {
            var season = new Season(seed: 5);
            season.Stats.SatisfiedTotal = 500;
            season.Stats.GiveUpTotal = 30;

            var result = new SeasonFinalizer().Finalize(season);

            Assert.AreEqual(470, result.Score);
            Assert.AreEqual(SeasonState.Finalized, season.State);
        }

        [Test]
        public void Finalize_WithoutPersistence_BestScoreEqualsScore()
        {
            var season = new Season(seed: 5);
            season.Stats.SatisfiedTotal = 200;
            season.Stats.GiveUpTotal = 10;

            var result = new SeasonFinalizer().Finalize(season, persistence: null);

            Assert.AreEqual(190, result.BestScore);
        }

        [Test]
        public void Finalize_CopiesFinalSentimentPerRegion()
        {
            var season = new Season(seed: 5);
            foreach (var region in Region.All)
            {
                season.GetRegionState(region.Id).Sentiment = region.Id * 10;
            }

            var result = new SeasonFinalizer().Finalize(season);

            foreach (var region in Region.All)
            {
                Assert.AreEqual(region.Id * 10, result.FinalSentimentByRegion[region.Id - 1]);
            }
        }

        [Test]
        public void Finalize_WithPersistence_UpdatesBestScore_AndDeletesInProgressSave()
        {
            var persistence = NewPersistenceAndTrack();
            var season = new Season(seed: 42);
            season.Stats.SatisfiedTotal = 1000;
            season.Stats.GiveUpTotal = 0;
            persistence.Save(season); // 精算前の進行中データを用意する

            var result = new SeasonFinalizer().Finalize(season, persistence);

            Assert.AreEqual(1000, result.Score);
            Assert.AreEqual(1000, result.BestScore);
            Assert.AreEqual(1000, persistence.LoadBestScore());
            Assert.AreEqual(42, persistence.LoadLastSeed());
            Assert.IsFalse(persistence.HasSavedSeason(), "精算後は進行中の保存データが削除されるはずである（5.6節手順5）。");
        }

        [Test]
        public void Finalize_WithPersistence_DoesNotLowerExistingBestScore()
        {
            var persistence = NewPersistenceAndTrack();

            var firstSeason = new Season(seed: 1);
            firstSeason.Stats.SatisfiedTotal = 800;
            new SeasonFinalizer().Finalize(firstSeason, persistence);
            Assert.AreEqual(800, persistence.LoadBestScore());

            var secondSeason = new Season(seed: 2);
            secondSeason.Stats.SatisfiedTotal = 300; // 前回より低いスコア
            var result = new SeasonFinalizer().Finalize(secondSeason, persistence);

            Assert.AreEqual(800, result.BestScore, "ベストスコアは今回より高い場合のみ更新されるはずである。");
            Assert.AreEqual(800, persistence.LoadBestScore());
        }
    }
}
