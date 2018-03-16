using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Web;

namespace httpServer
{
    class HttpHandle
    {
        //static public List<SnIpPortNode> snList;
        static private MySqlDbHelper httpserverDB;// = new MySqlDbHelper();

        static private int AlarmMaxNum = 32;
        static private string PeriodicGetValue = "GetParameterValue_Periodic";
        //private Thread myThread = null;
        private HttpListener httpListenner = null;

        /// <summary>
        /// 作为客户端向AP发送连接请求
        /// </summary>
        /// <param name="urlInfo">AP反向连接地址信息</param>
        static public void postUrl2Ap(ConnectUrlInfo urlInfo)
        {
            if (urlInfo.Url.Length <= 0)
            {
                //Log.WriteWarning("该AP的反向连接地址为空!");
                return;
            }
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(urlInfo.Url);
            request.Method = "GET";
            request.Credentials = new NetworkCredential(urlInfo.Name, urlInfo.Passwd);
            request.KeepAlive = true;

            try
            {
                WebResponse response = request.GetResponse();
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    while (reader.Peek() != -1)
                    {
                        Console.WriteLine(reader.ReadLine());
                    }

                }
            }
            catch (Exception ex)
            {
                Log.WriteError("postUrl2Ap:" + ex.Message);
            }
        }
        
        /// <summary>
        /// 创建http Server线程
        /// </summary>
        public void RunHttpServerThread()
        {
            httpserverDB = new MySqlDbHelper(GlobalParameter.DB_ConnStr);
            if (!httpserverDB.MyDbConnFlag)
            {
                Log.WriteError("打开数据库失败！" + GlobalParameter.DB_ConnStr);
                GlobalParameter.CloseThisApp();
            }

            httpListenner = new HttpListener();
            httpListenner.AuthenticationSchemes = AuthenticationSchemes.Anonymous;
            //httpListenner.AuthenticationSchemes = AuthenticationSchemes.Basic;
            httpListenner.Prefixes.Add("http://+:8080/");
            try
            {
                httpListenner.Start();
                Log.WriteDebug("Http Server 启动成功!");

                httpListenner.BeginGetContext(new AsyncCallback(Context), httpListenner);
                Log.WriteDebug("开始循环接收Http消息...");
            }
            catch (Exception ex)
            {
                Log.WriteCrash(ex);
                GlobalParameter.CloseThisApp();
            }

            //snList = new List<SnIpPortNode>();
        }

        public void StopHttpServerThread()
        {
            httpListenner.Stop();
            httpListenner.Close();
        }

        static void Context(IAsyncResult result)
        {
            if (!GlobalParameter.httpServerRun) return;

            HttpListener listenner = (HttpListener)result.AsyncState;
            try
            {
                HttpListenerContext context = listenner.EndGetContext(result);

                Thread myThread = new Thread(new ThreadStart(delegate
                {
                    HandleMsg(context);
                }));
                myThread.IsBackground = true;
                myThread.Start();
                Thread.Sleep(100);//休眠时间
            }
            catch
            {
                Log.WriteError("Http Server 处理消息出错!");
            }
            listenner.BeginGetContext(new AsyncCallback(Context), listenner);
        }


        /// <summary>
        /// 循环接收处理http消息
        /// </summary>
        /// <param name="httpListenner">http消息参数</param>
        static private void HandleMsg(HttpListenerContext context)
        {
            if (!GlobalParameter.httpServerRun) return;

            try
            {
                //HttpListenerContext context =httpListenner.GetContext();
                HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;

                Servlet servlet = new MyServlet(httpserverDB);
                servlet.onCreate();

                if (request.HttpMethod == "POST")
                {
                    servlet.onPost(request, response);
                    response.Close();
                }
                else if (request.HttpMethod == "GET")
                {
                    servlet.onGet(request, response);
                    response.Close();
                }
                Thread.Sleep(100);//休眠时间
            }
            catch
            {
                Log.WriteError("Http Server 处理消息出错!");
            }
        }

        #region 定义http消息处理类
        public class Servlet
        {
            public virtual void onGet(System.Net.HttpListenerRequest request, System.Net.HttpListenerResponse response) { }
            public virtual void onPost(System.Net.HttpListenerRequest request, System.Net.HttpListenerResponse response) { }
            public virtual void onCreate() { }
        }
        #endregion

