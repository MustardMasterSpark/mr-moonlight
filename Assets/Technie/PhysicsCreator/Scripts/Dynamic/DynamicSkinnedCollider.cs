
// Burst and Collections are *required*
#if TCC_HAS_BURST && TCC_HAS_COLLECTIONS
#define TCC_ENABLE_DYN_COLLIDER
#endif

// Unity 6.1 means we can use mesh LOD
#if UNITY_6000_1_OR_NEWER
#define TCC_USE_MESH_LOD
#endif

// Unity 6.3 means we can use memory labels and entity id
#if UNITY_6000_3_OR_NEWER
#define TCC_USE_ENTITY_ID
#endif

#if UNITY_6000_3_OR_NEWER && TCC_HAS_COLLECTIONS
#define TCC_USE_MEMORY_LABELS
#endif

using System.Runtime.InteropServices;
using UnityEngine;
using Unity.Jobs;
using UnityEngine.Rendering;
using UnityEngine.Profiling;

#if TCC_HAS_BURST
using Unity.Burst;
#endif

#if TCC_HAS_COLLECTIONS
using Unity.Collections;
#endif

#if UNITY_6000_0_OR_NEWER
using PhysicsMaterial = UnityEngine.PhysicsMaterial;
#else
using PhysicsMaterial = UnityEngine.PhysicMaterial;
#endif

/*

	John thoughts June 2026:
		
		- We're caching the mesh data, but caching it per instance. Would be nice if we could cache it once per source mesh and share it

		- Running as convex works, and is faster to bake the collision mesh
			- BUT unity spams the console with 'exceeded the maximum polygon limit' warnings, which is horrible

		- need to properly deal with becoming inactive/active and disabled/enabled if jobs are already in flight
		
		- If using LOD, we should reindex so we don't process lots of data unnessesarily
		- If *not* using LOD, we should share data from the Mesh directly
			eg. Mesh.GetBindposes() which returns a NativeArray which is still owned by the Mesh
		
		- prepare skinning job - seems to allocate memory

		- make sure if we set lod=0 then things work nicely
		- make sure things work if there are no lods in the source mesh
		
		- upgrade to using BoneWeight1 structure ( https://docs.unity3d.com/6000.4/Documentation/Manual/mesh-vertex-data.html )

		LOD details for Ellen (using MeshLOD on import)
		------------------------------------------------
		Base mesh (ie. no lods):		
			18.8k vertices total
			30k triangles total (90k indices)
			6 submeshes
				Submesh 0:
					2.2k triangles
				Submesh 1:
					15.7k triangles
		LOD 1:
			15k triangles total
		LOD 2:
			8k triangles total
		
*/

namespace Technie.PhysicsCreator.Dynamic
{
	[StructLayout(LayoutKind.Sequential)]
	public struct BlendIndices
	{
		public uint boneIndex0;
		public uint boneIndex1;
		public uint boneIndex2;
		public uint boneIndex3;
	}

#if TCC_HAS_BURST
	[BurstCompile]
#endif
	public class DynamicSkinnedCollider : MonoBehaviour
	{
		public enum UpdateBehaviour
		{
			Continuous,
			OnDemand,
			Throttled
			// TODO: Also an option for 'at start' for posed statues etc.?
		}

#if TCC_USE_MEMORY_LABELS
		static readonly MemoryLabel memLabel = new MemoryLabel("Technie", "DynamicSkinnedCollider", Allocator.Persistent);
#endif

		public SkinnedMeshRenderer skinnedRenderer;

		[Tooltip("Continuous - updates every frame.\nOnDemand - only updates when a custom script tells it to.\nThrottled - waits between updates so it runs at a lower framerate")]
		public UpdateBehaviour updateBehaviour = UpdateBehaviour.Continuous;

		[Tooltip("How many frame intervals to leave between each mesh update when in Throttled update mode")]
		public int throttledInterval = 0;
		
		[Tooltip("Select a mesh LOD level - level 0 is highest detail")]
		public int lod = 0; // which mesh lod to use for physics - requires lod generation in model import settings

		public bool convex = false;

