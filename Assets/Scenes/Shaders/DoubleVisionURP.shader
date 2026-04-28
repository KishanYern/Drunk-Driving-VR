Shader "Custom/DoubleVisionURP"
{
    Properties
    {
        _Offset ("Offset", Vector) = (0.01, 0.0, 0.0, 0.0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100
        ZWrite Off Cull Off ZTest Always
        Pass
        {
            Name "DoubleVision"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment
            
            // Includes for URP and Core
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _Offset;
            SAMPLER(sampler_BlitTexture); // Add the sampler definition

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Sample the screen texture twice using the URP Blit texture
                half4 col1 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, input.texcoord + _Offset.xy);
                half4 col2 = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, input.texcoord - _Offset.xy);
                
                return (col1 + col2) * 0.5;
            }
            ENDHLSL
        }
    }
}
