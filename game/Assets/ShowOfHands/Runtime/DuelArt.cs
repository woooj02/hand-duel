using System.Collections.Generic;
using UnityEngine;
namespace ShowOfHands {
 public static class DuelArt {
  static Mesh rock,paper,blade;
  static readonly Vector4 glow=new Vector4(.08f,0,0,0);
  static Vector4 Ink(Color c,float a=1)=>new Vector4(c.r,c.g,c.b,a);
  static Matrix4x4 At(Vector3 p,Quaternion q,float size)=>Matrix4x4.TRS(p,q,Vector3.one*size);
  public static void Warm(){if(rock!=null)return;
   float t=(1+Mathf.Sqrt(5))/2;var v=new[]{new Vector3(-1,t,0),new Vector3(1,t,0),new Vector3(-1,-t,0),new Vector3(1,-t,0),new Vector3(0,-1,t),new Vector3(0,1,t),new Vector3(0,-1,-t),new Vector3(0,1,-t),new Vector3(t,0,-1),new Vector3(t,0,1),new Vector3(-t,0,-1),new Vector3(-t,0,1)};
   int[] f={0,11,5,0,5,1,0,1,7,0,7,10,0,10,11,1,5,9,5,11,4,11,10,2,10,7,6,7,1,8,3,9,4,3,4,2,3,2,6,3,6,8,3,8,9,4,9,5,2,4,11,6,2,10,8,6,7,9,8,1};var points=new List<Vector3>();
   for(int i=0;i<f.Length;i++){var p=v[f[i]].normalized;points.Add(p*(.45f+.045f*Mathf.Sin(f[i]*2.7f)));}rock=Flat("Duel faceted rock",points);
   points.Clear();for(int side=0;side<2;side++)for(int col=0;col<4;col++){float x0=-.5f+col*.25f,x1=x0+.25f;float z0=(col%2==0?1:-1)*.055f,z1=-z0;var a=new Vector3(x0,-.42f,z0);var b=new Vector3(x0,.42f,z0);var c=new Vector3(x1,.42f,z1);var d=new Vector3(x1,-.42f,z1);if(side==0){points.Add(a);points.Add(b);points.Add(c);points.Add(a);points.Add(c);points.Add(d);}else{points.Add(c);points.Add(b);points.Add(a);points.Add(d);points.Add(c);points.Add(a);}}paper=Flat("Duel folded paper",points);
   points.Clear();var tip=new Vector3(0,1,0);var l=new Vector3(-.10f,0,0);var r=new Vector3(.10f,0,0);points.Add(l);points.Add(tip);points.Add(r);points.Add(r);points.Add(tip);points.Add(l);blade=Flat("Duel scissor blade",points);
  }
  static Mesh Flat(string name,List<Vector3> positions){var mesh=new Mesh{name=name};mesh.SetVertices(positions);var indices=new int[positions.Count];for(int i=0;i<indices.Length;i++)indices[i]=i;mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;}
  static void Model(ShapeBatch b,Mesh mesh,Matrix4x4 m,Color c,float alpha){b.AddMesh(mesh,m,Ink(c,alpha),glow);}
  public static void Ring(ShapeBatch b,Matrix4x4 m,float radius,Color color,float alpha,int segments=40){for(int i=0;i<segments;i++){float a=i*Mathf.PI*2/segments,c=(i+1)*Mathf.PI*2/segments;var p=new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius);var q=new Vector3(Mathf.Cos(c)*radius,0,Mathf.Sin(c)*radius);b.Tube(m.MultiplyPoint3x4(p),m.MultiplyPoint3x4(q),.002f*m.lossyScale.x,Ink(color,alpha),glow);}}
  public static void Arena(ShapeBatch b,Matrix4x4 stage,float time){Warm();
   Ring(b,stage*At(new Vector3(0,-.17f,1.22f),Quaternion.identity,1),.40f,Palette.Accent,.45f,56);
   for(int h=0;h<2;h++){var g=Glove.Find(h==0?"violet":"mint");var pos=new Vector3(h==0?-.29f:.29f,-.18f,1.13f);b.Ellipsoid(stage.MultiplyPoint3x4(pos),stage.rotation,Vector3.Scale(new Vector3(.15f,.008f,.12f),stage.lossyScale),Ink(g.Base,.55f),glow);Ring(b,stage*At(pos+Vector3.up*.008f,Quaternion.identity,1),.13f,g.Tip,.9f,28);}
   for(int i=0;i<14;i++){float a=i*.4488f+time*.11f;var p=new Vector3(Mathf.Cos(a)*.40f,-.17f+Mathf.Sin(time*1.5f+i)*.005f,1.22f+Mathf.Sin(a)*.40f);b.Sphere(stage.MultiplyPoint3x4(p),.003f*stage.lossyScale.x,Ink(Palette.Accent,.8f),glow);}
  }
  public static void Prop(ShapeBatch b,Matrix4x4 world,RpsMove move,Glove g,float alpha,float phase){Warm();if(alpha<=.01f||move==RpsMove.Unknown)return;
   if(move==RpsMove.Rock){Model(b,rock,world*Matrix4x4.TRS(Vector3.zero,Quaternion.Euler(10,phase*10,15),new Vector3(.23f,.20f,.21f)),Color.Lerp(Palette.Hex("#18212E"),g.Base,.10f),alpha);
    for(int i=0;i<4;i++){var p=new Vector3(Mathf.Sin(i*2)*.055f,Mathf.Cos(i*2.3f)*.05f,-.075f);var q=p+new Vector3(.015f,.025f,0);b.Tube(world.MultiplyPoint3x4(p),world.MultiplyPoint3x4(q),.002f*world.lossyScale.x,Ink(g.Tip,alpha),glow);}return;}
   if(move==RpsMove.Paper){Model(b,paper,world*Matrix4x4.TRS(Vector3.zero,Quaternion.Euler(0,Mathf.Sin(phase*2)*8,8),new Vector3(.25f,.25f,.25f)),Color.Lerp(Palette.Hex("#B9B2A2"),g.Base,.12f),alpha);return;}
   float open=16+Mathf.Sin(phase*7)*9;
   for(int i=-1;i<=1;i+=2){var transform=world*At(Vector3.zero,Quaternion.Euler(0,0,open*i),1);Model(b,blade,transform*At(new Vector3(0,-.015f,0),Quaternion.identity,.21f),Palette.Hex("#8295A8"),alpha);
    var center=new Vector3(0,-.07f,0);for(int j=0;j<14;j++){float a=j*Mathf.PI*2/14,c=(j+1)*Mathf.PI*2/14;b.Tube(transform.MultiplyPoint3x4(center+new Vector3(Mathf.Cos(a)*.026f,Mathf.Sin(a)*.033f,0)),transform.MultiplyPoint3x4(center+new Vector3(Mathf.Cos(c)*.026f,Mathf.Sin(c)*.033f,0)),.006f*transform.lossyScale.x,Ink(g.Base,alpha),glow);}}
   b.Sphere(world.MultiplyPoint3x4(Vector3.zero),.012f*world.lossyScale.x,Ink(Palette.Accent,alpha),glow);
  }
  public static void FillPose(Vector3[] joints,RpsMove move,int hand){
   for(int i=0;i<26;i++)joints[i]=Vector3.zero;float sign=hand==0?1:-1;joints[0]=new Vector3(0,.045f,0);joints[1]=Vector3.zero;
   for(int i=0;i<4;i++)joints[2+i]=new Vector3(sign*(.02f+i*.018f),.025f+i*.013f,-i*.004f);
   for(int f=0;f<4;f++){int m=6+f*5;float x=(f-1.5f)*.019f;bool open=move==RpsMove.Paper||(move==RpsMove.Scissors&&f<2);float length=f==3?.022f:.028f;
    joints[m]=new Vector3(x,.035f,0);joints[m+1]=new Vector3(x,.07f,0);
    if(open){joints[m+2]=new Vector3(x,.07f+length,0);joints[m+3]=new Vector3(x,.07f+length*1.7f,0);joints[m+4]=new Vector3(x,.07f+length*2.3f,0);}
    else{joints[m+2]=new Vector3(x,.085f,.018f);joints[m+3]=new Vector3(x,.070f,.037f);joints[m+4]=new Vector3(x,.047f,.026f);}}
  }
  public static void Clash(ShapeBatch b,Matrix4x4 stage,Vector3[] left,Vector3[] right,RpsMove l,RpsMove r,float time,bool reduced){
   int winner=RpsPose.Winner(l,r);float approach=Ui.EaseInOut(Mathf.Clamp01((time-.25f)/.65f));float impact=Mathf.Clamp01((time-.9f)/.8f);float separation=winner==0?Mathf.Lerp(.09f,.22f,impact):Mathf.Lerp(.29f,.075f,approach);
   for(int h=0;h<2;h++){float sign=h==0?-1:1;bool won=winner==(h==0?-1:1);var g=Glove.Find(h==0?"violet":"mint");float alpha=winner==0||won?1:1-impact;float x=sign*separation;if(won&&time>1)x=Mathf.Lerp(x,0,impact);
    var center=new Vector3(x,.015f+(reduced?0:Mathf.Sin(Mathf.PI*approach)*.055f),1.08f);var m=stage*At(center,Quaternion.Euler(0,0,reduced?0:sign*(1-approach)*-14),1);
    var j=h==0?left:right;var handSpace=m*At(new Vector3(0,-.125f,.035f),Quaternion.Euler(0,180,0),.8f)*Matrix4x4.Translate(-j[1]);HandDrawer.Draw(b,j,handSpace,.8f,g,alpha,false);
    Prop(b,m,h==0?l:r,g,alpha,time);
   }
   if(winner!=0&&time>.9f&&time<2.4f){float t=time-.9f;var g=Glove.Find(winner<0?"violet":"mint");
    if(!reduced)for(int i=0;i<28;i++){float a=i*2.39996f;float speed=.11f+(i%5)*.035f;var p=new Vector3(Mathf.Cos(a)*t*speed,.025f+Mathf.Sin(a)*t*speed-.12f*t*t,1.08f+Mathf.Sin(i*3.1f)*t*.06f);b.Sphere(stage.MultiplyPoint3x4(p),.004f*stage.lossyScale.x*(1-t/1.5f),Ink(g.Tip,1-t/1.5f),glow);}
    var win=winner<0?l:r;
    if(win==RpsMove.Paper){for(int i=0;i<8;i++){float a=i*45;var q=Quaternion.Euler(0,a,Mathf.Lerp(60,0,impact));var p=new Vector3(Mathf.Sin(a*Mathf.Deg2Rad)*.10f,.015f,1.08f+Mathf.Cos(a*Mathf.Deg2Rad)*.10f);Model(b,paper,stage*Matrix4x4.TRS(p,q,new Vector3(.12f,.18f,.12f)),g.Tip,1-impact*.5f);}}
    else for(int i=0;i<6;i++){float a=i*Mathf.PI/3;var p=new Vector3(Mathf.Cos(a)*t*.11f,-.02f+Mathf.Sin(a)*t*.07f-.13f*t*t,1.08f);Model(b,win==RpsMove.Rock?blade:paper,stage*Matrix4x4.TRS(p,Quaternion.Euler(t*i*25,0,i*60+t*60),Vector3.one*(win==RpsMove.Rock?.07f:.055f)),g.Tip,1-impact);}
   }
  }
 }
}
