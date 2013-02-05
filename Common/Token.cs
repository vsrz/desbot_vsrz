using System;
using System.Net;
using System.IO;
using System.Text;
using System.Reflection;
using System.Security.Cryptography;
using XS = System.Xml.Serialization;
namespace desBot
{
    [Serializable]
    public class Token
    {
        static XS.XmlSerializer serializer = new XS.XmlSerializer(typeof(Token));

        public string IP;
        public RSAParameters Key;

        /// <summary>
        /// Convert token to XML
        /// </summary>
        /// <returns>XML representing the token</returns>
        public string ToXML()
        {
            using (MemoryStream stream = new MemoryStream())
            {
                serializer.Serialize(stream, this);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        /// <summary>
        /// Convert XML to token
        /// </summary>
        /// <param name="xml">The XML to parse</param>
        /// <returns>A token</returns>
        public static Token FromXML(string xml)
        {
            using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(xml)))
            {
                return (Token)serializer.Deserialize(stream);
            }
        }

        /// <summary>
        /// Returns a token valid for the local host only
        /// </summary>
        /// <returns>Token identifying localhost</returns>
        public static Token LocalHostToken()
        {
            Token result = new Token();
            result.IP = "127.0.0.1";
            return result;
        }

        /// <summary>
        /// Get current version
        /// </summary>
        /// <returns>The current version of the Common assembly</returns>
        public static string GetCurrentVersion()
        {
            return Assembly.GetCallingAssembly().GetName().Version.ToString();
        }
    }
}