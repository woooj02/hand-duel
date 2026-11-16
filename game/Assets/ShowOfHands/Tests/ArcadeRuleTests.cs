using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ShowOfHands.Tests
{
    public class ArcadeRuleTests
    {
        static IEnumerable<int[]> Wins(){yield return new[]{0,1,2};yield return new[]{3,4,5};yield return new[]{6,7,8};yield return new[]{0,3,6};yield return new[]{1,4,7};yield return new[]{2,5,8};yield return new[]{0,4,8};yield return new[]{2,4,6};}
        [TestCaseSource(nameof(Wins))] public void EveryWinningLineEndsOnce(int[] line)
        {
            var g=new TicTacToeGame();g.Start();int spare=-1;for(int i=0;i<9;i++)if(System.Array.IndexOf(line,i)<0){spare=i;break;}
            Assert.IsTrue(g.Place(0,line[0]));Assert.IsTrue(g.Place(1,spare));Assert.IsTrue(g.Place(0,line[1]));
            int other=(spare+1)%9;while(g.Board[other]!=0||System.Array.IndexOf(line,other)>=0)other=(other+1)%9;
            Assert.IsTrue(g.Place(1,other));Assert.IsTrue(g.Place(0,line[2]));Assert.AreEqual(1,g.Winner);Assert.AreEqual(1,g.Wins[0]);
            Assert.IsFalse(g.Place(0,line[2]));Assert.AreEqual(1,g.Wins[0]);CollectionAssert.AreEquivalent(line,g.WinningLine);
        }
        [Test] public void DrawDoesNotAwardAWin()
        {
            var g=new TicTacToeGame();g.Start();foreach(int cell in new[]{0,1,2,4,3,5,7,6,8})Assert.IsTrue(g.Place(g.Player,cell));
            Assert.AreEqual(0,g.Winner);Assert.AreEqual(1,g.Draws);Assert.AreEqual(0,g.Wins[0]+g.Wins[1]);g.NextRound();Assert.IsFalse(g.Ended);Assert.AreEqual(1,g.Player);
        }
        [Test] public void WrongPlayerOccupiedAndOutOfRangeMovesAreRejected()
        {
            var g=new TicTacToeGame();g.Start();Assert.IsFalse(g.Place(1,0));Assert.IsFalse(g.Place(0,-1));Assert.IsFalse(g.Place(0,9));g.Place(0,4);Assert.IsFalse(g.Place(1,4));Assert.AreEqual(1,g.Player);
        }
        [Test] public void LessonIsClearlySeparateFromCompetitiveScores()
        {
            var g=new TicTacToeGame();g.Start(true);Assert.IsTrue(g.Place(0,2));Assert.AreEqual(1,g.Winner);Assert.AreEqual(0,g.Wins[0]);g.Start();Assert.AreEqual(0,g.Board[0]);
        }
        [Test] public void CellCursorMatchesAllNineCentresAndRejectsFarAwayHands()
        {for(int i=0;i<9;i++)Assert.AreEqual(i,TicTacToeGame.CellAt(TicTacToeGame.Centre(i)));Assert.AreEqual(-1,TicTacToeGame.CellAt(Vector3.one));}
        [Test] public void AllSevenPiecesHaveFourUniqueCellsInEveryRotation()
        {
            for(int kind=0;kind<7;kind++)for(int r=0;r<4;r++){var cells=FallingShape.Cells(kind,r);Assert.AreEqual(4,new HashSet<Vector2Int>(cells).Count);foreach(var p in cells){Assert.GreaterOrEqual(p.x,0);Assert.GreaterOrEqual(p.y,0);Assert.Less(p.x,4);Assert.Less(p.y,4);}}
        }
        static void Settle(FallingBlocksGame g){for(int i=0;i<20&&g.Phase==BlockPhase.Clearing;i++)g.Step(.1f);}
        [Test] public void LessonDropActuallyFillsAndClearsTheGap()
        {
            var g=new FallingBlocksGame();g.Start(true);Assert.AreEqual(1,g.Kind);Assert.AreEqual(2,g.X);Assert.AreEqual(0,g.GhostY);Assert.IsTrue(g.Drop());Assert.AreEqual(BlockPhase.Clearing,g.Phase);
            Settle(g);Assert.AreEqual(BlockPhase.Result,g.Phase);Assert.AreEqual(1,g.Rows);Assert.AreEqual(1,g.Score[0]);Assert.AreEqual(1,g.Pieces);
            int occupied=0;foreach(int cell in g.Board)if(cell>0)occupied++;Assert.AreEqual(2,occupied,"4 seeded + 4 placed - 6 cleared");
            g.Step(.1f);Assert.AreEqual(1,g.Score[0]);Assert.IsFalse(g.Drop());
        }
        [Test] public void ClearingTwoRowsCollapsesTheCellsAboveWithoutDuplication()
        {
            var g=new FallingBlocksGame();g.Start(true);for(int y=0;y<2;y++)for(int x=0;x<6;x++)g.Board[y*6+x]=(x==2||x==3)?0:8;
            g.Board[2*6]=7;g.Drop();Settle(g);Assert.AreEqual(2,g.Rows);Assert.AreEqual(7,g.Board[0]);Assert.AreEqual(2,g.Score[0]);
        }
        [Test] public void PlayersAlternateOnlyAfterThePlacedPieceAndItsClearResolve()
        {
            var g=new FallingBlocksGame();g.Start();int player=g.Player;g.Drop();Assert.AreEqual(1-player,g.Player);Assert.AreEqual(1,g.Pieces);Assert.AreEqual(0,g.Score[0]+g.Score[1]);
        }
        [Test] public void StackAndWallsBlockMovementWithoutTeleporting()
        {
            var g=new FallingBlocksGame();g.Start(true);g.MoveTo(0);g.Board[g.Y*6+2]=8;
            Assert.IsFalse(g.MoveTo(4));Assert.AreEqual(0,g.X);Assert.IsFalse(g.Fits(g.Kind,0,-1,0));Assert.IsFalse(g.Fits(g.Kind,0,5,0));
        }
        [Test] public void RotationCannotOverlapAnotherBlock()
        {
            var g=new FallingBlocksGame();g.Start();for(int y=0;y<10;y++)for(int x=0;x<6;x++)g.Board[y*6+x]=8;
            Assert.IsFalse(g.Rotate());Assert.IsFalse(g.Fits(g.Kind,0,g.X,g.Y));
        }
        [Test] public void LongPieceCanRotateImmediatelyAtTheTopOfTheWell()
        {
            var g=new FallingBlocksGame();for(int seed=1;;seed++){g.Start(seed:seed);if(g.Kind==0)break;}
            Assert.IsTrue(g.Rotate());Assert.AreEqual(1,g.Rotation);Assert.IsTrue(g.Fits(g.Kind,g.Rotation,g.X,g.Y));Assert.LessOrEqual(g.Y+FallingShape.Height(g.Kind,g.Rotation),10);
        }
        [Test] public void GhostExactlyPredictsWhereHardDropLocksThePiece()
        {
            var g=new FallingBlocksGame();g.Start();int kind=g.Kind,rotation=g.Rotation,x=g.X,y=g.GhostY;var cells=(Vector2Int[])FallingShape.Cells(kind,rotation).Clone();
            g.Drop();foreach(var p in cells)Assert.AreEqual(kind+1,g.Board[(y+p.y)*6+x+p.x]);
        }
        [Test] public void SameSeedProducesSamePieceSequence()
        {
            var a=new FallingBlocksGame();var b=new FallingBlocksGame();a.Start(seed:22);b.Start(seed:22);
            for(int i=0;i<7;i++){Assert.AreEqual(a.Kind,b.Kind);a.MoveTo(i%5);b.MoveTo(i%5);a.Drop();b.Drop();Settle(a);Settle(b);CollectionAssert.AreEqual(a.Board,b.Board);if(a.Phase==BlockPhase.Result)break;}
        }
        [Test] public void TrackingPauseByNotSteppingLeavesGravityAndBoardUnchanged()
        {
            var g=new FallingBlocksGame();g.Start();int y=g.Y;float before=g.FallProgress;Assert.AreEqual(y,g.Y);Assert.AreEqual(before,g.FallProgress);g.Step(float.NaN);g.Step(float.PositiveInfinity);Assert.AreEqual(y,g.Y);
        }
        [Test] public void FistRotatesOnceAndRequiresOpenHandBeforeDrop()
        {
            var input=new ArcadeGestureInput();input.Step(false,false,.1f);
            Assert.AreEqual(ArcadeGesture.Rotate,input.Step(true,true,.1f));Assert.AreEqual(ArcadeGesture.None,input.Step(true,true,.1f));
            for(int i=0;i<6;i++)Assert.AreEqual(ArcadeGesture.None,input.Step(true,false,.1f));
            input.Step(false,false,.1f);Assert.AreEqual(ArcadeGesture.Confirm,input.Step(true,false,1f/72));
            for(int i=0;i<5;i++)Assert.AreEqual(ArcadeGesture.None,input.Step(true,false,.1f));
        }
        [Test] public void HeldPinchAcrossTurnsOrPauseCannotPlaceAgain()
        {
            var input=new ArcadeGestureInput();for(int i=0;i<10;i++)Assert.AreEqual(ArcadeGesture.None,input.Step(true,false,.1f));
            input.Step(false,false,.1f);input.Step(true,false,.1f);input.Step(true,false,.1f,false);
            for(int i=0;i<6;i++)Assert.AreEqual(ArcadeGesture.None,input.Step(true,false,.1f));
        }
        [Test] public void LoweringPalmCanAccelerateFallWithoutHardDropping()
        {
            var normal=new FallingBlocksGame();normal.Start(true);var fast=new FallingBlocksGame();fast.Start(true);
            int start=fast.Y;normal.Step(.1f);fast.Step(.1f,true);
            Assert.AreEqual(start,normal.Y);Assert.Less(fast.Y,start);Assert.AreEqual(0,fast.Pieces);
            int y=fast.Y;fast.Step(.01f,false);Assert.AreEqual(y,fast.Y,"returning to normal speed must not teleport");
        }
        [Test] public void FreshPinchDropsOnFirstFrameAndDoesNotRepeat()
        {
            var input=new ArcadeGestureInput();for(int i=0;i<4;i++)input.Step(false,false,1f/72);
            Assert.AreEqual(ArcadeGesture.Confirm,input.Step(true,false,1f/72));
            for(int i=0;i<72;i++)Assert.AreEqual(ArcadeGesture.None,input.Step(true,false,1f/72));
        }
    }
}
