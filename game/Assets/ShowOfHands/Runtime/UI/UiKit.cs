using System.Collections.Generic;
using UnityEngine;

namespace ShowOfHands
{
    // Materials, the font and the small building blocks every screen is made of. Everything is built in code.
    public static class Ui
    {
        public static Material HandMaterial { get; private set; }
        public static Material PanelMaterial { get; private set; }
        public static Material LabelMaterial { get; private set; }
        public static Material BackdropMaterial { get; private set; }
        public static Font Font { get; private set; }
        // Settings that change how things are drawn (A4).
        public static bool HighContrast, ReducedMotion, LargeText;

        private static bool ready;

        public static void Init()
        {
            if (ready && HandMaterial != null) return;
            HandMaterial = Load("Hand");
            PanelMaterial = Load("Panel");
            BackdropMaterial = Load("Backdrop");
            Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var labelShader = Resources.Load<Shader>("Shaders/Label");
            if (labelShader != null && Font != null)
            {
                LabelMaterial = new Material(labelShader) { name = "SOH Label", hideFlags = HideFlags.HideAndDontSave };
                LabelMaterial.mainTexture = Font.material.mainTexture;
                Font.textureRebuilt += OnFontRebuilt;
            }
            ready = true;
        }

        private static void OnFontRebuilt(Font font)
        {
            if (font == Font && LabelMaterial != null) LabelMaterial.mainTexture = font.material.mainTexture;
        }

        // A material asset made by the editor setup (so instancing variants are in the build), or one made
        // here from the shader when running without those assets.
        private static Material Load(string name)
        {
            var asset = Resources.Load<Material>("Materials/" + name);
            if (asset != null) return asset;
            var shader = Resources.Load<Shader>("Shaders/" + name);
            if (shader == null) { Debug.LogError("Show of Hands: missing shader " + name); return null; }
            return new Material(shader) { name = "SOH " + name, enableInstancing = true, hideFlags = HideFlags.HideAndDontSave };
        }

        public static Panel Panel(Transform parent, string name, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var panel = go.AddComponent<Panel>();
            panel.Build();
            return panel;
        }

        public static Label Label(Transform parent, string text, float height, Color color, Vector3 localPosition, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var label = go.AddComponent<Label>();
            label.Build(text, height, color, anchor);
            return label;
        }

        // Exponential approach: the same feel at any frame rate.
        public static float Damp(float value, float target, float rate, float dt) => Mathf.Lerp(value, target, 1f - Mathf.Exp(-rate * dt));
        public static Vector3 Damp(Vector3 value, Vector3 target, float rate, float dt) => Vector3.Lerp(value, target, 1f - Mathf.Exp(-rate * dt));

        public static float EaseOut(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t) * (1f - t); }
        public static float EaseInOut(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
    }

    // Anything whose opacity a Fader can drive.
    public interface IFadable
    {
        void SetFade(float fade);
    }

    // One quad drawn with the panel shader: a rounded card, a picture, a ring or a glow.
    public sealed class Panel : MonoBehaviour, IFadable
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EdgeId = Shader.PropertyToID("_Edge");
        private static readonly int ShapeId = Shader.PropertyToID("_Shape");
        private static readonly int ModeId = Shader.PropertyToID("_Mode");
        private static readonly int TexId = Shader.PropertyToID("_MainTex");

        private MeshRenderer meshRenderer;
        private MaterialPropertyBlock block;
        private Color fill = Color.white, edge = Color.clear;
        private Vector4 shape, mode;
        private Texture picture;
        private float fade = 1f;

        public Color Fill { get => fill; set { fill = value; Apply(); } }
        public Color Edge { get => edge; set { edge = value; Apply(); } }
        public float RingFill { get => mode.y; set { mode.y = value; Apply(); } }
        public Vector2 Size => new Vector2(transform.localScale.x, transform.localScale.y);

        public void Build()
        {
            gameObject.AddComponent<MeshFilter>().sharedMesh = Shapes.Quad;
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = Ui.PanelMaterial;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            block = new MaterialPropertyBlock();
        }

        public Panel AsCard(Vector2 size, float corner, Color fillColor, Color edgeColor, float edgeWidth = 0.0025f)
        {
            transform.localScale = new Vector3(size.x, size.y, 1f);
            shape = new Vector4(size.x * 0.5f, size.y * 0.5f, corner, edgeWidth);
            mode = new Vector4(0f, 0f, 0f, 0f);
            fill = fillColor; edge = edgeColor;
            Apply();
            return this;
        }

