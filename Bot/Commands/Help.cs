using System;
using System.Collections.Generic;
namespace desBot
{
    /// <summary>
    /// Sort commands alphabetically
    /// </summary>
    class AlphabeticalCommandComparer : IComparer<Command>
    {
        public int Compare(Command x, Command y) { return string.Compare(x.GetKeyword(), y.GetKeyword()); }
    }

    /// <summary>
    /// Command that provides help on commands
    /// </summary>
    class HelpCommand : Command
    {
        public static void AutoRegister()
        {
            new HelpCommand();
        }

        HelpCommand()
        {
            //Privilege = PrivilegeLevel.OnChannel;
            Privilege = PrivilegeLevel.Operator;
        }

        public override string GetKeyword()
        {
            return "help";
        }

        public override string GetHelpText(PrivilegeLevel current, string more)
        {
            return " [<command>]: Provides help text for command (if command not specified, lists commands)";
        }

        public bool IsAvailable(string nick, Command command, IrcMessage msg, bool all)
        {
            if (command.Privilege > ((msg.IsChannelMessage && Program.IsJTV && !all) ? PrivilegeLevel.OnChannel : CommandHandler.GetPrivilegeLevel(nick))) return false;
#if QNETBOT
            if (command.QRequired > Irc.QLevel) return false;
#elif JTVBOT
            if(command.QRequired > QAuthLevel.None) return false;
#endif
            if (msg.IsPrivateMessage && command.TriggerOnPrivate) return true;
            if (msg.IsChannelMessage && command.TriggerOnChannel) return true;

            return false;
        }

        public override void Execute(IrcMessage message, string args)
        {
            Dictionary<string, Command> commands = CommandHandler.GetCommands();
            bool all = false;
            if (args.ToLower() == "all")
            {
                args = "";
                all = true;
            }
            if (args.Length == 0)
            {
                //list commands
                List<Command> available = new List<Command>();
                foreach (Command command in commands.Values)
                {
                    if (!command.IsHelpEnumerable) continue;
                    if (IsAvailable(message.From, command, message, all)) available.Add(command);
                }

                //if no commands available, say nothing
                if (available.Count == 0) return;

                //sort alphabetical
                available.Sort(new AlphabeticalCommandComparer());

                //make list
                string result = "The following commands are available to you:";
                foreach (Command command in available)
                {
                    result += " " + (command.RequiresPrefix ? CommandHandler.GetDefaultPrefix() : "") + command.GetKeyword();
                }
                result += " - To obtain help on a specific command, use '" + CommandHandler.GetDefaultPrefix() + "help <command>'";

                //send to user
                message.ReplyPrivate(result);
            }
            else
            {
                //look up command
                string keyword = CommandHandler.RemovePrefixFromString(args);
                int space = keyword.IndexOf(' ');
                if (space == -1) space = keyword.Length;
                string more = keyword.Substring(space).Trim();
                keyword = keyword.Substring(0, space).ToLower();
                if (commands.ContainsKey(keyword))
                {
                    Command command = commands[keyword];
                    if (IsAvailable(message.From, command, message, true))
                    {
                        message.ReplyAuto((command.RequiresPrefix ? CommandHandler.GetDefaultPrefix() : "") + keyword + command.GetHelpText(CommandHandler.GetPrivilegeLevel(message.From), more));
                        return;
                    }
                }
                message.ReplyPrivate("That command does not exist or is not available to you");
            }
        }
    }
}