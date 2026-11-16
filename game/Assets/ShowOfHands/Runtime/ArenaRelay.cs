using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace ShowOfHands
{
    [Serializable] public sealed class ArenaPacket
    {
        public int v=1,revision=3;
        public string code,mode="home",phase="lobby",title="HAND DUEL",hint="Choose a game",detail="",verdict="";
        public bool paused,pc,lesson,ready;
        public int player,left,right,draws,round,width,height,pieces,rows,shape,rotation,blockX,blockY,ghostY,next,winner=-1;
        public float fall;
        public int[] board,clearRows,winningLine;
        public Vector3 cursor0,cursor1;
        public int[] knots,lightOwners;public bool[] lightSpent;public Vector3[] lightPositions;
        public int lockedKnot=-1,weaveTurn,weaveLimit=12,weaveMatch,weaveSeed,weaveHeist;public float pulse;
    }
    [Serializable] sealed class RelayAnswer {public bool ok,partner;public string command;public int seq;}

    // Paired companion transport. The Quest owns all rules and scores.
    public sealed class ArenaRelay:MonoBehaviour
    {
        public string Code {get;private set;}
        public bool Connected {get;private set;}
        public bool PartnerPresent {get;private set;}
        public string BaseUrl {get;private set;}="http://127.0.0.1:8790";
        public string DisplayHost=>new Uri(BaseUrl).Authority;
        public ArenaPacket Packet=new ArenaPacket();
        public event Action<string> Command;
        readonly string key=Guid.NewGuid().ToString("N");
        int acknowledged;
        public void Init(string endpoint=null)
        {
            if(!string.IsNullOrWhiteSpace(endpoint)&&Uri.TryCreate(endpoint.Trim(),UriKind.Absolute,out var uri))
            {
                bool allowed=uri.Scheme==Uri.UriSchemeHttps;
                #if SOH_DEVELOPMENT || UNITY_EDITOR
                allowed|=uri.Scheme==Uri.UriSchemeHttp&&uri.IsLoopback;
                #endif
                if(allowed)BaseUrl=endpoint.Trim().TrimEnd('/');
            }
            const string alphabet="23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
            var bytes=Guid.NewGuid().ToByteArray();var code=new StringBuilder(6);
            for(int i=0;i<6;i++)code.Append(alphabet[bytes[i]%alphabet.Length]);
            Code=code.ToString();Debug.Log("SOH_ARCADE_PAIR "+Code);StartCoroutine(Send());
        }
        IEnumerator Send()
        {
            while(true)
            {
                Packet.code=Code;
                var bytes=Encoding.UTF8.GetBytes(JsonUtility.ToJson(Packet));
                using(var request=new UnityWebRequest(BaseUrl+"/api/frame","POST"))
                {
                    request.uploadHandler=new UploadHandlerRaw(bytes);request.downloadHandler=new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type","application/json");request.SetRequestHeader("X-Arena-Key",key);
                    request.SetRequestHeader("X-Arena-Ack",acknowledged.ToString());request.timeout=BaseUrl.StartsWith("https://")?6:2;
                    yield return request.SendWebRequest();
                    Connected=request.result==UnityWebRequest.Result.Success;
                    if(Connected)
                    {
                        RelayAnswer answer=null;
                        try{answer=JsonUtility.FromJson<RelayAnswer>(request.downloadHandler.text);}catch{}
                        Connected=answer!=null&&answer.ok;PartnerPresent=Connected&&answer.partner;
                        if(Connected&&answer.seq>acknowledged){acknowledged=answer.seq;Command?.Invoke(answer.command);}
                    }
                    else PartnerPresent=false;
                }
                yield return new WaitForSecondsRealtime(Connected?.12f:1f);
            }
        }
    }
}
