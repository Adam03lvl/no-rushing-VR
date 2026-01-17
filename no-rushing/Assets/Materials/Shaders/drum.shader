Shader "CustomRenderTexture/drum"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.05, 0.05, 0.1, 1)
        _Expand ("Expand Amount", Float) = 0
        _ExpandMult ("Expand Multiplier", Float) = 0.5
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _SpecularPower("Specular Power", Range(1.0, 128.0)) = 32.0
        _SpecularColor("Specular Color", Color) = (1, 1, 1, 1)
        _WaveFrequency("Wave Frequency", Float) = 2.0
        _WaveAmplitude("Wave Amplitude", Float) = 0.1
        
        _CircleColor("Circle Color", Color) = (1, 0.5, 0, 1)
        _CircleWidth("Circle Width", Range(0.01, 0.5)) = 0.01
        _CircleSpeed("Circle Speed", Float) = 0.1
        _CircleBrightness("Circle Brightness", Range(0.0, 5.0)) = 2.0
        _CircleRotationX("Circle Rotation X", Range(-3.14159, 3.14159)) = 0
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float4 shadowCoord : TEXCOORD2;
                float2 uv : TEXCOORD3;
                float3 positionOS : TEXCOORD4; // Pass original position for circle calculation
            };
            
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Expand;
                float _ExpandMult;
                float _Smoothness;
                float _Metallic;
                float _SpecularPower;
                float4 _SpecularColor;
                float _WaveFrequency;
                float _WaveAmplitude;
                float4 _CircleColor;
                float _CircleWidth;
                float _CircleSpeed;
                float _CircleBrightness;
                float _CircleRotationX;
            CBUFFER_END
            
            // Rotation matrix around X axis
            float3x3 RotateX(float angle)
            {
                float s = sin(angle);
                float c = cos(angle);
                return float3x3(
                    1, 0, 0,
                    0, c, -s,
                    0, s, c
                );
            }
            
            // Rotation matrix around Y axis
            float3x3 RotateY(float angle)
            {
                float s = sin(angle);
                float c = cos(angle);
                return float3x3(
                    c, 0, s,
                    0, 1, 0,
                    -s, 0, c
                );
            }
            
            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                
                float3 pos = IN.positionOS.xyz;
                float3 normal = IN.normalOS;
                
                // Store original position for circle calculation
                OUT.positionOS = IN.positionOS.xyz;
                
                // Calculate expansion with multiple effects
                float expandFactor = _Expand * _ExpandMult;
                
                // Vertical wave based on angle around cylinder
                float angle = atan2(pos.z, pos.x);
                float wave = sin(angle * _WaveFrequency + _Expand * 3.0) * _WaveAmplitude * expandFactor;
                
                // Apply wave displacement only
                pos.y += wave;
                
                // Transform to world/clip space
                VertexPositionInputs vertexInput = GetVertexPositionInputs(pos);
                OUT.positionWS = vertexInput.positionWS;
                OUT.positionCS = vertexInput.positionCS;
                OUT.normalWS = TransformObjectToWorldNormal(normal);
                OUT.shadowCoord = GetShadowCoord(vertexInput);
                OUT.uv = IN.uv;
                
                return OUT;
            }
            
            half4 frag (Varyings IN) : SV_Target
            {
                // Normalize inputs
                float3 normalWS = normalize(IN.normalWS);
                float3 viewDirWS = normalize(GetWorldSpaceViewDir(IN.positionWS));
                
                // Get main light
                Light mainLight = GetMainLight(IN.shadowCoord);
                float3 lightDir = normalize(mainLight.direction);
                float3 lightColor = mainLight.color;
                
                // Calculate expanding circles based on original position
                float3 pos = IN.positionOS;
                
                // Apply X-axis rotation to the position for circle calculation
                float3x3 rotMat = RotateX(_CircleRotationX);
                pos = mul(rotMat, pos);
                
                // Distance from center axis (for radial circles)
                // After rotation, we calculate distance from the rotated axis
                float radialDist = length(float2(pos.x, pos.z));
                
                // Create expanding circles that move outward with time
                float circlePattern = 0.0;
                float expandTime = _Expand * _CircleSpeed;
                
                float circleRadius = expandTime;
                float dist = abs(radialDist - circleRadius);
                
                // Create sharp circle edge with smoothstep
                float circle = 1.0 - smoothstep(0.0, _CircleWidth, dist);
                
                circlePattern += circle;
                
                
                // Base lighting calculation (Blinn-Phong)
                float3 ambient = _BaseColor.rgb * 0.2;
                
                // Diffuse (Lambert)
                float NdotL = max(0.0, dot(normalWS, lightDir));
                float3 diffuse = _BaseColor.rgb * lightColor * NdotL;
                
                // Specular (Blinn-Phong)
                float3 halfDir = normalize(lightDir + viewDirWS);
                float NdotH = max(0.0, dot(normalWS, halfDir));
                float specular = pow(NdotH, _SpecularPower);
                float3 specularColor = _SpecularColor.rgb * lightColor * specular * _Smoothness;
                
                // Apply shadow attenuation
                float shadow = mainLight.shadowAttenuation;
                diffuse *= shadow;
                specularColor *= shadow;
                
                // Combine base lighting
                float3 baseColor = ambient + diffuse + specularColor;
                
                // Add glowing circles on top of lighting
                float3 circleGlow = _CircleColor.rgb * circlePattern * _CircleBrightness;
                float3 finalColor = baseColor + circleGlow;
                
                return half4(finalColor, _BaseColor.a);
            }
            ENDHLSL
        }
        
        // Shadow caster pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            
            ZWrite On
            ZTest LEqual
            ColorMask 0
            
            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };
            
            CBUFFER_START(UnityPerMaterial)
                float _Expand;
                float _ExpandMult;
                float _WaveFrequency;
                float _WaveAmplitude;
            CBUFFER_END
            
            float3x3 RotateY(float angle)
            {
                float s = sin(angle);
                float c = cos(angle);
                return float3x3(c, 0, s, 0, 1, 0, -s, 0, c);
            }
            
            Varyings ShadowPassVertex(Attributes IN)
            {
                Varyings OUT;
                
                float3 pos = IN.positionOS.xyz;
                float expandFactor = _Expand * _ExpandMult;
                
                // Apply same vertex transformations as main pass (wave only)
                float angle = atan2(pos.z, pos.x);
                float wave = sin(angle * _WaveFrequency + _Expand * 3.0) * _WaveAmplitude * expandFactor;
                
                pos.y += wave;
                
                OUT.positionCS = GetVertexPositionInputs(pos).positionCS;
                return OUT;
            }
            
            half4 ShadowPassFragment(Varyings IN) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}