        public class MyServlet : HttpHandle.Servlet
        {
            private MySqlDbHelper myDB;// = new MySqlDbHelper();

            public MyServlet(MySqlDbHelper db)
            {
                //this.myDB = new MySqlDbHelper(GlobalParameter.DB_ConnStr);
                //if (!myDB.MyDbConnFlag)
                //{
                //    Log.WriteError("打开数据库失败！" + GlobalParameter.DB_ConnStr);
                //    GlobalParameter.httpServerRun = false;
                //}
                this.myDB = db;
            }

            public override void onCreate()
            {
                base.onCreate();
            }

            /// <summary>
            /// 获取下条要发到AP的消息。
            /// </summary>
            /// <param name="ip">要下发AP的IP地址</param>
            /// <param name="port">要下发AP的端口</param>
            /// <returns>如果有其它任务，返回其它任务XML。否则回复空消息。</returns>
            private byte[] NextSendMsgHandle(String ip,int port)
            {
                byte[] xml;

                ApConnHmsInfo connInfo = GlobalParameter.apConnHmsList.getSnForconnList(ip, port);
                if (connInfo != null)
                {
                    xml = Encoding.UTF8.GetBytes(""); //回复空post
                }
                else
                {
                    TaskType taskType= TaskType.TaskNull;
                    string taskId = "";
                    xml = myDB.GetTaskBySN(ref taskType, ref taskId,connInfo.Sn);
                    if ((xml != null) && (taskType != TaskType.TaskNull) && !string.IsNullOrEmpty(taskId))
                    {
                        if(!myDB.SetApTaskStatusBySN(connInfo.Sn, taskType, TaskStatus.SendTask))
                        {
                            string str = String.Format("修改SN({0}),任务类型({1}),任务状态({2})错误！",
                                connInfo.Sn, taskType, TaskStatus.SendTask);
                            Log.WriteError(str);
                        }
                    }  
                }

                return xml;
            }

            /// <summary>
            /// 收到空消息处理过程
            /// </summary>
            /// <param name="ip">发送消息AP的IP</param>
            /// <param name="port">发送消息AP的端口</param>
            /// <returns>如果有下条任务，返回下条任务XML，否则返回空</returns>
            private byte[] RecvEmptyMsgHandle(String ip, int port)
            {
                byte[] xml;

                ApConnHmsInfo connInfo = GlobalParameter.apConnHmsList.getSnForconnList(ip, port);
                if (connInfo == null)
                {
                    Log.WriteError("上线列表中找不到该AP，回复空消息。");
                    xml = Encoding.UTF8.GetBytes(""); //回复空post
                }
                else
                {
                    if (XmlHandle.GetEventInList(connInfo.EventCode,InformEventCode.BOOT))
                    {
                        List<XmlParameter> inList = new List<XmlParameter>();
                        System.Data.DataTable dt = new System.Data.DataTable();
                        if (0 == myDB.inform_1boot_record_entity_get(ref dt))
                        {
                            for (int i = 0; i < dt.Rows.Count; i++)
                            {
                                XmlParameter xmlParameter = new XmlParameter(dt.Rows[i][0].ToString(),
                                    dt.Rows[i][1].ToString());
                                inList.Add(xmlParameter);
                            }
                        }
                        
                        xml = XmlHandle.Create_1BOOT_InformResponse(inList);
                        //xml = XmlHandle.CreateRebootXmlFile("reboot1233");
                    }
                    else if (XmlHandle.GetEventInList(connInfo.EventCode, InformEventCode.PERIODIC))
                    {
                        //发送获取AP状态的节点
                        String[] nameList = new string[4];
                        nameList[0] = "Services.FAPService.1.CellConfig.LTE.RAN.RF.X_VENDOR_PHY_CELL_ID";
                        nameList[1] = "Services.FAPService.1.CellConfig.LTE.RAN.RF.EARFCNDL";
                        nameList[2] = "DeviceInfo.X_VENDOR_ENODEB_STATUS.X_VENDOR_CELL_STATUS";
                        nameList[3] = "Services.FAPService.1.CellConfig.LTE.EPC.TAC";
                        xml = XmlHandle.CreateGetParameterValuesXmlFile(PeriodicGetValue, nameList);
                    }
                    else
                    {
                        TaskType taskType = TaskType.TaskNull;
                        string taskId = "";
                        Log.WriteDebug("获取SN(" + connInfo.Sn + ")的任务。");
                        xml = myDB.GetTaskBySN(ref taskType, ref taskId, connInfo.Sn);
                        if ((xml != null) && (taskType != TaskType.TaskNull) && (false == string.IsNullOrEmpty(taskId)))
                        {
                            if (!myDB.SetApTaskStatusBySN(connInfo.Sn, taskType, TaskStatus.SendTask))
                            {
                                string str = String.Format("修改SN({0}),任务类型({1}),任务状态({2})！",
                                    connInfo.Sn, taskType, TaskStatus.SendTask);
                                Log.WriteError(str);
                            }
                        }
                        else
                        {
                            xml= Encoding.UTF8.GetBytes(""); //回复空post
                        }
                    }
                }

                return xml;
            }

