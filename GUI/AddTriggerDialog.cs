using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace desBot
{
    public partial class AddTriggerDialog : Form
    {
        public AddTriggerDialog()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (textBox1.Text.Length == 0) MessageBox.Show("You need to type the text to display when triggered");
            else if (textBox2.Text.Length == 0) MessageBox.Show("You need to type the trigger keyword for the trigger");
            else if(!char.IsLetter(textBox2.Text[0])) MessageBox.Show("The trigger keyword should start with a letter");
            else DialogResult = System.Windows.Forms.DialogResult.OK;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            DialogResult = System.Windows.Forms.DialogResult.Cancel;
        }

        public string TriggerKeyword { get { return textBox2.Text; } }
        public string TriggerText { get { return textBox1.Text; } }
    }
}
