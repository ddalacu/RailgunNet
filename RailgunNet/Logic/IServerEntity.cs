using RailgunNet.Connection.Server;
using RailgunNet.Factory;
using RailgunNet.Logic.Wrappers;
using RailgunNet.System.Types;

namespace RailgunNet.Logic
{
    public interface IServerEntity : IEntity
    {
        RailServerRoom Room { get; }
        RailServerPeer Controller { get; }
        void Initialize(EntityId id);
        void Added(RailServerRoom railServerRoom);
        void Removed();
        void MarkForRemoval();
        void HandleEvent(RailEvent @event, RailServerPeer railServerPeer);
        void AssignController(RailServerPeer railServerPeer);

        RailStateDelta ProduceDelta(IRailStateConstruction stateCreator, Tick latestLastReceivedTick, RailServerPeer target, bool forceAllMutable);

        void StoreRecord(IRailStateConstruction stateCreator);
    }
}