		[Tooltip("If set then the collider will not be solid but instead fire trigger events")]
		public bool isTrigger = false;

		public PhysicsMaterial physicsMaterial;
		public MeshColliderCookingOptions cookingOptions = MeshColliderCookingOptions.None;

		//[Header("Layer overrides")]
		public int layerOverridePriority = 0;
		public LayerMask includeLayers;
		public LayerMask excludeLayers;

#if TCC_ENABLE_DYN_COLLIDER // Chop out the internals, but keep the properties (above) so they remain persisted even if we don't have the correct packages

		// Internal State

		private enum Mode
		{
			Uninitialised,
			Reindexing,
			Idle,
			DoingSkinning,
			DoingPhysics,
		}

		private Mode mode = Mode.Uninitialised;

		private Mesh srcMesh;
		private Mesh simplifiedMesh;
		private Mesh colliderMesh;

		private MeshCollider targetCollider;

		private ReindexLodsJob reindexJob;
		private JobHandle reindexHandle;
		private CalcPoseMatricesJob calcPoseJob;
		private BakeAnimationMeshJob bakeAnimJob;
		private JobHandle bakeAnimHandle;
		private BakePhysicsMeshJob bakePhysicsJob;
		private JobHandle bakePhysicsHandle;

		// Cached mesh data
		private NativeArray<Vector3> srcVertices;
		private NativeArray<Matrix4x4> bindPosesData;
		private NativeArray<BoneWeight> boneWeightsData;
		private NativeArray<Matrix4x4> localToWorldMatrix;

		private NativeArray<Matrix4x4> currentPoseMatrices;
		private NativeArray<Vector3> dstVertices;

		private Mesh.MeshDataArray simplifiedDataArr;

		private Transform[] bones;

		private int throttleFramesRemaining;

		void Start()
		{
			Profiler.BeginSample("DynamicSkinnedCollider extract mesh data");

			this.srcMesh = skinnedRenderer.sharedMesh;
			this.bones = skinnedRenderer.bones;

			simplifiedMesh = new Mesh();
			simplifiedMesh.name = srcMesh.name + " (simplified)";

			colliderMesh = new Mesh();
			colliderMesh.name = srcMesh.name + " (collision)";

			Profiler.BeginSample("Create MeshCollider");
			{
				// Create the mesh collider we'll output to
				this.targetCollider = this.gameObject.AddComponent<MeshCollider>();
				this.targetCollider.sharedMaterial = physicsMaterial;
				this.targetCollider.cookingOptions = cookingOptions;
				this.targetCollider.convex = convex;
				this.targetCollider.isTrigger = isTrigger;
				this.targetCollider.layerOverridePriority = layerOverridePriority;
				this.targetCollider.includeLayers = layerOverridePriority;
				this.targetCollider.excludeLayers = excludeLayers;
				// NB: Do *not* set .sharedMesh here - it will cause an internal PhysX mesh bake. Instead wait until we've done that ourselves off the main thread.
			}
			Profiler.EndSample();

			Profiler.BeginSample("Create native data");
			this.bindPosesData = srcMesh.GetBindposes();
			this.localToWorldMatrix = CreatePersistentNativeArray<Matrix4x4>(srcMesh.bindposeCount);
			this.currentPoseMatrices = CreatePersistentNativeArray<Matrix4x4>(srcMesh.bindposeCount);
			Profiler.EndSample();

			
			// Extract mesh data

			reindexJob = new ReindexLodsJob()
			{
#if TCC_USE_MESH_LOD
				targetLod = Mathf.Min(this.lod, srcMesh.lodCount-1), // TODO: Check for off-by-one errors here
#endif
				srcDataArray = Mesh.AcquireReadOnlyMeshData(srcMesh),
				destMesh = Mesh.AllocateWritableMeshData(1),
			};
			reindexHandle = reindexJob.Schedule();

			Profiler.EndSample();

			mode = Mode.Reindexing;
		}

		private static NativeArray<T> CreatePersistentNativeArray<T>(int count) where T : struct
		{
#if TCC_USE_MEMORY_LABELS
			return new NativeArray<T>(count, memLabel);
#else
			return new NativeArray<T>(count, Allocator.Persistent);
#endif
		}

