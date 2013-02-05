using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Drawing;
using System.IO;

namespace desBot
{
    public partial class MainForm : Form
    {
        int lastid = 0;

        //constructor
        public MainForm()
        {
            //set up UI
            InitializeComponent();
            Command.KeyPress += new KeyPressEventHandler(Command_KeyPress);

            //write version number
            Log.Text = "--- desBot Control Panel v" + Token.GetCurrentVersion() + " ---\r\n"; 
        }

        //run application
        public void Run()
        {
            //show busy dialog
            Busy("Synchronizing with desBot instance...", new ReportFunction(Program.ReportConnectionState));
            SetJtvSettings(State.JtvSettings.Value);
            SetIrcSettings(State.IrcSettings.Value);

            //run UI on this thread
            try
            {
                Application.Run(this);
            }
            catch (Exception ex)
            {
                Program.Log("Exception during GUI event handling: " + ex.Message);
            }
            MainForm_FormClosed(null, null);
        }

        //show busy dialog
        void Busy(string operation, ReportFunction reporter)
        {
            DialogResult result = new BusyDialog(operation, reporter).ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK) return;
            ExecShutdown();
        }

        //execute command
        void ExecCommand(string command)
        {
            RemoteCommand cmd = new RemoteCommand();
            cmd.Command = command;
            cmd.ID = ++lastid;
            Program.Bot.Send(cmd);
            Busy("Executing remote command", new ReportFunction(new LastCommandReporter(cmd).Poll));
        }

        //shutdown GUI
        void ExecShutdown()
        {
            Program.Bot.Stop();
            Close();
        }

        delegate void CollectionChangedDelegate<T>(T item);
        delegate void PropertyChangedDelegate<T>(string name, T item);
        delegate void SettingsChangedDelegate(AllSettings settings);

        public void AddItem<T>(T item)
        {
            if (InvokeRequired)
            {
                Invoke(new CollectionChangedDelegate<T>(AddItem), new object[] { item });
            }
            else
            {
                if (item is User) AddUser(item as User);
                else if (item is Ban) AddBan(item as Ban);
                else if (item is Quote) AddQuote(item as Quote);
                else if (item is Trigger) AddTrigger(item as Trigger);
                else if (item is Account) AddAccount(item as Account);
                else if (item is MetaUser) /*ignored*/;
                else Program.Log("Ignored AddItem<T> with T = " + item.GetType().Name);
            }
        }

        public void RemoveItem<T>(T item)
        {
            if (InvokeRequired)
            {
                Invoke(new CollectionChangedDelegate<T>(RemoveItem), new object[] { item });
            }
            else
            {
                if (item is User) RemoveUser(item as User);
                else if (item is Ban) RemoveBan(item as Ban);
                else if (item is Quote) RemoveQuote(item as Quote);
                else if (item is Trigger) RemoveTrigger(item as Trigger);
                else if (item is Account) RemoveAccount(item as Account);
                else if (item is MetaUser) /*ignored*/;
                else Program.Log("Ignored RemoveItem<T> with T = " + item.GetType().Name);
            }
        }

        public void ApplySettings(AllSettings settings)
        {
            if (InvokeRequired)
            {
                Invoke(new SettingsChangedDelegate(ApplySettings), new object[] { settings });
            }
            else
            {
                SetIrcSettings(settings.QNet);
                SetJtvSettings(settings.JTV);
                settingsrecv = true;
            }
        }

