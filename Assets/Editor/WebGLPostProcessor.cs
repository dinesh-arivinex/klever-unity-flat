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
        if (!File.Exists(indexPath))
        {
            return;
        }

        string html = File.ReadAllText(indexPath);
        bool modified = false;

        // Fix 1: Manual synchronization of Unity Application.persistentDataPath via JS_FileSystem_Sync() is deprecated
        if (!html.Contains("autoSyncPersistentDataPath"))
        {
            html = Regex.Replace(html, @"var\s+config\s*=\s*\{", "var config = {\n        autoSyncPersistentDataPath: true,");
            modified = true;
            Debug.Log("[WebGLPostProcessor] Injected autoSyncPersistentDataPath = true into index.html.");
        }

        // Fix 2: Expose unityInstance globally to window.unityInstance for audio context management and plugins
        if (!html.Contains("window.unityInstance = unityInstance"))
        {
            html = Regex.Replace(
                html,
                @"createUnityInstance\(canvas,\s*config,\s*\(progress\)\s*=>\s*\{[\s\S]*?\}\)\.then\(\(unityInstance\)\s*=>\s*\{",
                match => match.Value + "\n                window.unityInstance = unityInstance;"
            );
            modified = true;
            Debug.Log("[WebGLPostProcessor] Injected window.unityInstance assignment into index.html.");
        }

        // Fix 3: Inject global user-gesture AudioContext resume listener
        if (!html.Contains("klever-audio-unlock"))
        {
            string unlockScript =
@"    <script id=""klever-audio-unlock"">
      (function() {
        function resumeAllAudio() {
          try {
            if (typeof WEBAudio !== 'undefined' && WEBAudio.audioContext && WEBAudio.audioContext.state === 'suspended') {
              WEBAudio.audioContext.resume();
            }
            if (window.unityInstance && window.unityInstance.Module && window.unityInstance.Module.WEBAudio && window.unityInstance.Module.WEBAudio.audioContext) {
              if (window.unityInstance.Module.WEBAudio.audioContext.state === 'suspended') {
                window.unityInstance.Module.WEBAudio.audioContext.resume();
              }
            }
            if (window.audioContext && window.audioContext.state === 'suspended') {
              window.audioContext.resume();
            }
          } catch(e) {}
        }
        var events = ['pointerdown', 'mousedown', 'touchstart', 'touchend', 'keydown'];
        events.forEach(function(evt) {
          window.addEventListener(evt, resumeAllAudio, { passive: true });
          document.addEventListener(evt, resumeAllAudio, { passive: true });
        });
      })();
    </script>
  </body>";
            html = html.Replace("</body>", unlockScript);
            modified = true;
            Debug.Log("[WebGLPostProcessor] Injected AudioContext resume listener into index.html.");
        }

        if (modified)
        {
            File.WriteAllText(indexPath, html);
        }
    }
}
