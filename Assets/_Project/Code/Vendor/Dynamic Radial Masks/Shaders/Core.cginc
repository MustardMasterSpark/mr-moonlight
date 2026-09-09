// Dynamic Radial Masks <https://u3d.as/1w0H>
// Copyright (c) Amazing Assets <https://amazingassets.world>
 

#ifndef SHADER_EXTENTIONS_DYNAMIC_RADIAL_MASKS
#define SHADER_EXTENTIONS_DYNAMIC_RADIAL_MASKS

//DATA1
#define DYNAMIC_RADIAL_MASKS_READ_POSITION(a, i) a##_DATA1[i].xyz
#define DYNAMIC_RADIAL_MASKS_READ_RADIUS(a, i)   a##_DATA1[i].w

//DATA2
#define DYNAMIC_RADIAL_MASKS_READ_INTENSITY(a, i)      a##_DATA2[i].x
#define DYNAMIC_RADIAL_MASKS_READ_EDGE_SIZE(a, i)      a##_DATA2[i].y
#define DYNAMIC_RADIAL_MASKS_READ_SMOOTH(a, i)         a##_DATA2[i].z
#define DYNAMIC_RADIAL_MASKS_READ_NOISE_STRENGTH(a, i) a##_DATA2[i].w

//DATA3
#define DYNAMIC_RADIAL_MASKS_READ_RING_COUNT(a, i) a##_DATA3[i].x
#define DYNAMIC_RADIAL_MASKS_READ_PHASE(a, i)      a##_DATA3[i].y
#define DYNAMIC_RADIAL_MASKS_READ_FREQUENCY(a, i)  a##_DATA3[i].z


inline float ShaderExtensions_DynamicRadialMasks_Torus_Advanced(float3 vertexWorldPosition, float3 maskPosition, float radius, float intensity, float edgeSize, float smooth, float noise)
{

	//Distance
	float d = distance(maskPosition, vertexWorldPosition);

	//Noise
	edgeSize += noise;


	float2 r = max(float2(0, 0), float2(radius - d, d - radius) + edgeSize) / edgeSize;

	float shape = saturate(r.x * r.y);	

	//Smooth			
	shape = pow(shape, smooth + 0.01);	//BUG: does not work if do not add 0.01. Hmmmm

	//Fade
	shape *= intensity;


	return shape;
}

inline float ShaderExtensions_DynamicRadialMasks_Torus_Simple(float3 vertexWorldPosition, float3 maskPosition, float radius, float intensity, float edgeSize)
{

	//Distance
	float d = distance(maskPosition, vertexWorldPosition);

	//Noise
	//-no


	float2 r = max(float2(0, 0), float2(radius - d, d - radius) + edgeSize) / edgeSize;

	float shape = saturate(r.x * r.y);	

	//Smooth			
	shape *= shape;

	//Fade
	shape *= intensity;


	return shape;
}

inline float ShaderExtensions_DynamicRadialMasks_Tube_Advanced(float3 vertexWorldPosition, float3 maskPosition, float radius, float intensity, float edgeSize, float noise)
{

	//Distance
	float d = distance(maskPosition, vertexWorldPosition);

	//Noise
	edgeSize += noise;


	float2 r = max(float2(0, 0), float2(radius - d, d - radius) + edgeSize) / edgeSize;

	float shape = saturate(r.x * r.y);
	shape = saturate(shape * 50);	

	//Fade
	shape *= intensity;


	return shape;
}

inline float ShaderExtensions_DynamicRadialMasks_Tube_Simple(float3 vertexWorldPosition, float3 maskPosition, float radius, float intensity, float edgeSize)
{

	//Distance
	float d = distance(maskPosition, vertexWorldPosition);
	
	//Noise
	//-no


	float2 r = max(float2(0, 0), float2(radius - d, d - radius) + edgeSize) / edgeSize;

	float shape = saturate(r.x * r.y);
	shape = saturate(shape * 50);	

	//Fade
	shape *= intensity;


	return shape;
}

inline float ShaderExtensions_DynamicRadialMasks_HeightField_Advanced(float3 vertexWorldPosition, float3 maskPosition, float radius, float intensity, float edgeSize, float smooth, float noise)
{

	//Distance
	float d = distance(maskPosition, vertexWorldPosition);

	//Noise
	radius += noise;

	
    float shape = 1 - saturate(max(0, d - radius + edgeSize) / edgeSize);
	
	//Smooth
	shape = pow(shape, smooth + 0.01);	//BUG: does not work if do not add 0.01. Hmmmm

	//Fade
	shape *= intensity;


	return shape;
}

inline float ShaderExtensions_DynamicRadialMasks_HeightField_Simple(float3 vertexWorldPosition, float3 maskPosition, float radius, float intensity, float edgeSize)
{

	//Distance
	float d = distance(maskPosition, vertexWorldPosition);

	//Noise
	//-no

	
    float shape = 1 - saturate(max(0, d - radius + edgeSize) / edgeSize);
	
	//Smooth
	shape *= shape;

	//Fade
	shape *= intensity;


	return shape;
}

inline float ShaderExtensions_DynamicRadialMasks_Dot_Advanced(float3 vertexWorldPosition, float3 maskPosition, float radius, float intensity, float noise)
{

	//Distance
	float d = distance(maskPosition, vertexWorldPosition);

	//Noise
	d += noise;

	//Shape
	float shape = d < radius ? 1 : 0;

	//Fade
	shape *= intensity;


	return shape;
}

