namespace RailgunNet.Logic
{
    public interface IServerCommandedEntity : IServerEntity
    {
        void ReceiveCommand(RailCommand command);
    }
}