using System;
using System.Collections.Generic;
using g3;


namespace gs
{
	// [TODO] find a way to not hardcode this??
	using LinearToolpath = LinearToolpath3<PrintVertex>;


	/// <summary>
	/// Utility class to simplify ToolpathSet construction.
	/// </summary>
	public class ToolpathSetBuilder
	{
		public ToolpathSet Paths;

		static readonly Vector2d NO_DIM = GCodeUtil.UnspecifiedDimensions;
		static readonly double NO_RATE = GCodeUtil.UnspecifiedValue;

		Vector3d currentPos;
		public Vector3d Position {
			get { return currentPos; }
		}

		Vector2d currentDims = GCodeUtil.UnspecifiedDimensions;
		public Vector2d Dimensions {
			get { return currentDims; }
		}

        public ToolpathTypes MoveType = ToolpathTypes.Deposition;


		public ToolpathSetBuilder(ToolpathSet paths = null)
		{
			Paths = (paths == null) ? new ToolpathSet() : paths;
		}

		public void Initialize(Vector3d startPos) {
			currentPos = startPos;
		}

		public virtual void AppendPath(IToolpath p) {
            if (IsCommandToolpath(p)) {
                Paths.Append(p);

            } else {

                if (!currentPos.EpsilonEqual(p.StartPosition, MathUtil.Epsilon))
                    throw new Exception("PathSetBuilder.AppendPath: disconnected path");
                Paths.Append(p);
                currentPos = p.EndPosition;
            }
        }

		public virtual Vector3d AppendZChange(double ZDelta, double fSpeed) {
			LinearToolpath zup = new LinearToolpath(ToolpathTypes.PlaneChange);
			zup.AppendVertex(new PrintVertex(currentPos, NO_RATE, NO_DIM), TPVertexFlags.IsPathStart);
			Vector3d toPos = new Vector3d(currentPos); 
			toPos.z += ZDelta;
			zup.AppendVertex(new PrintVertex(toPos, fSpeed, NO_DIM), TPVertexFlags.IsPathEnd);
			AppendPath(zup);
			return currentPos;
		}


		public virtual Vector3d AppendTravel(Vector2d toPos, double fSpeed)
		{
			return AppendTravel(new Vector3d(toPos.x, toPos.y, currentPos.z), fSpeed);
		}
		public virtual Vector3d AppendTravel(Vector3d toPos, double fSpeed)
		{
			LinearToolpath travel = new LinearToolpath(ToolpathTypes.Travel);
			travel.AppendVertex(new PrintVertex(currentPos, NO_RATE, NO_DIM), TPVertexFlags.IsPathStart);
			travel.AppendVertex(new PrintVertex(toPos, fSpeed, NO_DIM), TPVertexFlags.IsPathEnd);
			if ( travel.Length > MathUtil.Epsilonf )		// discard tiny travels
				AppendPath(travel);
			return currentPos;
		}
		public virtual Vector3d AppendTravel(List<Vector2d> toPoints, double fSpeed)
        {
			LinearToolpath travel = new LinearToolpath(ToolpathTypes.Travel);
			travel.AppendVertex(new PrintVertex(currentPos, NO_RATE, NO_DIM), TPVertexFlags.IsPathStart);
            for ( int k = 0; k < toPoints.Count; ++k ) {
                Vector2d pos2 = toPoints[k];
				Vector3d pos = new Vector3d(pos2.x, pos2.y, currentPos.z);
                TPVertexFlags flag = (k == toPoints.Count - 1) ? TPVertexFlags.IsPathEnd : TPVertexFlags.None;
				travel.AppendVertex(new PrintVertex(pos, fSpeed, NO_DIM), flag);
			}
			if (travel.Length > MathUtil.Epsilonf)
				AppendPath(travel);
			return currentPos;
		}
		public virtual Vector3d AppendTravel(List<Vector3d> toPoints, double fSpeed)
        {
			LinearToolpath travel = new LinearToolpath(ToolpathTypes.Travel);
			travel.AppendVertex(new PrintVertex(currentPos, NO_RATE, NO_DIM), TPVertexFlags.IsPathStart);
            for ( int k = 0; k < toPoints.Count; ++k ) {
                Vector3d pos = toPoints[k];
                TPVertexFlags flag = (k == toPoints.Count - 1) ? TPVertexFlags.IsPathEnd : TPVertexFlags.None;
                travel.AppendVertex(new PrintVertex(pos, fSpeed, NO_DIM), flag);
			}
			if (travel.Length > MathUtil.Epsilonf)
				AppendPath(travel);
			return currentPos;
		}



