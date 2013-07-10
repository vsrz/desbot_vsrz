using System;
using System.Collections.Generic;
using System.Text;

namespace desBot
{
#if JTVBOT
    static class JTV
    {
        /// <summary>
        /// Time out a user
        /// </summary>
        /// <param name="nick">Nickname of user</param>
        public static void TimeOut(string nick)
        {
#if OLDJTV
            Irc.Kick(nick, null);
#else
            Irc.SendChannelMessage("/timeout " + nick, false);
#endif
        }

        /// <summary>
        /// Permanently ban a user
        /// </summary>
        /// <param name="nick">Nickname of user</param>
        public static void PermBan(string nick)
        {
#if OLDJTV
            Irc.Ban(nick);
#else
            Irc.SendChannelMessage("/ban " + nick, false);
#endif
        }

        /// <summary>
        /// Unbans a timed out or permabanned user
        /// </summary>
        /// <param name="nick">Nickname of user</param>
        public static void Unban(string nick)
        {
#if OLDJTV
            Irc.Unban(nick);
#else
            Irc.SendChannelMessage("/unban " + nick, false);
#endif
        }

        /// <summary>
        /// Clear chat
        /// </summary>
        public static void Clear()
        {
            Irc.SendChannelMessage("/clear", false);
        }

        /// <summary>
        /// Purges chat of user
        /// </summary>
        /// <param name="nick">Nickname of user</param>
        public static void Purge(string nick, int length = 1)
        {
#if OLDJTV
            TimeOut(nick);
            Unban(nick);
#else
            Irc.SendChannelMessage("/timeout " + nick + " " + length.ToString(), false);
#endif
        }

        /// <summary>
        /// Enables or disables slow mode
        /// </summary>
        /// <param name="enabled">If true, enabled slow mode, of false, disables slow mode</param>
        public static void Slowmode(bool enabled)
        {
#if OLDJTV
            Irc.SetChannelMode(enabled ? "+m" : "-m");
#else
            Irc.SendChannelMessage(enabled ? "/slow 60" : "/slowoff", false);
#endif
        }

        /// <summary>
        /// Mark a user as JTV subscriber
        /// </summary>
        /// <param name="user"></param>
        public static void MarkSubscriber(string user)
        {
            User u = State.UserList.Lookup(user);
            if (u != null)
            {
                u.Meta.JTVSubscriber = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Handle 
        /// </summary>
        /// <param name="message"></param>
        public static void HandleMessage(IrcMessage message)
        {
            try
            {
                if (message.IsPrivateMessage && message.From.StartsWith("jtv") && message.Text.StartsWith("SPECIALUSER") && message.Text.EndsWith("subscriber"))
                {
                    MarkSubscriber(message.Text.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[1]);
                }
            }
            catch (Exception)
            {
                //ignore
            }
        }
    }
#endif
}
