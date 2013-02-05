using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;
using System.Net;
namespace desBot
{
    /// <summary>
    /// An account that allows some IP access to the bot
    /// </summary>
    [Serializable]
    public class Account : IKeyInsideValue<IPAddress>
    {
        /// <summary>
        /// RSA Key size
        /// </summary>
        public const int KeySize = 2400;

        /// <summary>
        /// The IP that has access to the bot
        /// </summary>
        public IPAddress IP;

        /// <summary>
        /// The private key that allows access to the bot
        /// </summary>
        public RSAParameters Key;

        /// <summary>
        /// Get hash map key
        /// </summary>
        /// <returns>IP of the account</returns>
        public IPAddress GetKey()
        {
            return IP;
        }

        /// <summary>
        /// Compare to other object
        /// </summary>
        /// <param name="obj">The object to compare</param>
        /// <returns>True if the accounts map the same IP</returns>
        public override bool Equals(object obj)
        {
            if (obj is Account)
            {
                Account other = (Account)obj;
                return other.IP.ToString() == IP.ToString();
            }
            return false;
        }

        /// <summary>
        /// Get hash code
        /// </summary>
        /// <returns>Hash code of IP</returns>
        public override int GetHashCode()
        {
            return IP.ToString().GetHashCode();
        }

        /// <summary>
        /// Generate an account for the given IP
        /// </summary>
        /// <param name="ip">The IP to generate an account for</param>
        /// <returns>A new account</returns>
        public static Account Generate(IPAddress ip)
        {
            Account result = new Account();
            result.IP = ip;
            RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(KeySize);
            result.Key = rsa.ExportParameters(true);
            return result;
        }

        /// <summary>
        /// Creates a token for this account
        /// </summary>
        /// <returns>A token identifying the account</returns>
        public Token GetToken()
        {
            Token token = new Token();
            token.IP = State.PublicIP.Value;
            RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(KeySize);
            rsa.ImportParameters(Key);
            token.Key = rsa.ExportParameters(false);
            return token;
        }
    }

    /// <summary>
    /// Serializable version of Account
    /// </summary>
    [Serializable]
    public class SerializableAccount
    {
        /// <summary>
        /// The IP that has access to the bot
        /// </summary>
        public string IP;

        /// <summary>
        /// The private key that allows access to the bot
        /// </summary>
        public RSAParameters Key;
    }

    /// <summary>
    /// Utility to serialize accounts
    /// </summary>
    class AccountSerializer : ISerializer<Account, SerializableAccount>
    {
        public SerializableAccount Serialize(Account other)
        {
            SerializableAccount result = new SerializableAccount();
            result.IP = other.IP.ToString();
            result.Key = other.Key;
            return result;
        }
        public Account Deserialize(SerializableAccount other)
        {
            Account result = new Account();
            result.IP = IPAddress.Parse(other.IP);
            result.Key = other.Key;
            return result;
        }
    }
}
