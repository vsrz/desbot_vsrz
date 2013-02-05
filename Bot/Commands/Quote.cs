using System;
using System.Collections.Generic;
using System.Threading;
namespace desBot
{
    class QuoteAddCommand : SubCommand
    {
        public QuoteAddCommand(CommandGroup group) : base(group)
        {
            Privilege = PrivilegeLevel.Operator;
        }

        public override string GetKeyword()
        {
            return "add";
        }
        
        public override string GetHelpText(PrivilegeLevel current, string more)
        {
            return " <text>: Adds a quote to the collection, where <text> is the quote to be added";
        }

        public override void Execute(IrcMessage message, string args)
        {
            //look for next highest ID
            int maxid = 0;
            foreach (Quote needle in State.QuoteList.GetItems())
            {
                if (needle.ID > maxid) maxid = needle.ID;
            }
            maxid++;

            //create new quote
            Quote quote = new Quote();
            quote.Created = DateTime.UtcNow;
            quote.SetBy = message.From;
            quote.Text = args;
            quote.ID = maxid;

            //add to collection
            State.QuoteList.Add(quote);

            //reply
            string text = ControlCharacter.Enabled ? quote.Text : ControlCharacter.Strip(quote.Text);
            message.ReplyAuto(message.From + " added quote " + ControlCharacter.Bold() + "#" + quote.ID.ToString() + ControlCharacter.Bold() + ": " + text);
        }
    }

    class QuoteInfoCommand : SubCommand
    {
        public QuoteInfoCommand(CommandGroup group) : base(group)
        {
            Privilege = PrivilegeLevel.OnChannel;
        }

        public override string GetKeyword()
        {
            return "info";
        }

        public override string GetHelpText(PrivilegeLevel current, string more)
        {
            return " <id>: Diplays information about the quote with the given <id>";
        }

        public override void Execute(IrcMessage message, string args)
        {
            if (Parent.Limiter.AttemptOperation(message.Level))
            {
                //look up by ID
                int id = -1;
                Quote quote = null;
                if (int.TryParse(args, out id))
                {
                    foreach (Quote needle in State.QuoteList.GetItems())
                    {
                        if (needle.ID == id)
                        {
                            quote = needle;
                            break;
                        }
                    }
                }
                if (quote == null)
                {
                    //not found
                    message.ReplyAuto("no quote #" + args + " was found");
                }
                else
                {
                    //print quote and info
                    string text = ControlCharacter.Enabled ? quote.Text : ControlCharacter.Strip(quote.Text);
                    message.ReplyAuto(quote.SetBy + " added quote " + ControlCharacter.Bold() + "#" + quote.ID.ToString() + ControlCharacter.Bold() + " on " + quote.Created.ToString() + ": " + text);
                }
            }
        }
    }

    class QuoteFindCommand : SubCommand
    {
        public QuoteFindCommand(CommandGroup group) : base(group)
        {
            Privilege = PrivilegeLevel.OnChannel;
        }

        public override string GetKeyword()
        {
            return "find";
        }

