using UnityEngine;



namespace ShowOfHands

{

    // The six gloves (contract section 6). Colours are stored linear, ready for the shader.

    public sealed class Glove

    {

        public readonly string Id;

        public readonly Color Base, Tip;



        private Glove(string id, string baseHex, string tipHex)

        {

            Id = id;

            Base = Palette.Hex(baseHex);

            Tip = Palette.Hex(tipHex);

        }



        public static readonly Glove[] All =

        {

            new Glove("ember", "#FF8A3D", "#FFD36E"),

            new Glove("mint", "#22C99A", "#B8FFF0"),

            new Glove("violet", "#7C4DFF", "#E0D0FF"),

            new Glove("rose", "#FF4F81", "#FFD0DD"),

            new Glove("sky", "#2F9BFF", "#D0F0FF"),

            new Glove("bone", "#CFC8B8", "#FFFFFF"),

        };



        // Unknown ids draw as ember.

        public static Glove Find(string id)

        {

            foreach (var g in All) if (g.Id == id) return g;

            return All[0];

        }

    }



    public static class Palette

    {

        public static readonly Color Background = Hex("#0E0B14");

        public static readonly Color StageGlow = Hex("#2A1E3F");

        public static readonly Color Text = Hex("#F4F1E8");

        public static readonly Color Accent = Hex("#FFD36E");

        public static readonly Color Right = Hex("#5BE38C");

        public static readonly Color Wrong = Hex("#FF6B6B");

        public static readonly Color Card = Hex("#241A36");

        public static readonly Color CardEdge = Hex("#5B4A86");



        // sRGB hex to a linear colour.

        public static Color Hex(string hex)

        {

            ColorUtility.TryParseHtmlString(hex, out var c);

            return c.linear;

        }

    }



    // Draws a hand as tubes and balls (contract section 6): the same recipe the phone page uses.

    public static class HandDrawer

    {

        // Radii in metres along a finger: knuckle, middle joint, last joint, tip.

        private const float Proximal = 0.010f, Intermediate = 0.009f, Distal = 0.008f, Tip = 0.007f, PalmTube = 0.011f;



        // joints are in `space`; scale multiplies radii (the gallery draws small hands). shown: 0 to 1.

        // dim: 1 is full brightness.

        public static void Draw(ShapeBatch batch, Vector3[] joints, in Matrix4x4 space, float scale, Glove glove, float shown, bool mitt, float dim = 1f)

        {

            if (shown <= 0.01f) return;

            var w = Scratch;

            for (int i = 0; i < ClipFormat.Joints; i++) w[i] = space.MultiplyPoint3x4(joints[i]);

            if (mitt) { DrawMitt(batch, w, scale, glove, shown, dim); return; }



            Vector4 none = Vector4.zero;

            // Wrist and palm.

            Vector4 wrist = Shade(glove, 0f, shown, dim);

            Vector4 palm = Shade(glove, 0.18f, shown, dim);

            Vector4 knuckle = Shade(glove, 0.36f, shown, dim);

            batch.Sphere(w[ClipFormat.Wrist], 0.013f * scale, wrist, none);

            for (int f = 0; f < 4; f++)

            {

                int meta = ClipFormat.IndexMetacarpal + f * 5;

                batch.Tube(w[ClipFormat.Wrist], w[meta], PalmTube * scale, wrist, none);

                batch.Sphere(w[meta], PalmTube * scale, palm, none);

                batch.Tube(w[meta], w[meta + 1], PalmTube * scale, palm, none);

                if (f < 3) batch.Tube(w[meta + 1], w[meta + 6], 0.0105f * scale, knuckle, none);

                DrawFinger(batch, w, meta + 1, scale, glove, shown, dim, 0f);

            }

            Vector3 centre = (w[ClipFormat.Wrist]+w[ClipFormat.IndexProximal]+w[ClipFormat.MiddleProximal]+w[ClipFormat.RingProximal]+w[ClipFormat.LittleProximal])/5f;

            Vector3 forward = w[ClipFormat.MiddleProximal]-w[ClipFormat.Wrist];

            Vector3 side = w[ClipFormat.IndexProximal]-w[ClipFormat.LittleProximal];

            Vector3 normal = Vector3.Cross(forward,side);

            if(forward.sqrMagnitude>1e-8f&&normal.sqrMagnitude>1e-8f)

                batch.Ellipsoid(centre,Quaternion.LookRotation(forward,normal),new Vector3(.031f,.012f,.043f)*scale,palm,none);

            // The web between thumb and index.

            batch.Tube(w[ClipFormat.ThumbMetacarpal], w[ClipFormat.IndexMetacarpal], PalmTube * scale, wrist, none);

            batch.Tube(w[ClipFormat.ThumbMetacarpal], w[ClipFormat.IndexProximal], 0.009f * scale, palm, none);

            batch.Tube(w[ClipFormat.Wrist], w[ClipFormat.ThumbMetacarpal], PalmTube * scale, wrist, none);

            batch.Sphere(w[ClipFormat.ThumbMetacarpal], 0.012f * scale, palm, none);

            batch.Tube(w[ClipFormat.ThumbMetacarpal], w[ClipFormat.ThumbProximal], 0.0115f * scale, palm, none);

            DrawFinger(batch, w, ClipFormat.ThumbProximal, scale, glove, shown, dim, 0.001f, true);

        }



