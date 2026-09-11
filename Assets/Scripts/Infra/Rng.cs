using System;

namespace TouristFlowBalancer.Infra
{
    /// <summary>
    /// 決定的な疑似乱数生成器（xorshift32）。
    /// <see cref="System.Random"/> はプラットフォーム・.NETランタイムによって内部実装が変わる可能性があり、
    /// WebGL（IL2CPP）を含む全プラットフォームで同一シードから同一の系列を再現する保証がないため使わない。
    /// このクラスは自己完結した整数演算のみで構成し、同一シードなら常に同一の系列を返す。
    /// </summary>
    public sealed class Rng
    {
        private uint _state;

        public Rng(uint seed)
        {
            // xorshiftは内部状態が0だと恒等写像になり乱数が壊れるため、0を避ける。
            _state = seed == 0 ? 0x9E3779B9u : seed;
        }

        private uint NextUInt()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }

        /// <summary>[0.0, 1.0) の一様乱数。</summary>
        public double NextDouble()
        {
            return NextUInt() / 4294967296.0; // 2^32
        }

        /// <summary>[minInclusive, maxExclusive) の一様乱数整数。</summary>
        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), maxExclusive, "maxExclusiveはminInclusiveより大きい必要があります。");
            }
            int range = maxExclusive - minInclusive;
            int offset = (int)(NextDouble() * range);
            if (offset >= range)
            {
                offset = range - 1; // NextDouble()が1.0に極めて近い場合の境界保護
            }
            return minInclusive + offset;
        }

        /// <summary>[0, 100) の整数（出来事の出現割合など、パーセンテージ抽選に使う）。</summary>
        public int NextPercent()
        {
            return NextInt(0, 100);
        }

        /// <summary>1.0を中心に ±range の一様な揺らぎ係数（例：range=0.10なら0.90〜1.10）。</summary>
        public double NextFluctuation(double range)
        {
            return 1.0 + (NextDouble() * 2.0 - 1.0) * range;
        }
    }
}
