using RailgunNet.Factory;
using RailgunNet.System.Buffer;
using RailgunNet.System.Types;
using RailgunNet.Util.Debug;
using RailgunNet.Util.Pooling;

namespace RailgunNet.Logic.Wrappers
{
    /// <summary>
    ///     Used to differentiate/typesafe state records. Not strictly necessary.
    /// </summary>
    public class RailStateRecord : IRailTimedValue, IRailPoolable<RailStateRecord>
    {
        private RailState state;

        private Tick tick;

        public RailStateRecord()
        {
            state = null;
            tick = Tick.INVALID;
        }

        public bool IsValid => tick.IsValid;
        public RailState State => state;
        private Tick Tick => tick;

        #region Interface
        Tick IRailTimedValue.Tick => tick;
        #endregion

        public void Overwrite(IRailStateConstruction stateCreator, Tick tick, RailState state)
        {
            RailDebug.Assert(tick.IsValid);

            this.tick = tick;
            if (this.state == null)
            {
                this.state = state.Clone(stateCreator);
            }
            else
            {
                this.state.OverwriteFrom(state);
            }
        }

        public void Invalidate()
        {
            tick = Tick.INVALID;
        }

        private void Reset()
        {
            tick = Tick.INVALID;
            RailPool.SafeReplace(ref state, null);
        }

        #region Pooling
        IRailMemoryPool<RailStateRecord> IRailPoolable<RailStateRecord>.OwnerPool { get; set; }

        void IRailPoolable<RailStateRecord>.Reset()
        {
            Reset();
        }

        void IRailPoolable<RailStateRecord>.Allocated()
        {
        }
        #endregion
    }
}
