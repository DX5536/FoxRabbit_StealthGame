// Full-screen broken-glass effect (URP port of Moein Saadati's "Broken Screen" image effect).
// - Direction map (RG = shard move direction, 0.5 = no move) refracts the screen UVs per shard.
// - Diffuse crack texture is added on top of the refracted screen.
// Without a direction map, everything is pushed radially outward from the impact point.
// All values are global properties set by GlassBreakFilter.
Shader "Hidden/ScreenFilters/GlassBreak"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "GlassBreakFilter"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_GlassBreakDiffuseTex);
            TEXTURE2D(_GlassBreakDirectionTex);
            float  _GlassBreakIntensity;
            float4 _GlassBreakParams;   // x refraction, y crack brightness, z chromatic aberration, w has direction map (1/0)
            float4 _GlassBreakTint;     // crack tint
            float4 _GlassBreakUV;       // xy scale, zw offset (used for flipping)
            float4 _GlassBreakCenter;   // xy impact point (viewport), z hole radius (no-map fallback), w screen aspect

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float2 crackUV = uv * _GlassBreakUV.xy + _GlassBreakUV.zw;
                float k = saturate(_GlassBreakIntensity);

                float2 dir;
                if (_GlassBreakParams.w > 0.5)
                {
                    dir = SAMPLE_TEXTURE2D_LOD(_GlassBreakDirectionTex, sampler_PointClamp, crackUV, 0).rg * 2.0 - 1.0;
                }
                else
                {
                    // No direction map: push everything outward from the impact point, nothing inside the hole radius.
                    float2 d = uv - _GlassBreakCenter.xy;
                    d.x *= _GlassBreakCenter.w;
                    float len = length(d);
                    dir = (d / max(len, 1e-4)) * smoothstep(_GlassBreakCenter.z, _GlassBreakCenter.z + 0.15, len);
                    dir.x /= max(_GlassBreakCenter.w, 1e-4);
                }
                if (_GlassBreakParams.w > 0.5) dir.y *= _GlassBreakUV.y < 0 ? -1.0 : 1.0; // keep map direction consistent when flipped

                // Same as the article: sample the screen from the opposite direction so shards appear pushed along dir.
                float2 offset = -dir * _GlassBreakParams.x * k;
                float ca = _GlassBreakParams.z * k;

                float3 screen;
                screen.r = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + offset * (1.0 + ca)).r;
                screen.g = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + offset).g;
                screen.b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + offset * (1.0 - ca)).b;

                float4 crack = SAMPLE_TEXTURE2D_LOD(_GlassBreakDiffuseTex, sampler_LinearClamp, crackUV, 0);
                float3 broken = screen + crack.rgb * crack.a * _GlassBreakTint.rgb * _GlassBreakParams.y;

                float3 original = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
                return float4(lerp(original, broken, k), 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
