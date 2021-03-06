﻿using System;
namespace desBot
{
    /// <summary>
    /// Command that identifies the user and privilege level
    /// </summary>
    class WhoamiCommand : Command
    {
        public static void AutoRegister()
        {
            new WhoamiCommand();
        }

        WhoamiCommand()
        {
            Privilege = PrivilegeLevel.Voiced;
        }

        public override string GetKeyword()
        {
            return "whoami";
        }

        public override string GetHelpText(PrivilegeLevel current, string more)
        {
            return ": Tells the privilege level the bot thinks you have";
        }

        public override void Execute(IrcMessage message, string args)
        {
            if (Limiter.AttemptOperation(message.Level))
            {
                PrivilegeLevel privilege = CommandHandler.GetPrivilegeLevel(message.From);
                string priv;
                switch (privilege)
                {
                    case PrivilegeLevel.Developer:
                    case PrivilegeLevel.OnChannel:
                    case PrivilegeLevel.Operator:
                    case PrivilegeLevel.Subscriber:
                        priv = " " + privilege.ToString();
                        break;

                    default:
                        priv = "n " + privilege.ToString();
                        break;

                }
                message.ReplyPrivate("'sup " + message.From + ", you are a" + priv);
            }
        }
    }
}