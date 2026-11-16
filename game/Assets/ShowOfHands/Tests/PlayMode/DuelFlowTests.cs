using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace ShowOfHands.Tests {
 public class DuelFlowTests {
  static BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
  static object Call(App app,string method,params object[] args)=>typeof(App).GetMethod(method,Flags).Invoke(app,args);
  static T Get<T>(App app,string name)=>(T)typeof(App).GetField(name,Flags).GetValue(app);
  class Source:IHandSource {public RpsMove Left=RpsMove.Rock,Right=RpsMove.Rock;readonly Transform head;public Source(Transform t){head=t;}public void Read(HandFrame f){f.Left.Tracked=f.Right.Tracked=true;f.HeadPosition=head.position;f.HeadRotation=head.rotation;DuelArt.FillPose(f.Left.Joints,Left,0);DuelArt.FillPose(f.Right.Joints,Right,1);for(int i=0;i<26;i++){f.Left.Joints[i]+=new Vector3(-.18f,-.2f,.4f);f.Right.Joints[i]+=new Vector3(.18f,-.2f,.4f);}}}
  [UnityTest] public IEnumerator DuelIsFirstAndRealInputsReachClashReplay(){
   var cam=new GameObject("Duel test camera").AddComponent<Camera>();cam.tag="MainCamera";cam.gameObject.AddComponent<AudioListener>();var go=new GameObject("Duel flow test");var app=go.AddComponent<App>();float until=Time.realtimeSinceStartup+10;while(app.Screen=="Loading"&&Time.realtimeSinceStartup<until)yield return null;
   Assert.That(app.Screen,Is.EqualTo("HAND DUEL"));yield return new WaitForSeconds(1.2f);Capture(cam,"duel-home.png");
   var source=new Source(cam.transform);typeof(App).GetField("source",Flags).SetValue(app,source);
   Call(app,"DuelPractice");source.Left=RpsMove.Paper;source.Right=RpsMove.Scissors;yield return new WaitForSeconds(.4f);Capture(cam,"duel-practice.png");
   source.Left=source.Right=RpsMove.Rock;Call(app,"DuelNewMatch");var round=Get<RpsRound>(app,"duelRound");until=Time.realtimeSinceStartup+6;while(round.Phase!=RpsPhase.Result&&Time.realtimeSinceStartup<until){if(round.Time>2.7f)source.Left=RpsMove.Paper;yield return null;}
   Assert.That(round.Phase,Is.EqualTo(RpsPhase.Result));Assert.That(Get<int>(app,"duelLeftPoints"),Is.EqualTo(1));yield return new WaitForSeconds(1.05f);Capture(cam,"duel-paper-wrap.png");
   for(int k=0;k<2;k++){var l=k==0?RpsMove.Rock:RpsMove.Scissors;var r=k==0?RpsMove.Scissors:RpsMove.Paper;typeof(App).GetField("duelLeftMove",Flags).SetValue(app,l);typeof(App).GetField("duelRightMove",Flags).SetValue(app,r);DuelArt.FillPose(Get<Vector3[]>(app,"duelLeft"),l,0);DuelArt.FillPose(Get<Vector3[]>(app,"duelRight"),r,1);typeof(App).GetField("duelTime",Flags).SetValue(app,1.15f);yield return null;Capture(cam,k==0?"duel-rock-smash.png":"duel-scissors-cut.png");}
   Call(app,"DuelReveal",true);yield return new WaitForSeconds(.3f);Assert.That(Get<int>(app,"duelLeftPoints"),Is.EqualTo(1));Assert.That(Get<float>(app,"duelSpeed"),Is.EqualTo(.25f));
   Call(app,"DuelFilm");Assert.That(Get<bool>(app,"duelFilm"),Is.True);Call(app,"Home");Assert.That(Get<bool>(app,"duelFilm"),Is.False);
   if(System.Environment.GetEnvironmentVariable("SOH_RENDER_DUEL_VIDEO")=="1"){
    var folder=System.IO.Path.Combine(Application.dataPath,"../Logs/duel-frames");System.IO.Directory.CreateDirectory(folder);int frame=0;
    for(int k=0;k<3;k++){var l=k==0?RpsMove.Rock:k==1?RpsMove.Paper:RpsMove.Scissors;var r=k==0?RpsMove.Scissors:k==1?RpsMove.Rock:RpsMove.Paper;
     typeof(App).GetField("duelLeftMove",Flags).SetValue(app,l);typeof(App).GetField("duelRightMove",Flags).SetValue(app,r);DuelArt.FillPose(Get<Vector3[]>(app,"duelLeft"),l,0);DuelArt.FillPose(Get<Vector3[]>(app,"duelRight"),r,1);
     Call(app,"DuelReveal",false);Call(app,"DuelFilm");Get<Label>(app,"duelFilmHint").Text=(k==0?"ROCK BREAKS SCISSORS":k==1?"PAPER WRAPS ROCK":"SCISSORS CUTS PAPER")+"\nUNITY EDITOR • SCRIPTED PREVIEW";
     for(int f=0;f<120;f++){typeof(App).GetField("duelTime",Flags).SetValue(app,f/30f);yield return null;CaptureVideo(cam,System.IO.Path.Combine(folder,(frame++).ToString("D4")+".png"));}
    }
   }
   Call(app,"CharadesHome");Assert.That(app.Screen,Is.EqualTo("Show of Hands"));
   Object.Destroy(go);Object.Destroy(cam.gameObject);
  }
  static void CaptureVideo(Camera c,string path){var target=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);c.targetTexture=target;c.aspect=16f/9;c.fieldOfView=43;c.Render();var old=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();System.IO.File.WriteAllBytes(path,image.EncodeToPNG());RenderTexture.active=old;c.targetTexture=null;Object.Destroy(image);Object.Destroy(target);}
  static void Capture(Camera c,string name){var r=new RenderTexture(1440,1080,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);c.targetTexture=r;c.aspect=4f/3;c.fieldOfView=65;c.Render();var old=RenderTexture.active;RenderTexture.active=r;var image=new Texture2D(1440,1080,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1440,1080),0,0);image.Apply();System.IO.File.WriteAllBytes(System.IO.Path.Combine(Application.dataPath,"../Logs/"+name),image.EncodeToPNG());RenderTexture.active=old;c.targetTexture=null;Object.Destroy(image);Object.Destroy(r);}
 }
}
