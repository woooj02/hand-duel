using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShowOfHands
{
    public sealed class TicTacToeGame
    {
        static readonly int[][] Lines={new[]{0,1,2},new[]{3,4,5},new[]{6,7,8},new[]{0,3,6},new[]{1,4,7},new[]{2,5,8},new[]{0,4,8},new[]{2,4,6}};
        public readonly int[] Board=new int[9],Wins=new int[2];
        public int Player {get;private set;} public int Starter {get;private set;} public int Round {get;private set;}
        public int Winner {get;private set;}=-1; public int Draws {get;private set;} public bool Lesson {get;private set;}
        public int[] WinningLine {get;private set;}=Array.Empty<int>();
        public bool Ended=>Winner>=0;
        public void Start(bool lesson=false){Lesson=lesson;Wins[0]=Wins[1]=Draws=Round=Starter=0;ResetBoard();}
        void ResetBoard()
        {
            Array.Clear(Board,0,9);Player=Starter;Winner=-1;WinningLine=Array.Empty<int>();
            if(Lesson){Board[0]=Board[1]=1;Board[3]=Board[4]=2;Player=0;}
        }
        public void NextRound(){if(!Ended)return;Round++;Starter=1-Starter;ResetBoard();}
        public bool Place(int player,int cell)
        {
            if(Ended||player!=Player||cell<0||cell>8||Board[cell]!=0)return false;
            Board[cell]=player+1;
            foreach(var line in Lines)if(Board[line[0]]==player+1&&Board[line[1]]==player+1&&Board[line[2]]==player+1)
            {Winner=player+1;WinningLine=(int[])line.Clone();if(!Lesson)Wins[player]++;return true;}
            bool full=true;foreach(int value in Board)if(value==0)full=false;
            if(full){Winner=0;if(!Lesson)Draws++;}else Player=1-Player;return true;
        }
        public static Vector3 Centre(int cell)=>new Vector3((cell%3-1)*.19f,(1-cell/3)*.19f,0);
        public static int CellAt(Vector3 cursor)
        {if(Mathf.Abs(cursor.x)>.295f||Mathf.Abs(cursor.y)>.295f)return -1;int col=Mathf.Clamp(Mathf.RoundToInt(cursor.x/.19f)+1,0,2),row=Mathf.Clamp(1-Mathf.RoundToInt(cursor.y/.19f),0,2);return row*3+col;}
    }

    public enum BlockPhase { Falling,Clearing,Result }
    public static class FallingShape
    {
        // I, O, T, S, Z, J, L. Coordinates use the lower-left corner, y upwards.
        static readonly Vector2Int[][] Bases={
            new[]{new Vector2Int(0,0),new Vector2Int(1,0),new Vector2Int(2,0),new Vector2Int(3,0)},
            new[]{new Vector2Int(0,0),new Vector2Int(1,0),new Vector2Int(0,1),new Vector2Int(1,1)},
            new[]{new Vector2Int(0,0),new Vector2Int(1,0),new Vector2Int(2,0),new Vector2Int(1,1)},
            new[]{new Vector2Int(0,0),new Vector2Int(1,0),new Vector2Int(1,1),new Vector2Int(2,1)},
            new[]{new Vector2Int(1,0),new Vector2Int(2,0),new Vector2Int(0,1),new Vector2Int(1,1)},
            new[]{new Vector2Int(0,0),new Vector2Int(1,0),new Vector2Int(2,0),new Vector2Int(0,1)},
            new[]{new Vector2Int(0,0),new Vector2Int(1,0),new Vector2Int(2,0),new Vector2Int(2,1)}};
        static readonly Vector2Int[][] Cache=Make();
        static Vector2Int[][] Make()
        {
            var all=new Vector2Int[28][];
            for(int kind=0;kind<7;kind++)for(int rotation=0;rotation<4;rotation++)
            {
                var cells=(Vector2Int[])Bases[kind].Clone();
                for(int r=0;r<rotation;r++)for(int i=0;i<4;i++)cells[i]=new Vector2Int(cells[i].y,-cells[i].x);
                int minX=99,minY=99;foreach(var p in cells){minX=Mathf.Min(minX,p.x);minY=Mathf.Min(minY,p.y);}
                for(int i=0;i<4;i++)cells[i]-=new Vector2Int(minX,minY);all[kind*4+rotation]=cells;
            }return all;
        }
        public static Vector2Int[] Cells(int kind,int rotation)=>Cache[Mathf.Clamp(kind,0,6)*4+((rotation%4+4)%4)];
        public static int Width(int kind,int rotation){int value=0;foreach(var p in Cells(kind,rotation))value=Mathf.Max(value,p.x+1);return value;}
        public static int Height(int kind,int rotation){int value=0;foreach(var p in Cells(kind,rotation))value=Mathf.Max(value,p.y+1);return value;}
    }
    public sealed class FallingBlocksGame
    {
        public const int Width=6,Height=10,PieceLimit=24;
        public readonly int[] Board=new int[Width*Height],Score=new int[2];
        public BlockPhase Phase {get;private set;}
        public int Player {get;private set;} public int Pieces {get;private set;} public int Rows {get;private set;}
        public int Kind {get;private set;} public int Rotation {get;private set;} public int X {get;private set;} public int Y {get;private set;}
        public int NextKind=>bag[(bagAt+1)%bag.Length];
        public int Winner {get;private set;}=-2;
        public bool Lesson {get;private set;} public bool Solo {get;private set;}
        public readonly List<int> ClearRows=new List<int>();
        public string Verdict {get;private set;}="";
        float fallClock,clearClock;bool softDropping;int bagAt;int[] bag;
        public float FallInterval=>Lesson?2.5f:Mathf.Max(.48f,1.45f-Rows*.04f);
        float ActiveFallInterval=>softDropping?.08f:FallInterval;
        public float FallProgress=>Phase==BlockPhase.Falling&&Fits(Kind,Rotation,X,Y-1)?Mathf.Clamp01(fallClock/ActiveFallInterval):0;
        public float ClearProgress=>Phase==BlockPhase.Clearing?Mathf.Clamp01(clearClock/.55f):0;
        public int GhostY {get{int y=Y;while(Fits(Kind,Rotation,X,y-1))y--;return y;}}
        public void Start(bool lesson=false,bool solo=false,int seed=137)
        {
            Lesson=lesson;Solo=solo||lesson;Player=Pieces=Rows=bagAt=0;Score[0]=Score[1]=0;Winner=-2;Verdict="";
            Array.Clear(Board,0,Board.Length);ClearRows.Clear();bag=new int[70];var random=new System.Random(seed);
            for(int k=0;k<10;k++){for(int i=0;i<7;i++)bag[k*7+i]=i;for(int i=6;i>0;i--){int j=random.Next(i+1),a=k*7+i,b=k*7+j;int old=bag[a];bag[a]=bag[b];bag[b]=old;}}
            if(lesson){bag[0]=1;Board[0]=Board[1]=Board[4]=Board[5]=8;}
            Spawn();
        }
        void Spawn(){Kind=bag[bagAt%bag.Length];Rotation=0;X=(Width-FallingShape.Width(Kind,0))/2;Y=Height-FallingShape.Height(Kind,0);fallClock=clearClock=0;softDropping=false;Phase=BlockPhase.Falling;}
        public bool Fits(int kind,int rotation,int x,int y)
        {
            foreach(var p in FallingShape.Cells(kind,rotation))
            {int cx=x+p.x,cy=y+p.y;if(cx<0||cx>=Width||cy<0||cy>=Height||Board[cy*Width+cx]!=0)return false;}return true;
        }
        public bool Move(int direction)=>MoveTo(X+Math.Sign(direction));
        public bool MoveTo(int x)
        {
            if(Phase!=BlockPhase.Falling)return false;x=Mathf.Clamp(x,0,Width-FallingShape.Width(Kind,Rotation));int original=X;
            while(X!=x){int next=X+Math.Sign(x-X);if(!Fits(Kind,Rotation,next,Y))break;X=next;}return original!=X;
        }
        public bool Rotate()
        {
            if(Phase!=BlockPhase.Falling||Kind==1)return false;
            int rotation=(Rotation+1)%4;
            int y=Mathf.Min(Y,Height-FallingShape.Height(Kind,rotation));
            foreach(int offset in new[]{0,-1,1,-2,2})if(Fits(Kind,rotation,X+offset,y)){Rotation=rotation;X+=offset;Y=y;return true;}return false;
        }
        public bool Drop(){if(Phase!=BlockPhase.Falling)return false;Y=GhostY;Lock();return true;}
        public void Step(float dt,bool softDrop=false)
        {
            if(float.IsNaN(dt)||float.IsInfinity(dt)||dt<=0||Phase==BlockPhase.Result)return;dt=Mathf.Min(dt,.1f);
            if(Phase==BlockPhase.Clearing){clearClock+=dt;if(clearClock>=.55f)Collapse();return;}
            if(softDrop!=softDropping){fallClock=Mathf.Clamp01(fallClock/ActiveFallInterval)*(softDrop?.08f:FallInterval);softDropping=softDrop;}
            fallClock+=dt;
            while(fallClock>=ActiveFallInterval&&Phase==BlockPhase.Falling){fallClock-=ActiveFallInterval;if(Fits(Kind,Rotation,X,Y-1))Y--;else Lock();}
        }
        void Lock()
        {
            foreach(var p in FallingShape.Cells(Kind,Rotation))Board[(Y+p.y)*Width+X+p.x]=Kind+1;
            Pieces++;ClearRows.Clear();
            for(int y=0;y<Height;y++){bool full=true;for(int x=0;x<Width;x++)if(Board[y*Width+x]==0)full=false;if(full)ClearRows.Add(y);}
            if(ClearRows.Count>0){Phase=BlockPhase.Clearing;clearClock=0;}else FinishTurn();
        }
        void Collapse()
        {
            int count=ClearRows.Count;Rows+=count;Score[Player]+=count;
            int destination=0;
            for(int source=0;source<Height;source++)if(!ClearRows.Contains(source))
            {for(int x=0;x<Width;x++)Board[destination*Width+x]=Board[source*Width+x];destination++;}
            for(int y=destination;y<Height;y++)for(int x=0;x<Width;x++)Board[y*Width+x]=0;
            ClearRows.Clear();
            if(Lesson){End(0,"ROW CLEARED! YOU GOT IT.");return;}FinishTurn();
        }
        void FinishTurn()
        {
            if(Pieces>=PieceLimit){End(Score[0]==Score[1]?-1:Score[0]>Score[1]?0:1,Solo?"SESSION COMPLETE":"24 BLOCKS • FINAL SCORE");return;}
            int previous=Player;bagAt++;if(!Solo)Player=1-Player;
            Spawn();if(!Fits(Kind,Rotation,X,Y))End(Solo?-1:1-previous,Solo?"THE STACK REACHED THE TOP":"PLAYER "+(previous+1)+" FILLED THE TOP");
        }
        void End(int winner,string verdict){Winner=winner;Verdict=verdict;Phase=BlockPhase.Result;}
    }
    public enum ArcadeGesture { None,Rotate,Confirm }
    public sealed class ArcadeGestureInput
    {
        bool releaseNeeded=true,wasFist;float openTime;
        public float Progress=>0;
        public void Reset(){releaseNeeded=true;wasFist=false;openTime=0;}
        public ArcadeGesture Step(bool pinch,bool fist,float dt,bool enabled=true)
        {
            if(!enabled){Reset();return ArcadeGesture.None;}
            if(fist){bool turn=!wasFist;wasFist=true;releaseNeeded=true;openTime=0;return turn?ArcadeGesture.Rotate:ArcadeGesture.None;}
            wasFist=false;
            if(!pinch){openTime+=Mathf.Clamp(dt,0,.1f);if(openTime>=.04f)releaseNeeded=false;return ArcadeGesture.None;}
            openTime=0;
            if(releaseNeeded)return ArcadeGesture.None;
            releaseNeeded=true;return ArcadeGesture.Confirm;
        }
    }
}
