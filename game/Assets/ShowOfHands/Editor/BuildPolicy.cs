using System;
using System.IO;
using System.Linq;
using System.Xml;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace ShowOfHands.EditorTools
{
    public sealed class BuildPolicy:IPreprocessBuildWithReport
    {
        public int callbackOrder=>-1000;
        public void OnPreprocessBuild(BuildReport report)
        {
            bool dev=PlayerSettings.GetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.Android).Split(';').Contains("SOH_DEVELOPMENT");
            var path="Assets/Plugins/Android/AndroidManifest.xml";var doc=new XmlDocument();doc.Load(path);
            const string android="http://schemas.android.com/apk/res/android",tools="http://schemas.android.com/tools";
            var app=(XmlElement)doc.SelectSingleNode("/manifest/application");app.SetAttribute("usesCleartextTraffic",android,dev?"true":"false");
            foreach(string name in new[]{"android.permission.RECORD_AUDIO","android.permission.MODIFY_AUDIO_SETTINGS","android.permission.BLUETOOTH","com.oculus.permission.USE_ANCHOR_API"})
            {
                XmlElement found=null;foreach(XmlElement e in doc.SelectNodes("/manifest/uses-permission"))if(e.GetAttribute("name",android)==name)found=e;
                if(found==null){found=doc.CreateElement("uses-permission");found.SetAttribute("name",android,name);doc.DocumentElement.AppendChild(found);}found.SetAttribute("node",tools,"remove");
            }
            doc.Save(path);
            if(!dev)
            {
                var arena=File.ReadAllText("Assets/StreamingAssets/arena.txt").Trim();
                if(!PublicHttps(arena))throw new BuildFailedException("Release requires a public HTTPS companion in arena.txt.");
                // Legacy charades can deliberately remain offline. Never auto-upload old local clips to a new service.
                var clips=File.ReadAllText("Assets/StreamingAssets/server.txt").Trim();
                if(clips.Length>0&&!PublicHttps(clips))throw new BuildFailedException("An enabled release clip service must use public HTTPS; leave server.txt empty for offline charades.");
            }
        }
        static bool PublicHttps(string value)=>Uri.TryCreate(value,UriKind.Absolute,out var uri)&&uri.Scheme==Uri.UriSchemeHttps&&!uri.IsLoopback;
    }
}
