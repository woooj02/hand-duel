using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
namespace ShowOfHands {
 [Serializable] public class Word { public string id,text,cat,emoji;public string[] alt;public int level,hands; }
 [Serializable] public class WordPack { public int v;public Word[] words;public string[] daily; }
 public sealed partial class App:MonoBehaviour {
  public string Screen {get;private set;}="Loading";
  readonly WaveDetector wave=new WaveDetector();readonly List<Card> answers=new List<Card>();readonly HashSet<string> guessed=new HashSet<string>();
  readonly Pointer pointer=new Pointer();readonly HandFrame hands=new HandFrame();
  readonly ClipRecorder recorder=new ClipRecorder();readonly ClipPlayer replay=new ClipPlayer();
  readonly List<HandClip> clips=new List<HandClip>();readonly List<ClipPlayer> wall=new List<ClipPlayer>();
  readonly List<HandClip> mine=new List<HandClip>();
  Transform head,stage,view;ShapeBatch batch;XrHandSource xr;IHandSource source;WordPack words;
  Label heading,note;float clock,away,elapsed;int round,score;bool opening,dailyAct,focused=true,oneHand;string word="wave",glove="ember";HandClip recorded;Action pending;float pendingAt;
  public string ServerBase {get;private set;}="";
  string arcadeEndpoint="http://127.0.0.1:8790";
  Transform backdrop;Material backdropMaterial;AudioSource ambientSource;AudioSource audioSource;ApiClient api;Panel timer;Fader fader;bool playing;readonly Vector3[] trailPoints=new Vector3[20];readonly bool[] trailReady=new bool[4];readonly bool[] wasMitt=new bool[4];
  int lastSecond=-1,minePage;string latestLocal;Glove selectedGlove;
  #if UNITY_EDITOR
  public Action<string> EditorCue;
  #endif
  IEnumerator Start(){
   Ui.Init();Application.targetFrameRate=72;selectedGlove=Glove.Find(PlayerPrefs.GetString("glove","ember"));glove=selectedGlove.Id;batch=new ShapeBatch(Ui.HandMaterial);audioSource=gameObject.AddComponent<AudioSource>();ambientSource=gameObject.AddComponent<AudioSource>();ambientSource.loop=true;ambientSource.volume=.035f;
   var rig=FindAnyObjectByType<OVRCameraRig>();head=rig!=null?rig.centerEyeAnchor:Camera.main?.transform;
   if(head==null){var camera=new GameObject("Editor Camera").AddComponent<Camera>();camera.tag="MainCamera";head=camera.transform;}
   foreach(var c in FindObjectsByType<Camera>(FindObjectsSortMode.None)){c.backgroundColor=Palette.Background;c.clearFlags=CameraClearFlags.SolidColor;c.nearClipPlane=.01f;}
   stage=new GameObject("Stage").transform;stage.SetParent(transform,false);stage.localScale=Vector3.one*.38f;Recentre();
   backdrop=new GameObject("Plum stage").transform;backdrop.SetParent(transform,false);backdrop.localScale=Vector3.one*20;backdrop.gameObject.AddComponent<MeshFilter>().sharedMesh=Shapes.Sphere;var renderer=backdrop.gameObject.AddComponent<MeshRenderer>();backdropMaterial=new Material(Ui.BackdropMaterial);renderer.sharedMaterial=backdropMaterial;backdropMaterial.SetColor("_Color",Palette.Background);backdropMaterial.SetColor("_Glow",Palette.StageGlow);
   xr=new XrHandSource(head,rig!=null?rig.trackingSpace:head);source=xr;
   yield return ReadAsset("words.json",s=>words=JsonUtility.FromJson<WordPack>(s));
   yield return ReadAsset("seedpack.json",s=>{var pack=JsonUtility.FromJson<SeedPackJson>(s);foreach(var d in pack.clips){var c=HandClip.FromDto(d,out var e);if(c!=null)clips.Add(c);else Debug.LogError(e);}});
   yield return ReadAsset("server.txt",s=>ServerBase=s.Trim());
   yield return ReadAsset("arena.txt",s=>arcadeEndpoint=s.Trim());
   if(words==null||words.words==null||clips.Count==0){Debug.LogError("SOH content missing");Show("Content missing");yield break;}
   foreach(var path in Directory.GetFiles(Application.persistentDataPath,"local-*.clip.json")){var c=HandClip.FromJson(File.ReadAllText(path),out _);if(c!=null)mine.Add(c);}
   api=gameObject.AddComponent<ApiClient>();api.Init(this,clips,mine);api.Uploaded+=OnUploaded;
   #if UNITY_EDITOR
   source=new SyntheticHandSource(clips[0],head);
   #endif
   oneHand=PlayerPrefs.GetInt("oneHand",0)==1;pointer.DwellEnabled=PlayerPrefs.GetInt("dwell",0)==1;
   Ui.HighContrast=PlayerPrefs.GetInt("contrast",0)==1;Ui.LargeText=PlayerPrefs.GetInt("large",0)==1;Ui.ReducedMotion=PlayerPrefs.GetInt("motion",0)==1;
   InitArcade();InitBorrowlight();Home();
   Debug.Log("SOH_READY offline clips="+clips.Count+" device="+SystemInfo.deviceModel);
  }
  IEnumerator ReadAsset(string name,Action<string> use){using(var r=UnityWebRequest.Get(Application.streamingAssetsPath+"/"+name)){r.timeout=6;yield return r.SendWebRequest();if(r.result==UnityWebRequest.Result.Success)try{use(r.downloadHandler.text);}catch(Exception e){Debug.LogError(name+": "+e.Message);}}}
  void Show(string title,string detail=""){
   CloseDuelFilm();CloseArcade();CloseBorrowlight();duelActive=false;Screen=title;for(int i=0;i<trailReady.Length;i++){trailReady[i]=false;wasMitt[i]=false;}if(ambientSource!=null){string ambient=title=="Same word, different hands"?"music_gallery_loop":"amb_stage_loop";var sound=SoundBank.Get(ambient);if(ambientSource.clip!=sound){ambientSource.clip=sound;ambientSource.Play();}}playing=false;timer=null;lastSecond=-1;answers.Clear();pointer.Clear();pointer.Active=true;pending=null;wall.Clear();if(view!=null){view.gameObject.SetActive(false);Destroy(view.gameObject);}view=new GameObject(title).transform;view.SetParent(stage,false);fader=view.gameObject.AddComponent<Fader>();
   heading=Ui.Label(view,title,.040f,Palette.Text,new Vector3(0,.38f,1.1f));
   note=Ui.Label(view,detail,.032f,Palette.Accent,new Vector3(0,.285f,1.1f));elapsed=0;StartCoroutine(FadeIn(view,fader));
  }
  Card Button(string text,float x,float y,Action action,float width=.24f){var button=Card.Create(view,pointer,new Vector3(x,y,1.1f),new Vector2(width,.16f),text,Resources.Load<Texture2D>("Icons/"+(text=="Keep & share"?"keep":text.ToLowerInvariant().Replace(' ','-'))),()=>{Cue(text=="Back"||text=="Home"?"ui_back":"ui_select");action();});button.EnsureMinimumTouchSize(.062f);return button;}
  Card WordCard(Word w,float x,float y,Action action,float width=.30f){return Card.Create(view,pointer,new Vector3(x,y,1.1f),new Vector2(width,.17f),Wrap(w.text),Resources.Load<Texture2D>("Icons/"+w.id),()=>{Cue("ui_select");action();});}
  static string Wrap(string text){if(text.Length<=15)return text;int cut=text.LastIndexOf(' ',Mathf.Min(14,text.Length-1));return cut>0?text.Substring(0,cut)+"\n"+text.Substring(cut+1):text;}
  IEnumerator FadeIn(Transform target,Fader fade){yield return null;if(target!=null){fade.Collect();fade.Snap(0);fade.Show();}}
  void NavHome(){Button("Home",.42f,.30f,Home,.16f);}

