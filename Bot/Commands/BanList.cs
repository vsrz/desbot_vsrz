using System;
using System.Collections.Generic;
using System.Text;

namespace desBot
{
#if !JTVBOT
    class BanListCommandGroup : CommandGroup
    {
        class FindCommand : SubCommand
        {
            public FindCommand(CommandGroup group) : base(group) { Privilege = PrivilegeLevel.Operator; }

            public override string GetKeyword()
            {
                return "find";
            }

            public override string GetHelpText(PrivilegeLevel current, string more)
            {
                return " <word> [<word>]: Looks for bans containing all of the specified words";
            }

            public override void Execute(IrcMessage message, string args)
            {
                //search terms
                string[] words = args.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if(words.Length == 0) throw new Exception("No search terms specified");

                //look for bans
                List<Ban> found = new List<Ban>();
                foreach (Ban ban in State.BanList.GetItems())
                {
                    bool matches = true;
                    foreach (string word in words)
                    {
                        if (!(ban.Affected.Contains(word) || ban.Mask.Mask.Contains(word) || ban.Reason.Contains(word)))
                        {
                            //skip bans of bad type
                            if (Program.IsJTV ^ ban.Enforcer == BanEnforcement.ByJtv) break;
                            matches = false;
                            break;
                        }
                    }
                    if (matches) found.Add(ban);
                }

                //output result
                if (found.Count == 0) message.ReplyAuto("No ban matching your request was found");
                else if (found.Count > 3) message.ReplyAuto("Too many bans match your request, please be more specific");
                else
                {
                    foreach (Ban ban in found)
                    {
                        message.ReplyAuto("Mask: " + ban.Mask.Mask + " which affects '" + ban.Affected + "' expires " + (ban.Expires == DateTime.MaxValue ? "never" : "at " + ban.Expires.ToString()) + ", reason: " + ban.Reason);
                    }
                }
            }
        }

        class CountCommand : SubCommand
        {
            public CountCommand(CommandGroup group) : base(group) { Privilege = PrivilegeLevel.Operator; }

            public override string GetKeyword()
            {
                return "count";
            }
            
            public override string GetHelpText(PrivilegeLevel current, string more)
            {
                return ": Returns the number of bans being enforced on the channel";
            }

            public override void Execute(IrcMessage message, string args)
            {
                if (args.Length != 0) throw new Exception("Failed to parse command");
                int jtvcount = 0;
                int othercount = 0;
                foreach (Ban ban in State.BanList.GetItems())
                {
                    if (ban.Enforcer == BanEnforcement.ByJtv) jtvcount++;
                    else othercount++;
                }
#if JTVBOT
                message.ReplyAuto("Currently " + jtvcount.ToString() + " bans are affecting the JTV chat");
#elif QNETBOT
                message.ReplyAuto("Currently " + State.BanList.GetCount() + " bans are affecting the channel, of which " + Irc.CountBans().ToString() + " bans are enforced on the channel");
#endif
                
            }
        }

#if QNETBOT
        class OffloadCommand : SubCommand
        {
            class PendingUnban
            {
                string mask;

                public PendingUnban(QCommand condition, string mask)
                {
                    this.mask = mask;
                    condition.OnFinished += new QCommand.OnFinishedDelegate(ExecuteIfTrue);
                }

                void ExecuteIfTrue(bool condition)
                {
                    if (condition)
                    {
                        Program.Log("Unbanning " + mask + " after offload succeeded");
                        Irc.Unban(mask);
                    }
                    else
                    {
                        Program.Log("Not unbanning " + mask + " because offload failed");
                    }
                }
            }

            class PendingBan
            {
                string mask;

                public PendingBan(QCommand condition, string mask)
                {
                    this.mask = mask;
                    condition.OnFinished += new QCommand.OnFinishedDelegate(ExecuteIfTrue);
                }

                void ExecuteIfTrue(bool condition)
                {
                    if (condition)
                    {
                        Program.Log("Banning " + mask + " after offload succeeded");
                        Irc.Ban(mask);
                    }
                    else
                    {
                        Program.Log("Not banning " + mask + " because offload failed");
                    }
                }
            }

            class PendingSetEnforcer
            {
                string mask;
                BanEnforcement enforcer;

                public PendingSetEnforcer(QCommand condition, string mask, BanEnforcement enforcer)
                {
                    this.mask = mask;
                    this.enforcer = enforcer;
                    condition.OnFinished += new QCommand.OnFinishedDelegate(ExecuteIfTrue);
                }

