using NUnit.Framework;
using TouristFlowBalancer.Core;

namespace TouristFlowBalancer.Tests.EditMode
{
    /// <summary>
    /// requirements.md 3.2節の表とSegmentマスタの値が一致することを検証する。
    /// </summary>
    public class SegmentMasterTests
    {
        [Test]
        public void All_Contains4Segments()
        {
            Assert.AreEqual(4, Segment.All.Count);
            Assert.AreEqual(4, Segment.Count);
        }

        [Test]
        public void Family_MatchesSpec()
        {
            var segment = Segment.All[0];
            Assert.AreEqual("家族", segment.Name);
            Assert.AreEqual(0.30f, segment.Share, 0.0001f);
            Assert.AreEqual(0.25f, segment.DistanceAversion, 0.0001f);
            Assert.AreEqual(PriceSensitivity.High, segment.Price);
            Assert.IsTrue(segment.CrowdAverse);
            CollectionAssert.AreEquivalent(new[] { Tag.Experience, Tag.Nature }, segment.PreferredTags);
        }

        [Test]
        public void Young_MatchesSpec()
        {
            var segment = Segment.All[1];
            Assert.AreEqual("若年", segment.Name);
            Assert.AreEqual(0.30f, segment.Share, 0.0001f);
            Assert.AreEqual(0.10f, segment.DistanceAversion, 0.0001f);
            Assert.AreEqual(PriceSensitivity.High, segment.Price);
            Assert.IsFalse(segment.CrowdAverse);
            CollectionAssert.AreEquivalent(new[] { Tag.Gourmet, Tag.Scenery }, segment.PreferredTags);
        }

        [Test]
        public void Senior_MatchesSpec()
        {
            var segment = Segment.All[2];
            Assert.AreEqual("シニア", segment.Name);
            Assert.AreEqual(0.20f, segment.Share, 0.0001f);
            Assert.AreEqual(0.40f, segment.DistanceAversion, 0.0001f);
            Assert.AreEqual(PriceSensitivity.Low, segment.Price);
            Assert.IsTrue(segment.CrowdAverse);
            CollectionAssert.AreEquivalent(new[] { Tag.History, Tag.Healing }, segment.PreferredTags);
        }

        [Test]
        public void Inbound_MatchesSpec()
        {
            var segment = Segment.All[3];
            Assert.AreEqual("訪日", segment.Name);
            Assert.AreEqual(0.20f, segment.Share, 0.0001f);
            Assert.AreEqual(0.15f, segment.DistanceAversion, 0.0001f);
            Assert.AreEqual(PriceSensitivity.Low, segment.Price);
            Assert.IsFalse(segment.CrowdAverse);
            CollectionAssert.AreEquivalent(new[] { Tag.History, Tag.Scenery }, segment.PreferredTags);
        }

        [Test]
        public void Shares_SumToOne()
        {
            float total = 0f;
            foreach (var segment in Segment.All)
            {
                total += segment.Share;
            }
            Assert.AreEqual(1.0f, total, 0.0001f);
        }

        [Test]
        public void CountMatchingTags_CountsOverlapWithRegionTags()
        {
            var family = Segment.All[0]; // 好むタグ：体験・自然
            var mountainVillage = Region.ById(4); // タグ：自然・体験
            var centralTouristArea = Region.ById(1); // タグ：歴史・グルメ

            Assert.AreEqual(2, family.CountMatchingTags(mountainVillage));
            Assert.AreEqual(0, family.CountMatchingTags(centralTouristArea));
        }
    }
}
