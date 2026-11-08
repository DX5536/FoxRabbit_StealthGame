using UnityEngine.Rendering.Universal;

/// <summary>
/// Renderer feature that applies the TimeStop color filter. Driven by TimeStopFilter_Manager.
/// Default target is the base camera only, so anything rendered by stacked overlay cameras
/// (the auto-created IgnoreTimeStop camera) and Screen Space - Overlay canvases stays unfiltered.
/// </summary>
public class TimeStopFilterFeature : ScreenFilterFeatureBase
{
    public TimeStopFilterFeature()
    {
        cameraTarget = CameraTarget.BaseCameraOnly;
        passEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    protected override string DefaultShaderName => "Hidden/ScreenFilters/TimeStop";
    protected override string IntensityProperty => "_TimeStopIntensity";
}
