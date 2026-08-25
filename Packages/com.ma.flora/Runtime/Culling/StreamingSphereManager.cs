// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace MA.Flora
{
    internal struct StreamingSphereManager : IDisposable
    {
        public struct StreamingIndex
        {
            public bool Valid;
            public int LastUsedFrameIndex;
            public EntityObjectRef<Camera> Camera;
        }

        private UnsafeHashMap<EntityObjectRef<Camera>, int> m_StreamingSphereIndexByCamera;
        private NativeList<StreamingIndex> m_Slots;
        private NativeList<BoundingSphere> m_StreamingSpheres;
        private NativeList<ushort> m_StreamingSphereFixedMoveDistance;
        private GraphicsBufferRef m_StreamingSphereBuffer;
        private int m_FrameIndex;

        public NativeArray<BoundingSphere> StreamingSpheres => m_StreamingSpheres.AsArray();

        public void Initialize()
        {
            m_StreamingSphereIndexByCamera = new UnsafeHashMap<EntityObjectRef<Camera>, int>(4, Allocator.Persistent);
            m_Slots = new NativeList<StreamingIndex>(4, Allocator.Persistent);
            m_StreamingSpheres = new NativeList<BoundingSphere>(4, Allocator.Persistent);
            m_StreamingSphereFixedMoveDistance = new NativeList<ushort>(4, Allocator.Persistent);
            m_StreamingSphereBuffer = new GraphicsBufferRef(GraphicsBuffer.Target.Raw, 4, UnsafeUtility.SizeOf<float4>(), "Flora.StreamingSpheres");
            m_FrameIndex = 0;
        }

        public void Dispose()
        {
            m_StreamingSphereIndexByCamera.Dispose();
            m_Slots.Dispose();
            m_StreamingSpheres.Dispose();
            m_StreamingSphereFixedMoveDistance.Dispose();
            m_StreamingSphereBuffer.Dispose();
        }

        public void Update()
        {
            for (int i = m_Slots.Length - 1; i >= 0; --i)
            {
                var slot = m_Slots[i];
                if (!slot.Valid)
                    continue;

                if ((m_FrameIndex - slot.LastUsedFrameIndex) >= 8)
                {
                    RemoveCamera(slot.Camera);
                }
            }

            m_FrameIndex++;
        }

        public void UpdateCamera(Camera camera)
        {
            var sphere = new BoundingSphere(camera.transform.position, camera.farClipPlane);

            if (!m_StreamingSphereIndexByCamera.TryGetValue(camera, out int listIndex))
            {
                listIndex = m_Slots.Length;
                m_StreamingSphereIndexByCamera.Add(camera, listIndex);
                m_Slots.Add(new StreamingIndex
                {
                    Valid = true,
                    LastUsedFrameIndex = m_FrameIndex,
                    Camera = camera
                });
                m_StreamingSpheres.Add(sphere);
                m_StreamingSphereFixedMoveDistance.Add(0xffff);
            }
            else
            {
                var slot = m_Slots[listIndex];
                slot.LastUsedFrameIndex = m_FrameIndex;
                m_Slots[listIndex] = slot;

                var previousSphere = m_StreamingSpheres[listIndex];
                var distance = math.distance(sphere.position, previousSphere.position);
                var fixedMoveDistance = (ushort)math.clamp((int)math.ceil(distance * 100.0f), 0, 0xffff);
                m_StreamingSphereFixedMoveDistance[listIndex] = fixedMoveDistance;
                m_StreamingSpheres[listIndex] = sphere;
            }
        }

        private void RemoveCamera(EntityObjectRef<Camera> camera)
        {
            if (m_StreamingSphereIndexByCamera.TryGetValue(camera, out int index))
            {
                m_Slots.RemoveAtSwapBack(index);
                m_StreamingSpheres.RemoveAtSwapBack(index);
                m_StreamingSphereFixedMoveDistance.RemoveAtSwapBack(index);

                if (index < m_StreamingSpheres.Length)
                {
                    var movedSlot = m_Slots[index];
                    m_StreamingSphereIndexByCamera[movedSlot.Camera] = index;
                }

                m_StreamingSphereIndexByCamera.Remove(camera);
            }
        }
    }
}
