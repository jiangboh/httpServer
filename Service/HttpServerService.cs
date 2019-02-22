using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using httpServerService;

namespace Service
{
    public partial class Service1 : ServiceBase
    {
        [DllImport("kernel32.dll")]
        public static extern int WinExec(string exeName, int operType);

        private Boolean isRun = false;

        private static string ExePath = System.AppDomain.CurrentDomain.BaseDirectory;//"C:\\Program Files (x86)\\Bravo\\HttpServer\\HttpServer.exe";
        private static string ExeName = @"HttpServer.exe";
        private static string ExePathName = ExePath + ExeName;

        private string ProcessName = "HttpServer"; //exe的进程名
        private Process runProcess = null;

        public Service1()
        {
            InitializeComponent();
        }

        private void writeLog(string str)
        {
            string LogFilePath = System.AppDomain.CurrentDomain.BaseDirectory;
            string FILE_NAME = LogFilePath + "log.txt";//每天按照日期建立一个不同的文件名  
            StreamWriter sr;
            if (File.Exists(FILE_NAME)) //如果文件存在,则创建File.AppendText对象  
            {
                sr = File.AppendText(FILE_NAME);
            }
            else  //如果文件不存在,则创建File.CreateText对象  
            {
                sr = File.CreateText(FILE_NAME);
            }
            string content = "[" + System.DateTime.Now.ToString() + "] " + str;
            sr.WriteLine(content);//将传入的字符串加上时间写入文本文件一行  
            sr.Close();
        }

        protected void StartExe(string appName)
        {
            ProcessStartInfo info = new ProcessStartInfo();
            info.FileName = appName;
            info.Arguments = "";
            info.WindowStyle = ProcessWindowStyle.Minimized;
            runProcess = Process.Start(info);
            writeLog("打开程序！" + appName);
            //runProcess.WaitForExit();
        }

        protected void CloseExe(string appName)
        {
            if (runProcess != null && runProcess.ProcessName.Equals(appName))
            {
                writeLog("关闭程序！" + appName);
                //try
                //{
                runProcess.Kill();
                //}
                //catch (Exception e)
                //{
                //    writeLog("关闭程序出错！" + e.Message.ToString());
                //}

                //runProcess.WaitForExit();
            }
        }

        private void CheckProcessByName()
        {
            if (runProcess == null)
            {
                StartExe(ExePathName);
            }
            else
            {
                Process[] localByName = Process.GetProcessesByName(ProcessName);
                if (!IsExistProcess(ProcessName)) //如果得到的进程数是0, 那么说明程序未启动，需要启动程序
                {
                    //Process.Start("HttpServer.exe"); //启动程序的路径
                    StartExe(ExePathName);
                }
            }
        }

        private bool IsExistProcess(string processName)
        {
            Process[] MyProcesses = Process.GetProcesses();
            foreach (Process MyProcess in MyProcesses)
            {
                if (MyProcess.ProcessName.CompareTo(processName) == 0)
                {
                    return true;
                }
            }
            return false;
        }

        public static void Delay(int milliSecond)
        {
            int start = Environment.TickCount;
            while (Math.Abs(Environment.TickCount - start) < milliSecond)
            {
                System.Threading.Thread.Sleep(100);
            }
        }

        protected void OnStartApp()
        {
            writeLog(string.Format("开始检测{0}程序运行是否正常！",ExePathName));
            while (isRun)
            {
                try
                {
                    CheckProcessByName(); //Timer_Click是到达时间的时候执行事件的函数
                } 
                catch (Exception ex)
                {
                    writeLog("检测进程是否存在出错！" + ex.Message.ToString());
                }
                Delay(3000);
            }
        }

        protected override void OnStart(string[] args)
        {
            isRun = true;
            writeLog("启动后台服务！");
            //writeLog("CurrentDirectory:" + System.Environment.CurrentDirectory);
            //writeLog("GetCurrentDirectory:" + System.IO.Directory.GetCurrentDirectory());
            //writeLog("BaseDirectory:" + System.AppDomain.CurrentDomain.BaseDirectory);
            //writeLog("ApplicationBase:" + System.AppDomain.CurrentDomain.SetupInformation.ApplicationBase);

            Thread t = new Thread(OnStartApp);//创建了线程还未开启
            t.Start();//用来给函数传递参数，开启线程
            //ui.CreateProcess("HttpServer.exe", @"E:\mySofte\vs2017\httpServer\httpServer\bin\Debug\");
            //ui.CreateProcess("cmd.exe", @"C:\Windows\System32\");
        }
        
        protected override void OnStop()
        {
            isRun = false;
            writeLog("关闭后台服务！");
            Delay(1000);
            CloseExe(ProcessName);
        }
    }
}
