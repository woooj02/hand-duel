using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace ShowOfHands.EditorTools
{
    public static class BorrowBenchmarkBuild
    {
        public static void Build()
        {
            var platform=NamedBuildTarget.Android;string define=PlayerSettings.GetScriptingDefineSymbols(platform);
            string id=PlayerSettings.GetApplicationIdentifier(platform),name=PlayerSettings.productName;
            try{
                PlayerSettings.SetScriptingDefineSymbols(platform,define+";BORROWLIGHT_BENCHMARK");
                PlayerSettings.SetApplicationIdentifier(platform,"com.keepsrunning.handduelbenchmark");PlayerSettings.productName="Hand Duel Performance Test";
                Directory.CreateDirectory("Builds");var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/ShowOfHands/Scenes/Main.unity"},locationPathName="Builds/Borrowlight-benchmark.apk",target=BuildTarget.Android,options=BuildOptions.None});
                if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Benchmark APK build failed");
            }finally{PlayerSettings.SetScriptingDefineSymbols(platform,define);PlayerSettings.SetApplicationIdentifier(platform,id);PlayerSettings.productName=name;AssetDatabase.SaveAssets();}
        }
    }
}
