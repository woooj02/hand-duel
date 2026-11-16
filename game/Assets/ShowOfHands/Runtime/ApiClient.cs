using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace ShowOfHands {
 [Serializable] public class PlayerReply { public string player,code; }
 [Serializable] public class UploadReply { public string id,url; public string[] options; public bool daily; }
 [Serializable] public class ClipSummary { public string id,glove,created,by; public int frames; }
 [Serializable] public class FeedReply { public ClipSummary[] clips; }
 [Serializable] public class OwnClip { public string id,word,created,topWrong; public int seen,correct,applause; }
 [Serializable] public class OwnReply { public string code; public OwnClip[] clips; }
 [Serializable] public class DailyReply { public string date,word; public int clips; }
 public sealed class ApiClient:MonoBehaviour {
  App app; List<HandClip> feed,mine; string root,queue,cache,storage; bool busy,wake;
  public string Player {get;private set;} public string Code {get;private set;}
  public bool Online {get;private set;} public OwnClip[] Own = Array.Empty<OwnClip>();
  public readonly List<HandClip> Daily = new List<HandClip>();
  public DailyReply Today; public event Action<HandClip,string> Uploaded;
  public string ProfileUrl=>string.IsNullOrEmpty(Code)?null:root+"/u/"+Code;
  public void Init(App app,List<HandClip> clips,List<HandClip> mine,string storageOverride=null){
   storage=storageOverride??Application.persistentDataPath;Directory.CreateDirectory(storage);this.app=app;feed=clips;this.mine=mine;root=app.ServerBase.TrimEnd('/');
   queue=Path.Combine(storage,"upload-queue");cache=Path.Combine(storage,"clip-cache");
   Directory.CreateDirectory(queue);Directory.CreateDirectory(cache);
   Player=Code="";var identity=Path.Combine(storage,"identity.json");if(File.Exists(identity)){try{var saved=JsonUtility.FromJson<StoredIdentity>(File.ReadAllText(identity));if(saved.server==root){Player=saved.player;Code=saved.code;}}catch(Exception){}}
   foreach(var file in Directory.GetFiles(cache,"*.json")){try{var c=HandClip.FromJson(File.ReadAllText(file),out _);if(c!=null&&!feed.Exists(x=>x.Id==c.Id))feed.Add(c);}catch(IOException){}}
   // Recovery after quitting immediately after Keep: the local copy is the source of truth.
   foreach(var file in Directory.GetFiles(storage,"local-*.clip.json")){try{var c=HandClip.FromJson(File.ReadAllText(file),out _);if(c!=null&&string.IsNullOrEmpty(c.Id))File.Copy(file,Path.Combine(queue,Path.GetFileName(file)),true);}catch(IOException){}}
   if(root.StartsWith("https://")||root.StartsWith("http://"))StartCoroutine(Pump());
  }
  IEnumerator Request(string method,string path,string body,Action<string> success,Action<long> failure=null){
   using(var r=new UnityWebRequest(root+"/api/v1"+path,method)){
    r.timeout=6;r.downloadHandler=new DownloadHandlerBuffer();
    if(body!=null){r.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));r.SetRequestHeader("Content-Type","application/json");}
    yield return r.SendWebRequest();Online=r.result==UnityWebRequest.Result.Success;
    if(Online){if(success!=null)success(r.downloadHandler.text);}else failure?.Invoke(r.responseCode);
   }
  }
  [Serializable] class StoredIdentity {public string player,code,server;}
  void SaveIdentity(){File.WriteAllText(Path.Combine(storage,"identity.json"),JsonUtility.ToJson(new StoredIdentity{player=Player,code=Code,server=root}));}
  IEnumerator Identity(){if(!string.IsNullOrEmpty(Player))yield break;yield return Request("POST","/players","{}",s=>{var p=JsonUtility.FromJson<PlayerReply>(s);Player=p.player;Code=p.code;SaveIdentity();});}
  IEnumerator Pump(){
   while(true){busy=true;yield return Identity();
    if(!string.IsNullOrEmpty(Player)){
     foreach(var file in Directory.GetFiles(queue,"*.clip.json")){
      HandClip c=null;try{c=HandClip.FromJson(File.ReadAllText(file),out _);}catch(IOException){}if(c==null)continue;
      yield return Request("POST","/clips",c.ToUploadJson(Player),s=>{
       var reply=JsonUtility.FromJson<UploadReply>(s);c.Id=reply.id;c.By=Code;c.Options=reply.options;c.Daily=reply.daily;
       var local=Path.Combine(storage,Path.GetFileName(file));File.WriteAllText(local,c.ToStoredJson());
       var live=mine.Find(x=>x.Created==c.Created&&x.Word==c.Word&&string.IsNullOrEmpty(x.Id));
       if(live!=null){live.Id=c.Id;live.By=Code;live.Options=c.Options;live.Daily=c.Daily;}
       File.Delete(file);Uploaded?.Invoke(live??c,reply.url);
      },status=>{if(status==401){Player="";File.Delete(Path.Combine(storage,"identity.json"));}if(status==400||status==413){File.Move(file,file+".rejected-"+Guid.NewGuid().ToString("N"));Debug.LogWarning("SOH upload rejected; original remains saved locally.");}});
      if(!Online)break;
     }
     yield return Refresh();
    }
    busy=false;for(int i=0;i<20&&!wake;i++)yield return new WaitForSecondsRealtime(1);wake=false;
   }
  }
  public void Upload(HandClip c,string key){File.WriteAllText(Path.Combine(queue,key+".clip.json"),c.ToStoredJson());wake=true;}
  public IEnumerator Refresh(){
   yield return Request("GET","/feed?n=20&player="+UnityWebRequest.EscapeURL(Player),null,s=>{var f=JsonUtility.FromJson<FeedReply>(s);pendingFeed=f?.clips;});
   var summaries=pendingFeed;pendingFeed=null;if(summaries!=null)foreach(var summary in summaries){yield return Fetch(summary.id,c=>{var existing=feed.Find(x=>x.Id==c.Id);if(existing==null)feed.Insert(0,c);else{existing.Options=c.Options;existing.Seen=c.Seen;existing.Correct=c.Correct;}});if(!Online)break;}
   yield return Request("GET","/players/"+Player+"/clips",null,s=>{Own=JsonUtility.FromJson<OwnReply>(s).clips??Array.Empty<OwnClip>();foreach(var item in Own){var c=mine.Find(x=>x.Id==item.id);if(c!=null){c.Seen=item.seen;c.Correct=item.correct;}}});
   yield return Request("GET","/daily",null,s=>Today=JsonUtility.FromJson<DailyReply>(s));
   yield return Request("GET","/daily/clips?n=24",null,s=>{pendingDaily=JsonUtility.FromJson<FeedReply>(s)?.clips;});
   var daily=pendingDaily;pendingDaily=null;if(daily!=null){Daily.Clear();foreach(var summary in daily)yield return Fetch(summary.id,c=>Daily.Add(c));}
  }
  ClipSummary[] pendingFeed,pendingDaily;
  public IEnumerator Fetch(string id,Action<HandClip> use){yield return Request("GET","/clips/"+id,null,s=>{var c=HandClip.FromJson(s,out _);if(c!=null){File.WriteAllText(Path.Combine(cache,id+".json"),c.ToStoredJson());use(c);}});}
  public void Guess(HandClip c,string guess,int ms){if(string.IsNullOrEmpty(root)||string.IsNullOrEmpty(c.Id))return;StartCoroutine(Request("POST","/clips/"+c.Id+"/guesses","{\"player\":"+(string.IsNullOrEmpty(Player)?"null":Json.Quote(Player))+",\"guess\":"+Json.Quote(guess)+",\"ms\":"+ms+",\"via\":\"headset\"}",null));}
  public void Report(HandClip c){if(c==null)return;feed.Remove(c);Daily.Remove(c);if(!string.IsNullOrEmpty(root)&&!string.IsNullOrEmpty(c.Id))StartCoroutine(Request("POST","/clips/"+c.Id+"/reports","{\"player\":"+(string.IsNullOrEmpty(Player)?"null":Json.Quote(Player))+",\"reason\":\"other\"}",null));}
  public void Applaud(HandClip c){if(!string.IsNullOrEmpty(root)&&!string.IsNullOrEmpty(c.Id))StartCoroutine(Request("POST","/clips/"+c.Id+"/applause","{\"player\":"+(string.IsNullOrEmpty(Player)?"null":Json.Quote(Player))+"}",null));}
  public void DeleteAll(Action<bool> done){StopAllCoroutines();busy=false;StartCoroutine(DeleteRoutine(done));}
  IEnumerator DeleteRoutine(Action<bool> done){bool ok=string.IsNullOrEmpty(Player);if(!ok&&!string.IsNullOrEmpty(root))yield return Request("DELETE","/players/"+Player,null,_=>ok=true);if(ok){foreach(var f in Directory.GetFiles(storage,"local-*.clip.json"))File.Delete(f);foreach(var f in Directory.GetFiles(queue))File.Delete(f);mine.Clear();Own=Array.Empty<OwnClip>();Player=Code="";File.Delete(Path.Combine(storage,"identity.json"));}done(ok);if(!string.IsNullOrEmpty(root))StartCoroutine(Pump());}
 }
}
