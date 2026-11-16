using UnityEngine;

namespace ShowOfHands
{
    public static class BorrowlightArt
    {
        static Mesh gem;
        static readonly Vector4 Glow=new Vector4(.5f,0,0,0);
        static readonly Color Neutral=Palette.Hex("#F9D993"),Grid=Palette.Hex("#35556C");
        static readonly Vector3[] Stars=MakeStars();
        static Vector3[] MakeStars(){var stars=new Vector3[72];for(int i=0;i<stars.Length;i++){float a=i*2.39996f,r=1.3f+(i%11)*.19f;stars[i]=new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r*.7f,3.3f+(i%7)*.27f);}return stars;}
        public static void Sky(ShapeBatch b,Matrix4x4 m,float time){var c=Palette.Hex("#7399B0");for(int i=0;i<Stars.Length;i++){float twinkle=Ui.ReducedMotion?.35f:.30f+Mathf.Sin(time*.45f+i)*.12f;b.Sphere(m.MultiplyPoint3x4(Stars[i]),(.003f+(i%3)*.001f)*m.lossyScale.x,Ink(c,twinkle),Vector4.zero);}}
        static Vector4 Ink(Color c,float a=1)=>new Vector4(c.r,c.g,c.b,a);
        static void Warm()
        {
            if(gem!=null)return;
            var top=Vector3.up;var down=Vector3.down;
            var ring=new[]{Vector3.left,Vector3.forward,Vector3.right,Vector3.back};
            var vertices=new Vector3[24];var triangles=new int[24];
            for(int i=0;i<4;i++){int at=i*6;vertices[at]=top;vertices[at+1]=ring[i];vertices[at+2]=ring[(i+1)%4];vertices[at+3]=down;vertices[at+4]=ring[(i+1)%4];vertices[at+5]=ring[i];for(int j=0;j<6;j++)triangles[at+j]=at+j;}
            gem=new Mesh{name="Borrowlight faceted beacon"};gem.vertices=vertices;gem.triangles=triangles;gem.RecalculateNormals();gem.RecalculateBounds();
        }
        static void OwnerMark(ShapeBatch b,Matrix4x4 m,Vector3 p,int owner,Color color)
        {
            p+=new Vector3(0,.060f,-.025f);float r=.016f;
            if(owner<0){ArcadeArt.Line(b,m,p+new Vector3(-r,-r,0),p+new Vector3(r,r,0),color,.0035f,.8f);ArcadeArt.Line(b,m,p+new Vector3(-r,r,0),p+new Vector3(r,-r,0),color,.0035f,.8f);}
            else if(owner==1){ArcadeArt.Line(b,m,p+new Vector3(-r,r,0),p+Vector3.down*r,color,.0035f);ArcadeArt.Line(b,m,p+Vector3.down*r,p+new Vector3(r,r,0),color,.0035f);}
            else{var a=p+new Vector3(-r,-r,0);var c=p+new Vector3(-r,r,0);var d=p;var e=p+new Vector3(r,r,0);var f=p+new Vector3(r,-r,0);ArcadeArt.Line(b,m,a,c,color,.0035f);ArcadeArt.Line(b,m,c,d,color,.0035f);ArcadeArt.Line(b,m,d,e,color,.0035f);ArcadeArt.Line(b,m,e,f,color,.0035f);}
        }
        public static void Draw(ShapeBatch b,Matrix4x4 m,BorrowlightGame g,int[] preview,float[] hits,int selected,int slot,float time,bool editable,bool replay=false,float replayT=0)
        {
            Warm();float scale=m.lossyScale.x;
            // A quiet frame makes depth legible without enclosing or moving the player.
            var low=new Vector3(-.56f,-.30f,-.25f);var high=new Vector3(.56f,.30f,.25f);
            for(int x=0;x<2;x++)for(int y=0;y<2;y++){
                var a=new Vector3(x==0?low.x:high.x,y==0?low.y:high.y,low.z);var c=a;c.z=high.z;ArcadeArt.Line(b,m,a,c,Grid,.0015f,.35f);
            }
            for(int z=0;z<2;z++)for(int y=0;y<2;y++)ArcadeArt.Line(b,m,new Vector3(low.x,y==0?low.y:high.y,z==0?low.z:high.z),new Vector3(high.x,y==0?low.y:high.y,z==0?low.z:high.z),Grid,.0015f,.35f);
            for(int x=0;x<2;x++)for(int z=0;z<2;z++)ArcadeArt.Line(b,m,new Vector3(x==0?low.x:high.x,low.y,z==0?low.z:high.z),new Vector3(x==0?low.x:high.x,high.y,z==0?low.z:high.z),Grid,.0015f,.35f);
            var color=ArcadeArt.Player(g.Player);int[] route=preview??g.Knots;
            bool changed=preview!=null&&selected>=0&&slot!=g.Knots[selected];
            Vector3 old=BorrowlightGame.Curve(route,0);
            for(int i=1;i<=BorrowlightGame.Samples;i++){
                var p=BorrowlightGame.Curve(route,i/(float)BorrowlightGame.Samples);
                ArcadeArt.Line(b,m,old,p,changed?Neutral:color,.0065f,.90f);
                if(changed&&i%2==0)ArcadeArt.Line(b,m,BorrowlightGame.Curve(g.Knots,(i-1)/(float)BorrowlightGame.Samples),BorrowlightGame.Curve(g.Knots,i/(float)BorrowlightGame.Samples),Grid,.002f,.45f);
                old=p;
            }
            for(int i=0;i<BorrowlightGame.LightCount;i++){
                bool potential=editable&&hits!=null&&hits[i]>=0&&!g.Spent[i]&&g.Owners[i]!=g.Player+1;
                float age=g.Pulse-(g.Player==0?g.Hits[i]:1-g.Hits[i]);bool flash=g.Phase==BorrowPhase.Pulse&&g.Changed[i]&&age>=0&&age<.22f;
                var c=g.Owners[i]==0?Neutral:ArcadeArt.Player(g.Owners[i]-1);float size=g.Spent[i]&&!flash?.015f:.025f;
                var p=g.Lights[i];var spin=Ui.ReducedMotion?Quaternion.identity:Quaternion.Euler(15,time*22+i*37,15);
                b.AddMesh(gem,m*Matrix4x4.TRS(p,spin,Vector3.one*size),Ink(flash?Color.Lerp(Color.white,c,age/.22f):c,g.Spent[i]&&!flash?.22f:1),Glow);
                if(flash){ArcadeArt.Ring(b,m,p,.045f+age*.16f,c,1-age/.22f);if(!Ui.ReducedMotion)for(int n=0;n<4;n++){float a=n*Mathf.PI*.5f;var spark=p+new Vector3(Mathf.Cos(a),Mathf.Sin(a),-.2f)*(.045f+age*.22f);b.Sphere(m.MultiplyPoint3x4(spark),.005f*scale,Ink(c,1-age/.22f),Glow);}}
                if(potential){ArcadeArt.Ring(b,m,p,.044f+(Ui.ReducedMotion?0:Mathf.Sin(time*4+i)*.003f),Neutral,.8f);}
                if(g.Spent[i])OwnerMark(b,m,p,-1,Color.Lerp(Grid,Palette.Text,.5f));else if(g.Owners[i]>0)OwnerMark(b,m,p,g.Owners[i],c);
            }
            if(editable&&selected>=0&&selected!=g.Locked)for(int s=0;s<9;s++){
                var p=BorrowlightGame.KnotPoint(selected,s);b.Sphere(m.MultiplyPoint3x4(p),.006f*scale,Ink(s==slot?Neutral:Grid,s==slot?1:.5f),Glow);
                if(s==slot)ArcadeArt.Ring(b,m,p,.055f,Neutral,.75f);
            }
            for(int k=0;k<3;k++){
                var p=BorrowlightGame.KnotPoint(k,route[k]);bool locked=k==g.Locked;
                b.Sphere(m.MultiplyPoint3x4(p),.035f*scale,Ink(Palette.Hex("#132A3B")),Vector4.zero);
                ArcadeArt.Ring(b,m,p,.045f,locked?Grid:k==selected?Neutral:color,locked?.5f:1);
                if(locked)for(int bar=-1;bar<=1;bar+=2)ArcadeArt.Line(b,m,p+new Vector3(-.022f,bar*.009f,-.035f),p+new Vector3(.022f,bar*.009f,-.035f),Neutral,.004f);
            }
            for(int end=0;end<2;end++){
                var p=new Vector3(end==0?-.55f:.55f,0,0);ArcadeArt.Ring(b,m,p,.065f,ArcadeArt.Player(end));
                b.Sphere(m.MultiplyPoint3x4(p),.018f*scale,Ink(ArcadeArt.Player(end)),Glow);
            }
            if(g.Phase==BorrowPhase.Pulse||replay){
                float t=replay?replayT:g.Pulse;float along=g.Player==0?t:1-t;
                for(int i=8;i>=0;i--){float a=Mathf.Clamp01(along+(g.Player==0?-1:1)*i*.012f);var p=BorrowlightGame.Curve(g.Knots,a);b.Sphere(m.MultiplyPoint3x4(p),(.022f-i*.0018f)*scale,Ink(i==0?Color.white:color,1-i*.085f),Glow);}
            }
        }
    }
}
