
/***************************************************************************************
  
    一、添加数据库的各种接口
                                            jianbinbz 2017-12-18
  
    二、添加Log操作类Logger
    
            用于将各种Log或出错信息异步的输出到MessageBox，文件或两者
            当输出到文件时，放在如下路径：Application.StartupPath + @"\logInfo\20xx年\xx月"	 
            即Log按年和月分开存储，另外，Log能记录出错的文件和行数，上级调用时的文件和行数。
        
        （1）配置Log输出的格式：
	         private enum LogOutType       //日志输出类型
	         {
	             MessageBoxOnly = 0,       //仅MessageBox输出
	             FileOnly = 1,             //仅日志输出
	             MessageBoxAndFile = 2,    //MessageBox输出+日志输出
	         }
    	        
	         //
	         // 在这里进行配置输出的格式
	         //
	         private static readonly LogOutType logOutType = LogOutType.FileOnly;        
        
        （2）记录普通信息
     	     Logger.Trace(Logger.__INFO__, "在此输入要记录的信息");
                  
        （3）记录出错信息
     	     Logger.Trace(e);  //其中e为Exception类型
                             
                                               jianbinbz 2018-01-23
 
      三、完善数据库的各种接口
                        
                                               jianbinbz 2018-01-29
 
      四、添加表parameterinfo和aploginfo的各种接口
                        
                                               jianbinbz 2018-02-02
  
 ***************************************************************************************/


using System;
using System.Collections.Generic;
using System.Text;
using MySql.Data.MySqlClient;
using System.Data;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;
using System.Collections;

namespace httpServer
{
    public enum TaskStatus    //任务状态
    {
        TaskNull = 0,                 //0:无该任务；
        NoSendReqst = 1,              //1:未下发请求；
        SendReqst = 2,                //2:已下发请示;
        SendTask = 3,                 //3:已下发任务;
        ReponseOk = 4,　　              //4:已收到回应,状态为成功;
        ReponseFail = 5,　              //5:已收到回应，状态为失败;
        TimeOut = 6                   //6:任务超时
    }

    public enum TaskType   //任务类型
    {
        TaskNull = 0,                 //0:无任务；
        UpgradTask = 1,               //1:升级任务；
        GetLogTask = 2,               //2:获取Log任务；
        GetParameterValuesTask = 3,   //3:获取参数值
        SetParameterValuesTask = 4,   //4:设置参数值
        RebootTask = 5                //5:重启AP任务
    }


    /// <summary>
    /// 用于更新deviceinfo的结构体
    /// </summary>
    public struct structDeviceInfo
    {
        public string bsName;
        public string ipAddr;
        public string type;
        public string s1Status;
        public string connHS;
        public string tac;
        public string enbId;
        public string cellId;
        public string earfcn;
        public string pci;
        public string updateMode;
        public string curVersion;
        public string curWarnCnt;
        public string onoffLineTime;
        public string aliasName;
        public string des;

        public string province;
        public string city;
        public string district;
        public string street;
    };


    /// <summary>
    /// 用于查询deviceinfo的结构体
    /// （1） 各个字段的值表示过滤或包含的信息
    /// （2） 字段为null或""时表示不过滤该字段
    /// </summary>
    public struct structDeviceInfoQuery
    {
        public string bsName;
        public string sn;
        public string ipAddr;
        public string type;
        public string s1Status;
        public string connHS;
        public string tac;
        public string enbId;
        public string cellId;
        public string earfcn;
        public string pci;
        public string updateMode;
        public string curVersion;
        public string curWarnCnt;
	
        /// <summary>
        /// 上下线时间的起始时间，如'2016-12-23 12:34:56' 
        /// 不过滤时，传入null或者""
        /// </summary>
        public string onoffLineTime_StartTime; 
 
        /// <summary>
        /// 上下线时间的结束时间，如'2018-06-23 12:34:56' 
        /// 不过滤时，传入null或者""
        /// </summary>
        public string onoffLineTime_EndTime;

        public string aliasName;
        public string des;

        public string province;
        public string city;
        public string district;
        public string street;
    };


    /// <summary>
    /// 用于查询versioninfo的结构体
    /// （1） 各个字段的值表示过滤或包含的信息
    /// （2） string类型字段为null或""时表示不过滤该字段
    /// （3） UInt32类型字段为0表示不过滤该字段
    /// </summary>
    public struct structVersionInfoQuery
    {
        public string versionNo;
        public string uploadUser;

        /// <summary>
        /// 上传时间的起始时间，如'2016-12-23 12:34:56' 
        /// 不过滤时，传入null或者""
        /// </summary>
        public string uploadTime_StartTime;

        /// <summary>
        /// 上传时间的结束时间，如'2018-06-23 12:34:56' 
        /// 不过滤时，传入null或者""
        /// </summary>
        public string uploadTime_EndTime;

        public string applicableDevice;
        public string patchName;

        /// <summary>
        /// 文件大小最小值
        /// 不过滤时，传入0
        /// </summary>
        public UInt32 fileSize_Start;

        /// <summary>
        /// 文件大小最大值
        /// 不过滤时，传入0
        /// </summary>
        public UInt32 fileSize_End;

        public string des;
    };


    public sealed class Logger
    {
        #region 定义

        private const string trace_exception = "\r\n-------------------- TRACE_INFO [{0}] --------------------";

        private static DateTime currentLogFileDate = DateTime.Now;

        private static TextWriterTraceListener twtl;

        private static string logRootDirectory = @"C:\Apache24\htdocs\server";
        //private static string logRootDirectory = Application.StartupPath + @"\logInfo";

        private static string logSubDirectory;
        private static string outString;

        private enum LogOutType       //日志输出类型
        {
            MessageBoxOnly = 0,       //仅MessageBox输出
            FileOnly = 1,             //仅日志输出
            MessageBoxAndFile = 2,    //MessageBox输出+日志输出
        }

        private enum FileFlushType    //日志文件刷新类型
        {
            Standard = 0,             //标准刷新
            RightNow = 1,             //立即刷新
        }


        /// <summary>
        /// 配置记录输出类型
        /// 可以修改成从配置文件读取
        /// </summary>
        private static readonly LogOutType logOutType = LogOutType.FileOnly;

        /// <summary>
        /// 配置记录文件的刷新类型
        /// 可以修改成从配置文件读取
        /// </summary>
        private static readonly FileFlushType fileFlushType = FileFlushType.RightNow;


        #endregion

        #region 属性

        private static string GetLogFullPath
        {
            get
            {
                return string.Concat(logRootDirectory, '\\', string.Concat(logSubDirectory, @"\log", currentLogFileDate.ToString("yyyy-MM-dd"), ".txt"));
            }
        }

        /// <summary>
        /// 跟踪输出日志文件
        /// </summary>
        private static TextWriterTraceListener TWTL
        {
            get
            {
                if (twtl == null)
                {
                    if (string.IsNullOrEmpty(logSubDirectory))
                    {
                        BuiderDir(DateTime.Now);
                    }
                    else
                    {
                        string logPath = GetLogFullPath;
                        if (!Directory.Exists(Path.GetDirectoryName(logPath)))
                        {
                            BuiderDir(DateTime.Now);
                        }
                    }

                    twtl = new TextWriterTraceListener(GetLogFullPath);
                }

                return twtl;
            }
        }

        /// <summary>
        /// 是否已经连接上数据库的标识
        /// </summary>
        public static string __INFO__
        {
            //get { return GetAuxiliaryInfo(); }            

            get
            {
                string retStr = null;
                System.Diagnostics.StackTrace st = new System.Diagnostics.StackTrace(1, true);

                //retStr += "文件名 -> " + st.GetFrame(0).GetFileName().ToString() + "\r\n";
                //retStr += "函数名 -> " + st.GetFrame(0).GetMethod().ToString() + "\r\n";
                //retStr += "所在行 -> " + st.GetFrame(0).GetFileLineNumber().ToString() + "\r\n";
                //retStr += "所在列 -> " + st.GetFrame(0).GetFileColumnNumber().ToString() + "\r\n";

                retStr += "文件名0 -> " + st.GetFrame(1).GetFileName().ToString() + "\r\n";
                retStr += "函数名0 -> " + st.GetFrame(1).GetMethod().ToString() + "\r\n";
                retStr += "所在行0 -> " + st.GetFrame(1).GetFileLineNumber().ToString() + "\r\n";

                retStr += "文件名1 -> " + st.GetFrame(0).GetFileName().ToString() + "\r\n";
                retStr += "函数名1 -> " + st.GetFrame(0).GetMethod().ToString() + "\r\n";
                retStr += "所在行1 -> " + st.GetFrame(0).GetFileLineNumber().ToString() + "\r\n";

                return retStr;
            }
        }

        #endregion

        #region 构造

        static Logger()
        {
            switch (logOutType)
            {
                case LogOutType.MessageBoxOnly:
                    {
                        break;
                    }
                case LogOutType.FileOnly:
                    {
                        System.Diagnostics.Trace.AutoFlush = true;
                        System.Diagnostics.Trace.Listeners.Clear();
                        System.Diagnostics.Trace.Listeners.Add(TWTL);
                        break;
                    }
                case LogOutType.MessageBoxAndFile:
                    {
                        System.Diagnostics.Trace.AutoFlush = true;
                        System.Diagnostics.Trace.Listeners.Clear();
                        System.Diagnostics.Trace.Listeners.Add(TWTL);
                        break;
                    }
            }
        }

        #endregion

        #region 方法

        #region trace

        public static void Trace(Exception ex)
        {
            new AsyncLogException(BeginTraceError).BeginInvoke(ex, null, null);
        }

        public static void Trace(string auxiliaryInfo, string logInfo)
        {
            new AsyncLogString(BeginTraceError).BeginInvoke(auxiliaryInfo, logInfo, null, null);
        }

        #endregion

        #region delegate

        private delegate void AsyncLogException(Exception ex);

        private delegate void AsyncLogString(string auxiliaryInfo, string logInfo);

        private static void BeginTraceError(Exception ex)
        {
            outString = string.Format("1 -> {0} {1}\r\n2 -> {2}\r\n3 -> Source:{3}", ex.GetType().Name, ex.Message, ex.StackTrace.Trim(), ex.Source);

            switch (logOutType)
            {
                case LogOutType.MessageBoxOnly:
                    {
                        MessageBox.Show(outString);
                        break;
                    }
                case LogOutType.FileOnly:
                    {
                        if (null != ex)
                        {
                            StrategyLog();

                            System.Diagnostics.Trace.WriteLine(string.Format(trace_exception, DateTime.Now));

                            while (null != ex)
                            {
                                System.Diagnostics.Trace.WriteLine(outString);
                                ex = ex.InnerException;
                            }

                            if (fileFlushType == FileFlushType.RightNow)
                            {
                                System.Diagnostics.Trace.Close();
                            }
                        }

                        break;
                    }
                case LogOutType.MessageBoxAndFile:
                    {
                        MessageBox.Show(string.Format(outString));

                        if (null != ex)
                        {
                            StrategyLog();

                            System.Diagnostics.Trace.WriteLine(string.Format(trace_exception, DateTime.Now));

                            while (null != ex)
                            {
                                System.Diagnostics.Trace.WriteLine(string.Format(outString));
                                ex = ex.InnerException;
                            }

                            if (fileFlushType == FileFlushType.RightNow)
                            {
                                System.Diagnostics.Trace.Close();
                            }
                        }

                        break;
                    }
            }
        }

        private static void BeginTraceError(string auxiliaryInfo, string logInfo)
        {
            if (string.IsNullOrEmpty(auxiliaryInfo) || string.IsNullOrEmpty(logInfo))
            {
                MessageBox.Show("参数非法！");
            }

            switch (logOutType)
            {
                case LogOutType.MessageBoxOnly:
                    {
                        MessageBox.Show(auxiliaryInfo + logInfo);
                        break;
                    }
                case LogOutType.FileOnly:
                    {
                        //检测日志日期
                        StrategyLog();

                        //输出日志头
                        System.Diagnostics.Trace.WriteLine(string.Format(trace_exception, DateTime.Now));
                        System.Diagnostics.Trace.WriteLine(auxiliaryInfo + logInfo);


                        if (fileFlushType == FileFlushType.RightNow)
                        {
                            System.Diagnostics.Trace.Close();
                        }

                        break;
                    }
                case LogOutType.MessageBoxAndFile:
                    {
                        MessageBox.Show(auxiliaryInfo + logInfo);

                        //检测日志日期
                        StrategyLog();

                        //输出日志头
                        System.Diagnostics.Trace.WriteLine(string.Format(trace_exception, DateTime.Now));
                        System.Diagnostics.Trace.WriteLine(auxiliaryInfo + logInfo);

                        if (fileFlushType == FileFlushType.RightNow)
                        {
                            System.Diagnostics.Trace.Close();
                        }

                        break;
                    }
            }
        }

        public static void setLogRootDirectory(string directory)
        {
            logRootDirectory = directory;
        }

        #endregion

        #region helper

        private static string GetAuxiliaryInfo()
        {
            string retStr = null;
            System.Diagnostics.StackTrace st = new System.Diagnostics.StackTrace(1, true);

            retStr += "文件名 -> " + st.GetFrame(0).GetFileName().ToString() + "\r\n";
            retStr += "函数名 -> " + st.GetFrame(0).GetMethod().ToString() + "\r\n";
            retStr += "所在行 -> " + st.GetFrame(0).GetFileLineNumber().ToString() + "\r\n";
            retStr += "所在列 -> " + st.GetFrame(0).GetFileColumnNumber().ToString() + "\r\n";

            return retStr;
        }

        private static void StrategyLog()
        {
            //判断日志日期
            if (DateTime.Compare(DateTime.Now.Date, currentLogFileDate.Date) != 0)
            {
                DateTime currentDate = DateTime.Now.Date;

                //生成子目录
                BuiderDir(currentDate);

                //更新当前日志日期
                currentLogFileDate = currentDate;

                System.Diagnostics.Trace.Flush();

                //更改输出
                if (twtl != null)
                {
                    System.Diagnostics.Trace.Listeners.Remove(twtl);
                }

                System.Diagnostics.Trace.Listeners.Add(TWTL);
            }
        }

        private static void BuiderDir(DateTime currentDate)
        {
            int year = currentDate.Year;
            int month = currentDate.Month;

            //年/月
            string subdir = string.Concat(string.Format("{0:D4}年", year), '\\', string.Format("{0:D2}月", month));
            string path = Path.Combine(logRootDirectory, subdir);

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            logSubDirectory = subdir;
        }

        #endregion

        #endregion
    }

    public class MySqlDbHelper
    {
        #region 定义

        private MySqlConnection myDbConn;

        private string server;
        private string database;
        private string uid;
        private string password;
        private string port;
        private bool myDbConnFlag = false;        

        #endregion

        #region 属性

        /// <summary>
        /// 是否已经连接上数据库的标识
        /// </summary>
        public bool MyDbConnFlag
        {
            get 
            { 
                return myDbConnFlag; 
            }

            //set 
            //{ 
            //    myDbConnFlag = value; 
            //}
        }

        #endregion

        #region 构造函数

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="conString">连接数据库的字符串</param>
        public MySqlDbHelper(string conString)
        {
            myDbConn = new MySqlConnection(conString);
            OpenDbConn();
        }


        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="server">数据库所在机器的IP地址</param>
        /// <param name="database">数据库名称</param>
        /// <param name="uid">用户名</param>
        /// <param name="password">密码</param>
        /// <param name="port">端口</param>
        public MySqlDbHelper(string server, string database, string uid, string password, string port)
        {
            this.server = server;
            this.uid = uid;
            this.password = password;
            this.port = port;
            this.database = database;

            string conString = "Data Source=" + server + ";" + "port=" + port + ";" + "Database=" + database + ";" + "User Id=" + uid + ";" + "Password=" + password + ";" + "CharSet=utf8";             
            myDbConn = new MySqlConnection(conString);

            OpenDbConn();
        }

        /// <summary>
        /// Default Constructor
        /// </summary>
        public MySqlDbHelper() : this("172.17.8.130", "hsdatabase", "root", "root", "3306")
        {
        }

        #endregion

        #region 打开和关闭

        /// <summary>
        /// open connection to database
        /// </summary>
        /// <returns>
        /// true  : 成功
        /// false ：失败
        /// </returns>
        public bool OpenDbConn()
        {
            try
            {
                myDbConn.Open();
                myDbConnFlag = true;
                return true;
            }
            catch (MySqlException e)
            {
                Logger.Trace(e);
                return false;
            }
        }

        /// <summary>
        /// Close connection
        /// </summary>
        /// <returns>
        /// true  : 成功
        /// false ：失败
        /// </returns>
        public bool CloseDbConn()
        {
            try
            {
                myDbConn.Close();
                myDbConnFlag = false;
                return true;
            }
            catch (MySqlException e)
            {
                Logger.Trace(e);
                return false;
            }
        }

        #endregion

        #region 获取表和列的名称


        /// <summary>
        /// 获取数据库中所有的表名称
        /// </summary>
        /// <returns>
        /// 成功 ： 数据库中所有表名的列表
        /// 失败 ： null
        /// </returns>
        public List<string> get_all_tableName()
        {
            List<string> retNameList = new List<string>();

            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return null;
            }

            DataTable tbName = myDbConn.GetSchema("Tables");

            if (tbName.Columns.Contains("TABLE_NAME"))
            {
                foreach (DataRow dr in tbName.Rows)
                {
                    retNameList.Add((string)dr["TABLE_NAME"]);
                }
            }

            return retNameList;
        }

        /// <summary>
        /// 获取某个表中的所有列
        /// </summary>
        /// <param name="tableName">要获取的表名称</param>
        /// <returns>
        /// 成功 ： 返回tableName中所有的列名称
        /// 失败 :  null
        /// </returns>
        public List<string> get_all_columnName(string tableName)
        {
            List<string> columnName = new List<string>();

            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return null;
            }

            if (string.IsNullOrEmpty(tableName))
            {
                Logger.Trace(Logger.__INFO__, "tableName is null.");
                return null;
            }