            /// <summary>
            /// 告警相关结构
            /// </summary>
            public struct StructAlarm
            {
                public string EventTime;
                public string AlarmIdentifier;
                public string NotificationType;
                public string ManagedObjectInstance;
                public string EventType;
                public string ProbableCause;
                public string SpecificProblem;
                public string PerceivedSeverity;
                public string AdditionalText;
                public string AdditionalInformation;
            }

            /// <summary>
            /// 将收到的参数值保存到临时数据库
            /// </summary>
            /// <param name="sn">上报AP的sn</param>
            /// <param name="id">下发任务的任务Id</param>
            /// <param name="valueChangeNode">收到的值列表</param>
            private void SaveParameterList(String sn, string id,List<XmlParameter> valueChangeNode)
            {
                //去掉周期性查询参数获取到的值
                if (String.Compare(id, PeriodicGetValue, true) == 0)
                {
                    return;
                }

                foreach (XmlParameter x in valueChangeNode)
                {
                    if (string.IsNullOrEmpty(x.name))
                    {
                        continue;
                    }

                    Log.WriteDebug("保存获取到的参数到临时表!!");

                    int errCode = myDB.tmpvalue_record_entity_insert(sn,id,x.name ,x.value,x.valueType);
                    if (0 != errCode)
                    {
                        Log.WriteError("更新AP状态或参数修改出错。出错码：" + errCode);
                    }
                }
            }
            
            /// <summary>
            /// 根据告警信息，更改S1状态
            /// </summary>
            /// <param name="sn"></param>
            /// <param name="structAlarm"></param>
            /// <returns></returns>
            private Boolean ChangeApDeviceStatus(string sn,StructAlarm[] structAlarm)
            {
                for (int i = 0; i < AlarmMaxNum; i++)
                {
                    //sctp告警
                    if (String.Compare(structAlarm[i].AlarmIdentifier, "84", true) == 0)
                    {
                        structDeviceInfo deviceInfo = new structDeviceInfo();
                        if (String.Compare(structAlarm[i].NotificationType, "NewAlarm", true) == 0)
                        {
                            deviceInfo.s1Status = "offline";
                        }
                        else
                        {
                            deviceInfo.s1Status = "online";
                        }

                        int errCode = myDB.deviceinfo_record_update(sn, deviceInfo);
                        if (0 != errCode)
                        {
                            Log.WriteError("更新AP S1状态出错。出错码：" + errCode);
                        }
                    }
                }
                return true;
            }

