using System;
using System.Collections;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.TestTools;

namespace ShowOfHands.Tests
{
    public class ArcadeFlowTests
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        App app;Camera cam;Source source;
        static object Call(App a,string name,params object[] args)=>typeof(App).GetMethod(name,Flags).Invoke(a,args);
        static T Get<T>(App a,string name)=>(T)typeof(App).GetField(name,Flags).GetValue(a);
        void StartMode(string method,string players){var type=typeof(App).GetNestedType("ArcadePlayers",BindingFlags.NonPublic);Call(app,method,Enum.Parse(type,players));}
        sealed class Source:IHandSource
        {
            public readonly Vector3[] Offset=new Vector3[2];public readonly bool[] Pinch=new bool[2],Tracked={true,true};
            public readonly RpsMove[] Pose={RpsMove.Paper,RpsMove.Paper};readonly Vector3[] raw=new Vector3[26];public Transform Head;
            public readonly Vector3?[] Tip=new Vector3?[2],AimTarget=new Vector3?[2];
            public readonly bool[] Controller=new bool[2],Rotate=new bool[2],KnownPinch=new bool[2];public readonly Vector2[] Stick=new Vector2[2];
            public void Read(HandFrame f)
            {
                f.HeadPosition=Head.position;f.HeadRotation=Head.rotation;
                for(int h=0;h<2;h++){var hand=f.Hand(h);hand.Tracked=Tracked[h];hand.IsController=Controller[h];hand.PinchKnown=KnownPinch[h];hand.SelectPressed=Pinch[h];hand.RotatePressed=Rotate[h];hand.Stick=Stick[h];DuelArt.FillPose(raw,Pose[h],h);if(Pinch[h])raw[5]=raw[10];var origin=raw[0];for(int j=0;j<26;j++)hand.Joints[j]=new Vector3(h==0?-.18f:.18f,-.28f,.30f)+Offset[h]+raw[j]-origin;if(Tip[h].HasValue)hand.Joints[ClipFormat.IndexTip]=Tip[h].Value;hand.AimValid=AimTarget[h].HasValue;if(hand.AimValid)hand.Aim=new Ray(hand.Joints[0],(AimTarget[h].Value-hand.Joints[0]).normalized);}
            }
        }
        IEnumerator Boot()
        {
            cam=new GameObject("Arcade v4 test camera").AddComponent<Camera>();cam.tag="MainCamera";cam.gameObject.AddComponent<AudioListener>();
            app=new GameObject("Arcade v4 flow").AddComponent<App>();float end=Time.realtimeSinceStartup+12;while(app.Screen=="Loading"&&Time.realtimeSinceStartup<end)yield return null;
            Assert.AreEqual("HAND DUEL",app.Screen);source=new Source{Head=cam.transform};typeof(App).GetField("source",Flags).SetValue(app,source);Get<Pointer>(app,"pointer").DwellEnabled=false;yield return new WaitForSeconds(.2f);
        }
        IEnumerator TicMove(int hand,int cell)
        {
            source.Pinch[hand]=false;source.AimTarget[hand]=Get<Target[]>(app,"ticTargets")[cell].Transform.position;yield return new WaitForSeconds(.8f);
            source.Pinch[hand]=true;yield return new WaitForSeconds(.15f);source.Pinch[hand]=false;yield return new WaitForSeconds(.2f);source.AimTarget[hand]=null;
        }
        [UnityTest] public IEnumerator DuplicateFocusCallbacksResumeAutoPauseAndPreserveManualPause()
        {
            yield return Boot();StartMode("TicStart","Hands");yield return new WaitForSeconds(.2f);
            Call(app,"OnApplicationFocus",false);Call(app,"OnApplicationPause",true);
            Assert.IsTrue(Get<bool>(app,"arcadePaused"));
            Call(app,"OnApplicationPause",false);Call(app,"OnApplicationFocus",true);yield return null;
            Assert.IsFalse(Get<bool>(app,"arcadePaused"),"Duplicate focus/pause loss must not erase automatic resume.");
            Call(app,"ArcadePause");Assert.IsTrue(Get<bool>(app,"arcadePaused"));
            Call(app,"OnApplicationFocus",false);Call(app,"OnApplicationFocus",false);Call(app,"OnApplicationFocus",true);yield return null;
            Assert.IsTrue(Get<bool>(app,"arcadePaused"),"A deliberate user pause must remain paused.");
        }
        [UnityTest] public IEnumerator BothTrackedHandsPlayAnActualTicTacToeWinAndRematch()
        {
            yield return Boot();Capture("v4-home.png");Call(app,"TicMenu");StartMode("TicStart","Hands");yield return new WaitForSeconds(.8f);
            var game=Get<TicTacToeGame>(app,"ticGame");source.Pinch[1]=true;yield return new WaitForSeconds(.5f);source.Pinch[1]=false;Assert.AreEqual(0,game.Board[4],"inactive hand cannot place");
            foreach(int cell in new[]{0,3,1,4,2}){int player=game.Player;yield return TicMove(player,cell);Assert.AreEqual(player+1,game.Board[cell],"cell "+cell);}
            Assert.AreEqual(1,game.Winner);Assert.AreEqual("X WINS!",app.Screen);Assert.AreEqual(1,game.Wins[0]);Capture("v4-tictactoe-win.png");Save("v4-tictactoe-result.json");
            Call(app,"ArcadeFilm");Assert.IsTrue(Get<bool>(app,"arcadeFilm"));Call(app,"Home");Assert.IsFalse(Get<bool>(app,"arcadeFilm"));Assert.AreEqual(3,Get<Pointer>(app,"pointer").HandMask);
            game.NextRound();Assert.AreEqual(1,game.Player);Assert.AreEqual(1,game.Wins[0]);
        }
        [UnityTest] public IEnumerator TicTacToeLessonCanBeCompletedWithARealPinchInput()
        {
            yield return Boot();Call(app,"TicMenu");StartMode("TicStart","Lesson");yield return new WaitForSeconds(.8f);Capture("v4-tictactoe-lesson.png");yield return TicMove(0,2);
            var game=Get<TicTacToeGame>(app,"ticGame");Assert.IsTrue(game.Ended);Assert.AreEqual(0,game.Wins[0]);
        }
        IEnumerator Poke(int h,Target target)
        {
            var point=target.Transform.position;var forward=target.Transform.forward;source.Tip[h]=point-forward*.05f;yield return new WaitForSeconds(.12f);
            source.Tip[h]=point;yield return new WaitForSeconds(.12f);source.Tip[h]=point-forward*.08f;yield return new WaitForSeconds(.35f);source.Tip[h]=null;
        }
        Target CardTarget(string label){foreach(var card in app.GetComponentsInChildren<Card>())if(card.Text!=null&&card.Text.Text==label)return card.Target;Assert.Fail("Missing card "+label);return null;}
        [UnityTest] public IEnumerator FingertipNavigatesMenusAndPlacesTileWhileHeadLooksAway()
        {
            yield return Boot();yield return Poke(1,CardTarget("Tic-tac-toe"));Assert.AreEqual("TIC-TAC-TOE",app.Screen);
            yield return Poke(1,CardTarget("Learn\none move"));yield return new WaitForSeconds(1);
            var stage=Get<Transform>(app,"stage");var position=stage.position;var rotation=stage.rotation;
            cam.transform.rotation=Quaternion.Euler(0,100,0);cam.transform.position+=Vector3.right*.06f;yield return new WaitForSeconds(2.5f);
            Assert.AreEqual(position,stage.position,"head turning/moving must not drag the board");Assert.AreEqual(rotation,stage.rotation);
            var tile=Get<Target[]>(app,"ticTargets")[2];Assert.Less(Vector3.Distance(cam.transform.position,tile.Transform.position),.55f,"tile must be reachable");
            yield return Poke(1,tile);Assert.IsTrue(Get<TicTacToeGame>(app,"ticGame").Ended);Assert.AreEqual("X WINS!",app.Screen);
        }
        [UnityTest] public IEnumerator ControllerRayTriggerPlacesTileAndStickButtonsControlBlocks()
        {
            yield return Boot();source.Controller[0]=true;source.AimTarget[0]=CardTarget("Tic-tac-toe").Transform.position;yield return new WaitForSeconds(.2f);source.Pinch[0]=true;yield return new WaitForSeconds(.15f);source.Pinch[0]=false;source.AimTarget[0]=null;
            Assert.AreEqual("TIC-TAC-TOE",app.Screen);StartMode("TicStart","Lesson");yield return TicMove(0,2);Assert.AreEqual("X WINS!",app.Screen);
            Call(app,"ShapeMenu");StartMode("BlocksStart","Hands");var game=Get<FallingBlocksGame>(app,"blockGame");for(int seed=17;game.Kind==1;seed++)game.Start(false,false,seed);
            yield return new WaitForSeconds(1);int old=game.Rotation;source.Rotate[0]=true;yield return new WaitForSeconds(.2f);source.Rotate[0]=false;Assert.AreEqual((old+1)%4,game.Rotation);
            int x=game.X;source.Stick[0]=Vector2.left;yield return new WaitForSeconds(.2f);source.Stick[0]=Vector2.zero;Assert.Less(game.X,x);
            source.Pinch[0]=true;yield return new WaitForSeconds(.5f);source.Pinch[0]=false;Assert.AreEqual(1,game.Pieces);Assert.AreEqual(1,game.Player);
        }
        [UnityTest] public IEnumerator FallingBlocksLessonClearsARowAndHandDuelRespectsTrackingPause()
        {
            yield return Boot();Call(app,"ShapeMenu");StartMode("BlocksStart","Lesson");yield return new WaitForSeconds(1);Capture("v4-aftershape-lesson.png");Save("v4-aftershape-lesson.json");
            source.Pinch[0]=true;yield return new WaitForSeconds(.5f);source.Pinch[0]=false;yield return new WaitForSeconds(.8f);
            var game=Get<FallingBlocksGame>(app,"blockGame");Assert.AreEqual(1,game.Rows);Assert.AreEqual(BlockPhase.Result,game.Phase);Assert.AreEqual("ROW CLEARED!",app.Screen);Capture("v4-aftershape-row-clear.png");
            StartMode("BlocksStart","Hands");game.Start(false,false,17);for(int seed=18;game.Kind==1;seed++)game.Start(false,false,seed);
            yield return new WaitForSeconds(.9f);int rotation=game.Rotation;source.Pose[0]=RpsMove.Rock;yield return new WaitForSeconds(.45f);Assert.AreEqual((rotation+1)%4,game.Rotation);Assert.AreEqual(0,game.Pieces,"a fist must rotate without dropping");
            source.Pose[0]=RpsMove.Paper;yield return new WaitForSeconds(.3f);source.Pinch[0]=true;yield return new WaitForSeconds(.5f);source.Pinch[0]=false;Assert.AreEqual(1,game.Pieces);Assert.AreEqual(1,game.Player);
            source.Tracked[1]=false;yield return new WaitForSeconds(.2f);int stopped=game.Y;yield return new WaitForSeconds(1.7f);Assert.AreEqual(stopped,game.Y);source.Tracked[1]=true;yield return new WaitForSeconds(.9f);
            Capture("v4-aftershape-duel.png");Save("v4-aftershape-duel.json");Call(app,"Home");Call(app,"DuelPractice");Assert.AreEqual("LEARN YOUR WEAPONS",app.Screen);
        }
        [UnityTest] public IEnumerator SdkPinchDropsImmediatelyEvenWithOtherFingersCurled()
        {
            yield return Boot();Call(app,"ShapeMenu");StartMode("BlocksStart","Hands");source.KnownPinch[0]=true;
            yield return new WaitForSeconds(.4f);var game=Get<FallingBlocksGame>(app,"blockGame");
            source.Pose[0]=RpsMove.Rock;source.Pinch[0]=true;yield return null;yield return null;
            Assert.AreEqual(1,game.Pieces,"SDK-confirmed pinch must drop on its first observed frame, not wait for a hold or become a rotation");
            yield return new WaitForSeconds(.4f);Assert.AreEqual(1,game.Pieces,"held pinch must not repeat");
        }
        [Serializable] sealed class Join {public string token;}
        [UnityTest] public IEnumerator AuthenticatedPCPlayerPlacesOAndDropsTheirOwnBlock()
        {
            if(Environment.GetEnvironmentVariable("SOH_TEST_ARENA")!="1")Assert.Ignore("Start local relay; set SOH_TEST_ARENA=1");
            yield return Boot();var relay=Get<ArenaRelay>(app,"arenaRelay");yield return Until(()=>relay.Connected);string token=null;
            yield return Http("/api/join","{\"code\":\""+relay.Code+"\"}",null,text=>token=JsonUtility.FromJson<Join>(text).token);var beat=app.StartCoroutine(Heartbeat(token));yield return Until(()=>relay.PartnerPresent);
            Call(app,"TicMenu");StartMode("TicStart","PC");yield return new WaitForSeconds(.8f);yield return TicMove(0,4);var tic=Get<TicTacToeGame>(app,"ticGame");Assert.AreEqual(1,tic.Player);yield return new WaitForSeconds(.5f);
            yield return Command(token,"place:0");yield return Until(()=>tic.Board[0]==2);Assert.AreEqual(0,tic.Player);yield return Command(token,"place:1");yield return new WaitForSeconds(.3f);Assert.AreEqual(0,tic.Board[1],"PC cannot play X's turn");
            Capture("v4-tictactoe-pc.png");Save("v4-tictactoe-pc.json");yield return Command(token,"pause");yield return Until(()=>Get<bool>(app,"arcadePaused"));yield return Command(token,"resume");yield return Until(()=>!Get<bool>(app,"arcadePaused"));
            StartMode("BlocksStart","PC");yield return new WaitForSeconds(.9f);source.Pinch[0]=true;yield return new WaitForSeconds(.5f);source.Pinch[0]=false;
            var blocks=Get<FallingBlocksGame>(app,"blockGame");Assert.AreEqual(1,blocks.Player);yield return new WaitForSeconds(.7f);yield return Command(token,"drop");yield return Until(()=>blocks.Pieces==2);Assert.AreEqual(0,blocks.Player);
            yield return Command(token,"drop");yield return new WaitForSeconds(.4f);Assert.AreEqual(2,blocks.Pieces,"PC cannot drop the headset player's piece");app.StopCoroutine(beat);
        }
        static IEnumerator Command(string token,string action)=>Http("/api/command","{\"command\":\""+action+"\"}",token,null);
        static IEnumerator Until(Func<bool> condition){float end=Time.realtimeSinceStartup+10;while(!condition()&&Time.realtimeSinceStartup<end)yield return null;Assert.IsTrue(condition(),"Expected state did not arrive");}
        static IEnumerator Heartbeat(string token){while(true){yield return Http("/api/watch",null,token,null);yield return new WaitForSecondsRealtime(.3f);}}
        static IEnumerator Http(string path,string body,string token,Action<string> use)
        {
            using(var r=new UnityWebRequest((Environment.GetEnvironmentVariable("SOH_ARENA_TEST_ORIGIN")??"http://127.0.0.1:8790")+path,body==null?"GET":"POST"))
            {if(body!=null){r.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));r.SetRequestHeader("Content-Type","application/json");}if(token!=null)r.SetRequestHeader("Authorization","Bearer "+token);r.downloadHandler=new DownloadHandlerBuffer();r.timeout=3;yield return r.SendWebRequest();Assert.AreEqual(UnityWebRequest.Result.Success,r.result,r.error);use?.Invoke(r.downloadHandler.text);}
        }
        void Capture(string name)
        {
            var target=new RenderTexture(1440,1080,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB){antiAliasing=4};cam.targetTexture=target;cam.aspect=4f/3;cam.fieldOfView=65;cam.Render();var old=RenderTexture.active;RenderTexture.active=target;
            var image=new Texture2D(1440,1080,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1440,1080),0,0);image.Apply();System.IO.File.WriteAllBytes(System.IO.Path.Combine(Application.dataPath,"../Logs/"+name),image.EncodeToPNG());RenderTexture.active=old;cam.targetTexture=null;UnityEngine.Object.Destroy(image);UnityEngine.Object.Destroy(target);
        }
        void Save(string name){Call(app,"PublishArena");System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath,"../Logs/"+name),JsonUtility.ToJson(Get<ArenaRelay>(app,"arenaRelay").Packet));}
        [UnityTearDown] public IEnumerator Cleanup(){if(app!=null)UnityEngine.Object.Destroy(app.gameObject);if(cam!=null)UnityEngine.Object.Destroy(cam.gameObject);yield return null;}
    }
}