  public void Recentre(){if(stage==null||head==null)return;var forward=head.forward;forward.y=0;if(forward.sqrMagnitude<.01f)forward=Vector3.forward;stage.SetPositionAndRotation(head.position+Vector3.down*.16f,Quaternion.LookRotation(forward));away=0;}
  void Opening(){opening=true;wave.Reset();Show("Wave hello","Wave back to the glowing hands");replay.Play(clips.First(c=>c.Word=="wave"));playing=true;}
  void CharadesHome(){opening=false;seedSession=false;int fresh=Mathf.Max(0,api.Own.Sum(c=>c.seen)-PlayerPrefs.GetInt("seenAtVisit",0));Show("Show of Hands",fresh>0?fresh+" new guesses on your performances":api.Online?"Point your hand, pinch, play":"Offline • your hands stay saved");Button("Guess",-.30f,-.18f,()=>{round=score=0;Guess();},.28f);Button("Act",0,-.18f,()=>Act(false),.28f);Button("Today",.30f,-.18f,Today,.28f);Button("Settings",-.21f,-.38f,Settings,.35f);Button("Your hands",.21f,-.38f,()=>{minePage=0;Mine();},.35f);}

  void Guess(){
   Show(opening?"What are these hands saying?":"Guess "+(round+1)+" / 5");var available=clips.Where(c=>(!oneHand||Lookup(c.Word).hands==1)&&!guessed.Contains(c.Id??c.Word)&&!mine.Contains(c)).ToList();if(available.Count==0&&!opening){Show("You caught them all","Act a word for the next player");Button("Act",0,-.23f,()=>Act(false));NavHome();return;}
   recorded=opening?clips.First(c=>c.Word=="wave"):available[UnityEngine.Random.Range(0,available.Count)];replay.Play(recorded);playing=true;MakeTimer();
   var options=recorded.Options?.Where(id=>words.words.Any(w=>w.id==id)).ToList()??new List<string>();
   if(options.Count!=4){options=words.words.Where(w=>w.id!=recorded.Word&&w.cat==Lookup(recorded.Word).cat).Select(w=>w.id).Take(3).ToList();foreach(var w in words.words)if(options.Count<3&&w.id!=recorded.Word&&!options.Contains(w.id))options.Add(w.id);options.Add(recorded.Word);options=options.OrderBy(_=>UnityEngine.Random.value).ToList();}
   int count=opening?3:4;if(opening){options.Remove(recorded.Word);options=options.Take(2).ToList();options.Insert(UnityEngine.Random.Range(0,3),recorded.Word);}
   for(int i=0;i<count;i++){string id=options[i];Card card=null;float x=opening?(i-1)*.31f:(i%2-.5f)*.44f;float y=opening?-.23f:-.17f-(i/2)*.19f;card=WordCard(Lookup(id),x,y,()=>Answer(id,card),opening?.29f:.42f);answers.Add(card);}
   if(!opening)Button("Report",-.43f,.30f,()=>{api.Report(recorded);guessed.Add(recorded.Id??recorded.Word);Guess();},.16f);
   NavHome();
  }

