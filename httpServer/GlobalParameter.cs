using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace httpServer
{
    /*
    public enum TaskStatus   //任务状态
    {
        TaskNull = 0, //0:无该任务；
        NoSendReqst = 1, //1:未下发请求；
        SendReqst = 2, //2:已下发请示;
        SendTask = 3, //3:已下发任务;
        ReponseOk = 4,　　//4:已收到回应,状态为成功;
        ReponseFail = 5,　　//5:已收到回应，状态为失败;
        TimeOut = 6  //6：任务超时
    }

    public enum TaskType   //任务类型
    {
        TaskNull = 0, //0:无任务；
        UpgradTask = 1, //1:升级任务；
        GetLogTask = 2, //2:获取Log任务；
        GetParameterValuesTask = 3, //3:获取参数值
        SetParameterValuesTask = 4,  //4：设置参数值
        RebootTask = 5  //重启AP任务
    }
    */
    public class GlobalParameter
    {
        #region  全局参数定义
        /// <summary>
        /// 服务器运行状态(true:正常运行;false:未正常运行)
        /// </summary>
        static public volatile bool httpServerRun = true;
        /// <summary>
        /// 当前在线的AP信息
        /// </summary>
        static public ApConnHmsList apConnHmsList = new ApConnHmsList();

        static private MainFunction mainFunction=null;
        static private HttpHandle httpHandle=null;

        //static public MySqlDbHelper myDB11 = new MySqlDbHelper();

        static public string logRootDirectory = Application.StartupPath + @"\Log";

        /// <summary>
        /// 网管连接字符串
        /// </summary>
        static public String DB_ConnStr = "192.168.88.101";
        /// <summary>
        /// 本机IP
        /// </summary>
        static public String ThisIp = "192.168.88.101";

        /// <summary>
        /// http Server的用户名
        /// </summary>
        static public String HttpServerName = "name";
        /// <summary>
        /// http Server的密码
        /// </summary>
        static public String HttpServerPasswd = "passwd";

        /// <summary>
        /// 文件服务器地址
        /// </summary>
        static public String UploadServerUrl = "http://" + ThisIp + ":8088"; //Kpi

        /// <summary>
        /// http Server的根地址
        /// </summary>
        static public String UploadServerRootPath = "d://";
        /// <summary>
        /// 文件服务器连接用户名
        /// </summary>
        static public String UploadServerUser = "uploadKpiUser"; //Kpi
        /// <summary>
        /// 文件服务器连接密码
        /// </summary>
        static public String UploadServerPasswd = "UploadKpiPassWd"; //Kpi

        /// <summary>
        /// NTP Server 1 地址
        /// </summary>
        static public String Ntp1ServerPath = "192.168.88.101"; 
        /// <summary>
        /// NTP Server 2 地址
        /// </summary>
        static public String Ntp2ServerPath = "192.168.88.101"; 

        /// <summary>
        /// AP 反向连接用户名
        /// </summary>
        static public String ConnectionRequestUsername = "test";
        /// <summary>
        /// AP 反向连接密码
        /// </summary>
        static public String ConnectionRequestPassWd = "test";

        /// <summary>
        /// AP心跳超时时间，（单位：秒）
        /// </summary>
        static public int ApHeartbeatTime = 60;   //

        /// <summary>
        /// XML文件的根节点（"Device."或者"InternetGatewayDevice."）
        /// </summary>
        static public string XmlRootNode = "Device.";

        static public Dictionary<string, string> AutonomousTransferKey = new Dictionary<string, string>();

        #endregion

        /// <summary>
        /// 获取配置文件
        /// </summary>
        static public void SetGlobalParameter()
        {
            Log.SetLogLevel(ConfigurationManager.AppSettings["LogLevel"].ToString());
            Log.MaxLogFileSize = int.Parse(ConfigurationManager.AppSettings["MaxLogFileSize"].ToString());
            logRootDirectory = ConfigurationManager.AppSettings["LogFolder"].ToString();
            Log.LogFolder = logRootDirectory;

            Log.WriteInfo("\n\n\n", false);
            Log.WriteInfo("启动" + Program.FormTitle + "...");
            Log.WriteInfo("获取配置参数...");
            try
            { 
                DB_ConnStr = ConfigurationManager.ConnectionStrings["dbConStr"].ToString();
            }
            catch (Exception e)
            {
                Log.WriteError("读取数据库连接配置出错。退出程序！" + e.Message);
                CloseThisApp();
                return;
            }
            //try
            //{
            //    ThisIp = ConfigurationManager.AppSettings["ThisIp"].ToString();
            //}
            //catch (Exception e)
            //{
            //    Log.WriteError("读取参数(ThisIp)出错！" + e.Message);
            //}

            try
            {
                HttpServerName = ConfigurationManager.AppSettings["HttpServerName"].ToString();
            }
            catch (Exception e)
            {
                Log.WriteError("读取参数(HttpServerName)出错！" + e.Message);
            }
           
            try
            {
                HttpServerPasswd = ConfigurationManager.AppSettings["HttpServerPasswd"].ToString();
            }
            catch (Exception e)
            {
                Log.WriteError("读取参数(HttpServerPasswd)出错！" + e.Message);
            }

            try
            {
                Ntp1ServerPath = ConfigurationManager.AppSettings["Ntp1ServerPath"].ToString();
            }
            catch (Exception e)
            {
                Log.WriteError("读取参数(Ntp1ServerPath)出错！" + e.Message);
            }
  
            try
            {
                Ntp2ServerPath = ConfigurationManager.AppSettings["Ntp2ServerPath"].ToString();
            }
            catch (Exception e)
            {
                Log.WriteError("读取参数(Ntp2ServerPath)出错！" + e.Message);
            }

            try
            {
                ConnectionRequestUsername = ConfigurationManager.AppSettings["ConnectionRequestUsername"].ToString();
            }
            catch (Exception e)
            {
                Log.WriteError("读取参数(ConnectionRequestUsername)出错！" + e.Message);
            }

            try
            {
                ConnectionRequestPassWd = ConfigurationManager.AppSettings["ConnectionRequestPassWd"].ToString();
            }
            catch (Exception e)
            {
                Log.WriteError("读取参数(ConnectionRequestPassWd)出错！" + e.Message);
            }

            try
            {
                UploadServerRootPath = ConfigurationManager.AppSettings["UploadServerRootPath"].ToString();
            }
            catch (Exception e)
            {
                Log.WriteError("读取参数(UploadServerRootPath)出错！" + e.Message);
            }
           
            try
            {
                UploadServerUrl = ConfigurationManager.AppSettings["UploadServerUrl"].ToString();
            }
            catch (Exception e)
            {
                Log.WriteError("读取参数(UploadServerUrl)出错！" + e.Message);
            }

            try
            {
                UploadServerUser = ConfigurationManager.AppSettings["UploadServerUser"].ToString();
            }
            catch (Exception e)
            {
                Log.WriteError("读取参数(UploadServerUser)出错！" + e.Message);
            }
   
            try
            {
                UploadServerPasswd = ConfigurationManager.AppSettings["UploadServerPasswd"].ToString();
            }
            catch (Exception e)
            {
                Log.WriteError("读取参数(UploadServerPasswd)出错！" + e.Message);
            }

            try
            {
                XmlRootNode = ConfigurationManager.AppSettings["XmlRootNode"].ToString();
            }
            catch (Exception e)
            {
                Log.WriteError("读取参数(XmlRootNode)出错！" + e.Message);
            }

            try
            {
                ApHeartbeatTime = int.Parse(ConfigurationManager.AppSettings["ApHeartbeatTime"].ToString());
            }
            catch (Exception e)
            {
                Log.WriteError("读取参数(ApHeartbeatTime)出错！" + e.Message);
            }
        }

        /// <summary>
        /// 开始运行主应用程序
        /// </summary>
        static public void StartThisApp()
        {
            GlobalParameter.SetGlobalParameter();
            Logger.LogRootDirectory = GlobalParameter.logRootDirectory;

            if (GlobalParameter.httpServerRun)
            {
                mainFunction = new MainFunction();
                mainFunction.RunMainFunctionThread();
            }
            if (GlobalParameter.httpServerRun)
            {
                httpHandle = new HttpHandle();
                httpHandle.RunHttpServerThread();
            }
        }

        /// <summary>
        /// 关闭应用程序
        /// </summary>
        static public void CloseThisApp(bool isShow)
        {
            if (isShow)
            {
                MessageBox.Show("HttpServer程序异常退出！", "错误：");
            }
            GlobalParameter.httpServerRun = false;
            if (mainFunction != null)
                mainFunction.StopMainFunctionThread();
            if (httpHandle != null)
                httpHandle.StopHttpServerThread();
            Application.Exit();
        }

        static public void SaveConfig2Db(MySqlDbHelper myDB,string name,string value,string type)
        {
            string errInfo="";
            strConfig strC = new strConfig();
            strC.name = name;
            strC.value = value;
            strC.type = type;
            if (0 == myDB.configinfo_record_create(strC,ref errInfo))
            {
                Log.WriteInfo(string.Format("将配置({0}={1})写入到数据库成功！" ,name,value));
            }
            else
            {
                Log.WriteError(string.Format("将配置({0}={1})写入到数据库失败！", name, value));
            }
        }
		
        /// <summary>
        /// 保存客户端也需要使用的配置到数据库
        /// </summary>
        /// <param name="myDB"></param>
        static public void SaveAllClientConfig(MySqlDbHelper myDB)
        {
            GlobalParameter.SaveConfig2Db(myDB, "RootDirectory", GlobalParameter.XmlRootNode, "Server");

            GlobalParameter.SaveConfig2Db(myDB, "UploadServerUrl", GlobalParameter.UploadServerUrl, "Server");
            GlobalParameter.SaveConfig2Db(myDB, "UploadServerUser", GlobalParameter.UploadServerUser, "Server");
            GlobalParameter.SaveConfig2Db(myDB, "UploadServerPasswd", GlobalParameter.UploadServerPasswd, "Server");

            GlobalParameter.SaveConfig2Db(myDB, "ConnectionRequestUsername", GlobalParameter.ConnectionRequestUsername, "Server");
            GlobalParameter.SaveConfig2Db(myDB, "ConnectionRequestPassWd", GlobalParameter.ConnectionRequestPassWd, "Server");

            GlobalParameter.SaveConfig2Db(myDB, "Ntp1ServerPath", GlobalParameter.Ntp1ServerPath, "Server");
            GlobalParameter.SaveConfig2Db(myDB, "Ntp2ServerPath", GlobalParameter.Ntp2ServerPath, "Server");
        }
		
        static public void CloseThisApp()
        {
            CloseThisApp(true);
        }
    }
}
