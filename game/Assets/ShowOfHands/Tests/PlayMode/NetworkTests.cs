using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace ShowOfHands.Tests {
 public class NetworkTests {
  [UnityTest] public IEnumerator QueuedOfflineClipRecoversUploadsAndDeletes(){
   string storage=Path.Combine(Application.temporaryCachePath,"soh-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(storage);
   var appObject=new GameObject("Network test config");var app=appObject.AddComponent<App>();app.enabled=false;
   var server=typeof(App).GetProperty("ServerBase");server.SetValue(app,"http://127.0.0.1:8788");
   string sample=Path.GetFullPath(Path.Combine(Application.dataPath,"../../contract/samples/wave.clip.json"));var clip=HandClip.FromJson(File.ReadAllText(sample),out _);clip.Created=DateTime.UtcNow.ToString("o");clip.Id=null;File.WriteAllText(Path.Combine(storage,"local-test.clip.json"),clip.ToStoredJson());var mine=new List<HandClip>{clip};
   var offlineObject=new GameObject("Offline client");var offline=offlineObject.AddComponent<ApiClient>();offline.Init(app,new List<HandClip>(),mine,storage);offline.Upload(clip,"local-test");yield return new WaitForSeconds(.3f);Assert.That(Directory.GetFiles(Path.Combine(storage,"upload-queue")).Length,Is.EqualTo(1));UnityEngine.Object.Destroy(offlineObject);yield return null;
   server.SetValue(app,"http://127.0.0.1:8787");var onlineObject=new GameObject("Online client");var online=onlineObject.AddComponent<ApiClient>();online.Init(app,new List<HandClip>(),mine,storage);
   float until=Time.realtimeSinceStartup+20;while(string.IsNullOrEmpty(clip.Id)&&Time.realtimeSinceStartup<until)yield return null;Assert.That(clip.Id,Has.Length.EqualTo(6),"Run npm run local on port 8787 before this integration test.");
   HandClip fetched=null;yield return online.Fetch(clip.Id,c=>fetched=c);Assert.That(fetched,Is.Not.Null);CollectionAssert.AreEqual(clip.Data,fetched.Data);Assert.That(Directory.GetFiles(Path.Combine(storage,"upload-queue")).Length,Is.EqualTo(0));
   bool finished=false,deleted=false;online.DeleteAll(ok=>{deleted=ok;finished=true;});until=Time.realtimeSinceStartup+8;while(!finished&&Time.realtimeSinceStartup<until)yield return null;Assert.That(deleted,Is.True);
   UnityEngine.Object.Destroy(onlineObject);UnityEngine.Object.Destroy(appObject);yield return null;
   Assert.That(Path.GetFullPath(storage).StartsWith(Path.GetFullPath(Application.temporaryCachePath)+Path.DirectorySeparatorChar),Is.True);Directory.Delete(storage,true);
  }
 }
}
