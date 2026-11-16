using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
namespace ShowOfHands {
 public sealed class XrHandSource : IHandSource, IGazeSource {
  readonly List<XRHandSubsystem> systems=new List<XRHandSubsystem>();
  readonly List<InputDevice> controllers=new List<InputDevice>();
  readonly Transform head,space; XRHandSubsystem system;
  public XrHandSource(Transform head,Transform space){this.head=head;this.space=space;}
  public void Read(HandFrame into){
   into.HeadPosition=head.position;into.HeadRotation=head.rotation;
   if(system==null||!system.running){SubsystemManager.GetSubsystems(systems);system=systems.Find(s=>s.running);}
   for(int h=0;h<2;h++){
    var hand=into.Hand(h);hand.Tracked=hand.AimValid=hand.IsController=hand.SelectPressed=hand.RotatePressed=hand.PinchKnown=hand.SelectSuppressed=false;hand.Stick=Vector2.zero;
    // Prefer a currently tracked bare hand. A controller left on the desk must
    // not shadow hand input when the runtime reports both devices.
    if(system!=null)ReadHand(h==0?system.leftHand:system.rightHand,hand);
    if(!hand.Tracked){ReadController(h,hand);continue;}
    var aim=h==0?MetaAimHand.left:MetaAimHand.right;
    if(hand.Tracked&&aim!=null){
     var flags=(MetaAimFlags)(ulong)aim.aimFlags.ReadValue();bool systemGesture=(flags&MetaAimFlags.SystemGesture)!=0;
     hand.SelectSuppressed=systemGesture;
     hand.PinchKnown=(flags&MetaAimFlags.Computed)!=0;
     hand.SelectPressed=hand.PinchKnown&&!systemGesture&&aim.indexPressed.isPressed;
     hand.AimValid=(flags&MetaAimFlags.Valid)!=0&&!systemGesture;
     if(hand.AimValid)hand.Aim=new Ray(space.TransformPoint(aim.devicePosition.ReadValue()),space.TransformDirection(aim.deviceRotation.ReadValue()*Vector3.forward));
    }
   }
  }
  #if SOH_INPUT_DIAGNOSTICS
  readonly List<XRHandSubsystem> diagnosticSystems=new List<XRHandSubsystem>();
  public string DiagnosticState(){
   SubsystemManager.GetSubsystems(diagnosticSystems);
   string result="subsystems="+diagnosticSystems.Count;
   foreach(var s in diagnosticSystems)result+=" [cached="+ReferenceEquals(s,system)+" running="+s.running+" L="+DiagnosticHand(s.leftHand)+" R="+DiagnosticHand(s.rightHand)+"]";
   for(int h=0;h<2;h++){var aim=h==0?MetaAimHand.left:MetaAimHand.right;result+=" aim"+h+"="+(aim==null?"none":aim.aimFlags.ReadValue().ToString());}
   return result;
  }
  static string DiagnosticHand(XRHand hand){
   if(!hand.isTracked)return "untracked";
   int valid=0;for(int i=0;i<26;i++){var id=i==0?XRHandJointID.Palm:i==1?XRHandJointID.Wrist:(XRHandJointID)(i+1);if(hand.GetJoint(id).TryGetPose(out var p))valid++;}
   return "tracked:"+valid+"/26";
  }
  #endif
  bool ReadController(int h,HandState hand){
   InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller|(h==0?InputDeviceCharacteristics.Left:InputDeviceCharacteristics.Right),controllers);
   foreach(var device in controllers){
    if(!device.isValid||!device.TryGetFeatureValue(CommonUsages.isTracked,out bool tracked)||!tracked||!device.TryGetFeatureValue(CommonUsages.devicePosition,out var position)||!device.TryGetFeatureValue(CommonUsages.deviceRotation,out var rotation))continue;
    hand.Tracked=hand.IsController=hand.AimValid=true;
    hand.Joints[ClipFormat.Palm]=space.TransformPoint(position);
    hand.Aim=new Ray(space.TransformPoint(position),space.TransformDirection(rotation*Vector3.forward));
    device.TryGetFeatureValue(CommonUsages.triggerButton,out hand.SelectPressed);
    device.TryGetFeatureValue(CommonUsages.primaryButton,out hand.RotatePressed);
    device.TryGetFeatureValue(CommonUsages.primary2DAxis,out hand.Stick);return true;
   }
   return false;
  }
  void ReadHand(XRHand hand,HandState into){
   into.Tracked=hand.isTracked;if(!into.Tracked)return;
   for(int i=0;i<26;i++){
    var id=i==0?XRHandJointID.Palm:i==1?XRHandJointID.Wrist:(XRHandJointID)(i+1);
    if(!hand.GetJoint(id).TryGetPose(out var p)){into.Tracked=false;return;}
    into.Joints[i]=space.TransformPoint(p.position);
   }
  }
  public GazeKind Read(out Ray ray){ray=default;return GazeKind.None;}
 }
 public sealed class SyntheticHandSource:IHandSource {
  readonly ClipPlayer player=new ClipPlayer();readonly Transform head;
  public SyntheticHandSource(HandClip clip,Transform head){player.Play(clip);this.head=head;}
  public void Read(HandFrame into){player.Advance(Time.deltaTime);into.HeadPosition=head.position;into.HeadRotation=head.rotation;
   for(int h=0;h<2;h++){var hand=into.Hand(h);hand.Tracked=true;hand.IsController=hand.AimValid=hand.PinchKnown=hand.SelectSuppressed=false;var joints=h==0?player.Pose.Left:player.Pose.Right;for(int j=0;j<26;j++)hand.Joints[j]=head.TransformPoint(joints[j]);}
  }
 }
}
