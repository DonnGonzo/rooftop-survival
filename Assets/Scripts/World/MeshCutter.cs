using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cuts a box-shaped hole out of a model's meshes: used to give street buildings a real doorway.
/// Triangles are clipped exactly against the box, so the hole has clean straight edges whatever
/// the model's own triangle layout. Each cut mesh is a private copy; the shared asset is untouched.
/// </summary>
public static class MeshCutter
{
    struct V
    {
        public Vector3 p, n; public Vector2 uv; public Color c;
        public static V Lerp(V a, V b, float t)
        {
            return new V { p = Vector3.LerpUnclamped(a.p, b.p, t), n = Vector3.LerpUnclamped(a.n, b.n, t), uv = Vector2.LerpUnclamped(a.uv, b.uv, t), c = Color.LerpUnclamped(a.c, b.c, t) };
        }
    }

    /// <summary>
    /// Removes everything inside 'box' (given in the space that 'toBox' maps world positions into).
    /// Returns how many triangles were touched; 0 means nothing was in the way or the mesh could not be read.
    /// </summary>
    public static int CutBox(GameObject model, Matrix4x4 toBox, Bounds box)
    {
        int touched = 0;
        foreach (var mf in model.GetComponentsInChildren<MeshFilter>())
        {
            var src = mf.sharedMesh;
            if (src == null || !src.isReadable) continue;
            Matrix4x4 m = toBox * mf.transform.localToWorldMatrix;   // mesh space -> box space
            // Skip meshes that do not reach the box at all.
            var wb = src.bounds; bool near = false;
            Vector3 mn = Vector3.positiveInfinity, mx = Vector3.negativeInfinity;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = m.MultiplyPoint3x4(wb.center + Vector3.Scale(wb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                mn = Vector3.Min(mn, corner); mx = Vector3.Max(mx, corner);
            }
            var mb = new Bounds(); mb.SetMinMax(mn, mx); near = mb.Intersects(box);
            if (!near) continue;

            var pos = new List<Vector3>(); src.GetVertices(pos);
            var nor = new List<Vector3>(); src.GetNormals(nor);
            var uvs = new List<Vector2>(); src.GetUVs(0, uvs);
            var col = new List<Color>(); src.GetColors(col);
            bool hasN = nor.Count == pos.Count, hasUv = uvs.Count == pos.Count, hasC = col.Count == pos.Count;
            var boxPos = new Vector3[pos.Count];
            for (int i = 0; i < pos.Count; i++) boxPos[i] = m.MultiplyPoint3x4(pos[i]);

            var outV = new List<V>(pos.Count);
            for (int i = 0; i < pos.Count; i++)
                outV.Add(new V { p = pos[i], n = hasN ? nor[i] : Vector3.up, uv = hasUv ? uvs[i] : Vector2.zero, c = hasC ? col[i] : Color.white });
            var outTris = new List<int>[src.subMeshCount];
            int meshTouched = 0;
            var poly = new List<V>(8); var polyB = new List<Vector3>(8);

            for (int sm = 0; sm < src.subMeshCount; sm++)
            {
                outTris[sm] = new List<int>();
                if (src.GetTopology(sm) != MeshTopology.Triangles) { outTris[sm] = null; continue; }
                var tris = src.GetTriangles(sm);
                for (int t = 0; t < tris.Length; t += 3)
                {
                    int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                    Vector3 pa = boxPos[a], pb = boxPos[b], pc = boxPos[c];
                    var tb = new Bounds(pa, Vector3.zero); tb.Encapsulate(pb); tb.Encapsulate(pc);
                    if (!tb.Intersects(box)) { outTris[sm].Add(a); outTris[sm].Add(b); outTris[sm].Add(c); continue; }

                    // Peel off the parts outside each of the box's six faces; what is left is inside and is dropped.
                    poly.Clear(); polyB.Clear();
                    poly.Add(outV[a]); poly.Add(outV[b]); poly.Add(outV[c]);
                    polyB.Add(pa); polyB.Add(pb); polyB.Add(pc);
                    meshTouched++;
                    for (int axis = 0; axis < 3 && poly.Count >= 3; axis++)
                    {
                        Peel(poly, polyB, axis, box.min[axis], false, outV, outTris[sm]);
                        if (poly.Count < 3) break;
                        Peel(poly, polyB, axis, box.max[axis], true, outV, outTris[sm]);
                    }
                }
            }
            if (meshTouched == 0) continue;
            touched += meshTouched;

            var cut = new Mesh { name = src.name + "_cut", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            var p2 = new List<Vector3>(outV.Count); var n2 = new List<Vector3>(outV.Count); var u2 = new List<Vector2>(outV.Count); var c2 = new List<Color>(outV.Count);
            foreach (var v in outV) { p2.Add(v.p); n2.Add(v.n.normalized); u2.Add(v.uv); c2.Add(v.c); }
            cut.SetVertices(p2);
            if (hasN) cut.SetNormals(n2);
            if (hasUv) cut.SetUVs(0, u2);
            if (hasC) cut.SetColors(c2);
            cut.subMeshCount = src.subMeshCount;
            for (int sm = 0; sm < src.subMeshCount; sm++)
            {
                if (outTris[sm] != null) cut.SetTriangles(outTris[sm], sm);
                else cut.SetIndices(src.GetIndices(sm), src.GetTopology(sm), sm);
            }
            if (!hasN) cut.RecalculateNormals();
            cut.RecalculateBounds();
            mf.sharedMesh = cut;
        }
        return touched;
    }

    /// <summary>
    /// Splits the polygon at the plane axis = value. The part beyond the plane (above it when 'above',
    /// otherwise below) is kept in the mesh; the polygon is replaced by the part on the box's side.
    /// </summary>
    static void Peel(List<V> poly, List<Vector3> polyB, int axis, float value, bool above, List<V> outV, List<int> outTris)
    {
        var keep = new List<V>(8); var inV = new List<V>(8); var inB = new List<Vector3>(8);
        int n = poly.Count;
        for (int i = 0; i < n; i++)
        {
            V v0 = poly[i], v1 = poly[(i + 1) % n];
            Vector3 b0 = polyB[i], b1 = polyB[(i + 1) % n];
            float d0 = above ? b0[axis] - value : value - b0[axis];   // > 0 means outside the box: keep
            float d1 = above ? b1[axis] - value : value - b1[axis];
            if (d0 > 0f) keep.Add(v0); else { inV.Add(v0); inB.Add(b0); }
            if ((d0 > 0f) != (d1 > 0f))
            {
                float t = d0 / (d0 - d1);
                V mid = V.Lerp(v0, v1, t); Vector3 midB = Vector3.LerpUnclamped(b0, b1, t);
                keep.Add(mid); inV.Add(mid); inB.Add(midB);
            }
        }
        if (keep.Count >= 3)
        {
            int first = outV.Count;
            outV.AddRange(keep);
            for (int i = 1; i < keep.Count - 1; i++) { outTris.Add(first); outTris.Add(first + i); outTris.Add(first + i + 1); }
        }
        poly.Clear(); poly.AddRange(inV); polyB.Clear(); polyB.AddRange(inB);
    }
}
