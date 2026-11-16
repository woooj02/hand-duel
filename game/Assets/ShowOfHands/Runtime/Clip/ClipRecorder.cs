using UnityEngine;

namespace ShowOfHands
{
    // Turns live hands into a clip (T3). Input arrives at the display rate (72, 90 or 120 Hz); output is one
    // sample every 1/30 s, interpolated between the two input frames around it, in stage space: the origin is
    // the head on the first frame and z is the way the head faced, flattened to the horizon.
    public sealed class ClipRecorder
    {
        private readonly byte[] data = new byte[ClipFormat.MaxFrames * ClipFormat.FrameBytes];
        private readonly HandFrame previous = new HandFrame(), current = new HandFrame(), smoothed = new HandFrame();
        private readonly Vector3[] lastLeft = new Vector3[ClipFormat.Joints], lastRight = new Vector3[ClipFormat.Joints];
        private Matrix4x4 toStage;
        private Quaternion toStageRotation;
        private double previousTime, currentTime;
        private int target, written;
        private bool hasPrevious, smoothedLeft, smoothedRight;

        public bool Recording { get; private set; }
        public int FramesWritten => written;
        public int TargetFrames => target;
        public float Progress => target == 0 ? 0f : written / (float)target;
        // Whether the mitt bits are set while recording (Q1). Off in tests that must run with no detector (N2).
        public bool DetectRude = true;

        public void Begin(HandFrame first, float seconds)
        {
            target = Mathf.Clamp(Mathf.RoundToInt(seconds * ClipFormat.Fps), ClipFormat.MinFrames, ClipFormat.MaxFrames);
            written = 0;
            Vector3 forward = first.HeadRotation * Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
            Quaternion yaw = Quaternion.LookRotation(forward.normalized, Vector3.up);
            toStage = Matrix4x4.TRS(first.HeadPosition, yaw, Vector3.one).inverse;
            toStageRotation = Quaternion.Inverse(yaw);
            System.Array.Clear(lastLeft, 0, lastLeft.Length);
            System.Array.Clear(lastRight, 0, lastRight.Length);
            hasPrevious = false; smoothedLeft = false; smoothedRight = false;
            previousTime = 0; currentTime = 0;
            Recording = true;
            Push(first, 0f);
        }

        // Call once per rendered frame with the time since the last call.
        public void Push(HandFrame frame, float deltaSeconds)
        {
            if (!Recording) return;
            if (hasPrevious) { previous.CopyFrom(current); previousTime = currentTime; }
            currentTime += deltaSeconds;
            Smooth(frame);
            current.CopyFrom(smoothed);
            if (!hasPrevious) { previous.CopyFrom(current); previousTime = currentTime; hasPrevious = true; }

            const double step = 1.0 / ClipFormat.Fps;
            while (written < target && written * step <= currentTime + 1e-9)
            {
                double t = written * step;
                double span = currentTime - previousTime;
                float a = span <= 1e-9 ? 1f : Mathf.Clamp01((float)((t - previousTime) / span));
                WriteFrame(a);
                written++;
            }
            if (written >= target) Recording = false;
        }

        public void Cancel() { Recording = false; written = 0; }

        // The finished clip, or null when nothing was recorded.
        static string Limit(string value)=>string.IsNullOrEmpty(value)?"unknown":value.Length>40?value.Substring(0,40):value;

        public HandClip End(string word, string glove, string app, string device)
        {
            Recording = false;
            if (written < ClipFormat.MinFrames) return null;
            var bytes = new byte[written * ClipFormat.FrameBytes];
            System.Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);
            return new HandClip { Word = word, Glove = glove, App = Limit(app), Device = Limit(device), Frames = written, Data = bytes };
        }

        // Light smoothing that gets out of the way of fast motion: slow joints are averaged, fast ones pass
        // straight through.
        private void Smooth(HandFrame frame)
        {
            smoothed.HeadPosition = frame.HeadPosition;
            smoothed.HeadRotation = frame.HeadRotation;
            SmoothHand(frame.Left, smoothed.Left, ref smoothedLeft);
            SmoothHand(frame.Right, smoothed.Right, ref smoothedRight);
        }

        private static void SmoothHand(HandState input, HandState output, ref bool has)
        {
            output.Tracked = input.Tracked;
            if (!input.Tracked) { has = false; return; }
            if (!has) { System.Array.Copy(input.Joints, output.Joints, input.Joints.Length); has = true; return; }
            for (int j = 0; j < input.Joints.Length; j++)
            {
                Vector3 delta = input.Joints[j] - output.Joints[j];
                // 4 mm of movement in one frame is already fast enough to follow exactly.
                float follow = Mathf.Clamp(delta.magnitude / 0.004f, 0.45f, 1f);
                output.Joints[j] += delta * follow;
            }
        }

        private void WriteFrame(float a)
        {
            int o = written * ClipFormat.FrameBytes;
            int flags = 0;
            bool left = current.Left.Tracked, right = current.Right.Tracked;
            if (left) flags |= ClipFormat.LeftTracked;
            if (right) flags |= ClipFormat.RightTracked;
            if (DetectRude)
            {
                if (HandPoses.IsRude(current.Left)) flags |= ClipFormat.LeftMitt;
                if (HandPoses.IsRude(current.Right)) flags |= ClipFormat.RightMitt;
            }
            data[o] = (byte)flags; data[o + 1] = (byte)(flags >> 8);

            Vector3 head = toStage.MultiplyPoint3x4(Vector3.Lerp(previous.HeadPosition, current.HeadPosition, a));
            PutPosition(o + ClipFormat.HeadOffset, head);
            Quaternion rot = toStageRotation * Quaternion.Slerp(previous.HeadRotation, current.HeadRotation, a);
            if (rot.w < 0f) rot = new Quaternion(-rot.x, -rot.y, -rot.z, -rot.w);
            int q = o + ClipFormat.HeadOffset + 6;
            PutShort(q, Mathf.RoundToInt(rot.x * 32767f)); PutShort(q + 2, Mathf.RoundToInt(rot.y * 32767f));
            PutShort(q + 4, Mathf.RoundToInt(rot.z * 32767f)); PutShort(q + 6, Mathf.RoundToInt(rot.w * 32767f));

            WriteHand(o + ClipFormat.LeftOffset, previous.Left, current.Left, lastLeft, a);
            WriteHand(o + ClipFormat.RightOffset, previous.Right, current.Right, lastRight, a);
        }

        private void WriteHand(int o, HandState before, HandState now, Vector3[] last, float a)
        {
            if (now.Tracked)
            {
                bool blend = before.Tracked;
                for (int j = 0; j < ClipFormat.Joints; j++)
                {
                    Vector3 world = blend ? Vector3.Lerp(before.Joints[j], now.Joints[j], a) : now.Joints[j];
                    last[j] = toStage.MultiplyPoint3x4(world);
                }
            }
            // An untracked hand repeats its last tracked joints.
            for (int j = 0; j < ClipFormat.Joints; j++) PutPosition(o + j * 6, last[j]);
        }

        private void PutPosition(int o, Vector3 metres)
        {
            PutShort(o, Mathf.RoundToInt(metres.x * 1000f));
            PutShort(o + 2, Mathf.RoundToInt(metres.y * 1000f));
            PutShort(o + 4, Mathf.RoundToInt(metres.z * 1000f));
        }

        private void PutShort(int o, int value)
        {
            value = Mathf.Clamp(value, short.MinValue, short.MaxValue);
            data[o] = (byte)value; data[o + 1] = (byte)(value >> 8);
        }
    }
}
