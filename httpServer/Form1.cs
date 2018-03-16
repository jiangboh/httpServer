using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

namespace httpServer
{
    public partial class Form1 : Form
    {
        //MySqlDbHelper myDB = new MySqlDbHelper();
        //public string FormTitle = "博威通小网管服务器";
        //private int ShowLen = 0;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            textBox1.Text = "";
            notifyIcon.Text = this.Text;
            this.Text = Program.FormTitle;
            //运行主程序
            GlobalParameter.StartThisApp();
        }

        /// <summary>
        /// 点击“退出”时的动作
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void label1_Click(object sender, EventArgs e)
        {
            if (DialogResult.OK == MessageBox.Show("确认关闭服务器程序?","关闭："
                , MessageBoxButtons.OKCancel,MessageBoxIcon.Question))
            {
                Log.WriteError("点击退出按钮，关闭程序！");
                GlobalParameter.CloseThisApp(false);
            }
        }

        /// <summary>
        /// 点击关闭窗口时，最小化到系统托盘
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (GlobalParameter.httpServerRun)
            {
                // 取消关闭窗体
                e.Cancel = true;

                // 将窗体变为最小化
                this.WindowState = FormWindowState.Minimized;

                this.ShowInTaskbar = false; //不显示在系统任务栏 
                notifyIcon.Visible = true; //托盘图标可见 
            }
        }

        /// <summary>
        /// 点击托盘上的按扭，显示界面
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void notifyIcon_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.Show();
                this.WindowState = FormWindowState.Normal;
                this.ShowInTaskbar = true;
            }
        }

        /// <summary>
        /// 鼠标移到“退出”上时弹出提示信息
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void label1_MouseEnter(object sender, EventArgs e)
        {
            //lable1toolTip.IsBalloon = true;
            lable1toolTip.SetToolTip(label1, "点我关闭服务器");
        }

        /// <summary>
        /// 定时任务，闪烁显示“运行中”
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void timer1_Tick(object sender, EventArgs e)
        {
            label2.Visible = !(label2.Visible);

            //ShowLen++;
            //if (ShowLen > FormTitle.Length) ShowLen = 0;
            //this.Text = FormTitle.Substring(0, ShowLen);
        }


        #region 测试按钮
        private void button2_Click(object sender, EventArgs e)
        {
            //模拟获取参数值
            textBox1.Text = "";
            textBox1.Text = new MainFunction().AddTaskTest_GetParameterValue();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            textBox1.Text = "";
            textBox1.Text = new MainFunction().AddTaskTest_SetParameterValue();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            textBox1.Text = "";
            textBox1.Text = new MainFunction().AddTaskTest_Reboot();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            //textBox1.Text = "";
            //textBox1.Text = new MainFunction().AddTaskTest_GetLog();
            MessageBox.Show("11111111111111111");
            Logger.LogRootDirectory = "d:\\httpserver\\log";
            Logger.Trace(Logger.__INFO__, "province:---------------------");
        }

        private void button5_Click(object sender, EventArgs e)
        {
            textBox1.Text = "";
            textBox1.Text = new MainFunction().AddTaskTest_Upgrad();
        }

        private void button6_Click(object sender, EventArgs e)
        {

            textBox1.Text = "";
            DateTime dt = DateTime.Now;

            new MainFunction().get_tmpvalue_table();

        }
        #endregion

 
        
    }

}