inline float ShaderExtensions_DynamicRadialMasks_Dot_Simple(float3 vertexWorldPosition, float3 maskPosition, float radius, float intensity)
{

	//Distance
	float d = distance(maskPosition, vertexWorldPosition);

	//Noise
	//-no

	//Shape
	float shape = d < radius ? 1 : 0;

	//Fade
	shape *= intensity;


	return shape;
}

inline float ShaderExtensions_DynamicRadialMasks_Shockwave_Advanced(float3 vertexWorldPosition, float3 maskPosition, float radius, float intensity, float edgeSize, float smooth, float noise)
{

	//Distance
	float d = distance(maskPosition, vertexWorldPosition);

	//Noise
	edgeSize += noise;


	float r = saturate(max(0, d - radius + edgeSize) / edgeSize);

	float shape = frac(r);

	//Smooth
	shape = pow(shape, smooth + 0.01);	//BUG: does not work if do not add 0.01. Hmmmm

	//Fade
	shape *= intensity;


	return shape;
}

inline float ShaderExtensions_DynamicRadialMasks_Shockwave_Simple(float3 vertexWorldPosition, float3 maskPosition, float radius, float intensity, float edgeSize)
{

	//Distance
	float d = distance(maskPosition, vertexWorldPosition);

	//Noise
	//-no


	float r = saturate(max(0, d - radius + edgeSize) / edgeSize);

	float shape = frac(r);

	//Smooth
	shape *= shape;

	//Fade
	shape *= intensity;


	return shape;
}

inline float ShaderExtensions_DynamicRadialMasks_Sonar_Advanced(float3 vertexWorldPosition, float3 maskPosition, float radius, float intensity, float edgeSize, int ringCount, float smooth, float noise)
{

	//Distance
	float d = distance(maskPosition, vertexWorldPosition);

	//Noise
	d += noise;


	float r = saturate(max(0, d - radius + edgeSize) / edgeSize);

	float shape = frac(r * ringCount);	
	
	//Smooth
	shape = pow(shape, smooth + 0.01);	//BUG: does not work if do not add 0.01. Hmmmm

	//Fade
	shape *= intensity;


	return shape;
}

inline float ShaderExtensions_DynamicRadialMasks_Sonar_Simple(float3 vertexWorldPosition, float3 maskPosition, float radius, float intensity, float edgeSize, int ringCount)
{

	//Distance
	float d = distance(maskPosition, vertexWorldPosition);

	//Noise
	//-no


	float r = saturate(max(0, d - radius + edgeSize) / edgeSize);

	float shape = frac(r * ringCount);	
	
	//Smooth
	shape *= shape;

	//Fade
	shape *= intensity;


	return shape;
}

inline float ShaderExtensions_DynamicRadialMasks_Rings_Advanced(float3 vertexWorldPosition, float3 maskPosition, float radius, float intensity, float edgeSize, int ringCount, float smooth, float noise)
{

	//Distance
	float d = distance(maskPosition, vertexWorldPosition);

	//Noise
	d += noise;


	float r = saturate(max(0, d - radius + edgeSize) / edgeSize);

	float shape = frac(r * ringCount);	
	shape = sin(shape * 3.14159);
	
	//Smooth
	shape = pow(shape, smooth + 0.01);	//BUG: does not work if do not add 0.01. Hmmmm

	//Fade
	shape *= intensity;


	return shape;
}

inline float ShaderExtensions_DynamicRadialMasks_Rings_Simple(float3 vertexWorldPosition, float3 maskPosition, float radius, float intensity, float edgeSize, int ringCount)
{

	//Distance
	float d = distance(maskPosition, vertexWorldPosition);

	//Noise
	//-no


	float r = saturate(max(0, d - radius + edgeSize) / edgeSize);

	float shape = frac(r * ringCount);	
	shape = sin(shape * 3.14159);
	
	//Smooth
	shape *= shape;

	//Fade
	shape *= intensity;


	return shape;
}

inline float ShaderExtensions_DynamicRadialMasks_Ripple_Advanced(float3 vertexWorldPosition, float3 maskPosition, float radius, float intensity, float phase, float frequency, float smooth, float noise)
{	

	//Distance
    float d = distance(maskPosition, vertexWorldPosition);

	//Noise
	phase += noise;   


    float shape = sin(d * frequency - phase);
	

	// //[-1; 1] -> [0, 1]
	shape = (shape + 1) / 2;

	//Smooth
	shape = pow(shape, smooth + 0.01);	//BUG: does not work if do not add 0.01. Hmmmm


    //Slope
	float r = saturate(max(0, radius - d) / radius);
    shape *= r / (d + 1);

	shape = (d < radius) ? shape : 0;

    //Fade
    shape *= intensity;
 

    return shape;
}

inline float ShaderExtensions_DynamicRadialMasks_Ripple_Simple(float3 vertexWorldPosition, float3 maskPosition, float radius, float intensity, float phase, float frequency)
{	

	//Distance
    float d = distance(maskPosition, vertexWorldPosition);

	//Noise
	//-no


    float shape = sin(d * frequency - phase);
	

	// //[-1; 1] -> [0, 1]
	shape = (shape + 1) / 2;

	//Smooth
	shape *= shape;


    //Slope
	float r = saturate(max(0, radius - d) / radius);
    shape *= r / (d + 1);

	shape = (d < radius) ? shape : 0;

    //Fade
    shape *= intensity;
 

    return shape;
}


#endif
