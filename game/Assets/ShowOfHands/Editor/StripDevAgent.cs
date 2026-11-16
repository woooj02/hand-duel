using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ShowOfHands.EditorTools {
 // The SDK's order-1 build callback recreates this Resource and injects a local connection token,
 // even when its assistant is disabled. Run after that callback, and clean up after SDK restoration.
 public sealed class StripDevAgent:IPreprocessBuildWithReport,IPostprocessBuildWithReport {
  const string AssetPath="Assets/Resources/DevAgentSettings.asset";
  public int callbackOrder=>int.MaxValue;
  public void OnPreprocessBuild(BuildReport report){Strip();}
  public void OnPostprocessBuild(BuildReport report){Strip();}
  static void Strip(){
   if(File.Exists(AssetPath)&&!AssetDatabase.DeleteAsset(AssetPath))
    throw new BuildFailedException("Could not exclude SDK debug-agent settings from the build.");
   if(File.Exists(AssetPath))throw new BuildFailedException("SDK debug-agent settings must not ship.");
   Debug.Log("SOH_POLICY excluded DevAgentSettings (no debug connection token is shipped).");
  }
 }
}