  void Answer(string guess,Card selected){if(pending!=null)return;bool right=guess==recorded.Word;selected.State=right?Card.Look.Right:Card.Look.Wrong;selected.Pop();guessed.Add(recorded.Id??recorded.Word);foreach(var card in answers)if(card.Text.Text==Wrap(Lookup(recorded.Word).text))card.State=Card.Look.Right;if(right){PlayerPrefs.SetInt("rightTotal",PlayerPrefs.GetInt("rightTotal",0)+1);}if(right)score+=100+Mathf.RoundToInt(100*Mathf.Clamp01(1-elapsed/(recorded.Seconds*3)));note.Text=right?"Yes! "+Lookup(recorded.Word).text:"It was "+Lookup(recorded.Word).text;Cue(right?"guess_right":"guess_wrong");api?.Guess(recorded,guess,(int)(elapsed*1000));pointer.Active=false;pendingAt=clock+1.4f;pending=()=>{if(opening){word="wave";dailyAct=false;Countdown();}else if(++round<5)Guess();else{Show("Round complete",score+" points");Button("Act one back",0,-.23f,()=>Act(false),.4f);Button("Home",0,-.4f,Home);}};}
  Word Lookup(string id)=>words.words.FirstOrDefault(w=>w.id==id)??new Word{id=id,text=id,cat="action",hands=2};
  string DailyWord()=>words.daily[(int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds()/86400%words.daily.Length)];
  void Act(bool daily){dailyAct=daily;Show(daily?"Today's word":"Choose your word");var choices=daily?new[]{Lookup(DailyWord())}:words.words.Where(w=>!oneHand||w.hands==1).OrderBy(_=>UnityEngine.Random.value).Take(3).ToArray();for(int i=0;i<choices.Length;i++){var w=choices[i];WordCard(w,(i-(choices.Length-1)*.5f)*.31f,-.23f,()=>{word=w.id;Countdown();},.29f);}Button("Home",-.18f,-.41f,Home);if(!daily)Button("Shuffle",.18f,-.41f,()=>Act(false));}
  void Countdown(){Show("Get ready",Lookup(word).text);pointer.Active=false;MakeTimer();Ui.Panel(view,"Word picture",new Vector3(0,-.08f,1.1f)).AsPicture(Resources.Load<Texture2D>("Icons/"+word),Vector2.one*.18f,Color.white);}
  void BeginRecording(){if(!hands.Left.Tracked&&!hands.Right.Tracked){Show("Show your hands","Hold a hand in front of you, below eye level");Button("Ready",0,-.23f,Countdown,.36f);NavHome();return;}Show("Act it out",Lookup(word).text);pointer.Active=false;recorder.Begin(hands,8);MakeTimer();Cue("count_go");}
  void Review(){recorded=recorder.End(word,glove,Application.version,SystemInfo.deviceModel);if(recorded==null||recorded.TrackedShare()<.5f){Show("Let's try again","Keep your hands in view");Button("Again",0,-.23f,Countdown);NavHome();return;}Show("Your performance",string.IsNullOrEmpty(ServerBase)?"Offline charades • your clip stays on this headset":"Keep & share publishes this hand clip");replay.Play(recorded);playing=true;Cue("rec_stop");Cue("reveal");Button(string.IsNullOrEmpty(ServerBase)?"Keep locally":"Keep & share",-.20f,-.23f,Keep,.34f);Button("Again",.21f,-.23f,Countdown,.30f);NavHome();}
  void Keep(){var c=recorded;c.Created=DateTime.UtcNow.ToString("o");latestLocal="local-"+Guid.NewGuid().ToString("N");File.WriteAllText(Path.Combine(Application.persistentDataPath,latestLocal+".clip.json"),c.ToStoredJson());mine.Add(c);api.Upload(c,latestLocal);PlayerPrefs.SetInt("opened",1);if(dailyAct)MarkDaily();PlayerPrefs.Save();Cue("send_whoosh");if(seedSession){ExportSeeds();seedIndex++;SeedNext();return;}ShowKept(c);}
  void ShowKept(HandClip c){Show("Kept",string.IsNullOrEmpty(c.Id)?"Saved here • share link pending":ServerBase+"/c/"+c.Id);recorded=c;playing=false;
   if(!string.IsNullOrEmpty(c.Id))Ui.Label(view,c.Id,.065f,Palette.Accent,new Vector3(0,.05f,1.1f));
   if(!string.IsNullOrEmpty(api.Code))Ui.Label(view,"Your page: /u/"+api.Code,.034f,Palette.Text,new Vector3(0,-.12f,1.1f));
   Button(dailyAct||opening?"Gallery":"Your hands",-.20f,-.38f,dailyAct||opening?(Action)Gallery:()=>{minePage=0;Mine();},.36f);Button("Home",.21f,-.38f,Home,.30f);
  }
  void OnUploaded(HandClip c,string url){Cue("receive_pop");if(Screen=="Kept"&&recorded==c)ShowKept(c);}

