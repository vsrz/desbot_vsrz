using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Net;
namespace desBot
{
    public partial class AddAccountDialog : Form
    {
        IPAddress addr;

        public AddAccountDialog()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {    
            if (textBox1.Text.Length == 0) MessageBox.Show("You need to type an IP in the textbox");
            else if (!IPAddress.TryParse(textBox1.Text, out addr) || IPAddress.IsLoopback(addr) || addr.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) MessageBox.Show("Failed to parse the input as a valid IPv4 address");
            else DialogResult = System.Windows.Forms.DialogResult.OK;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            DialogResult = System.Windows.Forms.DialogResult.Cancel;
        }

        public string Address { get { return addr.ToString(); } }
    }
}
