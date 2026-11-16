using System;
using UnityEngine;
namespace ShowOfHands {
 public sealed partial class App {
  enum DuelView { Attract,Practice,Ready,Reveal }
  readonly RpsRound duelRound=new RpsRound();
  readonly Vector3[] duelLeft=new Vector3[26],duelRight=new Vector3[26];
  bool duelActive,duelFilm,duelAnnounced;
  DuelView duelView;
  RpsMove duelLeftMove,duelRightMove;
  float duelTime,duelSpeed=1,duelStatusAt,duelFilmClock;
  int duelLeftPoints,duelRightPoints,duelCount=-1,duelAttractPair=-1;
  Label duelBig,duelLeftStatus,duelRightStatus,duelFilmHint;
  readonly Card[] duelResultButtons=new Card[4];

  void Home(){
   duelSpeed=1;opening=false;seedSession=false;Show("HAND DUEL","BORROWLIGHT + THREE TABLETOP CLASSICS");BorrowTheme();duelActive=true;duelView=DuelView.Attract;duelTime=0;duelAttractPair=-1;
   heading.transform.localPosition=new Vector3(0,.43f,1.1f);note.transform.localPosition=new Vector3(0,.35f,1.1f);
   Ui.Label(view,"ONE SHARED RIBBON. EVERY MOVE LEAVES A PUZZLE.",.026f,Palette.Accent,new Vector3(0,.28f,1.1f));
   var hero=Button("Borrowlight",0,-.17f,BorrowMenu,.84f);hero.State=Card.Look.Accent;
   Button("Aftershape",-.34f,-.36f,ShapeMenu,.30f);Button("Tic-tac-toe",0,-.36f,TicMenu,.30f);Button("Rock Paper\nScissors",.34f,-.36f,DuelNewMatch,.30f);
   Button("More",.46f,.42f,DuelMore,.15f);Button("Centre",-.46f,.42f,Recentre,.15f);
   Ui.Label(view,"TOUCH a button - or aim your HAND + pinch",.025f,Palette.Accent,new Vector3(0,-.53f,1.1f));DuelArt.Warm();
  }
  void DuelMore(){Show("More ways to play","Hand Duel is the main experience");Button("Charades",-.22f,-.12f,CharadesHome,.39f);Button("Settings",.22f,-.12f,Settings,.39f);Button("RPS practice",0,-.31f,DuelPractice,.40f);Button("Hand Duel",0,-.49f,Home,.40f);}
  void DuelPractice(){
   duelSpeed=1;Show("LEARN YOUR WEAPONS","Open palm = paper • two fingers = scissors • fist = rock");duelActive=true;duelView=DuelView.Practice;duelTime=0;duelStatusAt=0;
   duelLeftStatus=Ui.Label(view,"LEFT HAND",.040f,Glove.Find("violet").Tip,new Vector3(-.27f,.18f,1.1f));
   duelRightStatus=Ui.Label(view,"RIGHT HAND",.040f,Glove.Find("mint").Tip,new Vector3(.27f,.18f,1.1f));
   Button("Start match",-.22f,-.36f,DuelNewMatch,.39f);Button("Home",.22f,-.36f,Home,.39f);
  }
  void DuelNewMatch(){duelLeftPoints=duelRightPoints=0;DuelReady();}
  void DuelReady(){
   duelSpeed=1;Show("FIRST TO THREE","One left hand + one right hand • both hold a fist to start");duelActive=true;duelView=DuelView.Ready;duelTime=0;duelCount=-1;duelRound.Reset();
   DuelScores();duelBig=Ui.Label(view,"FISTS READY?",.062f,Palette.Accent,new Vector3(0,.025f,1.1f));
   Ui.Label(view,"Throw on GO. Hold the shape for half a second.",.030f,Palette.Text,new Vector3(0,-.15f,1.1f));
   Button("Practice",-.22f,-.36f,DuelPractice,.39f);Button("Home",.22f,-.36f,Home,.39f);
  }
  void DuelScores(){
   duelLeftStatus=Ui.Label(view,"PLAYER 1  /  "+duelLeftPoints,.037f,Glove.Find("violet").Tip,new Vector3(-.27f,.20f,1.1f));
   duelRightStatus=Ui.Label(view,"PLAYER 2  /  "+duelRightPoints,.037f,Glove.Find("mint").Tip,new Vector3(.27f,.20f,1.1f));
  }
  void UpdateDuel(float dt){
   duelTime+=dt*duelSpeed;
   if(duelFilm){duelFilmClock+=dt;if(duelTime>3.8f){duelTime=0;duelAnnounced=false;}if(duelFilmClock>.8f&&pointer.AnyPinchBegan){CloseDuelFilm();view.gameObject.SetActive(true);fader.Snap(1);pointer.Active=true;}return;}
   if(duelView==DuelView.Attract){int pair=(int)(duelTime/4.6f)%3;if(pair!=duelAttractPair){duelAttractPair=pair;duelLeftMove=pair==0?RpsMove.Rock:pair==1?RpsMove.Paper:RpsMove.Scissors;duelRightMove=pair==0?RpsMove.Scissors:pair==1?RpsMove.Rock:RpsMove.Paper;DuelArt.FillPose(duelLeft,duelLeftMove,0);DuelArt.FillPose(duelRight,duelRightMove,1);}return;}
   if(duelView==DuelView.Practice){
    duelLeftMove=RpsPose.Read(hands.Left);duelRightMove=RpsPose.Read(hands.Right);
    if(clock>=duelStatusAt){duelStatusAt=clock+.15f;duelLeftStatus.Text=hands.Left.Tracked?"LEFT: "+MoveName(duelLeftMove):"LEFT: NOT SEEN";duelRightStatus.Text=hands.Right.Tracked?"RIGHT: "+MoveName(duelRightMove):"RIGHT: NOT SEEN";}return;
   }
   if(duelView==DuelView.Reveal){
    pointer.Active=duelTime>1.5f;
    if(duelTime>=.9f&&!duelAnnounced){duelAnnounced=true;heading.Text=DuelWinnerText();note.Text=MoveName(duelLeftMove)+" vs "+MoveName(duelRightMove);Cue(duelRound.Result==0?"reveal":"guess_right");}
    return;
   }
   duelRound.Step(hands,dt);
   if(duelRound.Phase==RpsPhase.Waiting){pointer.Active=true;duelCount=-1;duelBig.Text="FISTS READY?";note.Text=!hands.Left.Tracked||!hands.Right.Tracked?"Keep one left and one right hand in view":"Both hold a fist • first to three wins";}
   else if(duelRound.Phase==RpsPhase.Countdown){pointer.Active=false;int count=duelRound.Time<3?3-Mathf.FloorToInt(duelRound.Time):0;if(count!=duelCount){duelCount=count;duelBig.Text=count==0?"GO!":count.ToString();Cue(count==0?"count_go":"count_tick");}note.Text="Choose on GO • hold your throw";}
   else if(duelRound.Phase==RpsPhase.Retry){Show("NO POINT — THROW AGAIN","A hand was missing or the shape was unclear");Button("Try again",-.22f,-.22f,DuelReady,.39f);Button("Practice",.22f,-.22f,DuelPractice,.39f);NavHome();}
   else if(duelRound.Phase==RpsPhase.Result){
    duelLeftMove=duelRound.Left;duelRightMove=duelRound.Right;DuelCapture(hands.Left,duelLeft);DuelCapture(hands.Right,duelRight);
    if(duelRound.Result<0)duelLeftPoints++;else if(duelRound.Result>0)duelRightPoints++;
    DuelReveal(false);
   }
  }
  void DuelCapture(HandState hand,Vector3[] target){var m=stage.worldToLocalMatrix;for(int j=0;j<26;j++)target[j]=m.MultiplyPoint3x4(hand.Joints[j]);}
  static string MoveName(RpsMove move)=>move==RpsMove.Unknown?"TRY A CLEAR SHAPE":move.ToString().ToUpperInvariant();
  string DuelWinnerText(){if(duelRound.Result==0)return"DRAW — NO POINT";int p=duelRound.Result<0?1:2;return"PLAYER "+p+(duelLeftPoints>=3||duelRightPoints>=3?" WINS THE MATCH":" WINS THE ROUND");}
  void DuelReveal(bool slow){
   Show(slow?"CLASH REPLAY":"SHOWDOWN",slow?"0.25× speed • your captured throws":"Rock breaks scissors. Paper wraps rock. Scissors cuts paper.");
   duelActive=true;duelView=DuelView.Reveal;duelTime=0;duelSpeed=slow?.25f:1;duelAnnounced=false;pointer.Active=false;DuelScores();
   bool complete=duelLeftPoints>=3||duelRightPoints>=3;
   duelResultButtons[0]=Button(complete?"Rematch":"Next round",-.22f,-.29f,()=>{duelSpeed=1;if(complete)DuelNewMatch();else DuelReady();},.39f);
   duelResultButtons[1]=Button("Slow replay",.22f,-.29f,()=>DuelReveal(true),.39f);
   duelResultButtons[2]=Button("Film view",-.22f,-.44f,DuelFilm,.39f);
   duelResultButtons[3]=Button("Home",.22f,-.44f,()=>{duelSpeed=1;Home();},.39f);
  }
  void DuelFilm(){duelFilm=true;duelTime=0;duelSpeed=.25f;duelAnnounced=false;duelFilmClock=0;view.gameObject.SetActive(false);pointer.Active=false;duelFilmHint=Ui.Label(stage,"CLASH REPLAY • 0.25×\nPinch to restore controls",.030f,Palette.Accent,new Vector3(0,.36f,1.1f));}
  void CloseDuelFilm(){duelFilm=false;if(duelFilmHint!=null){Destroy(duelFilmHint.gameObject);duelFilmHint=null;}}
  void PauseDuel(){if(duelView==DuelView.Ready){duelRound.Reset();duelCount=-1;} }
  void DrawDuel(){
   if(duelView==DuelView.Attract){DrawBorrowHome();return;}
   DuelArt.Arena(batch,stage.localToWorldMatrix,clock);
   if(duelView==DuelView.Attract||duelView==DuelView.Reveal){DuelArt.Clash(batch,stage.localToWorldMatrix,duelLeft,duelRight,duelLeftMove,duelRightMove,duelView==DuelView.Attract?duelTime%4.6f:duelTime,Ui.ReducedMotion);return;}
   if(duelView==DuelView.Practice){for(int h=0;h<2;h++){var move=h==0?duelLeftMove:duelRightMove;var pos=new Vector3(h==0?-.28f:.28f,-.015f,1.1f);DuelArt.Prop(batch,stage.localToWorldMatrix*Matrix4x4.TRS(pos,Quaternion.identity,Vector3.one),move,Glove.Find(h==0?"violet":"mint"),1,clock);}}
  }
 }
}