                void ExecuteIfTrue(bool condition)
                {
                    if (condition)
                    {
                        Program.Log("Setting enforcer of " + mask + " to " + enforcer.ToString() + " after offload succeeded");
                        Ban ban = State.BanList.Lookup(mask);
                        if (ban != null)
                        {
                            ban.Enforcer = enforcer;
                            State.BanList.MarkChanged(ban);
                        }
                    }
                    else
                    {
                        Program.Log("Not settings enforcer of " + mask + " to " + enforcer.ToString() + " because offload failed");
                    }
                }
            }

            class PendingOffload
            {
                int total = 0;
                int count = 0;
                int succeeded = 0;
                IrcMessage message;

                public PendingOffload(IrcMessage message)
                {
                    this.message = message;
                }

                public void Add(QCommand command)
                {
                    total++;
                    count++;
                    command.OnFinished += new QCommand.OnFinishedDelegate(Decrement);
                }

                void Decrement(bool succeeded)
                {
                    count--;
                    if(succeeded) this.succeeded++;
                    if (count == 0)
                    {
                        message.ReplyAuto(total.ToString() + " ban offloads completed, of which " + this.succeeded.ToString() + " succeeded");
                    }
                }
            }

            class BanTypeMatches
            {
                BanEnforcement type;
                public BanTypeMatches(BanEnforcement type) { this.type = type; }
                public bool Test(Ban ban) { return ban.Enforcer == type; }
            }

            public OffloadCommand(CommandGroup group) : base(group) { Privilege = PrivilegeLevel.Operator; }

            public override string GetKeyword()
            {
                return "offload";
            }

            public override string GetHelpText(PrivilegeLevel current, string more)
            {
                return " from <type> to <type> [limit <count>]: Offloads bans from one banlist to another, optionally limiting to <count> offloads. Type can be 'q', 'bot' or 'channel'";
            }

            static BanEnforcement ParseEnforcement(string str)
            {
                if (str == "q") return BanEnforcement.ByQ;
                if (str == "bot") return BanEnforcement.ByMe;
                if (str == "channel") return BanEnforcement.ByChannel;
                throw new Exception("Failed to parse '" + str + "' as ban type");
            }

