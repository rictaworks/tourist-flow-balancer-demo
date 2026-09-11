using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TouristFlowBalancer.Bootstrap
{
    /// <summary>
    /// requirements.md 14.3節：シーン・UI・オブジェクトはすべて起動時にコードで動的生成し、
    /// Prefab・シーンファイルの手作業編集を前提としない。
    ///
    /// このクラスは基盤issueの範囲として、起動時に最小限の土台（EventSystem・Canvas）だけを
    /// コードで生成する。実際の画面（地図・施策パネル等）は以降のUI issueがこのCanvas上に構築する。
    /// </summary>
    public static class GameBootstrap
    {
        private const string RootObjectName = "GameRoot";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BuildScene()
        {
            if (GameObject.Find(RootObjectName) != null)
            {
                // 既に構築済み（シーン再読込・テスト等での多重初期化を避ける）。
                return;
            }

            var root = new GameObject(RootObjectName);
            Object.DontDestroyOnLoad(root);

            BuildEventSystem(root.transform);
            BuildCanvas(root.transform);
        }

        private static void BuildEventSystem(Transform parent)
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null)
            {
                return;
            }

            var eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.transform.SetParent(parent, false);
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
        }

        private static void BuildCanvas(Transform parent)
        {
            var canvasObject = new GameObject("Canvas", typeof(RectTransform));
            canvasObject.transform.SetParent(parent, false);

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);

            canvasObject.AddComponent<GraphicRaycaster>();
        }
    }
}
