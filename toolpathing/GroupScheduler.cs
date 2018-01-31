using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using g3;

namespace gs
{
    /// <summary>
    /// GroupScheduler collects up paths and then optimizes their scheduling.
    /// You call BeginGroup() / EndGroup() around semantic groups of paths.
    /// Inside each group, a SortingScheduler is used to re-order the paths.
    /// These oredered paths are then passed to an input IPathScheduler on EndGroup()
    /// </summary>
    public class GroupScheduler : IPathScheduler
    {
        public SchedulerSpeedHint SpeedHint {
            get { return TargetScheduler.SpeedHint; }
            set { TargetScheduler.SpeedHint = value; }
        }

        public IPathScheduler TargetScheduler;

        SortingScheduler CurrentSorter;

        Vector2d lastPoint;
        public Vector2d CurrentPosition {
            get { return lastPoint; }
        }

        public GroupScheduler(IPathScheduler target, Vector2d startPoint)
        {
            TargetScheduler = target;
            lastPoint = startPoint;
        }
        ~GroupScheduler()
        {
            if (CurrentSorter != null)
                throw new Exception("GroupScheduler: still inside a sort group during destructor!");
        }


        public virtual void BeginGroup()
        {
            if (CurrentSorter != null)
                throw new Exception("GroupScheduler.BeginGroup: already in a group!");

            CurrentSorter = new SortingScheduler();
        }

        public virtual void EndGroup()
        {
            if (CurrentSorter != null) {
                CurrentSorter.SortAndAppendTo(lastPoint, TargetScheduler);
                lastPoint = CurrentSorter.OutPoint;
                CurrentSorter = null;
            }
        }

        public virtual bool InGroup {
            get { return CurrentSorter != null; }
        }


        public virtual void AppendPaths(List<FillPaths2d> paths)
        {
            if (CurrentSorter == null) {
                TargetScheduler.AppendPaths(paths);
                throw new Exception("TODO: need to update lastPoint...");
            } else {
                CurrentSorter.SpeedHint = this.SpeedHint;
                CurrentSorter.AppendPaths(paths);
            }
        }
    }




    /// <summary>
    /// This is for testing / debugging
    /// </summary>
    public class PassThroughGroupScheduler : GroupScheduler
    {
        public PassThroughGroupScheduler(IPathScheduler target, Vector2d startPoint) : base(target,startPoint)
        {
        }

        public override void BeginGroup() { }
        public override void EndGroup() { }
        public override bool InGroup {
            get { return false; }
        }

        public override void AppendPaths(List<FillPaths2d> paths) {
            TargetScheduler.AppendPaths(paths);
        }
    }




}
