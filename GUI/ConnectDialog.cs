using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace desBot
{
    public partial class ConnectDialog : Form
    {
        public Token ConnectToken { get; private set; }

        public ConnectDialog(Token token)
        {
            InitializeComponent();
            textBox1.Text = token.ToXML();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            DialogResult = System.Windows.Forms.DialogResult.Cancel;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                ConnectToken = Token.FromXML(textBox1.Text);
                DialogResult = System.Windows.Forms.DialogResult.OK;
            }
            catch (Exception ex)
            {
                MessageBox.Show("The entered connection token is not valid, please check the token\n\nAdditional information: " + ex.Message, "Bad token");
            }
        }
    }
}
