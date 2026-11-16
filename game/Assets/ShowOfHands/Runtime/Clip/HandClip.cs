using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ShowOfHands
{
    // The hand clip format, version 1. docs/contract.md section 1 is the specification and
    // contract/tools/handclip.py is the reference implementation.
    public static class ClipFormat
    {
        public const int Version = 1;
        public const int Fps = 30;
        public const int Joints = 26;
        public const int MinFrames = 30;
        public const int MaxFrames = 300;
        public const int FrameBytes = 2 + 6 + 8 + Joints * 6 * 2;

        public const int LeftTracked = 1, RightTracked = 2, LeftMitt = 4, RightMitt = 8;

        // OpenXR joint order.
        public const int Palm = 0, Wrist = 1;
        public const int ThumbMetacarpal = 2, ThumbProximal = 3, ThumbDistal = 4, ThumbTip = 5;
        public const int IndexMetacarpal = 6, IndexProximal = 7, IndexIntermediate = 8, IndexDistal = 9, IndexTip = 10;
        public const int MiddleMetacarpal = 11, MiddleProximal = 12, MiddleIntermediate = 13, MiddleDistal = 14, MiddleTip = 15;
        public const int RingMetacarpal = 16, RingProximal = 17, RingIntermediate = 18, RingDistal = 19, RingTip = 20;
        public const int LittleMetacarpal = 21, LittleProximal = 22, LittleIntermediate = 23, LittleDistal = 24, LittleTip = 25;

        public const int HeadOffset = 2, LeftOffset = 16, RightOffset = 16 + Joints * 6;
    }

    // The JSON shape of a clip, both as uploaded and as the server returns it.
    [Serializable]
    public sealed class ClipJson
    {
        public int v;
        public string id;
        public string word;
        public int fps;
        public int frames;
        public string glove;
        public string app;
        public string device;
        public string data;
        public string created;
        public string[] options;
        public bool daily;
        public string by;
        public ClipStats stats;
    }

    [Serializable]
    public sealed class ClipStats
    {
        public int seen;
        public int correct;
    }

    [Serializable]
    public sealed class SeedPackJson
    {
        public int v;
        public ClipJson[] clips;
    }

    // A decoded clip: metadata plus the raw frame bytes. Joints are read straight from the bytes, so a clip
    // costs 328 bytes a frame and nothing else.
    public sealed class HandClip
    {
        public string Id, Word, Glove = "ember", App, Device, Created, By;
        public string[] Options;
        public bool Daily;
        public int Seen, Correct;
        public int Frames;
        public byte[] Data;

        public float Seconds => Frames / (float)ClipFormat.Fps;

        public int Flags(int frame)
        {
            int o = frame * ClipFormat.FrameBytes;
            return Data[o] | (Data[o + 1] << 8);
        }

        public Vector3 HeadPosition(int frame) => ReadPosition(frame * ClipFormat.FrameBytes + ClipFormat.HeadOffset);

        public Quaternion HeadRotation(int frame)
        {
            int o = frame * ClipFormat.FrameBytes + ClipFormat.HeadOffset + 6;
            const float k = 1f / 32767f;
            var q = new Quaternion(ReadShort(o) * k, ReadShort(o + 2) * k, ReadShort(o + 4) * k, ReadShort(o + 6) * k);
            float m = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
            if (m < 1e-6f) return Quaternion.identity;
            float inv = 1f / Mathf.Sqrt(m);
            return new Quaternion(q.x * inv, q.y * inv, q.z * inv, q.w * inv);
        }

        // hand: 0 left, 1 right. Stage space, metres.
        public Vector3 Joint(int frame, int hand, int joint)
        {
            int o = frame * ClipFormat.FrameBytes + (hand == 0 ? ClipFormat.LeftOffset : ClipFormat.RightOffset) + joint * 6;
            return ReadPosition(o);
        }

        private Vector3 ReadPosition(int o) => new Vector3(ReadShort(o) * 0.001f, ReadShort(o + 2) * 0.001f, ReadShort(o + 4) * 0.001f);

        private short ReadShort(int o) => (short)(Data[o] | (Data[o + 1] << 8));

        // The share of frames in which at least one hand was tracked.
        public float TrackedShare()
        {
            if (Frames == 0) return 0f;
            int seen = 0;
            for (int i = 0; i < Frames; i++)
                if ((Flags(i) & (ClipFormat.LeftTracked | ClipFormat.RightTracked)) != 0) seen++;
            return seen / (float)Frames;
        }

        // Returns null and fills `problem` when the JSON is not a valid clip.
        public static HandClip FromJson(string json, out string problem)
        {
            ClipJson dto;
            try { dto = JsonUtility.FromJson<ClipJson>(json); }
            catch (Exception e) { problem = "not JSON: " + e.Message; return null; }
            return FromDto(dto, out problem);
        }

        public static HandClip FromDto(ClipJson dto, out string problem)
        {
            problem = null;
            if (dto == null) { problem = "empty"; return null; }
            if (dto.v != ClipFormat.Version) { problem = "v must be 1"; return null; }
            if (dto.fps != ClipFormat.Fps) { problem = "fps must be 30"; return null; }
            if (dto.frames < ClipFormat.MinFrames || dto.frames > ClipFormat.MaxFrames) { problem = "frames must be 30 to 300"; return null; }
            if (string.IsNullOrEmpty(dto.word) || dto.word.Length > 40) { problem = "word must be 1 to 40 characters"; return null; }
            byte[] data;
            try { data = Convert.FromBase64String(dto.data ?? ""); }
            catch (FormatException) { problem = "data is not base64"; return null; }
            if (data.Length != dto.frames * ClipFormat.FrameBytes)
            {
                problem = "data is " + data.Length + " bytes, expected " + dto.frames * ClipFormat.FrameBytes;
                return null;
            }
            var clip = new HandClip
            {
                Id = Empty(dto.id), Word = dto.word, Glove = string.IsNullOrEmpty(dto.glove) ? "ember" : dto.glove,
                App = dto.app, Device = dto.device, Created = Empty(dto.created), By = Empty(dto.by),
                Options = dto.options != null && dto.options.Length > 0 ? dto.options : null,
                Daily = dto.daily, Frames = dto.frames, Data = data,
                Seen = dto.stats != null ? dto.stats.seen : 0, Correct = dto.stats != null ? dto.stats.correct : 0,
            };
            if (clip.TrackedShare() <= 0f) { problem = "no frame has a tracked hand"; return null; }
            return clip;
        }

        private static string Empty(string s) => string.IsNullOrEmpty(s) ? null : s;

        // The upload body: the clip fields and, when given, the player's secret id. Server fields are left out.
        public string ToUploadJson(string player)
        {
            var b = new StringBuilder(Data.Length * 4 / 3 + 256);
            b.Append("{\"v\":1,\"word\":"); Json.Quote(b, Word);
            b.Append(",\"fps\":30,\"frames\":").Append(Frames);
            b.Append(",\"glove\":"); Json.Quote(b, Glove ?? "ember");
            b.Append(",\"app\":"); Json.Quote(b, App ?? "");
            b.Append(",\"device\":"); Json.Quote(b, Device ?? "");
            if (!string.IsNullOrEmpty(player)) { b.Append(",\"player\":"); Json.Quote(b, player); }
            b.Append(",\"data\":\"").Append(Convert.ToBase64String(Data, 0, Frames * ClipFormat.FrameBytes)).Append("\"}");
            return b.ToString();
        }

        // The stored form, used for the local cache and for clips recorded in a seed session.
        public string ToStoredJson()
        {
            var b = new StringBuilder(Data.Length * 4 / 3 + 512);
            b.Append("{\"v\":1");
            if (Id != null) { b.Append(",\"id\":"); Json.Quote(b, Id); }
            b.Append(",\"word\":"); Json.Quote(b, Word);
            b.Append(",\"fps\":30,\"frames\":").Append(Frames);
            b.Append(",\"glove\":"); Json.Quote(b, Glove ?? "ember");
            b.Append(",\"app\":"); Json.Quote(b, App ?? "");
            b.Append(",\"device\":"); Json.Quote(b, Device ?? "");
            if (Created != null) { b.Append(",\"created\":"); Json.Quote(b, Created); }
            if (By != null) { b.Append(",\"by\":"); Json.Quote(b, By); }
            if (Options != null)
            {
                b.Append(",\"options\":[");
                for (int i = 0; i < Options.Length; i++) { if (i > 0) b.Append(','); Json.Quote(b, Options[i]); }
                b.Append(']');
            }
            b.Append(",\"daily\":").Append(Daily ? "true" : "false");
            b.Append(",\"stats\":{\"seen\":").Append(Seen).Append(",\"correct\":").Append(Correct).Append('}');
            b.Append(",\"data\":\"").Append(Convert.ToBase64String(Data, 0, Frames * ClipFormat.FrameBytes)).Append("\"}");
            return b.ToString();
        }
    }

    // Just enough JSON writing for request bodies, where JsonUtility cannot leave a field out or write null.
    public static class Json
    {
        public static void Quote(StringBuilder b, string s)
        {
            if (s == null) { b.Append("null"); return; }
            b.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': b.Append("\\\""); break;
                    case '\\': b.Append("\\\\"); break;
                    case '\n': b.Append("\\n"); break;
                    case '\r': b.Append("\\r"); break;
                    case '\t': b.Append("\\t"); break;
                    default:
                        if (c < 0x20) b.Append("\\u").Append(((int)c).ToString("x4"));
                        else b.Append(c);
                        break;
                }
            }
            b.Append('"');
        }

        public static string Quote(string s)
        {
            var b = new StringBuilder();
            Quote(b, s);
            return b.ToString();
        }
    }
}
