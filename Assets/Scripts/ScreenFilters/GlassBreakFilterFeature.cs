using UnityEngine.Rendering.Universal;

/// <summary>
/// Renderer feature that applies the broken-glass screen effect. Driven by GlassBreakFilter.
/// Default target is the final camera in the stack, so the cracks cover everything the cameras draw
/// (including IgnoreTimeStop objects), but not Screen Space - Overlay canvases such as the pause menu.
/// </summary>
public class GlassBreakFilterFeature : ScreenFilterFeatureBase
{
    public GlassBreakFilterFeature()
    {
        cameraTarget = CameraTarget.FinalCameraInStack;
        passEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    protected override string DefaultShaderName => "Hidden/ScreenFilters/GlassBreak";
    protected override string IntensityProperty => "_GlassBreakIntensity";
}
