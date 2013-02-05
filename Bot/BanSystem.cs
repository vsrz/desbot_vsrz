using System;
using System.Collections.Generic;
using System.Text;
namespace desBot
{
#if QNETBOT
    /// <summary>
    /// Extended ban information supplied by IRC
    /// </summary>
    class ExtendedBanInfo
    {
        /// <summary>
        /// The nickname of the user who set the ban
        /// This data is from the IRC server point of view, and does not necessarily match Ban.SetBy, which may map to whomever invoked the ban command on Q or desBot
        /// </summary>
        public string SetBy;

        /// <summary>
        /// The time at which the ban was set on the channel
        /// </summary>
        public DateTime SetAt;
    }

    /// <summary>
    /// Possible states of the ban system
    /// </summary>
    enum BanSystemState
    {
        /// <summary>
        /// Waiting for channel ban information
        /// </summary>
        WaitingForChannel,

        /// <summary>
        /// Waiting for Q ban information
        /// </summary>
        WaitingForQ,

        /// <summary>
        /// Synchronized
        /// </summary>
        Synchronized,

        /// <summary>
        /// There was an error while attempting to synchronize
        /// </summary>
        Error,
    }

    /// <summary>
    /// Ban system implementation
    /// </summary>
    static class BanSystem
    {
        /// <summary>
        /// Maximum number of Q enforced bans in the Q internal ban list
        /// </summary>
        public const int QMaxBans = 50;

        /// <summary>
        /// Maximum number of channel bans
        /// </summary>
        public const int CMaxBans = 45;

        /// <summary>
        /// Number of slots to keep free when offloading
        /// </summary>
        public const int OffloadMargin = 3;

        /// <summary>
        /// Current ban system state
        /// </summary>
        static BanSystemState state = BanSystemState.WaitingForChannel;

        /// <summary>
        /// List of extended ban information
        /// </summary>
        static Dictionary<string, ExtendedBanInfo> extended = new Dictionary<string, ExtendedBanInfo>();

        /// <summary>
        /// Flag indicating a refresh is pending
        /// </summary>
        static bool refresh_pending = true;

        /// <summary>
        /// The next expected ban by Q
        /// </summary>
        static string next_q_ban = null;

        /// <summary>
        /// The next expected unban by Q
        /// </summary>
        static string next_q_unban = null;

        /// <summary>
        /// List of bans to keep in banlist
        /// </summary>
        static List<Ban> keep = null;

        /// <summary>
        /// Reset the ban system
        /// </summary>
        public static void Reset()
        {
            lock (State.GlobalSync)
            {
                state = BanSystemState.WaitingForChannel;
                extended = new Dictionary<string, ExtendedBanInfo>();
                refresh_pending = true;
                next_q_ban = null;
                next_q_unban = null;
                keep = null;
            }
        }

        /// <summary>
        /// Checks if a ban is currently enforced on the channel
        /// </summary>
        /// <param name="ban">The ban to check</param>
        /// <returns>True if the ban is enforced</returns>
        public static bool IsBanEnforced(Ban ban)
        {
            return Irc.IsBanEnforced(ban.Mask.Mask);
        }

        /// <summary>
        /// Checks if a channel ban can be set currently
        /// </summary>
        /// <returns>True if a ban can be set on the channel</returns>
        public static bool CanChannelBan()
        {
            return Irc.CountBans() < (CMaxBans - OffloadMargin);
        }

        /// <summary>
        /// Tests if a ban is enforced by Q
        /// </summary>
        /// <param name="ban">The ban to test</param>
        /// <returns>True if the ban is enforced by Q</returns>
        static bool IsQBan(Ban ban)
        {
            return ban.Enforcer == BanEnforcement.ByQ;
        }

        /// <summary>
        /// Checks if a Q ban can be set currently
        /// </summary>
        /// <returns>True if a ban can be set by Q</returns>
        public static bool CanQBan()
        {
            lock (State.GlobalSync)
            {
                return State.BanList.CountIf(new Predicate<Ban>(IsQBan)) > (QMaxBans - OffloadMargin);
            }
        }

        /// <summary>
        /// Called when a channel ban list item has been received
        /// </summary>
        /// <param name="hostmask">The hostmask that was banned</param>
        /// <param name="who">The nickname that set the ban</param>
        /// <param name="when">The time at which the ban was set</param>
        public static void OnIrcBanList(string hostmask, string who, DateTime when)
        {
            lock (State.GlobalSync)
            {
                if (state != BanSystemState.WaitingForChannel) throw new Exception("Invalid state for this command");
                ExtendedBanInfo info = new ExtendedBanInfo();
                info.SetBy = who;
                info.SetAt = when;
                extended[hostmask] = info;
            }
        }

