using System;
using System.Collections.Generic;
namespace desBot
{
    /// <summary>
    /// The enforcer of any given ban
    /// </summary>
    public enum BanEnforcement
    {
        /// <summary>
        /// The ban is enforced by Q
        /// </summary>
        ByQ,

        /// <summary>
        /// The ban is enforced by me
        /// </summary>
        ByMe,

        /// <summary>
        /// The ban is on the channel only
        /// </summary>
        ByChannel,

        /// <summary>
        /// The ban is a JustinTV ban
        /// </summary>
        ByJtv,
    }

    /// <summary>
    /// An entry in the ban list
    /// </summary>
    public class Ban : IKeyInsideValue<string>
    {
        public HostMask Mask { get; set; }
        public DateTime Expires { get; set; }
        public string SetBy { get; set; }
        public string Reason { get; set; }
        public string Affected { get; set; }
        public BanEnforcement Enforcer { get; set; }

        public string GetKey()
        {
            return Mask.Mask;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is Ban)) return false;
            return Mask.Mask == ((Ban)obj).Mask.Mask;
        }
        public override int GetHashCode()
        {
            return Mask.GetHashCode();
        }
    }

    /// <summary>
    /// Serializable version of the Ban class
    /// </summary>
    [Serializable]
    public class SerializableBan
    {
        public string HostMask;
        public DateTime Expires;
        public string SetBy;
        public string Reason;
        public string Affected;
        public int Enforcer = 0;
    }

    /// <summary>
    /// Utility to serialize bans
    /// </summary>
    public class BanSerializer : ISerializer<Ban, SerializableBan>
    {
        public SerializableBan Serialize(Ban ban)
        {
            SerializableBan result = new SerializableBan();
            result.HostMask = ban.Mask.Mask;
            result.Reason = ControlCharacter.Serialize(ban.Reason);
            result.Expires = ban.Expires;
            result.SetBy = ban.SetBy;
            result.Affected = ban.Affected;
            result.Enforcer = (int)ban.Enforcer;
            return result;
        }
        public Ban Deserialize(SerializableBan serialized)
        {
            Ban result = new Ban();
            result.Mask = new HostMask(serialized.HostMask);
            result.Reason = ControlCharacter.Deserialize(serialized.Reason);
            result.Expires = serialized.Expires;
            result.SetBy = serialized.SetBy;
            result.Affected = serialized.Affected;
            result.Enforcer = (BanEnforcement)serialized.Enforcer;
            return result;
        }
    }
}