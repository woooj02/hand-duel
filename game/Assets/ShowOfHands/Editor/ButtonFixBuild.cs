using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
namespace ShowOfHands.EditorTools
{
    public static class ButtonFixBuild
    {
        public static void Build()
        {
            var target=NamedBuildTarget.Android;
            var defines=PlayerSettings.GetScriptingDefineSymbols(target).Split(';').Where(x=>x!="SOH_DEVELOPMENT"&&x!="SOH_INPUT_DIAGNOSTICS"&&x!="BORROWLIGHT_BENCHMARK");
            PlayerSettings.SetScriptingDefineSymbols(target,string.Join(";",defines));
            PlayerSettings.bundleVersion=ProjectSetup.BuildVersion;
            PlayerSettings.Android.bundleVersionCode=17;
            AssetDatabase.SaveAssets();
            ProjectSetup.BuildApk();
        }
    }
}
