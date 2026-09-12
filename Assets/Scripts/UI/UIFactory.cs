using UnityEngine;
using UnityEngine.UI;

namespace TouristFlowBalancer.UI
{
    /// <summary>
    /// requirements.md 14.3節：シーン・UIはすべて起動時にコードで動的生成し、Prefab・シーンファイルの
    /// 手作業編集を前提としない。表示はプリミティブとuGUI既定フォントのみを用いる。
    /// この共通ヘルパーに正規化することで、各画面クラスが個別にフォント・アンカー計算を持たないようにする。
    /// </summary>
    internal static class UIFactory
    {
        private static Font _defaultFont;

        /// <summary>
        /// uGUIの既定フォント（Text コンポーネント。TextMeshPro 等の追加アセットは使わない。14.3節）。
        ///
        /// Unityの真のビルトインフォント（LegacyRuntime.ttf）は日本語グリフを一切持たず、
        /// 実機（WebGLビルドをブラウザで確認）では本文の日本語がすべて空白になる
        /// （Unity既定フォントの既知の制約。robo-farm-rules-demo で先に踏んだ問題と同型）。
        /// このため、Assets/Resources/Fonts/ に同梱した M PLUS 1p（SIL Open Font License 1.1、
        /// 再配布可）を「uGUIの既定として使うフォント」として読み込む。TextMeshPro・カスタムの
        /// フォントアセット機能（マテリアル調整等）は使わず、Text コンポーネントの font 参照を
        /// 差し替えるだけなので、14.3節「uGUI既定フォントのみ」の範囲内に収まる。
        /// 万一同梱フォントが読み込めない場合はUnityのビルトインへ後退する（表示が完全に消えるより安全）。
        ///
        /// 注記（2026-09-12実機確認）：Google FontsのNoto Sans JPはバリアブルフォント（VF）版のみが
        /// 配布されており、Unity 6のuGUI Text（Legacy）がVFフォントのグリフをレンダリングしようとすると
        /// ネイティブのフォントエンジン内で無限再帰（RangeError: Maximum call stack size exceeded）を
        /// 起こし、WebGLビルドが起動直後にフリーズする。静的ウェイト（Variableでない）のフォントに
        /// 差し替える必要があり、M PLUS 1p（同じくSIL OFL、静的ウェイト配布あり）に変更した。
        /// </summary>
        public static Font DefaultFont()
        {
            if (_defaultFont == null)
            {
                _defaultFont = Resources.Load<Font>("Fonts/MPLUS1p-Regular");
                if (_defaultFont == null)
                {
                    _defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
            }
            return _defaultFont;
        }

        public static RectTransform CreateRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.localScale = Vector3.one;
            return rt;
        }

        public static RectTransform CreatePanel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.localScale = Vector3.one;
            go.GetComponent<Image>().color = color;
            return rt;
        }

        public static Text CreateText(
            Transform parent,
            string name,
            string content,
            int fontSize,
            Color color,
            TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.localScale = Vector3.one;

            var text = go.GetComponent<Text>();
            text.font = DefaultFont();
            text.fontSize = fontSize;
            text.text = content;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        /// <summary>背景パネル＋中央寄せテキストのボタン。クリック処理は呼び出し側でonClickに登録する。</summary>
        public static Button CreateButton(Transform parent, string name, string label, Color background, Color textColor)
        {
            var rt = CreatePanel(parent, name, background);
            var button = AddButtonBehaviour(rt);

            var text = CreateText(rt, "Label", label, 18, textColor);
            Stretch((RectTransform)text.transform);

            return button;
        }

        /// <summary>
        /// 既存のパネル（Image付きRectTransform）にButtonを追加する。targetGraphicを明示的に
        /// そのImageへ結び付ける（AddComponentだけでは自動配線されず、押下時の色フィードバックが
        /// 効かないままになるため）。
        /// </summary>
        public static Button AddButtonBehaviour(RectTransform panel)
        {
            var button = panel.gameObject.AddComponent<Button>();
            var image = panel.GetComponent<Image>();
            if (image != null)
            {
                button.targetGraphic = image;
            }
            return button;
        }

        /// <summary>親いっぱいに広げる（オフセット0）。</summary>
        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// 親の矩形に対する割合（0〜1）でアンカーを設定する。オフセットは常に0にするため、
        /// 実際のピクセルサイズは親の実サイズに追従する（解像度非依存のレイアウト。14.3節）。
        /// </summary>
        public static void AnchorFraction(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
        {
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        /// <summary>
        /// 点アンカー（anchorMin==anchorMax）で親矩形上の1点に固定サイズの要素を置く。
        /// 流入アニメーションの移動点のように、解像度非依存のまま座標を補間したい場合に使う。
        /// </summary>
        public static void AnchorPoint(RectTransform rt, Vector2 fraction, Vector2 size)
        {
            rt.anchorMin = fraction;
            rt.anchorMax = fraction;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
        }

        /// <summary>
        /// 親矩形上の割合座標<paramref name="centerFraction"/>を中心に、親矩形に対する割合の幅・高さを持つ
        /// 矩形を配置する（地図上の地域ノードのように、解像度非依存のまま一定の大きさで置きたい場合に使う）。
        /// </summary>
        public static void AnchorPointBox(RectTransform rt, Vector2 centerFraction, float widthFraction, float heightFraction)
        {
            float halfW = widthFraction / 2f;
            float halfH = heightFraction / 2f;
            AnchorFraction(
                rt,
                centerFraction.x - halfW,
                centerFraction.y - halfH,
                centerFraction.x + halfW,
                centerFraction.y + halfH);
        }
    }
}
