using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.TestTools;

namespace ShowOfHands.Tests
{
    public class BorrowlightFlowTests
    {
        const BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
        App app;Camera camera;Source source;
        static T Get<T>(App a,string name)=>(T)typeof(App).GetField(name,Flags).GetValue(a);
        static object Call(App a,string name,params object[] args)=>typeof(App).GetMethod(name,Flags).Invoke(a,args);
        sealed class Source:IHandSource
        {
            public Transform Head;public readonly bool[] Pinch=new bool[2],Tracked={true,true},Controller=new bool[2],Suppressed=new bool[2];
            public readonly Vector3?[] Tip=new Vector3?[2],Aim=new Vector3?[2];public readonly Vector3[] Offset=new Vector3[2];readonly Vector3[] pose=new Vector3[26];
            public void Read(HandFrame f){f.HeadPosition=Head.position;f.HeadRotation=Head.rotation;for(int h=0;h<2;h++){
                var hand=f.Hand(h);hand.Tracked=Tracked[h];hand.PinchKnown=true;hand.IsController=Controller[h];hand.SelectPressed=Pinch[h];hand.SelectSuppressed=Suppressed[h];DuelArt.FillPose(pose,RpsMove.Paper,h);var origin=pose[0];
                for(int j=0;j<26;j++)hand.Joints[j]=Controller[h]?Vector3.zero:new Vector3(h==0?-.14f:.14f,-.25f,.29f)+Offset[h]+pose[j]-origin;
                if(Controller[h])hand.Joints[ClipFormat.Palm]=new Vector3(h==0?-.14f:.14f,-.25f,.29f)+Offset[h];
                if(Tip[h].HasValue)hand.Joints[ClipFormat.IndexTip]=Tip[h].Value;hand.AimValid=Aim[h].HasValue;if(hand.AimValid)hand.Aim=new Ray(hand.Joints[0],(Aim[h].Value-hand.Joints[0]).normalized);
            }}
        }
        IEnumerator Boot(){camera=new GameObject("Borrowlight camera").AddComponent<Camera>();camera.tag="MainCamera";camera.gameObject.AddComponent<AudioListener>();app=new GameObject("Borrowlight integration").AddComponent<App>();float until=Time.realtimeSinceStartup+12;while(app.Screen=="Loading"&&Time.realtimeSinceStartup<until)yield return null;Assert.AreEqual("HAND DUEL",app.Screen);source=new Source{Head=camera.transform};typeof(App).GetField("source",Flags).SetValue(app,source);Get<Pointer>(app,"pointer").DwellEnabled=false;yield return new WaitForSeconds(.35f);}
        void Start(string type){Call(app,"BorrowMenu");Call(app,"BorrowStart",Enum.Parse(typeof(App).GetNestedType("ArcadePlayers",BindingFlags.NonPublic),type));}
        Target Card(string label){foreach(var c in app.GetComponentsInChildren<Card>())if(c.Text!=null&&c.Text.Text==label)return c.Target;Assert.Fail("Missing card "+label);return null;}
        IEnumerator Poke(Target target,int h=0){var p=target.Transform.position;var f=target.Transform.forward;source.Tip[h]=p-f*.06f;yield return new WaitForSeconds(.15f);source.Tip[h]=p;yield return new WaitForSeconds(.12f);source.Tip[h]=p-f*.08f;yield return new WaitForSeconds(.4f);source.Tip[h]=null;}
        IEnumerator Grab(int knot,int h=0){source.Pinch[h]=false;source.Aim[h]=Get<Target[]>(app,"borrowNodes")[knot].Transform.position;yield return new WaitForSeconds(.25f);source.Pinch[h]=true;yield return new WaitForSeconds(.15f);Assert.AreEqual(h,Get<int>(app,"borrowDrag"));}
        IEnumerator Turn(int n){yield return Until(()=>Get<BorrowlightGame>(app,"borrowGame").Turn==n);yield return new WaitForSeconds(.4f);}
        [UnityTest] public IEnumerator OneTapLessonClaimsStealsAndReplaysWithoutASendButton()
        {
            yield return Boot();yield return new WaitForSeconds(.7f);Capture("borrowlight-home.png");yield return Poke(Card("Borrowlight"));Assert.AreEqual("BORROWLIGHT",app.Screen);yield return Poke(Card("Learn\none heist"));yield return new WaitForSeconds(.4f);Assert.IsFalse(Get<Card[]>(app,"borrowKnotCards")[0].Target.Enabled,"the first lesson should teach one action, not offer an unproductive move");
            Assert.AreEqual(1,Get<int>(app,"borrowSelected"),"Learn starts with B selected, without a hidden selection step");
            foreach(var c in app.GetComponentsInChildren<Card>())Assert.AreNotEqual("Send pulse",c.Text?.Text);
            var g=Get<BorrowlightGame>(app,"borrowGame");yield return Poke(Get<Card>(app,"borrowLessonDock").Target);Assert.AreEqual(8,g.Knots[1]);Assert.AreEqual(BorrowPhase.Pulse,g.Phase);Capture("borrowlight-pulse.png");
            yield return Turn(1);Assert.Greater(g.Score[0],0);Assert.AreEqual(1,g.Locked);Assert.IsFalse(Get<Card[]>(app,"borrowKnotCards")[1].Target.Enabled);
            Assert.AreEqual(0,Get<int>(app,"borrowSelected"));yield return Poke(Get<Card>(app,"borrowLessonDock").Target,1);Assert.AreEqual(BorrowPhase.Pulse,g.Phase);yield return new WaitForSeconds(.6f);Capture("borrowlight-steal.png");yield return Turn(2);Assert.AreEqual("YOU CAN BORROW LIGHT",app.Screen);int total=g.Score[0]+g.Score[1];Assert.Greater(g.LastSteals,0);Capture("borrowlight-result.png");
            yield return Poke(Card("Pulse replay"));yield return new WaitForSeconds(3.5f);Assert.AreEqual(total,g.Score[0]+g.Score[1]);yield return Poke(Card("Play duel"));Assert.AreEqual(0,Get<int>(app,"borrowStarter"),"a lesson must not silently swap the first real match starter");yield return Poke(Card("Home"));Assert.AreEqual("HAND DUEL",app.Screen);Assert.AreEqual(3,Get<Pointer>(app,"pointer").HandMask);
        }
        [UnityTest] public IEnumerator PinchDragsAKnotInDepthAndTrackingLossNeverCommitsIt()
        {
            yield return Boot();Start("Hands");yield return new WaitForSeconds(.6f);var target=Get<Target[]>(app,"borrowNodes")[1];yield return Grab(1);
            source.Offset[0]=new Vector3(0,.065f,.07f);yield return new WaitForSeconds(.3f);int previewSlot=Get<int>(app,"borrowSlot");Assert.AreNotEqual(4,previewSlot);var g=Get<BorrowlightGame>(app,"borrowGame");Assert.AreEqual(0,g.Turn);
            source.Tracked[0]=false;source.Pinch[0]=false;yield return new WaitForSeconds(.3f);Assert.AreEqual(-1,Get<int>(app,"borrowDrag"));Assert.AreEqual(0,g.Turn);Assert.AreEqual(BorrowPhase.Planning,g.Phase);
            source.Tracked[0]=true;source.Pinch[0]=true;source.Aim[0]=target.Transform.position;yield return new WaitForSeconds(.3f);Assert.AreEqual(-1,Get<int>(app,"borrowDrag"),"regaining tracking with a held pinch must not create a grab");
            yield return Grab(1);Assert.AreEqual(previewSlot,Get<int>(app,"borrowSlot"),"re-grabbing a cancelled draft preserves it");source.Pinch[0]=false;yield return new WaitForSeconds(.25f);Assert.AreEqual(0,g.Turn,"releasing an unchanged re-grab must not spend a turn");Assert.AreEqual(BorrowPhase.Planning,g.Phase);
        }
        [UnityTest] public IEnumerator InactiveHandCannotMoveAndHeadMotionCannotMoveTheArena()
        {
            yield return Boot();Start("Hands");yield return new WaitForSeconds(.5f);yield return Poke(Card("B"),1);Assert.AreEqual(-1,Get<int>(app,"borrowSelected"));var stage=Get<Transform>(app,"stage");var position=stage.position;camera.transform.position+=Vector3.right*.08f;camera.transform.rotation=Quaternion.Euler(10,30,0);yield return null;Assert.AreEqual(position,stage.position);
            yield return Poke(Card("B"),0);Assert.AreEqual(1,Get<int>(app,"borrowSelected"));Call(app,"OnApplicationFocus",false);Assert.IsTrue(Get<bool>(app,"borrowPaused"));Call(app,"OnApplicationFocus",true);Assert.IsFalse(Get<bool>(app,"borrowPaused"));
        }
        [UnityTest] public IEnumerator ControllerReleaseLaunchesOnceWithoutASecondButton()
        {
            yield return Boot();Start("Hands");source.Controller[0]=true;yield return new WaitForSeconds(.6f);yield return Grab(1);
            source.Offset[0]=new Vector3(0,.065f,.07f);yield return new WaitForSeconds(.3f);Assert.AreNotEqual(4,Get<int>(app,"borrowSlot"));var g=Get<BorrowlightGame>(app,"borrowGame");Assert.AreEqual(BorrowPhase.Planning,g.Phase,"a held move is still a preview");source.Pinch[0]=false;yield return new WaitForSeconds(.2f);Assert.AreEqual(BorrowPhase.Pulse,g.Phase);yield return Turn(1);source.Pinch[0]=true;yield return new WaitForSeconds(.2f);source.Pinch[0]=false;yield return new WaitForSeconds(.3f);Assert.AreEqual(1,g.Turn);
        }
        [UnityTest] public IEnumerator TouchThenHoldPinchGrabsFromTheSideWithoutAnAimRay()
        {
            yield return Boot();Start("Lesson");yield return new WaitForSeconds(.5f);var node=Get<Target[]>(app,"borrowNodes")[1];var p=node.Transform.position;
            source.Tip[1]=p+Vector3.right*.075f;yield return new WaitForSeconds(.1f);source.Tip[1]=p+Vector3.right*.015f;yield return new WaitForSeconds(.1f);
            source.Pinch[1]=true;yield return new WaitForSeconds(.1f);Assert.AreEqual(1,Get<int>(app,"borrowDrag"),"touch followed by pinch must grab, even during the UI select cooldown");
            var grip=Get<Vector3>(app,"borrowGrip");
            source.Tip[1]=p+new Vector3(.015f,.08f,.07f);yield return new WaitForSeconds(.2f);Assert.AreEqual(8,Get<int>(app,"borrowSlot"));
            var moved=node.Transform.position;source.Tip[1]=moved+Vector3.right*.065f;yield return new WaitForSeconds(.1f);source.Tip[1]=node.Transform.position;yield return new WaitForSeconds(.15f);Assert.AreEqual(grip,Get<Vector3>(app,"borrowGrip"),"touching the same moving knot while holding must not restart its drag origin");
            Capture("borrowlight-preview.png");source.Pinch[1]=false;source.Tip[1]=null;float end=Time.realtimeSinceStartup+1;while(Get<BorrowlightGame>(app,"borrowGame").Phase==BorrowPhase.Planning&&Time.realtimeSinceStartup<end)yield return null;Assert.AreEqual(BorrowPhase.Pulse,Get<BorrowlightGame>(app,"borrowGame").Phase,"release: selected="+Get<int>(app,"borrowSelected")+" slot="+Get<int>(app,"borrowSlot")+" drag="+Get<int>(app,"borrowDrag")+" origin="+Get<int>(app,"borrowDragSlot")+" message="+Get<string>(app,"borrowMessage"));yield return Turn(1);
        }
        [UnityTest] public IEnumerator DockTapLaunchesOnceAndHeldTouchCannotPlayTheNextTurn()
        {
            yield return Boot();Start("Lesson");yield return new WaitForSeconds(.5f);yield return Grab(1);Assert.IsFalse(Get<Card>(app,"borrowLessonDock").Target.Enabled,"a held drag cannot accidentally fire by brushing a dock");Assert.IsFalse(Get<Target>(app,"borrowGoalTarget").Enabled);
            yield return Poke(Card("B"),1);Assert.AreEqual(-1,Get<int>(app,"borrowDrag"),"selecting a card cancels the old drag without launching");var g=Get<BorrowlightGame>(app,"borrowGame");Assert.AreEqual(BorrowPhase.Planning,g.Phase);
            var dock=Get<Card>(app,"borrowLessonDock");yield return Poke(dock.Target,1);Assert.AreEqual(BorrowPhase.Pulse,g.Phase);source.Tip[1]=dock.transform.position;source.Pinch[0]=false;yield return Turn(1);yield return new WaitForSeconds(.5f);Assert.AreEqual(1,g.Turn);Assert.AreEqual(BorrowPhase.Planning,g.Phase,"keeping a finger on the reused lesson button must not play A");
        }
        [UnityTest] public IEnumerator OfflineBotPlaysBothHeistsAndPcCannotControlQuest()
        {
            yield return Boot();Call(app,"BorrowMenu");Call(app,"BorrowStart",Enum.Parse(typeof(App).GetNestedType("ArcadePlayers",BindingFlags.NonPublic),"PC"));Assert.IsTrue(Get<bool>(app,"borrowLobby"));
            Start("Solo");yield return new WaitForSeconds(.5f);var g=Get<BorrowlightGame>(app,"borrowGame");
            float end=Time.realtimeSinceStartup+90;while(g.Phase!=BorrowPhase.Result&&Time.realtimeSinceStartup<end){
                if(g.Phase==BorrowPhase.Planning&&g.Player==0){yield return new WaitForSeconds(.4f);g.BestMove(out int k,out int s);Call(app,"BorrowSelect",k,false);yield return Poke(Get<Card[]>(app,"borrowDocks")[s].Target);}
                else yield return new WaitForSeconds(.1f);
            }
            Assert.AreEqual(12,g.Turn);Assert.AreEqual(1,g.Heist);Assert.IsTrue(Get<bool>(app,"borrowFinished"));Assert.IsFalse(Get<ArenaRelay>(app,"arenaRelay").Packet.pc);
            Assert.IsFalse((bool)Call(app,"BorrowCommand","weave:1234:0:1:8"));
        }
        [UnityTest] public IEnumerator UnchangedGrabAndSingleFramePinchFlickerNeverLaunch()
        {
            yield return Boot();Start("Hands");yield return new WaitForSeconds(.5f);yield return Grab(1);var g=Get<BorrowlightGame>(app,"borrowGame");source.Pinch[0]=false;yield return new WaitForSeconds(.25f);Assert.AreEqual(BorrowPhase.Planning,g.Phase);Assert.AreEqual(0,g.Turn);
            yield return Grab(1);source.Offset[0]=new Vector3(0,.065f,.07f);yield return new WaitForSeconds(.2f);source.Pinch[0]=false;yield return null;source.Pinch[0]=true;yield return null;yield return new WaitForSeconds(.2f);Assert.AreEqual(BorrowPhase.Planning,g.Phase,"one bad pinch frame must not spend the move");
            source.Pinch[0]=false;yield return new WaitForSeconds(.2f);Assert.AreEqual(BorrowPhase.Pulse,g.Phase);yield return Turn(1);Assert.Greater(g.Score[0],0);
        }
        [UnityTest] public IEnumerator SystemGestureAndFocusLossCancelWithoutLaunching()
        {
            yield return Boot();Start("Hands");yield return new WaitForSeconds(.5f);yield return Grab(1);source.Offset[0]=new Vector3(0,.065f,.07f);yield return new WaitForSeconds(.2f);var g=Get<BorrowlightGame>(app,"borrowGame");source.Suppressed[0]=true;source.Pinch[0]=false;yield return new WaitForSeconds(.3f);Assert.AreEqual(-1,Get<int>(app,"borrowDrag"));Assert.AreEqual(BorrowPhase.Planning,g.Phase);Assert.AreEqual(0,Get<Pointer>(app,"pointer").HandMask);
            source.Suppressed[0]=false;source.Pinch[0]=true;yield return new WaitForSeconds(.2f);Assert.AreEqual(-1,Get<int>(app,"borrowDrag"));yield return Grab(1);source.Offset[0]=new Vector3(0,-.065f,-.07f);yield return new WaitForSeconds(.2f);Call(app,"OnApplicationFocus",false);Call(app,"OnApplicationFocus",false);source.Pinch[0]=false;Call(app,"OnApplicationFocus",true);yield return new WaitForSeconds(.5f);Assert.IsFalse(Get<bool>(app,"borrowPaused"),"paired focus/pause callbacks must not erase the automatic-resume flag");Assert.AreEqual(BorrowPhase.Planning,g.Phase);Assert.AreEqual(0,g.Turn);Assert.AreEqual(0,g.Score[0]+g.Score[1]);
            source.Aim[0]=Get<Card[]>(app,"borrowDocks")[8].transform.position;yield return new WaitForSeconds(.3f);source.Suppressed[0]=true;yield return new WaitForSeconds(.2f);source.Suppressed[0]=false;source.Pinch[0]=true;yield return new WaitForSeconds(.2f);Assert.AreEqual(BorrowPhase.Planning,g.Phase,"returning from the system menu with a held pinch cannot play a dock");source.Pinch[0]=false;yield return new WaitForSeconds(.2f);source.Pinch[0]=true;yield return new WaitForSeconds(.2f);Assert.AreEqual(BorrowPhase.Pulse,g.Phase,"a fresh pinch still works after the system gesture");
        }
        [UnityTest] public IEnumerator DockHoverPreviewsButOnlyATapLaunches()
        {
            yield return Boot();Start("Hands");yield return new WaitForSeconds(.5f);yield return Poke(Card("B"));var dock=Get<Card[]>(app,"borrowDocks")[8];source.Aim[0]=dock.transform.position;yield return new WaitForSeconds(.35f);var g=Get<BorrowlightGame>(app,"borrowGame");Assert.AreEqual(8,Get<int>(app,"borrowSlot"));Assert.AreEqual(4,g.Knots[1]);Assert.AreEqual(0,g.Turn);Assert.AreEqual(BorrowPhase.Planning,g.Phase);Assert.Greater(Get<int>(app,"borrowPreviewGain"),0);
            source.Pinch[0]=true;yield return new WaitForSeconds(.15f);Assert.AreEqual(BorrowPhase.Pulse,g.Phase);source.Pinch[0]=false;yield return Turn(1);Assert.Greater(g.Score[0],0);
        }
        [UnityTest] public IEnumerator PassTheQuestPlaysBothHeistsWithReadyEitherHandAndSafeFocusResume()
        {
            yield return Boot();var cues=new List<string>();app.EditorCue=cues.Add;yield return Poke(Card("Borrowlight"));yield return Poke(Card("Duel\n2 players"));Capture("borrowlight-duel-menu.png");yield return Poke(Card("Pass the\nQuest"));var g=Get<BorrowlightGame>(app,"borrowGame");Assert.IsTrue(Get<bool>(app,"borrowHandoff"));Assert.AreEqual(0,g.Turn);Capture("borrowlight-handoff.png");
            float end=Time.realtimeSinceStartup+110;
            while(g.Phase!=BorrowPhase.Result&&Time.realtimeSinceStartup<end){
                if(Get<bool>(app,"borrowHandoff")){
                    int oldTurn=g.Turn;yield return new WaitForSeconds(.25f);Assert.AreEqual(oldTurn,g.Turn,"handoff waits for readiness");if(g.Turn>0&&g.LastSteals>0)CollectionAssert.Contains(cues,"borrow_steal","a stealing pulse needs its own feedback after a handoff");cues.Clear();
                    if(g.Turn==1){Call(app,"OnApplicationFocus",false);Call(app,"OnApplicationFocus",false);camera.transform.position+=new Vector3(.05f,.03f,0);Call(app,"OnApplicationFocus",true);yield return null;Assert.IsFalse(Get<bool>(app,"borrowPaused"));}
                    yield return Poke(Card("I'm ready"),1-g.Player);Assert.IsFalse(Get<bool>(app,"borrowHandoff"));Assert.AreEqual(3,Get<Pointer>(app,"pointer").HandMask);var stage=Get<Transform>(app,"stage");Assert.Less((stage.position-camera.transform.position-Vector3.down*.16f).magnitude,.001f);Assert.AreEqual(g.Player,(int)Call(app,"BorrowInputPlayer",0));Assert.AreEqual(g.Player,(int)Call(app,"BorrowInputPlayer",1));
                    int h=1-g.Player;g.BestMove(out int k,out int s);yield return Poke(Card(((char)('A'+k)).ToString()),h);yield return Poke(Get<Card[]>(app,"borrowDocks")[s].Target,h);Assert.AreEqual(BorrowPhase.Pulse,g.Phase);
                }else yield return new WaitForSeconds(.1f);
            }
            Assert.AreEqual(12,g.Turn);Assert.AreEqual(1,g.Heist);Assert.IsTrue(Get<bool>(app,"borrowFinished"));Assert.Greater(g.Score[0]+g.Score[1],0);yield return Poke(Card("Rematch"));Assert.IsTrue(Get<bool>(app,"borrowHandoff"));Assert.AreEqual(1,g.Player);Assert.AreEqual(0,g.Turn);
        }
        static IEnumerator Until(Func<bool> condition){float end=Time.realtimeSinceStartup+12;while(!condition()&&Time.realtimeSinceStartup<end)yield return null;Assert.IsTrue(condition());}
        void Capture(string name){var pose=camera.transform.rotation;camera.transform.rotation=Quaternion.Euler(18,0,0);var target=new RenderTexture(1800,1200,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB){antiAliasing=4};camera.targetTexture=target;camera.aspect=1.5f;camera.fieldOfView=65;camera.Render();var old=RenderTexture.active;RenderTexture.active=target;var im=new Texture2D(1800,1200,TextureFormat.RGB24,false);im.ReadPixels(new Rect(0,0,1800,1200),0,0);im.Apply();File.WriteAllBytes(Path.Combine(Application.dataPath,"../Logs/"+name),im.EncodeToPNG());RenderTexture.active=old;camera.targetTexture=null;camera.transform.rotation=pose;UnityEngine.Object.Destroy(im);UnityEngine.Object.Destroy(target);}
        [UnityTearDown] public IEnumerator Cleanup(){if(app!=null)UnityEngine.Object.Destroy(app.gameObject);if(camera!=null)UnityEngine.Object.Destroy(camera.gameObject);yield return null;}
    }
}
