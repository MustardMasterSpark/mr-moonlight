// MRM-70 — measure every vegetation prefab's REAL mesh geometry.
// Run through UnityMCP execute_code (this is a method body, not a compiled file).
// Writes veg_sizes.csv next to the other scripts in Tools/vegetation/.
//
// Size comes from the Visual child's renderer bounds, NOT from colliders: the MRM-70 batch
// gave every prop a capsule spanning the full mesh height including below-ground roots, so
// collider height is systematically larger than the visible mesh. Colliders are read only
// for blockR — how wide the prop blocks the player.

var roots = new string[]{
 "Assets/_Project/Art/VegetationPrefabs",
 "Assets/_Project/Prefabs/World/Vegetation/RetroRealism",
 "Assets/_Project/Prefabs/World/Vegetation/GrassFlowers",
 "Assets/_Project/Prefabs/World/Vegetation/TerrainSampleAssets"};

var sb = new System.Text.StringBuilder();
sb.Append("name,folder,visW,visH,visD,footprint,minY,visibleH,blockR,colType,tris\n");

foreach (var r in roots) {
  foreach (var g in UnityEditor.AssetDatabase.FindAssets("t:Prefab", new string[]{r})) {
    var path = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
    var go = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
    if (go == null) continue;

    var inst = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(go);
    inst.transform.position = Vector3.zero;

    var rends = inst.GetComponentsInChildren<Renderer>(false);
    float w = 0, h = 0, d = 0, minY = 0, maxY = 0;
    int tris = 0;
    if (rends.Length > 0) {
      var b = rends[0].bounds;
      for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
      w = b.size.x; h = b.size.y; d = b.size.z; minY = b.min.y; maxY = b.max.y;
    }
    foreach (var mf in inst.GetComponentsInChildren<MeshFilter>(false))
      if (mf.sharedMesh != null) tris += mf.sharedMesh.triangles.Length / 3;

    // blockR = widest horizontal half-extent of any collider, in world scale
    float br = 0f; string ct = "NONE";
    foreach (var c in inst.GetComponentsInChildren<Collider>(false)) {
      var s = c.transform.lossyScale;
      if (c is CapsuleCollider) {
        var cc = (CapsuleCollider)c;
        br = Mathf.Max(br, cc.radius * Mathf.Max(s.x, s.z)); ct = "capsule";
      } else if (c is BoxCollider) {
        var bc = (BoxCollider)c;
        br = Mathf.Max(br, Mathf.Max(bc.size.x * s.x, bc.size.z * s.z) * 0.5f);
        ct = (ct == "capsule" ? "capsule+box" : "box");
      } else if (c is SphereCollider) {
        var sc = (SphereCollider)c;
        br = Mathf.Max(br, sc.radius * Mathf.Max(s.x, s.z)); ct = "sphere";
      } else if (c is MeshCollider) {
        ct = "mesh"; br = Mathf.Max(br, Mathf.Max(w, d) * 0.5f);
      }
    }

    sb.Append(go.name + "," + System.IO.Path.GetFileName(r) + ","
      + w.ToString("F2") + "," + h.ToString("F2") + "," + d.ToString("F2") + ","
      + Mathf.Max(w, d).ToString("F2") + "," + minY.ToString("F2") + ","
      + maxY.ToString("F2") + "," + br.ToString("F2") + "," + ct + "," + tris + "\n");

    GameObject.DestroyImmediate(inst);
  }
}

System.IO.File.WriteAllText(
  System.IO.Path.Combine(Application.dataPath, "../Tools/vegetation/veg_sizes.csv"),
  sb.ToString());
return "written, rows=" + (sb.ToString().Split('\n').Length - 2);
