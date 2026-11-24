using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace TrashCleaner3D.Editor
{
    /// <summary>
    /// Unity 프로젝트를 열 때 GitHub(origin/main)에 새 업데이트가 있으면 자동으로 git pull을 실행합니다.
    /// - 에디터 세션당 1회만 실행 (스크립트 리컴파일 시 중복 실행 방지)
    /// - 상단 메뉴 [TrashCleaner > Auto Pull On Open]으로 언제든 켜고 끌 수 있음
    /// - 로컬 수정 중일 때는 --autostash로 안전하게 병합하며, 충돌 시 안내 창 표시
    /// </summary>
    [InitializeOnLoad]
    public static class AutoGitSyncOnOpen
    {
        private const string SessionKey = "TC3D_AutoGitPullDone";
        private const string PrefEnabledKey = "TC3D_AutoGitPullEnabled";
        private const string MenuTogglePath = "TrashCleaner/Auto Pull On Open (Git)";
        private const string MenuSyncNowPath = "TrashCleaner/Sync Latest From GitHub Now";

        static AutoGitSyncOnOpen()
        {
            if (Application.isBatchMode) return;
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);

            if (!EditorPrefs.GetBool(PrefEnabledKey, true)) return;

            EditorApplication.delayCall += () => CheckAndPull(silentIfUpToDate: true);
        }

        [MenuItem(MenuTogglePath)]
        private static void ToggleAutoPull()
        {
            bool next = !EditorPrefs.GetBool(PrefEnabledKey, true);
            EditorPrefs.SetBool(PrefEnabledKey, next);
            Debug.Log($"[AutoGitSync] 프로젝트 열 때 자동 업데이트: {(next ? "ON" : "OFF")}");
        }

        [MenuItem(MenuTogglePath, true)]
        private static bool ToggleAutoPullValidate()
        {
            Menu.SetChecked(MenuTogglePath, EditorPrefs.GetBool(PrefEnabledKey, true));
            return true;
        }

        [MenuItem(MenuSyncNowPath)]
        private static void SyncNowMenu()
        {
            CheckAndPull(silentIfUpToDate: false);
        }

        private static string FindRepoRoot()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            for (int i = 0; i < 4; i++)
            {
                if (Directory.Exists(Path.Combine(dir, ".git")))
                    return dir;
                DirectoryInfo parent = Directory.GetParent(dir);
                if (parent == null) break;
                dir = parent.FullName;
            }
            return null;
        }

        private static void CheckAndPull(bool silentIfUpToDate)
        {
            string repoRoot = FindRepoRoot();
            if (string.IsNullOrEmpty(repoRoot))
            {
                if (!silentIfUpToDate)
                    Debug.LogWarning("[AutoGitSync] .git 폴더를 찾을 수 없습니다.");
                return;
            }

            if (!RunGit(repoRoot, "fetch origin main", 8000, out _, out string fetchErr))
            {
                if (!silentIfUpToDate)
                    Debug.LogWarning($"[AutoGitSync] GitHub 확인 실패 (오프라인이거나 인증 필요): {fetchErr}");
                return;
            }

            RunGit(repoRoot, "rev-parse HEAD", 3000, out string localHead, out _);
            RunGit(repoRoot, "rev-parse origin/main", 3000, out string remoteHead, out _);
            localHead = localHead.Trim();
            remoteHead = remoteHead.Trim();

            if (string.IsNullOrEmpty(remoteHead) || localHead == remoteHead)
            {
                if (!silentIfUpToDate)
                    EditorUtility.DisplayDialog("TrashCleaner3D", "이미 최신 버전입니다!", "확인");
                return;
            }

            RunGit(repoRoot, "rev-list --count HEAD..origin/main", 3000, out string behindStr, out _);
            if (!int.TryParse(behindStr.Trim(), out int behindCount) || behindCount <= 0)
            {
                if (!silentIfUpToDate)
                    EditorUtility.DisplayDialog("TrashCleaner3D", "이미 최신 버전입니다! (내 로컬에 미푸시 커밋이 있습니다.)", "확인");
                return;
            }

            bool ok = RunGit(repoRoot, "pull --autostash origin main", 15000, out string pullOut, out string pullErr);
            if (!ok)
            {
                EditorUtility.DisplayDialog(
                    "GitHub 자동 업데이트 충돌",
                    "최신 버전을 받아오는 중 충돌이 발생했습니다.\nGitHub Desktop 또는 AI에게 충돌 해결을 요청해 주세요.\n\n" + pullErr,
                    "확인"
                );
                return;
            }

            Debug.Log($"[AutoGitSync] 최신 커밋 {behindCount}개를 자동으로 받아왔습니다!\n{pullOut}");
            AssetDatabase.Refresh();

            bool rebuild = EditorUtility.DisplayDialog(
                "최신 버전 자동 업데이트 완료!",
                $"GitHub에서 최신 업데이트 {behindCount}건을 자동으로 받아왔습니다.\n\n지금 바로 [Build Room 01 Playable Prototype]을 새로 빌드할까요?",
                "바로 빌드하기",
                "나중에"
            );

            if (rebuild)
            {
                EditorApplication.delayCall += () =>
                {
                    EditorApplication.ExecuteMenuItem("TrashCleaner/Build Room 01 Playable Prototype");
                };
            }
        }

        private static bool RunGit(string workingDir, string args, int timeoutMs, out string stdout, out string stderr)
        {
            stdout = "";
            stderr = "";
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = args,
                    WorkingDirectory = workingDir,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using (var proc = Process.Start(psi))
                {
                    if (proc == null) return false;
                    stdout = proc.StandardOutput.ReadToEnd();
                    stderr = proc.StandardError.ReadToEnd();
                    if (!proc.WaitForExit(timeoutMs))
                    {
                        try { proc.Kill(); } catch { }
                        stderr = "Timeout";
                        return false;
                    }
                    return proc.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                stderr = ex.Message;
                return false;
            }
        }
    }
}
