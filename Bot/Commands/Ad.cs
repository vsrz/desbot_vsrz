using System;
using System.Net;
using System.Globalization;
namespace desBot
{
    /// <summary>
    /// Command that repeats text in channel
    /// </summary>
    class AdCommand : Command
    {
        public static void AutoRegister()
        {
            new AdCommand();
        }

        AdCommand()
        {
            Privilege = PrivilegeLevel.Operator;
        }

        public override string GetKeyword()
        {
            return "ad";
        }

        public override string GetHelpText(PrivilegeLevel current, string more)
        {
            return " [<key> [<value]]: If empty, show current advertisement. Keys are interval, enabled, and text.";
        }

        //keys
        const string KeyInterval = "interval";
        const string KeyEnabled = "enabled";
        const string KeyText = "text";

        //min interval
        const int MinInterval = 5;

        //show value for key
        void ShowKey(IrcMessage msg, string key)
        {
            switch (key.ToLower())
            {
                case KeyInterval:
                    msg.ReplyAuto("Advertisements will repeat every " + State.AdInterval.Value + " minutes");
                    break;
                case KeyEnabled:
                    msg.ReplyAuto("Advertising is " + (State.AdEnabled.Value ? "enabled" : "disabled"));
                    break;
                case KeyText:
                    if (State.AdText.Value == String.Empty)
                    {
                        msg.ReplyAuto("No ad text has been set");
                    } else msg.ReplyAuto("Current Ad: \"" + State.AdText.Value + "\"");
                    break;
                default:
                    throw new Exception("Unknown key specified");
            }
        }

        //set value for key
        void SetKey(IrcMessage msg, string key, string value)
        {
            switch (key.ToLower())
            {
                case KeyInterval:
                    {
                        int minutes;
                        if (int.TryParse(value, out minutes) && minutes >= MinInterval)
                        {
                            State.AdInterval.Value = minutes;
                            msg.ReplyAuto("Advertising repeat set to " + minutes + " minutes");
                        }
                        else throw new Exception("Interval must be integral value (minutes), >= " + MinInterval);
                    }
                    break;
                case KeyEnabled:
                    {
                        switch (value.ToLower())
                        {
                            case "1":
                            case "true":
                            case "yes":
                                State.AdEnabled.Value = true;
                                msg.ReplyAuto("Ads have been enabled");
                                break;
                            case "0":
                            case "false":
                            case "no":
                                State.AdEnabled.Value = false;
                                msg.ReplyAuto("Ads will no longer be displayed");
                                break;
                            default:
                                throw new Exception("Enabled flag must be 'true' or 'false'");
                        }
                    }
                    break;
                case KeyText:
                    {                        
                        State.AdText.Value = value;
                        msg.ReplyAuto("Advertisement has been set.");
                    }
                    break;
                default:
                    throw new Exception("Unknown key specified");
            }
        }

        //last repeat
        static DateTime lastrepeat = DateTime.MinValue;

        /* Check if ads are enabled and we want to send one */
        public static void CheckAd()
        {
            /* Check if ads are enabled */
            if (State.AdEnabled.Value)
            {
                /* Check if the time has expired */
                if ((DateTime.UtcNow - lastrepeat).TotalMinutes >= State.AdInterval.Value)
                {
                    AdCommand.SendAd();
                }
            }
        }

        static void SendAd()
        {            
            lastrepeat = DateTime.UtcNow;
            Irc.SendChannelMessage(State.AdText.Value, false);
        }

        //execute command
        public override void Execute(IrcMessage message, string args)
        {
            if (string.IsNullOrEmpty(args))
            {
                SendAd();
            }
            else
            {
                string[] words = args.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length == 0)
                {
                    ShowKey(message, "text");
                }
                else if (words.Length == 1)
                {
                    ShowKey(message, words[0]);
                }
                else if (words.Length == 2)
                {
                    SetKey(message, words[0], words[1]);
                }
                else if (words[0].ToLower() == "text")
                {
                    string text = null;
                    for (int i = 1; i < words.Length; i++)
                        text = (text == null) ? words[i] : text + " " + words[i];
                    if (text == null)
                    {
                        throw new Exception("Cannot advertise nothing");
                    }
                    SetKey(message, words[0], text);
                }
                else throw new Exception("Syntax is error, expects: !ad [<key> [<value>]]");
            }
        }
    }
}