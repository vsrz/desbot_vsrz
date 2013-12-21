using System;
using System.Collections.Generic;
using System.Threading;
namespace desBot
{
    class NukeAddCommand : SubCommand
    {
        public NukeAddCommand(CommandGroup group) : base(group)
        {
            Privilege = PrivilegeLevel.Operator;
        }

        public override string GetKeyword()
        {
            return "add";
        }
        
        public override string GetHelpText(PrivilegeLevel current, string more)
        {
            return " <text>: Nukes any user who mentions this phrase in chat.";
        }

        public override void Execute(IrcMessage message, string args)
        {
            //look for next highest ID
            int maxid = 0;
            foreach (Nuke needle in State.NukeList.GetItems())
            {
                if (needle.ID > maxid) maxid = needle.ID;
            }
            maxid++;

            //create new Nuke
            Nuke Nuke = new Nuke();
            Nuke.Created = DateTime.UtcNow;
            Nuke.SetBy = message.From;
            Nuke.Text = args;
            Nuke.ID = maxid;

            //add to collection
            State.NukeList.Add(Nuke);

            //reply
            string text = ControlCharacter.Enabled ? Nuke.Text : ControlCharacter.Strip(Nuke.Text);
            message.ReplyAuto(message.From + " added Nuke " + ControlCharacter.Bold() + "#" + Nuke.ID.ToString() + ControlCharacter.Bold() + ": " + text);
        }
    }

    class NukeInfoCommand : SubCommand
    {
        public NukeInfoCommand(CommandGroup group) : base(group)
        {
            Privilege = PrivilegeLevel.OnChannel;
        }

        public override string GetKeyword()
        {
            return "info";
        }

        public override string GetHelpText(PrivilegeLevel current, string more)
        {
            return " <id>: Diplays information about a nuke with the given <id>";
        }

        public override void Execute(IrcMessage message, string args)
        {
            if (Parent.Limiter.AttemptOperation(message.Level))
            {
                //look up by ID
                int id = -1;
                Nuke Nuke = null;
                if (int.TryParse(args, out id))
                {
                    foreach (Nuke needle in State.NukeList.GetItems())
                    {
                        if (needle.ID == id)
                        {
                            Nuke = needle;
                            break;
                        }
                    }
                }
                if (Nuke == null)
                {
                    //not found
                    message.ReplyAuto("no Nuke #" + args + " was found");
                }
                else
                {
                    //print Nuke and info
                    string text = ControlCharacter.Enabled ? Nuke.Text : ControlCharacter.Strip(Nuke.Text);
                    message.ReplyAuto(Nuke.SetBy + " added Nuke " + ControlCharacter.Bold() + "#" + Nuke.ID.ToString() + ControlCharacter.Bold() + " on " + Nuke.Created.ToString() + ": " + text);
                }
            }
        }
    }

    class NukeFindCommand : SubCommand
    {
        public NukeFindCommand(CommandGroup group) : base(group)
        {
            Privilege = PrivilegeLevel.OnChannel;
        }

        public override string GetKeyword()
        {
            return "find";
        }

        public override string GetHelpText(PrivilegeLevel current, string more)
        {
            return "<keyword(s)>: Searches through nuke phrases that contain all of the specified keywords";
        }

        public override void Execute(IrcMessage message, string args)
        {
            if (Parent.Limiter.AttemptOperation(message.Level))
            {
                //look up by word
                string[] words = args.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length != 0)
                {
                    string reply = "";
                    int count = 0;
                    Nuke Nuke = null;
                    foreach (Nuke needle in State.NukeList.GetItems())
                    {
                        int found = 0;
                        foreach (string word in words)
                        {
                            if (!needle.Text.Contains(word)) break;
                            found++;
                        }
                        if (found == words.Length)
                        {
                            Nuke = needle;

                            //print Nuke
                            string text = ControlCharacter.Enabled ? Nuke.Text : ControlCharacter.Strip(Nuke.Text);
                            reply += "Nuke " + ControlCharacter.Bold() + "#" + Nuke.ID.ToString() + ControlCharacter.Bold() + ": " + text + "\n";
                            count++;
                        }
                    }
                    if (Nuke == null)
                    {
                        //not found
                        message.ReplyAuto("no Nuke found containing the word(s): " + args);
                    }
                    else if (count > 3)
                    {
                        //too many matches
                        message.ReplyAuto("more than 3 Nukes matched your criteria, please be more exclusive in your criteria");
                    }
                    else
                    {
                        //print found Nukes
                        message.ReplyAuto(reply);
                    }
                }
                else
                {
                    //no words
                    message.ReplyPrivate("When using 'Nuke find', specify one or more words to search");
                }
            }
        }
    }

    class NukeDelCommand : SubCommand
    {
        public NukeDelCommand(CommandGroup group) : base(group)
        {
            Privilege = PrivilegeLevel.Operator;
        }

        public override string GetKeyword()
        {
            return "del";
        }

        public override string GetHelpText(PrivilegeLevel current, string more)
        {
            return " <id>: This lifts the ban given by the Nuke with a given ID";
        }
        
        public override void Execute(IrcMessage message, string args)
        {
            //look up by ID
            int id = -1;
            Nuke Nuke = null;
            if (int.TryParse(args, out id))
            {
                foreach (Nuke needle in State.NukeList.GetItems())
                {
                    if (needle.ID == id)
                    {
                        Nuke = needle;
                        break;
                    }
                }
            }
            if (Nuke == null)
            {
                //not found
                message.ReplyAuto("no Nuke #" + args + " was found");
            }
            else
            {
                //remove Nuke
                State.NukeList.Remove(Nuke);
                message.ReplyAuto("removed Nuke #" + id.ToString());
            }
        }
    }

    class NukeTimeCommand : SubCommand
    {
        public NukeTimeCommand(CommandGroup group) : base(group)
        {
            Privilege = PrivilegeLevel.Operator;
        }

        public override string GetKeyword()
        {
            return "time";
        }

        public override string GetHelpText(PrivilegeLevel current, string more)
        {
            return " [<minutes>]: Sets the ban levied by a nuke in minutes. Defaults to 10 minutes, 0 minutes levies a purge on the user.";
        }

        public override void Execute(IrcMessage message, string args)
        {
            if (args.Length == 0)
            {
                int mins = State.NukeTime.Value;
                if (mins > 0) message.ReplyAuto("Nuke penalty is set to " + mins.ToString() + " minutes");
                else message.ReplyAuto("Nuke penalty is less than 1 minute (purge only)");
            }
            else
            {
                int mins = 0;
                if (int.TryParse(args, out mins) && mins >= 0)
                {
                    State.NukeTime.Value = mins;
                    if (mins > 0) message.ReplyAuto("Users are nuked for " + mins.ToString() + " minutes");
                    else message.ReplyAuto("Nuke penalty is less than 1 minute (purge only)");
                }
                else message.ReplyAuto("Invalid argument specified: Expected a number >= 0");
            }
        }
    }
  
    class NukeCommand : CommandGroup
    {
        public static void AutoRegister()
        {
            new NukeCommand();
        }

        NukeCommand()
        {
            Privilege = PrivilegeLevel.OnChannel;
            new NukeAddCommand(this);
            new NukeDelCommand(this);
            new NukeInfoCommand(this);
            new NukeFindCommand(this);
            new NukeTimeCommand(this);
        }
        
        public override string GetKeyword()
        {
            return "Nuke";
        }
    }
}