  void Today(){word=DailyWord();Show("Today's word",Lookup(word).text+" • "+(api.Today?.clips??0)+" performances\n"+PlayerPrefs.GetInt("dayStreak")+"-day streak");Button("Act today's word",0,-.18f,()=>Act(true),.46f);if(PlayerPrefs.GetString("dailyDone")==DateTime.UtcNow.ToString("yyyy-MM-dd"))Button("Gallery",0,-.36f,Gallery,.34f);else Ui.Label(view,"Act it to unlock the gallery",.032f,Palette.Text,new Vector3(0,-.34f,1.1f));NavHome();}
  void Gallery(){Show("Same word, different hands",Lookup(word).text);var items=api.Daily.Where(c=>c.Word==word).Concat(mine.Where(c=>c.Word==word)).Concat(clips.Where(c=>c.Word==word)).GroupBy(c=>c.Id??c.Created??c.Word).Select(g=>g.First()).Take(24).ToList();
   if(items.Count==0){note.Text="No performances yet • yours starts it";}else{foreach(var c in items){var player=new ClipPlayer();player.Play(c);wall.Add(player);}BuildGalleryTargets();}
   Cue("gallery_open");NavHome();
  }
  void BuildGalleryTargets(){for(int i=0;i<wall.Count;i++){var c=wall[i].Clip;var pos=GalleryPosition(i,wall.Count);var target=new Target{Transform=new GameObject("Gallery target").transform,HalfSize=new Vector2(.085f,.08f)};target.Transform.SetParent(view,false);target.Transform.localPosition=pos;target.OnSelect=()=>Watch(c,Gallery);pointer.Add(target);}}
  static Vector3 GalleryPosition(int i,int count){int cols=Mathf.Min(6,count);float x=(i%cols-(cols-1)*.5f)*.20f;float y=.18f-(i/cols)*.14f;return new Vector3(x,y,1.30f+.10f*x*x);}
  void Watch(HandClip c,Action back){Show("A show of hands",c.Seen+" watched • "+c.Correct+" guessed");recorded=c;replay.Play(c);playing=true;Button("Back",-.24f,-.32f,back);Button("Applaud",.04f,-.32f,()=>{api.Applaud(c);Cue("applause");note.Text="Applause sent";});Button("Report",.32f,-.32f,()=>{api.Report(c);back();});}
  void Mine(){PlayerPrefs.SetInt("seenAtVisit",api.Own.Sum(c=>c.seen));PlayerPrefs.Save();int start=minePage*3;var items=mine.OrderByDescending(c=>c.Created).Take(20).ToList();Show("Your hands",api.ProfileUrl??(items.Count+" saved • sharing when connected"));
   for(int i=0;i<Mathf.Min(3,items.Count-start);i++){var c=items[start+i];var stats=api.Own.FirstOrDefault(x=>x.id==c.Id);string detail=stats==null?"Saved locally":stats.correct+" / "+stats.seen+" guessed • "+stats.applause+" applause";Button(Lookup(c.Word).text,0,.12f-i*.18f,()=>{Watch(c,Mine);note.Text=detail+(stats?.topWrong!=null?"\nOften guessed: "+Lookup(stats.topWrong).text:"");},.65f);}
   if(start>0)Button("Previous",-.22f,-.43f,()=>{minePage--;Mine();},.32f);if(start+3<items.Count)Button("Next",.22f,-.43f,()=>{minePage++;Mine();},.32f);NavHome();
  }
  void Settings(){Show("Settings");Button("Dwell: "+pointer.DwellEnabled,-.22f,.10f,()=>{pointer.DwellEnabled=!pointer.DwellEnabled;Save("dwell",pointer.DwellEnabled);Settings();},.40f);Button("One hand: "+oneHand,.22f,.10f,()=>{oneHand=!oneHand;Save("oneHand",oneHand);Settings();},.40f);Button("Contrast",-.22f,-.06f,()=>{Ui.HighContrast=!Ui.HighContrast;Save("contrast",Ui.HighContrast);Settings();},.40f);Button("Reduced motion",.22f,-.06f,()=>{Ui.ReducedMotion=!Ui.ReducedMotion;Save("motion",Ui.ReducedMotion);Settings();},.40f);Button("Large text",-.22f,-.22f,()=>{Ui.LargeText=!Ui.LargeText;Save("large",Ui.LargeText);Settings();},.40f);Button("More",.22f,-.22f,MoreSettings,.40f);Button("Opening",-.22f,-.39f,Opening,.40f);Button("Recentre",.22f,-.39f,Recentre,.40f);NavHome();}
  static void Save(string key,bool value){PlayerPrefs.SetInt(key,value?1:0);PlayerPrefs.Save();}
  void MoreSettings(){Show("Your preferences");Button("Gloves",-.22f,-.12f,Gloves,.38f);Button("Delete my clips",.22f,-.12f,ConfirmDelete,.38f);Button("Back",0,-.31f,Settings,.34f);
   #if SOH_DEVELOPMENT
   Button("Developer",0,.12f,Developer,.38f);
   #endif
   NavHome();}
  void ConfirmDelete(){Show("Delete your performances?","This removes your shared and local clips");Button("Delete",-.22f,-.23f,()=>{pointer.Active=false;api.DeleteAll(ok=>{Show(ok?"Deleted":"Could not delete",ok?"Your performances were removed":"Connect and try again • clips kept safe");Button("Back",0,-.23f,Settings);});},.36f);Button("Cancel",.22f,-.23f,MoreSettings,.36f);}
  void Gloves(){Show("Choose your glow");for(int i=0;i<Glove.All.Length;i++){var g=Glove.All[i];int n=i;bool unlocked=i==0||(i==1&&PlayerPrefs.GetInt("rightTotal")>=5)||(i==2&&mine.Count>=5)||(i==3&&api.Own.Count(c=>c.seen>0)>=10)||(i==4&&PlayerPrefs.GetInt("dayStreak")>=3)||(i==5&&PlayerPrefs.GetInt("dayStreak")>=7);var c=Button(g.Id+(unlocked?"":" • locked"),(i%3-1)*.30f,.08f-(i/3)*.19f,()=>{if(unlocked){glove=g.Id;selectedGlove=g;PlayerPrefs.SetString("glove",g.Id);PlayerPrefs.Save();Gloves();}},.28f);c.State=g.Id==glove?Card.Look.Accent:unlocked?Card.Look.Normal:Card.Look.Dim;}Button("Back",0,-.37f,MoreSettings,.32f);}
  void MarkDaily(){string today=DateTime.UtcNow.ToString("yyyy-MM-dd"),last=PlayerPrefs.GetString("dailyDone");if(last!=today){int streak=last==DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd")?PlayerPrefs.GetInt("dayStreak")+1:1;PlayerPrefs.SetInt("dayStreak",streak);PlayerPrefs.SetString("dailyDone",today);Cue("streak_up");}}
  void MakeTimer(){timer=Ui.Panel(view,"Time",new Vector3(0,.17f,1.1f)).AsRing(.12f,.09f,Palette.Accent,Palette.CardEdge,1);}

  void Update(){
   #if SOH_INPUT_DIAGNOSTICS
   PollInputDiagnostics();
   #endif
   if(stage==null||source==null||words==null||clips.Count==0||!focused)return;float dt=Mathf.Min(Time.deltaTime,.1f);clock+=dt;elapsed+=dt;source.Read(hands);
   #if UNITY_EDITOR
   if((UnityEngine.InputSystem.Keyboard.current?.wKey.isPressed==true))head.position+=head.forward*dt*.5f;if((UnityEngine.InputSystem.Keyboard.current?.sKey.isPressed==true))head.position-=head.forward*dt*.5f;if((UnityEngine.InputSystem.Keyboard.current?.aKey.isPressed==true))head.position-=head.right*dt*.5f;if((UnityEngine.InputSystem.Keyboard.current?.dKey.isPressed==true))head.position+=head.right*dt*.5f;
   #endif
   bool wasDuel=duelActive;PrepareArcadeTargets();PrepareBorrowTargets();var previousHover=pointer.Hovered;pointer.Update(hands,default,GazeKind.None,dt);if(pointer.Hovered!=null&&pointer.Hovered!=previousHover)Cue("ui_hover");
   if(pending!=null&&clock>=pendingAt){var action=pending;pending=null;action();}
   if(Screen=="Wave hello"){wave.Update(hands);if(wave.Waved||(elapsed>6&&wave.Raised)||elapsed>12)Guess();}
   if(Screen=="Get ready"){int second=Mathf.Max(1,3-(int)elapsed);if(lastSecond!=second){note.Text=Lookup(word).text+" • "+second;lastSecond=second;Cue("count_tick");}if(timer!=null)timer.RingFill=Mathf.Clamp01(1-elapsed/3);if(elapsed>=3)BeginRecording();}
   if(Screen=="Act it out"){recorder.Push(hands,dt);int second=Mathf.CeilToInt(8*(1-recorder.Progress));bool seen=hands.Left.Tracked||hands.Right.Tracked;if(lastSecond!=second||!seen){note.Text=seen?Lookup(word).text+" • "+second+"s":"Hands lost • bring them back into view";lastSecond=seen?second:-1;}if(timer!=null)timer.RingFill=1-recorder.Progress;if(!recorder.Recording)Review();}
   if((Screen.StartsWith("Guess ")||Screen=="What are these hands saying?")&&pending==null){if(timer!=null)timer.RingFill=Mathf.Clamp01(1-elapsed/(recorded.Seconds*3));if(elapsed>=recorded.Seconds*3)Answer("",answers[0]);}
   if(duelActive)UpdateDuel(dt);
   if(arcadeMode!=ArcadeMode.None)UpdateArcade(dt);
   if(borrowActive)UpdateBorrowlight(dt);
   if(clock>=arenaPublishAt){arenaPublishAt=clock+.1f;PublishArena();}
   backdrop.position=head.position;backdropMaterial.SetVector("_Forward",stage.forward);
   batch.Clear();for(int h=0;h<2;h++){var hand=hands.Hand(h);bool mitt=hand.Tracked&&!hand.IsController&&HandPoses.IsRude(hand);if(hand.Tracked&&!hand.IsController&&!arcadeFilm&&(!duelActive||duelView==DuelView.Attract||duelView==DuelView.Ready||duelView==DuelView.Practice))HandDrawer.Draw(batch,hand.Joints,Matrix4x4.identity,1,borrowActive?BorrowGlove(h):duelActive?(h==0?Glove.Find("violet"):Glove.Find("mint")):selectedGlove,1,(duelActive||borrowActive)?false:mitt);Trails(hand.Joints,Matrix4x4.identity,1,selectedGlove,h,!duelActive&&arcadeMode==ArcadeMode.None&&!borrowActive&&hand.Tracked&&!hand.IsController&&!mitt);if(!duelActive&&arcadeMode==ArcadeMode.None&&!borrowActive&&mitt&&!wasMitt[h])Cue("mitt_boing");wasMitt[h]=mitt;}
   if(playing){replay.Advance(dt);DrawClip(replay,stage.localToWorldMatrix*Matrix4x4.TRS(new Vector3(0,.15f,1.1f),Quaternion.Euler(0,180,0),Vector3.one*.7f),.7f);}
   for(int i=0;i<wall.Count;i++){var pos=GalleryPosition(i,wall.Count);Vector3 worldPos=stage.TransformPoint(pos);if(Vector3.Angle(head.forward,worldPos-head.position)>43)continue;wall[i].Advance(dt);DrawClip(wall[i],stage.localToWorldMatrix*Matrix4x4.TRS(pos,Quaternion.Euler(0,180,0),Vector3.one*.25f),.25f,true);}
   if(pointer.Active)for(int h=0;h<2;h++)if(pointer.RayVisible(h)){var ray=pointer.HandRay(h);var point=pointer.HandPoint(h);var color=ArcadeArt.Player(borrowActive?BorrowInputPlayer(h):h);batch.Tube(ray.origin,point,.0008f,new Vector4(color.r,color.g,color.b,.5f),Vector4.zero);batch.Sphere(point,.003f,new Vector4(color.r,color.g,color.b,1),Vector4.zero);}
   if(duelActive)DrawDuel();
   if(arcadeMode!=ArcadeMode.None)DrawArcade();
   if(borrowActive)DrawBorrowlight();
   batch.Draw();
   #if SOH_DEVELOPMENT
   if(Time.frameCount%60==0)UpdateDiagnostics();
   if(Time.frameCount%300==0)Debug.Log("SOH_METRICS fps="+(1/Time.smoothDeltaTime).ToString("F1")+" screen="+Screen+" tracked="+hands.Left.Tracked+","+hands.Right.Tracked);
   #endif
  }
  void DrawClip(ClipPlayer p,Matrix4x4 m,float scale,bool lowDetail=false){var pose=p.Pose;var g=Glove.Find(p.Clip?.Glove);if(lowDetail){HandDrawer.DrawSimple(batch,pose.Left,m,scale,g,pose.LeftShown,pose.LeftMitt);HandDrawer.DrawSimple(batch,pose.Right,m,scale,g,pose.RightShown,pose.RightMitt);HandDrawer.DrawHead(batch,pose.HeadPosition,pose.HeadRotation,m,scale,g,pose.HeadShown);return;}HandDrawer.Draw(batch,pose.Left,m,scale,g,pose.LeftShown,pose.LeftMitt);HandDrawer.Draw(batch,pose.Right,m,scale,g,pose.RightShown,pose.RightMitt);HandDrawer.DrawHead(batch,pose.HeadPosition,pose.HeadRotation,m,scale,g,pose.HeadShown);Trails(pose.Left,m,scale,g,2,pose.LeftShown>.95f&&!pose.LeftMitt);Trails(pose.Right,m,scale,g,3,pose.RightShown>.95f&&!pose.RightMitt);if((pose.LeftMitt&&!wasMitt[2])||(pose.RightMitt&&!wasMitt[3]))Cue("mitt_boing");wasMitt[2]=pose.LeftMitt;wasMitt[3]=pose.RightMitt;}
  void Trails(Vector3[] joints,Matrix4x4 space,float scale,Glove glove,int hand,bool visible){
   bool show=visible&&!Ui.ReducedMotion;for(int f=0;f<5;f++){int j=f==0?5:10+(f-1)*5,slot=hand*5+f;var point=space.MultiplyPoint3x4(joints[j]);float distance=(point-trailPoints[slot]).magnitude;if(show&&trailReady[hand]&&distance>.005f*scale&&distance<.1f*scale)batch.Tube(trailPoints[slot],point,.003f*scale,new Vector4(glove.Tip.r,glove.Tip.g,glove.Tip.b,.25f),new Vector4(.35f,0,0,0));trailPoints[slot]=point;}trailReady[hand]=show;
  }
  void Cue(string name){
   #if UNITY_EDITOR
   EditorCue?.Invoke(name);
   #endif
   var clip=SoundBank.Get(name);if(clip!=null)audioSource.PlayOneShot(clip,.4f);}
  void OnApplicationFocus(bool has){focused=has;if(!has){PauseArcade();PauseBorrowlight();}if(!has&&duelActive)PauseDuel();if(!has&&recorder.Recording){recorder.Cancel();Show("Recording paused","Your previous clips are safe");Button("Again",0,-.23f,Countdown);NavHome();}if(has){ResumeArcadeFocus();ResumeBorrowlight();}AudioListener.pause=!has;}
  void OnDestroy(){if(backdropMaterial!=null)Destroy(backdropMaterial);}
  void OnApplicationPause(bool paused){OnApplicationFocus(!paused);}
 }
}