        public Panel AsPicture(Texture texture, Vector2 size, Color tint)
        {
            transform.localScale = new Vector3(size.x, size.y, 1f);
            picture = texture;
            mode = new Vector4(1f, 0f, 0f, 0f);
            fill = tint;
            Apply();
            return this;
        }

        public Panel AsRing(float diameter, float thickness, Color on, Color off, float amount = 1f)
        {
            transform.localScale = new Vector3(diameter, diameter, 1f);
            mode = new Vector4(2f, amount, thickness, 0f);
            fill = on; edge = off;
            Apply();
            return this;
        }

        public Panel AsGlow(float diameter, Color color)
        {
            transform.localScale = new Vector3(diameter, diameter, 1f);
            mode = new Vector4(3f, 0f, 0f, 0f);
            fill = color;
            Apply();
            return this;
        }

        public void SetSortOrder(int order) { if (meshRenderer != null) meshRenderer.sortingOrder = order; }

        public void SetFade(float value) { fade = value; Apply(); }

        private void Apply()
        {
            if (meshRenderer == null) return;
            block.SetColor(ColorId, new Color(fill.r, fill.g, fill.b, fill.a * fade));
            block.SetColor(EdgeId, new Color(edge.r, edge.g, edge.b, edge.a * fade));
            block.SetVector(ShapeId, shape);
            block.SetVector(ModeId, mode);
            if (picture != null) block.SetTexture(TexId, picture);
            meshRenderer.SetPropertyBlock(block);
            meshRenderer.enabled = fade > 0.003f;
        }
    }

    // Text in the world. `height` is the height of a line in metres.
    public sealed class Label : MonoBehaviour, IFadable
    {
        private const int FontSize = 72;
        private TextMesh mesh;
        private MeshRenderer meshRenderer;
        private Color color;
        private float fade = 1f;

        public string Text { get => mesh.text; set { if (mesh.text != value) mesh.text = value; } }
        public Color Color { get => color; set { color = value; Apply(); } }

        public void Build(string text, float height, Color textColor, TextAnchor anchor)
        {
            mesh = gameObject.AddComponent<TextMesh>();
            meshRenderer = GetComponent<MeshRenderer>();
            mesh.font = Ui.Font;
            mesh.fontSize = FontSize;
            mesh.anchor = anchor;
            mesh.alignment = anchor == TextAnchor.MiddleLeft || anchor == TextAnchor.UpperLeft ? TextAlignment.Left : TextAlignment.Center;
            mesh.richText = false;
            // A TextMesh line is fontSize * characterSize / 10 units tall.
            if (Ui.LargeText) height *= 1.25f;
            mesh.characterSize = height * 10f / FontSize;
            mesh.text = text;
            if (Ui.LabelMaterial != null) meshRenderer.sharedMaterial = Ui.LabelMaterial;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            // Text faces the same way as panels: its readable side is -z.
            color = textColor;
            Apply();
        }

        public void SetFade(float value) { fade = value; Apply(); }

        private void Apply()
        {
            if (mesh == null) return;
            // TextMesh colours are sRGB vertex colours; the palette is linear.
            Color c = color.gamma;
            mesh.color = new Color(c.r, c.g, c.b, color.a * fade);
            meshRenderer.enabled = fade > 0.003f;
        }
    }

    // Fades everything under one root in and out together (P3).
    public sealed class Fader : MonoBehaviour
    {
        private readonly List<IFadable> items = new List<IFadable>();
        private float value, target = 1f;
        public float Seconds = 0.22f;
        public float Value => value;
        public bool Hidden => value <= 0.001f && target <= 0f;

        public void Collect()
        {
            items.Clear();
            foreach (var c in GetComponentsInChildren<MonoBehaviour>(true))
                if (c is IFadable f && !ReferenceEquals(c, this)) items.Add(f);
            Push();
        }

        public void Show() { target = 1f; }
        public void Hide() { target = 0f; }
        public void Snap(float v) { value = target = v; Push(); }

        private void Update()
        {
            if (Mathf.Approximately(value, target)) return;
            value = Mathf.MoveTowards(value, target, Time.deltaTime / Mathf.Max(0.01f, Seconds));
            Push();
        }

        private void Push()
        {
            float eased = Ui.EaseInOut(value);
            for (int i = 0; i < items.Count; i++) items[i].SetFade(eased);
        }
    }
}
