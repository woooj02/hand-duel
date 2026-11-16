using NUnit.Framework;
using UnityEngine;
namespace ShowOfHands.Tests {
 public class HandPointerTests {
  GameObject root;Pointer pointer;HandFrame frame;Target a,b;int left,right;
  [SetUp] public void Setup(){left=right=0;root=new GameObject("pointer regression");root.transform.localScale=Vector3.one*.38f;pointer=new Pointer();frame=new HandFrame();a=Target(-.3f,()=>left++);b=Target(.3f,()=>right++);foreach(var hand in new[]{frame.Left,frame.Right}){hand.Tracked=true;hand.AimValid=true;hand.Joints[5]=Vector3.left;hand.Joints[10]=Vector3.right;}}
  Target Target(float x,System.Action action){var go=new GameObject();go.transform.SetParent(root.transform,false);go.transform.localPosition=new Vector3(x,0,1.1f);var target=new Target{Transform=go.transform,HalfSize=new Vector2(.15f,.15f),OnSelect=action};pointer.Add(target);return target;}
  void Aim(int h,Target t){var origin=new Vector3(h==0?-.1f:.1f,-.1f,.2f);frame.Hand(h).Aim=new Ray(origin,(t.Transform.position-origin).normalized);}
  void Pinch(int h,bool pressed){frame.Hand(h).Joints[5]=pressed?frame.Hand(h).Joints[10]:Vector3.left;}
  void Tick(float dt=.4f)=>pointer.Update(frame,new Ray(Vector3.zero,Vector3.back),GazeKind.Head,dt);
  [Test] public void EachHandSelectsItsOwnRayRegardlessOfHeadDirection(){Aim(0,a);Aim(1,b);Tick();Pinch(0,true);Tick();Assert.AreEqual(1,left);Assert.AreEqual(0,right);}
  [Test] public void HeadRayAloneCannotHoverOrSelect(){frame.Left.Tracked=frame.Right.Tracked=false;pointer.Update(frame,new Ray(Vector3.zero,a.Transform.position.normalized),GazeKind.Head,1);Assert.IsNull(pointer.Hovered);Assert.AreEqual(0,pointer.Selections);}
  [Test] public void InactiveHandCannotPressActiveHandsTarget(){pointer.HandMask=1;Aim(0,a);Aim(1,b);Tick();Pinch(1,true);Tick();Assert.AreEqual(0,pointer.Selections);}
  [Test] public void ScaledTargetAcceptsFastPokeAndRequiresWithdrawal(){frame.Right.Tracked=false;frame.Left.AimValid=false;var point=a.Transform.position;frame.Left.Joints[10]=point-Vector3.forward*.12f;Tick();frame.Left.Joints[10]=point;Tick();Assert.AreEqual(1,left);Tick();Assert.AreEqual(1,left);frame.Left.Joints[10]=point-Vector3.forward*.04f;Tick();frame.Left.Joints[10]=point;Tick();Assert.AreEqual(2,left);}
  [Test] public void AppearingBehindTileDoesNotPress(){frame.Right.Tracked=false;frame.Left.AimValid=false;frame.Left.Joints[10]=a.Transform.position+Vector3.forward*.01f;Tick();Tick();Assert.AreEqual(0,left);}
  [Test] public void FloatingKnotAcceptsSideApproachAndRequiresWithdrawal(){frame.Right.Tracked=false;frame.Left.AimValid=false;a.TouchRadius=.026f;var p=a.Transform.position;frame.Left.Joints[10]=p+Vector3.right*.075f;Tick();frame.Left.Joints[10]=p+Vector3.right*.015f;Tick();Assert.AreEqual(1,left);Tick();Assert.AreEqual(1,left);frame.Left.Joints[10]=p+Vector3.up*.08f;Tick();frame.Left.Joints[10]=p+Vector3.up*.01f;Tick();Assert.AreEqual(2,left);}
  [Test] public void AppearingInsideFloatingKnotDoesNotAutoPress(){frame.Right.Tracked=false;frame.Left.AimValid=false;a.TouchRadius=.026f;frame.Left.Joints[10]=a.Transform.position;Tick();Tick();Assert.AreEqual(0,left);}
  [Test] public void RegainingTrackingWithHeldPinchRequiresRelease(){Aim(0,a);frame.Right.Tracked=false;Tick();frame.Left.Tracked=false;Tick();Pinch(0,true);frame.Left.Tracked=true;Tick();Assert.AreEqual(0,left);Pinch(0,false);Tick();Pinch(0,true);Tick();Assert.AreEqual(1,left);}
  [Test] public void ControllerTriggerUsesControllerRay(){frame.Left.IsController=true;frame.Right.Tracked=false;Aim(0,b);Tick();frame.Left.SelectPressed=true;Tick();Assert.AreEqual(1,right);Tick();Assert.AreEqual(1,right);}
  [Test] public void SystemGestureReturnWithHeldPinchRequiresFreshRelease(){frame.Right.Tracked=false;Aim(0,a);Tick();frame.Left.SelectSuppressed=true;Tick();Assert.IsNull(pointer.Hovered);frame.Left.SelectSuppressed=false;Pinch(0,true);Tick();Assert.AreEqual(0,left);Pinch(0,false);Tick();Pinch(0,true);Tick();Assert.AreEqual(1,left);}
  [Test] public void ReenablingAHandCannotTreatAnExistingPinchAsANewPress(){frame.Right.Tracked=false;Aim(0,a);pointer.HandMask=0;Tick();pointer.HandMask=1;Pinch(0,true);Tick();Assert.AreEqual(0,left);Pinch(0,false);Tick();Pinch(0,true);Tick();Assert.AreEqual(1,left);}
  [Test] public void FastDiagonalPokeCrossingBetweenSamplesSelectsOnce(){
   frame.Right.Tracked=false;frame.Left.AimValid=false;var p=a.Transform.position;
   frame.Left.Joints[10]=p+new Vector3(-.065f,0,-.04f);Tick();
   frame.Left.Joints[10]=p+new Vector3(.065f,0,.045f);Tick();
   Assert.AreEqual(1,left,"The fingertip crossed the target between input samples.");Tick();Assert.AreEqual(1,left);
  }
  [Test] public void VisibleFingertipPadCanPressWithoutPushingJointThroughPanel(){
   frame.Right.Tracked=false;frame.Left.AimValid=false;var p=a.Transform.position;
   frame.Left.Joints[10]=p-Vector3.forward*.04f;Tick();
   frame.Left.Joints[10]=p-Vector3.forward*.009f;Tick();
   Assert.AreEqual(1,left,"The rendered fingertip pad has reached the panel.");Tick();Assert.AreEqual(1,left);
   frame.Left.Joints[10]=p-Vector3.forward*.04f;Tick();frame.Left.Joints[10]=p-Vector3.forward*.009f;Tick();Assert.AreEqual(2,left);
  }
  [TearDown] public void Cleanup()=>Object.DestroyImmediate(root);
 }
}
