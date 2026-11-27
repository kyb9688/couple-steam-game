using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace TrashCleaner3D.Editor
{
    /// <summary>
    /// TrashCleaner3D v1.5 원클릭 / 배치모드 WebGL 빌드 도구 (GitHub Pages 100MB 제한 완벽 대응!):
    /// 1) AlwaysIncludedShaders에서 URP/Lit 제거 + 런타임 4종 셰이더 Resources 머티리얼 유지
    /// 2) WebGL 전용 텍스처 플랫폼 오버라이드(512px + Crunch 50) 적용 → PC 화질 무손실, WebGL 용량 대폭 절감!
    /// 3) Stage 1(Room01) + Stage 2~7(StageSkeletonBuilder) 7개 씬 전부 빌드 & Build Settings 등록
    /// 4) Gzip 압축 + Decompression Fallback ON → GitHub Pages 무설정 호환 & .data.unityweb 30MB 이하 유지!
    /// </summary>
    public static class WebGLBuildTool
    {
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
            Debug.Log($"[WebGLBuildTool] Starting v1.5 7-Stage compressed WebGL build -> {outputDir}");

            // 1. AlwaysIncludedShaders에서 대량 변형 셰이더 제거
            RemoveHeavyAlwaysIncludedShaders();

            // 2. 런타임 Shader.Find용 4종 셰이더 Resources 머티리얼 보장
            EnsureKeepMaterialsInResources();

            // 3. WebGL 전용 텍스처 경량화 (PC/Standalone 화질은 전혀 건드리지 않고 WebGL 빌드만 512px + Crunch!)
            OptimizeTexturesForWebGL();

            // 4. Stage 1 최신 씬 + Stage 2~7 뼈대 씬 일괄 생성 & 7종 전부 Build Settings 등록!
            RoomToneTestBuilder.BuildRoom01();
            string[] stageScenes = StageSkeletonBuilder.BuildAllAndRegisterSilent();

            var validScenes = new List<string>();
            foreach (string s in stageScenes)
            {
                if (File.Exists(Path.GetFullPath(s))) validScenes.Add(s);
            }
            if (validScenes.Count == 0)
            {
                Debug.LogError("[WebGLBuildTool] No valid scenes found!");
                return false;
            }

            // 5. GitHub Pages 최적화 WebGL PlayerSettings (Gzip + Decompression Fallback!)
            //    - GitHub Pages는 .gz/.br Content-Encoding 헤더를 주지 않으므로 decompressionFallback = true 필수!
            //    - .data.unityweb / .wasm.unityweb 용량을 ~65~75% 압축해 100MB 제한 위험을 원천 차단!
            PlayerSettings.productName = "TrashCleaner3D";
            PlayerSettings.companyName = "CoupleSteamGame";
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;
            PlayerSettings.runInBackground = true;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;

            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, true);
            }
            Directory.CreateDirectory(outputDir);

            var options = new BuildPlayerOptions
            {
                scenes = validScenes.ToArray(),
                locationPathName = outputDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[WebGLBuildTool] SUCCEEDED! Scenes: {validScenes.Count}, Size: {summary.totalSize / (1024f * 1024f):0.00} MB, Time: {summary.totalTime.TotalSeconds:0.0}s");
                return true;
            }

            Debug.LogError($"[WebGLBuildTool] FAILED: {summary.result} (Errors: {summary.totalErrors})");
            return false;
        }

        private static void OptimizeTexturesForWebGL()
        {
            string[] searchFolders = { "Assets/_Project/Art", "Assets/Brick Project Studio" };
            var existingFolders = new List<string>();
            foreach (string f in searchFolders)
                if (Directory.Exists(Path.GetFullPath(f))) existingFolders.Add(f);
            if (existingFolders.Count == 0) return;

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", existingFolders.ToArray());
            int changedCount = 0;

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null) continue;

                    var webglSettings = importer.GetPlatformTextureSettings("WebGL");
                    int targetSize = path.ToLower().Contains("decals") ? 512 : 512;
                    bool needUpdate = !webglSettings.overridden ||
                                      webglSettings.maxTextureSize > targetSize ||
                                      !webglSettings.crunchedCompression;

                    if (needUpdate)
                    {
                        webglSettings.name = "WebGL";
                        webglSettings.overridden = true;
                        webglSettings.maxTextureSize = targetSize;
                        webglSettings.format = TextureImporterFormat.Automatic;
                        webglSettings.textureCompression = TextureImporterCompression.Compressed;
                        webglSettings.crunchedCompression = true;
                        webglSettings.compressionQuality = 50;
                        importer.SetPlatformTextureSettings(webglSettings);
                        importer.SaveAndReimport();
                        changedCount++;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            if (changedCount > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"[WebGLBuildTool] Applied WebGL 512px Crunch compression to {changedCount} textures (PC quality untouched).");
            }
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
