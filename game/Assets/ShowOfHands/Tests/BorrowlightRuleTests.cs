using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
namespace ShowOfHands.Tests
{
    public class BorrowlightRuleTests
    {
        static void FinishPulse(BorrowlightGame g){for(int i=0;i<24&&g.Phase==BorrowPhase.Pulse;i++)g.Step(.1f);}
        [Test] public void ClaimStealAndBurnoutCannotBeFarmed()
        {
            var g=new BorrowlightGame();g.Start();for(int i=0;i<18;i++)g.Lights[i]=Vector3.one*4;g.Lights[0]=BorrowlightGame.KnotPoint(1,8);
            Assert.IsTrue(g.Launch(0,1,8,0));FinishPulse(g);Assert.AreEqual(1,g.Score[0]);Assert.AreEqual(1,g.Owners[0]);
            Assert.IsTrue(g.Launch(1,0,0,1));FinishPulse(g);Assert.AreEqual(2,g.Score[1]);Assert.IsTrue(g.Spent[0]);Assert.AreEqual(2,g.Owners[0]);
            Assert.IsTrue(g.Launch(0,2,0,2));FinishPulse(g);Assert.AreEqual(1,g.Score[0]);Assert.AreEqual(2,g.Score[1]);Assert.AreEqual(2,g.Owners[0]);
        }
        [Test] public void LastMovedKnotCoolsForExactlyTheOtherTurn()
        {
            var g=new BorrowlightGame();g.Start();Assert.IsTrue(g.Launch(0,1,8,0));FinishPulse(g);
            Assert.IsFalse(g.Legal(1,0));Assert.IsFalse(g.Launch(1,1,0,1));Assert.IsTrue(g.Launch(1,0,0,1));FinishPulse(g);
            Assert.IsTrue(g.Legal(1,0));Assert.IsFalse(g.Legal(0,8));
        }
        [Test] public void WrongPlayerDuplicateTurnAndNoChangeAreRejected()
        {
            var g=new BorrowlightGame();g.Start();Assert.IsFalse(g.Launch(1,1,8,0));Assert.IsFalse(g.Launch(0,1,4,0));Assert.IsFalse(g.Launch(0,1,8,1));
            Assert.IsTrue(g.Launch(0,1,8,0));Assert.IsFalse(g.Launch(0,0,0,0));FinishPulse(g);Assert.IsFalse(g.Launch(1,0,0,0));Assert.AreEqual(1,g.Turn);
        }
        [TestCase(-1,0)][TestCase(3,0)][TestCase(0,-1)][TestCase(0,9)][TestCase(int.MaxValue,int.MinValue)]
        public void IllegalCoordinatesDoNotMutateTheGame(int knot,int slot)
        {var g=new BorrowlightGame();g.Start();Assert.IsFalse(g.Launch(0,knot,slot,0));CollectionAssert.AreEqual(new[]{4,4,4},g.Knots);Assert.AreEqual(0,g.Turn);}
        [Test] public void PreviewIsPureAndMatchesTheActuallyBankedGain()
        {
            var g=new BorrowlightGame();g.Start();var route=new int[3];var hits=new float[18];int gain=g.Preview(1,8,route,hits,out int steals);
            Assert.Greater(gain,0);Assert.AreEqual(0,steals);CollectionAssert.AreEqual(new[]{4,4,4},g.Knots);Assert.AreEqual(0,g.Score[0]);
            g.Launch(0,1,8,0);FinishPulse(g);Assert.AreEqual(gain,g.Score[0]);
        }
        [Test] public void SecondHeistSwapsStarterKeepsBankAndResetsTheField()
        {
            var g=new BorrowlightGame();g.Start();for(int turn=0;turn<6;turn++){g.BestMove(out int k,out int s);Assert.IsTrue(g.Launch(g.Player,k,s,g.Turn));FinishPulse(g);}
            Assert.AreEqual(BorrowPhase.Refill,g.Phase);int left=g.Score[0],right=g.Score[1];for(int i=0;i<20;i++)g.Step(.1f);
            Assert.AreEqual(1,g.Player);Assert.AreEqual(1,g.Heist);Assert.AreEqual(6,g.Turn);CollectionAssert.AreEqual(new[]{4,4,4},g.Knots);Assert.AreEqual(-1,g.Locked);
            Assert.AreEqual(left,g.Score[0]);Assert.AreEqual(right,g.Score[1]);foreach(int owner in g.Owners)Assert.AreEqual(0,owner);
        }
        [Test] public void FullMatchEndsAndFurtherStepsOrMovesCannotAwardPoints()
        {
            var g=new BorrowlightGame();g.Start();int guard=0;
            while(g.Phase!=BorrowPhase.Result&&guard++<1000){if(g.Phase==BorrowPhase.Planning){g.BestMove(out int k,out int s);Assert.IsTrue(g.Launch(g.Player,k,s,g.Turn));}g.Step(.1f);}
            Assert.Less(guard,1000);Assert.AreEqual(12,g.Turn);Assert.LessOrEqual(g.Score[0]+g.Score[1],108);int total=g.Score[0]+g.Score[1];
            for(int i=0;i<100;i++)g.Step(.1f);Assert.IsFalse(g.Launch(g.Player,0,0,g.Turn));Assert.AreEqual(total,g.Score[0]+g.Score[1]);
        }
        [Test] public void InvalidTimeCannotFastForwardOrCorruptPulse()
        {var g=new BorrowlightGame();g.Start();g.Launch(0,1,8,0);foreach(float dt in new[]{float.NaN,float.PositiveInfinity,-1f,0f})g.Step(dt);Assert.AreEqual(0,g.Pulse);g.Step(1000);Assert.Less(g.Pulse,.05f);}
        [Test] public void StarterOrderIsSymmetricAcrossTheTwoHeists()
        {
            var scores=new int[2][];
            for(int starter=0;starter<2;starter++){var g=new BorrowlightGame();g.Start(0,starter);for(int i=0;i<1000&&g.Phase!=BorrowPhase.Result;i++){if(g.Phase==BorrowPhase.Planning){g.BestMove(out int k,out int s);g.Launch(g.Player,k,s,g.Turn);}g.Step(.1f);}scores[starter]=(int[])g.Score.Clone();}
            Assert.AreEqual(scores[0][0],scores[1][1]);Assert.AreEqual(scores[0][1],scores[1][0]);
        }
        [Serializable] class Fixture {public Entry[] entries;}
        [Serializable] class Entry {public int seed,knot,slot,gain,steals;public int[] route;}
        [Serializable] class RouteFixture {public RouteEntry[] entries;}
        [Serializable] class RouteEntry {public int seed,index,mask;}
        [Test] public void EveryPossibleRouteInAllFourFieldsMatchesBrowserCollisionResults()
        {
            var fixture=JsonUtility.FromJson<RouteFixture>(File.ReadAllText(Path.Combine(Application.dataPath,"ShowOfHands/Tests/BorrowlightAllRoutes.json")));
            var g=new BorrowlightGame();int lastSeed=-1;var knots=new int[3];var hits=new float[18];
            foreach(var entry in fixture.entries){if(entry.seed!=lastSeed){g.Start(entry.seed);lastSeed=entry.seed;}knots[0]=entry.index/81;knots[1]=entry.index/9%9;knots[2]=entry.index%9;g.Intersections(knots,hits);int mask=0;for(int i=0;i<hits.Length;i++)if(hits[i]>=0)mask|=1<<i;Assert.AreEqual(entry.mask,mask,$"field{entry.seed}/route{entry.index}: browser prediction must match Quest capture");}
            Assert.AreEqual(2916,fixture.entries.Length);
        }
        [Test] public void AllOpeningPreviewsAgreeWithIndependentBrowserRules()
        {
            var fixture=JsonUtility.FromJson<Fixture>(File.ReadAllText(Path.Combine(Application.dataPath,"ShowOfHands/Tests/BorrowlightParity.json")));
            var g=new BorrowlightGame();var route=new int[3];var hits=new float[18];
            foreach(var e in fixture.entries){g.Start(e.seed);int gain=g.Preview(e.knot,e.slot,route,hits,out int steals);Assert.AreEqual(e.gain,gain,$"seed{e.seed}/knot{e.knot}/slot{e.slot}");Assert.AreEqual(e.steals,steals);CollectionAssert.AreEqual(e.route,route);}
            Assert.AreEqual(96,fixture.entries.Length);
        }
    }
}