        public void SetProperty<T>(string name, T value)
        {
            if (InvokeRequired)
            {
                Invoke(new PropertyChangedDelegate<T>(SetProperty<T>), new object[] { name, value });
            }
            else
            {
                if (name == State.SendDelay.Name)
                {
                    FloodDelay.Text = State.SendDelay.Value.ToString();
                    FloodDelay.BackColor = Color.White;
                }
                else if (name == State.PublicIP.Name)
                {
                    PublicIP.Text = State.PublicIP.Value;
                }
                else if (name == State.QuoteInterval.Name)
                {
                    QuoteInterval.Text = State.QuoteInterval.Value.ToString();
                    QuoteInterval.BackColor = Color.White;
                }
                else if (name == State.ParseChannel.Name)
                {
                    suppress = true;
                    ParseChannel.Checked = State.ParseChannel.Value;
                    suppress = false;
                }
                else if (name == State.ControlCharacters.Name)
                {
                    suppress = true;
                    ControlChars.Checked = State.ControlCharacters.Value;
                    suppress = false;
                }
                else if (name == State.UseQEnforce.Name)
                {
                    suppress = true;
                    QEnforce.Checked = State.UseQEnforce.Value;
                    suppress = false;
                }
                else if (name == State.UseQuietBan.Name)
                {
                    suppress = true;
                    QuietBan.Checked = State.UseQuietBan.Value;
                    suppress = false;
                }
                else if (name == State.WarningThreshold.Name)
                {
                    WarningThreshold.Text = State.WarningThreshold.Value.ToString();
                    WarningThreshold.BackColor = Color.White;
                }
                else Program.Log("Ignored property " + name);
            }
        }

        void RemoveFromCollection(ListView.ListViewItemCollection collection, string text)
        {
            foreach (ListViewItem item in collection)
            {
                if (item.Text == text)
                {
                    collection.Remove(item);
                    return;
                }
            }
            throw new Exception("No control with text '" + text + "' in the collection");
        }

        void AddUser(User user)
        {
            Users.SuspendLayout();
            ListViewItem item = Users.Items.Add(user.Nick);
            item.SubItems.Add(user.HostMask.Mask.Substring(user.HostMask.Mask.IndexOf('!') + 1));
            item.SubItems.Add(user.Joined.ToLocalTime().ToString());
            item.SubItems.Add(user.Left == DateTime.MaxValue ? "has not left yet" : (user.Left == DateTime.MinValue ? "unknown time" : user.Left.ToLocalTime().ToString()));
            Users.ResumeLayout();
            UserTab.Text = "Users [" + Users.Items.Count.ToString() + "]";
        }

        void RemoveUser(User user)
        {
            RemoveFromCollection(Users.Items, user.Nick);
            UserTab.Text = "Users [" + Users.Items.Count.ToString() + "]";
        }

        void AddBan(Ban ban)
        {
            BanList.SuspendLayout();
            ListViewItem item = BanList.Items.Add(ban.Mask.Mask);
            item.SubItems.Add(ban.Expires == DateTime.MaxValue ? "never" : ban.Expires.ToString());
            item.SubItems.Add(ban.SetBy);
            item.SubItems.Add(ban.Affected);
            item.SubItems.Add(ban.Reason);
            item.ToolTipText = "Ban enforcement: " + ban.Enforcer.ToString();
            BanList.ResumeLayout();
            BanTab.Text = "Bans [" + BanList.Items.Count.ToString() + "]";
        }

        void RemoveBan(Ban ban)
        {
            RemoveFromCollection(BanList.Items, ban.Mask.Mask);
            BanTab.Text = "Bans [" + BanList.Items.Count.ToString() + "]";
        }

        void AddQuote(Quote quote)
        {
            Quotes.SuspendLayout();
            ListViewItem item = Quotes.Items.Add(quote.ID.ToString());
            item.SubItems.Add(quote.SetBy);
            item.SubItems.Add(quote.Created.ToLocalTime().ToString());
            item.SubItems.Add(ControlCharacter.Serialize(quote.Text));
            Quotes.ResumeLayout();
            QuotesTab.Text = "Quotes [" + Quotes.Items.Count.ToString() + "]";
        }

        void RemoveQuote(Quote quote)
        {
            RemoveFromCollection(Quotes.Items, quote.ID.ToString());
            QuotesTab.Text = "Quotes [" + Quotes.Items.Count.ToString() + "]";
        }

        void AddTrigger(Trigger trigger)
        {
            Triggers.SuspendLayout();
            ListViewItem item = Triggers.Items.Add(trigger.Keyword);
            item.SubItems.Add(ControlCharacter.Serialize(trigger.Text));
            Triggers.ResumeLayout();
            TriggerTab.Text = "Triggers [" + Triggers.Items.Count.ToString() + "]";
        }

