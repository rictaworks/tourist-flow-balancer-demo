using TouristFlowBalancer.Core;
using UnityEngine;

namespace TouristFlowBalancer.UI
{
    /// <summary>
    /// requirements.md 13章・14.3節：中央ハブと6地域を配置した簡易地図の座標。
    /// 実在の地図データ・地名を使わない架空の配置（14.3節）。すべて親矩形（地図パネル）に対する
    /// 割合（0〜1）で表し、計画画面（地図）と流入画面の両方から同じ座標を参照することで、
    /// 「地域の位置が画面によってずれる」ことを防ぐ。
    /// </summary>
    internal static class MapLayout
    {
        public const float HubX = 0.5f;
        public const float HubY = 0.55f;
        private const float RadiusX = 0.38f;
        private const float RadiusY = 0.34f;

        public static Vector2 Hub => new Vector2(HubX, HubY);

        /// <summary>地域ID（1始まり）の地図上の位置（割合座標）。6地域を円状に配置する。</summary>
        public static Vector2 RegionPosition(int regionId)
        {
            int index = regionId - 1;
            float angleDeg = -90f + index * (360f / Region.Count);
            float angleRad = angleDeg * Mathf.Deg2Rad;
            return new Vector2(
                HubX + RadiusX * Mathf.Cos(angleRad),
                HubY + RadiusY * Mathf.Sin(angleRad));
        }
    }
}
