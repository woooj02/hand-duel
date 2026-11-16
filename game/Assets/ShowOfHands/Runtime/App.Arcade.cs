using System;
using UnityEngine;

namespace ShowOfHands
{
    public sealed partial class App
    {
        enum ArcadeMode { None,Aftershape,TicTacToe }
        enum ArcadePlayers { Lesson,Hands,PC,Solo }
        ArcadeMode arcadeMode;ArcadePlayers arcadePlayers;
        readonly TicTacToeGame ticGame=new TicTacToeGame();
        readonly FallingBlocksGame blockGame=new FallingBlocksGame();
        readonly PinchDetector[] arcadePinch={new PinchDetector(),new PinchDetector()};
        readonly ArcadeGestureInput[] arcadeGestures={new ArcadeGestureInput(),new ArcadeGestureInput()};
        readonly Vector3[] arcadeOrigins=new Vector3[2],arcadeCursor=new Vector3[2];
        readonly bool[] arcadeCalibrated=new bool[2];
        readonly float[] arcadeCalibratedAt=new float[2];
        readonly Target[] ticTargets=new Target[9];
        readonly bool[] controllerRotateHeld=new bool[2];
        float controllerMoveAt;bool blockSoftDrop;
        ArenaRelay arenaRelay;Label arcadeStatus,arcadeScore,arcadeDetail,arcadeHint;
        bool arcadeLobby,arcadePaused,arcadeResultShown,arcadeFilm,arcadeFocusWasRunning,arcadeUnavailable;
        float arcadeTurnAt,arcadeFilmAt,arcadeResultAt,arenaPublishAt;int arcadeSeenTurn=-1,arcadeSeenRows,arcadeSeenPieces,ticHover=-1;
        Matrix4x4 ArcadeMatrix=>stage.localToWorldMatrix*Matrix4x4.Translate(new Vector3(0,-.035f,1.16f));
        void InitArcade()
        {
            arenaRelay=gameObject.AddComponent<ArenaRelay>();arenaRelay.Command+=ArenaCommand;var endpoint=arcadeEndpoint;
            #if UNITY_EDITOR
            if(Environment.GetEnvironmentVariable("SOH_PUBLIC_ARENA_TEST")!="1")endpoint=Environment.GetEnvironmentVariable("SOH_ARENA_TEST_ORIGIN")??"http://127.0.0.1:8790";
            #endif
            arenaRelay.Init(endpoint);ticGame.Start();blockGame.Start();
        }
        void CloseArcade()
        {
            arcadeMode=ArcadeMode.None;arcadeLobby=false;arcadeFilm=false;pointer.HandMask=3;
            if(arcadeHint!=null){Destroy(arcadeHint.gameObject);arcadeHint=null;}
        }
        void ArcadeHeader(){heading.transform.localPosition=new Vector3(0,.43f,1.1f);note.transform.localPosition=new Vector3(0,.35f,1.1f);}
        void ArcadeNavHome()=>Button("Home",.48f,.43f,Home,.14f);
        void Choice(string text,float x,Action action)=>Card.Create(view,pointer,new Vector3(x,-.39f,1.1f),new Vector2(.28f,.14f),text,null,()=>{Cue("ui_select");action();});
        void ShapeMenu()
        {
            Show("AFTERSHAPE","FALLING BLOCK DUEL • STACK, ROTATE, CLEAR ROWS");ArcadeHeader();arcadeMode=ArcadeMode.Aftershape;arcadeLobby=true;arcadePaused=false;blockGame.Start(true);
            Ui.Label(view,"Slide to steer. Fist to rotate. Pinch drops instantly.\nLower your palm to fall faster. Fill a row to clear it.",.030f,Palette.Text,new Vector3(0,.23f,1.1f));
            Choice("Learn\n1 row",-.30f,()=>BlocksStart(ArcadePlayers.Lesson));Choice("Hands\nduel",0,()=>BlocksStart(ArcadePlayers.Hands));Choice("Play with\nPC",.30f,()=>BlocksStart(ArcadePlayers.PC));
            Ui.Label(view,"PC: "+arenaRelay.DisplayHost+"\nPAIR CODE: "+arenaRelay.Code,.021f,Palette.Accent,new Vector3(0,-.50f,1.1f));ArcadeNavHome();
        }
        void TicMenu()
        {
            Show("TIC-TAC-TOE","TWO PLAYERS. THREE IN A ROW.");ArcadeHeader();arcadeMode=ArcadeMode.TicTacToe;arcadeLobby=true;arcadePaused=false;ticGame.Start(true);
            Ui.Label(view,"TOUCH a square with your index finger.\nOr aim your HAND and pinch. Three in a row wins.",.031f,Palette.Text,new Vector3(0,.23f,1.1f));
            Choice("Learn\none move",-.30f,()=>TicStart(ArcadePlayers.Lesson));Choice("Hands\nduel",0,()=>TicStart(ArcadePlayers.Hands));Choice("Play with\nPC",.30f,()=>TicStart(ArcadePlayers.PC));
            Ui.Label(view,"PC: "+arenaRelay.DisplayHost+"\nPAIR CODE: "+arenaRelay.Code,.021f,Palette.Accent,new Vector3(0,-.50f,1.1f));ArcadeNavHome();
        }
        bool PartnerReady(ArcadePlayers players)
        {
            if(players!=ArcadePlayers.PC||arenaRelay.Connected&&arenaRelay.PartnerPresent)return true;
            note.Text="Open the companion on PC • join with "+arenaRelay.Code;Cue("ui_back");return false;
        }
        void ResetArcadeInput()
        {
            for(int h=0;h<2;h++){arcadeCalibrated[h]=false;arcadePinch[h].Reset();arcadeGestures[h].Reset();arcadeCursor[h]=Vector3.zero;controllerRotateHeld[h]=hands.Hand(h).RotatePressed;}
            arcadeSeenTurn=-1;arcadeTurnAt=clock+.12f;arcadeUnavailable=false;ticHover=-1;blockSoftDrop=false;
        }
        void BlocksStart(ArcadePlayers players)
        {
            if(!PartnerReady(players))return;arcadePlayers=players;blockGame.Start(players==ArcadePlayers.Lesson,players==ArcadePlayers.Solo,UnityEngine.Random.Range(1,100000));
            ResetArcadeInput();arcadeSeenRows=arcadeSeenPieces=0;BlocksBoard();
        }
        void TicStart(ArcadePlayers players)
        {if(!PartnerReady(players))return;arcadePlayers=players;ticGame.Start(players==ArcadePlayers.Lesson);ResetArcadeInput();TicBoard();}
        void CommonBoard(ArcadeMode mode,string title,string subtitle)
        {
            Show(title,subtitle);ArcadeHeader();arcadeMode=mode;arcadeLobby=false;arcadePaused=arcadeResultShown=false;
            arcadeStatus=Ui.Label(view,"",.030f,Palette.Accent,new Vector3(0,.295f,1.1f));
            arcadeScore=Ui.Label(view,"",.028f,Palette.Text,new Vector3(0,-.355f,1.1f));
            Button("Centre\nboard",-.48f,.43f,()=>{Recentre();ResetArcadeInput();Cue("ui_back");},.14f);ArcadeNavHome();
            Button("Pause",.31f,-.45f,ArcadePause,.27f);
        }
        void TicBoard()
        {
            CommonBoard(ArcadeMode.TicTacToe,"TIC-TAC-TOE",ticGame.Lesson?"LESSON • complete the top row with X":arcadePlayers==ArcadePlayers.PC?"HEADSET = X • PC = O":"PLAYER 1: LEFT = X • PLAYER 2: RIGHT = O");
            Ui.Label(view,"TOUCH a tile • or HAND aim + pinch",.025f,Palette.Accent,new Vector3(-.14f,-.45f,1.1f));
            for(int i=0;i<9;i++){
                int cell=i;var target=new GameObject("Touchable square "+i).transform;target.SetParent(view,false);
                target.localPosition=new Vector3(0,-.035f,1.16f)+TicTacToeGame.Centre(i)*.9f;
                ticTargets[i]=new Target{Transform=target,HalfSize=Vector2.one*.0783f,AllowDwell=false,OnSelect=()=>TicConfirm(false,cell)};
                pointer.Add(ticTargets[i]);
            }
        }
        void BlocksBoard()
        {
            CommonBoard(ArcadeMode.Aftershape,"AFTERSHAPE",blockGame.Lesson?"LESSON • block lined up for you • PINCH once to clear a row":arcadePlayers==ArcadePlayers.PC?"HEADSET vs PC • clear rows on your turn":"LEFT vs RIGHT • clear rows on your turn");
            Button("Rotate",-.31f,-.45f,()=>BlocksAction("rotate",false),.27f);Button("Drop",0,-.45f,()=>BlocksAction("drop",false),.27f);
            Ui.Label(view,"NEXT",.022f,Palette.Text,new Vector3(.30f,.20f,1.1f));
            arcadeDetail=Ui.Label(view,"FIST\nrotate\n\nPINCH\ndrop now\n\nLOWER\nfall faster",.023f,Palette.Accent,new Vector3(-.31f,.01f,1.1f));
        }
        int ArcadePlayer=>arcadeMode==ArcadeMode.Aftershape?blockGame.Player:ticGame.Player;
        bool ArcadeEnded=>arcadeMode==ArcadeMode.Aftershape?blockGame.Phase==BlockPhase.Result:ticGame.Ended;
        int ControlHand=>arcadePlayers==ArcadePlayers.Hands?ArcadePlayer:pointer.HoverHand>=0&&hands.Hand(pointer.HoverHand).Tracked?pointer.HoverHand:hands.Left.Tracked?0:1;
        bool PeerTurn=>arcadePlayers==ArcadePlayers.PC&&ArcadePlayer==1;
        bool Available=>!arcadePaused&&(arcadePlayers!=ArcadePlayers.PC||arenaRelay.Connected&&arenaRelay.PartnerPresent)&&(PeerTurn||hands.Hand(ControlHand).Tracked);
        string TurnName=>arcadePlayers==ArcadePlayers.Lesson?"YOUR TURN":PeerTurn?"PC PLAYER'S TURN":arcadePlayers==ArcadePlayers.PC?"HEADSET PLAYER'S TURN":"PLAYER "+(ArcadePlayer+1)+" • "+(ArcadePlayer==0?"LEFT":"RIGHT")+" HAND ONLY";
        void PrepareArcadeTargets()
        {
            bool board=arcadeMode!=ArcadeMode.None&&!arcadeLobby&&!arcadeResultShown;
            pointer.HandMask=board&&!arcadePaused&&arcadePlayers==ArcadePlayers.Hands?1<<ArcadePlayer:3;
            for(int i=0;i<9;i++)if(ticTargets[i]!=null)ticTargets[i].Enabled=board&&arcadeMode==ArcadeMode.TicTacToe&&!PeerTurn&&!arcadePaused&&clock>=arcadeTurnAt&&ticGame.Board[i]==0;
        }
        void UpdateArcade(float dt)
        {
            for(int h=0;h<2;h++)arcadePinch[h].Update(hands.Hand(h));
            if(arcadeFilm&&clock-arcadeFilmAt>.8f&&pointer.AnyPinchBegan){arcadeFilm=false;view.gameObject.SetActive(true);fader.Snap(1);pointer.Active=true;if(arcadeHint!=null){Destroy(arcadeHint.gameObject);arcadeHint=null;}}
            if(arcadeLobby)return;
            if(arcadeResultShown)return;
            pointer.HandMask=arcadePlayers==ArcadePlayers.Hands?1<<ArcadePlayer:3;
            int key=(arcadeMode==ArcadeMode.Aftershape?blockGame.Pieces:CountTicMoves())*2+ArcadePlayer;
            if(key!=arcadeSeenTurn){arcadeSeenTurn=key;arcadeTurnAt=clock+.12f;blockSoftDrop=false;for(int h=0;h<2;h++)arcadeGestures[h].Reset();}
            bool available=Available;
            if(!available)
            {
                arcadeUnavailable=true;for(int h=0;h<2;h++)arcadeGestures[h].Reset();
                arcadeStatus.Text=arcadePaused?"PAUSED • choose Resume":arcadePlayers==ArcadePlayers.PC&&!arenaRelay.PartnerPresent?"PAUSED • reconnect the PC player":"Show your "+(ControlHand==0?"LEFT":"RIGHT")+" hand • turn paused";return;
            }
            if(arcadeUnavailable){arcadeUnavailable=false;arcadeTurnAt=clock+.12f;arcadeCalibrated[ControlHand]=false;}
            if(!PeerTurn)
            {
                int h=ControlHand;var hand=hands.Hand(h);var palm=stage.InverseTransformPoint(hand.Joints[0]);
                if(!arcadeCalibrated[h]){arcadeOrigins[h]=palm;arcadeCalibrated[h]=true;arcadeCalibratedAt[h]=clock;arcadeTurnAt=clock+.12f;}
                // A menu poke starts with an extended arm. Follow its initial withdrawal
                // so the resting hand, rather than the button position, becomes neutral.
                if(arcadeMode==ArcadeMode.Aftershape&&clock-arcadeCalibratedAt[h]<1.5f&&(arcadeOrigins[h].z-palm.z)*stage.lossyScale.z>.015f)arcadeOrigins[h]=palm;
                var delta=palm-arcadeOrigins[h];arcadeCursor[h]=new Vector3(Mathf.Clamp(delta.x*2.7f,-.32f,.32f),Mathf.Clamp(delta.y*2.7f,-.32f,.32f),0);
                if(arcadeMode==ArcadeMode.TicTacToe){ticHover=-1;for(int i=0;i<9;i++)if(pointer.Hovered==ticTargets[i])ticHover=i;if(ticHover>=0)arcadeCursor[h]=TicTacToeGame.Centre(ticHover);}
                // A genuine SDK pinch wins over a curled-finger pose classification.
                bool fist=RpsPose.Read(hand)==RpsMove.Rock&&!(hand.PinchKnown&&arcadePinch[h].Pinching);
                var gesture=arcadeGestures[h].Step(arcadePinch[h].Pinching,fist,dt,clock>arcadeTurnAt&&pointer.Hovered==null);
                if(arcadeMode==ArcadeMode.Aftershape&&blockGame.Phase==BlockPhase.Falling)
                {
                    // +/- 10 cm of physical travel covers the board. The piece cannot pass through a stack.
                    int width=FallingBlocksGame.Width-FallingShape.Width(blockGame.Kind,blockGame.Rotation);
                    if(hand.IsController){
                        if(Mathf.Abs(hand.Stick.x)>.5f&&clock>controllerMoveAt){blockGame.Move(hand.Stick.x>0?1:-1);controllerMoveAt=clock+.16f;}
                        if(hand.RotatePressed&&!controllerRotateHeld[h]&&pointer.Hovered==null)BlocksAction("rotate",false);
                        controllerRotateHeld[h]=hand.RotatePressed;
                    }else if(!blockGame.Lesson){
                        float column=Mathf.InverseLerp(-.10f,.10f,delta.x*stage.lossyScale.x)*width;
                        if(Mathf.Abs(column-blockGame.X)>.65f)blockGame.MoveTo(Mathf.RoundToInt(column));
                        float lower=delta.y*stage.lossyScale.y;
                        blockSoftDrop=blockSoftDrop?lower<-.025f:lower<-.04f;
                    }
                    if(gesture==ArcadeGesture.Rotate&&!hand.IsController)BlocksAction("rotate",false);
                    else if(gesture==ArcadeGesture.Confirm)BlocksAction("drop",false);
                }
            }
            if(arcadeResultShown)return;
            if(arcadeMode==ArcadeMode.Aftershape)
            {
                blockGame.Step(dt,blockSoftDrop&&!PeerTurn);
                if(blockGame.Rows>arcadeSeenRows){arcadeSeenRows=blockGame.Rows;Cue("guess_right");}
                if(blockGame.Pieces>arcadeSeenPieces){arcadeSeenPieces=blockGame.Pieces;Cue("receive_pop");}
                arcadeStatus.Text=blockGame.Phase==BlockPhase.Clearing?"ROW COMPLETE! • "+blockGame.ClearRows.Count+" clearing":TurnName+" • "+(PeerTurn?"use the PC buttons":blockSoftDrop?"FAST FALL • pinch to lock":"slide • fist to turn • pinch to drop");
                arcadeScore.Text="P1 "+blockGame.Score[0]+" : "+blockGame.Score[1]+" P2     •     "+blockGame.Rows+" rows     •     "+blockGame.Pieces+" / 24 blocks";
                if(blockGame.Phase==BlockPhase.Result)ArcadeResult();
            }
            else
            {
                arcadeStatus.Text=TurnName+" • "+(ArcadePlayer==0?"X":"O")+(PeerTurn?" • click a square on PC":" • touch a tile");
                if(ticGame.Lesson)arcadeStatus.Text="TOUCH the empty TOP-RIGHT square • or HAND aim + pinch";
                arcadeScore.Text="X "+ticGame.Wins[0]+" wins    •    O "+ticGame.Wins[1]+" wins    •    "+ticGame.Draws+" draws";
                if(ticGame.Ended)ArcadeResult();
            }
        }
        int CountTicMoves(){int count=0;foreach(int value in ticGame.Board)if(value!=0)count++;return count;}
        bool TicConfirm(bool fromPC,int cell=-1)
        {
            if(arcadeMode!=ArcadeMode.TicTacToe||arcadeLobby||arcadeResultShown||!Available||clock<arcadeTurnAt||fromPC!=PeerTurn)return false;
            if(cell<0)cell=ticHover;
            if(!ticGame.Place(ticGame.Player,cell)){Cue("guess_wrong");return false;}
            Cue("ui_select");arcadeTurnAt=clock+.45f;PrepareArcadeTargets();if(ticGame.Ended)ArcadeResult();return true;
        }
        bool BlocksAction(string action,bool fromPC)
        {
            if(arcadeMode!=ArcadeMode.Aftershape||arcadeLobby||arcadeResultShown||!Available||clock<arcadeTurnAt||fromPC!=PeerTurn)return false;
            bool changed=action=="left"?blockGame.Move(-1):action=="right"?blockGame.Move(1):action=="rotate"?blockGame.Rotate():action=="drop"&&blockGame.Drop();
            if(changed){Cue("ui_select");if(action=="drop"){arcadeTurnAt=clock+.12f;blockSoftDrop=false;}}return changed;
        }
        void ArcadeResult()
        {
            if(arcadeResultShown)return;var mode=arcadeMode;
            string title=mode==ArcadeMode.TicTacToe?ticGame.Winner==0?"IT'S A DRAW":(ticGame.Winner==1?"X":"O")+" WINS!":blockGame.Lesson?"ROW CLEARED!":blockGame.Winner<0?"MATCH DRAW":"PLAYER "+(blockGame.Winner+1)+" WINS!";
            string detail=mode==ArcadeMode.TicTacToe?ticGame.Lesson?"Three in a row • you know how to play":"X "+ticGame.Wins[0]+" wins • O "+ticGame.Wins[1]+" wins • "+ticGame.Draws+" draws":blockGame.Verdict+" • P1 "+blockGame.Score[0]+" : "+blockGame.Score[1]+" P2";
            Show(title,detail);ArcadeHeader();arcadeMode=mode;arcadeLobby=false;arcadeResultShown=true;arcadeResultAt=clock;pointer.HandMask=3;ticHover=-1;
            Button(arcadePlayers==ArcadePlayers.Lesson?"Play duel":"Next round",-.30f,-.44f,()=>
            {
                if(arcadePlayers==ArcadePlayers.Lesson){if(mode==ArcadeMode.Aftershape)ShapeMenu();else TicMenu();}
                else if(mode==ArcadeMode.Aftershape)BlocksStart(arcadePlayers);
                else{ticGame.NextRound();ResetArcadeInput();TicBoard();}
            },.28f);
            Button("Film view",0,-.44f,ArcadeFilm,.28f);Button("Home",.30f,-.44f,Home,.28f);Cue("guess_right");
        }
        void ArcadePause()
        {
            if(arcadeResultShown||arcadeLobby)return;arcadePaused=!arcadePaused;for(int h=0;h<2;h++)arcadeGestures[h].Reset();arcadeTurnAt=clock+.5f;
            foreach(var card in view.GetComponentsInChildren<Card>())if(card.Text!=null&&(card.Text.Text=="Pause"||card.Text.Text=="Resume"))card.Text.Text=arcadePaused?"Resume":"Pause";
        }
        void ArcadeFilm(){arcadeFilm=true;arcadeFilmAt=clock;view.gameObject.SetActive(false);pointer.Active=false;arcadeHint=Ui.Label(stage,"RESULT VIEW • pinch to restore controls",.025f,Palette.Accent,new Vector3(0,.43f,1.1f));}
        void PauseArcade(){if(arcadeMode==ArcadeMode.None)return;if(!arcadePaused&&!arcadeLobby&&!arcadeResultShown){arcadeFocusWasRunning=true;ArcadePause();}for(int h=0;h<2;h++)arcadeCalibrated[h]=false;}
        void ResumeArcadeFocus(){if(arcadeFocusWasRunning&&arcadeMode!=ArcadeMode.None&&arcadePaused)ArcadePause();arcadeFocusWasRunning=false;}
        void DrawArcade()
        {
            if(arcadeMode==ArcadeMode.Aftershape)ArcadeArt.BlocksBoard(batch,ArcadeMatrix*(arcadeLobby?Matrix4x4.Scale(Vector3.one*.75f):Matrix4x4.identity),blockGame,clock-arcadeResultAt);
            else if(arcadeMode==ArcadeMode.TicTacToe)ArcadeArt.Tic(batch,ArcadeMatrix*Matrix4x4.Scale(Vector3.one*(arcadeLobby?.72f:.9f)),ticGame,arcadeCursor[ControlHand],arcadeLobby||PeerTurn?-1:ticHover,arcadeGestures[ControlHand].Progress,clock-arcadeResultAt);
        }
        void DrawArcadeHome()
        {
            var left=stage.localToWorldMatrix*Matrix4x4.TRS(new Vector3(-.22f,.02f,1.15f),Quaternion.Euler(0,0,5),Vector3.one*.50f);
            ArcadeArt.BlocksBoard(batch,left,blockGame,clock);
            var right=stage.localToWorldMatrix*Matrix4x4.TRS(new Vector3(.22f,.02f,1.15f),Quaternion.identity,Vector3.one*.48f);
            ArcadeArt.Tic(batch,right,ticGame,Vector3.zero,-1,0,clock);
        }
        void PublishArena()
        {
            if(arenaRelay==null)return;var p=arenaRelay.Packet;
            if(borrowActive){PublishBorrowlight(p);return;}
            p.knots=p.lightOwners=Array.Empty<int>();p.lightSpent=Array.Empty<bool>();p.lightPositions=Array.Empty<Vector3>();
            p.code=arenaRelay.Code;p.title=Screen;p.mode=arcadeMode==ArcadeMode.Aftershape?"aftershape":arcadeMode==ArcadeMode.TicTacToe?"tictactoe":"home";
            p.pc=arcadePlayers==ArcadePlayers.PC;p.lesson=arcadePlayers==ArcadePlayers.Lesson;p.paused=arcadePaused||arcadeUnavailable;p.ready=clock>=arcadeTurnAt;p.player=ArcadePlayer;p.cursor0=arcadeCursor[0];p.cursor1=arcadeCursor[1];p.verdict="";
            p.phase=arcadeLobby||arcadeMode==ArcadeMode.None?"lobby":ArcadeEnded?"result":arcadeMode==ArcadeMode.Aftershape?blockGame.Phase==BlockPhase.Clearing?"clearing":"playing":"playing";
            if(arcadeMode==ArcadeMode.Aftershape)
            {
                p.board=blockGame.Board;p.width=6;p.height=10;p.left=blockGame.Score[0];p.right=blockGame.Score[1];p.pieces=blockGame.Pieces;p.rows=blockGame.Rows;p.shape=blockGame.Kind;p.rotation=blockGame.Rotation;p.blockX=blockGame.X;p.blockY=blockGame.Y;p.ghostY=blockGame.GhostY;p.next=blockGame.NextKind;p.fall=blockGame.FallProgress;p.clearRows=blockGame.ClearRows.ToArray();p.winner=blockGame.Winner;
                p.hint=arcadeLobby?"Learn one row, play a hands duel, or pair a PC player":ArcadeEnded?blockGame.Verdict:TurnName;
                p.detail="Fill a horizontal row to clear it and earn its point. Slide your palm, fist to rotate, pinch for instant drop. Lower your palm to fall faster. PC player uses the buttons on their turn.";
            }
            else if(arcadeMode==ArcadeMode.TicTacToe)
            {
                p.board=ticGame.Board;p.width=p.height=3;p.left=ticGame.Wins[0];p.right=ticGame.Wins[1];p.draws=ticGame.Draws;p.round=ticGame.Round;p.winningLine=ticGame.WinningLine;p.winner=ticGame.Winner;
                p.hint=arcadeLobby?"Choose Learn, Hands duel, or Play with PC":ticGame.Ended?ticGame.Winner==0?"Draw • next round?":(ticGame.Winner==1?"X":"O")+" wins!":TurnName;
                p.detail="X and O take turns. Get three across, down or diagonally. Headset: touch a square, or aim a hand and pinch. Controllers: aim and trigger. PC duel: headset plays X, PC clicks O.";
            }
            else{p.board=Array.Empty<int>();p.hint="Choose Aftershape or Tic-tac-toe in the headset";p.detail="One headset. Real turns. A shared board.";}
        }
        void ArenaCommand(string command)
        {
            if(borrowActive){BorrowCommand(command);return;}
            if(command=="pause"&&!arcadePaused)ArcadePause();else if(command=="resume"&&arcadePaused)ArcadePause();
            else if(command=="center"&&arcadePaused)ResetArcadeInput();
            else if(arcadePlayers==ArcadePlayers.PC&&arcadeMode==ArcadeMode.TicTacToe&&command.StartsWith("place:")&&int.TryParse(command.Substring(6),out int cell))TicConfirm(true,cell);
            else if(arcadePlayers==ArcadePlayers.PC&&arcadeMode==ArcadeMode.Aftershape)BlocksAction(command,true);
        }
    }
}