            string sql = string.Format("show columns from {0};", tableName);            
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            columnName.Add(dr[0].ToString());
                        }
                        dr.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return null;
            }

            return columnName;
        }

        public DataTable GetSchema(string str)
        {
            return myDbConn.GetSchema(str);
        }

        public DataTable GetSchema(string str, string[] restri)
        {
            return myDbConn.GetSchema(str, restri);
        }

        #endregion
       
        #region 01 - addressinfo操作

        /// <summary>
        /// 判断对应的sn是否存在addressinfo表中
        /// </summary>
        /// <param name="sn">AP的SN号</param>
        /// <returns> 
        ///   -1 ：数据库尚未打开
        ///   -2 ：参数有误
        ///   -4 ：数据库操作失败 
        ///    0 : 不存在
        ///    1 ：存在
        /// </returns>
        public int addressinfo_record_exist(string sn)
        {
            UInt32 cnt = 0;

            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }

            if (string.IsNullOrEmpty(sn))
            {
                Logger.Trace(Logger.__INFO__, "sn参数为空");
                return -2;
            }

            if (sn.Length > 32)
            {
                Logger.Trace(Logger.__INFO__, "sn参数长度有误");
                return -2;
            }

            string sql = string.Format("select count(*) from addressinfo where sn = '{0}'", sn);
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            cnt = Convert.ToUInt32(dr[0]);
                        }
                        dr.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            if (cnt <= 0)
            {
                //SN不存在
                return 0;
            }
            else
            {
                //SN存在
                return 1;
            }
        }

        /// <summary>
        /// 插入记录到addressinfo表中
        /// 其他字段用接口addressinfo_record_update进行更新
        /// </summary>
        /// <param name="sn">AP的SN号</param>
        /// <returns>
        ///   -1 ：数据库尚未打开
        ///   -2 ：参数有误
        ///   -3 ：记录已经存在
        ///   -4 ：数据库操作失败 
        ///    0 : 插入成功 
        /// </returns>
        public int addressinfo_record_insert(string sn)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }

            if (string.IsNullOrEmpty(sn))
            {
                Logger.Trace(Logger.__INFO__, "sn参数为空");
                return -2;
            }

            if (sn.Length > 32)
            {
                Logger.Trace(Logger.__INFO__, "sn参数长度有误");
                return -2;
            }


            //检查用户是否存在
            if (1 == addressinfo_record_exist(sn))
            {
                Logger.Trace(Logger.__INFO__, "sn记录已经存在");
                return -3;
            }

            string sql = string.Format("insert into addressinfo values(NULL,NULL,NULL,NULL,NULL,'{0}')", sn);
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    if (cmd.ExecuteNonQuery() < 0)
                    {
                        Logger.Trace(Logger.__INFO__, sql);
                        return -4;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            return 0;
        }

        /// <summary>
        /// 通过SN，更新省份，城市，区和街道
        /// </summary>
        /// <param name="province">省份，null或者""时表示不更新</param>
        /// <param name="city">城市，null或者""时表示不更新</param>
        /// <param name="district">区，null或者""时表示不更新</param>
        /// <param name="street">街道，null或者""时表示不更新</param>
        /// <param name="sn"></param>
        /// <returns>
        ///  返回值 ：
        /// -1 ：数据库尚未打开
        /// -2 ：参数有误
        /// -3 ：SN不存在
        /// -4 ：数据库操作失败 
        ///  0 : 更新成功  
        /// </returns>
        public int addressinfo_record_update(string province, string city, string district, string street, string sn)
        {
            string sql = "";
            string sqlSub = "";

            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }

            if (string.IsNullOrEmpty(sn))
            {
                Logger.Trace(Logger.__INFO__, "sn参数为空");
                return -2;
            }

            if (sn.Length > 32)
            {
                Logger.Trace(Logger.__INFO__, "sn参数长度有误");
                return -2;
            }

            if (!string.IsNullOrEmpty(province))
            {
                if (province.Length > 64)
                {
                    Logger.Trace(Logger.__INFO__, "province参数长度有误");
                    return -2;
                }
                else
                {
                    sqlSub += string.Format("province = '{0}',", province);
                }
            }

            if (!string.IsNullOrEmpty(city))
            {
                if (city.Length > 64)
                {
                    Logger.Trace(Logger.__INFO__, "city参数长度有误");
                    return -2;
                }
                else
                {
                    sqlSub += string.Format("city = '{0}',", city);
                }
            }

            if (!string.IsNullOrEmpty(district))
            {
                if (district.Length > 64)
                {
                    Logger.Trace(Logger.__INFO__, "district参数长度有误");
                    return -2;
                }
                else
                {
                    sqlSub += string.Format("district = '{0}',", district);
                }
            }

            if (!string.IsNullOrEmpty(street))
            {
                if (street.Length > 128)
                {
                    Logger.Trace(Logger.__INFO__, "street参数长度有误");
                    return -2;
                }
                else
                {
                    sqlSub += string.Format("street = '{0}',", street);
                }
            }

            if (sqlSub != "")
            {
                //去掉最后一个字符
                sqlSub = sqlSub.Remove(sqlSub.Length - 1, 1);
            }
            else
            { 
                //不需要更新
                Logger.Trace(Logger.__INFO__, "addressinfo_record_updateb,无需更新");
                return 0;
            }

            if (0 == addressinfo_record_exist(sn))
            {
                Logger.Trace(Logger.__INFO__, "sn记录不存在");
                return -3;
            }

            sql = string.Format("update addressinfo set {0} where sn = '{1}'",sqlSub,sn);

            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    if (cmd.ExecuteNonQuery() < 0)
                    {
                        Logger.Trace(Logger.__INFO__, sql);
                        return -4;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            return 0;
        }

        /// <summary>
        /// 在addressinfo表中删除指定的SN
        /// </summary>
        /// <param name="sn">AP的SN号</param>
        /// <returns>
        /// -1 ：数据库尚未打开
        /// -2 ：参数有误
        /// -3 ：用户不存在
        /// -4 ：数据库操作失败 
        ///  0 : 删除成功 
        /// </returns>
        public int addressinfo_record_delete(string sn)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }

            if (string.IsNullOrEmpty(sn))
            {
                Logger.Trace(Logger.__INFO__, "sn参数为空");
                return -2;
            }

            if (sn.Length > 32)
            {
                Logger.Trace(Logger.__INFO__, "sn参数长度有误");
                return -2;
            }
         
            if (0 == addressinfo_record_exist(sn))
            {
                Logger.Trace(Logger.__INFO__, "sn记录不存在");
                return -3;
            }

            string sql = string.Format("delete from addressinfo where sn = '{0}'", sn);
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    if (cmd.ExecuteNonQuery() < 0)
                    {
                        Logger.Trace(Logger.__INFO__, sql);
                        return -4;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            return 0;
        }

        /// <summary>
        /// 获取addressinfo表中的各条记录
        /// </summary>
        /// <param name="dt">
        /// 返回的DataTable，包含的列为：id,province,city,district,street,sn
        /// </param>
        /// <returns>
        /// -1 ：数据库尚未打开
        /// -4 ：数据库操作失败 
        ///  0 : 查询成功 
        /// </returns>
        public int addressinfo_record_entity_get(ref DataTable dt)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }

            dt = new DataTable("addressinfo");

            DataColumn column0 = new DataColumn();
            column0.DataType = System.Type.GetType("System.Int32");
            column0.ColumnName = "id";

            DataColumn column1 = new DataColumn();
            column1.DataType = System.Type.GetType("System.String");
            column1.ColumnName = "province";

            DataColumn column2 = new DataColumn();
            column2.DataType = System.Type.GetType("System.String");
            column2.ColumnName = "city";

            DataColumn column3 = new DataColumn();
            column3.DataType = System.Type.GetType("System.String");
            column3.ColumnName = "district";

            DataColumn column4 = new DataColumn();
            column4.DataType = System.Type.GetType("System.String");
            column4.ColumnName = "street";

            DataColumn column5 = new DataColumn();
            column5.DataType = System.Type.GetType("System.String");
            column5.ColumnName = "sn";

            dt.Columns.Add(column0);
            dt.Columns.Add(column1);
            dt.Columns.Add(column2);
            dt.Columns.Add(column3);
            dt.Columns.Add(column4);
            dt.Columns.Add(column5);

            string sql = string.Format("select id,province,city,district,street,sn from addressinfo");
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            DataRow row = dt.NewRow();

                            row[0] = Convert.ToInt32(dr[0]);
                            row[1] = dr[1].ToString();
                            row[2] = dr[2].ToString();
                            row[3] = dr[3].ToString();
                            row[4] = dr[4].ToString();
                            row[5] = dr[5].ToString();

                            dt.Rows.Add(row);
                        }
                        dr.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            return 0;
        }

        #endregion

        #region 02 - apaction操作

        /// <summary>
        /// 判读SN对应的记录是否在apaction表中
        /// </summary>
        /// <param name="sn">AP的SN号</param>
        /// <returns> 
        ///   -1 ：数据库尚未打开
        ///   -2 ：参数有误
        ///   -4 ：数据库操作失败 
        ///    0 : 不存在
        ///    1 ：存在
        /// </returns>
        public int apaction_record_exist(string sn)
        {
            UInt32 cnt = 0;

            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }

            if (string.IsNullOrEmpty(sn))
            {
                Logger.Trace(Logger.__INFO__, "sn参数为空");
                return -2;
            }

            if (sn.Length > 32)
            {
                Logger.Trace(Logger.__INFO__, "sn参数长度有误");
                return -2;
            }

            string sql = string.Format("select count(*) from apaction where sn = '{0}'", sn);
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            cnt = Convert.ToUInt32(dr[0]);
                        }
                        dr.Close();
                    }
                }                    
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            if (cnt <= 0)
            {
                //SN不存在
                return 0;
            }
            else
            {
                //SN存在
                return 1;
            }
        }


        /// <summary>
        /// 插入记录到apaction表中
        /// upgradeStatus,getLogStatus,getParStatus,
        /// setParStatus,rebootStatus都使用默认的0
        /// </summary>
        /// <param name="sn">AP的SN号</param>
        /// <returns> 
        ///   -1 ：数据库尚未打开
        ///   -2 ：参数有误
        ///   -3 ：记录已经存在
        ///   -4 ：数据库操作失败 
        ///    0 : 插入成功
        /// </returns>
        public int apaction_record_insert(string sn)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }

            if (string.IsNullOrEmpty(sn))
            {
                Logger.Trace(Logger.__INFO__, "sn参数为空");
                return -2;
            }

            if (sn.Length > 32)
            {
                Logger.Trace(Logger.__INFO__, "sn参数长度有误");
                return -2;
            }

            if (1 == apaction_record_exist(sn))
            {
                Logger.Trace(Logger.__INFO__, "sn记录已经存在");
                return -3;
            }

            string sql = string.Format("insert into apaction values('{0}',NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL)", sn);
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    if (cmd.ExecuteNonQuery() < 0)
                    {
                        Logger.Trace(Logger.__INFO__, sql);
                        return -4;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            return 0;
        }


        /// <summary>
        /// 在apaction表中删除指定的SN
        /// </summary>
        /// <param name="sn">AP的SN号</param>
        /// <returns>
        /// -1 ：数据库尚未打开
        /// -2 ：参数有误
        /// -3 ：用户不存在
        /// -4 ：数据库操作失败 
        ///  0 : 删除成功 
        /// </returns>
        public int apaction_record_delete(string sn)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }

            if (string.IsNullOrEmpty(sn))
            {
                Logger.Trace(Logger.__INFO__, "sn参数为空");
                return -2;
            }

            if (sn.Length > 32)
            {
                Logger.Trace(Logger.__INFO__, "sn参数长度有误");
                return -2;
            }

            if (0 == apaction_record_exist(sn))
            {
                Logger.Trace(Logger.__INFO__, "sn记录不存在");
                return -3;
            }

            string sql = string.Format("delete from apaction where sn = '{0}'", sn);
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    if (cmd.ExecuteNonQuery() < 0)
                    {
                        Logger.Trace(Logger.__INFO__, sql);
                        return -4;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            return 0;
        }

        /// <summary>
        /// 通过SN号更新apaction表中对应的id，type和status
        /// </summary>
        /// <param name="sn">AP的SN号</param>
        /// <param name="id">actiong对应的id名称</param>
        /// <param name="type">actiong对应的type</param>
        /// <param name="status">actiong对应的status</param>
        /// <returns> 
        ///   -1 ：数据库尚未打开
        ///   -2 ：参数有误
        ///   -3 ：记录不存在
        ///   -4 ：数据库操作失败 
        ///    0 : 更新成功
        /// </returns>
        public int apaction_record_update(string sn,string id,TaskType type,TaskStatus status)
        {
            string sql = "";

            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }

            if (string.IsNullOrEmpty(sn))
            {
                Logger.Trace(Logger.__INFO__, "sn参数为空");
                return -2;
            }

            if (sn.Length > 32)
            {
                Logger.Trace(Logger.__INFO__, "sn参数长度有误");
                return -2;
            }

            if (string.IsNullOrEmpty(id))
            {
                Logger.Trace(Logger.__INFO__, "id参数为空");
                return -2;
            }

            if (id.Length > 64)
            {
                Logger.Trace(Logger.__INFO__, "id参数长度有误");
                return -2;
            }

            if (0 == apaction_record_exist(sn))
            {
                Logger.Trace(Logger.__INFO__, "sn记录不存在");
                return -3;
            }

            switch (type)
            {
                case TaskType.TaskNull:
                    {                        
                        break;
                    }
                case TaskType.UpgradTask:
                {
                    if (status == TaskStatus.NoSendReqst)
                    {
                        sql = string.Format("update apaction set upgradeId = '{0}',upgradeStartTime = now(),upgradeStatus = '{1}' where sn = '{2}'", id, (int)status, sn);
                    }
                    else if ((status == TaskStatus.ReponseOk) || (status == TaskStatus.ReponseFail))
                    {
                        sql = string.Format("update apaction set upgradeId = '{0}',upgradeEndTime = now(),upgradeStatus = '{1}' where sn = '{2}'", id, (int)status, sn);
                    }
                    else
                    {
                        sql = string.Format("update apaction set upgradeId = '{0}',upgradeStatus = '{1}' where sn = '{2}'", id, (int)status, sn);
                    }                 

                    break;
                }
                case TaskType.GetLogTask:
                    {
                        if (status == TaskStatus.NoSendReqst)
                        {
                            sql = string.Format("update apaction set getLogId = '{0}',getLogStartTime = now(),getLogStatus = '{1}' where sn = '{2}'", id, (int)status, sn);
                        }
                        else if ((status == TaskStatus.ReponseOk) || (status == TaskStatus.ReponseFail))
                        {
                            sql = string.Format("update apaction set getLogId = '{0}',getLogEndTime = now(),getLogStatus = '{1}' where sn = '{2}'", id, (int)status, sn);
                        }
                        else
                        {
                            sql = string.Format("update apaction set getLogId = '{0}',getLogStatus = '{1}' where sn = '{2}'", id, (int)status, sn);
                        }    

                        break;
                    }
                case TaskType.GetParameterValuesTask:
                    {
                        if (status == TaskStatus.NoSendReqst)
                        {
                            sql = string.Format("update apaction set getParId = '{0}',getParStartTime = now(),getParStatus = '{1}' where sn = '{2}'", id, (int)status, sn);
                        }
                        else if ((status == TaskStatus.ReponseOk) || (status == TaskStatus.ReponseFail))
                        {
                            sql = string.Format("update apaction set getParId = '{0}',getParEndTime = now(),getParStatus = '{1}' where sn = '{2}'", id, (int)status, sn);
                        }
                        else
                        {
                            sql = string.Format("update apaction set getParId = '{0}',getParStatus = '{1}' where sn = '{2}'", id, (int)status, sn);
                        }    

                        break;
                    }
                case TaskType.SetParameterValuesTask:
                    {
                        if (status == TaskStatus.NoSendReqst)
                        {
                            sql = string.Format("update apaction set setParId = '{0}',setParStartTime = now(),setParStatus = '{1}' where sn = '{2}'", id, (int)status, sn);
                        }
                        else if ((status == TaskStatus.ReponseOk) || (status == TaskStatus.ReponseFail))
                        {
                            sql = string.Format("update apaction set setParId = '{0}',setParEndTime = now(),setParStatus = '{1}' where sn = '{2}'", id, (int)status, sn);
                        }
                        else
                        {
                            sql = string.Format("update apaction set setParId = '{0}',setParStatus = '{1}' where sn = '{2}'", id, (int)status, sn);
                        }    

                        break;
                    }
                case TaskType.RebootTask:
                    {
                        if (status == TaskStatus.NoSendReqst)
                        {
                            sql = string.Format("update apaction set rebootId = '{0}',rebootStartTime = now(),rebootStatus = '{1}' where sn = '{2}'", id, (int)status, sn);
                        }
                        else if ((status == TaskStatus.ReponseOk) || (status == TaskStatus.ReponseFail))
                        {
                            sql = string.Format("update apaction set rebootId = '{0}',rebootEndTime = now(),rebootStatus = '{1}' where sn = '{2}'", id, (int)status, sn);
                        }
                        else
                        {
                            sql = string.Format("update apaction set rebootId = '{0}',rebootStatus = '{1}' where sn = '{2}'", id, (int)status, sn);
                        }  

                        break;
                    }               
            }
            
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    if (cmd.ExecuteNonQuery() < 0)
                    {
                        Logger.Trace(Logger.__INFO__, sql);
                        return -4;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            return 0;
        }


        /// <summary>
        /// 从数据库获取是否有该ap的已下发请示。
        /// 然后返回对应的taskId,TaskType和xml命令
        /// </summary>
        /// <param name="taskType">成功时返回对应的TaskType</param>
        /// <param name="sn">要获取ap的sn号</param>
        /// <returns>
        /// 成功 ： 对应的XML内容
        /// 失败 ： null
        ///        失败包含数据库没连接，参数有误，数据库操作失败等 
        /// </returns>
        public byte[] GetTaskBySN(ref TaskType taskType,ref string taskId, string sn)
        {
            string id = null;
            string status = null;

            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return null;
            }

            if (string.IsNullOrEmpty(sn))
            {
                Logger.Trace(Logger.__INFO__, "sn参数为空");
                return null;
            }

            if (sn.Length > 32)
            {
                Logger.Trace(Logger.__INFO__, "sn参数长度有误");
                return null;
            }

            //获取是否有未下发请求
            string sql = string.Format("select upgradeId,upgradeStatus,getLogId," +
                "getLogStatus,getParId,getParStatus,setParId,setParStatus,rebootId,rebootStatus " +
                "from apaction where sn = '{0}'", sn);

            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            if ((TaskStatus)dr[1] == TaskStatus.SendReqst)
                            {
                                id = dr[0].ToString();
                                status = dr[1].ToString();
                                if (id.Length > 0)  break;
                            }

                            if ((TaskStatus)dr[3] == TaskStatus.SendReqst)
                            {
                                id = dr[2].ToString();
                                status = dr[3].ToString();
                                if (id.Length > 0) break;
                            }

                            if ((TaskStatus)dr[5] == TaskStatus.SendReqst)
                            {
                                id = dr[4].ToString();
                                status = dr[5].ToString();
                                if (id.Length > 0) break;
                            }

                            if ((TaskStatus)dr[7] == TaskStatus.SendReqst)
                            {
                                id = dr[6].ToString();
                                status = dr[7].ToString();
                                if (id.Length > 0) break;
                            }

                            if ((TaskStatus)dr[9] == TaskStatus.SendReqst)
                            {
                                id = dr[8].ToString();
                                status = dr[9].ToString();
                                if (id.Length > 0) break;
                            }
                        }
                        dr.Close();
                    }
                }                   
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return null;
            }


            if (id != null)
            {
                sql = string.Format("select actionId,actionType,actionXmlText from schedulerinfo where actionId = '{0}'", id);
                try
                {
                    using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                    {
                        using (MySqlDataReader dr = cmd.ExecuteReader())
                        {
                            byte[] byteArray = null;
                            if (dr.HasRows == true)
                            {
                                while (dr.Read())
                                {
                                    taskId = dr[0].ToString();
                                    taskType = (TaskType)Convert.ToInt32(dr[1]);
                                    byteArray = System.Text.Encoding.ASCII.GetBytes(dr[2].ToString());
                                }

                                dr.Close();
                                return byteArray;
                            }
                            else
                            {               
                                return null;
                            }
                        }
                    }                    
                }
                catch (Exception e)
                {
                    Logger.Trace(e);
                    return null;
                }
            }

            return null;
        }


        /// <summary>
        /// 将apTask表字段为upgradeStatus，getLogStatus，getParStatus,
        /// rebootStatus或者setParStatus，而且状态为TaskStatus.NoSendReqst的类型任务，
        /// 修改为TaskStatus.SendReqst状态。
        /// </summary>
        /// <param name="sn">要修改ap的sn号。</param>
        /// <returns>
        /// 成功 ： true
        /// 失败 ： false，
        ///         失败包含数据库没连接，参数有误，数据库操作失败,记录不存在等 
        /// </returns>
        public bool SetApTaskStatusToReqstBySN(string sn)
        {          
            bool ret = false;
            string sql = "";

            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return false;
            }

            if (string.IsNullOrEmpty(sn))
            {
                Logger.Trace(Logger.__INFO__, "sn参数为空");
                return false;
            }

            if (sn.Length > 32)
            {
                Logger.Trace(Logger.__INFO__, "sn参数长度有误");
                return false;
            }

            if (0 == apaction_record_exist(sn))
            {
                Logger.Trace(Logger.__INFO__, "sn记录不存在");
                return false;
            }

            List<string> listSts = new List<String>();
            listSts.Add("upgradeStatus");
            listSts.Add("getLogStatus");
            listSts.Add("getParStatus");
            listSts.Add("setParStatus");
            listSts.Add("rebootStatus");

            foreach (string str in listSts)
            {
                sql = string.Format("update apaction set {0}='{1}' where sn='{2}' and {3}='{4}'",
                    str, (int)TaskStatus.SendReqst, sn, str, (int)TaskStatus.NoSendReqst);
                try
                {
                    using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                    {
                        if (cmd.ExecuteNonQuery() > 0)
                        {
                            ret = true;
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.Trace(e);
                    return false;
                }
            }

            return ret;
        }


        /// <summary>
        ///  通过sn和TaskType获取对应的TaskStatus
        /// </summary>
        /// <param name="sn">AP的SN号</param>
        /// <param name="type">任务类型</param>
        /// <returns>
        /// 成功 ： 非TaskStatus.TaskNull
        /// 失败 ： TaskStatus.TaskNull
        ///        失败包含数据库没连接，参数有误，数据库操作失败,记录不存在等 
        /// </returns>
        public TaskStatus GetTaskStatusByApTask(string sn, TaskType type)
        {
            TaskStatus status = TaskStatus.TaskNull;
            string taskId = "";

            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return status;
            }

            if (string.IsNullOrEmpty(sn))
            {
                Logger.Trace(Logger.__INFO__, "sn参数为空");
                return status;
            }

            if (sn.Length > 32)
            {
                Logger.Trace(Logger.__INFO__, "sn参数长度有误");
                return status;
            }

            if (0 == apaction_record_exist(sn))
            {
                Logger.Trace(Logger.__INFO__, "sn记录不存在");
                return status;
            }

            if (type == TaskType.UpgradTask)
            {
                taskId = "upgradeStatus";
            }
            else if (type == TaskType.GetLogTask)
            {
                taskId = "getLogStatus";
            }
            else if (type == TaskType.GetParameterValuesTask)
            {
                taskId = "getParStatus";
            }
            else if (type == TaskType.SetParameterValuesTask)
            {
                taskId = "setParStatus";
            }
            else if (type == TaskType.RebootTask)
            {
                taskId = "rebootStatus";
            }
            else
            {              
                Logger.Trace(Logger.__INFO__, "暂不支持该任务类型");
                return status;
            }

            string sql = string.Format("select {0} from apaction where sn = '{1}'", taskId, sn);
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            status = (TaskStatus)int.Parse(dr[0].ToString());
                            break;
                        }
                        dr.Close();
                    }
                }               
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return status;
            }

            return status;
        }
      

        /// <summary>
        /// 从apTask表中获取有状态为
        /// TaskStatus.NoSendReqst，而且在线的所有sn号
        /// </summary>
        /// <returns>
        /// 成功 ： 非null，所有的SN列表
        /// 失败 ： null
        ///         
        /// </returns>
        public List<String> GetAllNoSendReqstSnByApTask()
        {
            List<String> listSN = new List<String>();

            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return null;
            }

            
            string sql = string.Format("SELECT a.sn FROM (select sn from apaction where upgradeStatus='{0}' or getLogStatus='{0}' or getParStatus='{0}' or setParStatus='{0}' or rebootStatus='{0}') AS a INNER JOIN deviceinfo As b ON a.sn=b.sn AND b.connHS='online'", (int)TaskStatus.NoSendReqst);		

            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            listSN.Add(dr[0].ToString());
                        }
                        dr.Close();
                    }
                }                 
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return null;
            }

            return listSN;
        }


        /// <summary>
        /// (1) 通过SN和type修改apTask表中的任务状态
        /// (2) 根据status的值修改schedulerinfo中的成功或失败数
        ///     A : ReponseOk = 4,    //4:已收到回应,状态为成功; 
        ///         此时successCount加一
        ///     B : ReponseFail = 5,  //5:已收到回应,状态为失败;
        ///         此时failCount加一
        /// 
        /// </summary>
        /// <param name="sn">要修改ap的sn号。</param>
        /// <param name="type">要修改状态的任务类型</param>
        /// <param name="status">修改后状态</param>
        /// <returns>
        /// 修改成功返回true
        /// 失败返回false
        /// </returns>
        public bool SetApTaskStatusBySN(string sn, TaskType type, TaskStatus status)
        {
            string actionStartTimeField = "";
            string actionEndTimeField = "";
            string actionStatusField = "";

            string actionIdValue = "";

            string sqlId = "";
            bool ret = false;

            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return false;
            }

            if (string.IsNullOrEmpty(sn))
            {
                Logger.Trace(Logger.__INFO__, "sn参数为空");
                return false;
            }

            if (sn.Length > 32)
            {
                Logger.Trace(Logger.__INFO__, "sn参数长度有误");
                return false;
            }

            //判断对应的记录是否存在
            if (0 == apaction_record_exist(sn))
            {
                Logger.Trace(Logger.__INFO__, "sn记录不存在");
                return false;
            }                       
  
            //获取action的status字段
            if (type == TaskType.UpgradTask)
            {
                actionStatusField = "upgradeStatus";
                actionStartTimeField = "upgradeStartTime";
                actionEndTimeField = "upgradeEndTime";

                sqlId = string.Format("select upgradeId from apaction where sn = '{0}'", sn);                                                                                                         
            }
            else if (type == TaskType.GetLogTask)
            {
                actionStatusField = "getLogStatus";
                actionStartTimeField = "getLogStartTime";
                actionEndTimeField = "getLogEndTime";

                sqlId = string.Format("select getLogId from apaction where sn = '{0}'", sn);  
            }
            else if (type == TaskType.GetParameterValuesTask)
            {
                actionStatusField = "getParStatus";
                actionStartTimeField = "getParStartTime";
                actionEndTimeField = "getParEndTime";

                sqlId = string.Format("select getParId from apaction where sn = '{0}'", sn);  
            }
            else if (type == TaskType.SetParameterValuesTask)
            {
                actionStatusField = "setParStatus";
                actionStartTimeField = "setParStartTime";
                actionEndTimeField = "setParEndTime";

                sqlId = string.Format("select setParId from apaction where sn = '{0}'", sn);  
            }
            else if (type == TaskType.RebootTask)
            {
                actionStatusField = "rebootStatus";
                actionStartTimeField = "rebootStartTime";
                actionEndTimeField = "rebootEndTime";

                sqlId = string.Format("select rebootId from apaction where sn = '{0}'", sn);  
            }
            else
            {
                Logger.Trace(Logger.__INFO__, "任务类型为:" + type + ",暂不支持该任务.");
                return false;
            }

                       
            string sql = "";
            if (status == TaskStatus.NoSendReqst)
            {
                sql = string.Format("update apaction set {0}='{1}',{2} = now() where sn='{3}'", actionStatusField, (int)status,actionStartTimeField, sn);
            }
            else if ((status == TaskStatus.ReponseOk) || (status == TaskStatus.ReponseFail))
            {
                sql = string.Format("update apaction set {0}='{1}',{2} = now() where sn='{3}'", actionStatusField, (int)status,actionEndTimeField, sn);
            }
            else
            {
                sql = string.Format("update apaction set {0}='{1}' where sn='{2}'", actionStatusField, (int)status, sn);
            }     
            
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    if (cmd.ExecuteNonQuery() > 0)
                    {
                        ret = true;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return false;
            }

            //更新schedulerinfo中的successCount或failCount
            if (true == ret)
            {
                try
                {
                    using (MySqlCommand cmd = new MySqlCommand(sqlId, myDbConn))
                    {
                        using (MySqlDataReader dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                actionIdValue = dr[0].ToString();
                                break;
                            }
                            dr.Close();
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.Trace(e);
                    return false;
                }

                if (status == TaskStatus.ReponseOk)
                {
                    UInt32 successCount = 0;
                    sql = string.Format("select successCount from schedulerinfo where actionId = '{0}'", actionIdValue);                                                                                                         

                    try
                    {
                        using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                        {
                            using (MySqlDataReader dr = cmd.ExecuteReader())
                            {
                                while (dr.Read())
                                {
                                    successCount = Convert.ToUInt32(dr[0]);
                                    break;
                                }
                                dr.Close();
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Trace(e);
                        return false;
                    }

                    sql = string.Format("update schedulerinfo set successCount = '{0}' where actionId = '{1}'", ++successCount, actionIdValue);
                    try
                    {
                        using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                        {
                            if (cmd.ExecuteNonQuery() > 0)
                            {
                                ret = true;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Trace(e);
                        return false;
                    }
                }


                if (status == TaskStatus.ReponseFail)
                {
                    UInt32 failCount = 0;
                    sql = string.Format("select failCount from schedulerinfo where actionId = '{0}'", actionIdValue);

                    try
                    {
                        using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                        {
                            using (MySqlDataReader dr = cmd.ExecuteReader())
                            {
                                while (dr.Read())
                                {
                                    failCount = Convert.ToUInt32(dr[0]);
                                    break;
                                }
                                dr.Close();
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Trace(e);
                        return false;
                    }

                    sql = string.Format("update schedulerinfo set failCount = '{0}' where actionId = '{1}'", ++failCount, actionIdValue);
                    try
                    {
                        using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                        {
                            if (cmd.ExecuteNonQuery() > 0)
                            {
                                ret = true;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Trace(e);
                        return false;
                    }
                }           
            }

            return ret;
        }


        /// <summary>
        /// (1) 通过SN和ID设置status
        /// (2) 根据status的值修改schedulerinfo中的成功或失败数
        ///     A : ReponseOk = 4,    //4:已收到回应,状态为成功; 
        ///         此时successCount加一
        ///     B : ReponseFail = 5,  //5:已收到回应,状态为失败;
        ///         此时failCount加一
        /// </summary>
        /// <param name="id">任务Id</param>
        /// <param name="sn">要修改ap的sn号</param>
        /// <param name="status">修改后的状态</param>
        /// <returns>
        /// 修改成功返回true
        /// 失败返回false
        /// </returns>
        public bool SetStatusBySnId(String id, String sn, TaskStatus status)
        {
            TaskType type = TaskType.TaskNull;
            string actionIdValue = id;
            bool ret = false;

            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return false;
            }

            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(sn))
            {
                Logger.Trace(Logger.__INFO__, "参数为空");
                return false;
            }

            if (sn.Length > 32 || id.Length > 128)
            {
                Logger.Trace(Logger.__INFO__, "参数长度有误");
                return false;
            }

            //判断对应的记录是否存在
            if (0 == apaction_record_exist(sn))
            {
                Logger.Trace(Logger.__INFO__, "SN对应的记录不存在");
                return false;
            }

            //判断id所对应的类型
            string sql = string.Format("select upgradeId,getLogId,getParId,setParId,rebootId from apaction where sn = '{0}'", sn);
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            if (dr[0].ToString() == id)
                            {
                                type = TaskType.UpgradTask;
                                break;
                            }

                            if (dr[1].ToString() == id)
                            {
                                type = TaskType.GetLogTask;
                                break;
                            }

                            if (dr[2].ToString() == id)
                            {
                                type = TaskType.GetParameterValuesTask;
                                break;
                            }

                            if (dr[3].ToString() == id)
                            {
                                type = TaskType.SetParameterValuesTask;
                                break;
                            }

                            if (dr[4].ToString() == id)
                            {
                                type = TaskType.RebootTask;
                                break;
                            }
                        }
                        dr.Close();
                    }
                }  
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return false;
            }

            switch (type)
            {
                case TaskType.TaskNull:
                    {
                        Logger.Trace(Logger.__INFO__, "无法通过id获取所对应的类型");
                        return false;
                    }
                case TaskType.UpgradTask:
                    {
                        if (status == TaskStatus.NoSendReqst)
                        {
                            sql = string.Format("update apaction set upgradeStartTime = now(),upgradeStatus = '{0}' where sn = '{1}'", (int)status, sn);
                        }
                        else if ((status == TaskStatus.ReponseOk) || (status == TaskStatus.ReponseFail))
                        {
                            sql = string.Format("update apaction set upgradeEndTime = now(),upgradeStatus = '{0}' where sn = '{1}'", (int)status, sn);
                        }
                        else
                        {
                            sql = string.Format("update apaction set upgradeStatus = '{0}' where sn = '{1}'", (int)status, sn);
                        }

                        break;
                    }
                case TaskType.GetLogTask:
                    {
                        if (status == TaskStatus.NoSendReqst)
                        {
                            sql = string.Format("update apaction set getLogStartTime = now(),getLogStatus = '{0}' where sn = '{1}'", (int)status, sn);
                        }
                        else if ((status == TaskStatus.ReponseOk) || (status == TaskStatus.ReponseFail))
                        {
                            sql = string.Format("update apaction set getLogEndTime = now(),getLogStatus = '{0}' where sn = '{1}'", (int)status, sn);
                        }
                        else
                        {
                            sql = string.Format("update apaction set getLogStatus = '{0}' where sn = '{1}'", (int)status, sn);
                        }

                        break;
                    }
                case TaskType.GetParameterValuesTask:
                    {
                        if (status == TaskStatus.NoSendReqst)
                        {
                            sql = string.Format("update apaction set getParStartTime = now(),getParStatus = '{0}' where sn = '{1}'", (int)status, sn);
                        }
                        else if ((status == TaskStatus.ReponseOk) || (status == TaskStatus.ReponseFail))
                        {
                            sql = string.Format("update apaction set getParEndTime = now(),getParStatus = '{0}' where sn = '{1}'", (int)status, sn);
                        }
                        else
                        {
                            sql = string.Format("update apaction set getParStatus = '{0}' where sn = '{1}'", (int)status, sn);
                        }

                        break;
                    }
                case TaskType.SetParameterValuesTask:
                    {
                        if (status == TaskStatus.NoSendReqst)
                        {
                            sql = string.Format("update apaction set setParStartTime = now(),setParStatus = '{0}' where sn = '{1}'", (int)status, sn);
                        }
                        else if ((status == TaskStatus.ReponseOk) || (status == TaskStatus.ReponseFail))
                        {
                            sql = string.Format("update apaction set setParEndTime = now(),setParStatus = '{0}' where sn = '{1}'", (int)status, sn);
                        }
                        else
                        {
                            sql = string.Format("update apaction set setParStatus = '{0}' where sn = '{1}'", (int)status, sn);
                        }

                        break;
                    }
                case TaskType.RebootTask:
                    {
                        if (status == TaskStatus.NoSendReqst)
                        {
                            sql = string.Format("update apaction set rebootStartTime = now(),rebootStatus = '{0}' where sn = '{1}'", (int)status, sn);
                        }
                        else if ((status == TaskStatus.ReponseOk) || (status == TaskStatus.ReponseFail))
                        {
                            sql = string.Format("update apaction set rebootEndTime = now(),rebootStatus = '{0}' where sn = '{1}'", (int)status, sn);
                        }
                        else
                        {
                            sql = string.Format("update apaction set rebootStatus = '{0}' where sn = '{1}'", (int)status, sn);
                        }

                        break;
                    }
            }

            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    if (cmd.ExecuteNonQuery() < 0)
                    {
                        Logger.Trace(Logger.__INFO__, sql);
                        return false;
                    }
                    else
                    {
                        ret = true;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return false;
            }

            //更新schedulerinfo中的successCount或failCount
            if (true == ret)
            {                         
                if (status == TaskStatus.ReponseOk)
                {
                    UInt32 successCount = 0;
                    sql = string.Format("select successCount from schedulerinfo where actionId = '{0}'", actionIdValue);

                    try
                    {
                        using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                        {
                            using (MySqlDataReader dr = cmd.ExecuteReader())
                            {
                                while (dr.Read())
                                {
                                    successCount = Convert.ToUInt32(dr[0]);
                                    break;
                                }
                                dr.Close();
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Trace(e);
                        return false;
                    }

                    sql = string.Format("update schedulerinfo set successCount = '{0}' where actionId = '{1}'", ++successCount, actionIdValue);
                    try
                    {
                        using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                        {
                            if (cmd.ExecuteNonQuery() > 0)
                            {
                                ret = true;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Trace(e);
                        return false;
                    }
                }


                if (status == TaskStatus.ReponseFail)
                {
                    UInt32 failCount = 0;
                    sql = string.Format("select failCount from schedulerinfo where actionId = '{0}'", actionIdValue);

                    try
                    {
                        using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                        {
                            using (MySqlDataReader dr = cmd.ExecuteReader())
                            {
                                while (dr.Read())
                                {
                                    failCount = Convert.ToUInt32(dr[0]);
                                    break;
                                }
                                dr.Close();
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Trace(e);
                        return false;
                    }

                    sql = string.Format("update schedulerinfo set failCount = '{0}' where actionId = '{1}'", ++failCount, actionIdValue);
                    try
                    {
                        using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                        {
                            if (cmd.ExecuteNonQuery() > 0)
                            {
                                ret = true;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Trace(e);
                        return false;
                    }
                }
            }

            return true;
        }
        

        /// <summary>
        /// 将string数组中重复的项去掉
        /// </summary>
        /// <param name="values">传入的string数组</param>
        /// <returns>返回的string数组</returns>
        public string[] RemoveDup(string[] values)
        {
            List<string> list = new List<string>();

            if (values == null || values.Length <= 1)
            {
                return values;
            }

            try
            {
                for (int i = 0; i < values.Length; i++)
                {
                    //对每个成员做一次新数组查询如果没有相等的则加到新数组
                    if (list.IndexOf(values[i].ToLower()) == -1)
                    {
                        list.Add(values[i]);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return null;
            }
           
            return list.ToArray();
        }


        /// <summary>
        /// 根据任务类型获取任务唯一的Id号
        /// 算法如下：
        /// TaskType的字符串 + 年月日时分秒 + schedulerinfo中最大id号加1
        /// 比如：GetLogTask_20180122170351_147
        /// </summary>
        /// <param name="type">任务类型</param>
        /// <param name="id">输出的唯一Id号</param>
        /// <returns>
        /// 成功 ： true
        /// 失败 ： false
        /// </returns>
        public bool GetTaskId(TaskType type,ref string id)
        {
            UInt32 cnt = 0;
            string actionId = "";

            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return false;
            }

            //获取调度表中的总记录数
            string sql = string.Format("select count(*) from schedulerinfo");
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            cnt = Convert.ToUInt32(dr[0]);
                        }
                        dr.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return false;
            }

            if (cnt <= 0)
            {
                //获取调度表中无记录的情况
                actionId = type.ToString() + "_" + string.Format("{0:yyyyMMddHHmmss}", DateTime.Now) + "_1";
                id = actionId;
                return true;
            }


            //获取ID的最大值
            sql = string.Format("select max(id) from schedulerinfo");
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        string currId = "1";
                        while (dr.Read())
                        {
                            if (dr.HasRows)
                            {
                                currId = (int.Parse(dr[0].ToString()) + 1).ToString();
                            }
                        }
                        dr.Close();

                        actionId = type.ToString() + "_" + string.Format("{0:yyyyMMddHHmmss}", DateTime.Now) + "_" + currId;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return false;
            }

            id = actionId;
            return true;
        }


        /// <summary>
        /// 向apTask表及tasktable表添加新任务
        /// 任务id自动生成，规则如下：
        /// TaskType的字符串 + 年月日时分秒 + schedulerinfo中最大id号加1
        /// 比如：GetLogTask_20180122170351_147
        /// </summary>
        /// <param name="name">任务名称</param>
        /// <param name="actionId">动作ID</param>
        /// <param name="type">任务类型</param>
        /// <param name="xml">任务的xml命令</param>
        /// <param name="listSN">要执行该命令的一批AP</param>
        /// <returns>
        /// 成功 ： true
        /// 失败 ： false
        /// </returns>
        public bool AddTaskToTable(string name,string actionId, TaskType type, string xml, string[] listSN)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return false;
            }

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(actionId) || string.IsNullOrEmpty(xml))
            {
                Logger.Trace(Logger.__INFO__, "参数为空");
                return false;
            }

            if (name.Length > 64 || actionId.Length > 64 || xml.Length > 8192)
            {
                Logger.Trace(Logger.__INFO__, "参数长度有误");
                return false;
            }

            if (type == TaskType.TaskNull)
            {
                Logger.Trace(Logger.__INFO__, "type == TaskNull");
                return false;
            }

            if ((listSN == null) || (listSN.Length < 1))
            {
                Logger.Trace(Logger.__INFO__, "listSN为空");
                return false;
            }            

            //去除数组中重复的项
            listSN = RemoveDup(listSN);

            UInt32 successUpdateCnt = 0;
            foreach (string str in listSN)
            {
                switch (type)
                {
                    case TaskType.UpgradTask:
                        {
                            //判断str是否在apaction中是否存在
                            if (apaction_record_exist(str) < 1)
                            {
                                break;
                            }

                            //更新apaction中相应的记录
                            if (apaction_record_update(str, actionId, TaskType.UpgradTask, TaskStatus.NoSendReqst) == 0)
                            {
                                successUpdateCnt++;                               
                            }

                            break;
                        }
                    case TaskType.GetLogTask:
                        {
                            //判断str是否在apaction中是否存在
                            if (apaction_record_exist(str) < 1)
                            {
                                break;
                            }

                            //更新apaction中相应的记录
                            if (apaction_record_update(str, actionId, TaskType.GetLogTask, TaskStatus.NoSendReqst) == 0)
                            {
                                successUpdateCnt++;                       
                            }

                            break;
                        }
                    case TaskType.GetParameterValuesTask:
                        {
                            //判断str是否在apaction中是否存在
                            if (apaction_record_exist(str) < 1)
                            {
                                break;
                            }

                            //更新apaction中相应的记录
                            if (apaction_record_update(str, actionId, TaskType.GetParameterValuesTask, TaskStatus.NoSendReqst) == 0)
                            {
                                successUpdateCnt++;      
                            }

                            break;
                        }
                    case TaskType.SetParameterValuesTask:
                        {
                            //判断str是否在apaction中是否存在
                            if (apaction_record_exist(str) < 1)
                            {
                                break;
                            }

                            //更新apaction中相应的记录
                            if (apaction_record_update(str, actionId, TaskType.SetParameterValuesTask, TaskStatus.NoSendReqst) == 0)
                            {
                                successUpdateCnt++;                
                            }

                            break;
                        }
                    case TaskType.RebootTask:
                        {
                            //判断str是否在apaction中是否存在
                            if (apaction_record_exist(str) < 1)
                            {
                                break;
                            }

                            //更新apaction中相应的记录
                            if (apaction_record_update(str, actionId, TaskType.RebootTask, TaskStatus.NoSendReqst) == 0)
                            {
                                successUpdateCnt++;     
                            }

                            break;
                        }
                    default:
                        {
                            Logger.Trace(Logger.__INFO__, "尚未支持的type");
                            break;
                        }
                }
            }

            if (0 == schedulerinfo_record_insert(name, actionId, type, xml, successUpdateCnt, 0, 0))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        #endregion
       
        #region 03 - apconninfo操作

        /// <summary>
        /// 判断对应的sn是否存在apconninfo表中
        /// </summary>
        /// <param name="sn">AP的SN号</param>
        /// <returns> 
        ///   -1 ：数据库尚未打开
        ///   -2 ：参数有误
        ///   -4 ：数据库操作失败 
        ///    0 : 不存在
        ///    1 ：存在
        /// </returns>
        public int apconninfo_record_exist(string sn)
        {
            UInt32 cnt = 0;

            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }

            if (string.IsNullOrEmpty(sn))
            {
                Logger.Trace(Logger.__INFO__, "参数为空");
                return -2;
            }

            if (sn.Length > 32)
            {
                Logger.Trace(Logger.__INFO__, "参数长度有误");
                return -2;
            }

            string sql = string.Format("select count(*) from apconninfo where sn = '{0}'", sn);
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            cnt = Convert.ToUInt32(dr[0]);
                        }
                        dr.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            if (cnt <= 0)
            {
                //SN不存在
                return 0;
            }
            else
            {
                //SN存在
                return 1;
            }
        }

        /// <summary>
        /// 插入记录到addressinfo表中
        /// </summary>
        /// <param name="sn">AP的SN好</param>
        /// <param name="url">AP反向连接地址</param>
        /// <param name="userName">AP反向连接用户名</param>
        /// <param name="psw">AP反向连接密码</param>
        /// <returns>
        ///   -1 ：数据库尚未打开
        ///   -2 ：参数有误
        ///   -3 ：记录已经存在
        ///   -4 ：数据库操作失败 
        ///    0 : 插入成功 
        /// </returns>
        public int apconninfo_record_insert(string sn, string url, string userName, string psw)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }

            if (string.IsNullOrEmpty(sn) || string.IsNullOrEmpty(url) ||
                string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(psw))
            {
                Logger.Trace(Logger.__INFO__, "参数为空");
                return -2;
            }

            if (sn.Length > 32 || url.Length > 128 || userName.Length > 32 || psw.Length > 32)
            {
                Logger.Trace(Logger.__INFO__, "参数长度有误");
                return -2;
            }

            //检查用户是否存在
            if (1 == apconninfo_record_exist(sn))
            {
                Logger.Trace(Logger.__INFO__, "记录已经存在");
                return -3;
            }

            string sql = string.Format("insert into apconninfo values(NULL,'{0}','{1}','{2}','{3}')",sn,url,userName,psw);
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    if (cmd.ExecuteNonQuery() < 0)
                    {
                        Logger.Trace(Logger.__INFO__, sql);
                        return -4;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            return 0;
        }

        /// <summary>
        /// 在apconninfo表中删除指定的SN
        /// </summary>
        /// <param name="sn">AP的SN号</param>
        /// <returns>
        /// -1 ：数据库尚未打开
        /// -2 ：参数有误
        /// -3 ：用户不存在
        /// -4 ：数据库操作失败 
        ///  0 : 删除成功 
        /// </returns>
        public int apconninfo_record_delete(string sn)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }

            if (string.IsNullOrEmpty(sn))
            {
                Logger.Trace(Logger.__INFO__, "参数为空");
                return -2;
            }

            if (sn.Length > 32)
            {
                Logger.Trace(Logger.__INFO__, "参数长度有误");
                return -2;
            }

            if (0 == apconninfo_record_exist(sn))
            {
                Logger.Trace(Logger.__INFO__, "记录不存在");
                return -3;
            }

            string sql = string.Format("delete from apconninfo where sn = '{0}'", sn);
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    if (cmd.ExecuteNonQuery() < 0)
                    {
                        Logger.Trace(Logger.__INFO__, sql);
                        return -4;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            return 0;
        }

        /// <summary>
        /// 获取apconninfo表中的各条记录
        /// </summary>
        /// <param name="dt">
        /// 返回的DataTable，包含的列为：id,sn,url,userName,psw
        /// </param>
        /// <returns>
        /// -1 ：数据库尚未打开
        /// -4 ：数据库操作失败 
        ///  0 : 查询成功 
        /// </returns>
        public int apconninfo_record_entity_get(ref DataTable dt)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }

            dt = new DataTable("apconninfo");

            DataColumn column0 = new DataColumn();
            column0.DataType = System.Type.GetType("System.Int32");
            column0.ColumnName = "id";

            DataColumn column1 = new DataColumn();
            column1.DataType = System.Type.GetType("System.String");
            column1.ColumnName = "sn";

            DataColumn column2 = new DataColumn();
            column2.DataType = System.Type.GetType("System.String");
            column2.ColumnName = "url";

            DataColumn column3 = new DataColumn();
            column3.DataType = System.Type.GetType("System.String");
            column3.ColumnName = "userName";

            DataColumn column4 = new DataColumn();
            column4.DataType = System.Type.GetType("System.String");
            column4.ColumnName = "psw";           

            dt.Columns.Add(column0);
            dt.Columns.Add(column1);
            dt.Columns.Add(column2);
            dt.Columns.Add(column3);
            dt.Columns.Add(column4);

            string sql = string.Format("select id,sn,url,userName,psw from apconninfo");
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            DataRow row = dt.NewRow();

                            row[0] = Convert.ToInt32(dr[0]);
                            row[1] = dr[1].ToString();
                            row[2] = dr[2].ToString();
                            row[3] = dr[3].ToString();
                            row[4] = dr[4].ToString();              

                            dt.Rows.Add(row);
                        }
                        dr.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            return 0;
        }

        /// <summary>
        /// 从apconninfo表查找AP反向连接信息
        /// </summary>
        /// <param name="url">返回的url</param>
        /// <param name="un">返回的username</param>
        /// <param name="psw">返回的密码</param>
        /// <param name="sn">要查找ap的sn号</param>
        /// <returns>
        /// 成功 : true
        /// 失败 : false
        ///        原因包括：数据库尚未连接，参数有误，参数长度有误，
        ///        记录不存在，数据库操作失败等
        /// </returns>
        public bool GetUrlInfoBySn(ref String url, ref String un, ref String psw, String sn)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return false;
            }

            if (string.IsNullOrEmpty(sn))
            {
                Logger.Trace(Logger.__INFO__, "参数为空");
                return false;
            }

            if (sn.Length > 32)
            {
                Logger.Trace(Logger.__INFO__, "参数长度有误");
                return false;
            }

            if (0 == apconninfo_record_exist(sn))
            {
                Logger.Trace(Logger.__INFO__, "记录不存在");
                return false;
            }

            string sql = string.Format("select url,userName,psw from apconninfo where sn = '{0}'", sn);
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            url = dr[0].ToString();
                            un = dr[1].ToString();
                            psw = dr[2].ToString();
                        }
                        dr.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return false;
            }

            return true;
        }

        /// <summary>
        /// 通过sn更新url，userName或者psw
        /// </summary>
        /// <param name="sn">AP的SN号</param>
        /// <param name="url">反向链接的url，传入null或者""时表示不更新</param>
        /// <param name="userName">用户名，传入null或者""时表示不更新</param>
        /// <param name="psw">密码，传入null或者""时表示不更新</param>
        /// <returns>
        ///  返回值 ：
        /// -1 ：数据库尚未打开
        /// -2 ：参数有误
        /// -3 ：SN不存在
        /// -4 ：数据库操作失败 
        ///  0 : 更新成功             
        /// </returns>
        public int apconninfo_record_update(string sn, string url, string userName, string psw)
        {
            string sql = "";
            string sqlSub = "";

            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }

            if (string.IsNullOrEmpty(sn))
            {
                Logger.Trace(Logger.__INFO__, "参数为空");
                return -2;
            }

            if (sn.Length > 32)
            {
                Logger.Trace(Logger.__INFO__, "sn参数长度有误");
                return -2;
            }


            if (!string.IsNullOrEmpty(url))
            {
                if (url.Length > 128)
                {
                    Logger.Trace(Logger.__INFO__, "url参数长度有误");
                    return -2;
                }
                else
                {
                    sqlSub += string.Format("url = '{0}',", url);
                }
            }

            if (!string.IsNullOrEmpty(userName))
            {
                if (userName.Length > 32)
                {
                    Logger.Trace(Logger.__INFO__, "userName参数长度有误");
                    return -2;
                }
                else
                {
                    sqlSub += string.Format("userName = '{0}',", userName);
                }
            }

            if (!string.IsNullOrEmpty(psw))
            {
                if (psw.Length > 32)
                {
                    Logger.Trace(Logger.__INFO__, "psw参数长度有误");
                    return -2;
                }
                else
                {
                    sqlSub += string.Format("psw = '{0}',", psw);
                }
            }

            if (sqlSub != "")
            {
                //去掉最后一个字符
                sqlSub = sqlSub.Remove(sqlSub.Length - 1, 1);
            }
            else
            {
                //不需要更新
                Logger.Trace(Logger.__INFO__, "apconninfo_record_update,无需更新");
                return 0;
            }

            if (0 == apconninfo_record_exist(sn))
            {
                Logger.Trace(Logger.__INFO__, "记录不存在");
                return -3;
            }

            sql = string.Format("update apconninfo set {0} where sn = '{1}'",sqlSub,sn);

            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    if (cmd.ExecuteNonQuery() < 0)
                    {
                        Logger.Trace(Logger.__INFO__, sql);
                        return -4;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            return 0;
        }

        #endregion

        #region 04 - aploginfo操作

    
        /// <summary>
        /// 判断sn和actionId对应的记录是否在aploginfo表中
        /// </summary>
        /// <param name="sn">AP的SN号</param>
        /// <param name="actionId">上传文件对应的actionId</param>
        /// <returns> 
        ///   -1 ：数据库尚未打开
        ///   -2 ：参数有误
        ///   -4 ：数据库操作失败 
        ///    0 : 不存在
        ///    1 ：存在
        /// </returns>
        public int aploginfo_record_exist(string sn, string actionId)
        {
            UInt32 cnt = 0;

            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }

            if (string.IsNullOrEmpty(sn))
            {
                Logger.Trace(Logger.__INFO__, "sn参数为空");
                return -2;
            }

            if (sn.Length > 32)
            {
                Logger.Trace(Logger.__INFO__, "sn参数长度有误");
                return -2;
            }

            if (string.IsNullOrEmpty(actionId))
            {
                Logger.Trace(Logger.__INFO__, "actionId参数为空");
                return -2;
            }

            if (actionId.Length > 64)
            {
                Logger.Trace(Logger.__INFO__, "actionId参数长度有误");
                return -2;
            }

            string sql = string.Format("select count(*) from aploginfo where sn = '{0}' and actionId = '{1}'", sn, actionId);
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            cnt = Convert.ToUInt32(dr[0]);
                        }
                        dr.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            if (cnt <= 0)
            {
                //记录不存在
                return 0;
            }
            else
            {
                //记录存在
                return 1;
            }
        }
       

        /// <summary>
        /// 插入记录到aploginfo表中
        /// 是否可用字段available插入记录时默认为0，
        /// 后续上层使用update接口进行更新
        /// </summary>
        /// <param name="sn">AP的SN号</param>
        /// <param name="actionId">上传log的SN对应的动作ID</param>
        /// <param name="uploadTime">上传时间</param>
        /// <param name="fileName">Log的文件名称</param>
        /// <param name="fileSize">Log的文件大小</param>
        /// <param name="des">描述</param>
        /// <returns> 
        ///   -1 ：数据库尚未打开
        ///   -2 ：参数有误
        ///   -3 ：记录已经存在
        ///   -4 ：数据库操作失败 
        ///   -5 ：用户名不存在
        ///    0 : 插入成功
        /// </returns>
        public int aploginfo_record_insert(string sn, string actionId, string uploadTime, string fileName, UInt32 fileSize, string des)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }

            if (string.IsNullOrEmpty(sn))
            {
                Logger.Trace(Logger.__INFO__, "sn参数为空");
                return -2;
            }

            if (sn.Length > 32)
            {
                Logger.Trace(Logger.__INFO__, "sn参数长度有误");
                return -2;
            }

            if (string.IsNullOrEmpty(actionId))
            {
                Logger.Trace(Logger.__INFO__, "actionId参数为空");
                return -2;
            }

            if (actionId.Length > 64)
            {
                Logger.Trace(Logger.__INFO__, "actionId参数长度有误");
                return -2;
            }


            if (string.IsNullOrEmpty(uploadTime))
            {
                Logger.Trace(Logger.__INFO__, "uploadTime参数为空");
                return -2;
            }

            if (uploadTime.Length > 32)
            {
                Logger.Trace(Logger.__INFO__, "uploadTime参数长度有误");
                return -2;
            }


            if (string.IsNullOrEmpty(fileName))
            {
                Logger.Trace(Logger.__INFO__, "fileName参数为空");
                return -2;
            }

            if (fileName.Length > 512)
            {
                Logger.Trace(Logger.__INFO__, "fileName参数长度有误");
                return -2;
            }
          
            if (string.IsNullOrEmpty(des))
            {
                Logger.Trace(Logger.__INFO__, "des参数为空");
                return -2;
            }

            if (des.Length > 1024)
            {
                Logger.Trace(Logger.__INFO__, "des参数长度有误");
                return -2;
            }


            // 判断记录是否已经存在
            if (1 == aploginfo_record_exist(sn,actionId))
            {
                Logger.Trace(Logger.__INFO__, "sn,actionId对应的记录已经存在");
                return -3;
            }


            string sql = string.Format("insert into aploginfo values(NULL,'{0}','{1}','{2}','{3}',{4},{5},'{6}')", sn, actionId, uploadTime, fileName, fileSize,0,des);
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    if (cmd.ExecuteNonQuery() < 0)
                    {
                        Logger.Trace(Logger.__INFO__, sql);
                        return -4;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            return 0;
        }

        /// <summary>
        /// 在aploginfo表中删除指定sn和actionId对应的记录
        /// </summary>
        /// <param name="sn">AP的SN号</param>
        /// <param name="actionId">上传文件对应的actionId</param>
        /// <returns>
        /// -1 ：数据库尚未打开
        /// -2 ：参数有误
        /// -3 ：用户不存在
        /// -4 ：数据库操作失败 
        ///  0 : 删除成功 
        /// </returns>
        public int aploginfo_record_delete(string sn, string actionId)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }

            if (string.IsNullOrEmpty(sn))
            {
                Logger.Trace(Logger.__INFO__, "sn参数为空");
                return -2;
            }

            if (sn.Length > 32)
            {
                Logger.Trace(Logger.__INFO__, "sn参数长度有误");
                return -2;
            }


            if (string.IsNullOrEmpty(actionId))
            {
                Logger.Trace(Logger.__INFO__, "actionId参数为空");
                return -2;
            }

            if (actionId.Length > 64)
            {
                Logger.Trace(Logger.__INFO__, "actionId参数长度有误");
                return -2;
            }


            if (0 == aploginfo_record_exist(sn,actionId))
            {
                Logger.Trace(Logger.__INFO__, "sn,actionId记录不存在");
                return -3;
            }

            string sql = string.Format("delete from aploginfo where sn = '{0}' and actionId = '{1}'", sn, actionId);
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    if (cmd.ExecuteNonQuery() < 0)
                    {
                        Logger.Trace(Logger.__INFO__, sql);
                        return -4;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            return 0;
        }


        /// <summary>
        /// 在aploginfo表中，通过sn和actionId更新available字段
        /// </summary>
        /// <param name="sn">AP的SN号</param>
        /// <param name="actionId">上传文件对应的actionId</param>
        /// <param name="available">是否可用字段，0或1</param>
        /// <returns></returns>
        public int aploginfo_record_update(string sn, string actionId, UInt32 available)
        {
            string sql = "";

            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }

            if (string.IsNullOrEmpty(sn))
            {
                Logger.Trace(Logger.__INFO__, "sn参数为空");
                return -2;
            }

            if (sn.Length > 32)
            {
                Logger.Trace(Logger.__INFO__, "sn参数长度有误");
                return -2;
            }

            if (string.IsNullOrEmpty(actionId))
            {
                Logger.Trace(Logger.__INFO__, "actionId参数为空");
                return -2;
            }

            if (actionId.Length > 64)
            {
                Logger.Trace(Logger.__INFO__, "actionId参数长度有误");
                return -2;
            }

            if (available != 0 && available != 1)
            {
                Logger.Trace(Logger.__INFO__, "available参数长度有误");
                return -2;
            }


            if (0 == aploginfo_record_exist(sn,actionId))
            {
                Logger.Trace(Logger.__INFO__, "sn记录不存在");
                return -3;
            }

            sql = string.Format("update aploginfo set available = {0} where sn = '{1}' and actionId = '{2}'", available,sn, actionId);

            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    if (cmd.ExecuteNonQuery() < 0)
                    {
                        Logger.Trace(Logger.__INFO__, sql);
                        return -4;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            return 0;
        }


        /// <summary>
        /// 返回aploginfo表中的所有available字段为1（可用）的记录
        /// </summary>
        /// <param name="dt">返回的DataTable</param>
        /// <returns>
        ///   -1 ：数据库尚未打开
        ///   -4 ：数据库操作失败 
        ///    0 : 获取成功          
        /// </returns>
        public int aploginfo_record_entity_get(ref DataTable dt)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }

            dt = new DataTable("aploginfo");

            DataColumn column0 = new DataColumn();
            column0.DataType = System.Type.GetType("System.Int32");
            column0.ColumnName = "id";

            DataColumn column1 = new DataColumn();
            column1.DataType = System.Type.GetType("System.String");
            column1.ColumnName = "sn";

            DataColumn column2 = new DataColumn();
            column2.DataType = System.Type.GetType("System.String");
            column2.ColumnName = "actionId";

            DataColumn column3 = new DataColumn();
            column3.DataType = System.Type.GetType("System.String");
            column3.ColumnName = "uploadTime";

            DataColumn column4 = new DataColumn();
            column4.DataType = System.Type.GetType("System.String");
            column4.ColumnName = "fileName";

            DataColumn column5 = new DataColumn();
            column5.DataType = System.Type.GetType("System.Int32");
            column5.ColumnName = "fileSize";

            DataColumn column6 = new DataColumn();
            column6.DataType = System.Type.GetType("System.Int32");
            column6.ColumnName = "available";

            DataColumn column7 = new DataColumn();
            column7.DataType = System.Type.GetType("System.String");
            column7.ColumnName = "des";

            dt.Columns.Add(column0);
            dt.Columns.Add(column1);
            dt.Columns.Add(column2);
            dt.Columns.Add(column3);
            dt.Columns.Add(column4);
            dt.Columns.Add(column5);
            dt.Columns.Add(column6);
            dt.Columns.Add(column7);

            string sql = string.Format("select * from aploginfo where available = {0}",1);
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            DataRow row = dt.NewRow();

                            row[0] = Int32.Parse(dr[0].ToString());

                            row[1] = dr[1].ToString();
                            row[2] = dr[2].ToString();
                            row[3] = dr[3].ToString();
                            row[4] = dr[4].ToString();

                            row[5] = Int32.Parse(dr[5].ToString());
                            row[6] = Int32.Parse(dr[6].ToString());

                            row[7] = dr[7].ToString();

                            dt.Rows.Add(row);
                        }
                        dr.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            return 0;
        }

        #endregion

        #region 05 - deviceinfo操作

        /// <summary>
        /// 判断对应的sn是否存在deviceinfo表中
        /// </summary>
        /// <param name="sn">AP的SN号</param>
        /// <returns> 
        ///   -1 ：数据库尚未打开
        ///   -2 ：参数有误
        ///   -4 ：数据库操作失败 
        ///    0 : 不存在
        ///    1 ：存在
        /// </returns>
        public int deviceinfo_record_exist(string sn)
        {
            UInt32 cnt = 0;

            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }

            if (string.IsNullOrEmpty(sn))
            {
                Logger.Trace(Logger.__INFO__, "参数为空");
                return -2;
            }

            if (sn.Length > 32)
            {
                Logger.Trace(Logger.__INFO__, "参数长度有误");
                return -2;
            }          

            string sql = string.Format("select count(*) from deviceinfo where sn = '{0}'", sn);
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            cnt = Convert.ToUInt32(dr[0]);
                        }
                        dr.Close();
                    }
                }  
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            if (cnt <= 0)
            {
                //SN不存在
                return 0;
            }
            else
            {
                //SN存在
                return 1;
            }
        }

        /// <summary>
        /// 插入记录到deviceinfo表中
        /// 设备的其他十几个字段由deviceinfo_record_update进行更新，
        /// 因为这些字段在添加设备时时不知道的，而且也可能经常更新
        /// </summary>
        /// <param name="bsName">基站名称</param>
        /// <param name="sn">AP的SN号</param>
        /// <returns>
        ///   -1 ：数据库尚未打开
        ///   -2 ：参数有误
        ///   -3 ：记录已经存在
        ///   -4 ：数据库操作失败 
        ///    0 : 插入成功 
        /// </returns>
        public int deviceinfo_record_insert(string bsName, string sn)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }

            if (string.IsNullOrEmpty(bsName) || string.IsNullOrEmpty(sn))
            {
                Logger.Trace(Logger.__INFO__, "参数为空");
                return -2;
            }

            if (bsName.Length > 64 || sn.Length > 32)
            {
                Logger.Trace(Logger.__INFO__, "参数长度有误");
                return -2;
            }

            //检查用户是否存在
            if (1 == deviceinfo_record_exist(sn))
            {
                Logger.Trace(Logger.__INFO__, "记录已经存在");
                return -3;
            }

            string sql = string.Format("insert into deviceinfo values(NULL,'{0}','{1}',NULL,,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL,NULL)", bsName, sn);
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    if (cmd.ExecuteNonQuery() < 0)
                    {
                        Logger.Trace(Logger.__INFO__, sql);
                        return -4;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }



            return 0;
        }

        /// <summary>
        /// 在deviceinfo表中删除指定的SN
        /// </summary>
        /// <param name="sn">AP的SN号</param>
        /// <returns>
        /// -1 ：数据库尚未打开
        /// -2 ：参数有误
        /// -3 ：用户不存在
        /// -4 ：数据库操作失败 
        ///  0 : 删除成功 
        /// </returns>
        public int deviceinfo_record_delete(string sn)
        {
            int ret = 0;

            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }

            if (string.IsNullOrEmpty(sn))
            {
                Logger.Trace(Logger.__INFO__, "参数为空");
                return -2;
            }

            if (sn.Length > 32)
            {
                Logger.Trace(Logger.__INFO__, "参数长度有误");
                return -2;
            }

            if (0 == deviceinfo_record_exist(sn))
            {
                Logger.Trace(Logger.__INFO__, "记录不存在");
                return -3;
            }

            string sql = string.Format("delete from deviceinfo where sn = '{0}'", sn);
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    if (cmd.ExecuteNonQuery() < 0)
                    {
                        Logger.Trace(Logger.__INFO__, sql);
                        return -4;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            ret = 0;
            ret += addressinfo_record_delete(sn);
            ret += apconninfo_record_delete(sn);
            ret += apaction_record_delete(sn);

            return ret;
        }

        /// <summary>
        /// 通过SN，更新结构体中的各种字段
        /// </summary>
        /// <param name="sn">AP的SN号</param>
        /// <param name="di">
        /// 要更新的各种字段，字段为null或者""时表示不更新
        /// 可更新的字段如下：
        /// public string bsName;
        /// public string ipAddr;
        /// public string type;
        /// public string s1Status;
        /// public string connHS;
        /// public string tac;
        /// public string enbId;
        /// public string cellId;
        /// public string earfcn;
        /// public string pci;
        /// public string updateMode;
        /// public string curVersion;
        /// public string curWarnCnt;
        /// public string onoffLineTime;
        /// public string aliasName;
        /// public string des;
        /// public string province;
        /// public string city;
        /// public string district;
        /// public string street;
        /// </param>
        /// <returns>
        /// 返回值 ：
        /// -1 ：数据库尚未打开
        /// -2 ：参数有误
        /// -3 ：SN不存在
        /// -4 ：数据库操作失败 
        ///  0 : 更新成功  
        /// </returns>
        public int deviceinfo_record_update(string sn,structDeviceInfo di)
        {
            int ret;
            string sql = "";
            string sqlSub = "";

            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }

            if (string.IsNullOrEmpty(sn))
            {
                Logger.Trace(Logger.__INFO__, "参数为空");
                return -2;
            }

            if (sn.Length > 32)
            {
                Logger.Trace(Logger.__INFO__, "sn参数长度有误");
                return -2;
            }

            if (!string.IsNullOrEmpty(di.bsName))
            {
                if (di.bsName.Length > 64)
                {
                    Logger.Trace(Logger.__INFO__, "di.bsName参数长度有误");
                    return -2;
                }
                else
                {
                    sqlSub += string.Format("bsName = '{0}',", di.bsName);
                }
            }

            if (!string.IsNullOrEmpty(di.ipAddr))
            {
                if (di.ipAddr.Length > 32)
                {
                    Logger.Trace(Logger.__INFO__, "di.ipAddr参数长度有误");
                    return -2;
                }
                else
                {
                    sqlSub += string.Format("ipAddr = '{0}',", di.ipAddr);
                }
            }

            if (!string.IsNullOrEmpty(di.type))
            {
                if (di.type.Length > 16)
                {
                    Logger.Trace(Logger.__INFO__, "di.type参数长度有误");
                    return -2;
                }
                else
                {
                    sqlSub += string.Format("type = '{0}',", di.type);
                }
            }


            if (!string.IsNullOrEmpty(di.s1Status))
            {
                if (di.s1Status.Length > 16)
                {
                    Logger.Trace(Logger.__INFO__, "di.s1Status参数长度有误");
                    return -2;
                }
                else
                {
                    sqlSub += string.Format("s1Status = '{0}',", di.s1Status);
                }
            }


            if (!string.IsNullOrEmpty(di.connHS))
            {
                if (di.connHS.Length > 16)
                {
                    Logger.Trace(Logger.__INFO__, "di.connHS参数长度有误");
                    return -2;
                }
                else
                {
                    sqlSub += string.Format("connHS = '{0}',", di.connHS);
                }
            }


            if (!string.IsNullOrEmpty(di.tac))
            {
                if (di.tac.Length > 16)
                {
                    Logger.Trace(Logger.__INFO__, "di.tac参数长度有误");
                    return -2;
                }
                else
                {
                    sqlSub += string.Format("tac = '{0}',", di.tac);
                }
            }

            if (!string.IsNullOrEmpty(di.enbId))
            {
                if (di.enbId.Length > 16)
                {
                    Logger.Trace(Logger.__INFO__, "di.enbId参数长度有误");
                    return -2;
                }
                else
                {
                    sqlSub += string.Format("enbId = '{0}',", di.enbId);
                }
            }

            if (!string.IsNullOrEmpty(di.cellId))
            {
                if (di.cellId.Length > 16)
                {
                    Logger.Trace(Logger.__INFO__, "di.cellId参数长度有误");
                    return -2;
                }
                else
                {
                    sqlSub += string.Format("cellId = '{0}',", di.cellId);
                }
            }


            if (!string.IsNullOrEmpty(di.earfcn))
            {
                if (di.earfcn.Length > 16)
                {
                    Logger.Trace(Logger.__INFO__, "di.earfcn参数长度有误");
                    return -2;
                }
                else
                {
                    sqlSub += string.Format("earfcn = '{0}',", di.earfcn);
                }
            }

            if (!string.IsNullOrEmpty(di.pci))
            {
                if (di.pci.Length > 16)
                {
                    Logger.Trace(Logger.__INFO__, "di.pci参数长度有误");
                    return -2;
                }
                else
                {
                    sqlSub += string.Format("pci = '{0}',", di.pci);
                }
            }


            if (!string.IsNullOrEmpty(di.updateMode))
            {
                if (di.updateMode.Length > 32)
                {
                    Logger.Trace(Logger.__INFO__, "di.updateMode参数长度有误");
                    return -2;
                }
                else
                {
                    sqlSub += string.Format("updateMode = '{0}',", di.updateMode);
                }
            }

            if (!string.IsNullOrEmpty(di.curVersion))
            {
                if (di.curVersion.Length > 64)
                {
                    Logger.Trace(Logger.__INFO__, "di.curVersion参数长度有误");
                    return -2;
                }
                else
                {
                    sqlSub += string.Format("curVersion = '{0}',", di.curVersion);
                }
            }


            if (!string.IsNullOrEmpty(di.curWarnCnt))
            {
                if (di.curWarnCnt.Length > 16)
                {
                    Logger.Trace(Logger.__INFO__, "di.curWarnCnt参数长度有误");
                    return -2;
                }
                else
                {
                    sqlSub += string.Format("curWarnCnt = '{0}',", di.curWarnCnt);
                }
            }


            if (!string.IsNullOrEmpty(di.onoffLineTime))
            {
                if (di.onoffLineTime.Length > 64)
                {
                    Logger.Trace(Logger.__INFO__, "di.onoffLineTime参数长度有误");
                    return -2;
                }
                else
                {
                    sqlSub += string.Format("onoffLineTime = now(),", di.onoffLineTime);
                }
            }


            if (!string.IsNullOrEmpty(di.aliasName))
            {
                if (di.aliasName.Length > 64)
                {
                    Logger.Trace(Logger.__INFO__, "di.aliasName参数长度有误");
                    return -2;
                }
                else
                {
                    sqlSub += string.Format("aliasName = '{0}',", di.aliasName);
                }
            }

            if (!string.IsNullOrEmpty(di.des))
            {
                if (di.des.Length > 1024)
                {
                    Logger.Trace(Logger.__INFO__, "di.des参数长度有误");
                    return -2;
                }
                else
                {
                    sqlSub += string.Format("des = '{0}',", di.des);
                }
            }
            
            if (sqlSub != "")
            {
                //去掉最后一个字符
                sqlSub = sqlSub.Remove(sqlSub.Length - 1, 1);

                if (0 == deviceinfo_record_exist(sn))
                {
                    Logger.Trace(Logger.__INFO__, "记录不存在于deviceinfo表中");
                    return -3;
                }


                //更新deviceinfo表中的信息
                sql = string.Format("update deviceinfo set {0} where sn = '{1}'", sqlSub, sn);

                try
                {
                    using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                    {
                        if (cmd.ExecuteNonQuery() < 0)
                        {
                            Logger.Trace(Logger.__INFO__, sql);
                            return -4;
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.Trace(e);
                    return -4;
                }
            }
            else
            {
                //不需要更新
                Logger.Trace(Logger.__INFO__, "deviceinfo_record_updateb,无需更新deviceinfo表");                
            }

            /*           
             * 开始检查是否更新addressinfo表中的字段
             */

            /*
             * 通过SN，更新省份，城市，区和街道
             */
            ret = 0;// addressinfo_record_update(di.province, di.city, di.district, di.street, sn);

            return ret;
        }

        /// <summary>
        /// 获取deviceinfo表中的各条记录
        /// </summary>
        /// <param name="dt">
        /// 返回的DataTable，包含的列为：id,bsName,sn,ipAddr,type,s1Status,connHS
        /// tac,enbId,cellId,earfcn,pci,updateMode,curVersion,curWarnCnt,onoffLineTime
        /// aliasName,des,province,city,district,street
        /// </param>
        /// <returns>
        /// -1 ：数据库尚未打开
        /// -4 ：数据库操作失败 
        ///  0 : 查询成功 
        /// </returns>
        public int deviceinfo_record_entity_get(ref DataTable dt)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }

            dt = new DataTable("deviceinfo");

            DataColumn column0 = new DataColumn();
            column0.DataType = System.Type.GetType("System.Int32");
            column0.ColumnName = "id";

            DataColumn column1 = new DataColumn();
            column1.DataType = System.Type.GetType("System.String");
            column1.ColumnName = "bsName";

            DataColumn column2 = new DataColumn();
            column2.DataType = System.Type.GetType("System.String");
            column2.ColumnName = "sn";

            DataColumn column3 = new DataColumn();
            column3.DataType = System.Type.GetType("System.String");
            column3.ColumnName = "ipAddr";

            DataColumn column4 = new DataColumn();
            column4.DataType = System.Type.GetType("System.String");
            column4.ColumnName = "type";

            DataColumn column5 = new DataColumn();
            column5.DataType = System.Type.GetType("System.String");
            column5.ColumnName = "s1Status";

            DataColumn column6 = new DataColumn();
            column6.DataType = System.Type.GetType("System.String");
            column6.ColumnName = "connHS";

            DataColumn column7 = new DataColumn();
            column7.DataType = System.Type.GetType("System.String");
            column7.ColumnName = "tac";

            DataColumn column8 = new DataColumn();
            column8.DataType = System.Type.GetType("System.String");
            column8.ColumnName = "enbId";

            DataColumn column9 = new DataColumn();
            column9.DataType = System.Type.GetType("System.String");
            column9.ColumnName = "cellId";

            DataColumn column10 = new DataColumn();
            column10.DataType = System.Type.GetType("System.String");
            column10.ColumnName = "earfcn";

            DataColumn column11 = new DataColumn();
            column11.DataType = System.Type.GetType("System.String");
            column11.ColumnName = "pci";

            DataColumn column12 = new DataColumn();
            column12.DataType = System.Type.GetType("System.String");
            column12.ColumnName = "updateMode";

            DataColumn column13 = new DataColumn();
            column13.DataType = System.Type.GetType("System.String");
            column13.ColumnName = "curVersion";

            DataColumn column14 = new DataColumn();
            column14.DataType = System.Type.GetType("System.String");
            column14.ColumnName = "curWarnCnt";

            DataColumn column15 = new DataColumn();
            column15.DataType = System.Type.GetType("System.String");
            column15.ColumnName = "onoffLineTime";

            DataColumn column16 = new DataColumn();
            column16.DataType = System.Type.GetType("System.String");
            column16.ColumnName = "aliasName";

            DataColumn column17 = new DataColumn();
            column17.DataType = System.Type.GetType("System.String");
            column17.ColumnName = "des";

            DataColumn column18 = new DataColumn();
            column18.DataType = System.Type.GetType("System.String");
            column18.ColumnName = "province";

            DataColumn column19 = new DataColumn();
            column19.DataType = System.Type.GetType("System.String");
            column19.ColumnName = "city";

            DataColumn column20 = new DataColumn();
            column20.DataType = System.Type.GetType("System.String");
            column20.ColumnName = "district";

            DataColumn column21 = new DataColumn();
            column21.DataType = System.Type.GetType("System.String");
            column21.ColumnName = "street";


            dt.Columns.Add(column0);
            dt.Columns.Add(column1);
            dt.Columns.Add(column2);
            dt.Columns.Add(column3);
            dt.Columns.Add(column4);
            dt.Columns.Add(column5);
            dt.Columns.Add(column6);
            dt.Columns.Add(column7);
            dt.Columns.Add(column8);
            dt.Columns.Add(column9);
            dt.Columns.Add(column10);
            dt.Columns.Add(column11);
            dt.Columns.Add(column12);
            dt.Columns.Add(column13);
            dt.Columns.Add(column14);
            dt.Columns.Add(column15);
            dt.Columns.Add(column16);
            dt.Columns.Add(column17);
            dt.Columns.Add(column18);
            dt.Columns.Add(column19);
            dt.Columns.Add(column20);
            dt.Columns.Add(column21);

            string sql = string.Format("SELECT a.*,b.province,b.city,b.district,b.street FROM (select * from deviceinfo ) AS a INNER JOIN addressinfo As b ON a.sn=b.sn");		

            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            DataRow row = dt.NewRow();

                            row[0] = Convert.ToInt32(dr[0]);
                            row[1] = dr[1].ToString();
                            row[2] = dr[2].ToString();
                            row[3] = dr[3].ToString();
                            row[4] = dr[4].ToString();
                            row[5] = dr[5].ToString();
                            row[6] = dr[6].ToString();
                            row[7] = dr[7].ToString();
                            row[8] = dr[8].ToString();
                            row[9] = dr[9].ToString();
                            row[10] = dr[10].ToString();
                            row[11] = dr[11].ToString();
                            row[12] = dr[12].ToString();
                            row[13] = dr[13].ToString();
                            row[14] = dr[14].ToString();
                            row[15] = dr[15].ToString();
                            row[16] = dr[16].ToString();
                            row[17] = dr[17].ToString();
                            row[18] = dr[18].ToString();
                            row[19] = dr[19].ToString();
                            row[20] = dr[20].ToString();
                            row[21] = dr[21].ToString();

                            dt.Rows.Add(row);
                        }
                        dr.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            return 0;
        }

        /// <summary>
        /// 通过diq中的过滤信息，获取对应的记录集合
        /// </summary>
        /// <param name="dt">
        /// 返回的DataTable，包含的列为：id,bsName,sn,ipAddr,type,s1Status,connHS
        /// tac,enbId,cellId,earfcn,pci,updateMode,curVersion,curWarnCnt,onoffLineTime
        /// aliasName,des,province,city,district,street
        /// </param>
        /// <param name="diq">
        /// 要过滤的各种字段，字段为null或者""时表示不过滤该字段
        /// </param>
        /// <returns>
        /// -1 ：数据库尚未打开
        /// -4 ：数据库操作失败 
        ///  0 : 查询成功 
        /// </returns>
        public int deviceinfo_record_entity_get_by_query(ref DataTable dt, structDeviceInfoQuery diq)
        {       
            string sqlSub = "";
            string sql = "";

            string startTime = "";
            string endTime = "";

            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }

            dt = new DataTable("deviceinfo");

            DataColumn column0 = new DataColumn();
            column0.DataType = System.Type.GetType("System.Int32");
            column0.ColumnName = "id";

            DataColumn column1 = new DataColumn();
            column1.DataType = System.Type.GetType("System.String");
            column1.ColumnName = "bsName";

            DataColumn column2 = new DataColumn();
            column2.DataType = System.Type.GetType("System.String");
            column2.ColumnName = "sn";

            DataColumn column3 = new DataColumn();
            column3.DataType = System.Type.GetType("System.String");
            column3.ColumnName = "ipAddr";

            DataColumn column4 = new DataColumn();
            column4.DataType = System.Type.GetType("System.String");
            column4.ColumnName = "type";

            DataColumn column5 = new DataColumn();
            column5.DataType = System.Type.GetType("System.String");
            column5.ColumnName = "s1Status";

            DataColumn column6 = new DataColumn();
            column6.DataType = System.Type.GetType("System.String");
            column6.ColumnName = "connHS";

            DataColumn column7 = new DataColumn();
            column7.DataType = System.Type.GetType("System.String");
            column7.ColumnName = "tac";

            DataColumn column8 = new DataColumn();
            column8.DataType = System.Type.GetType("System.String");
            column8.ColumnName = "enbId";

            DataColumn column9 = new DataColumn();
            column9.DataType = System.Type.GetType("System.String");
            column9.ColumnName = "cellId";

            DataColumn column10 = new DataColumn();
            column10.DataType = System.Type.GetType("System.String");
            column10.ColumnName = "earfcn";

            DataColumn column11 = new DataColumn();
            column11.DataType = System.Type.GetType("System.String");
            column11.ColumnName = "pci";

            DataColumn column12 = new DataColumn();
            column12.DataType = System.Type.GetType("System.String");
            column12.ColumnName = "updateMode";

            DataColumn column13 = new DataColumn();
            column13.DataType = System.Type.GetType("System.String");
            column13.ColumnName = "curVersion";

            DataColumn column14 = new DataColumn();
            column14.DataType = System.Type.GetType("System.String");
            column14.ColumnName = "curWarnCnt";

            DataColumn column15 = new DataColumn();
            column15.DataType = System.Type.GetType("System.String");
            column15.ColumnName = "onoffLineTime";

            DataColumn column16 = new DataColumn();
            column16.DataType = System.Type.GetType("System.String");
            column16.ColumnName = "aliasName";

            DataColumn column17 = new DataColumn();
            column17.DataType = System.Type.GetType("System.String");
            column17.ColumnName = "des";

            DataColumn column18 = new DataColumn();
            column18.DataType = System.Type.GetType("System.String");
            column18.ColumnName = "province";

            DataColumn column19 = new DataColumn();
            column19.DataType = System.Type.GetType("System.String");
            column19.ColumnName = "city";

            DataColumn column20 = new DataColumn();
            column20.DataType = System.Type.GetType("System.String");
            column20.ColumnName = "district";

            DataColumn column21 = new DataColumn();
            column21.DataType = System.Type.GetType("System.String");
            column21.ColumnName = "street";


            dt.Columns.Add(column0);
            dt.Columns.Add(column1);
            dt.Columns.Add(column2);
            dt.Columns.Add(column3);
            dt.Columns.Add(column4);
            dt.Columns.Add(column5);
            dt.Columns.Add(column6);
            dt.Columns.Add(column7);
            dt.Columns.Add(column8);
            dt.Columns.Add(column9);
            dt.Columns.Add(column10);
            dt.Columns.Add(column11);
            dt.Columns.Add(column12);
            dt.Columns.Add(column13);
            dt.Columns.Add(column14);
            dt.Columns.Add(column15);
            dt.Columns.Add(column16);
            dt.Columns.Add(column17);
            dt.Columns.Add(column18);
            dt.Columns.Add(column19);
            dt.Columns.Add(column20);
            dt.Columns.Add(column21);

            if (!string.IsNullOrEmpty(diq.bsName))
            {
                if (diq.bsName.Length > 64)
                {
                    Logger.Trace(Logger.__INFO__, "diq.bsName参数长度有误");
                    return -2;
                }
                else
                {
                    sqlSub += string.Format(" a.bsName like '%%{0}%%' and", diq.bsName);
                }
            }

            if (!string.IsNullOrEmpty(diq.sn))
            {
                if (diq.sn.Length > 32)
                {
                    Logger.Trace(Logger.__INFO__, "diq.sn参数长度有误");
                    return -2;
                }
                else
                {
                    sqlSub += string.Format(" a.sn like '%%{0}%%' and", diq.sn);
                }
            }


            if (!string.IsNullOrEmpty(diq.ipAddr))
            {
                if (diq.ipAddr.Length > 32)
                {
                    Logger.Trace(Logger.__INFO__, "diq.ipAddr参数长度有误");
                    return -2;
                }
                else
                {
                    sqlSub += string.Format(" a.ipAddr like '%%{0}%%' and", diq.ipAddr);
                }
            }


            if (!string.IsNullOrEmpty(diq.type))
            {
                if (diq.type.Length > 16)
                {
                    Logger.Trace(Logger.__INFO__, "diq.type参数长度有误");
                    return -2;
                }
                else
                {
                    sqlSub += string.Format(" a.type like '%%{0}%%' and", diq.type);
                }
            }


            if (!string.IsNullOrEmpty(diq.s1Status))
            {
                if (diq.s1Status.Length > 16)
                {
                    Logger.Trace(Logger.__INFO__, "diq.s1Status参数长度有误");
                    return -2;
                }
                else
                {
                    sqlSub += string.Format(" a.s1Status like '%%{0}%%' and", diq.s1Status);
                }
            }


            if (!string.IsNullOrEmpty(diq.connHS))
            {
                if (diq.connHS.Length > 16)
                {
                    Logger.Trace(Logger.__INFO__, "diq.connHS参数长度有误");
                    return -2;
                }
                else
                {
                    sqlSub += string.Format(" a.connHS like '%%{0}%%' and", diq.connHS);
                }
            }


            if (!string.IsNullOrEmpty(diq.tac))
            {
                if (diq.tac.Length > 16)
                {
                    Logger.Trace(Logger.__INFO__, "diq.tac参数长度有误");
                    return -2;
                }
                else
                {
                    sqlSub += string.Format(" a.tac like '%%{0}%%' and", diq.type);
                }
            }


            if (!string.IsNullOrEmpty(diq.enbId))
            {
                if (diq.enbId.Length > 16)
                {
                    Logger.Trace(Logger.__INFO__, "diq.enbId参数长度有误");
                    return -2;
                }
                else
                {
                    sqlSub += string.Format(" a.enbId like '%%{0}%%' and", diq.type);
                }
            }


            if (!string.IsNullOrEmpty(diq.cellId))
            {
                if (diq.cellId.Length > 16)
                {
                    Logger.Trace(Logger.__INFO__, "diq.cellId参数长度有误");
                    return -2;
                }
                else
                {
                    sqlSub += string.Format(" a.cellId like '%%{0}%%' and", diq.cellId);
                }
            }


            if (!string.IsNullOrEmpty(diq.earfcn))
            {
                if (diq.earfcn.Length > 16)
                {
                    Logger.Trace(Logger.__INFO__, "diq.earfcn参数长度有误");
                    return -2;
                }
                else
                {
                    sqlSub += string.Format(" a.earfcn like '%%{0}%%' and", diq.earfcn);
                }
            }


            if (!string.IsNullOrEmpty(diq.pci))
            {
                if (diq.pci.Length > 16)
                {
                    Logger.Trace(Logger.__INFO__, "diq.pci参数长度有误");
                    return -2;
                }
                else
                {
                    sqlSub += string.Format(" a.pci like '%%{0}%%' and", diq.pci);
                }
            }


            if (!string.IsNullOrEmpty(diq.updateMode))
            {
                if (diq.updateMode.Length > 32)
                {
                    Logger.Trace(Logger.__INFO__, "diq.updateMode参数长度有误");
                    return -2;
                }
                else
                {
                    sqlSub += string.Format(" a.updateMode like '%%{0}%%' and", diq.updateMode);
                }
            }


            if (!string.IsNullOrEmpty(diq.curVersion))
            {
                if (diq.curVersion.Length > 64)
                {
                    Logger.Trace(Logger.__INFO__, "diq.curVersion参数长度有误");
                    return -2;
                }
                else
                {
                    sqlSub += string.Format(" a.curVersion like '%%{0}%%' and", diq.curVersion);
                }
            }


            if (!string.IsNullOrEmpty(diq.curWarnCnt))
            {
                if (diq.curWarnCnt.Length > 16)
                {
                    Logger.Trace(Logger.__INFO__, "diq.curWarnCnt参数长度有误");
                    return -2;
                }
                else
                {
                    sqlSub += string.Format(" a.curWarnCnt like '%%{0}%%' and", diq.curWarnCnt);
                }
            }


            if (string.IsNullOrEmpty(diq.onoffLineTime_StartTime))
            {
                startTime = "";
            }
            else
            {
                try
                {
                    DateTime.Parse(diq.onoffLineTime_StartTime);                  
                }
                catch
                {
                    Logger.Trace(Logger.__INFO__, "diq.onoffLineTime_StartTime参数格式有误");
                    return -2;
                }

                startTime = diq.onoffLineTime_StartTime;
            }


            if (string.IsNullOrEmpty(diq.onoffLineTime_EndTime))
            {
                //赋一个很大的值
                endTime = "2100-01-01 12:34:56";
            }
            else
            {
                try
                {
                    DateTime.Parse(diq.onoffLineTime_EndTime);
                }
                catch
                {
                    Logger.Trace(Logger.__INFO__, "diq.onoffLineTime_EndTime参数格式有误");
                    return -2;
                }

                endTime = diq.onoffLineTime_EndTime;
            }

            sqlSub += string.Format(" a.onoffLineTime >= '{0}' and a.onoffLineTime <= '{1}' and", startTime, endTime);


            if (!string.IsNullOrEmpty(diq.aliasName))
            {
                if (diq.aliasName.Length > 64)
                {
                    Logger.Trace(Logger.__INFO__, "diq.aliasName参数长度有误");
                    return -2;
                }
                else
                {
                    sqlSub += string.Format(" a.aliasName like '%%{0}%%' and", diq.aliasName);
                }
            }

            if (!string.IsNullOrEmpty(diq.des))
            {
                if (diq.des.Length > 1024)
                {
                    Logger.Trace(Logger.__INFO__, "diq.des参数长度有误");
                    return -2;
                }
                else
                {
                    sqlSub += string.Format(" a.des like '%%{0}%%' and", diq.des);
                }
            }



            //////////////////////////////////////////////////////////////////
            //////////////////////////////////////////////////////////////////


            if (!string.IsNullOrEmpty(diq.province))
            {
                if (diq.province.Length > 64)
                {
                    Logger.Trace(Logger.__INFO__, "diq.province参数长度有误");
                    return -2;
                }
                else
                {              
                    sqlSub += string.Format(" b.province like '%%{0}%%' and", diq.province);
                }
            }

            if (!string.IsNullOrEmpty(diq.city))
            {
                if (diq.city.Length > 64)
                {
                    Logger.Trace(Logger.__INFO__, "diq.city参数长度有误");
                    return -2;
                }
                else
                {
                    sqlSub += string.Format(" b.city like '%%{0}%%' and", diq.city);
                }
            }

            if (!string.IsNullOrEmpty(diq.district))
            {
                if (diq.district.Length > 64)
                {
                    Logger.Trace(Logger.__INFO__, "diq.district参数长度有误");
                    return -2;
                }
                else
                {
                    sqlSub += string.Format(" b.district like '%%{0}%%' and", diq.district);
                }
            }

            if (!string.IsNullOrEmpty(diq.street))
            {
                if (diq.street.Length > 128)
                {
                    Logger.Trace(Logger.__INFO__, "diq.street参数长度有误");
                    return -2;
                }
                else
                {
                    sqlSub += string.Format(" b.street like '%%{0}%%' and", diq.street);
                }
            }


            if (sqlSub != "")
            {
                //去掉最后三个字符"and"
                sqlSub = sqlSub.Remove(sqlSub.Length - 3, 3);
                sql = string.Format("SELECT a.*,b.province,b.city,b.district,b.street FROM (select * from deviceinfo ) AS a INNER JOIN addressinfo As b ON a.sn=b.sn and {0}", sqlSub);
            }
            else
            {
                //无任何过滤的字段
                Logger.Trace(Logger.__INFO__, "deviceinfo_record_entity_get_by_query,无任何过滤的字段");
                sql = string.Format("SELECT a.*,b.province,b.city,b.district,b.street FROM (select * from deviceinfo ) AS a INNER JOIN addressinfo As b ON a.sn=b.sn");
            }
                       
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            DataRow row = dt.NewRow();

                            row[0] = Convert.ToInt32(dr[0]);
                            row[1] = dr[1].ToString();
                            row[2] = dr[2].ToString();
                            row[3] = dr[3].ToString();
                            row[4] = dr[4].ToString();
                            row[5] = dr[5].ToString();
                            row[6] = dr[6].ToString();
                            row[7] = dr[7].ToString();
                            row[8] = dr[8].ToString();
                            row[9] = dr[9].ToString();
                            row[10] = dr[10].ToString();
                            row[11] = dr[11].ToString();
                            row[12] = dr[12].ToString();
                            row[13] = dr[13].ToString();
                            row[14] = dr[14].ToString();
                            row[15] = dr[15].ToString();
                            row[16] = dr[16].ToString();
                            row[17] = dr[17].ToString();
                            row[18] = dr[18].ToString();
                            row[19] = dr[19].ToString();
                            row[20] = dr[20].ToString();
                            row[21] = dr[21].ToString();

                            dt.Rows.Add(row);
                        }
                        dr.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            return 0;
        }

        /// <summary>
        /// 从deviceinfo表设置AP的在线状态为在线
        /// </summary>
        /// <param name="sn">要修改ap的sn号</param>
        /// <returns>
        /// 成功 : true
        /// 失败 : false
        ///        原因包括：数据库尚未连接，参数有误，参数长度有误，
        ///        记录不存在，数据库操作失败等
        /// </returns>
        public bool SetconnHSToOnLine(String sn)
        {
            bool ret = false;

            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return false;
            }

            if (string.IsNullOrEmpty(sn))
            {
                Logger.Trace(Logger.__INFO__, "参数为空");
                return false;
            }

            if (sn.Length > 32)
            {
                Logger.Trace(Logger.__INFO__, "参数长度有误");
                return false;
            }          

            //判断记录是否存在
            if (0 == deviceinfo_record_exist(sn))
            {
                Logger.Trace(Logger.__INFO__, "记录不存在");
                return false;
            }

            string sql = string.Format("update deviceinfo set connHS = '{0}' ,onoffLineTime = now() where sn = '{1}'", "online", sn);
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                { 
                    if (cmd.ExecuteNonQuery() < 0)
                    {
                        Logger.Trace(Logger.__INFO__, sql);
                        ret = false;
                    }
                    else
                    {
                        ret = true;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return false;
            }

            return ret;
        }        

        /// <summary>
        /// 从deviceinfo表设置AP的在线状态为离线
        /// </summary>
        /// <param name="sn">要修改ap的sn号</param>
        /// <returns>
        /// 成功 : true
        /// 失败 : false
        ///        原因包括：数据库尚未连接，参数有误，参数长度有误，
        ///        记录不存在，数据库操作失败等
        /// </returns>
        public bool SetconnHSToOffLine(String sn)
        {
            bool ret = false;

            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return false;
            }

            if (string.IsNullOrEmpty(sn))
            {
                Logger.Trace(Logger.__INFO__, "参数为空");
                return false;
            }

            if (sn.Length > 32)
            {
                Logger.Trace(Logger.__INFO__, "参数长度有误");
                return false;
            }

            //判断记录是否存在
            if (0 == deviceinfo_record_exist(sn))
            {
                Logger.Trace(Logger.__INFO__, "记录不存在");
                return false;
            }

            string sql = string.Format("update deviceinfo set connHS = '{0}' ,onoffLineTime = now() where sn = '{1}'", "offline", sn);
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    if (cmd.ExecuteNonQuery() < 0)
                    {
                        Logger.Trace(Logger.__INFO__, sql);
                        ret = false;
                    }
                    else
                    {
                        ret = true;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return false;
            }

            return ret;
        }

        /// <summary>
        /// 从deviceinfo表查找所有在线AP的sn
        /// </summary>
        /// <returns>
        /// 成功 ：所有在线AP的列表
        /// 失败 ：null
        /// </returns>
        public List<String> GetconnHSByDeviceInfo()
        {
            List<String> listSN = new List<String>();

            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return null;
            }

            string sql = string.Format("select sn from deviceinfo where connHS = '{0}'","online");
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            listSN.Add(dr[0].ToString());                           
                        }
                        dr.Close();
                    }
                }                  
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return null;
            }

            return listSN;
        }

        #endregion
      
        #region 06 - inform_1boot操作
      
        /// <summary>
        /// 判断nodeName对应的记录是否在inform_1boot表中
        /// </summary>
        /// <param name="nodeName">节点名称</param>
        /// <returns> 
        ///   -1 ：数据库尚未打开
        ///   -2 ：参数有误
        ///   -4 ：数据库操作失败 
        ///    0 : 不存在
        ///    1 ：存在
        /// </returns>
        public int inform_1boot_record_exist(string nodeName)
        {
            UInt32 cnt = 0;

            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }

            if (string.IsNullOrEmpty(nodeName))
            {
                Logger.Trace(Logger.__INFO__, "nodeName参数为空");
                return -2;
            }

            if (nodeName.Length > 256)
            {
                Logger.Trace(Logger.__INFO__, "nodeName参数长度有误");
                return -2;
            }

            string sql = string.Format("select count(*) from inform_1boot where nodeName = '{0}'", nodeName);
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            cnt = Convert.ToUInt32(dr[0]);
                        }
                        dr.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            if (cnt <= 0)
            {
                //记录不存在
                return 0;
            }
            else
            {
                //记录存在
                return 1;
            }
        }

        /// <summary>
        /// 插入记录到inform_1boot表中
        /// </summary>
        /// <param name="nodeName">节点名称</param>
        /// <param name="nodeValue">节点值</param>
        /// <returns> 
        ///   -1 ：数据库尚未打开
        ///   -2 ：参数有误
        ///   -3 ：记录已经存在
        ///   -4 ：数据库操作失败 
        ///    0 : 插入成功
        /// </returns>
        public int inform_1boot_record_insert(string nodeName, string nodeValue)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }

            if (string.IsNullOrEmpty(nodeName))
            {
                Logger.Trace(Logger.__INFO__, "nodeName参数为空");
                return -2;
            }

            if (nodeName.Length > 256)
            {
                Logger.Trace(Logger.__INFO__, "nodeName参数长度有误");
                return -2;
            }

            if (string.IsNullOrEmpty(nodeValue))
            {
                Logger.Trace(Logger.__INFO__, "nodeValue参数为空");
                return -2;
            }

            if (nodeValue.Length > 256)
            {
                Logger.Trace(Logger.__INFO__, "nodeValue参数长度有误");
                return -2;
            }

            if (1 == inform_1boot_record_exist(nodeName))
            {
                Logger.Trace(Logger.__INFO__, "nodeName记录已经存在");
                return -3;
            }

            string sql = string.Format("insert into inform_1boot values(NULL,'{0}','{1}')", nodeName, nodeValue);
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    if (cmd.ExecuteNonQuery() < 0)
                    {
                        Logger.Trace(Logger.__INFO__, sql);
                        return -4;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            return 0;
        }

        /// <summary>
        /// 在inform_1boot表中删除指定的nodeName
        /// </summary>
        /// <param name="nodeName">要删除节点名称对应的记录</param>
        /// <returns>
        /// -1 ：数据库尚未打开
        /// -2 ：参数有误
        /// -3 ：用户不存在
        /// -4 ：数据库操作失败 
        ///  0 : 删除成功 
        /// </returns>
        public int inform_1boot_record_delete(string nodeName)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }

            if (string.IsNullOrEmpty(nodeName))
            {
                Logger.Trace(Logger.__INFO__, "nodeName参数为空");
                return -2;
            }

            if (nodeName.Length > 256)
            {
                Logger.Trace(Logger.__INFO__, "nodeName参数长度有误");
                return -2;
            }


            if (0 == inform_1boot_record_exist(nodeName))
            {
                Logger.Trace(Logger.__INFO__, "nodeName记录不存在");
                return -3;
            }

            string sql = string.Format("delete from inform_1boot where nodeName = '{0}'", nodeName);
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    if (cmd.ExecuteNonQuery() < 0)
                    {
                        Logger.Trace(Logger.__INFO__, sql);
                        return -4;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            return 0;
        }

        /// <summary>
        ///  返回inform_1boot表中的所有(nodeName,nodeValue)列
        /// </summary>
        /// <param name="dt">返回的DataTable</param>
        /// <returns>
        ///   -1 ：数据库尚未打开
        ///   -4 ：数据库操作失败 
        ///    0 : 获取成功          
        /// </returns>
        public int inform_1boot_record_entity_get(ref DataTable dt)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }

            dt = new DataTable("inform_1boot");

            DataColumn column0 = new DataColumn();
            column0.DataType = System.Type.GetType("System.String");
            column0.ColumnName = "nodeName";

            DataColumn column1 = new DataColumn();
            column1.DataType = System.Type.GetType("System.String");
            column1.ColumnName = "nodeValue";

            dt.Columns.Add(column0);
            dt.Columns.Add(column1);

            string sql = string.Format("select nodeName,nodeValue from inform_1boot");
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            DataRow row = dt.NewRow();
                         
                            row[0] = dr[0].ToString();
                            row[1] = dr[1].ToString();                           

                            dt.Rows.Add(row);
                        }
                        dr.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            return 0;
        }

        #endregion

        #region 07 - loginfo操作

        //暂时留空

        #endregion

        #region 08 - parameterinfo操作

        /// <summary>
        /// 判断name对应的记录是否在parameterinfo表中
        /// </summary>
        /// <param name="name">功能名称</param>
        /// <returns> 
        ///   -1 ：数据库尚未打开
        ///   -2 ：参数有误
        ///   -4 ：数据库操作失败 
        ///    0 : 不存在
        ///    1 ：存在
        /// </returns>
        public int parameterinfo_record_exist(string name)
        {
            UInt32 cnt = 0;

            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }

            if (string.IsNullOrEmpty(name))
            {
                Logger.Trace(Logger.__INFO__, "name参数为空");
                return -2;
            }

            if (name.Length > 256)
            {
                Logger.Trace(Logger.__INFO__, "name参数长度有误");
                return -2;
            }

            string sql = string.Format("select count(*) from parameterinfo where name = '{0}'", name);
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            cnt = Convert.ToUInt32(dr[0]);
                        }
                        dr.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            if (cnt <= 0)
            {
                //记录不存在
                return 0;
            }
            else
            {
                //记录存在
                return 1;
            }
        }

        /// <summary>
        /// 插入记录到parameterinfo表中
        /// </summary>
        /// <param name="name">参数的英文名称</param>
        /// <param name="des">参数的中文名称</param>
        /// <param name="readable">
        /// 是否可读
        /// 1 ： 可读
        /// 0 ： 不可读
        /// </param>
        /// <param name="writable">
        /// 是否可写
        /// 1 ： 可写
        /// 0 ： 不可写
        /// </param>
        /// <param name="type">参数的类型</param>
        /// <returns> 
        ///   -1 ：数据库尚未打开
        ///   -2 ：参数有误
        ///   -3 ：记录已经存在
        ///   -4 ：数据库操作失败 
        ///    0 : 插入成功
        /// </returns>
        public int parameterinfo_record_insert(string name, string des, int readable, int writable,string type)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }

            if (string.IsNullOrEmpty(name))
            {
                Logger.Trace(Logger.__INFO__, "name参数为空");
                return -2;
            }

            if (name.Length > 256)
            {
                Logger.Trace(Logger.__INFO__, "name参数长度有误");
                return -2;
            }

            if (string.IsNullOrEmpty(des))
            {
                Logger.Trace(Logger.__INFO__, "des参数为空");
                return -2;
            }

            if (des.Length > 256)
            {
                Logger.Trace(Logger.__INFO__, "des参数长度有误");
                return -2;
            }


            if (readable != 1 && readable != 0)
            {
                Logger.Trace(Logger.__INFO__, "adminEnabled必须为1或0");
                return -2;
            }

            if (writable != 1 && writable != 0)
            {
                Logger.Trace(Logger.__INFO__, "operEnabled必须为1或0");
                return -2;
            }


            if (string.IsNullOrEmpty(type))
            {
                Logger.Trace(Logger.__INFO__, "type参数为空");
                return -2;
            }

            if (type.Length > 256)
            {
                Logger.Trace(Logger.__INFO__, "type参数长度有误");
                return -2;
            }


            if (1 == parameterinfo_record_exist(name))
            {
                Logger.Trace(Logger.__INFO__, "name记录已经存在");
                return -3;
            }

            string sql = string.Format("insert into parameterinfo values(NULL,'{0}','{1}',{2},{3},'{4}')", name, des, readable, writable,type);
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    if (cmd.ExecuteNonQuery() < 0)
                    {
                        Logger.Trace(Logger.__INFO__, sql);
                        return -4;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            return 0;
        }

        /// <summary>
        /// 在parameterinfo表中删除指定的name
        /// </summary>
        /// <param name="funName">要删除的参数名称</param>
        /// <returns>
        /// -1 ：数据库尚未打开
        /// -2 ：参数有误
        /// -3 ：用户不存在
        /// -4 ：数据库操作失败 
        ///  0 : 删除成功 
        /// </returns>
        public int parameterinfo_record_delete(string name)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }

            if (string.IsNullOrEmpty(name))
            {
                Logger.Trace(Logger.__INFO__, "name参数为空");
                return -2;
            }

            if (name.Length > 256)
            {
                Logger.Trace(Logger.__INFO__, "name参数长度有误");
                return -2;
            }

            if (0 == parameterinfo_record_exist(name))
            {
                Logger.Trace(Logger.__INFO__, "name记录不存在");
                return -3;
            }

            string sql = string.Format("delete from parameterinfo where name = '{0}'", name);
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    if (cmd.ExecuteNonQuery() < 0)
                    {
                        Logger.Trace(Logger.__INFO__, sql);
                        return -4;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            return 0;
        }

        /// <summary>
        ///  返回parameterinfo表中的所有记录
        /// </summary>
        /// <param name="dt">返回的DataTable</param>
        /// <returns>
        ///   -1 ：数据库尚未打开
        ///   -4 ：数据库操作失败 
        ///    0 : 获取成功          
        /// </returns>
        public int parameterinfo_record_entity_get(ref DataTable dt)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }


            dt = new DataTable("parameterinfo");

            DataColumn column0 = new DataColumn();
            column0.DataType = System.Type.GetType("System.Int32");
            column0.ColumnName = "id";

            DataColumn column1 = new DataColumn();
            column1.DataType = System.Type.GetType("System.String");
            column1.ColumnName = "name";

            DataColumn column2 = new DataColumn();
            column2.DataType = System.Type.GetType("System.String");
            column2.ColumnName = "des";

            DataColumn column3 = new DataColumn();
            column3.DataType = System.Type.GetType("System.Int32");
            column3.ColumnName = "readable";

            DataColumn column4 = new DataColumn();
            column4.DataType = System.Type.GetType("System.Int32");
            column4.ColumnName = "writable";

            DataColumn column5 = new DataColumn();
            column5.DataType = System.Type.GetType("System.String");
            column5.ColumnName = "type";

            dt.Columns.Add(column0);
            dt.Columns.Add(column1);
            dt.Columns.Add(column2);
            dt.Columns.Add(column3);
            dt.Columns.Add(column4);
            dt.Columns.Add(column5);

            string sql = string.Format("select * from parameterinfo");
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            DataRow row = dt.NewRow();

                            row[0] = int.Parse(dr[0].ToString().Trim());

                            row[1] = dr[1].ToString().Trim();
                            row[2] = dr[2].ToString().Trim();

                            row[3] = int.Parse(dr[3].ToString().Trim());
                            row[4] = int.Parse(dr[4].ToString().Trim());

                            row[5] = dr[5].ToString().Trim();

                            dt.Rows.Add(row);
                        }
                        dr.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            return 0;
        }

        #endregion

        #region 09 - permiinfo操作
       
        /// <summary>
        /// 判断funName对应的记录是否在permiinfo表中
        /// </summary>
        /// <param name="funName">功能名称</param>
        /// <returns> 
        ///   -1 ：数据库尚未打开
        ///   -2 ：参数有误
        ///   -4 ：数据库操作失败 
        ///    0 : 不存在
        ///    1 ：存在
        /// </returns>
        public int permiinfo_record_exist(string funName)
        {
            UInt32 cnt = 0;

            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }

            if (string.IsNullOrEmpty(funName))
            {
                Logger.Trace(Logger.__INFO__, "funName参数为空");
                return -2;
            }

            if (funName.Length > 64)
            {
                Logger.Trace(Logger.__INFO__, "funName参数长度有误");
                return -2;
            }

            string sql = string.Format("select count(*) from permiinfo where funName = '{0}'", funName);
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            cnt = Convert.ToUInt32(dr[0]);
                        }
                        dr.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            if (cnt <= 0)
            {
                //记录不存在
                return 0;
            }
            else
            {
                //记录存在
                return 1;
            }
        }

        /// <summary>
        /// 插入记录到permiinfo表中
        /// </summary>
        /// <param name="funName">功能名称</param>
        /// <param name="aliasName">功能别名</param>
        /// <param name="groupBy">所属组</param>
        /// <param name="saEnabled">超级管理员是否可用，取值为1</param>
        /// <param name="adminEnabled">管理员是否可用，取值为0或1</param>
        /// <param name="operEnabled">操作员是否可用，取值为0或1</param>
        /// <param name="des">功能描述</param>
        /// <returns> 
        ///   -1 ：数据库尚未打开
        ///   -2 ：参数有误
        ///   -3 ：记录已经存在
        ///   -4 ：数据库操作失败 
        ///    0 : 插入成功
        /// </returns>
        public int permiinfo_record_insert(string funName, string aliasName,string groupBy,int saEnabled,int adminEnabled,int operEnabled,string des)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }

            if (string.IsNullOrEmpty(funName))
            {
                Logger.Trace(Logger.__INFO__, "funName参数为空");
                return -2;
            }

            if (funName.Length > 64)
            {
                Logger.Trace(Logger.__INFO__, "funName参数长度有误");
                return -2;
            }

            if (string.IsNullOrEmpty(aliasName))
            {
                Logger.Trace(Logger.__INFO__, "aliasName参数为空");
                return -2;
            }

            if (aliasName.Length > 64)
            {
                Logger.Trace(Logger.__INFO__, "aliasName参数长度有误");
                return -2;
            }

            if (string.IsNullOrEmpty(groupBy))
            {
                Logger.Trace(Logger.__INFO__, "groupBy参数为空");
                return -2;
            }

            if (groupBy.Length > 64)
            {
                Logger.Trace(Logger.__INFO__, "groupBy参数长度有误");
                return -2;
            }

            if (string.IsNullOrEmpty(des))
            {
                Logger.Trace(Logger.__INFO__, "des参数为空");
                return -2;
            }

            if (des.Length > 256)
            {
                Logger.Trace(Logger.__INFO__, "des参数长度有误");
                return -2;
            }


            if (saEnabled != 1)
            {
                Logger.Trace(Logger.__INFO__, "saEnabled必须为1");
            }

            if (adminEnabled != 1 && adminEnabled != 0)
            {
                Logger.Trace(Logger.__INFO__, "adminEnabled必须为1或0");
                return -2;
            }

            if (operEnabled != 1 && operEnabled != 0)
            {
                Logger.Trace(Logger.__INFO__, "operEnabled必须为1或0");
                return -2;
            }


            if (1 == permiinfo_record_exist(funName))
            {
                Logger.Trace(Logger.__INFO__, "funName记录已经存在");
                return -3;
            }

            string sql = string.Format("insert into permiinfo values(NULL,'{0}','{1}','{2}',{3},{4},{5},'{6}')",funName, aliasName,groupBy,saEnabled,adminEnabled,operEnabled,des);
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    if (cmd.ExecuteNonQuery() < 0)
                    {
                        Logger.Trace(Logger.__INFO__, sql);
                        return -4;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            return 0;
        }
      
        /// <summary>
        /// 在permiinfo表中删除指定的funName
        /// </summary>
        /// <param name="funName">要删除的功能名称</param>
        /// <returns>
        /// -1 ：数据库尚未打开
        /// -2 ：参数有误
        /// -3 ：用户不存在
        /// -4 ：数据库操作失败 
        ///  0 : 删除成功 
        /// </returns>
        public int permiinfo_record_delete(string funName)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }

            if (string.IsNullOrEmpty(funName))
            {
                Logger.Trace(Logger.__INFO__, "funName参数为空");
                return -2;
            }

            if (funName.Length > 64)
            {
                Logger.Trace(Logger.__INFO__, "funName参数长度有误");
                return -2;
            }


            if (0 == permiinfo_record_exist(funName))
            {
                Logger.Trace(Logger.__INFO__, "funName记录不存在");
                return -3;
            }

            string sql = string.Format("delete from permiinfo where funName = '{0}'", funName);
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    if (cmd.ExecuteNonQuery() < 0)
                    {
                        Logger.Trace(Logger.__INFO__, sql);
                        return -4;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            return 0;
        }

        /// <summary>
        ///  返回permiinfo表中的所有记录
        /// </summary>
        /// <param name="dt">返回的DataTable</param>
        /// <returns>
        ///   -1 ：数据库尚未打开
        ///   -4 ：数据库操作失败 
        ///    0 : 获取成功          
        /// </returns>
        public int permiinfo_record_entity_get(ref DataTable dt)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }


            dt = new DataTable("permiinfo");

            DataColumn column0 = new DataColumn();
            column0.DataType = System.Type.GetType("System.Int32");
            column0.ColumnName = "funId";

            DataColumn column1 = new DataColumn();
            column1.DataType = System.Type.GetType("System.String");
            column1.ColumnName = "funName";

            DataColumn column2 = new DataColumn();
            column2.DataType = System.Type.GetType("System.String");
            column2.ColumnName = "aliasName";

            DataColumn column3 = new DataColumn();
            column3.DataType = System.Type.GetType("System.String");
            column3.ColumnName = "groupBy";

            DataColumn column4 = new DataColumn();
            column4.DataType = System.Type.GetType("System.Int32");
            column4.ColumnName = "saEnabled";

            DataColumn column5 = new DataColumn();
            column5.DataType = System.Type.GetType("System.Int32");
            column5.ColumnName = "adminEnabled";

            DataColumn column6 = new DataColumn();
            column6.DataType = System.Type.GetType("System.Int32");
            column6.ColumnName = "operEnabled";

            DataColumn column7 = new DataColumn();
            column7.DataType = System.Type.GetType("System.String");
            column7.ColumnName = "des";

            dt.Columns.Add(column0);
            dt.Columns.Add(column1);
            dt.Columns.Add(column2);
            dt.Columns.Add(column3);
            dt.Columns.Add(column4);
            dt.Columns.Add(column5);
            dt.Columns.Add(column6);
            dt.Columns.Add(column7);

            string sql = string.Format("select * from permiinfo");
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            DataRow row = dt.NewRow();

                            row[0] = int.Parse(dr[0].ToString());
                            row[1] = dr[1].ToString();
                            row[2] = dr[2].ToString();
                            row[3] = dr[3].ToString();

                            row[4] = int.Parse(dr[4].ToString());
                            row[5] = int.Parse(dr[5].ToString());
                            row[6] = int.Parse(dr[6].ToString());

                            row[7] = dr[7].ToString();

                            dt.Rows.Add(row);
                        }
                        dr.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            return 0;
        }

        #endregion

        #region 10 - schedulerinfo操作

        /// <summary>
        /// 判断对应的actionId是否存在schedulerinfo表中
        /// </summary>
        /// <param name="actionId">动作ID</param>
        /// <returns>
        ///   -1 ：数据库尚未打开
        ///   -2 ：参数有误
        ///   -4 ：数据库操作失败 
        ///    0 : 不存在
        ///    1 ：存在
        /// </returns>
        public int schedulerinfo_record_exist_by_actionId(string actionId)
        {
            UInt32 cnt = 0;

            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }

            if (string.IsNullOrEmpty(actionId))
            {
                Logger.Trace(Logger.__INFO__, "actionId参数为空");
                return -2;
            }

            if (actionId.Length > 64)
            {
                Logger.Trace(Logger.__INFO__, "actionId参数长度有误");
                return -2;
            }

            string sql = string.Format("select count(*) from schedulerinfo where actionId = '{0}'", actionId);
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            cnt = Convert.ToUInt32(dr[0]);
                        }
                        dr.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            if (cnt <= 0)
            {
                //actionId不存在
                return 0;
            }
            else
            {
                //actionId存在
                return 1;
            }
        }

        /// <summary>
        /// 判断对应的actionName是否存在schedulerinfo表中
        /// </summary>
        /// <param name="an">actionName</param>
        /// <returns>
        ///   -1 ：数据库尚未打开
        ///   -2 ：参数有误
        ///   -4 ：数据库操作失败 
        ///    0 : 不存在
        ///    1 ：存在
        /// </returns>
        public int schedulerinfo_record_exist(string an)
        {
            UInt32 cnt = 0;

            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }

            if (string.IsNullOrEmpty(an))
            {
                Logger.Trace(Logger.__INFO__, "参数为空");
                return -2;
            }

            if (an.Length > 64)
            {
                Logger.Trace(Logger.__INFO__, "参数长度有误");
                return -2;
            }

            string sql = string.Format("select count(*) from schedulerinfo where actionName = '{0}'", an);
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            cnt = Convert.ToUInt32(dr[0]);
                        }
                        dr.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            if (cnt <= 0)
            {
                //actionName不存在
                return 0;
            }
            else
            {
                //actionName存在
                return 1;
            }
        }


        /// <summary>
        /// 插入记录到apaction表中
        /// </summary>
        /// <param name="actionName"></param>
        /// <param name="actionId"></param>
        /// <param name="actionType"></param>
        /// <param name="actionXmlText"></param>
        /// <param name="actionCount"></param>
        /// <param name="successCount"></param>
        /// <param name="failCount"></param>
        /// <returns> 
        ///   -1 ：数据库尚未打开
        ///   -2 ：参数有误
        ///   -3 ：记录已经存在
        ///   -4 ：数据库操作失败 
        ///    0 : 插入成功
        /// </returns>
        public int schedulerinfo_record_insert(string actionName,
                                               string actionId,
                                               TaskType actionType,
                                               string actionXmlText,
                                               UInt32 actionCount,
                                               UInt32 successCount,
                                               UInt32 failCount)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }

            if (string.IsNullOrEmpty(actionName) || string.IsNullOrEmpty(actionId) || string.IsNullOrEmpty(actionXmlText))
            {
                Logger.Trace(Logger.__INFO__, "参数为空");
                return -2;
            }

            if (actionName.Length > 64 || actionId.Length > 64 || actionXmlText.Length > 8192)
            {
                Logger.Trace(Logger.__INFO__, "参数长度有误");
                return -2;
            }

            //
            // 不需要判断记录是否存在
            //
            ////判断记录是否存在
            //if (schedulerinfo_record_exist(actionName) == 1)
            //{
            //    Logger.Trace(Logger.__INFO__, "记录已经存在");
            //    return -3;
            //}

            string sql = string.Format("insert into schedulerinfo values(NULL,'{0}','{1}','{2}','{3}','{4}','{5}','{6}',now())", actionName, actionId, (int)actionType, actionXmlText, actionCount, successCount, failCount);
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    if (cmd.ExecuteNonQuery() < 0)
                    {
                        Logger.Trace(Logger.__INFO__, sql);
                        return -4;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            return 0;
        }


        /// <summary>
        /// 在schedulerinfo表中删除指定的actionId
        /// </summary>
        /// <param name="actionId">动作ID</param>
        /// <returns>
        /// -1 ：数据库尚未打开
        /// -2 ：参数有误
        /// -3 ：用户不存在
        /// -4 ：数据库操作失败 
        ///  0 : 删除成功 
        /// </returns>
        public int schedulerinfo_record_delete(string actionId)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }

            if (string.IsNullOrEmpty(actionId))
            {
                Logger.Trace(Logger.__INFO__, "actionId参数为空");
                return -2;
            }

            if (actionId.Length > 64)
            {
                Logger.Trace(Logger.__INFO__, "actionId参数长度有误");
                return -2;
            }


            if (0 == schedulerinfo_record_exist_by_actionId(actionId))
            {
                Logger.Trace(Logger.__INFO__, "actionId记录不存在");
                return -3;
            }

            string sql = string.Format("delete from schedulerinfo where actionId = '{0}'", actionId);
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    if (cmd.ExecuteNonQuery() < 0)
                    {
                        Logger.Trace(Logger.__INFO__, sql);
                        return -4;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            return 0;
        }

        #endregion

        #region 11 - userinfo操作


        /// <summary>
        /// 检查用户记录是否存在
        /// </summary>
        /// <param name="name">用户名</param>
        /// <returns>
        ///   -1 ：数据库尚未打开
        ///   -2 ：参数有误
        ///   -4 ：数据库操作失败 
        ///    0 : 不存在
        ///    1 ：存在
        /// </returns>
        public int userinfo_record_exist(string name)
        {
            UInt32 cnt = 0;

            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }

            if (string.IsNullOrEmpty(name))
            {
                Logger.Trace(Logger.__INFO__, "参数为空");
                return -2;
            }

            string sql = string.Format("select count(*) from userinfo where userName = '{0}'", name);
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            cnt = Convert.ToUInt32(dr[0]);
                        }
                        dr.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            if (cnt > 0)
            {
                return 1;
            }
            else
            {
                return 0;
            }
        }


        /// <summary>
        /// 插入记录到用户信息表中
        /// </summary>
        /// <param name="name">用户名，最长64个字符 </param>
        /// <param name="level">用户类型，operator或administrator </param>
        /// <param name="psw">用户密码（明文），最长32个字符 </param>
        /// <returns>
        ///   -1 ：数据库尚未打开
        ///   -2 ：参数有误
        ///   -3 ：记录已经存在
        ///   -4 ：数据库操作失败 
        ///   -5 : 不能插入超级用户root 
        ///    0 : 插入成功 
        /// </returns>
        public int userinfo_record_insert(string name, string level, string psw)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(level) || string.IsNullOrEmpty(psw))
            {
                Logger.Trace(Logger.__INFO__, "参数为空");
                return -2;
            }

            if (name.Length > 64 || psw.Length > 32)
            {
                Logger.Trace(Logger.__INFO__, "参数长度有误");
                return -2;
            }

            if (level != "operator" && level != "administrator")
            {
                Logger.Trace(Logger.__INFO__, "level类型有误");
                return -2;
            }

            if (name == "root")
            {
                Logger.Trace(Logger.__INFO__, "不能插入root");
                return -5;
            }

            //检查用户是否存在
            if (1 == userinfo_record_exist(name))
            {
                Logger.Trace(Logger.__INFO__, "记录已经存在");
                return -3;
            }

            string sql = string.Format("insert into userinfo values(NULL,'{0}','{1}','MD5({2})',now())", name, level, psw);
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    if (cmd.ExecuteNonQuery() < 0)
                    {
                        Logger.Trace(Logger.__INFO__, sql);
                        return -4;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            return 0;
        }


        /// <summary>
        /// 在用户信息表中删除指定的用户 
        /// </summary>
        /// <param name="name">用户名</param>
        /// <param name="level">用户类型，operator或administrator </param>
        /// <returns>
        /// -1 ：数据库尚未打开
        /// -2 ：参数有误
        /// -3 ：用户不存在
        /// -4 ：数据库操作失败 
        /// -5 ：超级用户不能删除 
        ///  0 : 删除成功 
        /// </returns>
        public int userinfo_record_delete(string name, string level)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(level))
            {
                Logger.Trace(Logger.__INFO__, "参数为空");
                return -2;
            }

            if (name.Length > 64)
            {
                Logger.Trace(Logger.__INFO__, "参数长度有误");
                return -2;
            }

            if (level != "operator" && level != "administrator")
            {
                Logger.Trace(Logger.__INFO__, "level类型有误");
                return -2;
            }

            if (name == "root")
            {
                Logger.Trace(Logger.__INFO__, "超级用户不能删除 ");
                return -5;
            }

            //检查用户是否存在
            if (0 == userinfo_record_exist(name))
            {
                Logger.Trace(Logger.__INFO__, "用户不存在");
                return -3;
            }

            string sql = string.Format("delete from userinfo where userName = '{0}' and  level = '{1}'", name, level);
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    if (cmd.ExecuteNonQuery() < 0)
                    {
                        Logger.Trace(Logger.__INFO__, sql);
                        return -4;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            return 0;
        }


        /// <summary>
        /// 在用户信息表中验证指定的用户 
        /// </summary>
        /// <param name="name">用户名</param>
        /// <param name="psw">用户密码</param>
        /// <param name="level">输出用户类型，operator，administrator，或superadmin </param>
        /// <returns>
        /// -1 ：数据库尚未打开
        /// -2 ：参数有误
        /// -3 ：验证失败，密码有误
        /// -4 ：数据库操作失败 
        /// 0  : 验证成功，用户合法，并返回level
        /// </returns>
        public int userinfo_record_check(string name, string psw, ref string level)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(psw))
            {
                Logger.Trace(Logger.__INFO__, "参数为空");
                return -2;
            }

            if (name.Length > 64 || psw.Length > 32)
            {
                Logger.Trace(Logger.__INFO__, "参数长度有误");
                return -2;
            }

            //检查用户是否存在
            if (0 == userinfo_record_exist(name))
            {
                Logger.Trace(Logger.__INFO__, "用户不存在");
                return -3;
            }

            string sql = string.Format("select level from userinfo where userName = '{0}' and psw = 'MD5({1})'", name, psw);
            try
            {
                level = "";
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            level = dr[0].ToString();
                        }
                        dr.Close();
                    }
                }

                if (level == "")
                {
                    Logger.Trace(Logger.__INFO__, "密码有误");
                    return -3;
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            return 0;
        }


        /// <summary>
        /// 在用户信息表中修改用户的密码 
        /// </summary>
        /// <param name="name">用户名</param>
        /// <param name="oldPsw">用户的老密码</param>
        /// <param name="newPsw">用户的新密码</param>
        /// <returns>
        ///  返回值 ：
        /// -1 ：数据库尚未打开
        /// -2 ：参数有误
        /// -3 ：用户不存在
        /// -4 ：数据库操作失败 
        /// -5 ：用户和老密码不匹配 
        ///  0 : 更新密码成功             
        /// </returns>
        public int userinfo_record_update(string name, string oldPsw, string newPsw)
        {
            UInt32 cnt = 0;

            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(oldPsw) || string.IsNullOrEmpty(newPsw))
            {
                Logger.Trace(Logger.__INFO__, "参数为空");
                return -2;
            }

            if (name.Length > 64)
            {
                Logger.Trace(Logger.__INFO__, "参数长度有误");
                return -2;
            }

            //检查用户是否存在
            if (0 == userinfo_record_exist(name))
            {
                Logger.Trace(Logger.__INFO__, "用户不存在");
                return -3;
            }

            string sql = string.Format("select count(*) from userinfo where userName = '{0}' and psw = 'MD5({1})'", name, oldPsw);
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            cnt = Convert.ToUInt32(dr[0]);
                        }
                        dr.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            //用户和老密码不匹配 
            if (cnt <= 0)
            {
                Logger.Trace(Logger.__INFO__, "用户和老密码不匹配");
                return -5;
            }

            sql = string.Format("update userinfo set psw = 'MD5({0})' ,operTime = now() where userName = '{1}'", newPsw, name);
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    if (cmd.ExecuteNonQuery() < 0)
                    {
                        Logger.Trace(Logger.__INFO__, sql);
                        return -4;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            return 0;
        }


        /// <summary>
        /// 获取用户表中的各条记录
        /// </summary>
        /// <param name="dt">
        /// 返回的DataTable，包含的列为：id,userName,level,operTime
        /// </param>
        /// <returns>
        /// -1 ：数据库尚未打开
        /// -4 ：数据库操作失败 
        ///  0 : 查询成功 
        /// </returns>
        public int userinfo_record_entity_get(ref DataTable dt)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }

            dt = new DataTable("userInfo");

            DataColumn column0 = new DataColumn();
            column0.DataType = System.Type.GetType("System.Int32");
            column0.ColumnName = "id";

            DataColumn column1 = new DataColumn();
            column1.DataType = System.Type.GetType("System.String");
            column1.ColumnName = "userName";

            DataColumn column2 = new DataColumn();
            column2.DataType = System.Type.GetType("System.String");
            column2.ColumnName = "level";

            DataColumn column3 = new DataColumn();
            column3.DataType = System.Type.GetType("System.String");
            column3.ColumnName = "operTime";

            dt.Columns.Add(column0);
            dt.Columns.Add(column1);
            dt.Columns.Add(column2);
            dt.Columns.Add(column3);

            string sql = string.Format("select id,userName,level,operTime from userinfo");
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            DataRow row = dt.NewRow();
                            row[0] = Convert.ToInt32(dr[0]);
                            row[1] = dr[1].ToString();
                            row[2] = dr[2].ToString();
                            row[3] = dr[3].ToString();

                            dt.Rows.Add(row);
                        }
                        dr.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            return 0;
        }

        #endregion

        #region 12 - versioninfo操作

        /// <summary>
        /// 判断versionNo对应的记录是否在versioninfo表中
        /// </summary>
        /// <param name="versionNo">版本号</param>
        /// <returns> 
        ///   -1 ：数据库尚未打开
        ///   -2 ：参数有误
        ///   -4 ：数据库操作失败 
        ///    0 : 不存在
        ///    1 ：存在
        /// </returns>
        public int versioninfo_record_exist(string versionNo)
        {
            UInt32 cnt = 0;

            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }

            if (string.IsNullOrEmpty(versionNo))
            {
                Logger.Trace(Logger.__INFO__, "versionNo参数为空");
                return -2;
            }

            if (versionNo.Length > 128)
            {
                Logger.Trace(Logger.__INFO__, "versionNo参数长度有误");
                return -2;
            }

            string sql = string.Format("select count(*) from versioninfo where versionNo = '{0}'", versionNo);
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            cnt = Convert.ToUInt32(dr[0]);
                        }
                        dr.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            if (cnt <= 0)
            {
                //记录不存在
                return 0;
            }
            else
            {
                //记录存在
                return 1;
            }
        }

        /// <summary>
        /// 插入记录到versioninfo表中
        /// </summary>
        /// <param name="versionNo">版本号</param>
        /// <param name="uploadUser">上传用户</param>
        /// <param name="uploadTime">上传时间</param>
        /// <param name="applicableDevice">可使用的设备（SN）</param>
        /// <param name="patchName">patch名称</param>
        /// <param name="fileSize">文件大小</param>
        /// <param name="des">描述</param>
        /// <returns> 
        ///   -1 ：数据库尚未打开
        ///   -2 ：参数有误
        ///   -3 ：记录已经存在
        ///   -4 ：数据库操作失败 
        ///   -5 ：用户名不存在
        ///    0 : 插入成功
        /// </returns>
        public int versioninfo_record_insert(string versionNo,string uploadUser,string uploadTime,string applicableDevice,string patchName,UInt32 fileSize,string des)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }

            if (string.IsNullOrEmpty(versionNo))
            {
                Logger.Trace(Logger.__INFO__, "versionNo参数为空");
                return -2;
            }

            if (versionNo.Length > 128)
            {
                Logger.Trace(Logger.__INFO__, "versionNo参数长度有误");
                return -2;
            }

            if (string.IsNullOrEmpty(uploadUser))
            {
                Logger.Trace(Logger.__INFO__, "uploadUser参数为空");
                return -2;
            }

            if (uploadUser.Length > 32)
            {
                Logger.Trace(Logger.__INFO__, "uploadUser参数长度有误");
                return -2;
            }

            if (string.IsNullOrEmpty(uploadTime))
            {
                Logger.Trace(Logger.__INFO__, "uploadTime参数为空");
                return -2;
            }

            if (uploadTime.Length > 32)
            {
                Logger.Trace(Logger.__INFO__, "uploadTime参数长度有误");
                return -2;
            }


            if (string.IsNullOrEmpty(applicableDevice))
            {
                Logger.Trace(Logger.__INFO__, "applicableDevice参数为空");
                return -2;
            }

            if (applicableDevice.Length > 32)
            {
                Logger.Trace(Logger.__INFO__, "applicableDevice参数长度有误");
                return -2;
            }

            if (string.IsNullOrEmpty(patchName))
            {
                Logger.Trace(Logger.__INFO__, "patchName参数为空");
                return -2;
            }

            if (patchName.Length > 128)
            {
                Logger.Trace(Logger.__INFO__, "patchName参数长度有误");
                return -2;
            }

            if (string.IsNullOrEmpty(des))
            {
                Logger.Trace(Logger.__INFO__, "des参数为空");
                return -2;
            }

            if (des.Length > 1024)
            {
                Logger.Trace(Logger.__INFO__, "des参数长度有误");
                return -2;
            }


            // 判断版本是否已经存在
            if (1 == versioninfo_record_exist(versionNo))
            {
                Logger.Trace(Logger.__INFO__, "versionNo记录已经存在");
                return -3;
            }

            // 判断用户是否已经存在
            if (0 == userinfo_record_exist(uploadUser))
            {
                Logger.Trace(Logger.__INFO__, "用户名uploadUser不存在");
                return -3;
            }


            string sql = string.Format("insert into versioninfo values(NULL,'{0}','{1}','{2}','{3}','{4}',{5},'{6}')", versionNo,uploadUser,uploadTime,applicableDevice,patchName,fileSize,des);
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    if (cmd.ExecuteNonQuery() < 0)
                    {
                        Logger.Trace(Logger.__INFO__, sql);
                        return -4;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            return 0;
        }

        /// <summary>
        /// 在versioninfo表中删除指定的versionNo
        /// </summary>
        /// <param name="versionNo">版本号</param>
        /// <returns>
        /// -1 ：数据库尚未打开
        /// -2 ：参数有误
        /// -3 ：用户不存在
        /// -4 ：数据库操作失败 
        ///  0 : 删除成功 
        /// </returns>
        public int versioninfo_record_delete(string versionNo)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }

            if (string.IsNullOrEmpty(versionNo))
            {
                Logger.Trace(Logger.__INFO__, "versionNo参数为空");
                return -2;
            }

            if (versionNo.Length > 128)
            {
                Logger.Trace(Logger.__INFO__, "versionNo参数长度有误");
                return -2;
            }


            if (0 == versioninfo_record_exist(versionNo))
            {
                Logger.Trace(Logger.__INFO__, "versionNo记录不存在");
                return -3;
            }

            string sql = string.Format("delete from versioninfo where versionNo = '{0}'", versionNo);
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    if (cmd.ExecuteNonQuery() < 0)
                    {
                        Logger.Trace(Logger.__INFO__, sql);
                        return -4;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            return 0;
        }

        /// <summary>
        ///  返回versioninfo表中的所有记录
        /// </summary>
        /// <param name="dt">返回的DataTable</param>
        /// <returns>
        ///   -1 ：数据库尚未打开
        ///   -4 ：数据库操作失败 
        ///    0 : 获取成功          
        /// </returns>
        public int versioninfo_record_entity_get(ref DataTable dt)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }


            dt = new DataTable("versioninfo");

            DataColumn column0 = new DataColumn();
            column0.DataType = System.Type.GetType("System.Int32");
            column0.ColumnName = "id";

            DataColumn column1 = new DataColumn();
            column1.DataType = System.Type.GetType("System.String");
            column1.ColumnName = "versionNo";
      
            DataColumn column2 = new DataColumn();
            column2.DataType = System.Type.GetType("System.String");
            column2.ColumnName = "uploadUser";

            DataColumn column3 = new DataColumn();
            column3.DataType = System.Type.GetType("System.String");
            column3.ColumnName = "uploadTime";

            DataColumn column4 = new DataColumn();
            column4.DataType = System.Type.GetType("System.String");
            column4.ColumnName = "applicableDevice";

            DataColumn column5 = new DataColumn();
            column5.DataType = System.Type.GetType("System.String");
            column5.ColumnName = "patchName";

            DataColumn column6 = new DataColumn();
            column6.DataType = System.Type.GetType("System.Int32");
            column6.ColumnName = "fileSize";

            DataColumn column7 = new DataColumn();
            column7.DataType = System.Type.GetType("System.String");
            column7.ColumnName = "des";

            dt.Columns.Add(column0);
            dt.Columns.Add(column1);
            dt.Columns.Add(column2);
            dt.Columns.Add(column3);
            dt.Columns.Add(column4);
            dt.Columns.Add(column5);
            dt.Columns.Add(column6);
            dt.Columns.Add(column7);

            string sql = string.Format("select * from versioninfo");
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            DataRow row = dt.NewRow();

                            row[0] = int.Parse(dr[0].ToString());
                            
                            row[1] = dr[1].ToString();
                            row[2] = dr[2].ToString();
                            row[3] = dr[3].ToString();
                            row[3] = dr[4].ToString();
                            row[3] = dr[5].ToString();

                            row[6] = int.Parse(dr[6].ToString());

                            row[7] = dr[7].ToString();

                            dt.Rows.Add(row);
                        }
                        dr.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            return 0;
        }

        
        /// <summary>
        /// 通过diq中的过滤信息，获取对应的记录集合
        /// </summary>
        /// <param name="dt">
        /// 返回的DataTable，包含的列为：id,versionNo,uploadUser,
        /// uploadTime,applicableDevice,patchName,fileSize,des
        /// </param>
        /// <param name="viq">
        /// （1） 各个字段的值表示过滤或包含的信息
        /// （2） string类型字段为null或""时表示不过滤该字段
        /// （3） UInt32类型字段为0表示不过滤该字段
        /// </param>
        /// <returns>
        /// -1 ：数据库尚未打开
        /// -4 ：数据库操作失败 
        ///  0 : 查询成功 
        /// </returns>
        public int versioninfo_record_entity_get_by_query(ref DataTable dt, structVersionInfoQuery viq)
        {
            string sqlSub = "";
            string sql = "";

            string startTime = "";
            string endTime = "";

            UInt32 sizeStart = 0;
            UInt32 sizeEnd = 0;

            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }

            dt = new DataTable("versioninfo");

            DataColumn column0 = new DataColumn();
            column0.DataType = System.Type.GetType("System.Int32");
            column0.ColumnName = "id";

            DataColumn column1 = new DataColumn();
            column1.DataType = System.Type.GetType("System.String");
            column1.ColumnName = "versionNo";

            DataColumn column2 = new DataColumn();
            column2.DataType = System.Type.GetType("System.String");
            column2.ColumnName = "uploadUser";

            DataColumn column3 = new DataColumn();
            column3.DataType = System.Type.GetType("System.String");
            column3.ColumnName = "uploadTime";

            DataColumn column4 = new DataColumn();
            column4.DataType = System.Type.GetType("System.String");
            column4.ColumnName = "applicableDevice";

            DataColumn column5 = new DataColumn();
            column5.DataType = System.Type.GetType("System.String");
            column5.ColumnName = "patchName";

            DataColumn column6 = new DataColumn();
            column6.DataType = System.Type.GetType("System.Int32");
            column6.ColumnName = "fileSize";

            DataColumn column7 = new DataColumn();
            column7.DataType = System.Type.GetType("System.String");
            column7.ColumnName = "des";

            dt.Columns.Add(column0);
            dt.Columns.Add(column1);
            dt.Columns.Add(column2);
            dt.Columns.Add(column3);
            dt.Columns.Add(column4);
            dt.Columns.Add(column5);
            dt.Columns.Add(column6);
            dt.Columns.Add(column7);

            if (!string.IsNullOrEmpty(viq.versionNo))
            {
                if (viq.versionNo.Length > 128)
                {
                    Logger.Trace(Logger.__INFO__, "viq.versionNo参数长度有误");
                    return -2;
                }
                else
                {
                    sqlSub += string.Format(" versionNo like '%%{0}%%' and", viq.versionNo);
                }
            }

            if (!string.IsNullOrEmpty(viq.uploadUser))
            {
                if (viq.uploadUser.Length > 32)
                {
                    Logger.Trace(Logger.__INFO__, "viq.uploadUser参数长度有误");
                    return -2;
                }
                else
                {
                    sqlSub += string.Format(" uploadUser like '%%{0}%%' and", viq.uploadUser);
                }
            }


            if (string.IsNullOrEmpty(viq.uploadTime_StartTime))
            {
                startTime = "";
            }
            else
            {
                try
                {
                    DateTime.Parse(viq.uploadTime_StartTime);
                }
                catch
                {
                    Logger.Trace(Logger.__INFO__, "viq.uploadTime_StartTime参数格式有误");
                    return -2;
                }

                startTime = viq.uploadTime_StartTime;
            }

            if (string.IsNullOrEmpty(viq.uploadTime_EndTime))
            {
                //赋一个很大的值
                endTime = "2100-01-01 12:34:56";
            }
            else
            {
                try
                {
                    DateTime.Parse(viq.uploadTime_EndTime);
                }
                catch
                {
                    Logger.Trace(Logger.__INFO__, "viq.uploadTime_EndTime参数格式有误");
                    return -2;
                }

                endTime = viq.uploadTime_EndTime;
            }

            sqlSub += string.Format(" uploadTime >= '{0}' and uploadTime <= '{1}' and", startTime, endTime);


            if (!string.IsNullOrEmpty(viq.applicableDevice))
            {
                if (viq.applicableDevice.Length > 32)
                {
                    Logger.Trace(Logger.__INFO__, "viq.applicableDevice参数长度有误");
                    return -2;
                }
                else
                {
                    sqlSub += string.Format(" applicableDevice like '%%{0}%%' and", viq.applicableDevice);
                }
            }


            if (!string.IsNullOrEmpty(viq.patchName))
            {
                if (viq.patchName.Length > 128)
                {
                    Logger.Trace(Logger.__INFO__, "viq.patchName参数长度有误");
                    return -2;
                }
                else
                {
                    sqlSub += string.Format(" patchName like '%%{0}%%' and", viq.patchName);
                }
            }


            if ( 0 == viq.fileSize_Start)
            {
                sizeStart = 0;
            }
            else
            {
                sizeStart = viq.fileSize_Start;
            }

            if (0 == viq.fileSize_End)
            {
                sizeEnd = UInt32.MaxValue;
            }
            else
            {
                sizeEnd = viq.fileSize_End;
            }

            sqlSub += string.Format(" fileSize >= {0} and fileSize <= {1} and", sizeStart, sizeEnd);
   
            if (!string.IsNullOrEmpty(viq.des))
            {
                if (viq.des.Length > 1024)
                {
                    Logger.Trace(Logger.__INFO__, "viq.des参数长度有误");
                    return -2;
                }
                else
                {
                    sqlSub += string.Format(" des like '%%{0}%%' and", viq.des);
                }
            }

            if (sqlSub != "")
            {
                //去掉最后三个字符"and"
                sqlSub = sqlSub.Remove(sqlSub.Length - 3, 3);
                sql = string.Format("select * from versioninfo where {0}", sqlSub);
            }
            else
            {
                //无任何过滤的字段
                Logger.Trace(Logger.__INFO__, "versioninfo_record_entity_get_by_query,无任何过滤的字段");
                sql = string.Format("select * from versioninfo");
            }

            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            DataRow row = dt.NewRow();

                            row[0] = int.Parse(dr[0].ToString());

                            row[1] = dr[1].ToString();
                            row[2] = dr[2].ToString();
                            row[3] = dr[3].ToString();
                            row[3] = dr[4].ToString();
                            row[3] = dr[5].ToString();

                            row[6] = int.Parse(dr[6].ToString());

                            row[7] = dr[7].ToString();

                            dt.Rows.Add(row);
                        }
                        dr.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            return 0;
        }

        #endregion



        #region 13 - tmpvalue操作

        /// <summary>
        /// 向tmpvalue表中插入记录
        /// </summary>
        /// <param name="sn"></param>
        /// <param name="actionId"></param>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public int tmpvalue_record_entity_insert(string sn ,string actionId,string name,string value,string type)
        {
            string sql = string.Format("insert into tmpvalue values(NULL,'{0}','{1}','{2}','{3}','{4}','{5}')", 
                sn, actionId,name,value, type,DateTime.Now.ToString());
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    if (cmd.ExecuteNonQuery() < 0)
                    {
                        Logger.Trace(Logger.__INFO__, sql);
                        return -4;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }
            return 0;
        }

        /// <summary>
        /// 根所sn及任务id返回tmpvalue表中的内容
        /// </summary>
        /// <param name="dt">返回表内容</param>
        /// <param name="sn">sn号</param>
        /// <param name="apactionId">任务Id</param>
        /// <returns></returns>
        public int tmpvalue_record_entity_get_by_query(ref DataTable dt, string sn,string apactionId)
        {
            dt = new DataTable("tmpvalue");

            DataColumn column0 = new DataColumn();
            column0.DataType = System.Type.GetType("System.Int32");
            column0.ColumnName = "id";

            DataColumn column1 = new DataColumn();
            column1.DataType = System.Type.GetType("System.String");
            column1.ColumnName = "sn";

            DataColumn column2 = new DataColumn();
            column2.DataType = System.Type.GetType("System.String");
            column2.ColumnName = "apactionId";

            DataColumn column3 = new DataColumn();
            column3.DataType = System.Type.GetType("System.String");
            column3.ColumnName = "name";

            DataColumn column4 = new DataColumn();
            column4.DataType = System.Type.GetType("System.String");
            column4.ColumnName = "value";

            DataColumn column5 = new DataColumn();
            column5.DataType = System.Type.GetType("System.String");
            column5.ColumnName = "type";

            DataColumn column6 = new DataColumn();
            column6.DataType = System.Type.GetType("System.String");
            column6.ColumnName = "time";

            dt.Columns.Add(column0);
            dt.Columns.Add(column1);
            dt.Columns.Add(column2);
            dt.Columns.Add(column3);
            dt.Columns.Add(column4);
            dt.Columns.Add(column5);
            dt.Columns.Add(column6);

            string sql = string.Format("select * from tmpvalue where sn='{0}' and apactionId='{1}'",sn, apactionId);

            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            DataRow row = dt.NewRow();

                            row[0] = int.Parse(dr[0].ToString());

                            row[1] = dr[1].ToString();
                            row[2] = dr[2].ToString();
                            row[3] = dr[3].ToString();
                            row[4] = dr[4].ToString();
                            row[5] = dr[5].ToString();

                            row[6] = dr[6].ToString();

                            dt.Rows.Add(row);
                        }
                        dr.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            return 0;
        }

        /// <summary>
        /// 在tmpvalue表中删除指定任务返加的值
        /// </summary>
        /// <param name="actionId">任务Id</param>
        /// <returns>
        /// -1 ：数据库尚未打开
        /// -2 ：参数有误
        /// -3 ：用户不存在
        /// -4 ：数据库操作失败 
        ///  0 : 删除成功 
        /// </returns>
        public int tmpvalue_record_delete(string actionId)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(Logger.__INFO__, "数据库尚未连接");
                return -1;
            }

            if (string.IsNullOrEmpty(actionId))
            {
                Logger.Trace(Logger.__INFO__, "actionId参数为空");
                return -2;
            }

            if (actionId.Length > 64)
            {
                Logger.Trace(Logger.__INFO__, "actionId参数长度有误");
                return -2;
            }


            if (0 == versioninfo_record_exist(actionId))
            {
                Logger.Trace(Logger.__INFO__, "actionId记录不存在");
                return -3;
            }

            string sql = string.Format("delete from tmpvalue where actionId = '{0}'", actionId);
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    if (cmd.ExecuteNonQuery() < 0)
                    {
                        Logger.Trace(Logger.__INFO__, sql);
                        return -4;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            return 0;
        }
        
        
        #endregion
    }
}