        public virtual Vector3d AppendExtrude(Vector2d toPos, double fSpeed,
            FillTypeFlags pathTypeFlags = FillTypeFlags.Unknown)
        {
            return AppendExtrude(new Vector3d(toPos.x, toPos.y, currentPos.z), fSpeed, pathTypeFlags);
        }
        public virtual Vector3d AppendExtrude(Vector3d toPos, double fSpeed,
            FillTypeFlags pathTypeFlags = FillTypeFlags.Unknown)
        {
            LinearToolpath extrusion = new LinearToolpath(MoveType);
            extrusion.TypeModifiers = pathTypeFlags;
			extrusion.AppendVertex(new PrintVertex(currentPos, NO_RATE, currentDims), TPVertexFlags.IsPathStart);
			extrusion.AppendVertex(new PrintVertex(toPos, fSpeed, currentDims), TPVertexFlags.IsPathEnd);
            AppendPath(extrusion);
            return currentPos;
        }



        public virtual Vector3d AppendExtrude(List<Vector2d> toPoints, double fSpeed, 
            FillTypeFlags pathTypeFlags = FillTypeFlags.Unknown,
            List<TPVertexFlags> perVertexFlags = null )
        {
			return AppendExtrude(toPoints, fSpeed, currentDims, pathTypeFlags, perVertexFlags);
		}
		public virtual Vector3d AppendExtrude(List<Vector2d> toPoints, 
              double fSpeed, Vector2d dimensions,
			  FillTypeFlags pathTypeFlags = FillTypeFlags.Unknown,
			  List<TPVertexFlags> perVertexFlags = null)
		{
			Vector2d useDims = currentDims;
			if (dimensions.x > 0 && dimensions.x != NO_DIM.x)
				useDims.x = dimensions.x;
			if (dimensions.y > 0 && dimensions.y != NO_DIM.y)
				useDims.y = dimensions.y;

			LinearToolpath extrusion = new LinearToolpath(MoveType);

            extrusion.TypeModifiers = pathTypeFlags;
			extrusion.AppendVertex(new PrintVertex(currentPos, NO_RATE, useDims), TPVertexFlags.IsPathStart);

            for (int k = 0; k < toPoints.Count; ++k) {
                Vector3d pos = new Vector3d(toPoints[k].x, toPoints[k].y, currentPos.z);
                TPVertexFlags flag = (k == toPoints.Count - 1) ? TPVertexFlags.IsPathEnd : TPVertexFlags.None;
				extrusion.AppendVertex(new PrintVertex(pos, fSpeed, useDims), flag);
            }
            if (perVertexFlags != null)
                ToolpathUtil.AddPerVertexFlags(extrusion, perVertexFlags);
            AppendPath(extrusion);
			return currentPos;
		}






		public virtual Vector3d AppendExtrude(List<Vector3d> toPoints, double fSpeed, 
            FillTypeFlags pathTypeFlags = FillTypeFlags.Unknown,
            List<TPVertexFlags> perVertexFlags = null)
        {
			LinearToolpath extrusion = new LinearToolpath(MoveType);
            extrusion.TypeModifiers = pathTypeFlags;
			extrusion.AppendVertex(new PrintVertex(currentPos, NO_RATE, currentDims), TPVertexFlags.IsPathStart);
			for (int k = 0; k < toPoints.Count; ++k) {
                TPVertexFlags flag = (k == toPoints.Count - 1) ? TPVertexFlags.IsPathEnd : TPVertexFlags.None;
				extrusion.AppendVertex(new PrintVertex(toPoints[k], fSpeed, currentDims), flag);
            }
            if (perVertexFlags != null)
                ToolpathUtil.AddPerVertexFlags(extrusion, perVertexFlags);
			AppendPath(extrusion);
			return currentPos;
		}



		/// <summary>
		/// Dwell for a number of milliseconds (1000ms=1s). Optionally, retract/unretract.
		/// </summary>
		public virtual void AppendDwell(int ms, bool bRetract)
		{
			AssemblerCommandsToolpath dwell_path = new AssemblerCommandsToolpath() {
				AssemblerF = (iassembler, icomplier) => {
					BaseDepositionAssembler asm = iassembler as BaseDepositionAssembler;
					if (asm == null)
						throw new Exception("ToolpathSetBuilder.AppendDwell: unsupported assembler");
					asm.FlushQueues();
					if ( bRetract )
						asm.BeginRetractRelativeDist(asm.NozzlePosition, 9999, -1.0f);
					asm.AppendDwell(ms);
					if (bRetract)
						asm.EndRetract(asm.NozzlePosition, 9999);					
				}
			};
			AppendPath(dwell_path);			
		}


        /// <summary>
        /// Command toolpaths are used to pass special commands/etc to compiler.
        /// These toolpaths do not affect the current extruder position/etc.
        /// </summary>
        protected virtual bool IsCommandToolpath(IToolpath toolpath)
        {
            return toolpath.Type == ToolpathTypes.Custom
                || toolpath.Type == ToolpathTypes.CustomAssemblerCommands;
        }


    }
}
