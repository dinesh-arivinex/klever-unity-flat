using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Callbacks;
#if UNITY_2018_1_OR_NEWER
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
#endif
using UnityEngine;

public class WebGLPostProcessor
#if UNITY_2018_1_OR_NEWER
    : IPostprocessBuildWithReport
#endif
{
    public int callbackOrder => 0;

#if UNITY_2018_1_OR_NEWER
    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform == BuildTarget.WebGL)
        {
            FixIndexHtml(report.summary.outputPath);
        }
    }
#else
    [PostProcessBuild]
    public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target == BuildTarget.WebGL)
        {
            FixIndexHtml(pathToBuiltProject);
        }
    }
#endif

    private static void FixIndexHtml(string buildPath)
    {
        string indexPath = Path.Combine(buildPath, "index.html");
        if (File.Exists(indexPath))
        {
            string html = File.ReadAllText(indexPath);
            
            // Fix: Manual synchronization of Unity Application.persistentDataPath via JS_FileSystem_Sync() is deprecated
            if (!html.Contains("autoSyncPersistentDataPath"))
            {
                // Inject autoSyncPersistentDataPath: true into the configuration object
                html = Regex.Replace(html, @"var\s+config\s*=\s*\{", "var config = {\n        autoSyncPersistentDataPath: true,");
                File.WriteAllText(indexPath, html);
                Debug.Log("[WebGLPostProcessor] Injected autoSyncPersistentDataPath = true into index.html to fix deprecation warning.");
            }
        }
    }
}
