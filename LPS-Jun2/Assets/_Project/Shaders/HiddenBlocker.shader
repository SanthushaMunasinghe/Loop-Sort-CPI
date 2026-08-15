Shader "Custom/HiddenBlocker"
{
    Properties
    {
        _MainTex ("Question Mark Texture", 2D) = "white" {}
        _Color ("Background Color", Color) = (0.5, 0.5, 0.5, 1)
        _QuestionColor ("Question Mark Color", Color) = (1, 1, 1, 1)
        _ShadowColor ("Shadow Color", Color) = (0.3, 0.3, 0.3, 1.0)
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _Speed ("Animation Speed", Range(0.1, 5.0)) = 1.0
        _Density ("Question Mark Density", Range(1, 20)) = 8
        _RotationVariance ("Rotation Variance", Range(0, 360)) = 180
        _Scale ("Question Mark Scale", Range(0.1, 1.0)) = 0.6
        _Direction ("Movement Direction", Vector) = (1, 0, 0, 0)
        _Spacing ("Question Mark Spacing", Range(0.1, 0.9)) = 0.5
        _RenderScale ("Render Mesh Scale", Range(0.95, 1.2)) = 1

        [Header(Dissolve)]
        _DissolveTex ("Dissolve Texture", 2D) = "white" {}
        _DissolveAmount ("Dissolve Amount", Range(0, 1)) = 0
        _DissolveScale ("Dissolve Scale", Range(0, 10.0)) = 3.0
        _DissolveColor ("Dissolve Edge Color", Color) = (1, 0, 0, 1)
        _DissolveWidth ("Dissolve Edge Width", Range(0, 0.2)) = 0.05
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
		Offset 0.001, 0.001

        // Shadow pass - this allows the cube to cast shadows with the shadow color
        Pass {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_shadowcaster
            #include "UnityCG.cginc"

            sampler2D _DissolveTex;
            float4 _DissolveTex_ST;
            float _DissolveAmount;
            float _RenderScale;

            struct v2f {
                V2F_SHADOW_CASTER;
                float2 uv : TEXCOORD1;
            };

            v2f vert(appdata_base v) {
                v2f o;
                float inflate = _RenderScale - 1.0;
                v.vertex.xyz += normalize(v.normal) * inflate;
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                o.uv = TRANSFORM_TEX(v.texcoord, _DissolveTex);
                return o;
            }

            float4 frag(v2f i) : SV_Target {
                fixed4 dissolve = tex2D(_DissolveTex, i.uv);
                clip(dissolve.r - _DissolveAmount);
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }

        // Main pass - using standard lighting with PBR
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vertSurface
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        fixed4 _QuestionColor;
        fixed4 _ShadowColor;
        half _Glossiness;
        half _Metallic;
        float _Speed;
        float _Density;
        float _RotationVariance;
        float _Scale;
        float4 _Direction;
        float _Spacing;
        float _RenderScale;

        sampler2D _DissolveTex;
        float _DissolveAmount;
        float _DissolveScale;
        fixed4 _DissolveColor;
        float _DissolveWidth;

        struct Input
        {
            float3 worldPos;
            float3 worldNormal;
            float2 uv_DissolveTex;
        };

        void vertSurface(inout appdata_full v)
        {
            // Inflate along normals to avoid pivot-based directional shift.
            float inflate = _RenderScale - 1.0;
            v.vertex.xyz += normalize(v.normal) * inflate;
        }

        // Better hash function for random values
        float2 hash22(float2 p)
        {
            p = float2(dot(p, float2(127.1, 311.7)),
                       dot(p, float2(269.5, 183.3)));
            return frac(sin(p) * 43758.5453123);
        }

        // 2D rotation matrix
        float2x2 rot2D(float angle)
        {
            float s = sin(angle);
            float c = cos(angle);
            return float2x2(c, -s, s, c);
        }

        // Improved modulus function using floats instead of ints
        float fmod2(float x)
        {
            return x - 2.0 * floor(x * 0.5);
        }

        // Check if cell should be rendered based on checkerboard pattern
        bool shouldRenderCell(float2 cell_id)
        {
            // Use float-based modulus to avoid integer operations
            float checkX = fmod2(cell_id.x + 1000.0); // Add offset to handle negatives
            float checkY = fmod2(cell_id.y + 1000.0);

            // Convert to 0 or 1
            checkX = step(1.0, checkX);
            checkY = step(1.0, checkY);

            // Check if sum is odd (checkerboard pattern)
            float sum = checkX + checkY;
            return fmod2(sum) > 0.5;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Background color will be our base color
            fixed4 baseColor = _Color;

            // Normalize direction
            float2 dir = normalize(_Direction.xy);

            // Time variables for animation - add small offset to prevent exact zero
            float t = _Time.y * _Speed + 0.001;

            // Global offset based on time and direction
            float2 globalOffset = dir * t;

            // ─────────────────────────────────────────────────────────────
            // UVs built from world position – tri-planar variant
            float3 absN   = abs(IN.worldNormal) + 1e-5;          // avoid div-by-0
            float3 weight = absN / (absN.x + absN.y + absN.z);   // blend weights

            float2 uvX = IN.worldPos.yz;     // for faces whose normal is ±X
            float2 uvY = IN.worldPos.xz;     // for ±Y
            float2 uvZ = IN.worldPos.xy;     // for ±Z

            // choose the projection with the largest weight (cheap variant)
            float2 uv = (absN.x > absN.y && absN.x > absN.z) ? uvX :
                        (absN.y > absN.z)                    ? uvY : uvZ;

            // Dissolve Logic
            float2 dissolveUV = uv / _DissolveScale;
            fixed4 dissolve = tex2D(_DissolveTex, dissolveUV);
            clip(dissolve.r - _DissolveAmount);

            // Base UV with global animation offset
            float2 baseUV = uv + globalOffset * (1.0 / _Density);

            // Adjust density for the staggered grid
            float adjustedDensity = _Density * 0.7071; // 1/sqrt(2) to account for diagonal spacing

            // Scale UVs based on adjusted density
            float2 scaledUV = baseUV * adjustedDensity;

            // Setup grid - handle negative coordinates properly
            float2 id = floor(scaledUV);
            float2 grid_uv = frac(scaledUV);

            // Fix for negative coordinates: ensure grid_uv is always positive
            grid_uv = frac(scaledUV + 1000.0);

            // Initialize color as the background
            fixed4 col = baseColor;

            // Pre-calculate texture samples to avoid gradient issues in loop
            // We'll sample multiple positions and blend them
            fixed4 questionSamples[9];
            float questionWeights[9];
            int sampleIndex = 0;

            // Sample the grid and place question marks
            [unroll] // Force unroll to avoid gradient warnings
            for (int y = -1; y <= 1; y++) {
                [unroll]
                for (int x = -1; x <= 1; x++) {
                    float2 offset = float2(x, y);
                    float2 cell_id = id + offset;

                    // Use improved modulus function that avoids integer operations
                    bool isCellOdd = shouldRenderCell(cell_id);

                    // Skip cells based on checkerboard pattern for spacing
                    if (!isCellOdd) {
                        questionSamples[sampleIndex] = fixed4(0, 0, 0, 0);
                        questionWeights[sampleIndex] = 0.0;
                        sampleIndex++;
                        continue;
                    }

                    // Get random values for this cell - add large offset to avoid negative hash inputs
                    float2 hashInput = cell_id + float2(10000.0, 10000.0);
                    float2 random = hash22(hashInput);

                    // Position within the cell - ensure they don't get too close to the edges
                    float margin = lerp(0.1, 0.4, _Spacing);
                    float2 pos = random * (1.0 - 2.0 * margin) + margin;

                    // Position relative to this cell's question mark
                    float2 rel_pos = grid_uv - offset - pos;

                    // Random rotation angle
                    float rot_angle = radians(random.y * _RotationVariance);

                    // Apply rotation
                    float2 rotated_uv = mul(rot2D(rot_angle), rel_pos);

                    // Scale for question mark size
                    rotated_uv /= _Scale;

                    // Check if we're within the question mark area
                    float dist = length(rotated_uv);
                    if (dist < 0.8) {
                        // Convert to texture coords (0-1 range)
                        float2 tex_uv = rotated_uv * 0.5 + 0.5;

                        // Only sample if within texture bounds
                        if (all(tex_uv >= 0.0) && all(tex_uv <= 1.0)) {
                            // Sample question mark texture with mipmapping to reduce flickering
                            fixed4 question = tex2Dlod(_MainTex, float4(tex_uv, 0, 0));

                            // Calculate weight based on distance for smoother blending
                            float weight = saturate(1.0 - dist / 1.4);
                            weight = smoothstep(0.0, 1.0, weight); // Smooth the weight

                            questionSamples[sampleIndex] = question;
                            questionWeights[sampleIndex] = weight;
                        } else {
                            questionSamples[sampleIndex] = fixed4(0, 0, 0, 0);
                            questionWeights[sampleIndex] = 0.0;
                        }
                    } else {
                        questionSamples[sampleIndex] = fixed4(0, 0, 0, 0);
                        questionWeights[sampleIndex] = 0.0;
                    }
                    sampleIndex++;
                }
            }

            // Blend all samples outside the loop to avoid gradient warnings
            for (int i = 0; i < 9; i++) {
                fixed4 question = questionSamples[i];
                float weight = questionWeights[i];

                if (question.a > 0.1 && weight > 0.0) {
                    float finalAlpha = question.a * weight;
                    col = lerp(col, _QuestionColor * question, finalAlpha);
                }
            }

            // Set output properties
            o.Albedo = col.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = col.a;

            // Apply shadow color based on lighting (modify this based on your shadow needs)
            // We use a separate shadow color property for shaded areas
            float3 lightDir = normalize(UnityWorldSpaceLightDir(IN.worldPos));
            float ndotl = max(0, dot(IN.worldNormal, lightDir));

            // Simple lighting model that blends between shadow color and albedo
            // The closer to 0 ndotl is, the more in shadow the pixel is
            float shadowStrength = 1.0 - ndotl;

            // Apply shadow color as a blend toward the shadow color in dark areas
            o.Albedo = lerp(o.Albedo, _ShadowColor.rgb, shadowStrength * _ShadowColor.a);
            
            // Apply Emission for Dissolve Edge
            if (_DissolveAmount > 0)
            {
                float val = dissolve.r - _DissolveAmount;
                if (val < _DissolveWidth) 
                {
                    o.Emission += _DissolveColor.rgb;
                }
            }
        }
        ENDCG
    }
    FallBack "Standard"
}