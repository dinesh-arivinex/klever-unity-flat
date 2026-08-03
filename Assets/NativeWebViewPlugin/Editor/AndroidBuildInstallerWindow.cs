#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace NativeWebView.Editor
{
    public class AndroidBuildInstallerWindow : EditorWindow
    {
        private const string PrefBuildFolderKey = "NativeWebView.AndroidBuildInstaller.BuildFolder";
        private const string PrefBuildFileNameKey = "NativeWebView.AndroidBuildInstaller.BuildFileName";
        private const string PrefInstallFileNameKey = "NativeWebView.AndroidBuildInstaller.InstallFileName";
        private const string PrefInstallNameLinkedKey = "NativeWebView.AndroidBuildInstaller.InstallNameLinked";

        private string buildFolderPath = string.Empty;
        private string buildFileName = string.Empty;
        private string installFileName = string.Empty;
        private bool installNameLinked = true;

        private readonly List<AndroidDeviceInfo> devices = new List<AndroidDeviceInfo>();
        private int selectedDeviceIndex = -1;

        private string statusMessage = string.Empty;
        private string commandOutput = string.Empty;

        private Vector2 deviceScroll;
        private Vector2 outputScroll;

        [MenuItem("Tools/Native WebView/Android Build Installer")]
        private static void OpenWindow()
        {
            var window = GetWindow<AndroidBuildInstallerWindow>("Android Build Installer");
            window.minSize = new Vector2(560, 520);
            window.Show();
        }

        private void OnEnable()
        {
            buildFolderPath = EditorPrefs.GetString(PrefBuildFolderKey, GetDefaultBuildFolder());
            buildFileName = EditorPrefs.GetString(PrefBuildFileNameKey, GetDefaultBuildFileName());

            installNameLinked = EditorPrefs.GetBool(PrefInstallNameLinkedKey, true);
            var savedInstallName = EditorPrefs.GetString(PrefInstallFileNameKey, string.Empty);

            if (string.IsNullOrWhiteSpace(savedInstallName) || installNameLinked)
            {
                installFileName = buildFileName;
                installNameLinked = true;
            }
            else
            {
                installFileName = savedInstallName;
            }

            if (string.IsNullOrWhiteSpace(buildFolderPath))
            {
                buildFolderPath = GetDefaultBuildFolder();
            }

            if (string.IsNullOrWhiteSpace(buildFileName))
            {
                buildFileName = GetDefaultBuildFileName();
            }

            if (string.IsNullOrWhiteSpace(installFileName))
            {
                installFileName = buildFileName;
                installNameLinked = true;
            }

            RefreshDevices();
        }

        private void OnDisable()
        {
            EditorPrefs.SetString(PrefBuildFolderKey, buildFolderPath);
            EditorPrefs.SetString(PrefBuildFileNameKey, buildFileName);
            EditorPrefs.SetString(PrefInstallFileNameKey, installFileName);
            EditorPrefs.SetBool(PrefInstallNameLinkedKey, installNameLinked);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Build", EditorStyles.boldLabel);
            DrawBuildSection();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Install", EditorStyles.boldLabel);
            DrawInstallSection();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            DrawOutputSection();
        }

        private void DrawBuildSection()
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Folder", EditorStyles.miniBoldLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    buildFolderPath = EditorGUILayout.TextField(buildFolderPath);

                    if (GUILayout.Button("Select Folder", GUILayout.Width(110)))
                    {
                        var selectedFolder = EditorUtility.OpenFolderPanel("Select Android Build Folder",
                            buildFolderPath, string.Empty);
                        if (!string.IsNullOrWhiteSpace(selectedFolder))
                        {
                            buildFolderPath = selectedFolder;
                        }
                    }
                }

                EditorGUILayout.Space(4);

                EditorGUI.BeginChangeCheck();
                var newBuildFileName = EditorGUILayout.TextField("Build File Name", buildFileName);
                if (EditorGUI.EndChangeCheck())
                {
                    buildFileName = newBuildFileName;
                    if (installNameLinked)
                    {
                        installFileName = buildFileName;
                    }
                }

                EditorGUILayout.Space(6);

                if (GUILayout.Button("Build APK", GUILayout.Height(28)))
                {
                    BuildApk();
                }
            }
        }

        private void DrawInstallSection()
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("Devices", EditorStyles.miniBoldLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Refresh Devices", GUILayout.Width(120)))
                    {
                        RefreshDevices();
                    }

                    EditorGUILayout.LabelField("Devices from USB and adb over Wi-Fi are listed.",
                        EditorStyles.miniLabel);
                }

                deviceScroll = EditorGUILayout.BeginScrollView(deviceScroll, GUILayout.Height(80));
                if (devices.Count == 0)
                {
                    EditorGUILayout.HelpBox(
                        "No Android devices detected. Connect via USB or adb over Wi-Fi and refresh.",
                        MessageType.Info);
                }
                else
                {
                    var options = devices.Select(d => d.DisplayName).ToArray();
                    selectedDeviceIndex = EditorGUILayout.Popup("Target Device",
                        Mathf.Clamp(selectedDeviceIndex, 0, options.Length - 1), options);

                    if (selectedDeviceIndex >= 0 && selectedDeviceIndex < devices.Count &&
                        !devices[selectedDeviceIndex].IsOnline)
                    {
                        EditorGUILayout.HelpBox(
                            string.Format(
                                "Selected device is '{0}'. Device must be in 'device' state before installing.",
                                devices[selectedDeviceIndex].State),
                            MessageType.Warning);
                    }
                }

                EditorGUILayout.EndScrollView();

                EditorGUILayout.Space(4);

                EditorGUI.BeginChangeCheck();
                var newInstallName = EditorGUILayout.TextField("Install File Name", installFileName);
                if (EditorGUI.EndChangeCheck())
                {
                    installFileName = newInstallName;
                    installNameLinked = AreFileNamesEquivalent(installFileName, buildFileName);
                }

                EditorGUILayout.LabelField("Install path",
                    Path.Combine(buildFolderPath, EnsureApkFileName(installFileName)), EditorStyles.miniLabel);

                EditorGUILayout.Space(6);

                var canInstall = devices.Count > 0 &&
                                 selectedDeviceIndex >= 0 &&
                                 selectedDeviceIndex < devices.Count &&
                                 devices[selectedDeviceIndex].IsOnline;
                GUI.enabled = canInstall;
                if (GUILayout.Button("Install APK", GUILayout.Height(28)))
                {
                    InstallApkOnSelectedDevice();
                }

                GUI.enabled = true;
            }
        }

        private void DrawOutputSection()
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.HelpBox(
                    string.IsNullOrWhiteSpace(statusMessage) ? "No actions executed yet." : statusMessage,
                    MessageType.None);

                outputScroll = EditorGUILayout.BeginScrollView(outputScroll, GUILayout.MinHeight(140));
                EditorGUILayout.TextArea(commandOutput, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
            }
        }

        private void BuildApk()
        {
            if (!TryGetBuildOutputPath(out var outputPath))
            {
                return;
            }

            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (scenes.Length == 0)
            {
                EditorUtility.DisplayDialog("No Scenes", "No enabled scenes found in Build Settings.", "OK");
                SetStatus("Build aborted: no enabled scenes found.");
                return;
            }

            if (!SwitchToAndroidBuildTarget())
            {
                SetStatus("Build aborted: failed to switch build target to Android.");
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? buildFolderPath);

            var previousBuildAppBundle = EditorUserBuildSettings.buildAppBundle;
            try
            {
                EditorUserBuildSettings.buildAppBundle = false;

                var buildOptions = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = outputPath,
                    target = BuildTarget.Android,
                    options = BuildOptions.None
                };

                var report = BuildPipeline.BuildPlayer(buildOptions);
                var summary = report.summary;

                if (summary.result == BuildResult.Succeeded)
                {
                    var message =
                        $"Build succeeded.\nAPK: {outputPath}\nSize: {summary.totalSize / (1024f * 1024f):F2} MB\nTime: {summary.totalTime}";
                    SetStatus(message);
                    commandOutput = message;
                    UnityEngine.Debug.Log(message);
                }
                else
                {
                    var message =
                        $"Build failed. Result: {summary.result}.\nErrors: {summary.totalErrors}\nWarnings: {summary.totalWarnings}";
                    SetStatus(message);
                    commandOutput = message;
                    UnityEngine.Debug.LogError(message);
                }
            }
            finally
            {
                EditorUserBuildSettings.buildAppBundle = previousBuildAppBundle;
            }
        }

        private void InstallApkOnSelectedDevice()
        {
            if (devices.Count == 0 || selectedDeviceIndex < 0 || selectedDeviceIndex >= devices.Count)
            {
                SetStatus("Install aborted: no target device selected.");
                return;
            }

            if (!TryGetInstallApkPath(out var apkPath))
            {
                return;
            }

            var device = devices[selectedDeviceIndex];
            if (!device.IsOnline)
            {
                SetStatus($"Install aborted: selected device state is '{device.State}', expected 'device'.");
                return;
            }

            var installArgs = $"-s {QuoteArgument(device.Serial)} install -r {QuoteArgument(apkPath)}";
            if (!TryRunAdbCommand(installArgs, out var stdout, out var stderr, out var exitCode))
            {
                var message = $"Install failed: unable to start adb.\n{stderr}";
                SetStatus(message);
                commandOutput = message;
                return;
            }

            var outputBuilder = new StringBuilder();
            outputBuilder.AppendLine($"adb {installArgs}");
            if (!string.IsNullOrWhiteSpace(stdout))
            {
                outputBuilder.AppendLine(stdout.TrimEnd());
            }

            if (!string.IsNullOrWhiteSpace(stderr))
            {
                outputBuilder.AppendLine(stderr.TrimEnd());
            }

            commandOutput = outputBuilder.ToString().TrimEnd();

            if (exitCode == 0)
            {
                SetStatus($"Install completed on {device.DisplayName}.");
            }
            else
            {
                SetStatus($"Install failed on {device.DisplayName}. Exit code: {exitCode}");
            }
        }

        private void RefreshDevices()
        {
            const string args = "devices -l";

            if (!TryRunAdbCommand(args, out var stdout, out var stderr, out _))
            {
                devices.Clear();
                selectedDeviceIndex = -1;

                var message = $"Failed to run adb.\n{stderr}";
                SetStatus(message);
                commandOutput = message;
                return;
            }

            var parsedDevices = ParseDevices(stdout);
            devices.Clear();
            devices.AddRange(parsedDevices);

            selectedDeviceIndex = devices.Count > 0 ? Mathf.Clamp(selectedDeviceIndex, 0, devices.Count - 1) : -1;

            var onlineCount = devices.Count(d => d.IsOnline);
            var status = devices.Count > 0
                ? $"Found {devices.Count} device(s). Online: {onlineCount}."
                : "No Android devices detected.";

            SetStatus(status);

            var outputBuilder = new StringBuilder();
            outputBuilder.AppendLine("adb devices -l");
            outputBuilder.AppendLine(stdout.TrimEnd());
            if (!string.IsNullOrWhiteSpace(stderr))
            {
                outputBuilder.AppendLine(stderr.TrimEnd());
            }

            commandOutput = outputBuilder.ToString().TrimEnd();
        }

        private bool TryGetBuildOutputPath(out string outputPath)
        {
            outputPath = string.Empty;

            if (string.IsNullOrWhiteSpace(buildFolderPath))
            {
                EditorUtility.DisplayDialog("Missing Folder", "Please select a build folder.", "OK");
                SetStatus("Build aborted: build folder is missing.");
                return false;
            }

            var normalizedBuildName = EnsureApkFileName(buildFileName);
            if (string.IsNullOrWhiteSpace(normalizedBuildName))
            {
                EditorUtility.DisplayDialog("Missing File Name", "Please provide a build file name.", "OK");
                SetStatus("Build aborted: build file name is missing.");
                return false;
            }

            buildFileName = Path.GetFileNameWithoutExtension(normalizedBuildName);
            if (installNameLinked)
            {
                installFileName = buildFileName;
            }

            outputPath = Path.Combine(buildFolderPath, normalizedBuildName);
            return true;
        }

        private bool TryGetInstallApkPath(out string apkPath)
        {
            apkPath = string.Empty;

            if (string.IsNullOrWhiteSpace(buildFolderPath))
            {
                SetStatus("Install aborted: build folder is missing.");
                return false;
            }

            var normalizedInstallName = EnsureApkFileName(installFileName);
            if (string.IsNullOrWhiteSpace(normalizedInstallName))
            {
                SetStatus("Install aborted: install file name is missing.");
                return false;
            }

            apkPath = Path.Combine(buildFolderPath, normalizedInstallName);
            if (!File.Exists(apkPath))
            {
                SetStatus($"Install aborted: file not found at '{apkPath}'.");
                return false;
            }

            return true;
        }

        private static List<AndroidDeviceInfo> ParseDevices(string adbOutput)
        {
            var parsedDevices = new List<AndroidDeviceInfo>();
            if (string.IsNullOrWhiteSpace(adbOutput))
            {
                return parsedDevices;
            }

            var lines = adbOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("List of devices attached", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (line.StartsWith("*", StringComparison.Ordinal))
                {
                    continue;
                }

                var columns = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (columns.Length < 2)
                {
                    continue;
                }

                var serial = columns[0];
                var state = columns[1];
                string model = string.Empty;
                for (var i = 2; i < columns.Length; i++)
                {
                    const string modelPrefix = "model:";
                    if (columns[i].StartsWith(modelPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        model = columns[i].Substring(modelPrefix.Length).Replace('_', ' ');
                        break;
                    }
                }

                parsedDevices.Add(new AndroidDeviceInfo(serial, model, state));
            }

            return parsedDevices;
        }

        private static bool TryRunAdbCommand(string arguments, out string stdout, out string stderr, out int exitCode)
        {
            var adbExecutable = ResolveAdbExecutable();

            var startInfo = new ProcessStartInfo
            {
                FileName = adbExecutable,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                using (var process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        stdout = string.Empty;
                        stderr = "Unable to start adb process.";
                        exitCode = -1;
                        return false;
                    }

                    stdout = process.StandardOutput.ReadToEnd();
                    stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    exitCode = process.ExitCode;
                    return true;
                }
            }
            catch (Exception ex)
            {
                stdout = string.Empty;
                stderr = ex.Message;
                exitCode = -1;
                return false;
            }
        }

        private static string ResolveAdbExecutable()
        {
            var adbFileName = Application.platform == RuntimePlatform.WindowsEditor ? "adb.exe" : "adb";

            try
            {
                var sdkRootPath = AndroidExternalToolsSettings.sdkRootPath;
                if (!string.IsNullOrWhiteSpace(sdkRootPath))
                {
                    var sdkAdbPath = Path.Combine(sdkRootPath, "platform-tools", adbFileName);
                    if (File.Exists(sdkAdbPath))
                    {
                        return sdkAdbPath;
                    }
                }
            }
            catch
            {
                // Fall through to PATH resolution.
            }

            return adbFileName;
        }

        private static bool SwitchToAndroidBuildTarget()
        {
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
            {
                return true;
            }

            return EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        }

        private static string GetDefaultBuildFolder()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.Combine(projectRoot, "Builds", "Android");
        }

        private static string GetDefaultBuildFileName()
        {
            var defaultName = string.IsNullOrWhiteSpace(PlayerSettings.productName)
                ? "app"
                : PlayerSettings.productName;
            return SanitizeFileStem(defaultName);
        }

        private static string EnsureApkFileName(string rawName)
        {
            var sanitized = SanitizeFileStem(rawName);
            if (string.IsNullOrWhiteSpace(sanitized))
            {
                return string.Empty;
            }

            return sanitized + ".apk";
        }

        private static string SanitizeFileStem(string value)
        {
            var fallbackName = "app";
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallbackName;
            }

            var trimmed = value.Trim();
            if (trimmed.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = Path.GetFileNameWithoutExtension(trimmed);
            }

            var invalidCharacters = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(trimmed.Length);
            foreach (var ch in trimmed)
            {
                builder.Append(invalidCharacters.Contains(ch) ? '_' : ch);
            }

            var result = builder.ToString().Trim();
            return string.IsNullOrWhiteSpace(result) ? fallbackName : result;
        }

        private static bool AreFileNamesEquivalent(string left, string right)
        {
            var leftName = EnsureApkFileName(left);
            var rightName = EnsureApkFileName(right);
            return string.Equals(leftName, rightName, StringComparison.OrdinalIgnoreCase);
        }

        private static string QuoteArgument(string argument)
        {
            return string.Format("\"{0}\"", argument.Replace("\"", "\\\""));
        }

        private void SetStatus(string message)
        {
            statusMessage = message;
            Repaint();
        }

        private sealed class AndroidDeviceInfo
        {
            public string Serial { get; }
            public string Model { get; }
            public string State { get; }
            public bool IsOnline => State.Equals("device", StringComparison.OrdinalIgnoreCase);

            public string DisplayName
            {
                get
                {
                    var targetName = string.IsNullOrWhiteSpace(Model)
                        ? Serial
                        : string.Format("{0} ({1})", Serial, Model);
                    return string.Format("{0} - {1}", targetName, State);
                }
            }

            public AndroidDeviceInfo(string serial, string model, string state)
            {
                Serial = serial;
                Model = model;
                State = state;
            }
        }
    }
}

#endif