        public override string GetHelpText(PrivilegeLevel current, string more)
        {
            return "<keyword(s)>: Searches for quotes containing all of the specified keywords";
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
                    Quote quote = null;
                    foreach (Quote needle in State.QuoteList.GetItems())
                    {
                        int found = 0;
                        foreach (string word in words)
                        {
                            if (!needle.Text.Contains(word)) break;
                            found++;
                        }
                        if (found == words.Length)
                        {
                            quote = needle;

                            //print quote
                            string text = ControlCharacter.Enabled ? quote.Text : ControlCharacter.Strip(quote.Text);
                            reply += "Quote " + ControlCharacter.Bold() + "#" + quote.ID.ToString() + ControlCharacter.Bold() + ": " + text + "\n";
                            count++;
                        }
                    }
                    if (quote == null)
                    {
                        //not found
                        message.ReplyAuto("no quote found containing the word(s): " + args);
                    }
                    else if (count > 3)
                    {
                        //too many matches
                        message.ReplyAuto("more than 3 quotes matched your criteria, please be more exclusive in your criteria");
                    }
                    else
                    {
                        //print found quotes
                        message.ReplyAuto(reply);
                    }
                }
                else
                {
                    //no words
                    message.ReplyPrivate("When using 'quote find', specify one or more words to search");
                }
            }
        }
    }

    class QuoteDelCommand : SubCommand
    {
        public QuoteDelCommand(CommandGroup group) : base(group)
        {
            Privilege = PrivilegeLevel.Operator;
        }

        public override string GetKeyword()
        {
            return "del";
        }

        public override string GetHelpText(PrivilegeLevel current, string more)
        {
            return " <id>: Removes the quote with the given ID from the collection";
        }
        
        public override void Execute(IrcMessage message, string args)
        {
            //look up by ID
            int id = -1;
            Quote quote = null;
            if (int.TryParse(args, out id))
            {
                foreach (Quote needle in State.QuoteList.GetItems())
                {
                    if (needle.ID == id)
                    {
                        quote = needle;
                        break;
                    }
                }
            }
            if (quote == null)
            {
                //not found
                message.ReplyAuto("no quote #" + args + " was found");
            }
            else
            {
                //remove quote
                State.QuoteList.Remove(quote);
                message.ReplyAuto("removed quote #" + id.ToString());
            }
        }
    }

    class QuoteIntervalCommand : SubCommand
    {
        public QuoteIntervalCommand(CommandGroup group) : base(group)
        {
            Privilege = PrivilegeLevel.Operator;
        }

        public override string GetKeyword()
        {
            return "interval";
        }

        public override string GetHelpText(PrivilegeLevel current, string more)
        {
            return " [<minutes>]: Sets the interval between quotes to <minutes>. If <minutes> is not specified, returns the current interval. Set to 0 to disable";
        }

        public override void Execute(IrcMessage message, string args)
        {
            if (args.Length == 0)
            {
                int mins = State.QuoteInterval.Value;
                if (mins > 0) message.ReplyAuto("The current interval between quotes is set to " + mins.ToString() + " minutes");
                else message.ReplyAuto("The quote interval is currently disabled");
            }
            else
            {
                int mins = 0;
                if (int.TryParse(args, out mins) && mins >= 0)
                {
                    State.QuoteInterval.Value = mins;
                    if (mins > 0) message.ReplyAuto("The current interval between quotes has been set to " + mins.ToString() + " minutes");
                    else message.ReplyAuto("The quote interval has been disabled");
                }
                else message.ReplyAuto("Invalid argument specified: Expected a number >= 0");
            }
        }
    }

    class QuoteRandomCommand : SubCommand
    {
        static Random rng = new Random();
        static DateTime last = DateTime.UtcNow;

        public QuoteRandomCommand(CommandGroup group) : base(group)
        {
            Privilege = PrivilegeLevel.OnChannel;
        }

        public override string GetKeyword()
        {
            return "random";
        }

        public override string GetHelpText(PrivilegeLevel current, string more)
        {
            return ": Prints a random quote to the channel";
        }

        public override void Execute(IrcMessage message, string args)
        {
            if(Parent.Limiter.AttemptOperation(message.Level))
            {
                last = DateTime.UtcNow;
                if (args.Length > 0) throw new Exception("Failed to parse your command");
                int count = State.QuoteList.GetCount();
                if (count == 0) throw new Exception("No quotes are currently available");
                int idx = rng.Next(count);
                Quote quote = State.QuoteList[idx];
                string result = "Quote " + ControlCharacter.Bold() + "#" + quote.ID.ToString() + ControlCharacter.Bold() + ": " + quote.Text;
                message.ReplyChannel(result);
            }
        }
    }
    
    class QuoteCommand : CommandGroup
    {
        public static void AutoRegister()
        {
            new QuoteCommand();
        }

        QuoteCommand()
        {
            Privilege = PrivilegeLevel.OnChannel;
            new QuoteRandomCommand(this);
            new QuoteAddCommand(this);
            new QuoteDelCommand(this);
            new QuoteInfoCommand(this);
            new QuoteFindCommand(this);
            new QuoteIntervalCommand(this);
            DefaultSubCommand = "random";
        }
        
        public override string GetKeyword()
        {
            return "quote";
        }
    }
}