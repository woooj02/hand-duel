using System.Collections.Generic;

using UnityEngine;

namespace ShowOfHands {

 public static class SoundBank {

  static readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();

  static AudioClip BorrowCue(string name){
  int length=name=="borrow_refill"?48000:name=="borrow_launch"?22080:11520;var data=new float[length];
  float[] notes=name=="borrow_steal"?new[]{880f,1174.66f,1567.98f}:name=="borrow_claim"?new[]{659.25f,987.77f}:name=="borrow_refill"?new[]{329.63f,440f,659.25f,880f}:name=="borrow_dock"?new[]{523.25f}:new[]{220f,440f,880f};
  for(int i=0;i<length;i++){float t=i/48000f,p=i/(float)length;float sample=0;for(int n=0;n<notes.Length;n++){float start=n*.035f;if(t<start)continue;float q=t-start;sample+=Mathf.Sin(q*notes[n]*Mathf.PI*2)*Mathf.Exp(-q*(name=="borrow_refill"?5:14));}data[i]=sample*.14f/notes.Length*Mathf.Min(1,p*100)*Mathf.Min(1,(1-p)*30);}
  var clip=AudioClip.Create(name,length,1,48000,false);clip.SetData(data,0);return clip;
 }
 public static AudioClip Get(string name){if(clips.TryGetValue(name,out var result))return result;result=Resources.Load<AudioClip>("Audio/"+name);if(result==null&&name.StartsWith("borrow_"))result=BorrowCue(name);if(result==null){int n=9600;var data=new float[n];float hz=name=="guess_wrong"?240:660;for(int i=0;i<n;i++)data[i]=Mathf.Sin(i*hz*2*Mathf.PI/48000)*Mathf.Sin(Mathf.PI*i/n)*.15f;result=AudioClip.Create(name,n,1,48000,false);result.SetData(data,0);}clips[name]=result;return result;}

 }

}

