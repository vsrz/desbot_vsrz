using System;
using System.Collections.Generic;
namespace desBot
{
    /// <summary>
    /// Information about a user
    /// </summary>
    public class User : IKeyInsideValue<string>
    {
        DateTime joined;
        DateTime left;

        /// <summary>
        /// The nickname of the user
        /// </summary>
        public string Nick { get; private set; }

        /// <summary>
        /// The last change to this user
        /// </summary>
        public DateTime LastChanged { get { return left == DateTime.MaxValue ? joined : left; } }

        /// <summary>
        /// The host mask of this user
        /// </summary>
        public HostMask HostMask { get; private set; }

        /// <summary>
        /// The time the user joined the channel
        /// </summary>
        public DateTime Joined { get { return joined; } }

        /// <summary>
        /// The time the user left the channel
        /// </summary>
        public DateTime Left { get { return left; } set { left = value; } }

        /// <summary>
        /// Tag used for state cleanup
        /// </summary>
        public object Tag = null;
        
        /// <summary>
        /// Cache for metadata lookup
        /// </summary>
        MetaUser cache = null;

        /// <summary>
        /// Retrieve meta-data for this user set, identified by ident/host combination
        /// </summary>
        public MetaUser Meta
        {
            get
            {
                if (cache == null)
                {
                    MetaUser result = State.MetaUserList.Lookup(HostMask.WithAnyNick().Mask);
                    cache = result == null ? new MetaUser(this) : result;
                    if (cache != result)
                    {
                        State.MetaUserList.Add(cache);
                    }
                    cache.References.Add(this);
                }
                return cache;
            }
            set
            {
                if (value != null) throw new Exception("You can only clear the meta-reference using null");
                if (cache != null)
                {
                    cache.References.Remove(this);
                    cache = null;
                }
            }
        }

        /// <summary>
        /// Check if meta-data for this user set exists, and if so, returns that meta-data
        /// If no meta-data exists, it will NOT be created, and null is returned
        /// To create meta-data, use the Meta property instead
        /// </summary>
        /// <returns>Existign metadata, or null</returns>
        public MetaUser CheckExistingMetaData()
        {
            if (cache == null)
            {
                cache = State.MetaUserList.Lookup(HostMask.WithAnyNick().Mask);
                if (cache != null)
                {
                    cache.References.Add(this);
                }
            }
            return cache;
        }

        /// <summary>
        /// Creates a new user information container
        /// </summary>
        /// <param name="nick">The nickname of the user</param>
        /// <param name="host">The hostmask of the user</param>
        public User(string nick, HostMask host)
        {
            Nick = nick;
            HostMask = host;
            joined = DateTime.UtcNow;
            left = DateTime.MaxValue;
        }

        /// <summary>
        /// Creates a user information from saved data
        /// </summary>
        /// <param name="fromsave">The saved data</param>
        public User(string nick, HostMask mask, DateTime joined, DateTime left)
        {
            Nick = nick;
            HostMask = mask;
            this.joined = joined;
            this.left = left;
            //if (left > DateTime.UtcNow && left != DateTime.MaxValue) this.left = DateTime.UtcNow;
        }

        /// <summary>
        /// Key for hash map in common state
        /// </summary>
        /// <returns>Nickname</returns>
        public string GetKey()
        {
            return Nick;
        }

        /// <summary>
        /// Comapre object for equality
        /// </summary>
        /// <param name="obj">Object to check for equality</param>
        /// <returns>True if nicknames are identical</returns>
        public override bool Equals(object obj)
        {
            if (!(obj is User)) return false;
            return Nick == ((User)obj).Nick;
        }

        /// <summary>
        /// Get hash code for object
        /// </summary>
        /// <returns>Hashcode of nickname</returns>
        public override int GetHashCode()
        {
            return Nick.GetHashCode();
        }
    }

    /// <summary>
    /// Serializable version of the User class
    /// </summary>
    [Serializable]
    public class SerializableUser
    {
        public string Nick;
        public DateTime Joined;
        public DateTime Left;
        public string HostMask;
    }

    /// <summary>
    /// Utility to serialize users
    /// </summary>
    class UserSerializer : ISerializer<User, SerializableUser>
    {
        public SerializableUser Serialize(User user)
        {
            SerializableUser result = new SerializableUser();
            result.Nick = user.Nick;
            result.Joined = user.Joined;
            result.Left = user.Left;
            result.HostMask = user.HostMask.Mask;
            return result;
        }
        public User Deserialize(SerializableUser serialized)
        {
            return new User(serialized.Nick, new HostMask(serialized.HostMask), serialized.Joined, serialized.Left);
        }
    }
}