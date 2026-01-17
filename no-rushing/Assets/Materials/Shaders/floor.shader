Shader "CustomRenderTexture/floor"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0.05, 0.05, 0.1, 1)
        _Speed("Animation Speed", Float) = 1.0
        _Scale("Pattern Scale", Float) = 5.0
        _Complexity("Pattern Complexity", Float) = 3.0
        
        [Header(Audio Reactivity)]
        _AudioIntensity("Audio Intensity", Float) = 2.0
        _PulseSpeed("Pulse Speed", Float) = 10.0
        
        [Header(Color Cycling)]
        _Color1("Color 1", Color) = (1, 0, 0, 1)
        _Color2("Color 2", Color) = (0, 1, 0, 1)
        _Color3("Color 3", Color) = (0, 0, 1, 1)
        _ColorCycleSpeed("Color Cycle Speed", Float) = 0.5
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Background"
        }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            float4 _BaseColor;
            float _Speed;
            float _Scale;
            float _Complexity;
            float _AudioIntensity;
            float _PulseSpeed;
            
            float _AudioLoudness;
            
            float4 _Color1;
            float4 _Color2;
            float4 _Color3;
            float _ColorCycleSpeed;
            
            struct appData
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };
            
            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 viewDir : TEXCOORD1;
            };
            
            float hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }
            
            float noise(float3 x)
            {
                float3 p = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                
                return lerp(
                    lerp(lerp(hash(p + float3(0,0,0)), hash(p + float3(1,0,0)), f.x),
                         lerp(hash(p + float3(0,1,0)), hash(p + float3(1,1,0)), f.x), f.y),
                    lerp(lerp(hash(p + float3(0,0,1)), hash(p + float3(1,0,1)), f.x),
                         lerp(hash(p + float3(0,1,1)), hash(p + float3(1,1,1)), f.x), f.y),
                    f.z);
            }
            
            float fbm(float3 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                float frequency = 1.0;
                
                for(int i = 0; i < int(_Complexity); i++)
                {
                    value += amplitude * noise(p * frequency);
                    frequency *= 2.0;
                    amplitude *= 0.5;
                }
                return value;
            }
            
            v2f vert(appData v)
            {
                v2f o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.worldPos = TransformObjectToWorld(v.positionOS.xyz);
                o.viewDir = normalize(v.positionOS.xyz);
                return o;
            }
            
            float4 frag(v2f i) : SV_TARGET
            {
                float time = _Time.y * _Speed;
                float3 dir = normalize(i.viewDir);
                
                // Pattern generation
                float3 p = dir * _Scale + float3(time * 0.1, time * 0.15, time * 0.08);
                float pattern = fbm(p);
                
                // Audio pulse based on single loudness value
                float pulse = _AudioLoudness * sin(time * _PulseSpeed) * 0.5 + 0.5;
                
                // Color cycling - smoothly transition between three colors
                float colorTime = _Time.y * _ColorCycleSpeed;
                float cycle = frac(colorTime / 3.0) * 3.0;
                
                float4 currentColor;
                if (cycle < 1.0)
                {
                    currentColor = lerp(_Color1, _Color2, cycle);
                }
                else if (cycle < 2.0)
                {
                    currentColor = lerp(_Color2, _Color3, cycle - 1.0);
                }
                else
                {
                    currentColor = lerp(_Color3, _Color1, cycle - 2.0);
                }
                
                // Start with base color
                float4 color = _BaseColor;
                
                // Add audio-reactive colored pattern
                float audioZone = smoothstep(0.3, 0.7, pattern);
                color.rgb += currentColor.rgb * _AudioLoudness * _AudioIntensity * audioZone * pulse;
                
                // Overall brightness boost based on loudness
                color.rgb *= 1.0 + _AudioLoudness * 0.5;
                
                // Flash effect on loud peaks
                float loudnessFlash = pow(_AudioLoudness, 3.0) * 2.0;
                color.rgb += loudnessFlash * currentColor.rgb;
                
                return float4(color);
            }
            ENDHLSL
        }
    }
}