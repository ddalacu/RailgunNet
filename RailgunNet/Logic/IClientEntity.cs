using RailgunNet.Connection.Client;
using RailgunNet.Factory;
using RailgunNet.Logic.Wrappers;
using RailgunNet.System.Types;

namespace RailgunNet.Logic
{
    public interface IClientEntity : IEntity
    {
        RailClientRoom Room { get; }
        void HandleEvent(RailEvent @event);

        bool ReceiveDelta(RailStateDelta delta);
        void Added(RailClientRoom railClientRoom);
        void Removed();

        void PrimeState(RailStateDelta delta);
        void Initialize(EntityId id, RailClient client);
        bool HasReadyState(Tick serverTick);
    }
}