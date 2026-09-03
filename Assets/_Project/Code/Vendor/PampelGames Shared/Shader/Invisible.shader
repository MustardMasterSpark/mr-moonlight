Shader "PampelGames/Shared/Invisible"  
{  
    SubShader  
    {  
        Pass  
        {  
            ZWrite Off  
            Blend SrcAlpha OneMinusSrcAlpha            ColorMask 0  
        }  
    }
}