            /// <summary>
            /// 将告警信息存到告警数组列表中
            /// </summary>
            /// <param name="name"></param>
            /// <param name="value"></param>
            /// <param name="structAlarm"></param>
            /// <returns></returns>
            private Boolean GetAlarmStruct(String name , String value,ref StructAlarm[] structAlarm)
            {
                for (int i = 0; i< AlarmMaxNum; i++)
                {
                    String strName;
                    strName = string.Format("FaultMgmt.ExpeditedEvent.{0}.EventTime",i+1 );
                    if (String.Compare(name, strName, true) == 0)
                    {
                        structAlarm[i].EventTime = value;
                        return true;
                    }
                    strName = string.Format("FaultMgmt.ExpeditedEvent.{0}.EventType", i + 1);
                    if (String.Compare(name, strName, true) == 0)
                    {
                        structAlarm[i].EventType = value;
                        return true;
                    }
                    strName = string.Format("FaultMgmt.ExpeditedEvent.{0}.AlarmIdentifier", i + 1);
                    if (String.Compare(name, strName, true) == 0)
                    {
                        structAlarm[i].AlarmIdentifier = value;
                        return true;
                    }
                    strName = string.Format("FaultMgmt.ExpeditedEvent.{0}.NotificationType", i + 1);
                    if (String.Compare(name, strName, true) == 0)
                    {
                        structAlarm[i].NotificationType = value;
                        return true;
                    }
                    strName = string.Format("FaultMgmt.ExpeditedEvent.{0}.ManagedObjectInstance", i + 1);
                    if (String.Compare(name, strName, true) == 0)
                    {
                        structAlarm[i].ManagedObjectInstance = value;
                        return true;
                    }
                    strName = string.Format("FaultMgmt.ExpeditedEvent.{0}.ProbableCause", i + 1);
                    if (String.Compare(name, strName, true) == 0)
                    {
                        structAlarm[i].ProbableCause = value;
                        return true;
                    }
                    strName = string.Format("FaultMgmt.ExpeditedEvent.{0}.SpecificProblem", i + 1);
                    if (String.Compare(name, strName, true) == 0)
                    {
                        structAlarm[i].SpecificProblem = value;
                        return true;
                    }
                    strName = string.Format("FaultMgmt.ExpeditedEvent.{0}.PerceivedSeverity", i + 1);
                    if (String.Compare(name, strName, true) == 0)
                    {
                        structAlarm[i].PerceivedSeverity = value;
                        return true;
                    }
                    strName = string.Format("FaultMgmt.ExpeditedEvent.{0}.AdditionalText", i + 1);
                    if (String.Compare(name, strName, true) == 0)
                    {
                        structAlarm[i].AdditionalText = value;
                        return true;
                    }
                    strName = string.Format("FaultMgmt.ExpeditedEvent.{0}.AdditionalInformation", i + 1);
                    if (String.Compare(name, strName, true) == 0)
                    {
                        structAlarm[i].AdditionalInformation = value;
                        return true;
                    }
                }
                return false;
            }

            /// <summary>
            /// 遍历参数值表，更新数据库里AP状态及参数信息
            /// </summary>
            /// <param name="sn">AP的sn号</param>
            /// <param name="valueChangeNode">AP传过来的参数值表</param>
            private void CheckParameterList(String sn,List<XmlParameter> valueChangeNode)
            {
                structDeviceInfo deviceInfo = new structDeviceInfo();
                Boolean isChange = false;
       
                foreach (XmlParameter x in valueChangeNode)
                {
                    if (String.Compare(x.name, "DeviceInfo.SoftwareVersion", true) == 0)
                    {
                        deviceInfo.curVersion = x.value;
                        isChange = true;
                    }
                    else if (String.Compare(x.name, "DeviceInfo.HardwareVersion", true) == 0)
                    {
                        deviceInfo.type = x.value;
                        isChange = true;
                    }
                    else if (String.Compare(x.name, "Services.FAPService.1.CellConfig.LTE.RAN.Common.CellIdentity", true) == 0)
                    {
                        deviceInfo.cellId = x.value;
                        isChange = true;
                    }
                    else if (String.Compare(x.name, "Services.FAPService.1.CellConfig.LTE.RAN.RF.X_VENDOR_PHY_CELL_ID", true) == 0)
                    {
                        deviceInfo.pci = x.value;
                        isChange = true;
                    }
                    else if (String.Compare(x.name, "Services.FAPService.1.CellConfig.LTE.RAN.RF.EARFCNDL", true) == 0)
                    {
                        deviceInfo.earfcn = x.value;
                        isChange = true;
                    }
                    else if (String.Compare(x.name, "Services.FAPService.1.CellConfig.LTE.EPC.TAC", true) == 0)
                    {
                        deviceInfo.tac = x.value;
                        isChange = true;
                    }
                    else if (String.Compare(x.name, "DeviceInfo.X_VENDOR_ENODEB_STATUS.X_VENDOR_CELL_STATUS", true) == 0)
                    {
                        if (String.Compare(x.value, "active", true) == 0)
                            deviceInfo.s1Status = "online";
                        else
                            deviceInfo.s1Status = "offline";
                        isChange = true;
                    }
                }

                if (isChange)
                {
                    string str = string.Format("更新AP({0})状态或参数!!!",sn);
                    Log.WriteDebug(str);

                    int errCode = 0;
                    try
                    {
                        Log.WriteDebug("curVersion:(" + deviceInfo.curVersion + ")");
                        Log.WriteDebug("type:(" + deviceInfo.type + ")");
                        Log.WriteDebug("cellId:(" + deviceInfo.cellId + ")");
                        Log.WriteDebug("pci:(" + deviceInfo.pci + ")");
                        Log.WriteDebug("earfcn:(" + deviceInfo.earfcn + ")");
                        Log.WriteDebug("tac:(" + deviceInfo.tac + ")");
                        Log.WriteDebug("s1Status:(" + deviceInfo.s1Status + ")");

                        errCode = myDB.deviceinfo_record_update(sn, deviceInfo);
                        if (0 != errCode)
                        {
                            Log.WriteError(str + "出错。出错码：" + errCode);
                        }
                    }
                    catch (Exception e)
                    {
                        Log.WriteError("执行数据库函数（deviceinfo_record_update）出错。出错原因："
                            + e.Message.ToString());
                        errCode = -1;
                    }  
                }

            }