            public override void Execute(IrcMessage message, string args)
            {
                //parse arguments
                string[] arg = args.ToLower().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (arg.Length != 4 && arg.Length != 6) throw new Exception("Bad argument count");
                if (arg[0] != "from" || arg[2] != "to" || (arg.Length == 6 ? arg[4] != "limit" : false)) throw new Exception("Bad argument value");
                BanEnforcement from = ParseEnforcement(arg[1]);
                BanEnforcement to = ParseEnforcement(arg[3]);
                if (to == from) throw new Exception("Cannot offload when to and from are the same");
                int limit = 100;
                if (arg.Length == 6 && !int.TryParse(arg[5], out limit)) throw new Exception("Failed to parse limit");
                if (limit <= 0 || limit > 100) throw new Exception("Limit out of valid range (1-100)");

                //max allowed
                int max = 100;
                if (to == BanEnforcement.ByQ) max = BanSystem.QMaxBans - BanSystem.OffloadMargin - State.BanList.CountIf(new Predicate<Ban>(new BanTypeMatches(BanEnforcement.ByQ).Test));
                if (to == BanEnforcement.ByChannel) max = BanSystem.CMaxBans - BanSystem.OffloadMargin - State.BanList.CountIf(new Predicate<Ban>(new BanTypeMatches(BanEnforcement.ByChannel).Test));

                //find candidates
                int count = 0;
                PendingOffload offload = new PendingOffload(message);
                foreach (Ban ban in State.BanList.GetItems())
                {
                    if (ban.Enforcer == from && (to == BanEnforcement.ByChannel ? ban.Expires == DateTime.MaxValue : true))
                    {
                        switch(to)
                        {
                            case BanEnforcement.ByQ:
                                QCommand cmd;
                                if(ban.Expires == DateTime.MaxValue)
                                {
                                    cmd = new PermBanCommand(ban.Mask.Mask, ban.Reason);
                                }
                                else
                                {
                                    cmd = new TempBanCommand(ban.Mask.Mask, BanSystem.ParseDuration((DateTime.UtcNow - ban.Expires).Duration()), ban.Reason);
                                }
                                switch (from)
                                {
                                    case BanEnforcement.ByChannel:
                                        new PendingSetEnforcer(cmd, ban.Mask.Mask, BanEnforcement.ByQ);
                                        new PendingUnban(cmd, ban.Mask.Mask);
                                        break;
                                    case BanEnforcement.ByMe:
                                        new PendingSetEnforcer(cmd, ban.Mask.Mask, BanEnforcement.ByQ);
                                        if (!BanSystem.IsBanEnforced(ban)) new PendingUnban(cmd, ban.Mask.Mask);
                                        break;
                                }
                                offload.Add(cmd);
                                break;
                            case BanEnforcement.ByChannel:
                                switch (from)
                                {
                                    case BanEnforcement.ByQ:
                                        QCommand qcmd = new BanDelCommand(ban.Mask.Mask);
                                        new PendingBan(qcmd, ban.Mask.Mask);
                                        new PendingSetEnforcer(qcmd, ban.Mask.Mask, BanEnforcement.ByChannel);
                                        offload.Add(qcmd);
                                        break;
                                    case BanEnforcement.ByMe:
                                        if(!BanSystem.IsBanEnforced(ban)) Irc.Ban(ban.Mask.Mask);
                                        ban.Enforcer = BanEnforcement.ByChannel;
                                        Program.Log("Offload bot->channel: " + ban.Mask.Mask);
                                        break;
                                }
                                break;
                            case BanEnforcement.ByMe:
                                switch (from)
                                {
                                    case BanEnforcement.ByQ:
                                        QCommand qcmd = new BanDelCommand(ban.Mask.Mask);
                                        if(Irc.IsBanEnforced(ban.Mask.Mask)) new PendingBan(qcmd, ban.Mask.Mask);
                                        new PendingSetEnforcer(qcmd, ban.Mask.Mask, BanEnforcement.ByMe);
                                        offload.Add(qcmd);
                                        break;
                                    case BanEnforcement.ByChannel:
                                        ban.Enforcer = BanEnforcement.ByMe;
                                        Program.Log("Offload channel->bot: " + ban.Mask.Mask);
                                        break;
                                }
                                break;
                        }
                        count++;
                        if (count == limit) break;
                    }
                }
                if (count == 0) throw new Exception("No bans match the specified type");

                BanSystem.RefreshFromIrc();

                message.ReplyImmediately = true;
                message.ReplyAuto(count.ToString() + " ban offloads are pending...");
                message.ReplyImmediately = false;
                if(!(to == BanEnforcement.ByQ || from == BanEnforcement.ByQ))
                {
                    message.ReplyAuto(count.ToString() + " ban offloads succeeded");
                }
            }
        }
#endif

        class RefreshCommand : SubCommand
        {
            public RefreshCommand(CommandGroup group) : base(group) { Privilege = PrivilegeLevel.Operator; }

            public override string GetKeyword()
            {
                return "refresh";
            }

            public override string GetHelpText(PrivilegeLevel current, string more)
            {
                return ": Force a refresh of ban list data from IRC";
            }

            public override void Execute(IrcMessage message, string args)
            {
                if (args.Length != 0) throw new Exception("No arguments expected");
                BanSystem.RefreshFromIrc();
                message.ReplyAuto("Refreshing ban information");
            }
        }

#if QNETBOT
        class OptimizeCommand : SubCommand
        {
            public OptimizeCommand(CommandGroup group) : base(group) { Privilege = PrivilegeLevel.Operator; }

            public override string GetKeyword()
            {
                return "optimize";
            }

            public override string GetHelpText(PrivilegeLevel current, string more)
            {
                return " [limit <count>]: Optimizes the channel ban list, lifting up to <count> channel bans enforced by Q or me";
            }

            public override void Execute(IrcMessage message, string args)
            {
                string[] arg = args.ToLower().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                int limit = 100;
                if (arg.Length == 0) /*acceptable*/;
                else if (arg.Length == 2)
                {
                    if (arg[0] != "limit" || !int.TryParse(arg[1], out limit)) throw new Exception("Failed to parse limit");
                }
                else throw new Exception("Unexpected number of arguments");

                int result = BanSystem.FreeBanSlot(limit);
                message.ReplyAuto("Lifting " + result.ToString() + " bans from the channel ban list");
            }
        }
#endif

        public static void AutoRegister()
        {
            new BanListCommandGroup();
        }

        BanListCommandGroup()
        {
            Privilege = PrivilegeLevel.Operator;
            DefaultSubCommand = "count";
            new FindCommand(this);
            new CountCommand(this);
            new RefreshCommand(this);
#if QNETBOT
            new OffloadCommand(this);
            new OptimizeCommand(this);
#endif
        }

        public override string GetKeyword()
        {
            return "banlist";
        }
    }
#endif
}
