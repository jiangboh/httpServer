using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace httpServer
{
    #region AP反向连接地址信息
    /// <summary>
    /// AP反向连接地址信息
    /// </summary>
    public class ConnectUrlInfo
    {
        private String url;
        private String name;
        private String passwd;

        public ConnectUrlInfo() : this("", "", "")
        {
        }

        public ConnectUrlInfo(String url, String name, String passwd)
        {
            this.url = url;
            this.name = name;
            this.passwd = passwd;
        }

        public string Url { get => url; set => url = value; }
        public string Name { get => name; set => name = value; }
        public string Passwd { get => passwd; set => passwd = value; }
    }
    #endregion 

    #region 保存AP在线状态及当前连接地址的链表
    public class ApConnHmsInfo
    {
        private String sn;
        private DateTime time;
        private String ip;
        private int port;
        private String[] eventCode;

        public ApConnHmsInfo(String sn)
        {
            this.Sn = sn;
            this.ip = "";
            this.port = 0;
            this.Time = DateTime.Now;
            EventCode = new String[8];
        }

        public string Sn { get => sn; set => sn = value; }
        public DateTime Time { get => time; set => time = value; }
        public string Ip { get => ip; set => ip = value; }
        public int Port { get => port; set => port = value; }
        public string[] EventCode { get => eventCode; set => eventCode = value; }
    }
    public class ApConnHmsList
    {
        private static readonly object locker1 = new object();
        private List<ApConnHmsInfo> connList;

        //private MySqlDbHelper myDB;

        public ApConnHmsList()
        {
            connList = new List<ApConnHmsInfo>();
            //myDB = new MySqlDbHelper();
        }

        public List<ApConnHmsInfo> GetConnList()
        {
            lock (locker1)
            {
                return connList;
            }
        }

        public int GetCount()
        {
            lock (locker1)
            {
                return connList.Count;
            }
        }

        public void add(ApConnHmsInfo connInfo)
        {
            lock (locker1)
            {
                foreach (ApConnHmsInfo x in connList)
                {
                    if (String.Compare(x.Sn, connInfo.Sn, true) == 0)
                    {
                        connList.Remove(x);
                        break;
                    }
                }
                connInfo.Time = DateTime.Now;
                connList.Add(connInfo);
            }
        }

        public void add(List<String> snList)
        {
            lock (locker1)
            {
                foreach (String sn in snList)
                {
                    foreach (ApConnHmsInfo x in connList)
                    {
                        if (String.Compare(x.Sn, sn, true) == 0)
                        {
                            connList.Remove(x);
                            break;
                        }
                    }
                    ApConnHmsInfo connInfo = new ApConnHmsInfo(sn);
                    connList.Add(connInfo);
                }
            }
        }

        public void add(String sn)
        {
            lock (locker1)
            {
                foreach (ApConnHmsInfo x in connList)
                {
                    if (String.Compare(x.Sn, sn, true) == 0)
                    {
                        connList.Remove(x);
                        break;
                    }
                }
                ApConnHmsInfo connInfo = new ApConnHmsInfo(sn);
                connList.Add(connInfo);
            }
        }

        public void remov(String sn)
        {
            lock (locker1)
            {
                foreach (ApConnHmsInfo x in connList)
                {
                    if (String.Compare(x.Sn, sn, true) == 0)
                    {
                        connList.Remove(x);
                        break;
                    }
                }
            }
        }

        public void remov(ApConnHmsInfo connInfo)
        {
            lock (locker1)
            {
                 connList.Remove(connInfo);
            }
        }

        public List<ApConnHmsInfo> CheckConnHmsStatus()
        {
            List<ApConnHmsInfo> offLineList = new List<ApConnHmsInfo>();
            DateTime tNow = new DateTime();
            tNow = DateTime.Now;
            lock (locker1)
            {
                foreach (ApConnHmsInfo x in connList)
                {
                    TimeSpan timeSpan = tNow - x.Time;
                    //如果前次上线时间距当前时间大于70秒，表示Ap已下线。
                    double diff = timeSpan.TotalSeconds;
                    if (diff >= (GlobalParameter.ApHeartbeatTime + 10))
                    {
                        offLineList.Add(x);
                    }
                }
            }
            return offLineList;
        }

        public ApConnHmsInfo getSnForconnList(String ip, int port)
        {
            foreach (ApConnHmsInfo x in connList)
            {
                if (String.Compare(x.Ip, ip, true) == 0 && x.Port == port)
                {
                    return x;
                }
            }
            return null;
        }

    }
    #endregion 

    public class MainFunction
    {
        private MySqlDbHelper myDB;
        private Thread myThread = null;

        public MainFunction()
        {
            this.myDB =  new MySqlDbHelper(GlobalParameter.DB_ConnStr);
            if (!myDB.MyDbConnFlag)
            {
                Log.WriteError(string.Format("打开数据库({0})失败！" , GlobalParameter.DB_ConnStr));
                GlobalParameter.httpServerRun = false;
            }
            else
            {
                Log.WriteInfo(string.Format("打开数据库({0})成功！", GlobalParameter.DB_ConnStr));
            }

            #region 启动重连线程

            // 2019-02-27

            //通过ParameterizedThreadStart创建线程
            Thread thread10 = new Thread(new ParameterizedThreadStart(thread_for_rc_process));

            //给方法传值
            thread10.Start("thread_for_rc_process!\n");
            thread10.IsBackground = true;

            #endregion
        }

        #region 重连处理


        private void re_connection_db(ref MySqlDbHelper helper, string name, ref int reConnCnt)
        {
            try
            {
                string info = "";

                if (helper.Conn_Is_Closed_Or_Abnormal())
                {
                    info = string.Format("{0}:ConnectionState is NO.", name);
                    Logger.Trace(LogInfoType.EROR, info, "Main", LogCategory.I);

                    helper.MyDbConnFlag = false;
                    helper = new MySqlDbHelper(GlobalParameter.DB_ConnStr);

                    reConnCnt++;
                    string tmp = GlobalParameter.DB_ConnStr;

                    if (helper.MyDbConnFlag == true)
                    {
                        info = string.Format("{0}:重连数据库:【{1}->连接数据库OK！】\n", name, tmp);
                        Logger.Trace(LogInfoType.INFO, info, "Main", LogCategory.I);
                        //BeginInvoke(new show_log_info_delegate(show_log_info), new object[] { info, LogInfoType.INFO });
                    }
                    else
                    {
                        info = string.Format("{0}:重连数据库:【{1}->连接数据库FAILED！】\n", name, tmp);
                        Logger.Trace(LogInfoType.INFO, info, "Main", LogCategory.I);
                        //BeginInvoke(new show_log_info_delegate(show_log_info), new object[] { info, LogInfoType.EROR });
                    }
                }
                else
                {
                    info = string.Format("{0}:ConnectionState is OK.\n", name);
                    //Logger.Trace(LogInfoType.INFO, info, "Main", LogCategory.I);
                    //BeginInvoke(new show_log_info_delegate(show_log_info), new object[] { info, LogInfoType.INFO });
                }
            }
            catch (Exception ee)
            {
                string info = string.Format("{0}:re_connection_db : {1}", name, ee.Message + ee.StackTrace);
                Logger.Trace(LogInfoType.EROR, info, "Main", LogCategory.I);
            }
        }


        /// <summary>
        /// 重连线程
        /// </summary>
        /// <param name="obj"></param>
        private void thread_for_rc_process(object obj)
        {
            #region 变量定义

            /*
             * 用于修复mysql连接的空闲时间超过8小时后，MySQL自动断开该连接的问题
             * wait_timeout = 8*3600
             * 即每隔fix_for_wait_timeout的时间（秒数）就访问一下数据库
             */
            int fix_for_wait_timeout = 20; //10*60;

            int reConnCnt = 0;
            DateTime startTimeConn = System.DateTime.Now;
            DateTime endTimeConn = System.DateTime.Now;
            TimeSpan tsConn = endTimeConn.Subtract(startTimeConn);


            #endregion

            while (true)
            {
                Thread.Sleep(50);

                try
                {
                    #region 防止自动断开该连接的问题

                    endTimeConn = System.DateTime.Now;
                    tsConn = endTimeConn.Subtract(startTimeConn);

                    if (tsConn.TotalSeconds >= fix_for_wait_timeout)
                    {
                        // 2018-09-29
                        re_connection_db(ref myDB, "myDB", ref reConnCnt);

                        Thread.Sleep(5);
                        if (myDB.MyDbConnFlag)
                        {
                            List<string> listAllTbl = new List<string>();
                            listAllTbl = myDB.get_all_columnName("userinfo");
                        }

                        startTimeConn = System.DateTime.Now;
                    }

                    #endregion
                }
                catch (Exception ee)
                {
                    startTimeConn = System.DateTime.Now;
                    Logger.Trace(LogInfoType.EROR, ee.Message + ee.StackTrace, "Main", LogCategory.I);
                    continue;
                }
            }
        }

        #endregion

        #region 主函数
        /// <summary>
        /// 运行后台任务处理线程
        /// </summary>
        public void RunMainFunctionThread()
        {
            myThread = new Thread(new ThreadStart(delegate {
                try
                {
                    RunNextTask();
                }
                catch (Exception ex)
                {
                    Log.WriteCrash(ex);
                    GlobalParameter.httpServerRun = false;
                }
            }));
            myThread.IsBackground = true;
            myThread.Start();
        }

        /// <summary>
        /// 停止后台任务处理线程
        /// </summary>
        public void StopMainFunctionThread()
        {
            if (myThread != null)
            {
                myThread.Abort();//终止线程myThread
                //myThread.Join();//等待线程myThread结束
                myThread = null;
            }
        }

        /// <summary>
        /// 后台任务处理(1、检查是否有新任务；2、判断AP是否已经下线)
        /// </summary>
        public void RunNextTask()
        {
            if (!GlobalParameter.httpServerRun)
            {
                Log.WriteError("退出后台任务处理循环！");
                GlobalParameter.CloseThisApp();
                return;
            }

            GlobalParameter.apConnHmsList.add(myDB.GetconnHSByDeviceInfo());
            GlobalParameter.SaveAllClientConfig(myDB);

            Log.WriteDebug("进入后台任务处理循环...");
            while (GlobalParameter.httpServerRun)
            {
                //检查是否有新任务
                try
                {
                    List<String> snList = myDB.GetAllNoSendReqstSnByApTask();
                    foreach (String sn in snList)
                    {
                        String url = ""; String us = ""; String pwd = "";
                        if (!myDB.GetUrlInfoBySn(ref url, ref us, ref pwd, sn))
                        {
                            Log.WriteWarning("获取到的Url为空，不向该AP(" + sn + ")发送请求消息！");
                        }
                        else
                        {
                            if (!String.IsNullOrEmpty(url) && !url.Equals("null"))
                            {
                                ConnectUrlInfo urlInfo = new ConnectUrlInfo(url, us, pwd);
                                Log.WriteDebug("向AP:" + sn + "; Url:" + url + "发状请求连接消息！");
                                HttpHandle.postUrl2Ap(urlInfo);
                                Log.WriteDebug("设置AP(" + sn + ")的所有任务状态为发送请求状态!");
                                if (!myDB.SetApTaskStatusToReqstBySN(sn))
                                {
                                    Log.WriteError("设置AP(" + sn + ")的所有任务状态为发送请求状态错误!");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.WriteError("实时查询AP是否有待下发任务出错！" + ex.Message);
                }

                Thread.Sleep(100);//休眠时间

                //检查AP是否已经下线
                try
                {
                    List<ApConnHmsInfo> offLineList = GlobalParameter.apConnHmsList.CheckConnHmsStatus();
                    foreach (ApConnHmsInfo info in offLineList)
                    {
                        if (string.IsNullOrEmpty(info.Sn))
                        {
                            GlobalParameter.apConnHmsList.remov(info);
                            continue;
                        }
                        if (!myDB.SetconnHSToOffLine(info.Sn))
                        {
                            Log.WriteError("设置AP(" + info.Sn + ")为离线状态不成功!");
                        }
                        else
                        {
                            Log.WriteDebug("设置AP(" + info.Sn + ")为离线状态成功!");
                            GlobalParameter.apConnHmsList.remov(info);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.WriteError("实时检测AP离线状态出错！" + ex.Message);
                }

                Thread.Sleep(100);//休眠时间

                //Kpi文件存库
                try
                {
                    string kpiPath = GlobalParameter.UploadServerRootPath + "\\kpi\\";
                    string savePath = kpiPath + "\\Save\\";
                    DirectoryInfo TheFolder = new DirectoryInfo(kpiPath);
                    foreach (FileInfo NextFile in TheFolder.GetFiles())
                    {
                        int ret = 0;
                        //忽略掉临时文件
                        if (NextFile.Name.Substring(1, 5).Equals("davfs")) continue;
                        strPerformance kpi = new strPerformance();
                        String sn = "";
                        ret = XmlHandle.GetKpiFile2Db(NextFile, ref sn, ref kpi);
                        if (0 != ret)
                        {
                            Log.WriteError("解析Kpi文件[" + NextFile.Name + "]出错！出错原因:("
                                + ret + ")" + XmlHandle.GetErrorCode(ret));
                        }
                        else
                        {
                            if (myDB.deviceinfo_record_exist(sn) == 0)
                            {
                                Log.WriteError("解析Kpi文件[" + NextFile.Name + "]出错！出错原因:(设备未开户)");
                            }
                            else
                            {
                                ret = myDB.performanceInfo_record_insert(sn, kpi);
                                if (0 != ret)
                                {
                                    Log.WriteError("保存Kpi文件[" + NextFile.Name + "]到数据库出错！出错原因:("
                                        + ret + ")" + myDB.get_rtv_str(ret));
                                    continue;
                                }
                            }
                        }
                        
                        if (false == System.IO.Directory.Exists(savePath))
                        {
                            //创建Save文件夹
                            System.IO.Directory.CreateDirectory(savePath);
                        }

                        try
                        {
                            File.Copy(NextFile.FullName, savePath + "\\" + NextFile.Name,true);
                        }
                        catch (Exception ex)
                        {
                            Log.WriteError("复制Kpi文件[" + NextFile.Name + "]到备份路径出错！" + ex.Message);
                        }
                        File.Delete(NextFile.FullName);
                    }
                }
                catch (Exception ex)
                {
                    Log.WriteError("实时检测Kpi文件，并保存到数据库出错！" + ex.Message);
                }

                Thread.Sleep(100);//休眠时间
            }


            Log.WriteError("退出后台任务处理循环！");
            GlobalParameter.CloseThisApp();
        }
        #endregion

        #region 测试消息

        //public XmlParameter[] cwmp_GetParameterValues(String[] ParameterName)
        //{
        //    XmlParameter[] parmeterStruct = new XmlParameter[ParameterName.Length];


        //    return parmeterStruct;
        //}

        public String AddTaskTest_GetParameterValue()
        {
            //模拟获取参数值
            string Id = "GetParameterValue";
            if (!myDB.GetTaskId(TaskType.GetParameterValuesTask, ref Id))
            {
                return "";
            }
            String[] nameList = new string[9];
            nameList[0] = "Services.FAPService.1.Transport.SCTP.Assoc.1.PrimaryPeerAddress";
            nameList[1] = "Services.FAPService.1.Transport.SCTP.Assoc.8.PrimaryPeerAddress";
            nameList[2] = "Services.FAPService.1.FAPControl.LTE.Gateway.AGServerIp1";
            nameList[3] = "Services.FAPService.1.FAPControl.LTE.Gateway.AGPort1";
            nameList[4] = "Services.FAPService.1.FAPControl.LTE.Gateway.SecGWServer1";
            nameList[5] = "Services.FAPService.1.FAPControl.LTE.Gateway.SecGWServer2";
            nameList[6] = "Services.FAPService.1.FAPControl.LTE.Gateway.SecGWServer3";
            nameList[7] = "Time.NTPServer1";
            nameList[8] = "Time.NTPServer2";
            
            byte[] xmlStr = XmlHandle.CreateGetParameterValuesXmlFile(Id, nameList);

            String[] snList = new String[3];
            snList[0] = "EN18001151600006";
            snList[1] = "EN1800S116340039";
            snList[2] = "EN1801E116480235";
            myDB.AddTaskToTable("GetParameterValue", Id,
                 TaskType.GetParameterValuesTask, System.Text.Encoding.Default.GetString(xmlStr), "",snList);

            return System.Text.Encoding.Default.GetString(xmlStr);
        }

        public String AddTaskTest_SetParameterValue()
        {
            String Id = "SetParameterValue";
            if (!myDB.GetTaskId(TaskType.SetParameterValuesTask, ref Id))
            {
                return "";
            }
            List<XmlParameter> parameterList = new List<XmlParameter>();
            XmlParameter xmlParameter1 = new XmlParameter("Services.FAPService.1.FAPControl.LTE.Gateway.AGPort1", "123");
            parameterList.Add(xmlParameter1);
            XmlParameter xmlParameter2 = new XmlParameter("Services.FAPService.1.FAPControl.LTE.Gateway.AGServerIp1", "10.0.0.2");
            parameterList.Add(xmlParameter2);
            XmlParameter xmlParameter3 = new XmlParameter("Time.NTPServer1", "1.1.1.1");
            parameterList.Add(xmlParameter3);
            XmlParameter xmlParameter4 = new XmlParameter("Time.NTPServer2", "2.2.2.2");
            parameterList.Add(xmlParameter4);

            byte[] xmlStr = XmlHandle.CreateSetParameterValuesXmlFile(Id, parameterList);
            if (xmlStr.Length <= 0)
            {
                Log.WriteInfo("生成XML文件出错！");
                return "";
            }

            String[] snList = new String[3];
            snList[0] = "EN18001151600006";
            snList[1] = "EN1800S116340039";
            snList[2] = "EN1801E116480235";
            myDB.AddTaskToTable("SetParameterValue",Id,
                TaskType.SetParameterValuesTask, System.Text.Encoding.Default.GetString(xmlStr),"", snList);

            return System.Text.Encoding.Default.GetString(xmlStr);
        }

        public String AddTaskTest_Reboot()
        {
            String Id = "Reboot";
            if (!myDB.GetTaskId(TaskType.RebootTask, ref Id))
            {
                return "";
            }

            byte[] xmlStr = XmlHandle.CreateRebootXmlFile(Id);
            if (xmlStr.Length <= 0)
            {
                Log.WriteInfo("生成XML文件出错！");
                return "";
            }

            String[] snList = new String[3];
            snList[0] = "EN18001151600006";
            snList[1] = "EN1800S116340039";
            snList[2] = "EN1801E116480235";
            myDB.AddTaskToTable("Reboot",Id,
               TaskType.RebootTask, System.Text.Encoding.Default.GetString(xmlStr), "",snList);

            return System.Text.Encoding.Default.GetString(xmlStr);
        }

        public String AddTaskTest_GetLog()
        {
            String Id = "GetLog";
            string fileName = "test.tar.gz";
            if (!myDB.GetTaskId(TaskType.GetLogTask, ref Id))
            {
                return "";
            }

            byte[] xmlStr = XmlHandle.CreateUploadXmlFile(Id, Id,
                 1, fileName, UploadFileType.VendorConfigurationFile);
            if (xmlStr.Length <= 0)
            {
                Log.WriteInfo("生成XML文件出错！");
                return "";
            }


            String[] snList = new String[3];
            snList[0] = "EN18001151600006";
            snList[1] = "EN1800S116340039";
            snList[2] = "EN1801E116480235";
            myDB.AddTaskToTable("GetLog", Id,
                TaskType.GetLogTask, System.Text.Encoding.Default.GetString(xmlStr), "",snList);

            foreach (string sn in snList)
            {
                myDB.aploginfo_record_insert(sn,Id,DateTime.Now.ToString(), fileName,0,"up log");
            }

            return System.Text.Encoding.Default.GetString(xmlStr);
        }

        public String AddTaskTest_Upgrad()
        {
            String Id = "Upgrad";
            if (!myDB.GetTaskId(TaskType.UpgradTask, ref Id))
            {
                return "";
            }

            byte[] xmlStr = XmlHandle.CreateDownloadXmlFile(Id, Id,
                 1,316237, "JIANGBO_TEST20180115_Release.tar.gz", DownloadFileType.FirmwareUpgradeImage);
            if (xmlStr.Length <= 0 )
            {
                Log.WriteInfo("生成XML文件出错！");
                return "";
            }

            String[] snList = new String[3];
            snList[0] = "EN18001151600006";
            snList[1] = "EN1800S116340039";
            snList[2] = "EN1801E116480235";
            myDB.AddTaskToTable("Upgrad", Id,
                TaskType.UpgradTask, System.Text.Encoding.Default.GetString(xmlStr), DateTime.Now.ToString(), snList);

            return System.Text.Encoding.Default.GetString(xmlStr);
        }

        public void get_tmpvalue_table()
        {
            DataTable dt = new DataTable();
            myDB.tmpvalue_record_entity_get_by_query(ref dt, "EN1800S116340039", "GetParameterValuesTask_20180205101832_13");
            
        }
        #endregion


    }
}
