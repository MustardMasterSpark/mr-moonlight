// Copyright © Magnetic Arcade. All Rights Reserved.
// ReSharper disable InconsistentNaming

using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace MA.Flora
{
    internal static unsafe class ShaderPropertyId
    {
        // Unity (Per Draw)

        public static int unity_SpecCube0_HDR => PropertyArray.Ref.unity_SpecCube0_HDR;

        // Unity (Per Material)

        public static int unity_BaseColor => PropertyArray.Ref.unity_BaseColor;


        // Unity (Per Instance)

        public static int unity_DOTSInstanceData => PropertyArray.Ref.unity_DOTSInstanceData;
        public static int unity_SHCoefficients => PropertyArray.Ref.unity_SHCoefficients;
        public static int unity_EntityId => PropertyArray.Ref.unity_EntityId;
        public static int unity_ObjectToWorld => PropertyArray.Ref.unity_ObjectToWorld;
        public static int unity_WorldToObject => PropertyArray.Ref.unity_WorldToObject;
        public static int unity_MatrixPreviousM => PropertyArray.Ref.unity_MatrixPreviousM;
        public static int unity_MatrixPreviousMI => PropertyArray.Ref.unity_MatrixPreviousMI;
        public static int unity_LightmapST => PropertyArray.Ref.unity_LightmapST;
        public static int unity_WorldBoundingSphere => PropertyArray.Ref.unity_WorldBoundingSphere;
        public static int unity_RendererBounds_Min => PropertyArray.Ref.unity_RendererBounds_Min;
        public static int unity_RendererBounds_Max => PropertyArray.Ref.unity_RendererBounds_Max;

        // Flora (Per Instance)

        public static int flora_RandomID => PropertyArray.Ref.flora_RandomID;
        public static int flora_VariationColor => PropertyArray.Ref.flora_VariationColor;

        private struct PropertyArray
        {
            public struct PropertyData
            {
                public int unity_BaseColor;
                public int unity_SpecCube0_HDR;

                public int unity_DOTSInstanceData;
                public int unity_SHCoefficients;
                public int unity_EntityId;
                public int unity_ObjectToWorld;
                public int unity_WorldToObject;
                public int unity_MatrixPreviousM;
                public int unity_MatrixPreviousMI;
                public int unity_LightmapST;
                public int unity_WorldBoundingSphere;
                public int unity_RendererBounds_Min;
                public int unity_RendererBounds_Max;

                public int flora_RandomID;
                public int flora_VariationColor;
            }

            private PropertyData* m_PropertyData;

#if UNITY_EDITOR
            [UnityEditor.InitializeOnLoadMethod]
#else
            [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
#endif
            private static void Initialize()
            {
                if (StaticIdentifier.Ref.Data.m_PropertyData == null)
                {
                    PropertyData properties;

                    properties.unity_BaseColor = Shader.PropertyToID("unity_BaseColor");
                    properties.unity_SpecCube0_HDR = Shader.PropertyToID("unity_SpecCube0_HDR");

                    properties.unity_DOTSInstanceData = Shader.PropertyToID("unity_DOTSInstanceData");
                    properties.unity_SHCoefficients = Shader.PropertyToID("unity_SHCoefficients");
                    properties.unity_EntityId = Shader.PropertyToID("unity_EntityId");
                    properties.unity_ObjectToWorld = Shader.PropertyToID("unity_ObjectToWorld");
                    properties.unity_WorldToObject = Shader.PropertyToID("unity_WorldToObject");
                    properties.unity_MatrixPreviousM = Shader.PropertyToID("unity_MatrixPreviousM");
                    properties.unity_MatrixPreviousMI = Shader.PropertyToID("unity_MatrixPreviousMI");
                    properties.unity_LightmapST = Shader.PropertyToID("unity_LightmapST");
                    properties.unity_WorldBoundingSphere = Shader.PropertyToID("unity_WorldBoundingSphere");
                    properties.unity_RendererBounds_Min = Shader.PropertyToID("unity_RendererBounds_Min");
                    properties.unity_RendererBounds_Max = Shader.PropertyToID("unity_RendererBounds_Max");

                    properties.flora_RandomID = Shader.PropertyToID("flora_RandomID");
                    properties.flora_VariationColor = Shader.PropertyToID("flora_VariationColor");

                    StaticIdentifier.Ref.Data.m_PropertyData = (PropertyData*)UnsafeUtility.Malloc(sizeof(PropertyData), 16, Allocator.Persistent);
                    *StaticIdentifier.Ref.Data.m_PropertyData = properties;

                    void Shutdown()
                    {
                        UnsafeUtility.Free(StaticIdentifier.Ref.Data.m_PropertyData, Allocator.Persistent);
                        StaticIdentifier.Ref.Data.m_PropertyData = null;
                    }

                    AppDomain.CurrentDomain.DomainUnload += (_, _) => Shutdown();
                    AppDomain.CurrentDomain.ProcessExit += (_, _) => Shutdown();
                }
            }

            private sealed class StaticIdentifier
            {
                internal static readonly SharedStatic<PropertyArray> Ref = SharedStatic<PropertyArray>.GetOrCreate<StaticIdentifier>();
            }

            public static ref PropertyData Ref => ref *StaticIdentifier.Ref.Data.m_PropertyData;
        }
    }
}