        /// <summary>
        /// Called when the channel ban list has been received
        /// </summary>
        public static void OnIrcEndOfBanList()
        {
            lock (State.GlobalSync)
            {
                if (state != BanSystemState.WaitingForChannel) throw new Exception("Unexpected state transition");

                if (Irc.QLevel >= QAuthLevel.Operator)
                {
                    state = BanSystemState.WaitingForQ;
                    QCommand command = new BanListCommand(0);
                    command.OnFinished += new QCommand.OnFinishedDelegate(OnQInitialBanList);
                }
                else
                {
                    Program.Log("Insufficient Q access privileges to run ban system");
                    state = BanSystemState.Error;
                }
            }
        }

        /// <summary>
        /// Called when a user is banned on the channel
        /// </summary>
        /// <param name="hostmask">The hostmask of the person being banned</param>
        /// <param name="who">The nickname of the person who performed the ban</param>
        public static void OnIrcBan(string hostmask, string who)
        {
            lock (State.GlobalSync)
            {
                if (state == BanSystemState.WaitingForChannel) throw new Exception("Invalid bansystem state for this command");
                ExtendedBanInfo info = new ExtendedBanInfo();
                info.SetBy = who;
                info.SetAt = DateTime.UtcNow;
                extended[hostmask] = info;

                if (who == "Q" && next_q_ban == hostmask)
                {
                    //expected, no refresh
                    next_q_ban = null;
                }
                else if (who != Irc.Nickname)
                {
                    //unexpected, refresh
                    RefreshFromIrc();
                }
            }
        }

        /// <summary>
        /// Called when a user is unbanned from the channel
        /// </summary>
        /// <param name="hostmask">The hostmask of the person being unbanned</param>
        /// <param name="who">The nickname of the person who performed the unban</param>
        public static void OnIrcUnban(string hostmask, string who)
        {
            lock (State.GlobalSync)
            {
                if (state == BanSystemState.WaitingForChannel) throw new Exception("Invalid bansystem state for this command");
                extended.Remove(hostmask);

                //if there is a channel ban on this, remove from list
                Ban ban = State.BanList.Lookup(hostmask);
                if (ban != null)
                {
                    switch (ban.Enforcer)
                    {
                        case BanEnforcement.ByQ:
                            //Q will handle this
                            break;
                        case BanEnforcement.ByChannel:
                            //remove channel ban from list
                            State.BanList.Remove(ban);
                            break;
                        case BanEnforcement.ByMe:
                            //I am enforcing this ban, lift in state as well
                            if (who != Irc.Nickname) Program.Log("My ban on '" + hostmask + "' was incorrectly lifted by " + who);
                            User dummy = null;
                            if (IsQuietBan(ban, out dummy)) Irc.Ban(ban.Mask.Mask);
                            break;
                    }
                }

                if (who == "Q" && next_q_unban == hostmask)
                {
                    //expected, no refresh
                    next_q_unban = null;
                }
                else if (who != Irc.Nickname)
                {
                    //unexpected, refresh list
                    RefreshFromIrc();
                }
            }
        }

