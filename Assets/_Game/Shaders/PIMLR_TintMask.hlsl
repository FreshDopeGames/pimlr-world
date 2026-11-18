#ifndef PIMLR_TINTMASK_INCLUDED
#define PIMLR_TINTMASK_INCLUDED
// PIMLR World — mask-driven tint for the outfit color picker.
// changeColor.cs sets material.color (= _BaseColor). Only pixels where _TintMask.r = 1 take that color;
// trim, tee, chains, logo outlines and skin (mask 0) keep their texture color.
// Included after LitInput.hlsl and before the URP pass file, so every Lit pass uses it.
TEXTURE2D(_TintMask); SAMPLER(sampler_TintMask);

inline void PIMLR_InitializeTintedSurfaceData(float2 uv, out SurfaceData outSurfaceData)
{
    InitializeStandardLitSurfaceData(uv, outSurfaceData);
    half mask = SAMPLE_TEXTURE2D(_TintMask, sampler_TintMask, uv).r;
    half3 baseTex = SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).rgb;
    outSurfaceData.albedo = baseTex * lerp(half3(1.0h, 1.0h, 1.0h), _BaseColor.rgb, mask);
}
#define InitializeStandardLitSurfaceData PIMLR_InitializeTintedSurfaceData
#endif
