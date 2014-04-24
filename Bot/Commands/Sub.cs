using System;
using System.Net;
using System.Globalization;
namespace desBot
{
    /// <summary>
    /// Command that welcomes new subscribers in chat
    /// </summary>
    class NewSubCommand : Command
    {
        public static void AutoRegister()
        {
            new NewSubCommand();
        }

        NewSubCommand()
        {
            Privilege = PrivilegeLevel.Operator;
        }

        public override string GetKeyword()
        {
            return "sub";
        }

        public override string GetHelpText(PrivilegeLevel current, string more)
        {
            return " [<message>]: If empty, shows the current welcome message. \"disable\" will turn off messages. Use \"%s\" where you want the name to appear.";
        }

        public void ShowHelpText(IrcMessage msg)
        {
            msg.ReplyAuto(GetHelpText(Privilege, ""));
        }

        public void DisableWelcomeMessages()
        {
            State.NewSubText.Value = "";
        }

        public void ShowWelcomeMessage(IrcMessage msg)
        {
            msg.ReplyAuto("New subs will be welcomed with: " + State.NewSubText.Value);
        }

        public void SetNewSubText(String message)
        {
            State.NewSubText.Value = message;
        }

        /// <summary>
        /// Announces a new subscription, called when string matches in service
        /// </summary>
        /// <param name="msg"></param>
        public static void AnnounceNewSubscription(IrcMessage msg)
        {
            if (State.NewSubText.Value.Length < 1)
            {
                return;
            }

            if (msg.From.ToLower() == State.NewSubNotifyUser.Value.ToLower())
            {
                string username;
                string announcement = State.NewSubText.Value;
                string[] words = msg.Text.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (words[1] == "just" && words[2] == "subscribed!")
                {
                    username = words[0];
                    announcement = announcement.Replace("%s", username);
                    msg.ReplyAuto(announcement);
                }
                
            }
        }

        public override void Execute(IrcMessage message, string args)
        {
            if (string.IsNullOrEmpty(args))
            {
                ShowWelcomeMessage(message);
            }
            else
            {
                string[] words = args.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length == 0)
                {
                    ShowWelcomeMessage(message);
                }
                else if (words.Length == 1)
                {
                    if (words[0].ToLower() == "disable")
                    {
                        message.ReplyAuto("New subscribers will no longer be welcomed");
                        DisableWelcomeMessages();
                    }
                    else
                    {
                        ShowHelpText(message);
                    }
                }
                else 
                {
                    SetNewSubText(args);
                    ShowWelcomeMessage(message);
                }                
            }
        }
    }
}