        public static void DrawSimple(ShapeBatch batch,Vector3[] joints,in Matrix4x4 space,float scale,Glove glove,float shown,bool mitt){
            if(shown<=.01f)return;var w=Scratch;for(int j=0;j<26;j++)w[j]=space.MultiplyPoint3x4(joints[j]);if(mitt){DrawMitt(batch,w,scale,glove,shown,1);return;}
            var c=Shade(glove,.3f,shown,1);var tip=Shade(glove,1,shown,1);batch.Sphere(w[0],.028f*scale,c,Vector4.zero);
            for(int f=0;f<5;f++){int start=f==0?2:6+(f-1)*5;int middle=f==0?3:start+2;int end=f==0?5:start+4;batch.Tube(w[1],w[start],.011f*scale,c,Vector4.zero);batch.Tube(w[start],w[middle],.009f*scale,c,Vector4.zero);batch.Tube(w[middle],w[end],.007f*scale,tip,Vector4.zero);batch.Sphere(w[end],.009f*scale,tip,Vector4.zero);}
        }
        private static void DrawFinger(ShapeBatch batch, Vector3[] w, int proximal, float scale, Glove glove, float shown, float dim, float extra, bool thumb = false)

        {

            Vector4 none = Vector4.zero;

            Vector4 c0 = Shade(glove, 0.36f, shown, dim), c1 = Shade(glove, 0.58f, shown, dim), c2 = Shade(glove, 0.8f, shown, dim), c3 = Shade(glove, 1f, shown, dim);

            // Fingertips are the brightest part.

            Vector4 glow = new Vector4(0.55f * dim, 0f, 0f, 0f);

            if (thumb)

            {

                // Thumb: proximal, distal, tip.

                batch.Sphere(w[proximal], (Proximal + 0.001f + extra) * scale, c0, none);

                batch.Tube(w[proximal], w[proximal + 1], (Proximal + extra) * scale, c1, none);

                batch.Sphere(w[proximal + 1], (Intermediate + 0.0005f + extra) * scale, c2, none);

                batch.Tube(w[proximal + 1], w[proximal + 2], (Distal + 0.0005f + extra) * scale, c2, none);

                batch.Sphere(w[proximal + 2], (Tip + extra) * scale, c3, glow);

                return;

            }

            batch.Sphere(w[proximal], (Proximal + 0.0005f) * scale, c0, none);

            batch.Tube(w[proximal], w[proximal + 1], Proximal * scale, c0, none);

            batch.Sphere(w[proximal + 1], (Intermediate + 0.0005f) * scale, c1, none);

            batch.Tube(w[proximal + 1], w[proximal + 2], Intermediate * scale, c1, none);

            batch.Sphere(w[proximal + 2], (Distal + 0.0005f) * scale, c2, none);

            batch.Tube(w[proximal + 2], w[proximal + 3], (Distal + Tip) * 0.5f * scale, c2, none);

            batch.Sphere(w[proximal + 3], Tip * scale, c3, glow);

        }



