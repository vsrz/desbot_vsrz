using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Threading;
namespace desBot
{
    static class Program
    {
        /// <summary>
        /// Connection to the bot
        /// </summary>
        public static Connection Bot;
        public static MainForm Form;
        public static bool Synchronized = false;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            //set up environment
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            //ask for token
            Token token;
            using (ConnectDialog dialog = new ConnectDialog(Token.LocalHostToken()))
            {
                DialogResult result = dialog.ShowDialog();
                if (result != DialogResult.OK)
                {
                    return;
                }
                token = dialog.ConnectToken;
            }

            //reset handling
            State.OnReset += new OnStateReset(OnReset);

            //init watcher
            Watcher.Init();

            //create form
            Form = new MainForm();

            //set up connection
            Bot = new Connection(new Listener.LogDelegate(Program.Log), token);
            Bot.OnReceive += new Connection.OnReceiveEventHandler(Fetcher.OnBotObject);

            //run application
            Form.Run();

            //kill bot
            try
            {
                Thread.Sleep(100);
                Bot.Stop();
                Thread.Sleep(100);
            }
            catch (Exception ex)
            {
                Log("Exception during connection shutdown: " + ex.Message);
            }

            //display messagebox on error
            if (Bot.State == ConnectionState.Error)
            {
                MessageBox.Show("The connection to desBot failed:\n" + Bot.GetError(), "Connection failed");
            }
        }
       
        public static string ReportConnectionState()
        {
            if (Bot != null)
            {
                switch (Bot.State)
                {
                    case ConnectionState.Connecting:
                        return "Connecting to remote host";
                    case ConnectionState.Authing:
                        return "Authenticating";
                    case ConnectionState.Ready:
                        return Synchronized ? null : "Synchronizing state";
                }
            }
            else return "Initialising connection";
            throw new Exception("Connection failed");
        }

        /// <summary>
        /// Append to log
        /// </summary>
        /// <param name="text">The text to apply</param>
        public static void Log(string text)
        {
            System.Diagnostics.Debugger.Log(0, "", text + "\n");
            if (Form != null)
            {
                Form.AppendLog(text);
            }
        }

        /// <summary>
        /// Reset form
        /// </summary>
        static void OnReset()
        {
            if (Program.Synchronized)
            {
                //reset form
                MessageBox.Show("The connection was reset by the Bot (this may happen if someone restored a backup)");
                Environment.Exit(0);
            }
        }
    }
}
