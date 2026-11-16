#if BORROWLIGHT_BENCHMARK && !UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ShowOfHands
{
    public sealed partial class App
    {
        sealed class BenchmarkHands:IHandSource
        {
            public Transform Head;readonly Vector3[] raw=new Vector3[26];
            public void Read(HandFrame frame){frame.HeadPosition=Head.position;frame.HeadRotation=Head.rotation;for(int h=0;h<2;h++){
                var hand=frame.Hand(h);hand.Tracked=true;hand.PinchKnown=true;hand.SelectPressed=false;hand.IsController=false;hand.AimValid=false;hand.SelectSuppressed=false;
                DuelArt.FillPose(raw,RpsMove.Paper,h);var origin=raw[0];var centre=new Vector3(h==0?-.14f:.14f,-.16f+Mathf.Sin(Time.time*1.7f+h)*.012f,.31f);
                for(int j=0;j<26;j++)hand.Joints[j]=Head.TransformPoint(centre+raw[j]-origin);
            }}
        }
        [Serializable] sealed class BorrowBenchReport
        {
            public string version,device,gpu,source="Actual Quest rendering with simulated hands and automated legal moves; not a physical-user interaction test",utc;
            public int frames,maxInstances,focusLossFrames,framesAbove16_67ms;
            public float seconds,averageFps,p50FrameMs,p95FrameMs,p99FrameMs,maxFrameMs;
            public bool xrDeviceActive;
        }
        IEnumerator RunBorrowBenchmark()
        {
            yield return new WaitForSecondsRealtime(2);
            source=new BenchmarkHands{Head=head};arenaRelay.StopAllCoroutines();borrowSeed=0;borrowStarter=0;BorrowStart(ArcadePlayers.Solo);
            var frames=new List<float>(2160);int warm=120,maxInstances=0,focusLoss=0;float nextMove=Time.realtimeSinceStartup+1;
            Debug.Log("BORROW_BENCHMARK_BEGIN synthetic hands, real standalone renderer");
            while(frames.Count<2160){
                // Keep the entire actual game in view on the unattended headset.
                // Production builds do not contain this camera-relative benchmark code.
                stage.SetPositionAndRotation(head.position-head.up*.025f,head.rotation);
                if(borrowGame.Phase==BorrowPhase.Planning&&Time.realtimeSinceStartup>=nextMove){borrowGame.BestMove(out int k,out int s);borrowGame.Launch(borrowGame.Player,k,s,borrowGame.Turn);nextMove=Time.realtimeSinceStartup+2.5f;}
                if(borrowFinished){borrowSeed++;BorrowStart(ArcadePlayers.Solo);}
                if(!focused){focusLoss++;yield return null;continue;}
                if(warm>0)warm--;else{frames.Add(Time.unscaledDeltaTime*1000);maxInstances=Mathf.Max(maxInstances,batch.Instances);}
                yield return null;
            }
            frames.Sort();float sum=0;int slow=0;foreach(float ms in frames){sum+=ms;if(ms>1000f/60)slow++;}
            var report=new BorrowBenchReport{version=Application.version,device=SystemInfo.deviceModel,gpu=SystemInfo.graphicsDeviceName,utc=DateTime.UtcNow.ToString("o"),frames=frames.Count,maxInstances=maxInstances,focusLossFrames=focusLoss,framesAbove16_67ms=slow,seconds=sum/1000,averageFps=frames.Count/(sum/1000),p50FrameMs=frames[frames.Count/2],p95FrameMs=frames[(int)(frames.Count*.95f)],p99FrameMs=frames[(int)(frames.Count*.99f)],maxFrameMs=frames[frames.Count-1],xrDeviceActive=UnityEngine.XR.XRSettings.isDeviceActive};
            string json=JsonUtility.ToJson(report,true);File.WriteAllText(Path.Combine(Application.persistentDataPath,"borrowlight-benchmark.json"),json);Debug.Log("BORROW_BENCHMARK_DONE "+JsonUtility.ToJson(report));
        }
    }
}
#endif
