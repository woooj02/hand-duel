using UnityEngine;

namespace ShowOfHands {
 public enum RpsMove { Unknown, Rock, Paper, Scissors }
 public enum RpsPhase { Waiting, Countdown, Result, Retry }

 // Local left-vs-right rules. The XR provider supplies handedness, not player identity.
 public static class RpsPose {
  static bool Extended(HandState h,int p) {
   float baseLength=Vector3.Distance(h.Joints[p],h.Joints[ClipFormat.Wrist]);
   return baseLength>.015f && HandPoses.Straightness(h,p)>.80f
    && Vector3.Distance(h.Joints[p+3],h.Joints[ClipFormat.Wrist])>baseLength*1.45f;
  }
  static bool Curled(HandState h,int p) {
   float baseLength=Vector3.Distance(h.Joints[p],h.Joints[ClipFormat.Wrist]);
   return baseLength>.015f && (HandPoses.Straightness(h,p)<.73f
    || Vector3.Distance(h.Joints[p+3],h.Joints[ClipFormat.Wrist])<baseLength*1.35f);
  }
  public static RpsMove Read(HandState h) {
   if(!h.Tracked||h.IsController)return RpsMove.Unknown;
   bool i=Extended(h,7),m=Extended(h,12),r=Extended(h,17),l=Extended(h,22);
   bool ci=Curled(h,7),cm=Curled(h,12),cr=Curled(h,17),cl=Curled(h,22);
   if(i&&m&&r&&l)return RpsMove.Paper;
   if(i&&m&&cr&&cl)return RpsMove.Scissors;
   if(ci&&cm&&cr&&cl)return RpsMove.Rock;
   return RpsMove.Unknown;
  }
  // -1: left wins; 0: draw; +1: right wins. Unknown never wins.
  public static int Winner(RpsMove left,RpsMove right) {
   if(left==RpsMove.Unknown||right==RpsMove.Unknown||left==right)return 0;
   return (left==RpsMove.Rock&&right==RpsMove.Scissors)
    ||(left==RpsMove.Paper&&right==RpsMove.Rock)
    ||(left==RpsMove.Scissors&&right==RpsMove.Paper)?-1:1;
  }
 }

 public sealed class RpsRound {
  public RpsPhase Phase {get;private set;}
  public RpsMove Left {get;private set;}
  public RpsMove Right {get;private set;}
  public float Time {get;private set;}
  float fistTime,leftStable,rightStable;
  RpsMove previousLeft,previousRight;
  public int Result=>RpsPose.Winner(Left,Right);
  public void Reset(){Phase=RpsPhase.Waiting;Time=fistTime=leftStable=rightStable=0;Left=Right=previousLeft=previousRight=RpsMove.Unknown;}
  public void Step(HandFrame hands,float dt) {
   if(Phase==RpsPhase.Result||Phase==RpsPhase.Retry)return;
   var left=RpsPose.Read(hands.Left);var right=RpsPose.Read(hands.Right);
   if(Phase==RpsPhase.Waiting){
    fistTime=left==RpsMove.Rock&&right==RpsMove.Rock?fistTime+dt:0;
    if(fistTime>=.6f){Phase=RpsPhase.Countdown;Time=leftStable=rightStable=0;previousLeft=previousRight=RpsMove.Unknown;}
    return;
   }
   if(!hands.Left.Tracked||!hands.Right.Tracked){Reset();return;}
   float before=Time;Time+=Mathf.Max(0,dt);
   if(Time>=3){
    float sampleTime=Time-Mathf.Max(3,before);
    leftStable=left!=RpsMove.Unknown&&left==previousLeft?leftStable+sampleTime:sampleTime;
    rightStable=right!=RpsMove.Unknown&&right==previousRight?rightStable+sampleTime:sampleTime;
    previousLeft=left;previousRight=right;
   }
   // Both choices are sampled on the same deadline. Never wait for one player after revealing the other.
   if(Time>=3.5f){
    if(left==RpsMove.Unknown||right==RpsMove.Unknown||leftStable<.18f||rightStable<.18f){Phase=RpsPhase.Retry;return;}
    Left=left;Right=right;Phase=RpsPhase.Result;
   }
  }
 }
}
