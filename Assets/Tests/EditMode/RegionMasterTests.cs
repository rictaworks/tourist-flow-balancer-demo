using NUnit.Framework;
using TouristFlowBalancer.Core;

namespace TouristFlowBalancer.Tests.EditMode
{
    /// <summary>
    /// requirements.md 3.1節の表とRegionマスタの値が一致することを検証する。
    /// </summary>
    public class RegionMasterTests
    {
        [Test]
        public void All_Contains6Regions()
        {
            Assert.AreEqual(6, Region.All.Count);
            Assert.AreEqual(6, Region.Count);
        }

        [Test]
        public void CentralTouristArea_MatchesSpec()
        {
            var region = Region.ById(1);
            Assert.AreEqual("中央観光地", region.Name);
            Assert.AreEqual(450, region.Capacity);
            Assert.AreEqual(90, region.BaseAttraction);
            Assert.AreEqual(100, region.BaseAwareness);
            Assert.AreEqual(1, region.AccessSteps);
            Assert.IsFalse(region.Outdoor);
            Assert.AreEqual(10, region.SpendPerVisitor);
            Assert.IsTrue(region.HasTag(Tag.History));
            Assert.IsTrue(region.HasTag(Tag.Gourmet));
        }

        [Test]
        public void HotSpringVillage_MatchesSpec()
        {
            var region = Region.ById(2);
            Assert.AreEqual("温泉郷", region.Name);
            Assert.AreEqual(300, region.Capacity);
            Assert.AreEqual(70, region.BaseAttraction);
            Assert.AreEqual(45, region.BaseAwareness);
            Assert.AreEqual(2, region.AccessSteps);
            Assert.IsFalse(region.Outdoor);
            Assert.AreEqual(14, region.SpendPerVisitor);
            Assert.IsTrue(region.HasTag(Tag.Healing));
            Assert.IsTrue(region.HasTag(Tag.Gourmet));
        }

        [Test]
        public void PortTown_MatchesSpec()
        {
            var region = Region.ById(3);
            Assert.AreEqual("港町", region.Name);
            Assert.AreEqual(350, region.Capacity);
            Assert.AreEqual(65, region.BaseAttraction);
            Assert.AreEqual(40, region.BaseAwareness);
            Assert.AreEqual(2, region.AccessSteps);
            Assert.IsFalse(region.Outdoor);
            Assert.AreEqual(9, region.SpendPerVisitor);
            Assert.IsTrue(region.HasTag(Tag.Gourmet));
            Assert.IsTrue(region.HasTag(Tag.Scenery));
        }

        [Test]
        public void MountainVillage_MatchesSpec()
        {
            var region = Region.ById(4);
            Assert.AreEqual("山里", region.Name);
            Assert.AreEqual(200, region.Capacity);
            Assert.AreEqual(60, region.BaseAttraction);
            Assert.AreEqual(25, region.BaseAwareness);
            Assert.AreEqual(3, region.AccessSteps);
            Assert.IsTrue(region.Outdoor);
            Assert.AreEqual(8, region.SpendPerVisitor);
            Assert.IsTrue(region.HasTag(Tag.Nature));
            Assert.IsTrue(region.HasTag(Tag.Experience));
        }

        [Test]
        public void OldTown_MatchesSpec()
        {
            var region = Region.ById(5);
            Assert.AreEqual("古街", region.Name);
            Assert.AreEqual(250, region.Capacity);
            Assert.AreEqual(70, region.BaseAttraction);
            Assert.AreEqual(35, region.BaseAwareness);
            Assert.AreEqual(2, region.AccessSteps);
            Assert.IsFalse(region.Outdoor);
            Assert.AreEqual(9, region.SpendPerVisitor);
            Assert.IsTrue(region.HasTag(Tag.History));
            Assert.IsTrue(region.HasTag(Tag.Scenery));
        }

        [Test]
        public void RemoteIsland_MatchesSpec()
        {
            var region = Region.ById(6);
            Assert.AreEqual("離島", region.Name);
            Assert.AreEqual(150, region.Capacity);
            Assert.AreEqual(80, region.BaseAttraction);
            Assert.AreEqual(20, region.BaseAwareness);
            Assert.AreEqual(4, region.AccessSteps);
            Assert.IsTrue(region.Outdoor);
            Assert.AreEqual(12, region.SpendPerVisitor);
            Assert.IsTrue(region.HasTag(Tag.Nature));
            Assert.IsTrue(region.HasTag(Tag.Scenery));
        }

        [Test]
        public void TotalCapacity_Is1700()
        {
            // 3.1節：総収容人数は1,700人であり、週末の来訪数（約1,800人）を上回らない。
            int total = 0;
            foreach (var region in Region.All)
            {
                total += region.Capacity;
            }
            Assert.AreEqual(1700, total);
        }

        [Test]
        public void ById_UnknownId_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => Region.ById(999));
        }
    }
}
