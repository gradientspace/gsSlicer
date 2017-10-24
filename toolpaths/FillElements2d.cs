using System;
using System.Collections.Generic;
using g3;

namespace gs
{
	[Flags]
	public enum PathTypeFlags
	{
		Unknown = 0,

		PerimeterShell = 1,
		OutermostShell = 1<<1,
		OuterPerimeter = PerimeterShell | OutermostShell,

		InteriorShell = 1<<2,

		OpenShellPath = 1<<3,    // ie for single-line-wide features

		SolidInfill = 1<<8,
		SparseInfill = 1<<9
	}



	/// <summary>
	/// Additive polygon path
	/// </summary>
	public class FillPolygon2d : Polygon2d
	{
		public PathTypeFlags TypeFlags = PathTypeFlags.Unknown;

		public bool HasTypeFlag(PathTypeFlags f) {
			return (TypeFlags & f) != 0;
		}

		public FillPolygon2d() : base()
		{
		}

		public FillPolygon2d(Vector2d[] v) : base(v)
		{
		}

		public FillPolygon2d(Polygon2d p) : base(p)
		{
		}	
	}





	/// <summary>
	/// Additive polyline path
	/// </summary>
	public class FillPolyline2d : PolyLine2d
	{
		public PathTypeFlags TypeFlags = PathTypeFlags.Unknown;

		public bool HasTypeFlag(PathTypeFlags f) {
			return (TypeFlags & f) == f;
		}

		// [TODO] maybe remove? see below.
		List<Index3i> flags;
		bool has_flags = false;

		public FillPolyline2d() : base()
		{
		}

		public FillPolyline2d(Vector2d[] v) : base(v)
		{
		}

		public FillPolyline2d(PolyLine2d p) : base(p)
		{
		}

		void alloc_flags()
		{
			if (flags == null) {
				flags = new List<Index3i>();
				for (int i = 0; i < vertices.Count; ++i)
					flags.Add(Index3i.Zero);
			}
		}

		public override void AppendVertex(Vector2d v)
		{
			base.AppendVertex(v);
			if (flags != null)
				flags.Add(Index3i.Zero);
		}
		public override void AppendVertices(IEnumerable<Vector2d> v)
		{
			base.AppendVertices(v);
			if (flags != null) {
				foreach (var x in v)
					flags.Add(Index3i.Zero);
			}
		}

		public override void Reverse()
		{
			base.Reverse();
			if (flags != null)
				flags.Reverse();
		}
		public override void Simplify(double clusterTol = 0.0001,
										double lineDeviationTol = 0.01,
									  bool bSimplifyStraightLines = true)
		{
			throw new Exception("not supported yet...");
		}


		public void AppendVertex(Vector2d v, Index3i flag)
		{
			alloc_flags();
			base.AppendVertex(v);
			flags.Add(flag);
			has_flags = true;
		}
		public void AppendVertices(IEnumerable<Vector2d> v, IEnumerable<Index3i> f)
		{
			alloc_flags();
			base.AppendVertices(v);
			flags.AddRange(f);
			has_flags = true;
		}


		// [RMS] this is *only* used for PathUtil.ConnectorVFlags. Maybe remove this capability?
		public Index3i GetFlag(int i) { return (flags == null) ? Index3i.Zero : flags[i]; }
		public void SetFlag(int i, Index3i flag) { alloc_flags(); flags[i] = flag; }

		public bool HasFlags {
			get { return flags != null && has_flags; }
		}
		public IReadOnlyList<Index3i> Flags() { return flags.AsReadOnly(); }
	}
}
