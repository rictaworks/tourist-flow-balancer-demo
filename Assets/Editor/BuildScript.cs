using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// requirements.md 14.3節・14.5節、CLAUDE.md「開発コマンド」：CLIビルド用のエディタスクリプト。
/// 名前空間を持たないグローバル名前空間に置く（仕様書・CLAUDE.mdが明示する実行コマンドが
/// 常に "-executeMethod BuildScript.BuildWebGL" という名前空間なしの完全修飾名だからである）。
///
/// 実行例：
///   Unity.exe -batchmode -nographics -executeMethod BuildScript.BuildWebGL -quit
/// </summary>
public static class BuildScript
{
    private const string GeneratedSceneDirectory = "Assets/Scenes";
    private const string GeneratedScenePath = GeneratedSceneDirectory + "/Main.unity";
    private const string WebGlOutputDirectory = "Build/WebGL";

    public static void BuildWebGL()
    {
        string scenePath = EnsureMainSceneExists();

        var options = new BuildPlayerOptions
        {
            scenes = new[] { scenePath },
            locationPathName = WebGlOutputDirectory,
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        Debug.Log(
            $"Build result: {summary.result} " +
            $"(errors: {summary.totalErrors}, warnings: {summary.totalWarnings}, size: {summary.totalSize} bytes)");

        if (summary.result != BuildResult.Succeeded)
        {
            EditorApplication.Exit(1);
        }
    }

    /// <summary>
    /// requirements.md 14.3節：シーン・UIはすべてコードで動的生成し、Prefab・シーンファイルの
    /// 手作業編集を前提としない。GameBootstrapが起動時に全オブジェクトを生成するための、
    /// 内容を持たない最小限のシーンをコードから用意する（Editor GUIでの手編集は行わない）。
    /// </summary>
    private static string EnsureMainSceneExists()
    {
        if (!Directory.Exists(GeneratedSceneDirectory))
        {
            Directory.CreateDirectory(GeneratedSceneDirectory);
        }

        if (!File.Exists(GeneratedScenePath))
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, GeneratedScenePath);
            AssetDatabase.Refresh();
        }

        RegisterSceneInBuildSettings(GeneratedScenePath);
        return GeneratedScenePath;
    }

    private static void RegisterSceneInBuildSettings(string scenePath)
    {
        var scenes = EditorBuildSettings.scenes;
        for (int i = 0; i < scenes.Length; i++)
        {
            if (scenes[i].path == scenePath)
            {
                return;
            }
        }

        var updated = new EditorBuildSettingsScene[scenes.Length + 1];
        scenes.CopyTo(updated, 0);
        updated[scenes.Length] = new EditorBuildSettingsScene(scenePath, true);
        EditorBuildSettings.scenes = updated;
    }
}
