using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace desBot
{
    delegate string ReportFunction();

    partial class BusyDialog : Form
    {
        ReportFunction func;

        public BusyDialog(string operation, ReportFunction function)
        {
            func = function;

            InitializeComponent();

            label1.Text = operation;
            UpdateF();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            UpdateF();
        }

        void UpdateF()
        {
            try
            {
                string val = func();
                if (Program.Bot.State >= ConnectionState.Disconnected) val = null;
                if (val == null) DialogResult = System.Windows.Forms.DialogResult.OK;
                else label2.Text = val;
            }
            catch (Exception ex)
            {
                Program.Log("Exception during ReportFunction: " + ex.Message);
                DialogResult = System.Windows.Forms.DialogResult.Cancel;
            }
        }
    }
}
