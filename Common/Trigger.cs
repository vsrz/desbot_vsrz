using System;
namespace desBot
{
    /// <summary>
    /// A trigger
    /// </summary>
    public class Trigger
    {
        public string Keyword { get; set; }
        public string Text { get; set; }

        public override bool Equals(object obj)
        {
            if (!(obj is Trigger)) return false;
            return Keyword == ((Trigger)obj).Keyword;
        }
        public override int GetHashCode()
        {
            return Keyword.GetHashCode();
        }
    }
    
    /// <summary>
    /// Serializable version of the Trigger class
    /// </summary>
    [Serializable]
    public class SerializableTrigger
    {
        public string Keyword;
        public string Text;
    }

    /// <summary>
    /// Utility to serialize triggers
    /// </summary>
    class TriggerSerializer : ISerializer<Trigger, SerializableTrigger>
    {
        public SerializableTrigger Serialize(Trigger other)
        {
            SerializableTrigger result = new SerializableTrigger();
            result.Keyword = ControlCharacter.Serialize(other.Keyword);
            result.Text = ControlCharacter.Serialize(other.Text);
            return result;
        }
        public Trigger Deserialize(SerializableTrigger other)
        {
            Trigger result = new Trigger();
            result.Keyword = ControlCharacter.Deserialize(other.Keyword);
            result.Text = ControlCharacter.Deserialize(other.Text);
            return result;
        }
    }
}