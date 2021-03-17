using RailgunNet.Util;

namespace RailgunNet.Logic.Scope
{
    public class RailScopeEvaluator
    {
        public virtual bool Evaluate(RailEvent evnt)
        {
            return true;
        }

        public virtual bool Evaluate(
            IServerEntity entity,
            int ticksSinceSend,
            int ticksSinceAck,
            out float priority)
        {
            priority = 0.0f;
            return true;
        }
    }
}