        /// <summary>
        /// Called when someone enters IRC (by JOIN or WHO)
        /// </summary>
        /// <param name="mask">The hostmask of the user</param>
        /// <param name="nick">The nickname of the user</param>
        public static void OnIrcEnter(HostMask mask, string nick)
        {
            lock (State.GlobalSync)
            {
                foreach (Ban ban in State.BanList.GetItems())
                {
                    if (ban.Enforcer == BanEnforcement.ByMe && ban.Mask.Matches(mask))
                    {
                        if (!CanChannelBan() && FreeBanSlot(1) == 0)
                        {
                            Irc.Kick(nick, "Enforcing ban: " + ban.Reason);
                        }
                        else if (ban.Expires == DateTime.MaxValue || State.UseQuietBan.Value == false)
                        {
                            Irc.Kick(nick, "Enforcing ban: " + ban.Reason);
                            Irc.Ban(ban.Mask.Mask);
                        }
                        else
                        {
                            Irc.Ban(ban.Mask.Mask);
                        }
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Called when a user leaves IRC (by PART, KICK or QUIT)
        /// </summary>
        /// <param name="mask">The hostmask of the user</param>
        public static void OnIrcLeave(HostMask mask)
        {
            //consider removing a ban here?
        }

        /// <summary>
        /// Called when the initial Q ban list command was received
        /// </summary>
        /// <param name="succeeded"></param>
        public static void OnQInitialBanList(bool succeeded)
        {
            lock (State.GlobalSync)
            {
                if (state != BanSystemState.WaitingForQ) throw new Exception("Unexpected state transition");
                state = BanSystemState.Synchronized;
                Program.Log("BanSystem entering ready state");
            }
        }

        /// <summary>
        /// Called to test if a ban should be removed
        /// </summary>
        /// <param name="ban">The ban to test</param>
        /// <returns>True if the ban should be removed</returns>
        static bool ShouldRemoveBan(Ban ban)
        {
            return (ban.Enforcer == BanEnforcement.ByQ || ban.Enforcer == BanEnforcement.ByChannel) && !keep.Contains(ban);
        }

        /// <summary>
        /// Called when the ban list has been updated from IRC by using the Q BanListCommand
        /// </summary>
        /// <param name="bans">The bans that are currently known on IRC</param>
        public static void OnQBanListReceived(IEnumerable<Ban> bans)
        {
            lock (State.GlobalSync)
            {
                if (state == BanSystemState.WaitingForChannel) throw new Exception("Invalid bansystem state for this command");
                keep = new List<Ban>();
                foreach (Ban ban in bans)
                {
                    if (ban.Enforcer == BanEnforcement.ByQ)
                    {
                        //Q ban, update current ban list
                        Ban needle = State.BanList.Lookup(ban.Mask.Mask);
                        if (needle == null)
                        {
                            keep.Add(ban);
                            State.BanList.Add(ban);
                        }
                        else
                        {
                            //check if identical
                            bool changed = needle.Affected != ban.Affected || needle.Enforcer != ban.Enforcer || needle.Reason != ban.Reason || needle.SetBy != ban.SetBy;
                            if (!changed) changed = (needle.Expires - ban.Expires).Duration() >= TimeSpan.FromMinutes(1);
                            if (changed)
                            {
                                //replace if not identical
                                State.BanList.Remove(needle);
                                State.BanList.Add(ban);
                                keep.Add(ban);
                            }
                            else
                            {
                                //keep the existing ban
                                keep.Add(needle);
                            }
                        }
                    }
                    else if (ban.Enforcer == BanEnforcement.ByChannel)
                    {
                        //channel ban OR one of my bans
                        Ban needle = State.BanList.Lookup(ban.Mask.Mask);
                        if (needle == null)
                        {
                            State.BanList.Add(ban);
                            keep.Add(ban);
                        }
                        else
                        {
                            if (needle.Enforcer == BanEnforcement.ByMe)
                            {
                                //my ban, discard new info
                                break;
                            }

                            //check if ban on channel
                            ExtendedBanInfo info;
                            if (!extended.TryGetValue(ban.Mask.Mask, out info))
                            {
                                //ban not on channel
                                Program.Log("Ignoring a channel ban claim by Q that was not found on the channel");
                                break;
                            }

                            if (needle.SetBy != "<manual>")
                            {
                                //fetch possibly better quality info from current banlist
                                ban.SetBy = needle.SetBy;
                                ban.Reason = needle.Reason;
                            }
                            else
                            {
                                //generate better information from extended info
                                ban.SetBy = info.SetBy;
                                ban.Reason = "Channel ban set on " + info.SetAt.ToString() + " by " + info.SetBy;
                            }

                            //check if identical
                            bool changed = needle.Affected != ban.Affected || needle.Enforcer != ban.Enforcer || needle.Reason != ban.Reason || needle.SetBy != ban.SetBy;
                            if (!changed) changed = (needle.Expires - ban.Expires).Duration() >= TimeSpan.FromMinutes(1);
                            if (changed)
                            {
                                //replace if not identical
                                State.BanList.Remove(needle);
                                State.BanList.Add(ban);
                                keep.Add(ban);
                            }
                            else
                            {
                                //keep needle
                                keep.Add(needle);
                            }
                        }
                    }
                    else
                    {
                        //ban info from Q should be either Enforcement.ByQ or Enforcement.ByChannel
                        throw new Exception("Unexpected enforcement value");
                    }
                }

                //remove bans no longer in Q list / channel list
                State.BanList.RemoveIf(new Predicate<Ban>(ShouldRemoveBan));
                keep = null;

                //clear refresh flag
                refresh_pending = false;
            }
        }

        /// <summary>
        /// Called when Q system starts a ban
        /// </summary>
        /// <param name="mask">The mask being banned</param>
        public static void OnQBanStarted(string mask)
        {
            lock (State.GlobalSync)
            {
                if (state != BanSystemState.Synchronized) throw new Exception("Bad state");
                if (Irc.IsBanEnforced(mask))
                {
                    next_q_ban = mask;
                }
                else next_q_ban = null;
            }
        }

        /// <summary>
        /// Called when Q system finishes a ban
        /// </summary>
        /// <param name="success">If true, Q succeeded</param>
        public static void OnQBanFinished(bool success)
        {
            lock (State.GlobalSync)
            {
                if (state != BanSystemState.Synchronized) throw new Exception("Bad state");
                if (!success || next_q_ban != null) RefreshFromIrc();
                next_q_ban = null;
            }
        }

        /// <summary>
        /// Called when Q system starts an unban
        /// </summary>
        /// <param name="mask">The mask being banned</param>
        public static void OnQUnbanStarted(string mask)
        {
            lock (State.GlobalSync)
            {
                if (state != BanSystemState.Synchronized) throw new Exception("Bad state");
                if (Irc.IsBanEnforced(mask))
                {
                    next_q_unban = mask;
                }
                else next_q_unban = null;
            }
        }

        /// <summary>
        /// Called when Q finished an unban
        /// </summary>
        /// <param name="success">If true, Q succeeded</param>
        public static void OnQUnbanFinished(bool success)
        {
            lock (State.GlobalSync)
            {
                if (state != BanSystemState.Synchronized) throw new Exception("Bad state");
                if (!success || next_q_unban != null) RefreshFromIrc();
                next_q_unban = null;
            }
        }

        /// <summary>
        /// Refresh ban list from IRC
        /// </summary>
        public static void RefreshFromIrc()
        {
            lock (State.GlobalSync)
            {
                if (state != BanSystemState.Synchronized) throw new Exception("Bansystem not in acceptable state");
                if (refresh_pending) return;
                new BanListCommand(1);
            }
        }

        /// <summary>
        /// Gets a user-friendly display of nicknames affected by a ban
        /// </summary>
        /// <param name="banmask">The banmask being searched</param>
        /// <returns>User-friendly string</returns>
        public static string GetAffectedNicks(HostMask banmask)
        {
            lock (State.GlobalSync)
            {
                string result = null;
                foreach (User user in State.UserList.GetItems())
                {
                    if (banmask.Matches(user.HostMask)) result = (result == null ? user.Nick : result + ", " + user.Nick);
                }
                return result == null ? "" : result;
            }
        }

        /// <summary>
        /// Looks for the banmask with which a user was banned
        /// </summary>
        /// <returns>The host mask with which the user was banned, or null, if not found</returns>
        public static HostMask FindBanByNick(string nick)
        {
            lock (State.GlobalSync)
            {
                foreach (Ban entry in State.BanList.GetItems())
                {
                    if (entry.Affected.Contains(nick)) return new HostMask(entry.Mask.Mask);
                }
                return null;
            }
        }

        /// <summary>
        /// Creates a banmask for a nickname
        /// </summary>
        /// <param name="nick">The nick to find a banmask for</param>
        /// <returns>A ban mask, or null</returns>
        public static HostMask CreateBanMask(string nick)
        {
            lock (State.GlobalSync)
            {
                User user = State.UserList.Lookup(nick);
                return user == null ? null : new HostMask("*!*" + user.HostMask.Mask.Substring(user.HostMask.Mask.IndexOf("!") + 1));
            }
        }

        /// <summary>
        /// Performs a ban
        /// </summary>
        /// <param name="hostmask">The mask to ban</param>
        /// <param name="duration_or_null">The duration, or null</param>
        /// <param name="reason">The reason for the ban</param>
        /// <param name="by">The user who set the ban</param>
        public static void PerformBan(string hostmask, string duration_or_null, string reason, string by)
        {
            lock (State.GlobalSync)
            {
                if (state != BanSystemState.Synchronized) throw new Exception("Bad state");
                if (State.UseQEnforce.Value && CanQBan())
                {
                    //if we use Q enforcement, queue a Q command
                    if (duration_or_null == null) new PermBanCommand(hostmask, reason);
                    else new TempBanCommand(hostmask, duration_or_null, reason);
                }
                else
                {
                    //hostmask
                    HostMask mask = new HostMask(hostmask);

                    //add to list
                    Ban ban = new Ban();
                    TimeSpan actual_duration = duration_or_null == null ? TimeSpan.FromSeconds(0) : ParseDuration(duration_or_null);
                    ban.Expires = duration_or_null == null ? DateTime.MaxValue : DateTime.UtcNow + actual_duration;
                    ban.Enforcer = BanEnforcement.ByMe;
                    ban.Affected = GetAffectedNicks(mask);
                    ban.Mask = mask;
                    ban.Reason = reason;
                    ban.SetBy = by;

                    //check if a longer ban already in place
                    Ban existing = State.BanList.Lookup(ban.Mask.Mask);
                    if (existing != null && existing.Expires > ban.Expires)
                    {
                        throw new Exception("A ban with a longer duration is already in place");
                    }

                    //add to banlist
                    if (existing != null) State.BanList.Remove(existing);
                    State.BanList.Add(ban);

                    //ban from channel
                    if (!CanChannelBan() && FreeBanSlot(1) == 0)
                    {
                        foreach (User user in State.UserList.GetItems())
                        {
                            if (user.Left == DateTime.MaxValue && mask.Matches(user.HostMask))
                            {
                                //kick from channel
                                Irc.Kick(user.Nick, "Enforcing ban: " + reason);
                            }
                        }
                    }
                    else
                    {
                        Irc.Ban(hostmask);
                        if (State.UseQuietBan.Value == false || duration_or_null == null)
                        {
                            foreach (User user in State.UserList.GetItems())
                            {
                                if (user.Left == DateTime.MaxValue && mask.Matches(user.HostMask))
                                {
                                    //kick from channel
                                    Irc.Kick(user.Nick, "Enforcing ban: " + reason);
                                }
                            }
                        }
                        else
                        {
                            //put channel message
                            Irc.SendChannelMessage(GetAffectedNicks(mask) + " has been timed out from chatting for " + ((int)(actual_duration.TotalMinutes + 0.5)).ToString() + " minutes: " + reason, false);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Performs an unban
        /// </summary>
        /// <param name="hostmask">The hostmask to unban</param>
        public static void PerformUnban(string hostmask)
        {
            lock (State.GlobalSync)
            {
                if (state != BanSystemState.Synchronized) throw new Exception("Bad state");
                Ban ban = State.BanList.Lookup(hostmask);
                if (ban == null) throw new Exception("No ban matching the request was found");
                switch (ban.Enforcer)
                {
                    case BanEnforcement.ByQ:
                        new BanDelCommand(hostmask);
                        break;
                    case BanEnforcement.ByChannel:
                        Irc.Unban(hostmask);
                        break;
                    case BanEnforcement.ByMe:
                        if (IsBanEnforced(ban)) Irc.Unban(hostmask);
                        State.BanList.Remove(ban);
                        break;
                    default:
                        throw new Exception("Cannot lift a ban where type == " + ban.Enforcer.ToString());
                }
            }
        }

        /// <summary>
        /// Periodically evaluate a ban
        /// </summary>
        /// <param name="ban">The ban to evaluate</param>
        /// <returns>True if the ban is now useless and should be removed</returns>
        static bool EvaluateBan(Ban ban)
        {
            if (ban.Enforcer == BanEnforcement.ByMe && ban.Expires < DateTime.UtcNow)
            {
                if (IsBanEnforced(ban))
                {
                    //remove ban, but keep in list until unban completed
                    Irc.Unban(ban.Mask.Mask);
                    return false;
                }
                else
                {
                    //remove ban, it's not enforced
                    return true;
                }
            }

            //anythign else, just keep
            return false;
        }

        /// <summary>
        /// Check for expired bans and remove those
        /// </summary>
        public static void PerformUpdate()
        {
            if (state != BanSystemState.Synchronized) return;
            lock (State.GlobalSync)
            {
                State.BanList.RemoveIf(new Predicate<Ban>(EvaluateBan));
            }
        }

        /// <summary>
        /// Candidate to free up list
        /// </summary>
        class Candidate
        {
            Ban ban;
            DateTime set;

            public Candidate(Ban ban)
            {
                this.ban = ban;
                ExtendedBanInfo info;
                this.set = extended.TryGetValue(ban.Mask.Mask, out info) ? info.SetAt : DateTime.MinValue;
            }

            public Ban GetBan()
            {
                return ban;
            }
            
            public static int Compare(Candidate c1, Candidate c2)
            {
                return DateTime.Compare(c2.set, c1.set);
            }
        }

        /// <summary>
        /// Tests if a ban is used to quiet a user on the channel
        /// </summary>
        /// <param name="ban">The ban to test</param>
        /// <returns>True if the ban is used to quiet the user</returns>
        public static bool IsQuietBan(Ban ban, out User match)
        {
            //clear flag
            match = null;

            //if expired, the ban never quiets
            if (ban.Expires < DateTime.UtcNow && ban.Enforcer != BanEnforcement.ByJtv) return false;

            //look for users
            foreach (User user in State.UserList.GetItems())
            {
                if (user.Left == DateTime.MaxValue && ban.Mask.Matches(user.HostMask))
                {
                    match = user;
                    return true;
                }
            }

            //no user affected
            return false;
        }

        /// <summary>
        /// Free up a channel ban slot
        /// </summary>
        /// <param name="limit">The maximum number of slots to free up</param>
        /// <returns>The number of slots actually freed up</returns>
        public static int FreeBanSlot(int limit)
        {
            //find candidates for freeing up
            List<Candidate> candidates = new List<Candidate>();
            Program.Log("FBS: inspecting " + extended.Count.ToString() + " enforced bans");
            foreach (string enforced in extended.Keys)
            {
                Ban ban = State.BanList.Lookup(enforced);
                if (ban == null)
                {
                    //not found
                    Program.Log("FBS: " + enforced + " not a candidate because no matching ban found");
                    continue;
                }
                if (ban.Enforcer == BanEnforcement.ByQ)
                {
                    //if this ban is enforced by Q, we can safely remove it from the channel
                    candidates.Add(new Candidate(ban));
                }
                else if (ban.Enforcer == BanEnforcement.ByMe)
                {
                    //check if this ban is a quiet ban
                    User user = null;
                    if (IsQuietBan(ban, out user))
                    {
                        Program.Log("FBS: " + enforced + " not a candidate because it's a quiet ban (matches " + user.HostMask.Mask + ")");
                        continue;
                    }

                    //non-quiet bans can be removed from the channel
                    candidates.Add(new Candidate(ban));
                }
                else
                {
                    //channel ban type
                    Program.Log("FBS: " + enforced + " not a candidate because type == " + ban.Enforcer.ToString());
                }
            }
            Program.Log("FBS: Found " + candidates.Count.ToString() + " candidates");

            //no candidates
            if (candidates.Count == 0) return 0;

            //sort by usefulness
            candidates.Sort(new Comparison<Candidate>(Candidate.Compare));

            //free up slots
            if (candidates.Count < limit) limit = candidates.Count;
            for (int i = 0; i < limit; i++)
            {
                Irc.Unban(candidates[i].GetBan().Mask.Mask);
            }

            //return number of unbans
            return limit;
        }
#elif JTVBOT
    static class BanSystem
    {
        static bool refresh_pending = false;
        static List<PendingOperation> pending = new List<PendingOperation>();
        static List<string> banlist;
        static DateTime lastupdated = DateTime.MinValue;

        abstract class PendingOperation
        {
            abstract public void Execute(List<string> bans);
        }
        
        class PendingUnban : PendingOperation
        {
            string nick;
            bool quiet;
            
            public PendingUnban(string nick, bool quiet)
            {
                this.nick = nick.ToLower();
                this.quiet = quiet;
            }

            public override void Execute(List<string> bans)
            {
                Ban lookup = State.BanList.Lookup(new HostMask(nick).Mask);
                if (lookup != null)
                {
                    State.BanList.Remove(lookup);
                }
                foreach (string ban in bans)
                {
                    if (ban == nick)
                    {
                        bans.Remove(ban);
                        break;
                    }
                }
                Irc.Unban(nick);
                if (!quiet) Irc.SendChannelMessage("Lifted existing ban on '" + nick + "'", false);
                else
                {
                    Ban ban = State.BanList.Lookup(new HostMask(nick).Mask);
                    if (ban != null && (DateTime.UtcNow - ban.Expires).Duration() < TimeSpan.FromSeconds(10))
                    {
                        Irc.SendChannelMessage("The ban on '" + nick + "' expired. Please take note of the rules from now on!", false);
                    }
                }
            }
        }

        class PendingBan : PendingOperation
        {
            string nick;
            Ban toadd;

            public PendingBan(string nick, Ban toadd)
            {
                this.nick = nick;
                this.toadd = toadd;
            }

            public override void Execute(List<string> bans)
            {
                //check for longer
                Ban lookup = State.BanList.Lookup(nick + "!*@*");
                if(lookup != null && lookup.Expires > toadd.Expires)
                {
                    if((lookup.Expires - toadd.Expires).Duration() > TimeSpan.FromSeconds(5))
                    {
                        Irc.SendChannelMessage("A ban with a longer duration is already in place for '" + nick + "'", false);
                    }
                    return;
                }

                //remove existing
                bool updated = false;
                if (lookup != null)
                {
                    State.BanList.Remove(lookup);
                    updated = true;
                }

                //add new
                State.BanList.Add(toadd);

                //send message
                TimeSpan actual_duration = toadd.Expires == DateTime.MaxValue ? TimeSpan.MaxValue : (toadd.Expires - DateTime.UtcNow).Duration();
                string affix = "";
                if (!toadd.Reason.EndsWith("by " + toadd.SetBy)) affix = " (by " + toadd.SetBy + ")";
                Irc.SendChannelMessage((updated ? "Updated" : "Added") + " a ban on " + nick + (actual_duration == TimeSpan.MaxValue ? " until the end of time" : " for " + ((int)(actual_duration.TotalMinutes + 0.5)).ToString() + " minutes") + ": " + toadd.Reason + affix, false);

                //apply ban
                if (actual_duration == TimeSpan.MaxValue)
                {
                    Irc.Ban(nick);
                    bans.Add(nick);
                }
                else Irc.Kick(nick, "");
            }
        }
        
        /// <summary>
        /// Add operation to pending queue
        /// </summary>
        /// <param name="op">The operation to execute</param>
        static void AddOperation(PendingOperation op)
        {
            lock (State.GlobalSync)
            {
                pending.Add(op);
                RefreshFromIrc();
            }
        }

        /// <summary>
        /// Reset bansystem
        /// </summary>
        public static void Reset()
        {
            refresh_pending = false;
            lastupdated = DateTime.MinValue;
            banlist = null;
        }

        /// <summary>
        /// Refresh ban list
        /// </summary>
        public static void RefreshFromIrc()
        {
            lock (State.GlobalSync)
            {
                //check state
                if (refresh_pending) return;
                if (Irc.State != IrcState.Ready) return;

                //trigger refresh
                banlist = new List<string>();
                Irc.SetChannelMode("+b");
                refresh_pending = true;
            }
        }

        /// <summary>
        /// Perform bansystem update
        /// </summary>
        public static void PerformUpdate()
        {
            DateTime now = DateTime.UtcNow;
            foreach (Ban ban in State.BanList.GetItems())
            {
                if (ban.Enforcer == BanEnforcement.ByJtv && ban.Expires < now)
                {
                    PerformUnban(ban.Mask.Mask, true);
                }
            }
            if ((now - lastupdated).Duration() > TimeSpan.FromMinutes(1.0))
            {
                RefreshFromIrc();
            }
        }

        /// <summary>
        /// Performs an unban
        /// </summary>
        /// <param name="hostmask">The hostmask to unban</param>
        public static void PerformUnban(string hostmask, bool quiet)
        {
            int index = hostmask.IndexOf('!');
            if (index <= 0) throw new Exception("Not a valid hostmask");
            AddOperation(new PendingUnban(hostmask.Substring(0, index), quiet));
        }

        /// <summary>
        /// Performs a ban
        /// </summary>
        /// <param name="hostmask">The mask to ban</param>
        /// <param name="duration_or_null">The duration, or null</param>
        /// <param name="reason">The reason for the ban</param>
        /// <param name="by">The user who set the ban</param>
        public static void PerformBan(string hostmask, string duration_or_null, string reason, string by)
        {
            int index = hostmask.IndexOf('!');
            if (index <= 0) throw new Exception("Not a valid hostmask");
            TimeSpan actual_duration = duration_or_null == null ? TimeSpan.FromSeconds(0) : ParseDuration(duration_or_null);
            Ban ban = new Ban();
            ban.Mask = new HostMask(hostmask);
            ban.Reason = reason;
            ban.SetBy = by;
            ban.Expires = duration_or_null == null ? DateTime.MaxValue : DateTime.UtcNow + actual_duration; 
            ban.Enforcer = BanEnforcement.ByJtv;
            ban.Affected = hostmask.Substring(0, index);
            AddOperation(new PendingBan(ban.Affected, ban));
        }

        /// <summary>
        /// Called when a channel ban list item has been received
        /// </summary>
        /// <param name="hostmask">The hostmask that was banned</param>
        /// <param name="who">The nickname that set the ban</param>
        /// <param name="when">The time at which the ban was set</param>
        public static void OnIrcBanList(string hostmask, string who, DateTime when)
        {
            lock (State.GlobalSync)
            {
                int index = hostmask.IndexOf('!');
                banlist.Add(index <= 0 ? hostmask : hostmask.Substring(0, index));
            }
        }

        /// <summary>
        /// Called when the channel ban list has been received
        /// </summary>
        public static void OnIrcEndOfBanList()
        {
            lock (State.GlobalSync)
            {
                try
                {
                    foreach (PendingOperation op in pending)
                    {
                        op.Execute(banlist);
                    }
                    pending.Clear();
                    List<Ban> toremove = new List<Ban>();
                    foreach (Ban ban in State.BanList.GetItems())
                    {
                        if (ban.Enforcer != BanEnforcement.ByJtv) break;
                        string hostmask = ban.Mask.Mask;
                        int index = hostmask.IndexOf('!');
                        if (index <= 0) break;
                        string nick = hostmask.Substring(0, index);
                        if (banlist.Contains(nick))
                        {
                            banlist.Remove(nick);
                        }
                        else if (ban.Expires == DateTime.MaxValue || ban.Expires < DateTime.UtcNow)
                        {
                            toremove.Add(ban);
                        }
                    }
                    foreach (Ban ban in toremove)
                    {
                        Program.Log("Removing ban on " + ban.Affected + " from internal list, it's no longer on the JTV ban list");
                        State.BanList.Remove(ban);
                    }
                    foreach (string nick in banlist)
                    {
                        Ban ban = new Ban();
                        ban.Affected = nick;
                        ban.Enforcer = BanEnforcement.ByJtv;
                        ban.Expires = DateTime.MaxValue;
                        ban.Mask = new HostMask(nick);
                        ban.Reason = "Manual JTV ban";
                        ban.SetBy = "JTV";
                        State.BanList.Add(ban);
                    }
                }
                finally
                {
                    lastupdated = DateTime.UtcNow;
                    refresh_pending = false;
                    banlist = null;
                }
            }
        }

        /// <summary>
        /// Check if user banned
        /// </summary>
        /// <param name="nick">Person who spoke</param>
        /// <returns>True if banned</returns>
        public static bool OnIrcChannelMessage(string nick)
        {
            lock (State.GlobalSync)
            {
                HostMask mask = new HostMask(nick);
                Ban ban = State.BanList.Lookup(mask.Mask);
                if (ban == null || ban.Expires < DateTime.UtcNow || ban.Expires == DateTime.MaxValue || ban.Enforcer != BanEnforcement.ByJtv) return false;
                Irc.Kick(nick, "");
                TimeSpan remaining = (ban.Expires - DateTime.UtcNow).Duration();
                double seconds = remaining.TotalSeconds;
                string time = seconds < 100.0 ? ((int)seconds).ToString() + " seconds" : ((int)(seconds / 60.0 + 0.5)).ToString() + " minutes";
                Irc.SendChannelMessage(nick + ", you are still banned for " + time + ": " + ban.Reason, false);
                return true;
            }
        }

        /// <summary>
        /// Looks for the banmask with which a user was banned
        /// </summary>
        /// <returns>The host mask with which the user was banned, or null, if not found</returns>
        public static HostMask FindBanByNick(string nick)
        {
            lock (State.GlobalSync)
            {
                HostMask tofind = new HostMask(nick);
                return State.BanList.Lookup(tofind.Mask) == null ? null : tofind;
            }
        }

        /// <summary>
        /// Creates a banmask for a nickname
        /// </summary>
        /// <param name="nick">The nick to find a banmask for</param>
        /// <returns>A ban mask, or null</returns>
        public static HostMask CreateBanMask(string nick)
        {
            lock (State.GlobalSync)
            {
                return new HostMask(nick);
            }
        }
#endif

        /// <summary>
        /// Try to parse string as a duration
        /// </summary>
        /// <param name="duration">The duration string to parse</param>
        /// <param name="timespan">The resulting time span</param>
        /// <returns>True if parsing succeeded</returns>
        public static bool TryParseDuration(string duration, out TimeSpan timespan)
        {
            timespan = new TimeSpan();
            if (string.IsNullOrEmpty(duration)) return false;
            int count = 0;
            TimeSpan result = new TimeSpan(0);
            for (int idx = 0; idx < duration.Length; idx++)
            {
                char c = duration[idx];
                if (char.IsDigit(c))
                {
                    count = count * 10 + int.Parse(new string(c, 1));
                }
                else
                {
                    if (count == 0)
                    {
                        return false;
                    }
                    switch (c)
                    {
                        case 'm':
                            result = result.Add(new TimeSpan(0, count, 0));
                            break;
                        case 'h':
                            result = result.Add(new TimeSpan(count, 0, 0));
                            break;
                        case 'd':
                            result = result.Add(new TimeSpan(count, 0, 0, 0));
                            break;
                        case 'w':
                            result = result.Add(new TimeSpan(count * 7, 0, 0, 0));
                            break;
                        case 'M':
                            result = result.Add(new TimeSpan(count * 30, 0, 0, 0));
                            break;
                        case 'y':
                            result = result.Add(new TimeSpan(count * 365, 0, 0, 0));
                            break;
                        default:
                            return false;
                    }
                    count = 0;
                }
            }
            if (count != 0)
            {
                return false;
            }
            timespan = result;
            return true;
        }


        /// <summary>
        /// Try to parse a timespan as a duration
        /// </summary>
        /// <param name="span">The timespan to parse</param>
        /// <param name="duration">The resulting duration string</param>
        /// <returns>True if parsing succeeded</returns>
        public static bool TryParseDuration(TimeSpan span, out string duration)
        {
            duration = default(string);
            int years = span.Days / 365;
            int days = span.Days % 365;
            int hours = span.Hours;
            int minutes = span.Minutes;
            string result = "";
            if (years > 0) result += years.ToString() + "y";
            if (days > 0) result += days.ToString() + "d";
            if (hours > 0) result += hours.ToString() + "h";
            if (minutes > 0) result += minutes.ToString() + "m";
            if (result.Length == 0) return false;
            duration = result;
            return true;
        }

        /// <summary>
        /// Parse ban duration
        /// </summary>
        /// <param name="duration">String representing duration</param>
        /// <returns>Time interval representing the duration</returns>
        public static TimeSpan ParseDuration(string duration)
        {
            TimeSpan result;
            if (!TryParseDuration(duration, out result)) throw new Exception("Failed to parse '" + duration + "' as a duration");
            return result;
        }

        /// <summary>
        /// Parse TimeSpan as a duration string
        /// </summary>
        /// <param name="span">The TimeSpan to parse</param>
        /// <returns>The duration string</returns>
        public static string ParseDuration(TimeSpan span)
        {
            string result;
            if (!TryParseDuration(span, out result)) throw new Exception("Failed to parse timespan '" + span.ToString() + "' as a duration");
            return result;
        }
    }
}
