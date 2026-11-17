using UnityEngine;
using UnityEngine.Rendering;

namespace Xyla
{
    public sealed class AtlasDottedPointer : MonoBehaviour
    {
        const int DotCount = 15;
        Transform panel, viewer, target;
        Renderer targetRenderer;
        Transform[] dots;
        Material material;
        Vector3 localAnchor;
        Color color = new Color(0.12f, 0.92f, 1f, 0.9f);

        public void Initialize(Transform panelTransform, Transform viewerTransform, Vector3 anchor)
        {
            panel = panelTransform;
            viewer = viewerTransform;
            localAnchor = anchor;
            if (dots != null) return;
            var template = Resources.Load<Material>("AtlasDottedLaser");
            Shader laserShader = template ? template.shader : Shader.Find("Universal Render Pipeline/Unlit");
            if (!laserShader) throw new System.InvalidOperationException("Falta el material holográfico de Atlas.");
            material = template ? new Material(template) : new Material(laserShader);
            material.name = "Atlas · láser holográfico punteado";
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.One);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = 3000;
            dots = new Transform[DotCount];
            for (int i = 0; i < dots.Length; i++)
            {
                var dot = GameObject.CreatePrimitive(PrimitiveType.Quad);
                dot.name = "Punto holográfico " + (i + 1);
                dot.layer = 2;
                dot.transform.SetParent(null, true);
                dot.transform.localScale = Vector3.one * 0.004f;
                var collider = dot.GetComponent<Collider>();
                if (collider) Destroy(collider);
                var renderer = dot.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                dots[i] = dot.transform;
                dot.SetActive(false);
            }
        }

        public void SetTarget(Transform value, Color systemColor)
        {
            target = value;
            targetRenderer = target ? target.GetComponentInChildren<Renderer>() : null;
            color = Color.Lerp(systemColor, new Color(0.15f, 0.95f, 1f), 0.58f);
            color.a = 0.92f;
            if (material)
            {
                material.color = color;
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            }
            enabled = target;
            SetDots(target != null);
        }

        public void ClearTarget()
        {
            target = null;
            targetRenderer = null;
            enabled = false;
            SetDots(false);
        }

        void LateUpdate()
        {
            if (!panel || !target) { SetDots(false); return; }
            Vector3 start = panel.TransformPoint(localAnchor);
            Vector3 end = targetRenderer ? targetRenderer.bounds.center : target.position;
            float distance = Vector3.Distance(start, end);
            int visible = Mathf.Clamp(Mathf.CeilToInt(distance / 0.055f), 7, DotCount);
            float pulse = 0.88f + Mathf.Sin(Time.unscaledTime * 4.2f) * 0.12f;
            for (int i = 0; i < dots.Length; i++)
            {
                bool active = i < visible;
                if (dots[i].gameObject.activeSelf != active) dots[i].gameObject.SetActive(active);
                if (!active) continue;
                float t = (i + 0.65f) / (visible + 0.3f);
                dots[i].position = Vector3.Lerp(start, end, t);
                if (viewer) dots[i].rotation = Quaternion.LookRotation(viewer.position - dots[i].position, Vector3.up);
                float size = Mathf.Lerp(0.0062f, 0.0038f, t) * pulse;
                dots[i].localScale = new Vector3(size, size, size);
            }
        }

        void SetDots(bool active)
        {
            if (dots == null) return;
            foreach (var dot in dots) if (dot) dot.gameObject.SetActive(active);
        }

        void OnEnable()
        {
            if (target) SetDots(true);
        }

        void OnDisable()
        {
            SetDots(false);
        }

        void OnDestroy()
        {
            if (dots != null)
            {
                foreach (var dot in dots) if (dot) Destroy(dot.gameObject);
            }
            if (material) Destroy(material);
        }
    }
}