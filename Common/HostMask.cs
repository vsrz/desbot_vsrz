using System;
using System.Text.RegularExpressions;
namespace desBot
{
    /// <summary>
    /// Utility for IRC host masks, and checking if they match
    /// </summary>
    public class HostMask
    {
        Regex regex;

        /// <summary>
        /// The host mask string
        /// </summary>
        public string Mask { get; private set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="mask_or_nick">A host mask or nickname</param>
        public HostMask(string mask_or_nick)
        {
            if (string.IsNullOrEmpty(mask_or_nick)) throw new Exception("Bad parameter");
            Mask = mask_or_nick;
            if (!Mask.Contains("!")) Mask += "!*@*";
        }

        /// <summary>
        /// Checks if the provided host mask matches this hostmask
        /// </summary>
        /// <param name="mask">The host mask to check</param>
        /// <returns>True if the masks match</returns>
        public bool Matches(HostMask mask)
        {
            if (regex == null)
            {
                string pattern = "^";
                foreach (char c in Mask)
                {
                    if (char.IsLetterOrDigit(c)) pattern += c;
                    else if (c == '*') pattern += ".*?";
                    else pattern += "\\" + c;
                }
                pattern += "$";
                regex = new Regex(pattern, RegexOptions.IgnoreCase);
            }
            return regex.Match(mask.Mask).Success;
        }

        /// <summary>
        /// Creates a host mask that identifies this host mask with any nickname
        /// </summary>
        /// <returns>A new hostmask that matches anyone from the same ident/host combination, ignoring the nickname</returns>
        public HostMask WithAnyNick()
        {
            int bangpos = Mask.IndexOf('!');
            if (bangpos <= 0) throw new Exception("Invalid host mask state");
            string mask = "*!" + Mask.Substring(bangpos + 1);
            if (mask == "*!*@*") return this;
            return new HostMask(mask);
        }

        /// <summary>
        /// Compare for equality
        /// </summary>
        /// <param name="obj">The object to compare with</param>
        /// <returns>True of the masks are the same</returns>
        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is HostMask)) return false;
            return (obj as HostMask).Mask == Mask;
        }

        /// <summary>
        /// Get hash for object
        /// </summary>
        /// <returns>Hash of mask</returns>
        public override int GetHashCode()
        {
            return Mask.GetHashCode();
        }
    }
}