            /// <summary>
            /// 修改AP状态（4 Value Change 时调用）
            /// </summary>
            /// <param name="parameterStruct"> inform 消息传过来的参数</param>
            /// <returns></returns>
            private Boolean ChangeApDeviceInfo(XmlParameterStruct parameterStruct)
            {
                StructAlarm[] structAlarm = new StructAlarm[AlarmMaxNum];
                Boolean isAlarm = false;

                CheckParameterList(parameterStruct.xmlInform.SN, parameterStruct.xmlInform.ValueChange.valueChangeNode);
                foreach (XmlParameter x in parameterStruct.xmlInform.ValueChange.valueChangeNode)
                {
                    if (GetAlarmStruct(x.name,x.value,ref structAlarm))
                    {
                        isAlarm = true;
                    }                 
                }

                if (isAlarm)
                {
                    ChangeApDeviceStatus(parameterStruct.xmlInform.SN, structAlarm);
                }

                return true;
            }

            public override void onGet(HttpListenerRequest request, HttpListenerResponse response)
            {
                /*
                Console.WriteLine("GET:" + request.Url);
                byte[] buffer = XmlHandle.CreateInformResponseXmlFile();//Encoding.UTF8.GetBytes("OK");

                System.IO.Stream output = response.OutputStream;
                output.Write(buffer, 0, buffer.Length);
                // You must close the output stream.
                output.Close();
                //listener.Stop();
                */
                onPost(request, response);
            }

