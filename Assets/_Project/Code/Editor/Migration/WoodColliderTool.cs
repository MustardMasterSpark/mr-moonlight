using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;
using Technie.PhysicsCreator;
using Technie.PhysicsCreator.Rigid;

namespace MrMoonlight.EditorTools.Migration
{
	// MRM-84, 2026-09-18: tree colliders as ONE non-convex MeshCollider built from the triangles
	// Carlos painted as wood (Technie paint on the prefab's "Visual" child, every hull together).
	//
	// Why this replaced convex hulls (TreeColliderTool): the bar is cover - a shot through air
	// the player can see must never hit an invisible collider. A collider made of the bark's own
	// triangles meets that exactly, with one physics shape per tree. Convex hulls can only
	// approximate it: DeadTree01 needed 256 of them, Deadtree06 (flat roots, hollow snag top)
	// still stood 0.87 m off the bark at 492.
	//
	//   WoodColliderTool.Run(new[] { "AP_Tree_X", ... }, dryRun)   headless, no Prefab Mode needed
	//
	// Per prefab: builds <prefab>_Wood.asset (mesh) in WoodColliders/, puts it on a child
	// "Visual/WoodCollider" (MeshCollider, not convex, same layer + tag as Visual), removes every
	// other collider on Visual (Technie's generated hulls), saves, reloads from disk to verify,
	// and renders <project>/Temp/TreeColliders/<prefab>_wood.png. The paint stays, so the tool
	// can be re-run after Carlos repaints.
	//
	// The collider is static-only (a non-convex MeshCollider can't sit on a moving Rigidbody).
	// A future knock-down/burn mechanic disables WoodCollider and adds a Rigidbody + capsule.
	// Full process: Docs/technie-vegetation-collider-process.md
	public static class WoodColliderTool
	{
		public const string PrefabFolder = "Assets/_Project/Art/VegetationPrefabs/AST116_ColliderTest/";
		public const string MeshFolder = PrefabFolder + "WoodColliders/";
		public const string ChildName = "WoodCollider";
		private const int PreviewLayer = 31;

