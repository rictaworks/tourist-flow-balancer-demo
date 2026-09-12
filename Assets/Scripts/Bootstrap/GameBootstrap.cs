using TouristFlowBalancer.Infra;
using TouristFlowBalancer.Logic;
using TouristFlowBalancer.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TouristFlowBalancer.Bootstrap
{
    /// <summary>
    /// requirements.md 14.3節：シーン・UI・オブジェクトはすべて起動時にコードで動的生成し、
    /// Prefab・シーンファイルの手作業編集を前提としない。
    ///
    /// 起動時にEventSystem・Canvasの土台を構築したのち、<see cref="Persistence"/>（保存・日次リセット）と
    /// <see cref="GameFlow"/>（状態遷移）を組み立て、<see cref="UIPresenter"/>へ渡してCanvas上に
    /// 5画面（タイトル・計画・流入・日次レポート・シーズン結果）を構築させる（10章 classDiagram）。
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
            GameObject canvasObject = BuildCanvas(root.transform);

            var persistence = new Persistence();
            var gameFlow = new GameFlow(persistence);

            var presenter = canvasObject.AddComponent<UIPresenter>();
            presenter.Initialize(canvasObject.transform, gameFlow);
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

        private static GameObject BuildCanvas(Transform parent)
        {
            var canvasObject = new GameObject("Canvas", typeof(RectTransform));
            canvasObject.transform.SetParent(parent, false);

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);

            canvasObject.AddComponent<GraphicRaycaster>();

            return canvasObject;
        }
    }
}
