using BallsOut.Publishing;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
#if UNITY_IOS
using System.IO;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
#endif

namespace BallsOut.Publishing.Editor
{
    public sealed class CpiBuildProcessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => 100;

        public void OnPreprocessBuild(BuildReport report)
        {
            var settings = CpiProjectSetup.EnsureSettings();
            if (settings.EnableAppsFlyer && string.IsNullOrWhiteSpace(settings.AppsFlyerDevKey))
                throw new BuildFailedException("Flick CPI: AppsFlyer Dev Key is missing.");

            if (report.summary.platform == BuildTarget.iOS && settings.EnableAppsFlyer &&
                string.IsNullOrWhiteSpace(settings.IosAppId))
                throw new BuildFailedException("Flick CPI: enter the numeric Apple App Store ID in Publisher Settings.");
        }

#if UNITY_IOS
        [PostProcessBuild(999)]
        public static void ConfigureIos(BuildTarget target, string outputPath)
        {
            if (target != BuildTarget.iOS) return;
            var settings = CpiProjectSetup.EnsureSettings();
            string plistPath = Path.Combine(outputPath, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.SetString("NSUserTrackingUsageDescription", settings.TrackingUsageDescription);
            plist.root.SetString("NSAdvertisingAttributionReportEndpoint", "https://appsflyer-skadnetwork.com/");
            plist.WriteToFile(plistPath);

            string projectPath = PBXProject.GetPBXProjectPath(outputPath);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);
            project.AddFrameworkToProject(project.GetUnityFrameworkTargetGuid(), "AppTrackingTransparency.framework", true);
            project.WriteToFile(projectPath);
        }
#endif
    }
}
