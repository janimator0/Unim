// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

// © 2017 ARTISANIMATION.COM ALL RIGHTS RESERVED

Shader "Unim/Standard" {
	Properties{
        _MainTex ("Color (RGB) Alpha (A)", 2D) = "white"
	}
	SubShader{
		Tags { "Queue"="Transparent" "RenderType"="Transparent" }
		LOD 100
		ZWrite Off
		Cull Off
		Blend SrcAlpha OneMinusSrcAlpha 
		
		Pass{
			CGPROGRAM

			#pragma vertex   vert
			#pragma fragment frag

			sampler2D _MainTex;

			struct vertIn {
				float4 vertex : POSITION;
				float4 uv : TEXCOORD0;
				float4 vertexColor : COLOR;
				float4 vertexColorAddRG : TEXCOORD6;
				float4 vertexColorAddBA : TEXCOORD7;
			};
			
			struct vert2frag {
				float4 pos : SV_POSITION;
				float4 uv : TEXCOORD0;
				fixed4 color : COLOR;
				float4 colorAdd : TEXCOORD6; 
			};

			vert2frag vert(vertIn v) {
				vert2frag o;

				o.pos = UnityObjectToClipPos(v.vertex);
				o.color = v.vertexColor;
				o.colorAdd.r = v.vertexColorAddRG[0];
				o.colorAdd.g = v.vertexColorAddRG[1];
				o.colorAdd.b = v.vertexColorAddBA[0];
				o.colorAdd.a = v.vertexColorAddBA[1];
				o.uv = v.uv;
				return o;
			}
	
			float4 frag(vert2frag i)  : SV_Target
			{
			    fixed4 fCol = tex2D(_MainTex, i.uv);
				fixed4 col = fCol * i.color + i.colorAdd;
				//col.a = clamp(col.a, 0, fCol.a);
				col.a = clamp(col.a, 0, fCol.a);
				return col;
			}
			
			ENDCG
		}
	}
	//FallBack "Diffuse"
}