using NUnit.Framework;
using TouristFlowBalancer.Infra;

namespace TouristFlowBalancer.Tests.EditMode
{
    /// <summary>
    /// requirements.md 5.1節手順1・14.1節：同一シードから同一の3系列（出来事種類・対象地域・来訪数）を
    /// 再現できることを検証する。
    /// </summary>
    public class SplitRngTests
    {
        private const int SampleCount = 50;

        [Test]
        public void SameSeed_ProducesSameEventKindSequence()
        {
            var a = new SplitRng(12345);
            var b = new SplitRng(12345);

            for (int i = 0; i < SampleCount; i++)
            {
                Assert.AreEqual(a.EventKind.NextPercent(), b.EventKind.NextPercent(), $"i={i}");
            }
        }

        [Test]
        public void SameSeed_ProducesSameEventTargetSequence()
        {
            var a = new SplitRng(12345);
            var b = new SplitRng(12345);

            for (int i = 0; i < SampleCount; i++)
            {
                Assert.AreEqual(a.EventTarget.NextInt(0, 6), b.EventTarget.NextInt(0, 6), $"i={i}");
            }
        }

        [Test]
        public void SameSeed_ProducesSameArrivalsSequence()
        {
            var a = new SplitRng(12345);
            var b = new SplitRng(12345);

            for (int i = 0; i < SampleCount; i++)
            {
                Assert.AreEqual(a.Arrivals.NextFluctuation(0.10), b.Arrivals.NextFluctuation(0.10), 1e-12, $"i={i}");
            }
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentSequences()
        {
            var a = new SplitRng(1);
            var b = new SplitRng(2);

            bool anyDifferent = false;
            for (int i = 0; i < SampleCount; i++)
            {
                if (a.EventKind.NextPercent() != b.EventKind.NextPercent())
                {
                    anyDifferent = true;
                    break;
                }
            }
            Assert.IsTrue(anyDifferent, "異なるシードなら出来事種類の系列も異なるはずである。");
        }

        [Test]
        public void StreamsWithinSameSeed_AreIndependent()
        {
            // 系列を分離する目的（14.1節）：施策の内容が出来事や来訪数に影響しないよう、
            // 同一シード内でも3系列は互いに異なる（同一の値の列を返さない）系列でなければならない。
            var rng = new SplitRng(777);

            bool anyDifferent = false;
            for (int i = 0; i < SampleCount; i++)
            {
                double eventKindValue = rng.EventKind.NextDouble();
                double eventTargetValue = rng.EventTarget.NextDouble();
                double arrivalsValue = rng.Arrivals.NextDouble();

                if (eventKindValue != eventTargetValue || eventTargetValue != arrivalsValue)
                {
                    anyDifferent = true;
                    break;
                }
            }
            Assert.IsTrue(anyDifferent, "3系列は独立しているため、同一シード内でも値の列が一致しないはずである。");
        }

        [Test]
        public void Rng_NextInt_StaysWithinRange()
        {
            var rng = new Rng(42);
            for (int i = 0; i < 1000; i++)
            {
                int value = rng.NextInt(0, 6);
                Assert.GreaterOrEqual(value, 0);
                Assert.Less(value, 6);
            }
        }

        [Test]
        public void Rng_SameSeed_ProducesSameSequence()
        {
            var a = new Rng(999);
            var b = new Rng(999);

            for (int i = 0; i < SampleCount; i++)
            {
                Assert.AreEqual(a.NextDouble(), b.NextDouble(), 1e-12);
            }
        }
    }
}
