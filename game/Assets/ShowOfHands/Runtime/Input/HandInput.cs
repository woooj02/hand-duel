using UnityEngine;

namespace ShowOfHands
{
    // One hand in world space: 26 joints in OpenXR order (see ClipFormat).
    public sealed class HandState
    {
        public bool Tracked;
        public bool AimValid, IsController, SelectPressed, RotatePressed, PinchKnown, SelectSuppressed;
        public Ray Aim;
        public Vector2 Stick;
        public readonly Vector3[] Joints = new Vector3[ClipFormat.Joints];

        public void CopyFrom(HandState other)
        {
            Tracked = other.Tracked;
            AimValid=other.AimValid; IsController=other.IsController; SelectPressed=other.SelectPressed; RotatePressed=other.RotatePressed; PinchKnown=other.PinchKnown; SelectSuppressed=other.SelectSuppressed; Aim=other.Aim; Stick=other.Stick;
            System.Array.Copy(other.Joints, Joints, Joints.Length);
        }
    }

    // What the app knows about the player in one rendered frame.
    public sealed class HandFrame
    {
        public readonly HandState Left = new HandState();
        public readonly HandState Right = new HandState();
        public Vector3 HeadPosition;
        public Quaternion HeadRotation = Quaternion.identity;

        public HandState Hand(int index) => index == 0 ? Left : Right;

        public void CopyFrom(HandFrame other)
        {
            Left.CopyFrom(other.Left);
            Right.CopyFrom(other.Right);
            HeadPosition = other.HeadPosition;
            HeadRotation = other.HeadRotation;
        }
    }

    // Where hands and the head come from: XR Hands on the headset, a script in tests and in the editor.
    public interface IHandSource
    {
        void Read(HandFrame into);
    }

    public enum GazeKind { None, Head, Eyes, Hands, Controller }

    public static class HandAim
    {
        public static bool TryRay(HandState hand, out Ray ray)
        {
            ray=hand.Aim;
            if(!hand.Tracked)return false;
            if(hand.AimValid)return ray.direction.sqrMagnitude>.5f;
            if(hand.IsController)return false;
            // Geometric fallback for providers without Meta's stabilized aim extension.
            var direction=hand.Joints[ClipFormat.IndexTip]-hand.Joints[ClipFormat.IndexProximal];
            if(direction.sqrMagnitude<.0001f)return false;
            ray=new Ray(hand.Joints[ClipFormat.IndexTip],direction.normalized);return true;
        }
    }

    // Where the player is looking. Eyes on devices with eye tracking, the head's direction otherwise.
    public interface IGazeSource
    {
        GazeKind Read(out Ray ray);
    }

    // Thumb tip to index tip, with hysteresis so a pinch does not flicker at the threshold. A hand that is
    // not tracked is never pinching, and losing tracking releases the pinch without counting as a new one.
    public sealed class PinchDetector
    {
        public const float OnDistance = 0.020f;
        public const float OffDistance = 0.035f;

        public bool Pinching { get; private set; }
        // True only on the frame the pinch starts.
        public bool Began { get; private set; }

        public void Update(HandState hand)
        {
            Began = false;
            if (!hand.Tracked) { Pinching = false; return; }
            if(hand.IsController||hand.PinchKnown){Began=hand.SelectPressed&&!Pinching;Pinching=hand.SelectPressed;return;}
            float d = Vector3.Distance(hand.Joints[ClipFormat.ThumbTip], hand.Joints[ClipFormat.IndexTip]);
            if (!Pinching && d < OnDistance) { Pinching = true; Began = true; }
            else if (Pinching && d > OffDistance) Pinching = false;
        }

        public void Reset() { Pinching = false; Began = false; }
    }

    public static class HandPoses
    {
        // How straight a finger is: 1 when the three bones are in line, toward 0 as it curls.
        public static float Straightness(HandState hand, int proximal)
        {
            Vector3 a = hand.Joints[proximal], b = hand.Joints[proximal + 1], c = hand.Joints[proximal + 2], d = hand.Joints[proximal + 3];
            float bones = Vector3.Distance(a, b) + Vector3.Distance(b, c) + Vector3.Distance(c, d);
            return bones < 1e-4f ? 0f : Vector3.Distance(a, d) / bones;
        }

        // The middle finger up on its own: the middle finger is straight and the index, ring and little
        // fingers are curled. It only decides whether a hand is drawn as a mitt (Q1).
        public static bool IsRude(HandState hand)
        {
            if (!hand.Tracked) return false;
            return Straightness(hand, ClipFormat.MiddleProximal) > 0.93f
                && Straightness(hand, ClipFormat.IndexProximal) < 0.75f
                && Straightness(hand, ClipFormat.RingProximal) < 0.75f
                && Straightness(hand, ClipFormat.LittleProximal) < 0.78f;
        }

        // An open hand: all four fingers fairly straight.
        public static bool IsOpen(HandState hand)
        {
            if (!hand.Tracked) return false;
            return Straightness(hand, ClipFormat.IndexProximal) > 0.85f
                && Straightness(hand, ClipFormat.MiddleProximal) > 0.85f
                && Straightness(hand, ClipFormat.RingProximal) > 0.85f
                && Straightness(hand, ClipFormat.LittleProximal) > 0.82f;
        }
    }

    // The opening's wave (F1). Generous on purpose: a raised hand that moves side to side a couple of times.
    // If it never fires, the opening moves on by itself, so a miss costs a few seconds and nothing else.
    public sealed class WaveDetector
    {
        private float lastX, travelled;
        private int direction, reversals,lastHand=-1;
        private bool has;

        public bool Raised { get; private set; }
        public bool Waved => reversals >= 2;

        public void Reset() { has = false; reversals = 0; direction = 0; travelled = 0f; lastHand=-1; Raised = false; }

        public void Update(HandFrame frame)
        {
            Raised = false;
            Vector3 right = frame.HeadRotation * Vector3.right;
            for (int i = 0; i < 2; i++)
            {
                var hand = frame.Hand(i);
                if (!hand.Tracked) continue;
                Vector3 palm = hand.Joints[ClipFormat.Palm];
                // Raised: above the chest (within 35 cm below the eyes).
                if (palm.y < frame.HeadPosition.y - 0.35f) continue;
                Raised = true;
                float x = Vector3.Dot(palm - frame.HeadPosition, right);
                if (!has || lastHand!=i) { has = true; lastHand=i; lastX = x; direction=0;travelled=0;return; }
                float dx = x - lastX;
                lastX = x;
                if (Mathf.Abs(dx) < 0.0015f) continue;
                int d = dx > 0 ? 1 : -1;
                if (d == direction) travelled += Mathf.Abs(dx);
                else
                {
                    if (direction != 0 && travelled > 0.04f) reversals++;
                    direction = d;
                    travelled = Mathf.Abs(dx);
                }
                return;
            }
        }
    }
}
