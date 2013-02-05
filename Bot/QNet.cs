using System;
using System.Collections.Generic;
namespace desBot
{
#if QNETBOT
    /// <summary>
    /// A command to be executed by Q
    /// </summary>
    abstract class QCommand
    {
        /// <summary>
        /// The command to execute
        /// </summary>
        public string Command;

        /// <summary>
        /// The success flag
        /// </summary>
        protected bool Succeeded = false;

        /// <summary>
        /// Event handler for finished event
        /// </summary>
        /// <param name="succeeded">The success flag</param>
        public delegate void OnFinishedDelegate(bool succeeded);
        
        /// <summary>
        /// The finished event
        /// </summary>
        public event OnFinishedDelegate OnFinished;
      
        /// <summary>
        /// The response handler
        /// </summary>
        /// <param name="response">The response of Q</param>
        /// <returns>true if more responses are required</returns>
        public abstract bool OnResponse(string response);
        
        /// <summary>
        /// Checks if this command is the same as some other command
        /// </summary>
        /// <param name="other">The other command to check</param>
        /// <returns>True if this command is the same</returns>
        public virtual bool IsSame(QCommand other)
        {
            return Command == other.Command;
        }

        /// <summary>
        /// Called when the command begins execution
        /// </summary>
        public virtual void OnExecute()
        {
            //empty on purpose
        }

        /// <summary>
        /// Creates a Q command for the given IRC tool
        /// </summary>
        /// <param name="parent">The IRC tool</param>
        public QCommand(string command, bool delay)
        {
            Command = command;
            if (!delay) DelayedPush();
        }

        /// <summary>
        /// Push delayed command
        /// </summary>
        protected void DelayedPush()
        {
            if (Irc.Queue != null) Irc.Queue.Push(this);
            else Program.Log("Ignoring Q command, no command queue available");
        }

        /// <summary>
        /// Raise finished event
        /// </summary>
        public void RaiseFinished()
        {
            if (OnFinished != null) OnFinished.Invoke(Succeeded);
        }
    }

    /// <summary>
    /// TEMPBAN command
    /// </summary>
    class TempBanCommand : QCommand
    {
        string banmask;

        public TempBanCommand(string banmask, string duration, string reason)
            : base("tempban " + Irc.Channel + " " + banmask + " " + duration + " " + reason, true)
        {
            this.banmask = banmask;
            DelayedPush();
        }

        public override void OnExecute()
        {
            BanSystem.OnQBanStarted(banmask);
        }

        public override bool OnResponse(string response)
        {
            if (response.StartsWith("Replaced existing"))
            {
                return true;
            }
            if (response != "Done.")
            {
                Program.Log("Failed to tempban using '" + Command + "': " + response);
            }
            else
            {
                Succeeded = true;
            }
            BanSystem.OnQBanFinished(Succeeded);
            return false;
        }
    }

    /// <summary>
    /// PERMBAN command
    /// </summary>
    class PermBanCommand : QCommand
    {
        string banmask;

        public PermBanCommand(string banmask, string reason)
            : base("permban " + Irc.Channel + " " + banmask + " " + reason, true)
        {
            this.banmask = banmask;
            DelayedPush();
        }

        public override void OnExecute()
        {
            BanSystem.OnQBanStarted(banmask);
        }

        public override bool OnResponse(string response)
        {
            if (response.StartsWith("A temporary ban"))
            {
                return true;
            }
            if (response != "Done.")
            {
                Program.Log("Failed to permban using '" + Command + "': " + response);
            }
            else
            {
                Succeeded = true;
            }
            BanSystem.OnQBanFinished(Succeeded);
            return false;
        }
    }

    /// <summary>
    /// BANDEL command
    /// </summary>
    class BanDelCommand : QCommand
    {
        string banmask;

        public BanDelCommand(string banmask)
            : base("bandel " + Irc.Channel + " " + banmask, true)
        {
            this.banmask = banmask;
            DelayedPush();
        }

        public override void OnExecute()
        {
            BanSystem.OnQUnbanStarted(banmask);
        }

        public override bool OnResponse(string response)
        {
            if (!response.StartsWith("Removed registered ban") && !response.StartsWith("Removed channel ban"))
            {
                Program.Log("Failed to bandel using '" + Command + "': " + response);
            }
            else
            {
                Succeeded = true;
            }
            BanSystem.OnQUnbanFinished(Succeeded);
            return false;
        }
    }

