using System;
using System.IO;
using System.Linq;
using UnityEngine;
namespace ShowOfHands {
 public sealed partial class App {
  bool seedSession;int seedIndex;
  #if SOH_DEVELOPMENT
  Label diagnostics;
  void UpdateDiagnostics(){if(duelActive||arcadeMode!=ArcadeMode.None){if(diagnostics!=null)diagnostics.gameObject.SetActive(false);return;}if(diagnostics!=null)diagnostics.gameObject.SetActive(true);if(diagnostics==null&&stage!=null)diagnostics=Ui.Label(stage,"",.030f,Palette.Text,new Vector3(0,-.49f,1.2f));if(diagnostics!=null)diagnostics.Text=(1/Time.smoothDeltaTime).ToString("F0")+" fps • "+pointer.Kind+" • "+batch.Instances+" shapes";}
  #endif
  #if SOH_DEVELOPMENT
  void Developer(){Show("Developer tools","Device checks • not shown in release");Button("Seed session",-.22f,-.12f,()=>{seedSession=true;seedIndex=mine.Count;SeedNext();},.40f);Button("Export seeds",.22f,-.12f,()=>{ExportSeeds();note.Text="seedpack-export.json saved to app files";},.40f);Button("24-clip load test",0,-.31f,()=>{word="wave";Show("24-clip load test","Synthetic copies • performance test only");for(int i=0;i<24;i++){var p=new ClipPlayer();p.Play(clips[i%clips.Count],(i%3)*.12f);wall.Add(p);}BuildGalleryTargets();NavHome();},.50f);NavHome();}
  #endif
  void SeedNext(){
   #if SOH_DEVELOPMENT
   var choices=words.words.Where(w=>!oneHand||w.hands==1).ToArray();if(seedIndex>=choices.Length){seedSession=false;Show("Seed session complete",mine.Count+" saved performances");Button("Home",0,-.23f,Home);return;}word=choices[seedIndex].id;Show("Seed "+(seedIndex+1)+" / "+choices.Length,Lookup(word).text);WordCard(Lookup(word),0,-.16f,Countdown,.40f);Button("Skip",-.22f,-.38f,()=>{seedIndex++;SeedNext();},.34f);Button("Finish",.22f,-.38f,()=>{ExportSeeds();seedSession=false;Home();},.34f);
   #endif
  }
  void ExportSeeds(){
   #if SOH_DEVELOPMENT
   const string alphabet="23456789ABCDEFGHJKLMNPQRSTUVWXYZ";var chosen=mine.OrderBy(c=>c.Created).Take(150).ToArray();var entries=new string[chosen.Length];for(int i=0;i<chosen.Length;i++){var c=HandClip.FromJson(chosen[i].ToStoredJson(),out _);int n=i;string id="";for(int k=0;k<6;k++){id=alphabet[n%alphabet.Length]+id;n/=alphabet.Length;}c.Id=id;c.By="SEEDS";entries[i]=c.ToStoredJson();}File.WriteAllText(Path.Combine(Application.persistentDataPath,"seedpack-export.json"),"{\"v\":1,\"clips\":["+string.Join(",",entries)+"]}");
   #endif
  }
 }
}
