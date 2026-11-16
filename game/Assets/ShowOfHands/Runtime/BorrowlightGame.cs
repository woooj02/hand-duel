using System;
using UnityEngine;

namespace ShowOfHands
{
    public enum BorrowPhase { Planning, Pulse, Refill, Result }
    // Deterministic, clock-independent rules. Both real players use these same actions.
    public sealed class BorrowlightGame
    {
        public const int TurnLimit=12, Samples=80, LightCount=18;
        public const float CatchRadius=.072f;
        public readonly int[] Knots=new int[3], Owners=new int[LightCount], Score=new int[2];
        public readonly Vector3[] Lights=new Vector3[LightCount];
        public readonly float[] Hits=new float[LightCount];
        public readonly bool[] Resolved=new bool[LightCount], Spent=new bool[LightCount], Changed=new bool[LightCount];
        readonly Vector3[] path=new Vector3[Samples+1];
        readonly int[] scratch=new int[3];
        public BorrowPhase Phase {get;private set;}
        public int Player {get;private set;} public int Turn {get;private set;}
        public int Limit {get;private set;} public int Seed {get;private set;}
        public int Locked {get;private set;}=-1; public int Winner {get;private set;}=-2;
        public float Pulse {get;private set;}
        public int LastGain {get;private set;} public int LastSteals {get;private set;}
        public int ClaimVersion {get;private set;}
        public int Heist {get;private set;} int starter;float refill;
        public void Start(int seed=0,int starter=0,int limit=TurnLimit)
        {
            Seed=seed;this.starter=starter==1?1:0;Player=this.starter;Turn=Heist=0;refill=0;Limit=Mathf.Clamp(limit,1,TurnLimit);Locked=-1;Winner=-2;
            Phase=BorrowPhase.Planning;Pulse=0;LastGain=LastSteals=ClaimVersion=0;
            for(int i=0;i<3;i++)Knots[i]=4;
            Array.Clear(Owners,0,LightCount);Array.Clear(Score,0,2);Array.Clear(Resolved,0,LightCount);Array.Clear(Spent,0,LightCount);Array.Clear(Changed,0,LightCount);
            int n=((seed%4)+4)%4,at=0;
            for(int k=0;k<3;k++)foreach(int slot in new[]{0,2,6,8})Lights[at++]=KnotPoint(k,RotateSlot(slot,n));
            int[][] routes={new[]{0,8,2},new[]{6,2,8},new[]{8,0,6}};
            foreach(var route in routes){for(int k=0;k<3;k++)scratch[k]=RotateSlot(route[k],n);Lights[at++]=Curve(scratch,.375f);Lights[at++]=Curve(scratch,.625f);}
        }
        static int RotateSlot(int slot,int n){int y=slot/3-1,z=slot%3-1;for(int i=0;i<n;i++){int old=y;y=-z;z=old;}return(y+1)*3+z+1;}
        public static Vector3 KnotPoint(int knot,int slot)=>new Vector3((knot-1)*.27f,(slot/3-1)*.23f,(slot%3-1)*.20f);
        static Vector3 Control(int[] knots,int i)=>i<=0?new Vector3(-.55f,0,0):i>=4?new Vector3(.55f,0,0):KnotPoint(i-1,knots[i-1]);
        public static Vector3 Curve(int[] knots,float t)
        {
            t=Mathf.Clamp01(t)*4;int segment=Mathf.Min(3,(int)t);float u=t-segment;
            var a=Control(knots,Mathf.Max(0,segment-1));var b=Control(knots,segment);var c=Control(knots,segment+1);var d=Control(knots,Mathf.Min(4,segment+2));
            return .5f*(2*b+(-a+c)*u+(2*a-5*b+4*c-d)*u*u+(-a+3*b-3*c+d)*u*u*u);
        }
        public bool Legal(int knot,int slot)=>Phase==BorrowPhase.Planning&&knot>=0&&knot<3&&slot>=0&&slot<9&&knot!=Locked&&slot!=Knots[knot];
        public void Intersections(int[] knots,float[] hits)
        {
            for(int j=0;j<=Samples;j++)path[j]=Curve(knots,j/(float)Samples);
            for(int i=0;i<LightCount;i++){
                float best=float.MaxValue,at=-1;
                for(int j=0;j<Samples;j++){var a=path[j];var d=path[j+1]-a;float u=Mathf.Clamp01(Vector3.Dot(Lights[i]-a,d)/Mathf.Max(1e-12f,d.sqrMagnitude));float distance=(Lights[i]-a-u*d).sqrMagnitude;if(distance<best){best=distance;at=(j+u)/Samples;}}
                hits[i]=best<=CatchRadius*CatchRadius?at:-1;
            }
        }
        public int Preview(int knot,int slot,int[] knots,float[] hits,out int steals)
        {
            Array.Copy(Knots,knots,3);if(knot>=0&&knot<3&&slot>=0&&slot<9)knots[knot]=slot;
            Intersections(knots,hits);int gain=0;steals=0;
            for(int i=0;i<LightCount;i++)if(hits[i]>=0&&!Spent[i]&&Owners[i]!=Player+1){gain+=Owners[i]==0?1:2;if(Owners[i]>0)steals++;}
            return gain;
        }
        public bool Launch(int player,int knot,int slot,int expectedTurn)
        {
            if(player!=Player||expectedTurn!=Turn||!Legal(knot,slot))return false;
            Preview(knot,slot,scratch,Hits,out _);Array.Copy(scratch,Knots,3);Array.Clear(Resolved,0,LightCount);Array.Clear(Changed,0,LightCount);
            Locked=knot;Phase=BorrowPhase.Pulse;Pulse=0;LastGain=LastSteals=0;return true;
        }
        public void Step(float dt)
        {
            if(dt<=0||float.IsNaN(dt)||float.IsInfinity(dt))return;
            if(Phase==BorrowPhase.Refill){refill+=Mathf.Min(dt,.1f);if(refill>=1.8f){Heist=1;Player=1-starter;for(int k=0;k<3;k++)Knots[k]=4;Locked=-1;Array.Clear(Owners,0,LightCount);Array.Clear(Spent,0,LightCount);Phase=BorrowPhase.Planning;LastGain=0;}return;}
            if(Phase!=BorrowPhase.Pulse)return;
            Pulse=Mathf.Min(1,Pulse+Mathf.Min(dt,.1f)/2.1f);
            for(int i=0;i<LightCount;i++)if(Hits[i]>=0&&(Player==0?Hits[i]:1-Hits[i])<=Pulse&&!Resolved[i]){
                Resolved[i]=true;if(Spent[i]||Owners[i]==Player+1)continue;bool steal=Owners[i]>0;int gain=steal?2:1;
                Score[Player]+=gain;LastGain+=gain;if(steal){LastSteals++;Spent[i]=true;}Owners[i]=Player+1;Changed[i]=true;ClaimVersion++;
            }
            if(Pulse>=1){Turn++;if(Turn>=Limit){Phase=BorrowPhase.Result;Winner=Score[0]==Score[1]?-1:Score[0]>Score[1]?0:1;}else if(Limit==TurnLimit&&Turn==6){Phase=BorrowPhase.Refill;refill=0;}else{Phase=BorrowPhase.Planning;Player=1-Player;}}
        }
        public void BestMove(out int knot,out int slot)
        {
            knot=slot=-1;float value=float.MinValue;var temp=new int[3];var hits=new float[LightCount];
            for(int k=0;k<3;k++)for(int s=0;s<9;s++)if(Legal(k,s)){
                float v=Preview(k,s,temp,hits,out _);for(int i=0;i<LightCount;i++)if(hits[i]>=0&&Owners[i]==Player+1)v+=.035f;
                v+=(k==1?.002f:0)-s*.00001f;if(v>value){value=v;knot=k;slot=s;}
            }
        }
    }
}
