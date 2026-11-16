using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShowOfHands
{
    // Something the player can choose: a flat rectangle in the world whose readable side is -z.
    public sealed class Target
    {
        public Transform Transform;
        public Vector2 HalfSize;
        // Optional physical radius for a floating object; zero keeps flat-card poke behavior.
        public float TouchRadius;
        public Action OnSelect;
        public bool Enabled = true;
        public bool AllowDwell = true;
        // Driven by the pointer, read by whatever draws the target.
        public bool Hovered;
        public float Dwell;
        public float PressedAt = -10f;
    }

    // Direct fingertip poke takes priority over a hand/controller ray. Head pose never selects.
    public sealed class Pointer
    {
        public const float ConeDegrees=1f, SelectCooldown=.3f;
        const float PokeArmDistance=.018f, PokePressDistance=.010f;
        readonly List<Target> targets=new List<Target>();
        readonly PinchDetector[] pinch={new PinchDetector(),new PinchDetector()};
        readonly Target[] candidates=new Target[2], touchTarget=new Target[2];
        readonly Vector3[] points=new Vector3[2];
        readonly bool[] touchArmed=new bool[2], visible=new bool[2];
        readonly bool[] hadTip=new bool[2],pressArmed=new bool[2];
        readonly Vector3[] previousTip=new Vector3[2];
        readonly Ray[] rays=new Ray[2];
        float sinceSelect=10,clock;
        bool dwellArmed=true;
        public Target Hovered {get;private set;}
        public bool DwellEnabled;
        public float DwellSeconds=1.2f;
        public bool Active=true;
        public int HandMask=3;
        public bool AnyPinchBegan {get;private set;}
        public Vector3 GazePoint {get;private set;}
        public GazeKind Kind {get;private set;}
        public int Selections {get;private set;}
        public int HoverHand {get;private set;}=-1;
        public float Clock=>clock;
        public bool RayVisible(int h)=>Active&&visible[h];
        public Ray HandRay(int h)=>rays[h];
        public Vector3 HandPoint(int h)=>points[h];
        public void Add(Target target){if(!targets.Contains(target))targets.Add(target);}
        public void Remove(Target target){targets.Remove(target);if(Hovered==target)SetHovered(null);}
        public void Clear(){SetHovered(null);targets.Clear();for(int h=0;h<2;h++){candidates[h]=touchTarget[h]=null;touchArmed[h]=visible[h]=hadTip[h]=pressArmed[h]=false;}}
        public void Update(HandFrame hands,Ray ignored,GazeKind ignoredKind,float dt)
        {
            clock+=dt;sinceSelect+=dt;AnyPinchBegan=false;Kind=GazeKind.None;HoverHand=-1;
            for(int h=0;h<2;h++){
                pinch[h].Update(hands.Hand(h));candidates[h]=null;visible[h]=false;
                bool inputEnabled=(HandMask&(1<<h))!=0&&!hands.Hand(h).SelectSuppressed;
                if(!hands.Hand(h).Tracked||!inputEnabled){pressArmed[h]=false;hadTip[h]=touchArmed[h]=false;touchTarget[h]=null;}else if(!pinch[h].Pinching)pressArmed[h]=true;
                if(inputEnabled)AnyPinchBegan|=pinch[h].Began;
            }
            if(!Active){SetHovered(null);for(int h=0;h<2;h++){touchTarget[h]=null;touchArmed[h]=hadTip[h]=false;}return;}
            for(int h=0;h<2;h++){
                var hand=hands.Hand(h);
                if((HandMask&(1<<h))==0||!hand.Tracked||hand.SelectSuppressed){touchTarget[h]=null;touchArmed[h]=hadTip[h]=false;continue;}
                // Proximity suppresses the ray. A press must first approach from the front;
                // remaining inside, dragging sideways, or newly appearing behind cannot press again.
                Target touch=null;float depth=0,nearest=float.MaxValue;bool sweptTouch=false;Vector3 touchPoint=Vector3.zero;
                if(!hand.IsController)foreach(var t in targets){
                    if(!Usable(t))continue;
                    if(t.TouchRadius>0){
                        float surface=Vector3.Distance(hand.Joints[ClipFormat.IndexTip],t.Transform.position)-t.TouchRadius;
                        if(surface>.05f)continue;
                        if(Mathf.Abs(surface)<nearest){touch=t;depth=-surface;nearest=Mathf.Abs(surface);sweptTouch=false;touchPoint=t.Transform.position;}
                        continue;
                    }
                    var local=t.Transform.InverseTransformPoint(hand.Joints[ClipFormat.IndexTip]);
                    float z=local.z*Mathf.Abs(t.Transform.lossyScale.z);
                    bool inside=Mathf.Abs(local.x)<=t.HalfSize.x&&Mathf.Abs(local.y)<=t.HalfSize.y&&z>=-.09f&&z<=.035f;
                    bool swept=false;var contact=local;
                    if(hadTip[h]&&(hand.Joints[ClipFormat.IndexTip]-previousTip[h]).sqrMagnitude<.25f*.25f){
                        var old=t.Transform.InverseTransformPoint(previousTip[h]);float oldZ=old.z*Mathf.Abs(t.Transform.lossyScale.z);
                        if(oldZ < -PokeArmDistance && z >= -PokePressDistance && z>oldZ){
                            var at=Vector3.Lerp(old,local,(-PokePressDistance-oldZ)/(z-oldZ));
                            if(Mathf.Abs(at.x)<=t.HalfSize.x&&Mathf.Abs(at.y)<=t.HalfSize.y){swept=true;contact=at;}
                        }
                    }
                    if(!inside&&!swept)continue;
                    float distance=swept?0:Mathf.Abs(z);
                    if(distance<nearest){touch=t;depth=swept?-PokePressDistance:z;nearest=distance;sweptTouch=swept;touchPoint=t.Transform.TransformPoint(new Vector3(contact.x,contact.y,0));}
                }
                if(touch!=null){
                    if(touchTarget[h]!=touch)touchArmed[h]=false;
                    if(sweptTouch)touchArmed[h]=true;
                    if(hadTip[h]){
                        if(touch.TouchRadius>0){if(Vector3.Distance(previousTip[h],touch.Transform.position)>touch.TouchRadius+.018f)touchArmed[h]=true;}
                        else{var old=touch.Transform.InverseTransformPoint(previousTip[h]);if(Mathf.Abs(old.x)<touch.HalfSize.x&&Mathf.Abs(old.y)<touch.HalfSize.y&&old.z*Mathf.Abs(touch.Transform.lossyScale.z)<-PokeArmDistance)touchArmed[h]=true;}
                    }
                    if(depth<-PokeArmDistance)touchArmed[h]=true;
                    previousTip[h]=hand.Joints[ClipFormat.IndexTip];hadTip[h]=true;
                    touchTarget[h]=touch;candidates[h]=touch;points[h]=touchPoint;
                    if(touchArmed[h]&&depth>=(touch.TouchRadius>0?-.004f:-PokePressDistance)&&sinceSelect>SelectCooldown){touchArmed[h]=false;SetHovered(touch);HoverHand=h;GazePoint=points[h];Kind=GazeKind.Hands;Select(touch);return;}
                    continue;
                }
                touchTarget[h]=null;touchArmed[h]=false;
                previousTip[h]=hand.Joints[ClipFormat.IndexTip];hadTip[h]=!hand.IsController;
                if(!HandAim.TryRay(hand,out var ray))continue;
                rays[h]=ray;float best=ConeDegrees;
                foreach(var t in targets){
                    if(!Usable(t))continue;float angle=AngleTo(t,ray,out var point);
                    if(angle<best){best=angle;candidates[h]=t;points[h]=point;}
                }
                visible[h]=candidates[h]!=null;
            }
            // The hand that pinches can only select its own candidate.
            for(int h=0;h<2;h++)if(pressArmed[h]&&pinch[h].Began&&candidates[h]!=null&&sinceSelect>SelectCooldown){
                pressArmed[h]=false;HoverHand=h;GazePoint=points[h];Kind=hands.Hand(h).IsController?GazeKind.Controller:GazeKind.Hands;SetHovered(candidates[h]);Select(candidates[h]);return;
            }
            int chosen=candidates[1]!=null?1:candidates[0]!=null?0:-1;
            HoverHand=chosen;var next=chosen<0?null:candidates[chosen];
            if(next!=Hovered){SetHovered(next);dwellArmed=true;}
            if(chosen>=0){GazePoint=points[chosen];Kind=hands.Hand(chosen).IsController?GazeKind.Controller:GazeKind.Hands;}
            if(DwellEnabled&&Hovered!=null&&Hovered.AllowDwell&&dwellArmed){Hovered.Dwell+=dt/Mathf.Max(.2f,DwellSeconds);if(Hovered.Dwell>=1){var target=Hovered;dwellArmed=false;Select(target);}}
        }
        static bool Usable(Target t)=>t.Enabled&&t.Transform!=null&&t.Transform.gameObject.activeInHierarchy;
        void Select(Target target){sinceSelect=0;target.PressedAt=clock;Selections++;target.OnSelect?.Invoke();}
        void SetHovered(Target target){if(Hovered!=null){Hovered.Hovered=false;Hovered.Dwell=0;}Hovered=target;if(target!=null)target.Hovered=true;}
        public static float AngleTo(Target t,Ray ray,out Vector3 nearest)
        {
            var tr=t.Transform;float denom=Vector3.Dot(ray.direction,tr.forward);
            nearest=tr.position;if(Mathf.Abs(denom)<.0001f)return 180;
            float distance=Vector3.Dot(tr.position-ray.origin,tr.forward)/denom;if(distance<=0)return 180;
            var local=tr.InverseTransformPoint(ray.GetPoint(distance));
            local.x=Mathf.Clamp(local.x,-t.HalfSize.x,t.HalfSize.x);local.y=Mathf.Clamp(local.y,-t.HalfSize.y,t.HalfSize.y);local.z=0;
            nearest=tr.TransformPoint(local);return Vector3.Angle(ray.direction,nearest-ray.origin);
        }
    }

    // A card: a rounded panel with a picture and a word. It lifts when looked at and presses when chosen.
    public sealed class Card : MonoBehaviour
    {
        public enum Look { Normal, Right, Wrong, Dim, Accent }

        public Target Target { get; private set; }
        public Label Text { get; private set; }
        public Panel Back { get; private set; }
        public Panel Picture { get; private set; }
        public Look State = Look.Normal;

        private Pointer pointer;
        private Panel dwellRing;
        private Vector3 home;
        private float hover, press, pop;
        private Vector2 size;

        public static Card Create(Transform parent, Pointer pointer, Vector3 localPosition, Vector2 size, string text, Texture picture, Action onSelect)
        {
            var go = new GameObject("Card " + text);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var card = go.AddComponent<Card>();
            card.Build(pointer, size, text, picture, onSelect);
            return card;
        }

        private void Build(Pointer p, Vector2 cardSize, string text, Texture picture, Action onSelect)
        {
            pointer = p; size = cardSize; home = transform.localPosition;
            Back = Ui.Panel(transform, "Back", Vector3.zero).AsCard(size, Mathf.Min(size.x, size.y) * 0.18f, Palette.Card, Palette.CardEdge);
            bool hasText = !string.IsNullOrEmpty(text);
            float textHeight = Mathf.Clamp(size.y * 0.2f, 0.030f, 0.036f);
            if (picture != null)
            {
                float side = Mathf.Min(size.x, size.y - (hasText ? textHeight * 1.9f : 0f)) * 0.72f;
                Picture = Ui.Panel(transform, "Picture", new Vector3(0f, hasText ? textHeight * 0.85f : 0f, -0.002f)).AsPicture(picture, new Vector2(side, side), Color.white);
                Picture.SetSortOrder(1);
            }
            if (hasText)
            {
                float y = picture != null ? -size.y * 0.5f + textHeight * 1.15f : 0f;
                Text = Ui.Label(transform, text, picture != null ? textHeight : Mathf.Min(size.y * 0.3f, 0.036f), Palette.Text, new Vector3(0f, y, -0.003f));
            }
            dwellRing = Ui.Panel(transform, "Dwell", new Vector3(size.x * 0.5f - 0.02f, size.y * 0.5f - 0.02f, -0.004f))
                .AsRing(0.022f, 0.3f, Palette.Accent, new Color(1f, 1f, 1f, 0.12f), 0f);
            dwellRing.SetSortOrder(2);
            dwellRing.gameObject.SetActive(false);
            Target = new Target { Transform = transform, HalfSize = size * 0.5f, OnSelect = onSelect };
            pointer?.Add(Target);
        }

        public void SetInteractable(bool on) { Target.Enabled = on; }
        public void EnsureMinimumTouchSize(float metres)
        {
            var scale=transform.lossyScale;
            Target.HalfSize=Vector2.Max(Target.HalfSize,new Vector2(metres/(2*Mathf.Max(.001f,Mathf.Abs(scale.x))),metres/(2*Mathf.Max(.001f,Mathf.Abs(scale.y)))));
        }

        // A quick scale bounce, used for a right answer (P6).
        public void Pop() { pop = 1f; }

        private void OnDestroy() { pointer?.Remove(Target); }

        private void Update()
        {
            float dt = Time.deltaTime;
            bool motion = !Ui.ReducedMotion;
            hover = Ui.Damp(hover, Target.Hovered && Target.Enabled ? 1f : 0f, 18f, dt);
            float sincePress = pointer != null ? pointer.Clock - Target.PressedAt : 10f;
            press = sincePress < 0.18f ? Mathf.Sin(sincePress / 0.18f * Mathf.PI) : 0f;
            pop = Mathf.MoveTowards(pop, 0f, dt / 0.35f);

            // Toward the player is -z.
            float lift = 0f;
            transform.localPosition = home + new Vector3(0f, 0f, -lift);
            float scale = 1f + (motion ? hover * 0.05f - press * 0.04f + Mathf.Sin(pop * Mathf.PI) * 0.12f : 0f);
            transform.localScale = new Vector3(scale, scale, 1f);

            Color fill = Palette.Card, edge = Palette.CardEdge;
            switch (State)
            {
                case Look.Right: fill = Color.Lerp(Palette.Card, Palette.Right, 0.55f); edge = Palette.Right; break;
                case Look.Wrong: fill = Color.Lerp(Palette.Card, Palette.Wrong, 0.45f); edge = Palette.Wrong; break;
                case Look.Dim: fill = Palette.Card * 0.6f; fill.a = 0.7f; edge = Palette.CardEdge * 0.6f; break;
                case Look.Accent: edge = Palette.Accent; break;
            }
            if (Ui.HighContrast) { fill = State == Look.Normal ? Color.black : fill; edge = State == Look.Normal ? Color.white : edge; fill.a = 1f; }
            edge = Color.Lerp(edge, Palette.Accent, hover * (State == Look.Normal || State == Look.Accent ? 1f : 0f));
            fill = Color.Lerp(fill, fill * 1.35f + new Color(0.01f, 0.008f, 0.02f, 0f), hover);
            fill.a = State == Look.Dim ? 0.7f : 0.94f;
            Back.Fill = fill; Back.Edge = edge;
            if (Text != null) Text.Color = State == Look.Dim ? new Color(Palette.Text.r, Palette.Text.g, Palette.Text.b, 0.55f) : Palette.Text;
            if (Picture != null) Picture.Fill = State == Look.Dim ? new Color(0.6f, 0.6f, 0.6f, 0.7f) : Color.white;

            bool showDwell = Target.Dwell > 0.02f;
            if (dwellRing.gameObject.activeSelf != showDwell) dwellRing.gameObject.SetActive(showDwell);
            if (showDwell) dwellRing.RingFill = Target.Dwell;
        }
    }
}
