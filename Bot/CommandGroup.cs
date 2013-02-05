using System;
using System.Collections.Generic;
using System.Text;

namespace desBot
{
    /// <summary>
    /// Command group is a utility that groups a set of related commands on a second keyword
    /// ie, 'quote add', 'quote del', 'quote find' form a command group 'quote' with 'add', 'del' and 'find' subcommands
    /// </summary>
    abstract class CommandGroup : Command
    {
        List<Command> subcommands = new List<Command>();
        
        public string DefaultSubCommand = null;

        public void AddSubCommand(Command sub)
        {
            if (subcommands.Contains(sub)) throw new Exception("Duplicate sub command");
            subcommands.Add(sub);
        }

        public override string GetHelpText(PrivilegeLevel current, string more)
        {
            if(more.Length != 0)
            {
                int space = more.Trim().IndexOf(' ');
                string subname = space == -1 ? more.Trim().ToLower() : more.Trim().Substring(0, space).ToLower();
                foreach (Command sub in subcommands)
                {
                    if (sub.GetKeyword().ToLower() == subname)
                    {
                        return " " + subname + sub.GetHelpText(current, null);
                    }
                }
            }
            string result = " - Use 'help " + GetKeyword() + " <command>' to get help for a subcommand, <command> can be any of: ";
            int count = 0;
            foreach (Command sub in subcommands)
            {
                if (sub.Privilege <= current)
                {
                    result += sub.GetKeyword() + (sub.GetKeyword() == DefaultSubCommand ? " (default)" : "") + ", ";
                    count++;
                }
            }
            if (count == 0) return ": no subcommands available at your privilege level";
            else return result.Substring(0, result.Length - 2);
        }

        public override void Execute(IrcMessage message, string args)
        {
            int space = args.IndexOf(' ');
            if (space == -1) space = args.Length;
            if (space == 0 && string.IsNullOrEmpty(DefaultSubCommand)) throw new Exception("Expected a subcommand, please see 'help " + GetKeyword() + "'");
            string subname = args.Substring(0, space);
            args = args.Substring(space).Trim();
            for (int i = 0; i < 2; i++)
            {
                foreach (Command sub in subcommands)
                {
                    if (sub.GetKeyword() == subname && sub.Privilege <= CommandHandler.GetPrivilegeLevel(message.From))
                    {
                        sub.Execute(message, args);
                        return;
                    }
                }
                if (i == 0 && !string.IsNullOrEmpty(DefaultSubCommand))
                {
                    subname = DefaultSubCommand;
                }
                else break;
            }
            throw new Exception("Cannot find any subcommand '" + subname + "' for command '" + GetKeyword() + "'");
        }
    }

    /// <summary>
    /// Subcommand prevents automatic registration
    /// </summary>
    abstract class SubCommand : Command
    {
        public CommandGroup Parent { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public SubCommand(CommandGroup group)
            : base(true)
        {
            Parent = group;
            group.AddSubCommand(this);
        }
    }
}
