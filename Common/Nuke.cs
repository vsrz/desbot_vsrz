using System;
namespace desBot
{
    /// <summary>
    /// Nuke text
    /// </summary>
    public class Nuke
    {
        public int ID { get; set; }
        public string Text { get; set; }
        public string SetBy { get; set; }
        public DateTime Created { get; set; }

        public override bool Equals(object obj)
        {
            if (!(obj is Nuke)) return false;
            return ID == ((Nuke)obj).ID;
        }
        public override int GetHashCode()
        {
            return ID;
        }
    }

    /// <summary>
    /// Serializable version of the Nuke class
    /// </summary>
    [Serializable]
    public class SerializableNuke
    {
        public int ID;
        public string Text;
        public string SetBy;
        public DateTime Created;
    }

    /// <summary>
    /// Utility to serialize Nukes
    /// </summary>
    class NukeSerializer : ISerializer<Nuke, SerializableNuke>
    {
        public SerializableNuke Serialize(Nuke other)
        {
            SerializableNuke result = new SerializableNuke();
            result.ID = other.ID;
            result.Text = ControlCharacter.Serialize(other.Text);
            result.SetBy = other.SetBy;
            result.Created = other.Created;
            return result;
        }
        public Nuke Deserialize(SerializableNuke other)
        {
            Nuke result = new Nuke();
            result.ID = other.ID;
            result.Text = ControlCharacter.Deserialize(other.Text);
            result.SetBy = other.SetBy;
            result.Created = other.Created;
            return result;
        }
    }
}