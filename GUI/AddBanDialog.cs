using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Windows.Forms;

namespace desBot
{
    public partial class AddBanDialog : Form
    {
        public AddBanDialog(string nick_or_host, string duration, string reason)
        {
            InitializeComponent();
            textBox1.Text = nick_or_host;
            comboBox1.Text = duration;
            textBox2.Text = reason;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (textBox1.Text.Length == 0 || textBox2.Text.Length == 0 || comboBox1.Text.Length == 0)
            {
                MessageBox.Show("Please fill in the entire form", "Add ban error");
            }
            else DialogResult = System.Windows.Forms.DialogResult.OK;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            DialogResult = System.Windows.Forms.DialogResult.Cancel;
        }

        public string NickOrHost { get { return textBox1.Text; } }

        public string Duration { get { return comboBox1.Text; } }

        public string Reason { get { return textBox2.Text; } }
    }
}
