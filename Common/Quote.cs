using System;
namespace desBot
{
    /// <summary>
    /// A quote
    /// </summary>
    public class Quote
    {
        public int ID { get; set; }
        public string Text { get; set; }
        public string SetBy { get; set; }
        public DateTime Created { get; set; }

        public override bool Equals(object obj)
        {
            if (!(obj is Quote)) return false;
            return ID == ((Quote)obj).ID;
        }
        public override int GetHashCode()
        {
            return ID;
        }
    }

    /// <summary>
    /// Serializable version of the Quote class
    /// </summary>
    [Serializable]
    public class SerializableQuote
    {
        public int ID;
        public string Text;
        public string SetBy;
        public DateTime Created;
    }

    /// <summary>
    /// Utility to serialize quotes
    /// </summary>
    class QuoteSerializer : ISerializer<Quote, SerializableQuote>
    {
        public SerializableQuote Serialize(Quote other)
        {
            SerializableQuote result = new SerializableQuote();
            result.ID = other.ID;
            result.Text = ControlCharacter.Serialize(other.Text);
            result.SetBy = other.SetBy;
            result.Created = other.Created;
            return result;
        }
        public Quote Deserialize(SerializableQuote other)
        {
            Quote result = new Quote();
            result.ID = other.ID;
            result.Text = ControlCharacter.Deserialize(other.Text);
            result.SetBy = other.SetBy;
            result.Created = other.Created;
            return result;
        }
    }
}