using UnityEngine;

namespace ShowOfHands
{
    // One moment of a clip, ready to draw. Positions are in stage space.
    public sealed class ClipPose
    {
        public readonly Vector3[] Left = new Vector3[ClipFormat.Joints];
        public readonly Vector3[] Right = new Vector3[ClipFormat.Joints];
        // 0 hidden to 1 fully shown.
        public float LeftShown, RightShown;
        public bool LeftMitt, RightMitt;
        public Vector3 HeadPosition;
        public Quaternion HeadRotation = Quaternion.identity;
        public float HeadShown;
    }

    // Plays a clip at any display rate (T4): interpolates between the 30 Hz samples, fades a hand out while it
    // is untracked, and fades the whole clip out and in across the loop point so the loop never jumps.
    public sealed class ClipPlayer
    {
        public const float FadeSeconds = 0.15f;
        public const float LoopFadeSeconds = 0.3f;

        public HandClip Clip { get; private set; }
        public float Time { get; private set; }
        public int Loops { get; private set; }
        public bool Loop = true;
        public bool Finished { get; private set; }
        public readonly ClipPose Pose = new ClipPose();

        private float leftSeen, rightSeen;

        public void Play(HandClip clip, float startTime = 0f)
        {
            Clip = clip;
            Time = Mathf.Max(0f, startTime);
            Loops = 0;
            Finished = false;
            leftSeen = rightSeen = 0f;
            Sample();
        }

        public void Advance(float deltaSeconds)
        {
            if (Clip == null || Finished) return;
            Time += deltaSeconds;
            float length = Clip.Seconds;
            if (Time >= length)
            {
                if (Loop) { Time %= length; Loops++; }
                else { Time = length; Finished = true; }
            }
            int frame = Mathf.Min(Clip.Frames - 1, (int)(Time * ClipFormat.Fps));
            int flags = Clip.Flags(frame);
            float step = FadeSeconds <= 0f ? 1f : deltaSeconds / FadeSeconds;
            leftSeen = Mathf.MoveTowards(leftSeen, (flags & ClipFormat.LeftTracked) != 0 ? 1f : 0f, step);
            rightSeen = Mathf.MoveTowards(rightSeen, (flags & ClipFormat.RightTracked) != 0 ? 1f : 0f, step);
            Sample();
        }

        private void Sample()
        {
            if (Clip == null) return;
            float f = Time * ClipFormat.Fps;
            int a = Mathf.Clamp((int)f, 0, Clip.Frames - 1);
            int b = Mathf.Min(a + 1, Clip.Frames - 1);
            float t = Mathf.Clamp01(f - a);
            int flagsA = Clip.Flags(a), flagsB = Clip.Flags(b);

            SampleHand(0, a, b, t, flagsA, flagsB, ClipFormat.LeftTracked, Pose.Left);
            SampleHand(1, a, b, t, flagsA, flagsB, ClipFormat.RightTracked, Pose.Right);
            Pose.LeftMitt = (flagsA & ClipFormat.LeftMitt) != 0;
            Pose.RightMitt = (flagsA & ClipFormat.RightMitt) != 0;
            Pose.HeadPosition = Vector3.Lerp(Clip.HeadPosition(a), Clip.HeadPosition(b), t);
            Pose.HeadRotation = Quaternion.Slerp(Clip.HeadRotation(a), Clip.HeadRotation(b), t);

            float edge = 1f;
            if (Loop && Clip.Seconds > LoopFadeSeconds * 3f)
            {
                float fromStart = Time, toEnd = Clip.Seconds - Time;
                edge = Mathf.Clamp01(Mathf.Min(fromStart, toEnd) / LoopFadeSeconds);
            }
            Pose.LeftShown = leftSeen * edge;
            Pose.RightShown = rightSeen * edge;
            Pose.HeadShown = edge;
        }

        private void SampleHand(int hand, int a, int b, float t, int flagsA, int flagsB, int bit, Vector3[] into)
        {
            // Interpolate only between two tracked samples: an untracked sample holds stale joints.
            bool blend = (flagsA & bit) != 0 && (flagsB & bit) != 0;
            int from = (flagsA & bit) != 0 || (flagsB & bit) == 0 ? a : b;
            for (int j = 0; j < ClipFormat.Joints; j++)
            {
                Vector3 p = Clip.Joint(from, hand, j);
                into[j] = blend ? Vector3.Lerp(p, Clip.Joint(b, hand, j), t) : p;
            }
        }
    }
}
