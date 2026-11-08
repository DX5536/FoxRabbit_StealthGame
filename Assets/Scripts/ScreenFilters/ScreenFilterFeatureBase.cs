using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Base URP renderer feature for full-screen filters driven by global shader properties.
/// The pass is only enqueued while the filter's global intensity is above zero, so it costs nothing when idle.
/// Add the concrete features (TimeStopFilterFeature / GlassBreakFilterFeature) to your Renderer2D asset.
/// </summary>
public abstract class ScreenFilterFeatureBase : ScriptableRendererFeature
{
    public enum CameraTarget
    {
        /// <summary>Base camera only. Overlay cameras stacked on top (e.g. the IgnoreTimeStop camera) are not filtered.</summary>
        BaseCameraOnly,
        /// <summary>Last camera of the stack only, i.e. after every camera has rendered.</summary>
        FinalCameraInStack,
        AllCameras
    }

    [SerializeField] protected Shader shader;
    [SerializeField] protected RenderPassEvent passEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    [SerializeField] protected CameraTarget cameraTarget = CameraTarget.BaseCameraOnly;
    [Tooltip("Also show the filter in the Scene view while it is active in play mode.")]
    [SerializeField] protected bool showInSceneView = false;

    protected abstract string DefaultShaderName { get; }
    protected abstract string IntensityProperty { get; }

    Material material;
    FilterPass pass;
    int intensityId;

    public override void Create()
    {
        if (shader == null) shader = Shader.Find(DefaultShaderName);
        intensityId = Shader.PropertyToID(IntensityProperty);

        if (material != null) CoreUtils.Destroy(material);
        material = shader != null ? CoreUtils.CreateEngineMaterial(shader) : null;

        pass = new FilterPass(name, cameraTarget);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (material == null || pass == null) return;
        if (Shader.GetGlobalFloat(intensityId) <= 0.0001f) return;

        var type = renderingData.cameraData.cameraType;
        if (type == CameraType.Preview || type == CameraType.Reflection) return;
        if (type == CameraType.SceneView && !showInSceneView) return;

        pass.Setup(material, passEvent, type == CameraType.Game ? cameraTarget : CameraTarget.AllCameras);
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(material);
        material = null;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (shader == null)
        {
            shader = Shader.Find(DefaultShaderName);
            if (shader != null) UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif

    class FilterPass : ScriptableRenderPass
    {
        readonly string filterName;
        Material material;
        CameraTarget target;

        public FilterPass(string name, CameraTarget target)
        {
            filterName = string.IsNullOrEmpty(name) ? "ScreenFilter" : name;
            this.target = target;
            profilingSampler = new ProfilingSampler(filterName);
            requiresIntermediateTexture = true;
        }

        public void Setup(Material mat, RenderPassEvent evt, CameraTarget camTarget)
        {
            material = mat;
            renderPassEvent = evt;
            target = camTarget;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var cameraData = frameData.Get<UniversalCameraData>();
            if (target == CameraTarget.BaseCameraOnly && cameraData.renderType != CameraRenderType.Base) return;
            if (target == CameraTarget.FinalCameraInStack && !cameraData.resolveFinalTarget) return;

            var resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer) return;

            TextureHandle source = resourceData.activeColorTexture;
            if (!source.IsValid()) return;

            TextureDesc desc = renderGraph.GetTextureDesc(source);
            desc.name = filterName + "_Temp";
            desc.clearBuffer = false;
            desc.msaaSamples = MSAASamples.None;
            TextureHandle temp = renderGraph.CreateTexture(desc);

            // Filter into a temp texture, then copy back so later cameras in the stack keep drawing on top of it.
            renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source, temp, material, 0), passName: filterName);
            renderGraph.AddBlitPass(temp, source, Vector2.one, Vector2.zero, passName: filterName + "_CopyBack");
        }
    }
}
