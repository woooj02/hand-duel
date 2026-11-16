using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
namespace ShowOfHands.EditorTools
{
    public static class InputDiagnosticBuild
    {
        public static void Build()
        {
            var platform=NamedBuildTarget.Android;
            string oldDefines=PlayerSettings.GetScriptingDefineSymbols(platform);
            string oldVersion=PlayerSettings.bundleVersion;
            int oldCode=PlayerSettings.Android.bundleVersionCode;
            try
            {
                PlayerSettings.SetScriptingDefineSymbols(platform,oldDefines+";SOH_INPUT_DIAGNOSTICS");
                PlayerSettings.bundleVersion="0.5.5";
                PlayerSettings.Android.bundleVersionCode=16;
                Directory.CreateDirectory("Builds");
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
                    scenes=new[]{ProjectSetup.ScenePath},locationPathName="Builds/Hand-Duel-input-diagnostic.apk",
                    target=BuildTarget.Android,options=BuildOptions.None});
                if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Input diagnostic build failed");
            }
            finally
            {
                PlayerSettings.SetScriptingDefineSymbols(platform,oldDefines);
                PlayerSettings.bundleVersion=oldVersion;
                PlayerSettings.Android.bundleVersionCode=oldCode;
                AssetDatabase.SaveAssets();
            }
        }
    }
}
