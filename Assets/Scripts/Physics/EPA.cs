using System.Collections.Generic;
using UnityEngine;
using System;
public static class EPA {


    public class Face
    {
        public Vector3 A, B, C;
        public Vector3 Normal;
        public float Distance;

        public Face(Vector3 a, Vector3 b, Vector3 c)
        {
            A = a;
            B = b;
            C = c; 
            Normal = Vector3.Cross(b - a, c - a).normalized;

            // Ensure normal points outward (toward the origin)
            if (Vector3.Dot(Normal, a) > 0)
                Normal = -Normal;

            Distance = Mathf.Abs(Vector3.Dot(Normal, a));
        }
    }



    private const int MaxIterations = 64;
    private const float Epsilon = 1f;

    public static EPAResult Expand(Simplex simplex, System.Func<Vector3, Vector3> support)
    {
        EPAResult result = new EPAResult { Success = false };

        // 0) تأكد أن simplex يحتوي على 4 نقاط
        if (simplex == null || simplex.Count < 4)
            return result;

        // 1) بناء polytope ابتدائي من simplex (tetrahedron)
        List<Face> faces = BuildInitialPolytope(simplex);
        if (faces == null || faces.Count == 0)
            return result; // لا وجوه صالحة → فشل

        for (int iter = 0; iter < MaxIterations; iter++)
        {
            // 2) الحصول على أقرب وجه للأصل
            Face closest = FindClosestFace(faces);

            // 3) دعم نقطة جديدة
            Vector3 p = support(closest.Normal);
            float dist = Vector3.Dot(p, closest.Normal);

            // 4) تحقق من التقارب
            if (dist - closest.Distance < Epsilon)
            {
                // أحسب ContactPoint كمركز (centroid) لرؤوس الوجه
                Vector3 cp = (closest.A + closest.B + closest.C) / 3f;

                result.Success          = true;
                result.Normal           = closest.Normal;
                result.PenetrationDepth = dist;
                result.ContactPoint     = cp;    // ← نقطة الاتصال المعدّلة
                return result;
            }

            // 5) توسيع polytope
            AddVertexToPolytope(p, faces);
            if (faces.Count == 0)
                break; // إذا أخفينا كل الوجوه عن طريق الخطأ
        }

        return result;
    }



    private static List<Face> BuildInitialPolytope(Simplex simplex)
    {
        var pts = simplex.Points;
        if (pts == null || pts.Count != 4)
            return null;

        return new List<Face>
        {
            new Face(pts[0], pts[1], pts[2]),
            new Face(pts[0], pts[3], pts[1]),
            new Face(pts[0], pts[2], pts[3]),
            new Face(pts[1], pts[3], pts[2])
        };
    }


    private static Face FindClosestFace(List<Face> faces)
    {

        if (faces == null || faces.Count == 0)
        {
            throw new System.ArgumentException("Faces list is empty.");
        }

        Face closestFace = faces[0];
        float minDistance = closestFace.Distance;

        for (int i = 1; i < faces.Count; i++)
        {
            if (faces[i].Distance < minDistance)
            {
                closestFace = faces[i];
                minDistance = faces[i].Distance;
            }
        }

        return closestFace;
    }

    private struct Edge
    {
        public Vector3 A, B;

        public Edge(Vector3 a, Vector3 b)
        {
            A = a;
            B = b;
        }

        // For edge comparison in dictionaries/sets
        public override bool Equals(object obj)
        {
            if (!(obj is Edge)) return false;
            Edge other = (Edge)obj;
            return (A == other.A && B == other.B) || (A == other.B && B == other.A);
        }

        public override int GetHashCode()
        {
            return A.GetHashCode() ^ B.GetHashCode();
        }
    }

    private static void AddVertexToPolytope(Vector3 newVertex, List<Face> faces)
    {
        List<Face> toRemove = new List<Face>();
        HashSet<Edge> edgeSet = new HashSet<Edge>();

        // Step 1: Find visible faces
        foreach (var face in faces)
        {
            if (Vector3.Dot(face.Normal, newVertex - face.A) > 0)
            {
                toRemove.Add(face);

                // Collect face edges (for silhouette edge detection)
                AddEdge(edgeSet, new Edge(face.A, face.B));
                AddEdge(edgeSet, new Edge(face.B, face.C));
                AddEdge(edgeSet, new Edge(face.C, face.A));
            }
        }

        // Step 2: Remove visible faces
        foreach (var face in toRemove)
        {
            faces.Remove(face);
        }

        // Step 3: Build new faces from silhouette edges to newVertex
        foreach (var edge in edgeSet)
        {
            faces.Add(new Face(edge.A, edge.B, newVertex));
        }
    }

    private static void AddEdge(HashSet<Edge> edges, Edge edge)
    {
        if (!edges.Remove(edge))
            edges.Add(edge); // only keep edges that appear once (silhouette)
    }



}