            /// <summary>
            /// 收到POST消息处理
            /// </summary>
            /// <param name="request">request消息结构</param>
            /// <param name="response">response消息结构</param>
            public override void onPost(HttpListenerRequest request, HttpListenerResponse response)
            {
                
                Console.WriteLine("POST:" + request.Url);

                String ip = request.RemoteEndPoint.Address.ToString();
                int port = request.RemoteEndPoint.Port;

                Log.WriteDebug("收到POST消息:IP=" + ip +";Port=" + port);
                try
                {
                    //空消息
                    if (!request.HasEntityBody)
                    {
                        Log.WriteDebug("收到POST消息，消息内容为空!");
                        byte[] xml = RecvEmptyMsgHandle(ip, port);
                        if (xml.Length <= 0)
                        {
                            response.ContentLength64 = 0; //回复空post
                            Log.WriteDebug("收到POST消息，消息内容为空!回复空消息");
                        }
                        else
                        {
                            response.OutputStream.Write(xml, 0, xml.Length);
                            Log.WriteDebug("收到POST消息，消息内容为空!回复消息:\n" + 
                                System.Text.Encoding.Default.GetString(xml));
                        }
                        //MessageBox.Show("yyyy:" + ip + ":" + port + "  " + getSnForSnIpPortNode(ip,port));
                    }
                    else
                    {
                        //接收POST参数
                        Stream stream = request.InputStream;
                        System.IO.StreamReader reader = new System.IO.StreamReader(stream, Encoding.UTF8);
                        //String body = reader.ReadToEnd();
                        XmlParameterStruct parameterStruct = new XmlParameterStruct();
                        parameterStruct = new XmlHandle().HandleRecvApMsg(reader.ReadToEnd());

                        Log.WriteDebug("收到POST消息，消息Method="+ parameterStruct.Method + "!");
                        if (String.Compare(parameterStruct.Method, RPCMethod.Inform, true) == 0)
                        {
                            ApConnHmsInfo connInfo = new ApConnHmsInfo(parameterStruct.xmlInform.SN);
                            connInfo.Ip = request.RemoteEndPoint.Address.ToString();
                            connInfo.Port = request.RemoteEndPoint.Port;
                            connInfo.EventCode = parameterStruct.xmlInform.EventCode;

                            Log.WriteDebug("收到Inform消息，SN = " + connInfo.Sn);
                            foreach (string eventcode in connInfo.EventCode)
                            {
                                if (string.IsNullOrEmpty(eventcode)) break;
                                Log.WriteDebug("消息EventCode = " + eventcode);
                            }
                            GlobalParameter.apConnHmsList.add(connInfo);
                            Log.WriteDebug("设置" + parameterStruct.xmlInform.SN + "为上线状态!");
                            if (!myDB.SetconnHSToOnLine(parameterStruct.xmlInform.SN))
                            {
                                Log.WriteError("设置" + parameterStruct.xmlInform.SN + "为上线状态失败!");
                            }
                            if (XmlHandle.GetEventInList(connInfo.EventCode, InformEventCode.BOOT))
                            {
                                Log.WriteDebug("更新AP(" + parameterStruct.xmlInform.SN +
                                    ")反向连接地址为:" + parameterStruct.xmlInform.ConnectionRequestURL);
                                int re = myDB.apconninfo_record_update(parameterStruct.xmlInform.SN,
                                    parameterStruct.xmlInform.ConnectionRequestURL,
                                    GlobalParameter.ConnectionRequestUsername,
                                    GlobalParameter.ConnectionRequestPassWd);
                                if (re != 0)
                                {
                                    Log.WriteError("更新AP反向连接地址出错，出错码：" + re);
                                }
                            }
                            if (XmlHandle.GetEventInList(connInfo.EventCode, InformEventCode.M_Reboot))
                            {
                                string str = String.Format("修改SN({0}),任务类型({1}),任务状态({2})！",
                                        connInfo.Sn, TaskType.RebootTask, TaskStatus.ReponseOk);
                                Log.WriteDebug(str);
                                if (!myDB.SetApTaskStatusBySN(connInfo.Sn, TaskType.RebootTask,
                                                TaskStatus.ReponseOk))
                                {
                                    Log.WriteError(str);
                                }
                            }
                            if (XmlHandle.GetEventInList(connInfo.EventCode, InformEventCode.VALUE_CHANGE))
                            {
                                ChangeApDeviceInfo(parameterStruct);
                            }
                            //if (XmlHandle.GetEventInList(connInfo.EventCode, InformEventCode.PERIODIC))
                            //{
                            //    CheckParameterList(connInfo.Sn, parameterStruct.parameterNode);
                            //}
                            byte[] res = XmlHandle.CreateInformResponseXmlFile();// Encoding.UTF8.GetBytes("OK");
                            response.OutputStream.Write(res, 0, res.Length);
                            Log.WriteDebug("收到Inform消息，回复消息：\n" + System.Text.Encoding.Default.GetString(res));
                        }
                        else
                        {
                            ApConnHmsInfo connInfo = GlobalParameter.apConnHmsList.getSnForconnList(ip, port);
                            if (connInfo != null)
                            {
                                if (String.Compare(parameterStruct.Method, RPCMethod.GetParameterValuesResponse, true) == 0)
                                {
                                    //去掉周期性查询
                                    if (String.Compare(parameterStruct.ID, PeriodicGetValue, true) != 0)
                                    {
                                        string str = String.Format("修改SN({0}),任务ID({1}),任务状态({2})！",
                                                connInfo.Sn, parameterStruct.ID, TaskStatus.ReponseOk);
                                        Log.WriteDebug(str);
                                        if (!myDB.SetStatusBySnId(parameterStruct.ID, connInfo.Sn,
                                           TaskStatus.ReponseOk))
                                        {
                                            Log.WriteError(str);
                                        }
                                    }
                                    Log.WriteDebug("更改数据库中AP的状态或参数值...");
                                    CheckParameterList(connInfo.Sn, parameterStruct.parameterNode);

                                    SaveParameterList(connInfo.Sn, parameterStruct.ID, parameterStruct.parameterNode);
                                }
                                else if (String.Compare(parameterStruct.Method, RPCMethod.SetParameterValuesResponse, true) == 0)
                                {
                                    //去掉1Boot的回复。
                                    if (String.Compare(parameterStruct.ID, XmlHandle.SetParameterValueFor1Boot, true) != 0)
                                    {
                                        string str = String.Format("修改SN({0}),任务ID({1}),任务状态({2})！",
                                                    connInfo.Sn, parameterStruct.ID, TaskStatus.ReponseOk);
                                        Log.WriteDebug(str);
                                        if (!myDB.SetStatusBySnId(parameterStruct.ID, connInfo.Sn,
                                            TaskStatus.ReponseOk))
                                        {
                                            Log.WriteError(str);
                                        }
                                    }
                                }
                                else if (String.Compare(parameterStruct.Method, RPCMethod.TransferComplete, true) == 0)
                                {
                                    TaskStatus status = TaskStatus.TaskNull;
                                    if (parameterStruct.transferComplete.FaultCode == 0)  //AP返回成功
                                    {
                                        status = TaskStatus.ReponseOk;
                                        //设置LOG上传成功标志
                                        string taskType = parameterStruct.transferComplete.CommandKey.Substring(0, TaskType.GetLogTask.ToString().Length);
                                        if (taskType.Equals(TaskType.GetLogTask.ToString()))
                                        {
                                            string tmpstr = String.Format("修改SN({0}),任务ID({1}),AP LOG表状态为可用！",
                                                connInfo.Sn, parameterStruct.transferComplete.CommandKey);
                                            Log.WriteDebug(tmpstr);
                                            if (0 != myDB.aploginfo_record_update(
                                                connInfo.Sn, parameterStruct.transferComplete.CommandKey, 1))
                                            {
                                                Log.WriteError(tmpstr);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        status = TaskStatus.ReponseFail;
                                    }
                                    string str = String.Format("修改SN({0}),任务ID({1}),任务状态({2})！",
                                                connInfo.Sn, parameterStruct.transferComplete.CommandKey, status);
                                    Log.WriteDebug(str);
                                    if (!myDB.SetStatusBySnId(parameterStruct.transferComplete.CommandKey,
                                        connInfo.Sn, status))
                                    {
                                        Log.WriteError(str);
                                    }

                                    byte[] res = XmlHandle.CreateTransferCompleteResponseXmlFile();// Encoding.UTF8.GetBytes("OK");
                                    response.OutputStream.Write(res, 0, res.Length);
                                    Log.WriteDebug("收到TransferComplete消息，回复消息：\n" + System.Text.Encoding.Default.GetString(res));
                                    return;
                                }
                                else if (String.Compare(parameterStruct.Method, RPCMethod.RebootResponse, true) == 0)
                                {
                                    string str = String.Format("修改SN({0}),任务ID({1}),任务状态({2})！",
                                                connInfo.Sn, parameterStruct.ID, TaskStatus.SendTask);
                                    Log.WriteDebug(str);
                                    if (!myDB.SetStatusBySnId(parameterStruct.ID, connInfo.Sn,
                                        TaskStatus.SendTask))
                                    {
                                        Log.WriteError(str);
                                    }
                                }
                                else if (String.Compare(parameterStruct.Method, RPCMethod.Fault, true) == 0)
                                {
                                    string str = String.Format("修改SN({0}),任务ID({1}),任务状态({2})！",
                                               connInfo.Sn, parameterStruct.ID, TaskStatus.ReponseFail);
                                    Log.WriteDebug(str);
                                    if (!myDB.SetStatusBySnId(parameterStruct.ID, connInfo.Sn,
                                        TaskStatus.ReponseFail))
                                    {
                                        Log.WriteError(str);
                                    }
                                }
                            }

                            //如果有其它任务，下发其它任务，否则回复空
                            byte[] xml = NextSendMsgHandle(ip, port);
                            if (xml.Length <= 0)
                            {
                                response.ContentLength64 = 0; //回复空post
                            }
                            else
                            {
                                response.OutputStream.Write(xml, 0, xml.Length);
                            }
                        }
                    }
                }
                catch(Exception e)
                {
                    Log.WriteError("处理收到的消息(" + ip + ":" + port + ")出错。");
                    Log.WriteError("出错原因：" + e.Message.ToString());
                }
            }

            


        }
    }
}