		private void OnDestroy()
		{
			// Need to complete any outstanding jobs before we dispose of our native collections
			reindexHandle.Complete();
			bakeAnimHandle.Complete();
			bakePhysicsHandle.Complete();

			simplifiedDataArr.Dispose();

			if (localToWorldMatrix.IsCreated)
				localToWorldMatrix.Dispose();

			if (currentPoseMatrices.IsCreated)
				currentPoseMatrices.Dispose();

			if (dstVertices.IsCreated)
				dstVertices.Dispose();
		}

		[ContextMenu("Regen Collider")]
		public void RegenColliderAsync()
		{
			Tick(true);
		}

		private void FixedUpdate()
		{
			// We can start if we're in Continuous mode, or if Throttled and the throttle interval has finished
			bool canStart = (mode == Mode.Idle
							&& (updateBehaviour == UpdateBehaviour.Continuous || (updateBehaviour == UpdateBehaviour.Throttled && throttleFramesRemaining == 0)));

			Tick(canStart);
		}

		void Update()
		{
			if (mode == Mode.Idle && updateBehaviour == UpdateBehaviour.Throttled && throttleFramesRemaining > 0)
				throttleFramesRemaining--;

			Tick(false);
		}

		private void LateUpdate()
		{
			Tick(false);
		}

		private void Tick(bool allowStart)
		{
			if (mode == Mode.Uninitialised)
			{

			}
			else if (mode == Mode.Reindexing)
			{
				if (reindexHandle.IsCompleted)
				{
					reindexHandle.Complete();
					reindexJob.srcDataArray.Dispose();

					Mesh.ApplyAndDisposeWritableMeshData(reindexJob.destMesh, simplifiedMesh); // TODO: Pass in update flags here
					simplifiedMesh.bounds = srcMesh.bounds;

					// Allocate working buffers for animation job

					this.simplifiedDataArr = Mesh.AcquireReadOnlyMeshData(simplifiedMesh);
					Mesh.MeshData tmpMeshData = simplifiedDataArr[0];
					this.srcVertices = tmpMeshData.GetVertexData<Vector3>(0); // TODO: Highly suspect! gets *all* of stream 0, which might not just be positions, Maybe .GetVertices is more appropriate?
					int boneWeightId = tmpMeshData.GetVertexAttributeStream(VertexAttribute.BlendWeight);
					this.boneWeightsData = tmpMeshData.GetVertexData<BoneWeight>(boneWeightId);

					// Make a blank collision mesh with just vertex positions (matching the vertex count of the simplified mesh)
					VertexAttributeDescriptor[] desc = new VertexAttributeDescriptor[]
					{
					new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0)
					};
					colliderMesh.SetVertexBufferParams(simplifiedMesh.vertexCount, desc);
					colliderMesh.SetIndices(simplifiedMesh.triangles, MeshTopology.Triangles, 0); // TODO: Optimise this copy
					colliderMesh.bounds = simplifiedMesh.bounds;

					dstVertices = CreatePersistentNativeArray<Vector3>(simplifiedMesh.vertexCount);


					// Debug visulisation
					//GameObject tmp = new GameObject("Simplified collider mesh");
					//tmp.AddComponent<MeshFilter>().sharedMesh = simplifiedMesh;
					//tmp.AddComponent<MeshRenderer>();

					mode = Mode.Idle;
				}
			}
			else if (mode == Mode.Idle && allowStart)
			{
				Profiler.BeginSample("Prepare skinning job");

				Profiler.BeginSample("Get bones");
				// Regen the bone matrices for the current animation pose
				for (int i = 0; i < bones.Length; i++)
				{
					localToWorldMatrix[i] = bones[i].localToWorldMatrix;
				}
				Profiler.EndSample();

				calcPoseJob = new CalcPoseMatricesJob()
				{
					numBones = bindPosesData.Length,
					localToWorldMatrix = this.localToWorldMatrix,
					bindPoses = this.bindPosesData,
					worldToComponentMatrix = this.transform.worldToLocalMatrix,
					currentPoseMatrices = this.currentPoseMatrices
				};
				JobHandle poseHandle = calcPoseJob.Schedule();

				bakeAnimJob = new BakeAnimationMeshJob()
				{
					numBones = bindPosesData.Length,
					srcVertices = this.srcVertices,
					weights = this.boneWeightsData,

					currentPoseMatrices = this.currentPoseMatrices,
					dstVertices = this.dstVertices
				};
				bakeAnimHandle = bakeAnimJob.ScheduleParallel(dstVertices.Length, 512, poseHandle);

				mode = Mode.DoingSkinning;
				Profiler.EndSample();
			}
			else if (mode == Mode.DoingSkinning)
			{
				if (bakeAnimHandle.IsCompleted)
				{
					Profiler.BeginSample("Apply vertices to mesh");

					bakeAnimHandle.Complete();
					colliderMesh.SetVertexBufferData(bakeAnimJob.dstVertices, 0, 0, bakeAnimJob.dstVertices.Length); // TODO: Pass in sensible MeshUpdateFlags

					bakePhysicsJob = new BakePhysicsMeshJob()
					{
#if TCC_USE_ENTITY_ID
						meshId = colliderMesh.GetEntityId(),
#else
						meshId = colliderMesh.GetInstanceID(),
#endif
						convex = targetCollider.convex,
						cookingOptions = targetCollider.cookingOptions,
					};

					bakePhysicsHandle = bakePhysicsJob.Schedule();

					mode = Mode.DoingPhysics;

					Profiler.EndSample();
				}
			}
			else if (mode == Mode.DoingPhysics)
			{
				if (bakePhysicsHandle.IsCompleted)
				{
					Profiler.BeginSample("Apply mesh to collider");

					bakePhysicsHandle.Complete();
					targetCollider.sharedMesh = colliderMesh;

					throttleFramesRemaining = this.throttledInterval;
					
					mode = Mode.Idle;

					Profiler.EndSample();
				}
			}
		}

