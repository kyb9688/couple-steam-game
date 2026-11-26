using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace TrashCleaner3D.Editor
{
    /// <summary>
    /// TrashCleaner3D 원클릭 / 배치모드 WebGL 빌드 도구:
    /// 1) 최신 v1.2 씬(Room01_PlayablePrototype)을 빌드
    /// 2) AlwaysIncludedShaders에서 URP/Lit 제거(수만 개 변형 폭주 방지!)하고,
    ///    런타임 Shader.Find 대상 셰이더들을 Resources/WebGLShaderKeep 머티리얼로 가볍게 참조(2초 컴파일!)
    /// 3) GitHub Pages 호환 설정(Compression Disabled + Data Caching)으로 WebGL 빌드 출력
    /// </summary>
    public static class WebGLBuildTool
    {
        private const string ScenePath = "Assets/_Project/Scenes/Room01_PlayablePrototype.unity";
        private const string DefaultOutputDir = @"C:\Users\qaws9\.gemini\antigravity\scratch\couple-steam-game\docs\webgl";
        private const string KeepMatDir = "Assets/Resources/WebGLShaderKeep";

        private static readonly string[] KeepRuntimeShaders =
        {
            "Universal Render Pipeline/Unlit",
            "TrashCleaner3D/WhiteOutlineInstanced",
            "TrashCleaner3D/DetectOutlineXRay",
            "TrashCleaner3D/CursorRingOverlay"
        };

        [MenuItem("TrashCleaner/Build WebGL (GitHub Pages Ready)")]
        public static void BuildWebGLMenu()
        {
            BuildWebGLInternal(DefaultOutputDir);
        }

        public static void BuildWebGLBatch()
        {
            string outDir = DefaultOutputDir;
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-webglOut")
                {
                    outDir = args[i + 1];
                    break;
                }
            }

            bool ok = BuildWebGLInternal(outDir);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        private static bool BuildWebGLInternal(string outputDir)
        {
            Debug.Log($"[WebGLBuildTool] Starting fast v1.2 WebGL build -> {outputDir}");

            // 1. AlwaysIncludedShaders에서 URP/Lit 등 대량 변형 셰이더 제거!
            RemoveHeavyAlwaysIncludedShaders();

            // 2. 런타임 Shader.Find용 4종 셰이더를 Resources 머티리얼로 가볍게 참조 (변형 폭주 0!)
            EnsureKeepMaterialsInResources();

            // 3. 최신 v1.2 씬 보장
            RoomToneTestBuilder.BuildRoom01();

            // 4. Build Settings 씬 등록
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };

            // 5. GitHub Pages 최적화 WebGL PlayerSettings
            PlayerSettings.productName = "TrashCleaner3D";
            PlayerSettings.companyName = "CoupleSteamGame";
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;
            PlayerSettings.runInBackground = true;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;

            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, true);
            }
            Directory.CreateDirectory(outputDir);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outputDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[WebGLBuildTool] SUCCEEDED! Size: {summary.totalSize / (1024f * 1024f):0.00} MB, Time: {summary.totalTime.TotalSeconds:0.0}s");
                return true;
            }

            Debug.LogError($"[WebGLBuildTool] FAILED: {summary.result} (Errors: {summary.totalErrors})");
            return false;
        }

        private static void RemoveHeavyAlwaysIncludedShaders()
        {
            var graphicsSettingsObj = AssetDatabase.LoadAssetAtPath<GraphicsSettings>("ProjectSettings/GraphicsSettings.asset");
            if (graphicsSettingsObj == null) return;

            var serialized = new SerializedObject(graphicsSettingsObj);
            var arrayProp = serialized.FindProperty("m_AlwaysIncludedShaders");
            if (arrayProp == null) return;

            bool changed = false;
            for (int i = arrayProp.arraySize - 1; i >= 0; i--)
            {
                var sh = arrayProp.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
                if (sh == null) continue;
                if (sh.name == "Universal Render Pipeline/Lit" ||
                    sh.name == "Universal Render Pipeline/Unlit" ||
                    sh.name.StartsWith("TrashCleaner3D/"))
                {
                    arrayProp.DeleteArrayElementAtIndex(i);
                    changed = true;
                    Debug.Log($"[WebGLBuildTool] Removed from AlwaysIncludedShaders: {sh.name}");
                }
            }

            if (changed)
            {
                serialized.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
            }
        }

        private static void EnsureKeepMaterialsInResources()
        {
            Directory.CreateDirectory(KeepMatDir);
            for (int i = 0; i < KeepRuntimeShaders.Length; i++)
            {
                string shaderName = KeepRuntimeShaders[i];
                Shader sh = Shader.Find(shaderName);
                if (sh == null) continue;

                string safeName = shaderName.Replace("/", "_").Replace(" ", "_");
                string matPath = $"{KeepMatDir}/Keep_{safeName}.mat";
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    mat = new Material(sh);
                    mat.enableInstancing = true;
                    AssetDatabase.CreateAsset(mat, matPath);
                }
                else
                {
                    mat.shader = sh;
                    mat.enableInstancing = true;
                    EditorUtility.SetDirty(mat);
                }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
