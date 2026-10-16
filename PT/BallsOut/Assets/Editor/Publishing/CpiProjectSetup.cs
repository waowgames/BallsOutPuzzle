using System;
using System.IO;
using BallsOut.Publishing;
using UnityEditor;
using UnityEngine;

namespace BallsOut.Publishing.Editor
{
    public static class CpiProjectSetup
    {
        public const string SettingsPath = "Assets/Resources/Publishing/CpiAnalyticsSettings.asset";

        [InitializeOnLoadMethod]
        private static void PrepareMetaEditorTools()
        {
#if UNITY_EDITOR_WIN
            // Meta uses OpenSSL to calculate Android key hashes. Reuse an installed Git copy
            // in this Editor process only; never modify the user's system or user PATH.
            string path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (string directory in path.Split(Path.PathSeparator))
                if (File.Exists(Path.Combine(directory.Trim('"'), "openssl.exe"))) return;

            string[] candidates =
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "usr", "bin"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Git", "usr", "bin")
            };
            foreach (string directory in candidates)
            {
                if (!File.Exists(Path.Combine(directory, "openssl.exe"))) continue;
                Environment.SetEnvironmentVariable("PATH", path + Path.PathSeparator + directory, EnvironmentVariableTarget.Process);
                return;
            }
#endif
        }

        [MenuItem("Tools/Flick CPI/Select Publisher Settings")]
        public static void SelectSettings() => Selection.activeObject = EnsureSettings();

        public static CpiAnalyticsSettings EnsureSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<CpiAnalyticsSettings>(SettingsPath);
            if (settings != null)
                return settings;

            Directory.CreateDirectory("Assets/Resources/Publishing");
            AssetDatabase.Refresh();
            settings = ScriptableObject.CreateInstance<CpiAnalyticsSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        public static void PrepareGameAnalytics()
        {
            // Never copy the old TinySauce credentials. These are the current GA SDK's own settings.
            var settings = GameAnalyticsSDK.GameAnalytics.SettingsGA;
            if (!settings.Platforms.Contains(RuntimePlatform.Android))
                settings.AddPlatform(RuntimePlatform.Android);
            if (!settings.Platforms.Contains(RuntimePlatform.IPhonePlayer))
                settings.AddPlatform(RuntimePlatform.IPhonePlayer);
            settings.InfoLogBuild = false;
            settings.VerboseLogBuild = false;
            settings.EnsureBuildNumberAutoDetectList();
            for (int i = 0; i < settings.Platforms.Count; i++)
                settings.BuildNumberAutoDetect[i] = true;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }
    }
}