        // The oven mitt: one rounded shape over the four fingers, a thumb, and a cuff. It is meant to be funny.

        private static void DrawMitt(ShapeBatch batch, Vector3[] w, float scale, Glove glove, float shown, float dim)

        {

            Vector3 wrist = w[ClipFormat.Wrist];

            Vector3 knuckles = (w[ClipFormat.IndexProximal] + w[ClipFormat.MiddleProximal] + w[ClipFormat.RingProximal] + w[ClipFormat.LittleProximal]) * 0.25f;

            Vector3 forward = knuckles - wrist;

            float reach = forward.magnitude;

            if (reach < 1e-4f) return;

            forward /= reach;

            Vector3 side = w[ClipFormat.IndexProximal] - w[ClipFormat.LittleProximal];

            side -= forward * Vector3.Dot(side, forward);

            if (side.sqrMagnitude < 1e-8f) side = Vector3.Cross(forward, Vector3.up);

            side.Normalize();

            Vector3 up = Vector3.Cross(forward, side).normalized;

            var rotation = Quaternion.LookRotation(forward, up);



            Vector4 quilt = new Vector4(0.05f, 1f, 0f, 0f);

            Vector4 body = Shade(glove, 0.45f, shown, dim);

            Vector4 cuff = Shade(glove, 1f, shown, dim);

            // Radii: x across the hand, y through it, z along the fingers.

            batch.Ellipsoid(wrist + forward * (reach * 0.55f), rotation, new Vector3(0.050f * scale, 0.030f * scale, reach * 0.62f), body, quilt);

            batch.Ellipsoid(knuckles + forward * (0.045f * scale), rotation, new Vector3(0.052f, 0.032f, 0.062f) * scale, body, quilt);

            batch.Tube(w[ClipFormat.ThumbMetacarpal], w[ClipFormat.ThumbTip], 0.017f * scale, body, quilt);

            batch.Sphere(w[ClipFormat.ThumbTip], 0.017f * scale, body, quilt);

            batch.Sphere(w[ClipFormat.ThumbMetacarpal], 0.017f * scale, body, quilt);

            batch.Tube(wrist - forward * (0.03f * scale), wrist + forward * (0.012f * scale), 0.043f * scale, cuff, Vector4.zero);

        }



        // The head: a small rounded mask with two dot eyes, so "drinking" has something to aim at.

        public static void DrawHead(ShapeBatch batch, Vector3 position, Quaternion rotation, in Matrix4x4 space, float scale, Glove glove, float shown, float dim = 1f)

        {

            if (shown <= 0.01f) return;

            Vector3 centre = space.MultiplyPoint3x4(position + rotation * new Vector3(0f, -0.01f, -0.03f));

            Quaternion world = space.rotation * rotation;

            Vector4 mask = Shade(glove, 1f, 0.35f * shown, dim * 0.8f);

            batch.Ellipsoid(centre, world, new Vector3(0.078f, 0.100f, 0.085f) * scale, mask, Vector4.zero);

            Vector4 eye = new Vector4(Palette.Background.r, Palette.Background.g, Palette.Background.b, 0.9f * shown);

            for (int i = -1; i <= 1; i += 2)

            {

                Vector3 p = space.MultiplyPoint3x4(position + rotation * new Vector3(0.030f * i, 0.012f, 0.050f));

                batch.Sphere(p, 0.009f * scale, eye, Vector4.zero);

            }

        }



        private static Vector4 Shade(Glove glove, float t, float shown, float dim)

        {

            Color c = Color.Lerp(glove.Base, glove.Tip, t);

            return new Vector4(c.r * dim, c.g * dim, c.b * dim, shown);

        }



        [System.ThreadStatic] private static Vector3[] scratch;

        private static Vector3[] Scratch => scratch ?? (scratch = new Vector3[ClipFormat.Joints]);

    }

}

