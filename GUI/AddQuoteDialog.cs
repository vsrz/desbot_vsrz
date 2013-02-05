using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace desBot
{
    public partial class AddQuoteDialog : Form
    {
        public AddQuoteDialog()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (textBox1.Text.Length == 0) MessageBox.Show("You need to type a quote in the text box to add");
            else DialogResult = System.Windows.Forms.DialogResult.OK;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            DialogResult = System.Windows.Forms.DialogResult.Cancel;
        }

        public string Quote { get { return textBox1.Text; } }
    }
}
