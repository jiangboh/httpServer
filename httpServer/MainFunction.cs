using System;
using System.Collections.Generic;
using System.Data;
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
                    double diff = timeSpan.TotalMinutes;
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
                Log.WriteError("打开数据库失败！" + GlobalParameter.DB_ConnStr);
                GlobalParameter.httpServerRun = false;
            }
        }

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
                 TaskType.GetParameterValuesTask, System.Text.Encoding.Default.GetString(xmlStr), snList);

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
                TaskType.SetParameterValuesTask, System.Text.Encoding.Default.GetString(xmlStr), snList);

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
               TaskType.RebootTask, System.Text.Encoding.Default.GetString(xmlStr), snList);

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
                TaskType.GetLogTask, System.Text.Encoding.Default.GetString(xmlStr), snList);

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
                TaskType.UpgradTask, System.Text.Encoding.Default.GetString(xmlStr), snList);

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
