using System;
namespace desBot
{
#if JTVBOT
    /// <summary>
    /// Command that manages slow mode
    /// </summary>
    class PurgeCommand : Command
    {
        public static void AutoRegister()
        {
            new PurgeCommand();
        }

        PurgeCommand()
        {
            Privilege = PrivilegeLevel.Operator;
        }

        public override string GetKeyword()
        {
            return "purge";
        }

        public override string GetHelpText(PrivilegeLevel current, string more)
        {
            return " <who>: Purges all text from a user";
        }

        public override void Execute(IrcMessage message, string args)
        {
            if(args.Contains(" ") || args.Length == 0)
            {
                message.ReplyAuto("Usage: '!purge <name>', where <name> is replaced by the target user");
            }
            else
            {
                if (CommandHandler.GetPrivilegeLevel(args) >= PrivilegeLevel.Operator)
                {
                    throw new Exception("Unable to purge moderator/operator");
                }
                else
                {
                    JTV.Purge(args);
                    message.ReplyAuto("Chat from '" + args + "' was purged");
                }
            }
        }
    }
#endif
}