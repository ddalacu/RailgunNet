using System;
using JetBrains.Annotations;
using RailgunNet.Connection.Traffic;
using RailgunNet.Factory;

namespace RailgunNet.Connection
{
    /// <summary>
    ///     Server is the core executing class for communication. It is responsible
    ///     for managing connection contexts and payload I/O.
    /// </summary>
    public abstract class RailConnection
    {
        public event Action Started;

        private bool hasStarted;

        protected RailResource Resource { get; }

        protected RailInterpreter Interpreter { get; }


        protected RailConnection(RailRegistry registry)
        {
            Resource = new RailResource(registry);
            Interpreter = new RailInterpreter();
            hasStarted = false;
        }

        protected void DoStart()
        {
            if (!hasStarted)
            {
                hasStarted = true;
                Started?.Invoke();
            }
        }
    }
}
