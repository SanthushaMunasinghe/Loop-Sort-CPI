Shader "Custom/CubeStencilWriter"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry-1" }
        
        // Don't write to color or depth, only stencil
        ColorMask 0
        ZWrite Off
        
        Stencil
        {
            Ref 64
            ReadMask 64
            WriteMask 64
            Comp Always
            Pass Replace
        }
        
        // Render back faces to mark the interior
        Pass
        {
            Cull Front
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
            };
            
            struct v2f
            {
                float4 pos : SV_POSITION;
            };
            
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }
            
            fixed4 frag(v2f i) : SV_Target
            {
                return fixed4(0, 0, 0, 0);
            }
            ENDCG
        }
    }
}
