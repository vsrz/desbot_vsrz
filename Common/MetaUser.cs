using System;
using System.Collections.Generic;
using System.Text;

namespace desBot
{
    /// <summary>
    /// A warning
    /// </summary>
    [Serializable]
    public class Warning
    {
        /// <summary>
        /// The identifier of the warning
        /// </summary>
        public int ID;

        /// <summary>
        /// The person who issued the warning
        /// </summary>
        public string IssuedBy;

        /// <summary>
        /// The reason the warning was issued
        /// </summary>
        public string Reason;

        /// <summary>
        /// The time at which the warning was issued
        /// </summary>
        public DateTime Created;
    }

    /// <summary>
    /// Metadata for a user
    /// Metadata is not based on User, but a set of Users sharing the same hostmask (ie, renamed users)
    /// </summary>
    [Serializable]
    public class MetaUser : IKeyInsideValue<string>
    {
        /// <summary>
        /// Default constructor (required for serialization)
        /// </summary>
        public MetaUser() { }

        /// <summary>
        /// Construct default metadata for a given user
        /// </summary>
        /// <param name="toextend">The user to extend metadata for</param>
        public MetaUser(User toextend)
        {
            PartialMask = toextend.HostMask.WithAnyNick().Mask;
        }
        
        /// <summary>
        /// List of user references
        /// </summary>
        [NonSerialized]
        [System.Xml.Serialization.XmlIgnore]
        public List<User> References = new List<User>();

        /// <summary>
        /// The partial host mask of this user  (nick part ignored)
        /// </summary>
        public string PartialMask = null;

        /// <summary>
        /// If set, user has elevated permission set to Developer
        /// </summary>
        public bool Elevation = false;

        /// <summary>
        /// If set, user has JTV moderator flag set
        /// </summary>
        public bool JTVModerator = false;

        /// <summary>
        /// Time of deop, this is used as a temporary workaround for buggy twitch chat (incomplete userlist)
        /// Is only taken into account when -buggytwitch command line option is used
        /// </summary>
        [NonSerialized]
        [System.Xml.Serialization.XmlIgnore]
        public DateTime DeopTime = DateTime.MinValue;

        /// <summary>
        /// Per-user rate limiter, configuration is reset every time a command is considered from the per-user settings
        /// </summary>
        [NonSerialized]
        [System.Xml.Serialization.XmlIgnore]
        public RateLimiter Limiter = new RateLimiter(TimeSpan.FromSeconds(10.0), TimeSpan.FromSeconds(20.0));

        /// <summary>
        /// If set, user has JTV subscriber flag set
        /// </summary>
        public DateTime JTVSubscriber = DateTime.MinValue;

        /// <summary>
        /// A list of warnings issued to this user
        /// </summary>
        public List<Warning> Warnings = new List<Warning>();

        /// <summary>
        /// Get hask key value
        /// </summary>
        /// <returns>Hashable value</returns>
        public string GetKey()
        {
            return PartialMask;
        }

        /// <summary>
        /// Compare equality
        /// </summary>
        /// <param name="obj">The object to compare with</param>
        /// <returns>True if the object references the same metadata</returns>
        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is MetaUser)) return false;
            return (obj as MetaUser).PartialMask == PartialMask;
        }

        /// <summary>
        /// Get hash code for this object
        /// </summary>
        /// <returns>Hash of PartialMask</returns>
        public override int GetHashCode()
        {
            return PartialMask.GetHashCode();
        }
    }
}
