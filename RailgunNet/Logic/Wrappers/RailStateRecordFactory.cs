using RailgunNet.Factory;
using RailgunNet.Logic.State;
using RailgunNet.System.Types;

namespace RailgunNet.Logic.Wrappers
{
    public static class RailStateRecordFactory
    {
        /// <summary>
        ///     Creates a record of the current state, taking the latest record (if
        ///     any) into account. If a latest state is given, this function will
        ///     return null if there is no change between the current and latest.
        /// </summary>
        public static RailStateRecord Create(
            IRailStateConstruction stateCreator,
            Tick tick,
            RailState current,
            RailStateRecord latestRecord = null)
        {
            if (latestRecord != null)
            {
                RailState latest = latestRecord.State;
                bool shouldReturn = current.CompareMutableData(latest) > 0 ||
                                    current.IsControllerDataEqual(latest) == false;
                if (shouldReturn == false) return null;
            }

            RailStateRecord record = stateCreator.CreateRecord();
            record.Overwrite(stateCreator, tick, current);
            return record;
        }
    }
}
