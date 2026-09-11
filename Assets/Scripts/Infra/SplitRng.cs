namespace TouristFlowBalancer.Infra
{
    /// <summary>
    /// requirements.md 5.1節手順1・14.1節：シードから出来事の種類（系列E）・出来事の対象地域（系列T）・
    /// 来訪数の揺らぎ（系列N）の3つの独立した乱数系列を作る。
    /// 系列を分離することで、プレイヤーが打つ施策の内容が出来事や来訪数に影響しない
    /// （＝同一シードなら施策の違いだけが結果の違いになる）。
    /// 同一シードを渡せば、常に同一の3系列（EventKind・EventTarget・Arrivals それぞれの抽選結果の列）を再現する。
    /// </summary>
    public sealed class SplitRng
    {
        public Rng EventKind { get; }
        public Rng EventTarget { get; }
        public Rng Arrivals { get; }

        // 各系列を区別するための固定タグ。単純に seed, seed+1, seed+2 のような近傍値を使うと、
        // xorshiftの内部状態が似通い、系列間の統計的独立性が損なわれるおそれがあるため、
        // SplitMix32相当の混合手順で十分に異なる初期状態を作る。
        private const uint EventKindStreamTag = 0x9E3779B1u;
        private const uint EventTargetStreamTag = 0x85EBCA77u;
        private const uint ArrivalsStreamTag = 0xC2B2AE3Du;

        public SplitRng(int seed)
        {
            EventKind = new Rng(DeriveStreamSeed(seed, EventKindStreamTag));
            EventTarget = new Rng(DeriveStreamSeed(seed, EventTargetStreamTag));
            Arrivals = new Rng(DeriveStreamSeed(seed, ArrivalsStreamTag));
        }

        private static uint DeriveStreamSeed(int seed, uint streamTag)
        {
            unchecked
            {
                uint z = (uint)seed + streamTag;
                // SplitMix32のミキシング手順（既知の定数を用いたビット拡散）。
                z = (z ^ (z >> 16)) * 0x85EBCA6Bu;
                z = (z ^ (z >> 13)) * 0xC2B2AE35u;
                z ^= z >> 16;
                return z == 0 ? 1u : z;
            }
        }
    }
}