		private static string ReportDir
		{
			get { return System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Temp", "TreeColliders")); }
		}

		// Every prefab in the test folder that has Technie paint, for bulk runs.
		public static string[] PaintedPrefabs()
		{
			List<string> names = new List<string>();
			foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder.TrimEnd('/') }))
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				if (path.StartsWith(MeshFolder)) continue;
				GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
				Transform vis = asset != null ? asset.transform.Find("Visual") : null;
				RigidColliderCreator cr = vis != null ? vis.GetComponent<RigidColliderCreator>() : null;
				if (cr == null || cr.paintingData == null) continue;
				foreach (Hull h in cr.paintingData.hulls)
					if (h.GetSelectedFaces().Length > 0) { names.Add(asset.name); break; }
			}
			names.Sort();
			return names.ToArray();
		}

		// Prefabs sit in the test folder or a subfolder (RetroRealism/, GRASS PREFABS/); names are unique.
		private static string PrefabPath(string prefabName)
		{
			string top = PrefabFolder + prefabName + ".prefab";
			if (AssetDatabase.LoadAssetAtPath<GameObject>(top) != null) return top;
			foreach (string guid in AssetDatabase.FindAssets(prefabName + " t:Prefab", new[] { PrefabFolder.TrimEnd('/') }))
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				if (!path.StartsWith(MeshFolder) && System.IO.Path.GetFileNameWithoutExtension(path) == prefabName) return path;
			}
			return top;
		}

		public static string Run(string[] prefabNames, bool dryRun)
		{
			StringBuilder sb = new StringBuilder();
			sb.AppendLine("WoodColliderTool " + (dryRun ? "DRY RUN (nothing saved)" : "RUN") + " - " + prefabNames.Length + " prefab(s)");
			int ok = 0;
			foreach (string n in prefabNames)
			{
				try { string line = One(n, dryRun); sb.AppendLine(line); if (line.Contains("OK")) ok++; }
				catch (System.Exception e) { sb.AppendLine(n + ": ERROR " + e.Message); }
			}
			sb.AppendLine(ok + " of " + prefabNames.Length + " OK");
			System.IO.Directory.CreateDirectory(ReportDir);
			string path = System.IO.Path.Combine(ReportDir, "wood_run.txt");
			System.IO.File.WriteAllText(path, sb.ToString());
			return sb.ToString() + "Report: " + path;
		}

		private static string One(string prefabName, bool dryRun)
		{
			string prefabPath = PrefabPath(prefabName);
			if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null) return prefabName + ": no such prefab in " + PrefabFolder;
			if (PrefabStageOpenOn(prefabPath)) return prefabName + ": open in Prefab Mode - close it first (the stage would overwrite this run on its next save)";

			string summary;
			GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
			try
			{
				Transform vis = root.transform.Find("Visual");
				if (vis == null) return prefabName + ": no Visual child";
				RigidColliderCreator cr = vis.GetComponent<RigidColliderCreator>();
				if (cr == null || cr.paintingData == null) return prefabName + ": not painted";
				Mesh src = vis.GetComponent<MeshFilter>().sharedMesh;

				// Wood = every painted triangle, whatever hull it's in (hull names, splits and
				// overlaps don't matter any more).
				HashSet<int> wood = new HashSet<int>();
				int hulls = 0;
				foreach (Hull h in cr.paintingData.hulls)
				{
					int[] f = h.GetSelectedFaces();
					if (f.Length > 0) hulls++;
					foreach (int t in f) wood.Add(t);
				}
				if (wood.Count == 0) return prefabName + ": paint is empty";
				List<int> faces = new List<int>(wood);
				faces.Sort();

				// Faces whose material renders both sides (Retro Lit "_Cull" 0 on most of these
				// trees) must block from both sides too: physics ignores back faces, so a shot
				// through a hole in the mesh onto the visible inside of the bark passed through
				// (DeadTree01, 13 of 558 test rays, 2026-09-18). Culled materials stay one-sided -
				// their back faces are invisible, and a hit there would be a hit in visible air.
				Material[] mats = vis.GetComponent<MeshRenderer>().sharedMaterials;
				bool[] twoSided = new bool[src.subMeshCount];
				for (int s = 0; s < twoSided.Length; s++)
				{
					Material m = s < mats.Length ? mats[s] : null;
					twoSided[s] = m != null && m.HasProperty("_Cull") && Mathf.Approximately(m.GetFloat("_Cull"), 0f);
				}
				int doubled;
				Mesh woodMesh = BuildWoodMesh(src, faces, twoSided, prefabName + "_Wood", out doubled);
				string coverage = BarkCoverage(src, wood);
				string image = Render(root, vis, src, faces, woodMesh, prefabName + "_wood.png");

				int oldColliders = vis.GetComponents<Collider>().Length;
				summary = prefabName + ": " + hulls + " painted hull(s), " + faces.Count + " wood tris of " + (src.triangles.Length / 3) + " (" + doubled + " two-sided) | " + coverage + " | old colliders on Visual: " + oldColliders + " | image " + image;
				if (dryRun) { Object.DestroyImmediate(woodMesh); return summary + " | DRY OK"; }

				// Mesh asset: overwrite in place so the GUID (and the collider's reference) survives re-runs.
				if (!AssetDatabase.IsValidFolder(MeshFolder.TrimEnd('/')))
					AssetDatabase.CreateFolder(PrefabFolder.TrimEnd('/'), "WoodColliders");
				string meshPath = MeshFolder + prefabName + "_Wood.asset";
				Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
				if (existing != null)
				{
					EditorUtility.CopySerialized(woodMesh, existing);
					Object.DestroyImmediate(woodMesh);
					woodMesh = existing;
					EditorUtility.SetDirty(existing);
				}
				else AssetDatabase.CreateAsset(woodMesh, meshPath);
				AssetDatabase.SaveAssets();

				// Technie's generated hull colliders go; its paint stays for re-runs.
				cr.RemoveAllGenerated();
				foreach (Collider c in vis.GetComponents<Collider>()) Object.DestroyImmediate(c);

				Transform child = vis.Find(ChildName);
				if (child == null)
				{
					child = new GameObject(ChildName).transform;
					child.SetParent(vis, false);
				}
				child.localPosition = Vector3.zero;
				child.localRotation = Quaternion.identity;
				child.localScale = Vector3.one;
				// Physics reads the collider's OWN tag and layer (unity_tag_must_be_on_collider).
				child.gameObject.layer = vis.gameObject.layer;
				child.gameObject.tag = vis.gameObject.tag;
				child.gameObject.isStatic = vis.gameObject.isStatic;
				foreach (Collider c in child.GetComponents<Collider>()) Object.DestroyImmediate(c);
				MeshCollider mc = child.gameObject.AddComponent<MeshCollider>();
				mc.convex = false;
				mc.sharedMesh = woodMesh;

				PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
			}
			finally { PrefabUtility.UnloadPrefabContents(root); }

			return summary + "\n   " + Verify(prefabName, prefabPath);
		}

		// Physics proof, per prefab: instantiates it in an isolated preview physics scene and fires
		// `rays` raycasts from outside (a third skimming the root zone, every fifth from above) and
		// compares each hit with where the ray meets the VISIBLE wood: painted triangles, front
		// faces, back faces too where the material renders both sides. Pass = no collider hit
		// where there's no wood, no wood without a collider hit, same point within 1 cm.
		public static string RayTest(string[] prefabNames, int rays)
		{
			StringBuilder sb = new StringBuilder();
			foreach (string n in prefabNames)
			{
				UnityEngine.SceneManagement.Scene scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
				try
				{
					GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(n));
					GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
					Physics.SyncTransforms();
					PhysicsScene ps = scene.GetPhysicsScene();
					Transform vis = go.transform.Find("Visual");
					RigidColliderCreator cr = vis.GetComponent<RigidColliderCreator>();
					Mesh src = vis.GetComponent<MeshFilter>().sharedMesh;
					Material[] mats = vis.GetComponent<MeshRenderer>().sharedMaterials;
					int[] tris = src.triangles;
					Vector3[] wv = src.vertices;
					for (int i = 0; i < wv.Length; i++) wv[i] = vis.TransformPoint(wv[i]);
					HashSet<int> wood = new HashSet<int>();
					foreach (Hull h in cr.paintingData.hulls) foreach (int f in h.GetSelectedFaces()) wood.Add(f);
					List<int> faces = new List<int>(wood);
					bool[] two = new bool[faces.Count];
					for (int i = 0; i < faces.Count; i++)
					{
						int s = 0;
						for (int k = 0; k < src.subMeshCount; k++)
						{
							UnityEngine.Rendering.SubMeshDescriptor d = src.GetSubMesh(k);
							if (faces[i] * 3 >= d.indexStart && faces[i] * 3 < d.indexStart + d.indexCount) { s = k; break; }
						}
						Material m = s < mats.Length ? mats[s] : null;
						two[i] = m != null && m.HasProperty("_Cull") && Mathf.Approximately(m.GetFloat("_Cull"), 0f);
					}
					Bounds b = new Bounds(wv[tris[faces[0] * 3]], Vector3.zero);
					foreach (int f in faces) for (int k = 0; k < 3; k++) b.Encapsulate(wv[tris[f * 3 + k]]);

					System.Random rnd = new System.Random(1);
					int hitsPhys = 0, hitsWood = 0, agree = 0, falseHit = 0, miss = 0;
					float worst = 0f;
					for (int r = 0; r < rays; r++)
					{
						float hgt = (float)rnd.NextDouble();
						if (r < rays / 3) hgt *= 0.08f;
						Vector3 target = new Vector3(Mathf.Lerp(b.min.x, b.max.x, (float)rnd.NextDouble()), Mathf.Lerp(b.min.y, b.max.y, hgt), Mathf.Lerp(b.min.z, b.max.z, (float)rnd.NextDouble()));
						float ang = (float)(rnd.NextDouble() * 2.0 * System.Math.PI);
						float el = r % 5 == 0 ? -1.2f : (float)(rnd.NextDouble() - 0.5) * 0.4f;
						Vector3 dir = new Vector3(Mathf.Cos(ang) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Sin(ang) * Mathf.Cos(el)).normalized;
						Vector3 origin = target - dir * (b.extents.magnitude * 2f);
						float maxD = b.extents.magnitude * 4f;
						RaycastHit hit;
						bool ph = ps.Raycast(origin, dir, out hit, maxD);
						float best = float.MaxValue;
						for (int i = 0; i < faces.Count; i++)
						{
							int f = faces[i];
							Vector3 a0 = wv[tris[f * 3]], e1 = wv[tris[f * 3 + 1]] - a0, e2 = wv[tris[f * 3 + 2]] - a0;
							Vector3 pv = Vector3.Cross(dir, e2);
							float det = Vector3.Dot(e1, pv);
							// det = -dot(dir, cross(e1, e2)): positive when the ray meets the front face
							// (Unity's front normal is cross(b - a, c - a)).
							if (Mathf.Abs(det) < 1e-9f || (!two[i] && det < 0f)) continue;
							float inv = 1f / det;
							Vector3 tv = origin - a0;
							float u = Vector3.Dot(tv, pv) * inv;
							if (u < 0f || u > 1f) continue;
							Vector3 qv = Vector3.Cross(tv, e1);
							float w = Vector3.Dot(dir, qv) * inv;
							if (w < 0f || u + w > 1f) continue;
							float d = Vector3.Dot(e2, qv) * inv;
							if (d > 0f && d < best) best = d;
						}
						bool wh = best < maxD;
						if (ph) hitsPhys++;
						if (wh) hitsWood++;
						if (ph && wh) { float err = Mathf.Abs(hit.distance - best); worst = Mathf.Max(worst, err); if (err < 0.01f) agree++; }
						else if (ph) falseHit++;
						else if (wh) miss++;
					}
					bool pass = falseHit == 0 && miss == 0 && agree == hitsWood;
					sb.AppendLine(n + ": " + (pass ? "RAYTEST OK" : "RAYTEST PROBLEM") + " - " + rays + " rays, " + hitsWood + " hit visible wood, " + agree + " collider hits on the same point | collider hit, no wood: " + falseHit + " | wood, no collider hit: " + miss + " | worst error " + worst.ToString("F3") + " m");
				}
				catch (System.Exception e) { sb.AppendLine(n + ": RAYTEST ERROR " + e.Message); }
				finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
			}
			return sb.ToString();
		}

		private static bool PrefabStageOpenOn(string prefabPath)
		{
			UnityEditor.SceneManagement.PrefabStage st = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
			return st != null && st.assetPath == prefabPath;
		}

		// Reads the prefab back from disk: exactly one collider under Visual, the non-convex wood
		// MeshCollider on Visual/WoodCollider, its mesh the saved asset, tag/layer matching Visual.
		private static string Verify(string prefabName, string prefabPath)
		{
			GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
			try
			{
				Transform vis = root.transform.Find("Visual");
				Collider[] all = root.GetComponentsInChildren<Collider>(true);
				Transform child = vis.Find(ChildName);
				MeshCollider mc = child != null ? child.GetComponent<MeshCollider>() : null;
				List<string> problems = new List<string>();
				if (mc == null) problems.Add("no MeshCollider on Visual/" + ChildName);
				else
				{
					if (mc.convex) problems.Add("collider is convex");
					if (mc.sharedMesh == null || AssetDatabase.GetAssetPath(mc.sharedMesh) != MeshFolder + prefabName + "_Wood.asset") problems.Add("collider mesh is not " + prefabName + "_Wood.asset");
					if (child.gameObject.layer != vis.gameObject.layer || child.tag != vis.tag) problems.Add("layer/tag differ from Visual");
				}
				if (all.Length != 1) problems.Add(all.Length + " colliders under the root (expected 1)");
				int tris = mc != null && mc.sharedMesh != null ? mc.sharedMesh.triangles.Length / 3 : 0;
				return prefabName + ": " + (problems.Count == 0 ? "VERIFY OK" : "VERIFY PROBLEM " + string.Join("; ", problems.ToArray())) + " (1 MeshCollider, " + tris + " tris, layer " + LayerMask.LayerToName(vis.gameObject.layer) + ", tag " + vis.tag + ")";
			}
			finally { PrefabUtility.UnloadPrefabContents(root); }
		}

		private static Mesh BuildWoodMesh(Mesh src, List<int> faces, bool[] twoSidedSubmesh, string name, out int doubled)
		{
			Vector3[] verts = src.vertices;
			int[] tris = src.triangles;
			int[] faceSub = new int[tris.Length / 3];
			for (int s = 0; s < src.subMeshCount; s++)
			{
				UnityEngine.Rendering.SubMeshDescriptor d = src.GetSubMesh(s);
				for (int f = d.indexStart / 3; f < (d.indexStart + d.indexCount) / 3; f++) faceSub[f] = s;
			}
			Dictionary<int, int> remap = new Dictionary<int, int>();
			List<Vector3> v = new List<Vector3>();
			List<int> t = new List<int>(faces.Count * 6);
			doubled = 0;
			foreach (int f in faces)
			{
				int[] n = new int[3];
				for (int k = 0; k < 3; k++)
				{
					int old = tris[f * 3 + k];
					if (!remap.TryGetValue(old, out n[k])) { n[k] = v.Count; remap[old] = n[k]; v.Add(verts[old]); }
				}
				t.Add(n[0]); t.Add(n[1]); t.Add(n[2]);
				if (twoSidedSubmesh[faceSub[f]]) { t.Add(n[0]); t.Add(n[2]); t.Add(n[1]); doubled++; }
			}
			Mesh m = new Mesh();
			m.name = name;
			if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
			m.SetVertices(v);
			m.SetTriangles(t, 0);
			m.RecalculateNormals();
			m.RecalculateBounds();
			return m;
		}

		// Many of these meshes keep bark and leaves in separate submeshes. When they do, the
		// bark submesh (the larger-textured first one) says how much bark went unpainted - a
		// branch Carlos missed shows up here. With one submesh this can't be told apart.
		private static string BarkCoverage(Mesh src, HashSet<int> wood)
		{
			if (src.subMeshCount < 2) return "single submesh (no bark check)";
			int start = (int)src.GetSubMesh(0).indexStart / 3;
			int count = (int)src.GetSubMesh(0).indexCount / 3;
			int painted = 0;
			for (int f = start; f < start + count; f++) if (wood.Contains(f)) painted++;
			int outside = wood.Count - painted;
			return "submesh 0: " + painted + "/" + count + " painted (" + (100f * painted / Mathf.Max(1, count)).ToString("F0") + "%), " + outside + " painted tris in other submeshes";
		}

		// Four views (three sides + straight down). Left: the whole mesh, wood in orange and
		// everything unpainted (leaves, missed bark) in grey - a grey branch is a missed branch.
		// Right: the collider alone.
		private static string Render(GameObject root, Transform vis, Mesh src, List<int> faces, Mesh woodMesh, string fileName)
		{
			List<Object> temp = new List<Object>();
			string path = null;
			try
			{
				UnityEngine.SceneManagement.Scene scene = root.scene;
				Shader lit = Shader.Find("Universal Render Pipeline/Lit");
				if (lit == null) lit = Shader.Find("Standard");

				HashSet<int> wood = new HashSet<int>(faces);
				List<int> rest = new List<int>();
				for (int f = 0; f < src.triangles.Length / 3; f++) if (!wood.Contains(f)) rest.Add(f);

				GameObject holder = new GameObject("~WoodPreview");
				temp.Add(holder);
				holder.transform.SetParent(vis, false);
				Renderer woodR = Add(holder, Flat(src, faces), new Color(0.95f, 0.55f, 0.15f), lit, temp);
				Renderer restR = Add(holder, Flat(src, rest), new Color(0.55f, 0.57f, 0.6f), lit, temp);
				Renderer colR = Add(holder, Flat(woodMesh, null), new Color(0.2f, 0.85f, 0.45f), lit, temp);

				GameObject lightGo = new GameObject("~WoodLight");
				temp.Add(lightGo);
				UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lightGo, scene);
				Light light = lightGo.AddComponent<Light>();
				light.type = LightType.Directional;
				light.intensity = 1.2f;
				light.cullingMask = 1 << PreviewLayer;

				GameObject camGo = new GameObject("~WoodCamera");
				temp.Add(camGo);
				UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camGo, scene);
				Camera cam = camGo.AddComponent<Camera>();
				cam.enabled = false;
				cam.scene = scene;
				cam.cullingMask = 1 << PreviewLayer;
				cam.clearFlags = CameraClearFlags.SolidColor;
				cam.backgroundColor = new Color(0.16f, 0.17f, 0.2f);
				cam.orthographic = true;

				Bounds b = woodR.bounds;
				float radius = Mathf.Max(b.extents.magnitude, 0.05f);
				Vector3[] dirs = { new Vector3(0f, -0.15f, 1f), new Vector3(-1f, -0.15f, 0f), new Vector3(0.7f, -0.9f, -0.7f), Vector3.down };
				const int tile = 512;
				RenderTexture rt = new RenderTexture(tile, tile, 24, RenderTextureFormat.ARGB32);
				rt.antiAliasing = 4;
				temp.Add(rt);
				cam.targetTexture = rt;
				Texture2D sheet = new Texture2D(tile * 2, tile * dirs.Length, TextureFormat.RGB24, false);
				temp.Add(sheet);
				RenderTexture prev = RenderTexture.active;
				for (int v = 0; v < dirs.Length; v++)
				{
					Vector3 fwd = dirs[v].normalized;
					bool top = Mathf.Abs(fwd.y) > 0.95f;
					camGo.transform.position = b.center - fwd * radius * 3f;
					camGo.transform.rotation = Quaternion.LookRotation(fwd, top ? Vector3.forward : Vector3.up);
					cam.orthographicSize = radius * 1.02f;
					cam.nearClipPlane = 0.01f;
					cam.farClipPlane = radius * 6f;
					lightGo.transform.rotation = Quaternion.LookRotation(fwd + new Vector3(0.3f, -0.6f, 0.2f), Vector3.up);
					for (int col = 0; col < 2; col++)
					{
						woodR.enabled = col == 0;
						restR.enabled = col == 0;
						colR.enabled = col == 1;
						cam.Render();
						RenderTexture.active = rt;
						sheet.ReadPixels(new Rect(0, 0, tile, tile), col * tile, (dirs.Length - 1 - v) * tile);
					}
				}
				sheet.Apply();
				RenderTexture.active = prev;
				cam.targetTexture = null;
				System.IO.Directory.CreateDirectory(ReportDir);
				path = System.IO.Path.Combine(ReportDir, fileName);
				System.IO.File.WriteAllBytes(path, sheet.EncodeToPNG());
			}
			finally
			{
				for (int i = temp.Count - 1; i >= 0; i--) if (temp[i] != null) Object.DestroyImmediate(temp[i]);
			}
			return path;
		}

		private static Renderer Add(GameObject holder, Mesh mesh, Color colour, Shader shader, List<Object> temp)
		{
			temp.Add(mesh);
			GameObject go = new GameObject("~part");
			go.layer = PreviewLayer;
			go.transform.SetParent(holder.transform, false);
			go.AddComponent<MeshFilter>().sharedMesh = mesh;
			MeshRenderer r = go.AddComponent<MeshRenderer>();
			Material m = new Material(shader);
			if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", colour);
			if (m.HasProperty("_Color")) m.SetColor("_Color", colour);
			if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f);
			// Leaf cards are single-sided; draw both sides so they read from every angle.
			if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
			temp.Add(m);
			r.sharedMaterial = m;
			r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			return r;
		}

		// Unshared vertices per triangle so lighting shows every facet.
		private static Mesh Flat(Mesh src, List<int> faces)
		{
			Vector3[] verts = src.vertices;
			int[] tris = src.triangles;
			int count = faces != null ? faces.Count : tris.Length / 3;
			Vector3[] v = new Vector3[count * 3];
			int[] t = new int[count * 3];
			for (int i = 0; i < count; i++)
			{
				int f = faces != null ? faces[i] : i;
				for (int k = 0; k < 3; k++) { v[i * 3 + k] = verts[tris[f * 3 + k]]; t[i * 3 + k] = i * 3 + k; }
			}
			Mesh m = new Mesh();
			m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
			m.vertices = v;
			m.triangles = t;
			m.RecalculateNormals();
			m.RecalculateBounds();
			return m;
		}
	}
}
