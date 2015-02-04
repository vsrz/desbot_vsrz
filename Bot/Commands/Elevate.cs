using System;
namespace desBot
{
    /// <summary>
    /// Elevates operator to developer
    /// </summary>
    class ElevateCommand : Command
    {
        public static void AutoRegister()
        {
            new ElevateCommand();
        }

        ElevateCommand()
        {
            Privilege = PrivilegeLevel.Operator;
            TriggerOnChannel = true;
            IsHelpEnumerable = false;
        }

        public override string GetKeyword()
        {
            return "devcmd";
        }

        public override string GetHelpText(PrivilegeLevel current, string more)
        {
            return " [on|off]: Turns on or off additional developer commands, which are not very useful for non-developers";
        }

        public override void Execute(IrcMessage message, string args)
        {
            User user = State.UserList.Lookup(message.From);
            if (user == null) throw new Exception("User not known");
            bool enabled;
            if (args.ToLower() == "on") enabled = true;
            else if (args.ToLower() == "off") enabled = false;
            else throw new Exception("Argument expected: on or off");
            user.Meta.Elevation = enabled;
            State.MetaUserList.MarkChanged(user.Meta);
            message.ReplyPrivate("Developer commands are now " + (user.Meta.Elevation ? "enabled" : "disabled") + " for " + message.From);
        }
    }
}