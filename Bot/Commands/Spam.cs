using System;
using System.Collections.Generic;
using System.Text;

namespace desBot
{
    /// <summary>
    /// Command that generates a random number
    /// </summary>
    class SpamCommand : Command
    {
        public static void AutoRegister()
        {
            new SpamCommand();
        }

        SpamCommand()
        {
            Privilege = PrivilegeLevel.Operator;
        }

        public override string GetKeyword()
        {
            return "spam";
        }

        public override string GetHelpText(PrivilegeLevel current, string more)
        {
            return " [set (0|1|2)]: Set the spam level - 0 = disabled - 1 = purge links - 2 = purge and tempban links";
        }

        public override void Execute(IrcMessage message, string args)
        {
            string[] arg = args.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string prefix = "The current spam level is: ";
            if (arg.Length == 2 && arg[0].ToLower() == "set")
            {
                //set spam level
                int newlevel;
                if (!int.TryParse(arg[1], out newlevel) || newlevel < 0 || newlevel > 2)
                {
                    message.ReplyAuto("The spam level can only be set to 0, 1 or 2");
                    return;
                }
                else
                {
                    prefix = "The new spam level is: ";
                    State.AntiSpamLevel.Value = newlevel;
                }
            }
            
            //print spam level
            string level = "<invalid>";
            switch (State.AntiSpamLevel.Value)
            {
                case 0:
                    level = "level 0 - anti-spam is disabled";
                    break;
                case 1:
                    level = "level 1 - links are purged";
                    break;
                case 2:
                    level = "level 2 - links are purged, and offender is tempbanned";
                    break;
            }
            message.ReplyAuto(prefix + level);
        }
    }
}
