// Full-screen TimeStop filter: maps screen luminance through a color gradient (single tint or multi-color mix).
// All values are global properties set by TimeStopFilter_Manager.
Shader "Hidden/ScreenFilters/TimeStop"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "TimeStopFilter"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_TimeStopGradientTex);
            float  _TimeStopIntensity;
            float4 _TimeStopParams;  // x contrast, y brightness, z linear color space (1/0), w screen aspect
            float4 _TimeStopRadial;  // xy center (viewport), z radius, w softness
            float4 _TimeStopEdge;    // rgb edge glow color, a edge glow strength

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;

                float4 src = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                float3 col = src.rgb;

                float lum = dot(saturate(col), float3(0.2126, 0.7152, 0.0722));
                if (_TimeStopParams.z > 0.5) lum = LinearToSRGB(lum); // perceptual luminance
                lum = saturate((lum - 0.5) * _TimeStopParams.x + 0.5 + _TimeStopParams.y);

                float3 mapped = SAMPLE_TEXTURE2D_LOD(_TimeStopGradientTex, sampler_LinearClamp, float2(lum, 0.5), 0).rgb;

                // Radial reveal mask (radius is huge in Fade mode, so mask = 1).
                float2 d = uv - _TimeStopRadial.xy;
                d.x *= _TimeStopParams.w;
                float dist = length(d);
                float soft = max(_TimeStopRadial.w, 0.0001);
                float mask = 1.0 - smoothstep(_TimeStopRadial.z - soft, _TimeStopRadial.z, dist);

                float t = saturate(_TimeStopIntensity) * mask;
                float3 result = lerp(col, mapped, t);

                // Glow on the expanding edge of the radial wipe.
                float edge = mask * (1.0 - mask) * 4.0;
                result += _TimeStopEdge.rgb * _TimeStopEdge.a * edge * saturate(_TimeStopIntensity);

                return float4(result, src.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
