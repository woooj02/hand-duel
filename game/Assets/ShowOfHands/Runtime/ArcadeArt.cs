using UnityEngine;

namespace ShowOfHands
{
    public static class ArcadeArt
    {
        static Mesh cube;
        static readonly Vector4 Fx=new Vector4(.12f,0,0,0);
        public static readonly Color Gold=Palette.Hex("#FFD782"),Dark=Palette.Hex("#172840"),Ice=Palette.Hex("#74BDD4");
        static readonly Color[] Blocks={Palette.Hex("#67E7F0"),Palette.Hex("#FFD782"),Palette.Hex("#C2A6FF"),Palette.Hex("#A6F8DD"),Palette.Hex("#FF8495"),Palette.Hex("#80AAFF"),Palette.Hex("#FFAD73"),Palette.Hex("#587087")};
        public static Color Player(int player)=>Palette.Hex(player==0?"#C5ACFF":"#A6F8DD");
        static Vector4 Ink(Color color,float alpha=1)=>new Vector4(color.r,color.g,color.b,alpha);
        static void Warm(){if(cube!=null)return;var go=GameObject.CreatePrimitive(PrimitiveType.Cube);cube=go.GetComponent<MeshFilter>().sharedMesh;go.SetActive(false);Object.Destroy(go);}
        public static void Box(ShapeBatch b,Matrix4x4 m,Vector3 p,Vector3 size,Color color,float alpha=1)
        {Warm();b.AddMesh(cube,m*Matrix4x4.TRS(p,Quaternion.identity,size),Ink(color,alpha),Fx);}
        public static void Line(ShapeBatch b,Matrix4x4 m,Vector3 a,Vector3 c,Color color,float radius=.003f,float alpha=1)=>b.Tube(m.MultiplyPoint3x4(a),m.MultiplyPoint3x4(c),radius*m.lossyScale.x,Ink(color,alpha),Fx);
        public static void Ring(ShapeBatch b,Matrix4x4 m,Vector3 p,float radius,Color color,float alpha=1)=>DuelArt.Ring(b,m*Matrix4x4.TRS(p,Quaternion.Euler(90,0,0),Vector3.one),radius,color,alpha,40);
        public static void Mark(ShapeBatch b,Matrix4x4 m,Vector3 p,int player,float scale=1,float alpha=1)
        {
            var color=Player(player);float s=.048f*scale;
            if(player==0){Line(b,m,p+new Vector3(-s,-s,0),p+new Vector3(s,s,0),color,.009f*scale,alpha);Line(b,m,p+new Vector3(-s,s,0),p+new Vector3(s,-s,0),color,.009f*scale,alpha);}
            else Ring(b,m,p,.055f*scale,color,alpha);
        }
        public static void Tic(ShapeBatch b,Matrix4x4 m,TicTacToeGame game,Vector3 cursor,int cell,float progress,float time)
        {
            Box(b,m,new Vector3(0,0,.065f),new Vector3(.65f,.65f,.032f),Dark);
            for(int i=0;i<9;i++)
            {
                var p=TicTacToeGame.Centre(i);bool hover=i==cell&&!game.Ended;
                Box(b,m,p+Vector3.forward*.03f,new Vector3(.174f,.174f,.025f),hover?Color.Lerp(Dark,Player(game.Player),.25f):Palette.Hex("#243B56"));
                if(game.Board[i]>0)Mark(b,m,p+Vector3.back*.002f,game.Board[i]-1);
                else if(hover){Mark(b,m,p+Vector3.back*.01f,game.Player,.85f,.28f);Ring(b,m,p+Vector3.back*.015f,.079f,Player(game.Player),.8f);}
            }
            if(!game.Ended&&cell>=0)
            {
                b.Sphere(m.MultiplyPoint3x4(cursor+Vector3.back*.045f),.006f*m.lossyScale.x,Ink(Gold),Fx);
                if(progress>0)Ring(b,m,TicTacToeGame.Centre(cell)+Vector3.back*.022f,.082f+progress*.008f,Gold);
            }
            if(game.WinningLine.Length==3)
            {
                var a=TicTacToeGame.Centre(game.WinningLine[0])+Vector3.back*.035f;var c=TicTacToeGame.Centre(game.WinningLine[2])+Vector3.back*.035f;
                Line(b,m,Vector3.LerpUnclamped(a,c,-.13f),Vector3.LerpUnclamped(a,c,1.13f),Gold,.007f);
                Celebration(b,m,time,game.Winner-1);
            }
        }
        static void Celebration(ShapeBatch b,Matrix4x4 m,float time,int player)
        {
            if(Ui.ReducedMotion)return;
            float t=time%2.4f;
            for(int i=0;i<22;i++){float a=i*2.39996f;var p=new Vector3(Mathf.Cos(a)*t*.12f,.13f+Mathf.Sin(a)*t*.15f-.14f*t*t,-.08f);if(p.y<-.35f)continue;b.Sphere(m.MultiplyPoint3x4(p),.004f*m.lossyScale.x,Ink(i%2==0?Gold:Player(player)),Fx);}
        }
        public const float Cell=.054f;
        public static Vector3 BlockCentre(float x,float y)=>new Vector3((x-2.5f)*Cell,(y-4.5f)*Cell,0);
        static void Block(ShapeBatch b,Matrix4x4 m,float x,float y,int kind,bool ghost=false)
        {
            var p=BlockCentre(x,y);var color=Blocks[Mathf.Clamp(kind-1,0,7)];
            if(ghost)
            {
                float s=Cell*.44f;var z=Vector3.back*.02f;
                Line(b,m,p+new Vector3(-s,-s,0)+z,p+new Vector3(s,-s,0)+z,color,.0014f);
                Line(b,m,p+new Vector3(s,-s,0)+z,p+new Vector3(s,s,0)+z,color,.0014f);
                Line(b,m,p+new Vector3(s,s,0)+z,p+new Vector3(-s,s,0)+z,color,.0014f);
                Line(b,m,p+new Vector3(-s,s,0)+z,p+new Vector3(-s,-s,0)+z,color,.0014f);return;
            }
            Box(b,m,p,new Vector3(Cell*.91f,Cell*.91f,.034f),Color.Lerp(color,Dark,.15f));
            Box(b,m,p+Vector3.back*.019f,new Vector3(Cell*.73f,Cell*.73f,.006f),color);
        }
        public static void BlocksBoard(ShapeBatch b,Matrix4x4 m,FallingBlocksGame game,float time)
        {
            Box(b,m,new Vector3(0,0,.055f),new Vector3(.354f,.572f,.035f),Dark);
            for(int y=0;y<10;y++)for(int x=0;x<6;x++)
            {
                int value=game.Board[y*6+x];var p=BlockCentre(x,y);
                if(value>0){if(game.ClearRows.Contains(y)&&Mathf.Sin(game.ClearProgress*35)>0)Block(b,m,x,y,2);else Block(b,m,x,y,value);}
                else Box(b,m,p+Vector3.forward*.028f,new Vector3(Cell*.94f,Cell*.94f,.004f),Palette.Hex("#20364D"));
            }
            var frame=Player(game.Player);
            for(int side=-1;side<=1;side+=2)Line(b,m,new Vector3(side*.176f,-.285f,0),new Vector3(side*.176f,.285f,0),frame,.004f);
            if(game.Phase==BlockPhase.Falling)
            {
                foreach(var p in FallingShape.Cells(game.Kind,game.Rotation))Block(b,m,game.X+p.x,game.GhostY+p.y,game.Kind+1,true);
                foreach(var p in FallingShape.Cells(game.Kind,game.Rotation))Block(b,m,game.X+p.x,game.Y+p.y-game.FallProgress,game.Kind+1);
            }
            var next=FallingShape.Cells(game.NextKind,0);
            var nextM=m*Matrix4x4.TRS(new Vector3(.30f,.12f,0),Quaternion.identity,Vector3.one*.65f);
            foreach(var p in next)Block(b,nextM,p.x+1.5f,p.y+4,game.NextKind+1);
            if(game.Phase==BlockPhase.Result&&game.Rows>0)Celebration(b,m,time,game.Winner<0?0:game.Winner);
        }
    }
}
