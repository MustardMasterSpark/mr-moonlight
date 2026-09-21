using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;
using Technie.PhysicsCreator;
using Technie.PhysicsCreator.Rigid;

namespace MrMoonlight.EditorTools.Migration
{
	// MRM-84 follow-up, 2026-09-21: READ-ONLY audit. Which painted "wood" triangles actually sit on
	// leaf cards (flat alpha-cutout planes that only show leaves)? Changes nothing: no prefab, mesh,
	// or scene is written. Only Temp/TreeColliders/leaf_audit.txt.
	//
	//   WoodColliderLeafAudit.Run()   -> per-prefab table, worst first
	//
	// Signals per prefab (using the same paint WoodColliderTool bakes):
	//   - submesh material is alpha-cutout ("cutout"): bark is opaque, leaves/twig cards are cutout;
	//   - transparent tris: painted tris whose base-map alpha under the triangle is mostly clear,
	//     i.e. the collider stands in visible air;
	//   - flat components: welded groups of painted tris that are a flat sheet rather than a tube.
	public static class WoodColliderLeafAudit
	{
		private static readonly Dictionary<Texture, Color32[]> TexCache = new Dictionary<Texture, Color32[]>();
		private const int TexRes = 128;

		public static string Run()
		{
			TexCache.Clear();
			StringBuilder sb = new StringBuilder();
			string[] names = WoodColliderTool.PaintedPrefabs();
			List<string> rows = new List<string>();
			foreach (string n in names)
			{
				try { rows.Add(Audit(n)); }
				catch (System.Exception e) { rows.Add("0|" + n + ": ERROR " + e.Message); }
			}
			rows.Sort(delegate(string a, string b)
			{
				int ia = int.Parse(a.Substring(0, a.IndexOf('|'))), ib = int.Parse(b.Substring(0, b.IndexOf('|')));
				return ib.CompareTo(ia);
			});
			sb.AppendLine("WoodColliderLeafAudit (read-only) - " + names.Length + " prefabs, worst first");
			foreach (string r in rows) sb.AppendLine(r.Substring(r.IndexOf('|') + 1));
			string dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Temp", "TreeColliders"));
			System.IO.Directory.CreateDirectory(dir);
			System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "leaf_audit.txt"), sb.ToString());
			return sb.ToString();
		}

		// Where the suspect paint is: every welded group of painted tris that sits on a cutout
		// submesh, with size in prefab metres, height above the mesh's lowest point, and distance from
		// the mesh's vertical centre line, so Carlos can find them in the Technie paint view.
		public static string Detail(string[] prefabNames)
		{
			TexCache.Clear();
			StringBuilder sb = new StringBuilder();
			foreach (string prefabName in prefabNames)
			{
				GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(PathOf(prefabName));
				Transform vis = asset.transform.Find("Visual");
				Mesh mesh = vis.GetComponent<MeshFilter>().sharedMesh;
				Material[] mats = vis.GetComponent<MeshRenderer>().sharedMaterials;
				RigidColliderCreator cr = vis.GetComponent<RigidColliderCreator>();
				HashSet<int> wood = new HashSet<int>();
				foreach (Hull h in cr.paintingData.hulls) foreach (int f in h.GetSelectedFaces()) wood.Add(f);
				int[] tris = mesh.triangles; Vector3[] v = mesh.vertices; Vector2[] uv = mesh.uv;
				int triCount = tris.Length / 3;
				Vector3 sc = vis.lossyScale;
				float k = (Mathf.Abs(sc.x) + Mathf.Abs(sc.y) + Mathf.Abs(sc.z)) / 3f;
				Bounds b = mesh.bounds;
				int[] sub = new int[triCount];
				for (int s = 0; s < mesh.subMeshCount; s++)
				{
					UnityEngine.Rendering.SubMeshDescriptor d = mesh.GetSubMesh(s);
					for (int t = (int)d.indexStart / 3; t < (int)(d.indexStart + d.indexCount) / 3; t++) sub[t] = s;
				}
				List<int> suspects = new List<int>();
				foreach (int t in wood)
				{
					Material m = sub[t] < mats.Length ? mats[sub[t]] : null;
					if (m != null && (m.IsKeywordEnabled("_ALPHATEST_ON") || (m.HasProperty("_AlphaClip") && m.GetFloat("_AlphaClip") > 0.5f))) suspects.Add(t);
				}
				sb.AppendLine(prefabName + "  (mesh height " + (b.size.y * k).ToString("F1") + " m, trunk-axis at local x/z " + b.center.x.ToString("F2") + "/" + b.center.z.ToString("F2") + ")  suspects " + suspects.Count);
				int[] parent = new int[triCount];
				for (int i = 0; i < triCount; i++) parent[i] = i;
				Dictionary<long, int> weld = new Dictionary<long, int>();
				foreach (int t in suspects)
					for (int q = 0; q < 3; q++)
					{
						Vector3 p = v[tris[t * 3 + q]];
						long key = ((long)Mathf.RoundToInt(p.x * 2000f) * 73856093L) ^ ((long)Mathf.RoundToInt(p.y * 2000f) * 19349663L) ^ ((long)Mathf.RoundToInt(p.z * 2000f) * 83492791L);
						int o; if (weld.TryGetValue(key, out o)) Union(parent, t, o); else weld[key] = t;
					}
				Dictionary<int, List<int>> comps = new Dictionary<int, List<int>>();
				foreach (int t in suspects) { int r = Find(parent, t); List<int> l; if (!comps.TryGetValue(r, out l)) { l = new List<int>(); comps[r] = l; } l.Add(t); }
				List<string> lines = new List<string>();
				foreach (KeyValuePair<int, List<int>> kv in comps)
				{
					Bounds cb = new Bounds(v[tris[kv.Value[0] * 3]], Vector3.zero);
					float area = 0f, alpha = 0f;
					foreach (int t in kv.Value)
					{
						for (int q = 0; q < 3; q++) cb.Encapsulate(v[tris[t * 3 + q]]);
						area += Vector3.Cross(v[tris[t * 3 + 1]] - v[tris[t * 3]], v[tris[t * 3 + 2]] - v[tris[t * 3]]).magnitude * 0.5f;
						alpha += TriAlpha(mats[sub[t]], uv, tris, t);
					}
					float dist = new Vector2(cb.center.x - b.center.x, cb.center.z - b.center.z).magnitude * k;
					float up = (cb.center.y - b.min.y) * k;
					lines.Add(kv.Value.Count.ToString("D5") + "|      " + kv.Value.Count + " tris, " + (area * k * k).ToString("F2") + " m2, size " + (cb.size.x * k).ToString("F1") + "x" + (cb.size.y * k).ToString("F1") + "x" + (cb.size.z * k).ToString("F1") + " m, " + up.ToString("F1") + " m up, " + dist.ToString("F1") + " m off the trunk axis, mean alpha " + (alpha / kv.Value.Count).ToString("F2") + (IsFlatSheet(kv.Value, v, tris) ? ", FLAT" : ""));
				}
				lines.Sort(delegate(string a, string c) { return c.CompareTo(a); });
				int shown = 0;
				foreach (string ln in lines) { if (shown++ >= 10) { sb.AppendLine("      ... " + (lines.Count - 10) + " more groups"); break; } sb.AppendLine(ln.Substring(ln.IndexOf('|') + 1)); }
			}
			string dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Temp", "TreeColliders"));
			System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "leaf_audit_detail.txt"), sb.ToString());
			return sb.ToString();
		}

		// Picture of the suspects: painted tris on a cutout submesh in RED, other painted wood ORANGE,
		// unpainted GREY. Row 1 = whole tree (front, side); each further row = close-up of one red
		// group (front, side). Writes Temp/TreeColliders/<prefab>_suspects.png. Read-only.
		public static string Highlight(string prefabName)
		{
			GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(PathOf(prefabName));
			Transform vis = asset.transform.Find("Visual");
			Mesh mesh = vis.GetComponent<MeshFilter>().sharedMesh;
			Material[] mats = vis.GetComponent<MeshRenderer>().sharedMaterials;
			RigidColliderCreator cr = vis.GetComponent<RigidColliderCreator>();
			HashSet<int> wood = new HashSet<int>();
			foreach (Hull h in cr.paintingData.hulls) foreach (int f in h.GetSelectedFaces()) wood.Add(f);
			int[] tris = mesh.triangles; Vector3[] v = mesh.vertices;
			int triCount = tris.Length / 3;
			int[] sub = new int[triCount];
			for (int s = 0; s < mesh.subMeshCount; s++)
			{
				UnityEngine.Rendering.SubMeshDescriptor d = mesh.GetSubMesh(s);
				for (int t = (int)d.indexStart / 3; t < (int)(d.indexStart + d.indexCount) / 3; t++) sub[t] = s;
			}
			List<int> red = new List<int>(), orange = new List<int>(), grey = new List<int>();
			for (int t = 0; t < triCount; t++)
			{
				Material m = sub[t] < mats.Length ? mats[sub[t]] : null;
				bool cut = m != null && (m.IsKeywordEnabled("_ALPHATEST_ON") || (m.HasProperty("_AlphaClip") && m.GetFloat("_AlphaClip") > 0.5f));
				if (wood.Contains(t)) { if (cut) red.Add(t); else orange.Add(t); } else grey.Add(t);
			}
			// Welded red groups.
			int[] parent = new int[triCount];
			for (int i = 0; i < triCount; i++) parent[i] = i;
			Dictionary<long, int> weld = new Dictionary<long, int>();
			foreach (int t in red)
				for (int q = 0; q < 3; q++)
				{
					Vector3 p = v[tris[t * 3 + q]];
					long key = ((long)Mathf.RoundToInt(p.x * 2000f) * 73856093L) ^ ((long)Mathf.RoundToInt(p.y * 2000f) * 19349663L) ^ ((long)Mathf.RoundToInt(p.z * 2000f) * 83492791L);
					int o; if (weld.TryGetValue(key, out o)) Union(parent, t, o); else weld[key] = t;
				}
			Dictionary<int, List<int>> comps = new Dictionary<int, List<int>>();
			foreach (int t in red) { int r = Find(parent, t); List<int> l; if (!comps.TryGetValue(r, out l)) { l = new List<int>(); comps[r] = l; } l.Add(t); }
			List<List<int>> groups = new List<List<int>>(comps.Values);
			groups.Sort(delegate(List<int> a, List<int> b) { return b.Count.CompareTo(a.Count); });
			if (groups.Count > 6) groups.RemoveRange(6, groups.Count - 6);

			UnityEngine.SceneManagement.Scene scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
			List<Object> temp = new List<Object>();
			try
			{
				Shader lit = Shader.Find("Universal Render Pipeline/Lit");
				if (lit == null) lit = Shader.Find("Standard");
				GameObject holder = new GameObject("~sus");
				UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(holder, scene);
				temp.Add(holder);
				MakePart(holder, mesh, grey, new Color(0.55f, 0.57f, 0.6f), lit, temp);
				MakePart(holder, mesh, orange, new Color(0.95f, 0.55f, 0.15f), lit, temp);
				MakePart(holder, mesh, red, new Color(1f, 0.05f, 0.05f), lit, temp);

				GameObject lightGo = new GameObject("~l");
				UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lightGo, scene);
				temp.Add(lightGo);
				Light light = lightGo.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.2f;
				GameObject camGo = new GameObject("~c");
				UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camGo, scene);
				temp.Add(camGo);
				Camera cam = camGo.AddComponent<Camera>();
				cam.enabled = false; cam.scene = scene;
				cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.16f, 0.17f, 0.2f);
				cam.orthographic = true;

				const int tile = 512;
				int rows = 1 + groups.Count;
				RenderTexture rt = new RenderTexture(tile, tile, 24, RenderTextureFormat.ARGB32);
				rt.antiAliasing = 4; temp.Add(rt);
				cam.targetTexture = rt;
				Texture2D sheet = new Texture2D(tile * 2, tile * rows, TextureFormat.RGB24, false);
				temp.Add(sheet);
				RenderTexture prev = RenderTexture.active;
				Vector3[] dirs = { new Vector3(0f, -0.15f, 1f), new Vector3(-1f, -0.15f, 0f) };
				for (int row = 0; row < rows; row++)
				{
					Vector3 centre; float size;
					if (row == 0) { centre = mesh.bounds.center; size = mesh.bounds.extents.magnitude * 1.02f; }
					else
					{
						Bounds gb = new Bounds(v[tris[groups[row - 1][0] * 3]], Vector3.zero);
						foreach (int t in groups[row - 1]) for (int q = 0; q < 3; q++) gb.Encapsulate(v[tris[t * 3 + q]]);
						centre = gb.center; size = Mathf.Max(gb.extents.magnitude * 2.5f, 0.6f);
					}
					for (int col = 0; col < 2; col++)
					{
						Vector3 fwd = dirs[col].normalized;
						camGo.transform.position = centre - fwd * 200f;
						camGo.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
						cam.orthographicSize = size; cam.nearClipPlane = 0.01f; cam.farClipPlane = 400f;
						lightGo.transform.rotation = Quaternion.LookRotation(fwd + new Vector3(0.3f, -0.6f, 0.2f), Vector3.up);
						cam.Render();
						RenderTexture.active = rt;
						sheet.ReadPixels(new Rect(0, 0, tile, tile), col * tile, (rows - 1 - row) * tile);
					}
				}
				sheet.Apply();
				RenderTexture.active = prev;
				cam.targetTexture = null;
				string dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Temp", "TreeColliders"));
				System.IO.Directory.CreateDirectory(dir);
				string path = System.IO.Path.Combine(dir, prefabName + "_suspects.png");
				System.IO.File.WriteAllBytes(path, sheet.EncodeToPNG());
				return path + "  (" + red.Count + " red tris in " + comps.Count + " groups, " + groups.Count + " close-ups)";
			}
			finally
			{
				for (int i = temp.Count - 1; i >= 0; i--) if (temp[i] != null) Object.DestroyImmediate(temp[i]);
				UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
			}
		}

		private static void MakePart(GameObject holder, Mesh src, List<int> faces, Color colour, Shader shader, List<Object> temp)
		{
			if (faces.Count == 0) return;
			Vector3[] sv = src.vertices; int[] st = src.triangles;
			Vector3[] nv = new Vector3[faces.Count * 3]; int[] nt = new int[faces.Count * 3];
			for (int i = 0; i < faces.Count; i++)
				for (int k = 0; k < 3; k++) { nv[i * 3 + k] = sv[st[faces[i] * 3 + k]]; nt[i * 3 + k] = i * 3 + k; }
			Mesh m = new Mesh();
			m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
			m.vertices = nv; m.triangles = nt; m.RecalculateNormals(); m.RecalculateBounds();
			temp.Add(m);
			GameObject go = new GameObject("~part");
			go.transform.SetParent(holder.transform, false);
			go.AddComponent<MeshFilter>().sharedMesh = m;
			MeshRenderer r = go.AddComponent<MeshRenderer>();
			Material mat = new Material(shader);
			if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", colour);
			if (mat.HasProperty("_Color")) mat.SetColor("_Color", colour);
			if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
			if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
			temp.Add(mat);
			r.sharedMaterial = mat;
			r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
		}

		private static string PathOf(string prefabName)
		{
			foreach (string guid in AssetDatabase.FindAssets(prefabName + " t:Prefab", new[] { WoodColliderTool.PrefabFolder.TrimEnd('/') }))
			{
				string p = AssetDatabase.GUIDToAssetPath(guid);
				if (!p.StartsWith(WoodColliderTool.MeshFolder) && System.IO.Path.GetFileNameWithoutExtension(p) == prefabName) return p;
			}
			return null;
		}

		private static string Audit(string prefabName)
		{
			GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(PathOf(prefabName));
			Transform vis = asset.transform.Find("Visual");
			Mesh mesh = vis.GetComponent<MeshFilter>().sharedMesh;
			Material[] mats = vis.GetComponent<MeshRenderer>().sharedMaterials;
			RigidColliderCreator cr = vis.GetComponent<RigidColliderCreator>();
			HashSet<int> wood = new HashSet<int>();
			foreach (Hull h in cr.paintingData.hulls) foreach (int f in h.GetSelectedFaces()) wood.Add(f);

			int[] tris = mesh.triangles;
			Vector3[] v = mesh.vertices;
			Vector2[] uv = mesh.uv;
			int triCount = tris.Length / 3;

			int[] sub = new int[triCount];
			for (int s = 0; s < mesh.subMeshCount; s++)
			{
				UnityEngine.Rendering.SubMeshDescriptor d = mesh.GetSubMesh(s);
				for (int t = (int)d.indexStart / 3; t < (int)(d.indexStart + d.indexCount) / 3; t++) sub[t] = s;
			}

			// Per submesh: cutout? and painted / total.
			bool[] cutout = new bool[mesh.subMeshCount];
			string[] matName = new string[mesh.subMeshCount];
			int[] paintedIn = new int[mesh.subMeshCount];
			int[] totalIn = new int[mesh.subMeshCount];
			for (int s = 0; s < cutout.Length; s++)
			{
				Material m = s < mats.Length ? mats[s] : null;
				matName[s] = m != null ? m.name : "null";
				cutout[s] = m != null && (m.IsKeywordEnabled("_ALPHATEST_ON") || (m.HasProperty("_AlphaClip") && m.GetFloat("_AlphaClip") > 0.5f));
			}
			for (int t = 0; t < triCount; t++) { totalIn[sub[t]]++; if (wood.Contains(t)) paintedIn[sub[t]]++; }

			// Alpha under each painted tri on a cutout submesh.
			int onCutout = 0, clear = 0, mostlyClear = 0;
			foreach (int t in wood)
			{
				int s = sub[t];
				if (!cutout[s]) continue;
				onCutout++;
				float a = TriAlpha(mats[s], uv, tris, t);
				if (a < 0.15f) clear++;
				if (a < 0.5f) mostlyClear++;
			}

			// Flat components among the painted tris.
			Dictionary<long, int> weld = new Dictionary<long, int>();
			int[] parent = new int[triCount];
			for (int i = 0; i < triCount; i++) parent[i] = i;
			foreach (int t in wood)
			{
				for (int k = 0; k < 3; k++)
				{
					Vector3 p = v[tris[t * 3 + k]];
					long key = ((long)Mathf.RoundToInt(p.x * 2000f) * 73856093L) ^ ((long)Mathf.RoundToInt(p.y * 2000f) * 19349663L) ^ ((long)Mathf.RoundToInt(p.z * 2000f) * 83492791L);
					int other;
					if (weld.TryGetValue(key, out other)) Union(parent, t, other); else weld[key] = t;
				}
			}
			Dictionary<int, List<int>> comps = new Dictionary<int, List<int>>();
			foreach (int t in wood)
			{
				int r = Find(parent, t);
				List<int> l;
				if (!comps.TryGetValue(r, out l)) { l = new List<int>(); comps[r] = l; }
				l.Add(t);
			}
			int flatComps = 0, flatTris = 0, biggestFlat = 0;
			foreach (KeyValuePair<int, List<int>> kv in comps)
			{
				if (IsFlatSheet(kv.Value, v, tris))
				{
					flatComps++; flatTris += kv.Value.Count;
					if (kv.Value.Count > biggestFlat) biggestFlat = kv.Value.Count;
				}
			}

			StringBuilder sb = new StringBuilder();
			sb.Append(prefabName + ": painted " + wood.Count + "/" + triCount + " | components " + comps.Count + " | FLAT sheets " + flatComps + " (" + flatTris + " tris, biggest " + biggestFlat + ")");
			sb.Append(" | on cutout material " + onCutout + " (alpha<0.15: " + clear + ", <0.5: " + mostlyClear + ")");
			sb.Append(" | submeshes: ");
			for (int s = 0; s < cutout.Length; s++)
				sb.Append("[" + matName[s] + (cutout[s] ? " CUTOUT " : " opaque ") + paintedIn[s] + "/" + totalIn[s] + "] ");
			// Rank: transparent tris weigh most, then flat sheets, then any paint on cutout.
			int score = clear * 4 + mostlyClear * 2 + flatTris + onCutout;
			return score + "|" + sb.ToString();
		}

		private static int Find(int[] p, int x) { while (p[x] != x) { p[x] = p[p[x]]; x = p[x]; } return x; }
		private static void Union(int[] p, int a, int b) { a = Find(p, a); b = Find(p, b); if (a != b) p[a] = b; }

		// A leaf card is a sheet: nearly all of its area faces the same (or exactly opposite) way.
		// A trunk or limb is a tube: normals go all around. Bent leaf clumps land in between, so the
		// bar is deliberately loose (70% of area within 35 degrees of the dominant axis).
		private static bool IsFlatSheet(List<int> comp, Vector3[] v, int[] tris)
		{
			if (comp.Count < 2) return false;
			Vector3 dom = Vector3.zero;
			float bestArea = 0f;
			float total = 0f;
			foreach (int t in comp)
			{
				Vector3 c = Vector3.Cross(v[tris[t * 3 + 1]] - v[tris[t * 3]], v[tris[t * 3 + 2]] - v[tris[t * 3]]);
				float a = c.magnitude * 0.5f;
				total += a;
				if (a > bestArea) { bestArea = a; dom = c.normalized; }
			}
			if (total < 1e-8f) return false;
			float aligned = 0f;
			foreach (int t in comp)
			{
				Vector3 c = Vector3.Cross(v[tris[t * 3 + 1]] - v[tris[t * 3]], v[tris[t * 3 + 2]] - v[tris[t * 3]]);
				float a = c.magnitude * 0.5f;
				if (a <= 0f) continue;
				if (Mathf.Abs(Vector3.Dot(c.normalized, dom)) > 0.82f) aligned += a;
			}
			return aligned / total > 0.7f;
		}

		private static float TriAlpha(Material m, Vector2[] uv, int[] tris, int t)
		{
			Texture tex = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
			if (tex == null && m.HasProperty("_MainTex")) tex = m.GetTexture("_MainTex");
			if (tex == null) return 1f;
			Color32[] px = Pixels(tex);
			Vector2 a = uv[tris[t * 3]], b = uv[tris[t * 3 + 1]], c = uv[tris[t * 3 + 2]];
			Vector3[] bary = { new Vector3(1f/3, 1f/3, 1f/3), new Vector3(.6f,.2f,.2f), new Vector3(.2f,.6f,.2f), new Vector3(.2f,.2f,.6f), new Vector3(.45f,.45f,.1f), new Vector3(.1f,.45f,.45f), new Vector3(.45f,.1f,.45f) };
			float sum = 0f;
			foreach (Vector3 w in bary)
			{
				Vector2 p = a * w.x + b * w.y + c * w.z;
				float u = p.x - Mathf.Floor(p.x), vv = p.y - Mathf.Floor(p.y);
				int ix = Mathf.Clamp((int)(u * TexRes), 0, TexRes - 1), iy = Mathf.Clamp((int)(vv * TexRes), 0, TexRes - 1);
				sum += px[iy * TexRes + ix].a / 255f;
			}
			return sum / bary.Length;
		}

		private static Color32[] Pixels(Texture tex)
		{
			Color32[] cached;
			if (TexCache.TryGetValue(tex, out cached)) return cached;
			RenderTexture rt = RenderTexture.GetTemporary(TexRes, TexRes, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
			RenderTexture prev = RenderTexture.active;
			Graphics.Blit(tex, rt);
			RenderTexture.active = rt;
			Texture2D r = new Texture2D(TexRes, TexRes, TextureFormat.RGBA32, false);
			r.ReadPixels(new Rect(0, 0, TexRes, TexRes), 0, 0);
			Color32[] px = r.GetPixels32();
			Object.DestroyImmediate(r);
			RenderTexture.active = prev;
			RenderTexture.ReleaseTemporary(rt);
			TexCache[tex] = px;
			return px;
		}
	}
}