    /// <summary>
    /// BANLIST command
    /// </summary>
    class BanListCommand : QCommand
    {
        //list of bans
        List<Ban> results = new List<Ban>();

        public BanListCommand(int dummy) : base("banlist " + Irc.Channel, false)
        { 
            //empty on purpose
        }
        public override bool OnResponse(string response)
        {
            if (response.StartsWith("Registered bans on")) return true;
            if (response.StartsWith("ID  Hostmask")) return true;
            if (response.StartsWith("No bans on"))
            {
                //inform ban system
                BanSystem.OnQBanListReceived(results);

                //done
                Succeeded = true;
                return false;
            }
            if (response.StartsWith("End of list"))
            {
                //inform ban system
                BanSystem.OnQBanListReceived(results);

                //done
                Succeeded = true;
                return false;
            }
            try
            {
                //parse a ban list entry
                string[] args = response.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                Ban result = new Ban();
                int next = 3;
                result.Mask = new HostMask(args[1]);
                if (args[2] == "Permanent") result.Expires = DateTime.MaxValue;
                else
                {
                    //parse time
                    result.Expires = DateTime.UtcNow;
                    result.Expires = result.Expires.AddDays(int.Parse(args[2]));
                    string[] temp = args[4].Split(new char[] { ':' });
                    result.Expires = result.Expires.AddHours(int.Parse(temp[0]));
                    result.Expires = result.Expires.AddMinutes(int.Parse(temp[1]));
                    result.Expires = result.Expires.AddSeconds(int.Parse(temp[2]));
                    next = 5;
                }

                //parse set by
                result.Enforcer = BanEnforcement.ByQ;
                result.SetBy = args[next];
                if(result.SetBy == "(channel")
                {
                    result.SetBy = "<manual>";
                    result.Enforcer = BanEnforcement.ByChannel;
                    next += 2;
                }
                else
                {
                    next += 1;
                }

                //parse reason
                result.Reason = "";
                for (int i = next; i < args.Length; i++) result.Reason += args[i] + " ";
                result.Reason.Trim();

                //look up source of ban
                string[] split = result.Reason.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (split.Length >= 2 && split[split.Length - 2] == "by")
                {
                    result.SetBy = split[split.Length - 1];
                }

                //get affected names
                result.Affected = BanSystem.GetAffectedNicks(result.Mask);

                //add ban to list
                results.Add(result);

                //read more entries
                return true;
            }
            catch (Exception ex)
            {
                //error during parsing
                Program.Log("Unexpected response while updating ban list with '" + response + "': " + ex.Message);
                return false;
            }
        }
    }

    /// <summary>
    /// A utility that queues and executes Q commands
    /// </summary>
    class QCommandQueue
    {
        QCommand current;
        Queue<QCommand> commands;

        public delegate void UpdateEventHandler();
        public event UpdateEventHandler OnUpdate;

        /// <summary>
        /// Creates a new Q command list
        /// </summary>
        /// <param name="parent">The IRC tool that runs the Q command list</param>
        public QCommandQueue()
        {
            commands = new Queue<QCommand>();
            Update();
        }

        /// <summary>
        /// Updates the Q command list
        /// </summary>
        void Update()
        {
            if (current == null && commands.Count != 0)
            {
                current = commands.Dequeue();
                current.OnExecute();
                Irc.SendPrivateMessage(current.Command, "Q", false, false);
            }
            if (OnUpdate != null) OnUpdate.Invoke();
        }

        /// <summary>
        /// Handles a response from Q
        /// </summary>
        /// <param name="text">The text of the response by Q</param>
        public void OnResponse(string text)
        {
            if (current == null) Program.Log("Unexpected response from Q: " + text);
            else
            {
                if (!current.OnResponse(text))
                {
                    current.RaiseFinished();
                    current = null;
                    Update();
                }
            }
        }

        /// <summary>
        /// Adds a new command to the Q command queue
        /// </summary>
        /// <param name="command">The command to add</param>
        public void Push(QCommand command)
        {
            foreach (QCommand other in commands) if (command.IsSame(other)) return;
            commands.Enqueue(command);
            Update();
        }

        /// <summary>
        /// The current state of the Q queue
        /// </summary>
        public string State
        {
            get
            {
                if (current == null) return "Idle";
                else return "Executing " + current.GetType().Name;
            }
        }
    }
#endif
}
