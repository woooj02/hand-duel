using System;
using UnityEngine;

namespace ShowOfHands
{
    public sealed partial class App
    {
        readonly BorrowlightGame borrowGame=new BorrowlightGame(),borrowAttract=new BorrowlightGame();
        readonly int[] borrowPreview=new int[3];readonly float[] borrowHits=new float[BorrowlightGame.LightCount];
        readonly Target[] borrowNodes=new Target[3];readonly Label[] borrowNodeLabels=new Label[3];
        readonly Label[] borrowPopups=new Label[BorrowlightGame.LightCount];
        readonly Card[] borrowKnotCards=new Card[3],borrowDocks=new Card[9];
        readonly PinchDetector[] borrowPinch={new PinchDetector(),new PinchDetector()};
        readonly bool[] borrowGrabArmed={true,true};
        bool borrowActive,borrowLobby,borrowPaused,borrowAutoPause,borrowFinished,borrowReplay,borrowPass,borrowHandoff;
        ArcadePlayers borrowPlayers;int borrowSelected=-1,borrowSlot=4,borrowDrag=-1,borrowDragKnot=-1,borrowDragSlot,borrowTurn=-1,borrowClaims,borrowStarter,borrowSeed,borrowMatch;
        int borrowPreviewGain,borrowPreviewSteals,borrowWins0,borrowWins1,borrowStealsHeard;
        Vector3 borrowGrip,borrowGrabPoint;float borrowGate,borrowBotAt,borrowReplayAt,borrowLastCue,borrowAttractAt,borrowLabelsAt;
        float borrowReleaseAt=-1;
        Label borrowStatus,borrowScore,borrowHint,borrowGain,borrowAction;Card borrowPause,borrowLessonDock;Target borrowGoalTarget;string borrowMessage="";
        Matrix4x4 BorrowMatrix=>stage.localToWorldMatrix*Matrix4x4.TRS(new Vector3(-.10f,-.015f,1.16f),Quaternion.Euler(-17,0,0),Vector3.one*.88f);
        int BorrowHand=>borrowPlayers==ArcadePlayers.Hands&&!borrowPass?borrowGame.Player:borrowDrag>=0?borrowDrag:hands.Left.Tracked?0:1;
        int BorrowInputPlayer(int h)=>borrowLobby||borrowPlayers==ArcadePlayers.Hands&&!borrowPass?h:borrowPlayers==ArcadePlayers.Solo?0:borrowGame.Player;
        Glove BorrowGlove(int h)=>Glove.Find(BorrowInputPlayer(h)==0?"violet":"mint");
        bool BorrowCanEdit=>borrowActive&&!borrowLobby&&!borrowHandoff&&!borrowPaused&&!borrowFinished&&borrowGame.Phase==BorrowPhase.Planning&&clock>=borrowGate;
        bool BorrowLesson=>borrowPlayers==ArcadePlayers.Lesson;
        int BorrowGoalKnot=>borrowGame.Turn==0?1:0;
        int BorrowGoalSlot=>borrowGame.Turn==0?8:0;
        bool BorrowValidPreview=>borrowGame.Legal(borrowSelected,borrowSlot)&&(!BorrowLesson||borrowSelected==BorrowGoalKnot&&borrowSlot==BorrowGoalSlot);
        void BorrowTheme(){backdropMaterial.SetColor("_Color",new Color(.0035f,.008f,.015f));backdropMaterial.SetColor("_Glow",new Color(.018f,.045f,.070f));}
        void InitBorrowlight(){borrowGame.Start();borrowAttract.Start();borrowMatch=UnityEngine.Random.Range(1000,99999);borrowWins0=PlayerPrefs.GetInt("borrowWins0");borrowWins1=PlayerPrefs.GetInt("borrowWins1");borrowSeed=PlayerPrefs.GetInt("borrowSeed");
            #if BORROWLIGHT_BENCHMARK && !UNITY_EDITOR
            StartCoroutine(RunBorrowBenchmark());
            #endif
        }
        void CloseBorrowlight(){borrowActive=borrowHandoff=false;borrowDrag=borrowDragKnot=-1;borrowReplay=false;borrowGoalTarget=null;borrowLessonDock=null;for(int k=0;k<3;k++){borrowNodes[k]=null;borrowNodeLabels[k]=null;}pointer.HandMask=3;}
        void BorrowMenu()
        {
            Show("BORROWLIGHT","ONE RIBBON. TWO RIVALS. BORROW THEIR NEXT MOVE.");ArcadeHeader();BorrowTheme();borrowPlayers=ArcadePlayers.Hands;borrowActive=borrowLobby=true;borrowPass=borrowPaused=borrowFinished=false;borrowGame.Start(borrowSeed);
            Ui.Label(view,"PINCH-HOLD a knot. Move it. RELEASE to launch.\nYour rival inherits the ribbon you leave behind.",.031f,Palette.Text,new Vector3(0,.20f,1.1f));
            Card.Create(view,pointer,new Vector3(-.34f,-.33f,1.1f),new Vector2(.31f,.18f),"Learn\none heist",null,()=>BorrowStart(ArcadePlayers.Lesson));
            Card.Create(view,pointer,new Vector3(0,-.33f,1.1f),new Vector2(.31f,.18f),"Duel\n2 players",null,BorrowDuelMenu);
            Card.Create(view,pointer,new Vector3(.34f,-.33f,1.1f),new Vector2(.31f,.18f),"Play with\nbot",null,()=>BorrowStart(ArcadePlayers.Solo));
            Ui.Label(view,"NO NETWORK NEEDED\nCLAIM +1  /  STEAL +2, THEN BURN OUT  /  LAST KNOT COOLS",.021f,Palette.Accent,new Vector3(0,-.50f,1.1f));ArcadeNavHome();
        }
        void BorrowDuelMenu()
        {
            Show("ONE QUEST. TWO PLAYERS.","CHOOSE HOW YOU SHARE THE TABLE");ArcadeHeader();BorrowTheme();borrowActive=borrowLobby=true;
            Ui.Label(view,"PASS THE QUEST: take turns wearing it. Both hands work.\nLEFT / RIGHT: each hand is a player. Share a cast view\nso a second person can see the board, or practise both roles.",.027f,Palette.Text,new Vector3(0,.18f,1.1f));
            var pass=Button("Pass the\nQuest",-.25f,-.28f,()=>{borrowPass=true;BorrowStart(ArcadePlayers.Hands);},.43f);pass.State=Card.Look.Accent;
            Button("Left / Right\nhands",.25f,-.28f,()=>{borrowPass=false;BorrowStart(ArcadePlayers.Hands);},.43f);Button("Back",0,-.47f,BorrowMenu,.30f);ArcadeNavHome();
        }
        void BorrowHandoffScreen()
        {
            Show((borrowGame.Player==0?"VIOLET":"MINT")+"'S TURN","PASS THE QUEST - PLAYER "+(borrowGame.Player+1));ArcadeHeader();BorrowTheme();borrowActive=borrowHandoff=true;borrowLobby=borrowPaused=borrowAutoPause=borrowFinished=false;
            Ui.Label(view,"Put on the headset in the same seat.\nBoth hands work. Ready places the table in front of you.",.026f,Palette.Text,new Vector3(0,.22f,1.1f));
            Ui.Label(view,"VIOLET "+borrowGame.Score[0]+"  :  "+borrowGame.Score[1]+" MINT",.028f,Palette.Accent,new Vector3(0,-.19f,1.1f));
            var ready=Button("I'm ready",0,-.36f,()=>{Recentre();BorrowBoard();},.54f);ready.State=Card.Look.Accent;ArcadeNavHome();
        }
        void BorrowStart(ArcadePlayers players)
        {
            if(players==ArcadePlayers.PC)return;
            borrowPlayers=players;if(players!=ArcadePlayers.Hands)borrowPass=false;borrowGame.Start(players==ArcadePlayers.Lesson?0:borrowSeed,players==ArcadePlayers.Lesson?0:borrowStarter,players==ArcadePlayers.Lesson?2:BorrowlightGame.TurnLimit);
            borrowMatch=UnityEngine.Random.Range(1000,99999);if(borrowPass)BorrowHandoffScreen();else BorrowBoard();
        }
        void BorrowBoard()
        {
            Show("BORROWLIGHT",borrowPass?"PASS THE QUEST / BOTH HANDS WORK ON YOUR TURN":borrowPlayers==ArcadePlayers.Solo?"YOU = VIOLET  /  SPARRING BOT = MINT":borrowPlayers==ArcadePlayers.Lesson?"LEARN ONE MOVE AT A TIME":"PLAYER 1 = LEFT HAND  /  PLAYER 2 = RIGHT HAND");
            BorrowTheme();heading.transform.localPosition=new Vector3(0,.52f,1.1f);note.transform.localPosition=new Vector3(0,.425f,1.1f);
            borrowActive=true;borrowLobby=borrowHandoff=borrowPaused=borrowFinished=false;borrowSelected=BorrowLesson?BorrowGoalKnot:-1;borrowSlot=4;borrowTurn=borrowGame.Turn;borrowDrag=-1;borrowClaims=borrowGame.ClaimVersion;borrowGate=clock+.3f;borrowMessage="";borrowBotAt=clock+1.1f;
            borrowStatus=Ui.Label(view,"",.030f,Palette.Accent,new Vector3(-.10f,.355f,1.1f));
            borrowScore=Ui.Label(view,"",.029f,Palette.Text,new Vector3(-.10f,-.32f,1.1f));
            borrowHint=Ui.Label(view,"",.022f,Palette.Text,new Vector3(0,-.55f,1.1f));
            borrowGain=Ui.Label(view,"CHOOSE A KNOT",.023f,Palette.Accent,new Vector3(.66f,.295f,1.1f));
            if(!BorrowLesson){for(int col=0;col<3;col++)Ui.Label(view,col==0?"NEAR":col==1?"MID":"FAR",.016f,Palette.Text,new Vector3(.66f+(col-1)*.125f,.225f,1.1f));
            Ui.Label(view,"HIGH",.016f,Palette.Text,new Vector3(.435f,.145f,1.1f));Ui.Label(view,"MID",.016f,Palette.Text,new Vector3(.435f,.02f,1.1f));Ui.Label(view,"LOW",.016f,Palette.Text,new Vector3(.435f,-.105f,1.1f));}
            for(int k=0;k<3;k++){
                int node=k;var tr=new GameObject("Borrowlight knot "+k).transform;tr.SetParent(view,false);
                borrowNodes[k]=new Target{Transform=tr,HalfSize=Vector2.one*.10f,TouchRadius=.026f,AllowDwell=false,OnSelect=()=>BorrowSelect(node,true)};pointer.Add(borrowNodes[k]);
                borrowNodeLabels[k]=Ui.Label(view,((char)('A'+k)).ToString(),.035f,Palette.Text,Vector3.zero);
                borrowKnotCards[k]=Button(((char)('A'+k)).ToString(),-.48f+k*.19f,-.44f,()=>BorrowSelect(node,false),.16f);
            }
            for(int s=0;s<9;s++){
                int slot=s;borrowDocks[s]=Card.Create(view,pointer,new Vector3(.66f+(s%3-1)*.125f,.02f+(s/3-1)*.125f,1.1f),Vector2.one*.12f,"",null,()=>BorrowPlace(slot));borrowDocks[s].gameObject.SetActive(!BorrowLesson);
            }
            if(BorrowLesson){
                borrowLessonDock=Card.Create(view,pointer,new Vector3(.64f,.04f,1.1f),new Vector2(.34f,.22f),"Move B\n+ launch",null,()=>{borrowSelected=BorrowGoalKnot;BorrowPlace(BorrowGoalSlot);});
                var goal=new GameObject("Gold destination").transform;goal.SetParent(view,false);borrowGoalTarget=new Target{Transform=goal,HalfSize=Vector2.one*.11f,TouchRadius=.026f,AllowDwell=false,OnSelect=()=>{borrowSelected=BorrowGoalKnot;BorrowPlace(BorrowGoalSlot);}};pointer.Add(borrowGoalTarget);
            }
            for(int i=0;i<BorrowlightGame.LightCount;i++){borrowPopups[i]=Ui.Label(view,"",.023f,Palette.Accent,Vector3.zero);borrowPopups[i].gameObject.SetActive(false);}
            borrowAction=Ui.Label(view,"MOVE + RELEASE\nTO LAUNCH",.027f,Palette.Accent,new Vector3(.64f,-.23f,1.1f));
            Ui.Label(view,"V: VIOLET  M: MINT\nX: SPENT LIGHT",.016f,Palette.Text,new Vector3(.64f,-.325f,1.1f));
            borrowPause=Button("Pause",.66f,-.44f,BorrowPause,.24f);
            Button("Home",.70f,.52f,Home,.14f);Button("Centre",-.60f,.52f,()=>{BorrowCancelDrag();Recentre();},.16f);
            borrowReleaseAt=-1;for(int h=0;h<2;h++){borrowPinch[h].Reset();borrowGrabArmed[h]=false;}BorrowRefreshPreview();BorrowLabels();Debug.Log("SOH_BORROW start "+borrowPlayers);
        }
        void BorrowSelect(int knot,bool fromOrb)
        {
            if(!BorrowCanEdit||borrowPlayers==ArcadePlayers.Solo&&borrowGame.Player==1)return;
            if(fromOrb&&borrowDrag>=0&&borrowDragKnot==knot)return;
            if(knot==borrowGame.Locked){borrowMessage="That knot is cooling. Choose a different knot.";Cue("ui_back");return;}
            if(BorrowLesson&&knot!=BorrowGoalKnot){borrowMessage="Start with the glowing "+(char)('A'+BorrowGoalKnot)+" knot.";return;}
            if(!fromOrb||borrowDragKnot!=knot)borrowDrag=borrowDragKnot=-1;
            if(borrowSelected!=knot)borrowSlot=borrowGame.Knots[knot];borrowSelected=knot;borrowMessage="";BorrowRefreshPreview();Cue("ui_select");
            int h=pointer.HoverHand>=0?pointer.HoverHand:BorrowHand;
            if(fromOrb&&h>=0&&borrowPinch[h].Pinching&&borrowGrabArmed[h])BorrowBeginDrag(h,knot);
            Debug.Log("SOH_BORROW select "+knot);
        }
        Vector3 BorrowGripPoint(HandState hand)=>hand.Joints[hand.IsController?ClipFormat.Palm:ClipFormat.IndexTip];
        void BorrowBeginDrag(int h,int knot){borrowDrag=h;borrowDragKnot=knot;borrowDragSlot=borrowSlot;borrowReleaseAt=-1;borrowGrabArmed[h]=false;borrowGrip=BorrowMatrix.inverse.MultiplyPoint3x4(BorrowGripPoint(hands.Hand(h)));borrowGrabPoint=BorrowlightGame.KnotPoint(knot,borrowSlot);borrowMessage="";}
        void BorrowPlace(int slot)
        {
            if(!BorrowCanEdit||borrowDrag>=0||borrowPlayers==ArcadePlayers.Solo&&borrowGame.Player==1||borrowSelected<0||borrowSelected==borrowGame.Locked)return;
            BorrowCancelDrag();borrowSlot=slot;borrowMessage="";BorrowRefreshPreview();Cue("ui_hover");Debug.Log("SOH_BORROW dock "+slot);
            if(BorrowValidPreview)BorrowLaunch(false);else borrowMessage=BorrowLesson?"Use the gold destination for this move.":"Choose a different dock to launch.";
        }
        void BorrowRefreshPreview()
        {borrowPreviewGain=borrowGame.Preview(borrowSelected,borrowSlot,borrowPreview,borrowHits,out borrowPreviewSteals);}
        void BorrowCancelDrag(){borrowDrag=borrowDragKnot=-1;borrowReleaseAt=-1;for(int h=0;h<2;h++){borrowPinch[h].Reset();borrowGrabArmed[h]=false;}}
        bool BorrowLaunch(bool fromPC,int knot=-1,int slot=-1,int turn=-1,int match=-1)
        {
            if(!BorrowCanEdit||fromPC)return false;
            if(!fromPC){if(!BorrowValidPreview)return false;knot=borrowSelected;slot=borrowSlot;turn=borrowGame.Turn;}
            if(!borrowGame.Launch(fromPC?1:borrowGame.Player,knot,slot,turn)){borrowMessage="Move a different knot to a new dock first.";return false;}
            BorrowCancelDrag();borrowSelected=-1;borrowClaims=borrowGame.ClaimVersion;borrowStealsHeard=0;borrowMessage="";Cue("borrow_launch");Debug.Log("SOH_BORROW pulse turn="+borrowGame.Turn);PublishArena();return true;
        }
        void PrepareBorrowTargets()
        {
            if(!borrowActive)return;
            if(borrowHandoff){pointer.HandMask=3;return;}
            for(int h=0;h<2;h++){var hand=hands.Hand(h);borrowPinch[h].Update(hand);if(!hand.Tracked||hand.SelectSuppressed)borrowGrabArmed[h]=false;else if(!borrowPinch[h].Pinching)borrowGrabArmed[h]=true;}
            pointer.HandMask=!borrowLobby&&!borrowFinished&&!borrowPaused&&borrowPlayers==ArcadePlayers.Hands&&!borrowPass?1<<borrowGame.Player:3;
            for(int h=0;h<2;h++)if(hands.Hand(h).SelectSuppressed)pointer.HandMask&=~(1<<h);
            bool edit=BorrowCanEdit&&!(borrowPlayers==ArcadePlayers.Solo&&borrowGame.Player==1);
            for(int k=0;k<3;k++)if(borrowNodes[k]!=null)borrowNodes[k].Enabled=edit&&k!=borrowGame.Locked&&(!BorrowLesson||k==BorrowGoalKnot);
            for(int k=0;k<3;k++)if(borrowKnotCards[k]!=null&&borrowKnotCards[k].gameObject.activeInHierarchy)borrowKnotCards[k].Target.Enabled=edit&&k!=borrowGame.Locked&&(!BorrowLesson||k==BorrowGoalKnot);
            for(int s=0;s<9;s++)if(borrowDocks[s]!=null&&borrowDocks[s].gameObject.activeInHierarchy)borrowDocks[s].Target.Enabled=edit&&borrowSelected>=0&&borrowDrag<0;
            if(borrowLessonDock!=null)borrowLessonDock.Target.Enabled=edit&&borrowDrag<0;
            if(borrowGoalTarget!=null)borrowGoalTarget.Enabled=edit&&borrowDrag<0&&borrowSlot!=BorrowGoalSlot;
        }
        void UpdateBorrowlight(float dt)
        {
            if(borrowLobby||borrowHandoff)return;
            if(borrowFinished)return;
            if(borrowPaused){borrowStatus.Text="PAUSED - choose Resume";return;}
            if(borrowGame.Phase==BorrowPhase.Planning&&!(borrowPlayers==ArcadePlayers.Solo&&borrowGame.Player==1)&&!hands.Hand(BorrowHand).Tracked){BorrowCancelDrag();borrowStatus.Text=borrowPlayers==ArcadePlayers.Hands&&!borrowPass?"Show your "+(BorrowHand==0?"left":"right")+" hand. Your turn is safe.":"Show either hand to play. Your turn is safe.";return;}
            if(borrowGame.Turn!=borrowTurn){borrowTurn=borrowGame.Turn;borrowSelected=BorrowLesson?BorrowGoalKnot:-1;borrowSlot=4;borrowStealsHeard=0;borrowGate=clock+.25f;borrowBotAt=clock+1.1f;BorrowRefreshPreview();}
            if(BorrowCanEdit&&borrowDrag<0&&borrowSelected>=0&&!(borrowPlayers==ArcadePlayers.Solo&&borrowGame.Player==1))for(int s=0;s<9;s++)if(pointer.Hovered==borrowDocks[s].Target&&borrowDocks[s].Target.Enabled&&borrowSlot!=s){borrowSlot=s;BorrowRefreshPreview();break;}
            if(borrowDrag<0&&BorrowCanEdit&&!(borrowPlayers==ArcadePlayers.Solo&&borrowGame.Player==1))for(int h=0;h<2;h++){
                if(borrowPlayers==ArcadePlayers.Hands&&!borrowPass&&h!=borrowGame.Player||!hands.Hand(h).Tracked||!borrowPinch[h].Pinching||!borrowGrabArmed[h])continue;
                for(int k=0;k<3;k++)if(borrowNodes[k].Enabled&&(pointer.Hovered==borrowNodes[k]&&pointer.HoverHand==h||!hands.Hand(h).IsController&&Vector3.Distance(BorrowGripPoint(hands.Hand(h)),borrowNodes[k].Transform.position)<.043f)){
                    if(borrowSelected!=k)borrowSlot=borrowGame.Knots[k];borrowSelected=k;BorrowRefreshPreview();BorrowBeginDrag(h,k);break;
                }
                if(borrowDrag>=0)break;
            }
            if(borrowDrag>=0){
                var hand=hands.Hand(borrowDrag);
                if(!hand.Tracked||hand.SelectSuppressed||!BorrowCanEdit){BorrowCancelDrag();borrowMessage="Grab cancelled. Your move was not played.";}
                else if(!borrowPinch[borrowDrag].Pinching){
                    if(borrowReleaseAt<0)borrowReleaseAt=clock;
                    if(clock-borrowReleaseAt>=.10f){bool moved=borrowSlot!=borrowDragSlot;borrowDrag=borrowDragKnot=-1;borrowReleaseAt=-1;if(moved&&BorrowValidPreview)BorrowLaunch(false);else if(moved&&BorrowLesson)borrowMessage="Move to the gold ring, then release.\nOr tap the gold Move button.";}
                }
                else{
                    borrowReleaseAt=-1;
                    var delta=BorrowMatrix.inverse.MultiplyPoint3x4(BorrowGripPoint(hand))-borrowGrip;
                    var p=borrowGrabPoint+delta;float y=p.y/.23f+1,z=p.z/.20f+1;int row=borrowSlot/3,col=borrowSlot%3;
                    if(Mathf.Abs(y-row)>.56f)row=Mathf.Clamp(Mathf.RoundToInt(y),0,2);if(Mathf.Abs(z-col)>.56f)col=Mathf.Clamp(Mathf.RoundToInt(z),0,2);int slot=row*3+col;
                    if(BorrowLesson&&(p-BorrowlightGame.KnotPoint(BorrowGoalKnot,BorrowGoalSlot)).sqrMagnitude<.15f*.15f)slot=BorrowGoalSlot;
                    if(slot!=borrowSlot){borrowSlot=slot;BorrowRefreshPreview();if(clock-borrowLastCue>.09f){Cue("borrow_dock");borrowLastCue=clock;}}
                }
            }
            if(borrowPlayers==ArcadePlayers.Solo&&borrowGame.Player==1&&BorrowCanEdit&&clock>=borrowBotAt){borrowGame.BestMove(out int knot,out int slot);borrowGame.Launch(1,knot,slot,borrowGame.Turn);borrowSelected=-1;Cue("borrow_launch");}
            var phase=borrowGame.Phase;borrowGame.Step(dt);
            if(borrowGame.ClaimVersion!=borrowClaims){borrowClaims=borrowGame.ClaimVersion;Cue(borrowGame.LastSteals>borrowStealsHeard?"borrow_steal":"borrow_claim");borrowStealsHeard=borrowGame.LastSteals;}
            if(phase!=borrowGame.Phase&&borrowGame.Phase==BorrowPhase.Refill){Cue("borrow_refill");BorrowCancelDrag();}
            if(borrowGame.Phase==BorrowPhase.Result){BorrowResult();return;}
            if(borrowPass&&borrowGame.Phase==BorrowPhase.Planning&&(phase==BorrowPhase.Pulse||phase==BorrowPhase.Refill)){BorrowHandoffScreen();return;}
            if(phase==BorrowPhase.Pulse&&borrowGame.Phase==BorrowPhase.Planning){borrowMessage=BorrowLesson?"":"LAST PULSE +"+borrowGame.LastGain+(borrowGame.LastSteals>0?" / "+borrowGame.LastSteals+" stolen":"");Cue("receive_pop");}
            if(clock>=borrowLabelsAt){borrowLabelsAt=clock+.08f;BorrowLabels();}
        }
        void BorrowLabels()
        {
            string name=borrowPlayers==ArcadePlayers.Lesson?"YOUR TURN":borrowPlayers==ArcadePlayers.Solo&&borrowGame.Player==1?"SPARRING BOT":"PLAYER "+(borrowGame.Player+1)+(borrowPlayers==ArcadePlayers.Hands&&!borrowPass?borrowGame.Player==0?" - LEFT HAND":" - RIGHT HAND":" - YOUR TURN");
            borrowStatus.Text=borrowGame.Phase==BorrowPhase.Refill?"SECOND HEIST - FRESH LIGHTS, STARTERS SWAP":borrowGame.Phase==BorrowPhase.Pulse?"PULSE +"+borrowGame.LastGain+(borrowGame.LastSteals>0?"  /  STOLEN "+borrowGame.LastSteals:""):name+"  /  MOVE "+(borrowGame.Turn%6+1)+" OF "+(borrowPlayers==ArcadePlayers.Lesson?2:6);
            if(BorrowLesson&&borrowGame.Phase==BorrowPhase.Planning)borrowStatus.Text=borrowDrag>=0&&BorrowValidPreview?"RELEASE TO LAUNCH":"MOVE "+(char)('A'+BorrowGoalKnot)+" TO GOLD + RELEASE";
            borrowScore.Text="VIOLET "+borrowGame.Score[0]+"    :    "+borrowGame.Score[1]+" MINT"+(borrowPlayers==ArcadePlayers.Lesson?"":"  /  HEIST "+(borrowGame.Heist+1)+" OF 2");
            string lessonHint=borrowGame.Turn==0?"Pinch-HOLD B, lift to GOLD, then RELEASE to launch (+1).\nOr TAP Move B + launch. Either hand works.":"B is cooling. Move A down to GOLD, then RELEASE (+2 steal).\nOr TAP Move A + launch. Watch MINT steal VIOLET.";
            borrowHint.Text=borrowMessage!=""?borrowMessage:BorrowLesson&&borrowGame.Phase==BorrowPhase.Planning?lessonHint:borrowGame.Phase==BorrowPhase.Refill?"Same field. Opposite starter. Your banked points carry over.":borrowPlayers==ArcadePlayers.Hands&&!borrowPass?(borrowGame.Player==0?"LEFT HAND ONLY this turn. Right waits.":"RIGHT HAND ONLY this turn. Left waits.")+"\nPINCH-HOLD -> MOVE -> RELEASE. Or tap a dock.":"PINCH-HOLD -> MOVE -> RELEASE TO LAUNCH.\nOr choose A/B/C, then TAP a dock. Claim +1. Steal +2 once.";
            borrowGain.Text=borrowGame.Phase==BorrowPhase.Pulse?"LIGHT IN FLIGHT":borrowSelected<0?"CHOOSE A KNOT":((char)('A'+borrowSelected))+"  /  PREVIEW +"+borrowPreviewGain+(borrowPreviewSteals>0?"\nSTEAL "+borrowPreviewSteals:"");
            for(int k=0;k<3;k++){borrowKnotCards[k].State=k==borrowGame.Locked||BorrowLesson&&k!=BorrowGoalKnot?Card.Look.Dim:k==borrowSelected||BorrowLesson&&k==BorrowGoalKnot?Card.Look.Accent:Card.Look.Normal;borrowKnotCards[k].Text.Text=((char)('A'+k))+(k==borrowGame.Locked?"\nCOOL":"");}
            for(int s=0;s<9;s++)borrowDocks[s].State=borrowSelected>=0&&s==borrowSlot?Card.Look.Accent:BorrowLesson&&borrowSelected>=0&&s==BorrowGoalSlot?Card.Look.Right:Card.Look.Normal;
            borrowAction.Text=borrowGame.Phase==BorrowPhase.Refill?"FRESH LIGHTS":borrowGame.Phase!=BorrowPhase.Planning?"PULSE TRAVELLING":borrowDrag>=0?BorrowValidPreview?"RELEASE\nTO LAUNCH":"MOVE TO A NEW DOCK\nTHEN RELEASE":BorrowLesson?"DRAG TO GOLD\nOR TAP MOVE "+(char)('A'+BorrowGoalKnot):borrowSelected<0?"CHOOSE A/B/C\nOR GRAB A KNOT":"HOVER TO PREVIEW\nTAP TO LAUNCH";
            if(borrowLessonDock!=null){borrowLessonDock.Text.Text=borrowGame.Phase==BorrowPhase.Pulse?"Light\nin flight":"Move "+(char)('A'+BorrowGoalKnot)+"\n+ launch";borrowLessonDock.State=borrowGame.Phase!=BorrowPhase.Planning||borrowDrag>=0?Card.Look.Dim:Card.Look.Accent;}
        }
        void BorrowPause(){if(borrowLobby||borrowHandoff||borrowFinished)return;borrowPaused=!borrowPaused;BorrowCancelDrag();if(borrowPause!=null)borrowPause.Text.Text=borrowPaused?"Resume":"Pause";borrowGate=clock+.25f;}
        void PauseBorrowlight(){if(borrowActive&&!borrowLobby&&!borrowHandoff&&!borrowFinished&&!borrowPaused){borrowAutoPause=true;BorrowPause();}}
        void ResumeBorrowlight(){if(borrowHandoff){Recentre();return;}if(borrowAutoPause&&borrowActive&&borrowPaused)BorrowPause();borrowAutoPause=false;}
        void BorrowResult()
        {
            if(borrowFinished)return;bool lesson=borrowPlayers==ArcadePlayers.Lesson;
            if(!lesson){if(borrowGame.Winner==0)borrowWins0++;else if(borrowGame.Winner==1)borrowWins1++;PlayerPrefs.SetInt("borrowWins0",borrowWins0);PlayerPrefs.SetInt("borrowWins1",borrowWins1);PlayerPrefs.SetInt("borrowSeed",++borrowSeed);PlayerPrefs.Save();}
            string title=lesson?"YOU CAN BORROW LIGHT":borrowGame.Winner<0?"LIGHT SHARED - DRAW":(borrowGame.Winner==0?"VIOLET":"MINT")+" WINS THE HEIST";
            Show(title,"VIOLET "+borrowGame.Score[0]+"  :  "+borrowGame.Score[1]+" MINT");ArcadeHeader();BorrowTheme();borrowActive=borrowFinished=true;borrowLobby=borrowPaused=false;borrowReplayAt=clock;pointer.HandMask=3;
            Ui.Label(view,lesson?"A moved knot cools. Your rival must bend another.\nSteal a claimed beacon once; then it burns out.":"RIVALRY: VIOLET "+borrowWins0+" - "+borrowWins1+" MINT\nTwo heists. Both players get the opening move.",.027f,Palette.Text,new Vector3(0,.24f,1.1f));
            Button(lesson?"Play duel":"Rematch",-.30f,-.44f,()=>{if(lesson)BorrowMenu();else{borrowStarter=1-borrowStarter;BorrowStart(borrowPlayers);}},.28f);
            Button("Pulse replay",0,-.44f,()=>{borrowReplay=true;borrowReplayAt=clock;Cue("borrow_launch");},.28f);Button("Home",.30f,-.44f,Home,.28f);Cue("guess_right");
        }
        void DrawBorrowlight()
        {
            BorrowlightArt.Sky(batch,stage.localToWorldMatrix,clock);var matrix=BorrowMatrix;
            if(borrowHandoff){var mini=stage.localToWorldMatrix*Matrix4x4.TRS(new Vector3(0,.025f,1.19f),Quaternion.Euler(-20,0,0),Vector3.one*.44f);BorrowlightArt.Draw(batch,mini,borrowGame,null,null,-1,4,clock,false);return;}
            if(borrowLobby)matrix=matrix*Matrix4x4.Scale(Vector3.one*.45f);
            BorrowlightArt.Draw(batch,matrix,borrowGame,borrowGame.Phase==BorrowPhase.Planning&&!borrowFinished&&!borrowLobby?borrowPreview:null,borrowHits,borrowSelected,borrowSlot,clock,BorrowCanEdit,borrowReplay,Mathf.Clamp01((clock-borrowReplayAt)/3.2f));
            if(borrowGoalTarget!=null){
                var goal=BorrowlightGame.KnotPoint(BorrowGoalKnot,BorrowGoalSlot);borrowGoalTarget.Transform.SetPositionAndRotation(matrix.MultiplyPoint3x4(goal),stage.rotation);
                if(borrowGame.Phase==BorrowPhase.Planning&&borrowSlot!=BorrowGoalSlot){ArcadeArt.Ring(batch,matrix,goal,.083f,Palette.Accent);ArcadeArt.Line(batch,matrix,BorrowlightGame.KnotPoint(BorrowGoalKnot,borrowSlot),goal,Palette.Accent,.003f,.65f);}
            }
            if(borrowReplay&&clock-borrowReplayAt>3.2f)borrowReplay=false;
            if(borrowDrag>=0&&borrowDragKnot>=0){var p=matrix.MultiplyPoint3x4(BorrowlightGame.KnotPoint(borrowDragKnot,borrowSlot));batch.Tube(BorrowGripPoint(hands.Hand(borrowDrag)),p,.0012f,new Vector4(1,.84f,.43f,.8f),Vector4.zero);}
            for(int k=0;k<3;k++)if(borrowNodes[k]!=null){
                var p=matrix.MultiplyPoint3x4(BorrowlightGame.KnotPoint(k,borrowGame.Phase==BorrowPhase.Planning?borrowPreview[k]:borrowGame.Knots[k]));
                borrowNodes[k].Transform.position=p;borrowNodes[k].Transform.rotation=stage.rotation;
                borrowNodeLabels[k].transform.position=p+stage.TransformVector(new Vector3(0,0,-.055f));borrowNodeLabels[k].Color=k==borrowSelected?Palette.Accent:Palette.Text;
            }
            for(int i=0;i<BorrowlightGame.LightCount;i++)if(borrowPopups[i]!=null){
                float age=borrowGame.Pulse-(borrowGame.Player==0?borrowGame.Hits[i]:1-borrowGame.Hits[i]);
                bool show=!borrowLobby&&!borrowFinished&&borrowGame.Phase==BorrowPhase.Pulse&&borrowGame.Changed[i]&&age>=0&&age<.26f;
                if(borrowPopups[i].gameObject.activeSelf!=show)borrowPopups[i].gameObject.SetActive(show);
                if(show){borrowPopups[i].Text=borrowGame.Spent[i]?"+2 STOLEN":"+1 CLAIMED";borrowPopups[i].transform.position=matrix.MultiplyPoint3x4(borrowGame.Lights[i]+Vector3.up*(.08f+(Ui.ReducedMotion?0:age*.17f)))+stage.TransformVector(Vector3.back*.04f);}
            }
        }
        void DrawBorrowHome()
        {
            BorrowlightArt.Sky(batch,stage.localToWorldMatrix,clock);
            if(clock>=borrowAttractAt&&borrowAttract.Phase==BorrowPhase.Planning){borrowAttract.BestMove(out int k,out int s);borrowAttract.Launch(borrowAttract.Player,k,s,borrowAttract.Turn);borrowAttractAt=clock+3.2f;}
            borrowAttract.Step(Mathf.Min(Time.deltaTime,.1f));if(borrowAttract.Phase==BorrowPhase.Result)borrowAttract.Start();
            var m=stage.localToWorldMatrix*Matrix4x4.TRS(new Vector3(0,.055f,1.19f),Quaternion.Euler(-20,0,0),Vector3.one*.44f);
            BorrowlightArt.Draw(batch,m,borrowAttract,null,null,-1,4,clock,false);
        }
        void PublishBorrowlight(ArenaPacket p)
        {
            p.mode="borrowlight";p.title=Screen;p.phase=borrowLobby||borrowHandoff?"lobby":borrowGame.Phase.ToString().ToLowerInvariant();p.pc=false;p.lesson=borrowPlayers==ArcadePlayers.Lesson;
            p.paused=borrowPaused||borrowHandoff;p.ready=BorrowCanEdit;p.player=borrowGame.Player;p.left=borrowGame.Score[0];p.right=borrowGame.Score[1];p.winner=borrowGame.Winner;
            p.width=p.height=0;p.board=Array.Empty<int>();p.clearRows=p.winningLine=Array.Empty<int>();p.knots=borrowGame.Knots;p.lightOwners=borrowGame.Owners;p.lightSpent=borrowGame.Spent;p.lightPositions=borrowGame.Lights;
            p.lockedKnot=borrowGame.Locked;p.weaveTurn=borrowGame.Turn;p.weaveLimit=borrowGame.Limit;p.weaveMatch=borrowMatch;p.weaveSeed=borrowGame.Seed;p.weaveHeist=borrowGame.Heist;p.pulse=borrowGame.Pulse;
            p.hint=borrowHandoff?"Pass the headset to Player "+(borrowGame.Player+1)+" and choose Ready":borrowLobby?"Choose Learn, Duel or Play with bot on Quest":borrowGame.Phase==BorrowPhase.Result?"Heist complete":borrowGame.Phase==BorrowPhase.Refill?"Second heist - starters swap":borrowPlayers==ArcadePlayers.Hands&&!borrowPass?"PLAYER "+(borrowGame.Player+1)+(borrowGame.Player==0?" - LEFT HAND":" - RIGHT HAND"):"QUEST PLAYER'S TURN";
            p.detail="One shared ribbon. Claim a beacon +1. Steal it once +2, then it burns out. The last moved knot cools for the next turn. Two six-move heists swap the starter.";
        }
        bool BorrowCommand(string command)
        {
            return false; // A browser cannot control this Quest-only game.
        }
    }
}
