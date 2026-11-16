using NUnit.Framework;
using UnityEngine;
namespace ShowOfHands.Tests {
 public class DuelRuleTests {
  public static HandState Hand(RpsMove move,int side=0){var h=new HandState{Tracked=true};DuelArt.FillPose(h.Joints,move,side);return h;}
  [TestCase(RpsMove.Rock)] [TestCase(RpsMove.Paper)] [TestCase(RpsMove.Scissors)]
  public void RecognisesBothHandsWithoutOrientationDependence(RpsMove move){for(int side=0;side<2;side++){var h=Hand(move,side);var q=Quaternion.Euler(71,-32,120);for(int i=0;i<26;i++)h.Joints[i]=q*(h.Joints[i]*.8f)+new Vector3(1,2,3);Assert.That(RpsPose.Read(h),Is.EqualTo(move));}}
  [TestCase(RpsMove.Rock,RpsMove.Rock,0)] [TestCase(RpsMove.Rock,RpsMove.Paper,1)] [TestCase(RpsMove.Rock,RpsMove.Scissors,-1)]
  [TestCase(RpsMove.Paper,RpsMove.Rock,-1)] [TestCase(RpsMove.Paper,RpsMove.Paper,0)] [TestCase(RpsMove.Paper,RpsMove.Scissors,1)]
  [TestCase(RpsMove.Scissors,RpsMove.Rock,1)] [TestCase(RpsMove.Scissors,RpsMove.Paper,-1)] [TestCase(RpsMove.Scissors,RpsMove.Scissors,0)]
  public void AllNineMatchups(RpsMove l,RpsMove r,int winner){Assert.That(RpsPose.Winner(l,r),Is.EqualTo(winner));}
  [Test] public void MissingAndInvalidHandsAreNotMoves(){Assert.That(RpsPose.Read(new HandState()),Is.EqualTo(RpsMove.Unknown));Assert.That(RpsPose.Read(new HandState{Tracked=true}),Is.EqualTo(RpsMove.Unknown));}
  static HandFrame Fists(){var f=new HandFrame();f.Left.CopyFrom(Hand(RpsMove.Rock));f.Right.CopyFrom(Hand(RpsMove.Rock,1));return f;}
  static void Ready(RpsRound round,HandFrame f){round.Reset();for(int i=0;i<50;i++)round.Step(f,1f/72);Assert.That(round.Phase,Is.EqualTo(RpsPhase.Countdown));}
  [Test] public void SimultaneousDeadlineAndNoEarlyReveal(){var r=new RpsRound();var f=Fists();Ready(r,f);for(int i=0;i<180;i++)r.Step(f,1f/72);Assert.That(r.Phase,Is.EqualTo(RpsPhase.Countdown));Assert.That(r.Left,Is.EqualTo(RpsMove.Unknown));f.Left.CopyFrom(Hand(RpsMove.Paper));for(int i=0;i<100&&r.Phase==RpsPhase.Countdown;i++)r.Step(f,1f/72);Assert.That(r.Phase,Is.EqualTo(RpsPhase.Result));Assert.That(r.Result,Is.EqualTo(-1));}
  [Test] public void TrackingLossCancelsCountdownWithoutPoint(){var r=new RpsRound();var f=Fists();Ready(r,f);f.Right.Tracked=false;r.Step(f,.1f);Assert.That(r.Phase,Is.EqualTo(RpsPhase.Waiting));Assert.That(r.Left,Is.EqualTo(RpsMove.Unknown));}
  [Test] public void UnclearPoseAtDeadlineRetries(){var r=new RpsRound();var f=Fists();Ready(r,f);f.Right.Joints[10]=new Vector3(0,0,0);for(int i=0;i<26;i++)f.Right.Joints[i]=Vector3.zero;for(int i=0;i<300&&r.Phase==RpsPhase.Countdown;i++)r.Step(f,1f/72);Assert.That(r.Phase,Is.EqualTo(RpsPhase.Retry));}
 }
}