        void RemoveTrigger(Trigger trigger)
        {
            RemoveFromCollection(Triggers.Items, trigger.Keyword);
            TriggerTab.Text = "Triggers [" + Triggers.Items.Count.ToString() + "]";
        }

        void AddAccount(Account account)
        {
            Accounts.Items.Add(account.IP.ToString());
            AccountsTab.Text = "Accounts [" + Accounts.Items.Count.ToString() + "]";
        }

        void RemoveAccount(Account account)
        {
            RemoveFromCollection(Accounts.Items, account.IP.ToString());
            AccountsTab.Text = "Accounts [" + Accounts.Items.Count.ToString() + "]";
        }

        void SetIrcSettings(IrcSettings settings)
        {
            IRCHostName.Text = settings.Server;
            IRCHostPort.Text = settings.Port.ToString();
            IRCUserName.Text = settings.Username;
            IRCNickName.Text = settings.Nickname;
            IRCChannel.Text = settings.Channel;
            QUser.Text = settings.QAccount;
            QPass.Text = settings.QPassword;
            QHideIP.Checked = settings.QHideIP;
        }

        void SetJtvSettings(JtvSettings settings)
        {
            JTVChannel.Text = settings.Channel;
            JTVUser.Text = settings.Nickname;
            JTVPass.Text = settings.Password;
        }

        delegate void AppendLogSignature(string text);
        public void AppendLog(string text)
        {
            if (Log.InvokeRequired)
            {
                Log.Invoke(new AppendLogSignature(AppendLog), new object[] { text });
            }
            else
            {
                Log.Text += DateTime.UtcNow.ToString("HH:mm:ss - ") + text + "\r\n";
                Log.SelectionStart = Log.Text.Length - 2;
                Log.ScrollToCaret();
            }
        }

        delegate void SetStatsSignature(Stats stats);
        public void SetStats(Stats stats)
        {
            if (InvokeRequired)
            {
                Invoke(new SetStatsSignature(SetStats), new object[] { stats });
            }
            else
            {
                //IRC stats
                Color red = Color.FromArgb(0xFF, 0x99, 0x99);
                Color yellow = Color.FromArgb(0xFF, 0xFF, 0x99);
                Color green = Color.FromArgb(0x99, 0xFF, 0x99);

                StateConnection.Text = stats.Irc.ToString();
                if (stats.Irc == IrcState.Ready) StateConnection.BackColor = green;
                else if (StateConnection.Text.StartsWith("Error")) StateConnection.BackColor = red;
                else StateConnection.BackColor = yellow;

                StateNick.Text = stats.Nick;
                if (stats.Irc <= IrcState.Registering) StateNick.BackColor = yellow;
                else if (StateConnection.Text.StartsWith("Error")) StateNick.BackColor = red;
                else StateNick.BackColor = green;

                StateChannel.Text = stats.Channel;
                if (stats.Irc < IrcState.Ready) StateChannel.BackColor = yellow;
                else if (StateConnection.Text.StartsWith("Error")) StateChannel.BackColor = red;
                else StateChannel.BackColor = green;

                StateQ.Text = stats.Qlevel.ToString();
                if (stats.Qlevel == QAuthLevel.None) StateQ.BackColor = red;
                else if (stats.Qlevel >= QAuthLevel.Master) StateQ.BackColor = green;
                else StateQ.BackColor = yellow;

                if (stats.Qquery.Length > 0)
                {
                    StateQQuery.Text = stats.Qquery;
                    StateQQuery.BackColor = StateQQuery.Text == "Idle" ? green : yellow;
                }
                else
                {
                    StateQQuery.Text = "";
                    StateQQuery.BackColor = Color.White;
                }

                //performance stats
                CPUUsage.Text = (stats.CPUUsage * 100.0).ToString("F2") + "%";
                RAMUsage.Text = (stats.PhysicalMemory / 1024 / 1024).ToString() + " MB";
                VirtualMemory.Text = (stats.VirtualMemory / 1024 / 1024).ToString() + " MB";
                ThreadCount.Text = stats.Threads.ToString() + " threads";
            }
        }