		// Take a source mesh and target lod, and create a new mesh with just the vertices+skinning data
		[BurstCompile]
		public struct ReindexLodsJob : IJob
		{
#if TCC_USE_MESH_LOD
		[ReadOnly]
		public int targetLod;
#endif

			[ReadOnly]
			public Mesh.MeshDataArray srcDataArray;

			public Mesh.MeshDataArray destMesh;

			public void Execute()
			{
				Mesh.MeshData srcMeshData = srcDataArray[0];

				uint numCompactedIndices = 0;
				for (int s = 0; s < srcMeshData.subMeshCount; s++)
				{
#if TCC_USE_MESH_LOD
					MeshLodRange range = srcMeshData.GetLod(s, targetLod);
					numCompactedIndices += range.indexCount;
#else
					numCompactedIndices += (uint)srcMeshData.GetSubMesh(s).indexCount;
#endif
				}

				NativeArray<ushort> newIndices = new NativeArray<ushort>((int)numCompactedIndices, Allocator.Temp);
#if TCC_USE_MESH_LOD
				NativeHashMap<ushort, ushort> reindexMap = new NativeHashMap<ushort, ushort>((int)numCompactedIndices / 3, Allocator.Temp);

				int nextFreeIndex = 0;
				int writeCursor = 0;

				for (int s = 0; s < srcMeshData.subMeshCount; s++)
				{
					MeshLodRange range = srcMeshData.GetLod(s, targetLod);
					NativeArray<ushort> subMeshIndices = new NativeArray<ushort>((int)range.indexCount, Allocator.Temp);
					srcMeshData.GetIndices(subMeshIndices, s, targetLod, true);

					for (uint i = range.indexStart; i < range.indexStart + range.indexCount; i++)
					{
						ushort existingIndex = subMeshIndices[(int)(i - range.indexStart)];

						ushort newIndex;
						if (!reindexMap.TryGetValue(existingIndex, out newIndex))
						{
							newIndex = (ushort)nextFreeIndex++;
							reindexMap.Add(existingIndex, newIndex);
						}
						newIndices[writeCursor++] = newIndex;
					}

					subMeshIndices.Dispose();
				}
				int numCompactedVertices = reindexMap.Count;
#else
				int writeIndex = 0;
				for (int s = 0; s < srcMeshData.subMeshCount; s++)
				{
					SubMeshDescriptor subDesc = srcMeshData.GetSubMesh(s);

					NativeArray<ushort> subIndices = new NativeArray<ushort>(subDesc.indexCount, Allocator.Temp);
					srcMeshData.GetIndices(subIndices, s, false);

					NativeArray<ushort>.Copy(subIndices, 0, newIndices, writeIndex, subIndices.Length);
					writeIndex += subIndices.Length;
				}
				int numCompactedVertices = srcMeshData.vertexCount;
#endif

				NativeArray<Vector3> vertices = new NativeArray<Vector3>(srcMeshData.vertexCount, Allocator.Temp);
				srcMeshData.GetVertices(vertices);
				
				NativeArray<BoneWeight> boneWeights = ExtractBoneWeights(srcMeshData);
#if TCC_USE_MESH_LOD
				// Use the reindex map to extract just the vertex data we want for the simplified mesh
				NativeArray<Vector3> srcVertices = new NativeArray<Vector3>(numCompactedVertices, Allocator.Temp);
				NativeArray<BoneWeight> boneWeightsData = new NativeArray<BoneWeight>(numCompactedVertices, Allocator.Temp);
				NativeKeyValueArrays<ushort, ushort> reindexArray = reindexMap.GetKeyValueArrays(Allocator.Temp);
				for (int i = 0; i < reindexArray.Length; i++)
				{
					ushort key = reindexArray.Keys[i];
					ushort value = reindexArray.Values[i];

					srcVertices[value] = vertices[key];
					boneWeightsData[value] = boneWeights[key];
				}
#else
				// Just use the vertices and bone weights directly
				NativeArray<Vector3> srcVertices = vertices;
				NativeArray<BoneWeight> boneWeightsData = boneWeights;
#endif

				// Populate the mesh data

				Mesh.MeshData destMeshData = destMesh[0];

				NativeArray<VertexAttributeDescriptor> desc = new NativeArray<VertexAttributeDescriptor>(3, Allocator.Temp);
				desc[0] = new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0);
				desc[1] = new VertexAttributeDescriptor(VertexAttribute.BlendWeight, VertexAttributeFormat.Float32, 4, 1);
				desc[2] = new VertexAttributeDescriptor(VertexAttribute.BlendIndices, VertexAttributeFormat.SInt32, 4, 1);
				destMeshData.SetVertexBufferParams(numCompactedVertices, desc);

