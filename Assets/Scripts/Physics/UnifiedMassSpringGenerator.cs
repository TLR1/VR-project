using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System;

namespace Physics
{
	[RequireComponent(typeof(MeshFilter), typeof(MassSpringBody))]
	public class UnifiedMassSpringGenerator : MonoBehaviour
	{
		MeshFilter mf;
		MassSpringBody body;

		[Header("Voxel Grid (Local)")]
		public Vector3Int dimensions = new Vector3Int(5, 5, 5);
		public float voxelSize = 0.1f;

		[Header("Springs (Mutual k-NN)")]
		[Tooltip("عدد الجيران الأقرب لكل فوكسل")]
		public int connectionsPerVoxel = 6;
		public float stiffness = 1000f;
		public float damping = 50f;

		[Header("Visualization (optional)")]
		public GameObject voxelPrefab;
		List<GameObject> instances = new List<GameObject>();

		void Awake()
		{
			mf = GetComponent<MeshFilter>();
			body = GetComponent<MassSpringBody>();
			Generate();
		}

		void Generate()
		{
			var mesh = mf.sharedMesh;
			if (mesh == null) return;

			// نظّف القوائم
			body.Points.Clear();
			body.Springs.Clear();
			instances.ForEach(Destroy);
			instances.Clear();

			// 1) surface voxels من رؤوس الميش
			foreach (var localV in mesh.vertices.Distinct())
			{
				AddPoint(localV, mesh);
			}

			// 2) interior voxels من توزيع منتظم داخل bounds المحلّي
			Bounds lb = mesh.bounds;
			Vector3 min = lb.min;
			Vector3 size = lb.size;
			Vector3 step = new Vector3(
				size.x / (dimensions.x - 1),
				size.y / (dimensions.y - 1),
				size.z / (dimensions.z - 1)
			);

			for (int x = 0; x < dimensions.x; x++)
				for (int y = 0; y < dimensions.y; y++)
					for (int z = 0; z < dimensions.z; z++)
					{
						Vector3 localP = min + Vector3.Scale(step, new Vector3(x, y, z));
						if (IsInsideWinding(localP, mesh))
							AddPoint(localP, mesh);
					}

			// 3) ربط النيابات بنوابض Mutual k-NN
			int n = body.Points.Count;
			var nbrs = new List<List<int>>(n);
			for (int i = 0; i < n; i++)
			{
				var pi = body.Points[i];
				nbrs.Add(
					body.Points
						.Select((p, idx) => new { idx, d2 = (p.Position - pi.Position).sqrMagnitude })
						.Where(x => x.idx != i)
						.OrderBy(x => x.d2)
						.Take(connectionsPerVoxel)
						.Select(x => x.idx)
						.ToList()
				);
			}
			for (int i = 0; i < n; i++)
				foreach (int j in nbrs[i])
					if (j > i && nbrs[j].Contains(i))
						body.Springs.Add(new SpringLink(
							body.Points[i],
							body.Points[j],
							stiffness, damping
						));
		}

		void AddPoint(Vector3 localP, Mesh mesh)
		{
			Vector3 worldP = transform.TransformPoint(localP);
			var mp = new MassPoint { Position = worldP, PreviousPosition = worldP };
			body.Points.Add(mp);
			if (voxelPrefab != null)
			{
				var go = Instantiate(voxelPrefab, worldP, Quaternion.identity, transform);
				go.transform.localScale = Vector3.one * voxelSize;
				instances.Add(go);
			}
		}

		void Update()
		{
			int c = Mathf.Min(instances.Count, body.Points.Count);
			for (int i = 0; i < c; i++)
				instances[i].transform.position = body.Points[i].Position;
		}

		// --- Winding Number: مجموع الزوايا الصلبة ---
		bool IsInsideWinding(Vector3 localP, Mesh mesh)
		{
			var verts = mesh.vertices;
			var tris = mesh.triangles;
			double totalAngle = 0.0;
			for (int t = 0; t < tris.Length; t += 3)
			{
				Vector3 a = verts[tris[t]] - localP;
				Vector3 b = verts[tris[t + 1]] - localP;
				Vector3 c = verts[tris[t + 2]] - localP;
				totalAngle += SolidAngle(a, b, c);
			}
			// داخل إذا مجموع الزوايا ≈ 4π (نستخدم > π كعتبة آمنة)
			return Math.Abs(totalAngle) > Math.PI;
		}

		double SolidAngle(Vector3 a, Vector3 b, Vector3 c)
		{
			double la = a.magnitude, lb = b.magnitude, lc = c.magnitude;
			double numerator = Vector3.Dot(a, Vector3.Cross(b, c));
			double denominator = la * lb * lc
							   + Vector3.Dot(a, b) * lc
							   + Vector3.Dot(b, c) * la
							   + Vector3.Dot(c, a) * lb;
			return 2.0 * Math.Atan2(numerator, denominator);
		}
	}
}
