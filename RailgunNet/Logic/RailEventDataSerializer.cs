using System;
using System.Collections.Generic;
using System.Reflection;
using RailgunNet.System.Encoding;

namespace RailgunNet.Logic
{
    public class RailEventDataSerializer
    {
        private readonly RailEvent eventInstance;
        private readonly List<IRailSynchronized> members = new List<IRailSynchronized>();

        public RailEventDataSerializer(RailEvent instance)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            eventInstance = instance;
            foreach (var prop in instance
                                          .GetType()
                                          .GetProperties(
                                              BindingFlags.Instance |
                                              BindingFlags.Public |
                                              BindingFlags.NonPublic))
            {
                if (Attribute.IsDefined(prop, typeof(EventDataAttribute)))
                {
                    members.Add(RailSynchronizedFactory.Create(instance, prop));
                }
            }
        }

        public void SetDataFrom(RailEventDataSerializer other)
        {
            if (eventInstance.GetType() != other.eventInstance.GetType())
            {
                throw new ArgumentException(
                    $"The instance to copy from is not for the same event type. Expected {eventInstance.GetType()}, got {other.eventInstance.GetType()}.",
                    nameof(other));
            }

            var membersCount = members.Count;
            for (var i = 0; i < membersCount; ++i)
                members[i].ApplyFrom(other.members[i]);
        }

        public void WriteData(RailBitBuffer buffer)
        {
            var membersCount = members.Count;
            for (var i = 0; i < membersCount; ++i)
                members[i].WriteTo(buffer);
        }

        public void ReadData(RailBitBuffer buffer)
        {
            var membersCount = members.Count;
            for (var i = 0; i < membersCount; ++i)
                members[i].ReadFrom(buffer);
        }

        public void ResetData()
        {
            var membersCount = members.Count;
            for (var i = 0; i < membersCount; ++i)
                members[i].Reset();
        }
    }
}