				NativeArray<Vector3> verts = destMeshData.GetVertexData<Vector3>(destMeshData.GetVertexAttributeStream(VertexAttribute.Position));
				verts.CopyFrom(srcVertices); // TODO: see if we can remove this copy later

				NativeArray<BoneWeight> boneW = destMeshData.GetVertexData<BoneWeight>(destMeshData.GetVertexAttributeStream(VertexAttribute.BlendWeight));
				boneW.CopyFrom(boneWeightsData); // TODO: see if we can remove this copy later

				destMeshData.SetIndexBufferParams((int)numCompactedIndices, IndexFormat.UInt16);
				NativeArray<ushort> ind = destMeshData.GetIndexData<ushort>();
				ind.CopyFrom(newIndices); // TODO: see if we can remove this copy later

				destMeshData.subMeshCount = 1;
				destMeshData.SetSubMesh(0, new SubMeshDescriptor(0, (int)numCompactedIndices));
			}

			private static NativeArray<BoneWeight> ExtractBoneWeights(Mesh.MeshData srcMeshData)
			{
				NativeArray<BoneWeight> boneWeights;

				int boneWeightId = srcMeshData.GetVertexAttributeStream(VertexAttribute.BlendWeight);
				if (boneWeightId >= 0)
				{
					boneWeights = srcMeshData.GetVertexData<BoneWeight>(boneWeightId);
				}
				else
				{
					// Some models *just* have a single BlendIndex per vertex and no blend weights
					// (eg. for robotic characters where every vertex is linked to only one bone)
					// Pull out the blend indices and expand it to a full BoneWeight structure so later code has consistent data to work with
					int blendIndexId = srcMeshData.GetVertexAttributeStream(VertexAttribute.BlendIndices);
					int dim = srcMeshData.GetVertexAttributeDimension(VertexAttribute.BlendIndices);
					VertexAttributeFormat format = srcMeshData.GetVertexAttributeFormat(VertexAttribute.BlendIndices);
					NativeArray<int> blendIndices = srcMeshData.GetVertexData<int>(blendIndexId);

					boneWeights = new NativeArray<BoneWeight>(blendIndices.Length, Allocator.Temp);
					for (int i = 0; i < blendIndices.Length; i++)
					{
						int src = blendIndices[i];
						boneWeights[i] = new BoneWeight()
						{
							boneIndex0 = src,
							boneIndex1 = 0,
							boneIndex2 = 0,
							boneIndex3 = 0,

							weight0 = 1.0f,
							weight1 = 0.0f,
							weight2 = 0.0f,
							weight3 = 0.0f,
						};
					}
				}
				return boneWeights;
			}
		}


		// Calculate per-bone matrices that take the vertices from their bind pose into the current animated pose
		// That means we need to composite:
		//	- the bind pose (ie. where the bone is when modeled in it's T-pose)
		//	- the current local-to-world for the bone in it's animated position
		//	- a world-to-component matrix to take the whole thing from world space into local space (relative to this component)
		[BurstCompile]
		public struct CalcPoseMatricesJob : IJob
		{
			public int numBones;

			[ReadOnly]
			public NativeArray<Matrix4x4> localToWorldMatrix;

			[ReadOnly]
			public NativeArray<Matrix4x4> bindPoses;

			[ReadOnly]
			public Matrix4x4 worldToComponentMatrix;

			[WriteOnly]
			public NativeArray<Matrix4x4> currentPoseMatrices;

			public void Execute()
			{
				for (int i = 0; i < numBones; i++)
				{
					currentPoseMatrices[i] = worldToComponentMatrix * localToWorldMatrix[i] * bindPoses[i];
				}
			}
		}

		// Transform the source vertices (in t-pose) by the current animation matrices to get the actual posed mesh
		[BurstCompile]
		public struct BakeAnimationMeshJob : IJobFor
		{
			[ReadOnly]
			public int numBones;

			[ReadOnly]
			public NativeArray<Vector3> srcVertices;

			[ReadOnly]
			public NativeArray<BoneWeight> weights;

			[ReadOnly]
			public NativeArray<Matrix4x4> currentPoseMatrices;

			[WriteOnly]
			public NativeArray<Vector3> dstVertices;

			public void Execute(int i)
			{
				Vector3 srcPos = srcVertices[i];
				BoneWeight weight = weights[i];

				Vector3 result = Vector3.zero;

				result += currentPoseMatrices[weight.boneIndex0].MultiplyPoint3x4(srcPos) * weight.weight0;
				result += currentPoseMatrices[weight.boneIndex1].MultiplyPoint3x4(srcPos) * weight.weight1;
				result += currentPoseMatrices[weight.boneIndex2].MultiplyPoint3x4(srcPos) * weight.weight2;
				result += currentPoseMatrices[weight.boneIndex3].MultiplyPoint3x4(srcPos) * weight.weight3;

				dstVertices[i] = result;
			}
		}

		public struct BakePhysicsMeshJob : IJob
		{
#if TCC_USE_ENTITY_ID
		[ReadOnly]
		public EntityId meshId;
#else
			[ReadOnly]
			public int meshId;
#endif

			public MeshColliderCookingOptions cookingOptions;
			public bool convex;

			public void Execute()
			{
				Physics.BakeMesh(meshId, convex, cookingOptions);
			}
		}
#endif // TCC_ENABLE_DYN_COLLIDER
	}

} // namespace Technie.PhysicsCreator.Dynamic