        void Command_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)13)
            {
                string command = Command.Text;
                Command.Text = string.Empty;
                ExecCommand(command);
            }
        }

        bool settingsrecv;
        string SettingsReporter()
        {
            return settingsrecv ? null : "Waiting for confirmation";
        }
        void IRCServerApply_Click(object sender, EventArgs e)
        {
            string server = IRCHostName.Text;
            if (server.Length != 0)
            {
                int port;
                if (int.TryParse(IRCHostPort.Text, out port))
                {
                    string user = IRCUserName.Text;
                    if (user.Length != 0 && !user.Contains(" "))
                    {
                        string nick = IRCNickName.Text;
                        if (nick.Length != 0 && !nick.Contains(" "))
                        {
                            string channel = IRCChannel.Text;
                            if (channel.Length > 1 && channel.StartsWith("#") && !channel.Contains(" "))
                            {      
                                string quser = QUser.Text;
                                if (quser.Length != 0 && !quser.Contains(" "))
                                {
                                    string qpass = QPass.Text;
                                    if (qpass.Length != 0 && !qpass.Contains(" "))
                                    {
                                        string jtvchannel = JTVChannel.Text;
                                        if (jtvchannel.Length != 0 && jtvchannel.StartsWith("#") && !jtvchannel.Contains(" "))
                                        {
                                            string jtvuser = JTVUser.Text;
                                            if (jtvuser.Length != 0 && !jtvuser.Contains(" "))
                                            {
                                                string jtvpass = JTVPass.Text;
                                                if (jtvpass.Length != 0 && !jtvpass.Contains(" "))
                                                {
                                                    if (MessageBox.Show("You are about to change IRC connection parameters.\nThe bot will have to disconnect from IRC to apply changes\n\nAre you sure you wish to continue?", "Disconnection warning!", MessageBoxButtons.YesNo) == System.Windows.Forms.DialogResult.Yes)
                                                    {
                                                        IrcSettings settings = new IrcSettings();
                                                        settings.Server = server;
                                                        settings.Port = port;
                                                        settings.Username = user;
                                                        settings.Nickname = nick;
                                                        settings.Channel = channel;
                                                        settings.QAccount = quser;
                                                        settings.QPassword = qpass;
                                                        settings.QHideIP = QHideIP.Checked;

                                                        JtvSettings jtv = new JtvSettings();
                                                        jtv.Channel = jtvchannel;
                                                        jtv.Nickname = jtvuser;
                                                        jtv.Password = jtvpass;

                                                        AllSettings all = new AllSettings();
                                                        all.JTV = jtv;
                                                        all.QNet = settings;

                                                        settingsrecv = false;
                                                        Program.Bot.Send(all);
                                                        Busy("Changing IRC connection settings", new ReportFunction(SettingsReporter));
                                                    }
                                                }
                                                else MessageBox.Show("JTV password not specified, or contains spaces", "IRC server settings");
                                            }
                                            else MessageBox.Show("JTV username not specified, or contains spaces", "IRC server settings");
                                        }
                                        else MessageBox.Show("JTV channel not specified, doesn't start with #, or contains spaces", "IRC server settings");
                                    }
                                    else MessageBox.Show("Q password not specified, or contains spaces", "IRC server settings");
                                }
                                else MessageBox.Show("Q user name not specified, or contains spaces", "IRC server settings");
                            }
                            else MessageBox.Show("IRC channel not specified, doesn't start with #, or contains spaces", "IRC server settings");
                        }
                        else MessageBox.Show("IRC nick name not specified, or contains spaces", "IRC server settings");
                    }
                    else MessageBox.Show("IRC user name not specified, or contains spaces", "IRC server settings");
                }
                else MessageBox.Show("IRC port must be a number (usually 6667 or 6668)", "IRC server settings");
            }
            else MessageBox.Show("IRC server should be host name of IRC server", "IRC server settings");
        }

        private void BanList_SelectedIndexChanged(object sender, EventArgs e)
        {
            UnbanButton.Enabled = BanList.SelectedIndices.Count != 0;
        }

        private void UnbanButton_Click(object sender, EventArgs e)
        {
            string hostmask = BanList.SelectedItems[0].Text;
            ExecCommand("unban " + hostmask);
        }

        private void Users_SelectedIndexChanged(object sender, EventArgs e)
        {
            bool enabled = Users.SelectedIndices.Count != 0;
            TempBanButton.Enabled = enabled;
            PermBanButton.Enabled = enabled;
        }
        
        private void DisplayBanDialog(string nick, string duration, string reason)
        {
            AddBanDialog dialog = new AddBanDialog(nick, duration, reason);
            DialogResult result = dialog.ShowDialog(this);
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                ExecCommand("ban " + dialog.NickOrHost + " " + dialog.Duration + " " + dialog.Reason);
            }
        }

        private void TempBanButton_Click(object sender, EventArgs e)
        {
            string nick = Users.SelectedItems[0].Text;
            DisplayBanDialog(nick, "10m", "GUI: time out");
        }

        private void PermBanButton_Click(object sender, EventArgs e)
        {
            string nick = Users.SelectedItems[0].Text;
            DisplayBanDialog(nick, "permanent", "GUI: permanent ban");
        }

        private void Quotes_SelectedIndexChanged(object sender, EventArgs e)
        {
            QuoteRemoveButton.Enabled = Quotes.SelectedIndices.Count != 0;
        }

        private void QuoteRemoveButton_Click(object sender, EventArgs e)
        {
            string id = Quotes.SelectedItems[0].Text;
            ExecCommand("quote del " + id);
        }

        private void QuoteAddButton_Click(object sender, EventArgs e)
        {
            AddQuoteDialog dialog = new AddQuoteDialog();
            DialogResult result = dialog.ShowDialog(this);
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                ExecCommand("quote add " + dialog.Quote);
            }
        }

        private void QuoteInterval_TextChanged(object sender, EventArgs e)
        {
            int interval = 0;
            bool valid = int.TryParse(QuoteInterval.Text.Trim(), out interval) && interval >= 0;
            QuoteInterval.BackColor = valid ? Color.FromArgb(0x99, 0xFF, 0x99) : Color.FromArgb(0xFF, 0x99, 0x99);
        }

        private void QuoteInterval_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 13)
            {
                if (QuoteInterval.BackColor == Color.FromArgb(0x99, 0xFF, 0x99))
                {
                    string command = "quote interval " + QuoteInterval.Text.Trim();
                    QuoteInterval.Text = State.QuoteInterval.Value.ToString();
                    ExecCommand(command);
                    QuoteInterval.BackColor = Color.White;
                }
                else
                {
                    MessageBox.Show("Invalid value, expected positive integral value, or 0 to disable");
                }
            }
        }

        private void FloodDelay_TextChanged(object sender, EventArgs e)
        {
            int val;
            bool valid = int.TryParse(FloodDelay.Text.Trim(), out val) && val >= 0;
            FloodDelay.BackColor = valid ? Color.FromArgb(0x99, 0xFF, 0x99) : Color.FromArgb(0xFF, 0x99, 0x99);
        }

        private void FloodDelay_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 13)
            {
                if (FloodDelay.BackColor == Color.FromArgb(0x99, 0xFF, 0x99))
                {
                    string command = "prop sd " + FloodDelay.Text.Trim();
                    FloodDelay.Text = State.SendDelay.Value.ToString();
                    ExecCommand(command);
                    FloodDelay.BackColor = Color.White;
                }
                else
                {
                    MessageBox.Show("Invalid value, expected positive integral value");
                }
            }
        }

        private void WarningThreshold_TextChanged(object sender, EventArgs e)
        {
            int val;
            bool valid = int.TryParse(WarningThreshold.Text.Trim(), out val) && val > 1;
            WarningThreshold.BackColor = valid ? Color.FromArgb(0x99, 0xFF, 0x99) : Color.FromArgb(0xFF, 0x99, 0x99);
        }

        private void WarningThreshold_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 13)
            {
                if (WarningThreshold.BackColor == Color.FromArgb(0x99, 0xFF, 0x99))
                {
                    string command = "prop wt " + WarningThreshold.Text.Trim();
                    WarningThreshold.Text = State.WarningThreshold.Value.ToString();
                    ExecCommand(command);
                    WarningThreshold.BackColor = Color.White;
                }
                else
                {
                    MessageBox.Show("Invalid value, expected positive integral value, > 1");
                }
            }
        }

        bool suppress = false;
        private void ControlChars_CheckedChanged(object sender, EventArgs e)
        {
            if (!suppress)
            {
                string command = "prop cc " + ControlChars.Checked.ToString();
                suppress = true;
                ControlChars.Checked = State.ControlCharacters.Value;
                suppress = false;
                ExecCommand(command);
            }
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (!suppress)
            {
                string command = "prop pc " + ParseChannel.Checked.ToString();
                suppress = true;
                ParseChannel.Checked = State.ParseChannel.Value;
                suppress = false;
                ExecCommand(command);
            }
        }

        private void QEnforce_CheckedChanged(object sender, EventArgs e)
        {
            if (!suppress)
            {

                string command = "prop qe " + QEnforce.Checked.ToString();
                suppress = true;
                QEnforce.Checked = State.UseQEnforce.Value;
                suppress = false;
                ExecCommand(command);
            }
        }


        private void QuietBan_CheckedChanged(object sender, EventArgs e)
        {
            if (!suppress)
            {
                string command = "prop qb " + QuietBan.Checked.ToString();
                suppress = true;
                QuietBan.Checked = State.UseQEnforce.Value;
                suppress = false;
                ExecCommand(command);
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (Program.Bot.State >= ConnectionState.Disconnected) Close();
        }

        private void Accounts_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (Accounts.SelectedIndices.Count == 0)
            {
                AccountToken.Text = "<select an account on the left>";
                DeleteAccountButton.Enabled = false;
            }
            else
            {
                string needle = Accounts.SelectedItems[0].Text;
                foreach (Account acct in State.AccountList.GetItems())
                {
                    if (acct.IP.ToString() == needle)
                    {
                        AccountToken.Text = acct.GetToken().ToXML();
                        return;
                    }
                }
                AccountToken.Text = "<failed to find a connect token>";
                DeleteAccountButton.Enabled = true;
            }
        }

        private void BackupButton_Click(object sender, EventArgs e)
        {
            try
            {
                SaveFileDialog dialog = new SaveFileDialog();
                dialog.Title = "Back up settings to where?";
                dialog.Filter = "XML file|*.xml";
                DialogResult result = dialog.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    string file = dialog.FileName;
                    using(Stream stream = new FileStream(file, FileMode.Create, FileAccess.Write))
                    {
                        string stats = desBot.Settings.Save(stream, new XmlSerializer());
                        MessageBox.Show("Backed up settings to " + file + "\n" + stats, "Settings backed up!");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to back up settings:\n" + ex.Message, "Backup failed");
            }
        }

        bool closed = false;
        string RestoreReporter()
        {
            if (closed)
            {
                return null;
            }
            return "Uploading backup...";
        }

        private void RestoreButton_Click(object sender, EventArgs e)
        {
            try
            {
                OpenFileDialog dialog = new OpenFileDialog();
                dialog.Title = "Restore settings from where?";
                dialog.Filter = "XML file|*.xml";
                DialogResult result = dialog.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    string file = dialog.FileName;
                    using (Stream stream = new FileStream(file, FileMode.Open, FileAccess.Read))
                    {
                        SerializableSettings settings = new XmlSerializer().Deserialize(stream);
                        if (MessageBox.Show("You are about to restore previous backup\nWARNING: All changes made since the backup will be irrecoverably destroyed\nConsider making a backup BEFORE restoring settings!\n\nAre you sure you wish to proceed?", "Are you VERY sure?", MessageBoxButtons.YesNo) == System.Windows.Forms.DialogResult.Yes)
                        {
                            Program.Bot.Send(settings);
                            Busy("Restoring settings", new ReportFunction(RestoreReporter));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to restore settings:\n" + ex.Message, "Restore failed");
            }
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            //clear reference
            if (Program.Form == this)
            {
                Program.Form = null;
            }
            closed = true;
        }

        int Users_PrevSortColumn = -1;
        private void Users_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            IComparer sorter = (e.Column >= 2 ? (IComparer)new DateTimeSorter(e.Column, Users_PrevSortColumn == e.Column) : new AlphabeticalSorter(e.Column, Users_PrevSortColumn == e.Column));
            Users.ListViewItemSorter = sorter;
            Users_PrevSortColumn = (Users_PrevSortColumn == e.Column ? -1 : e.Column);
            Users.Sort();
        }

        int BanList_PrevSortColumn = -1;
        private void BanList_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            IComparer sorter = (e.Column == 1 ? (IComparer)new DateTimeSorter(e.Column, BanList_PrevSortColumn == e.Column) : new AlphabeticalSorter(e.Column, BanList_PrevSortColumn == e.Column));
            BanList.ListViewItemSorter = sorter;
            BanList_PrevSortColumn = (BanList_PrevSortColumn == e.Column ? -1 : e.Column);
            BanList.Sort();
        }

        int Quotes_PrevSortColumn = -1;
        private void Quotes_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            IComparer sorter = null;
            switch(e.Column)
            {
                case 2:
                    sorter = new DateTimeSorter(e.Column, Quotes_PrevSortColumn == e.Column);
                    break;
                case 0:
                    sorter = new NumericalSorter(e.Column, Quotes_PrevSortColumn == e.Column);
                    break;
                default:
                    sorter = new AlphabeticalSorter(e.Column, Quotes_PrevSortColumn == e.Column);
                    break;
            }
            Quotes.ListViewItemSorter = sorter;
            Quotes_PrevSortColumn = (Quotes_PrevSortColumn == e.Column ? -1 : e.Column);
            Quotes.Sort();
        }

        int Triggers_PrevSortColumn = -1;
        private void Triggers_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            IComparer sorter = new AlphabeticalSorter(e.Column, Triggers_PrevSortColumn == e.Column);
            Triggers.ListViewItemSorter = sorter;
            Triggers_PrevSortColumn = (Triggers_PrevSortColumn == e.Column ? -1 : e.Column);
            Triggers.Sort();
        }

        int Accounts_PrevSortColumn = -1;
        private void Accounts_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            IComparer sorter = new AlphabeticalSorter(e.Column, Accounts_PrevSortColumn == e.Column);
            Accounts.ListViewItemSorter = sorter;
            Accounts_PrevSortColumn = (Accounts_PrevSortColumn == e.Column ? -1 : e.Column);
            Accounts.Sort();
        }

        private void Restart_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("You are about to restart the bot. This will cause the bot to lose IRC connectivity.\n\nAre you sure you wish to continue?", "Restart confirmation", MessageBoxButtons.YesNo) == System.Windows.Forms.DialogResult.Yes)
            {
                ExecCommand("restart");
            }
        }

        private void Terminate_Click(object sender, EventArgs e)
        {
            if(MessageBox.Show("You are about to terminate the bot.\nCAUTION: You will not be able to restart the bot using this application\n\nAre you sure you wish to continue?", "Terminate confirmation", MessageBoxButtons.YesNo) == System.Windows.Forms.DialogResult.Yes)
            {
                ExecCommand("terminate");
            }
        }

        private void Refresh_Click(object sender, EventArgs e)
        {
            ExecCommand("banlist refresh");
        }

        private void Triggers_SelectedIndexChanged(object sender, EventArgs e)
        {
            DeleteTrigger.Enabled = Triggers.SelectedIndices.Count != 0;
        }

        private void AddTriggerButton_Click(object sender, EventArgs e)
        {
            AddTriggerDialog dialog = new AddTriggerDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ExecCommand("trigger add " + dialog.TriggerKeyword + " " + dialog.TriggerText);
            }
        }

        private void DeleteTrigger_Click(object sender, EventArgs e)
        {
            ExecCommand("trigger del " + Triggers.SelectedItems[0].Text);
        }

        private void DeleteAccountButton_Click(object sender, EventArgs e)
        {
            ExecCommand("token del " + Accounts.SelectedItems[0].Text);
        }

        private void AddAccountButton_Click(object sender, EventArgs e)
        {
            AddAccountDialog dialog = new AddAccountDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ExecCommand("token add " + dialog.Address);
            }
        }
    }
}
