#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEditor.iOS.Xcode.Extensions;
using UnityEngine;

namespace NativeWebView.Editor
{
    internal static class IOSFrameworkPostProcessor
    {
        private const string FrameworkName = "Native_WebView_Framework.framework";
        private static readonly string[] FrameworkProjectPathCandidates =
        {
            $"Frameworks/Plugins/iOS/{FrameworkName}",
            $"Libraries/Plugins/iOS/{FrameworkName}"
        };

        [PostProcessBuild(700)]
        private static void OnPostProcessBuild(BuildTarget buildTarget, string buildPath)
        {
            if (buildTarget != BuildTarget.iOS)
            {
                return;
            }

            var frameworkProjectPath = ResolveFrameworkProjectPath(buildPath);
            if (string.IsNullOrEmpty(frameworkProjectPath))
            {
                Debug.LogWarning(
                    $"[NativeWebView] Could not find {FrameworkName} in generated iOS project. " +
                    "Skipping framework embed step.");
                return;
            }

            var projectPath = PBXProject.GetPBXProjectPath(buildPath);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);

            var mainTargetGuid = project.GetUnityMainTargetGuid();
            var frameworkTargetGuid = project.GetUnityFrameworkTargetGuid();

            var frameworkFileGuid = project.FindFileGuidByProjectPath(frameworkProjectPath);
            if (string.IsNullOrEmpty(frameworkFileGuid))
            {
                frameworkFileGuid = project.AddFile(frameworkProjectPath, frameworkProjectPath, PBXSourceTree.Source);
            }

            project.AddFileToBuild(frameworkTargetGuid, frameworkFileGuid);
            PBXProjectExtensions.AddFileToEmbedFrameworks(project, mainTargetGuid, frameworkFileGuid);

            project.AddBuildProperty(mainTargetGuid, "LD_RUNPATH_SEARCH_PATHS", "$(inherited) @executable_path/Frameworks");
            project.AddBuildProperty(frameworkTargetGuid, "LD_RUNPATH_SEARCH_PATHS", "$(inherited) @loader_path/Frameworks");

            project.WriteToFile(projectPath);
        }

        private static string ResolveFrameworkProjectPath(string buildPath)
        {
            foreach (var candidate in FrameworkProjectPathCandidates)
            {
                var fullPath = Path.Combine(buildPath, candidate);
                if (Directory.Exists(fullPath))
                {
                    return candidate;
                }
            }

            var frameworkDirectories = Directory.GetDirectories(buildPath, FrameworkName, SearchOption.AllDirectories);
            foreach (var frameworkDirectory in frameworkDirectories)
            {
                var relativePath = Path.GetRelativePath(buildPath, frameworkDirectory).Replace("\\", "/");
                if (relativePath.StartsWith("Pods/", System.StringComparison.Ordinal))
                {
                    continue;
                }

                return relativePath;
            }

            return null;
        }
    }
}
#endif
