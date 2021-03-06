
/************************************* 修改记录 ***********************************************
  
    01、添加数据库的各种接口
                                            jianbinbz 2017-12-18
  
    02、添加Log操作类Logger
    
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
 
      03、完善数据库的各种接口                        
                                               jianbinbz 2018-01-29
 
      04、添加表parameterinfo和aploginfo的各种接口                        
                                               jianbinbz 2018-02-02
  
      05、在GetTaskBySN接口中添加string.IsNullOrEmpty(dr[1/3/5/7/9].ToString())的判断
          修改apaction_record_insert接口，使得插入记录时各个status为0                                 
                                      jianbinbz 2018-02-26
 
      06、 修改Logger如下：
          （1）添加LogRootDirectory属性
          （2）增加记录Log时进行互斥操作
                                      jianbinbz 2018-02-28
 
      07、 增加省市区的操作
                                      jianbinbz 2018-03-13
 
      08、 修改用于专网
                                      jianbinbz 2019-01-04
 
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
using System.Threading;
using Newtonsoft.Json;
using System.Runtime.CompilerServices;

namespace httpServer
{
    #region 类外定义

    //日志信息类型
    public enum LogInfoType
    {
        DEBG = 0,
        INFO = 1,
        WARN = 2,
        EROR = 3, 
    }

    public enum LogCategory
    {
        R = 0,   //接收
        S = 1,   //发送
        I = 2    //信息
    }

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
    public struct strDevice
    {
        public int id;
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
        public string onoffLineTime;
        public string aliasName;
        public string des;  

        public string Fullname;          //预留
        public string station_and_name;  //预留     
        public string port;              //预留
        public string netmask;           //预留

        public string affDomainId;
    };


    /// <summary>
    /// alarminfo的结构体
    /// </summary>
    public struct strAlarm
    {
        public int id;             //id
        public string vendor;      //厂商
        public string level;       //告警级别,Critical,Major,Minor,Warning
        public string alarmTime;   //告警时间,输入时不需要带入
	    public string clearTime;   //清除时间,输入时不需要带入     
        public string noticeType;  //通知类型,NewAlarm,ClearAlarm

        public string cause;       //告警原因
        public string flag;        //告警标识，100000,72,54,34等
        public string des;         //告警描述
        public string addDes;      //附加描述
        public string addInfo;     //附加信息
        public string res1;        //保留字段1
        public string res2;        //保留字段2
        public string sn;          //外键，PK
    };


    /// <summary>
    /// alarm查询条件
    /// </summary>
    public struct strAlarmQuery
    {        
        public string timeStart;    /* 
                                     * ""表示不过滤，开始时间
                                     * noticeType="",表示NewAlarm和ClearAlarm的开始时间
                                     * noticeType="NewAlarm",表示NewAlarm的开始时间
                                     * noticeType="ClearAlarm",表示ClearAlarm的开始时间                                     
                                     */ 

        public string timeEnded;    /*
                                     * ""表示不过滤，结束时间  
                                     * noticeType="",表示NewAlarm和ClearAlarm的结束时间
                                     * noticeType="NewAlarm",表示NewAlarm的结束时间
                                     * noticeType="ClearAlarm",表示ClearAlarm的结束时间
                                     */ 

        public string vendor;       // ""表示不过滤，或过滤字符串
        public string level;        // ""表示不过滤，或过滤字符串
        public string noticeType;   // ""表示不过滤，"NewAlarm"或者"ClearAlarm"

        public string cause;        // ""表示不过滤，或过滤字符串
        public string flag;         // ""表示不过滤，或过滤字符串
        public string des;          // ""表示不过滤，或过滤字符串
        public string addDes;       // ""表示不过滤，或过滤字符串
        public string addInfo;      // ""表示不过滤，或过滤字符串
        public string sn;           // ""表示不过滤，或过滤字符串
    };

    
    /// <summary>
    /// strPerformance的结构体
    /// </summary>
    public struct strPerformance
    {
        public int RRC_SuccConnEstab;      //RRC建立成功次数
        public int RRC_AttConnEstab;       //RRC建立请求次数 

        public int ERAB_NbrSuccEstab;       //E-RAB建立成功次数
        public int ERAB_NbrAttEstab;        //E-RAB建立请求次数

        public int ERAB_NbrSuccEstab_1;     //QCI=1的E-RAB建立成功次数
        public int ERAB_NbrAttEstab_1;      //QCI=1的E-RAB建立请求次数

        public int RRC_ConnMax;             //当前UE的接入数

        public int HO_SuccOutInterEnbS1;    //LTE切换成功次数
        public int HO_AttOutInterEnbS1;     //LTE切换请求次数

        public int RRC_ConnReleaseCsfb;     //CSFB次数

        public int PDCP_UpOctUl;            //UL业务量（MB）
        public int PDCP_UpOctDl;            //DL业务量（MB）

        public int HO_SuccOutInterEnbS1_1;   //VoLTE切换成功次数
        public int HO_AttOutInterEnbS1_1;    //VoLTE切换请求次数

        public int HO_SuccOutInterFreq;      //ESRVCC切换成功次数
        public int HO_AttOutExecInterFreq;   //ESRVCC切换请求次数	

        public string timeStart;             //开始时间
        public string timeEnded;             //终止时间
        public string sn;                    //SN号

        public int res1;                     //保留字段1
        public int res2;                     //保留字段2
    };

    /// <summary>
    /// strPerformance统计结构体
    /// </summary>
    public struct strPerforStat
    {
        public int RRC_SuccConnEstab;      //RRC建立成功次数
        public int RRC_AttConnEstab;       //RRC建立请求次数 
        public double RRC_EstabRate;       //RRC建立成功率

        public int ERAB_NbrSuccEstab;       //E-RAB建立成功次数
        public int ERAB_NbrAttEstab;        //E-RAB建立请求次数
        public double ERAB_EstabRate;       //E-RAB建立成功率

        public int ERAB_NbrSuccEstab_1;     //QCI=1的E-RAB建立成功次数
        public int ERAB_NbrAttEstab_1;      //QCI=1的E-RAB建立请求次数
        public double ERAB_Estab_1Rate;     //QCI=1的E-RAB建立成功率

        public int RRC_ConnMax;             //当前UE的接入数

        public int HO_SuccOutInterEnbS1;    //LTE切换成功次数
        public int HO_AttOutInterEnbS1;     //LTE切换请求次数
        public double HO_EnbS1Rate;         //LTE切换成功率

        public int RRC_ConnReleaseCsfb;     //CSFB次数

        public int PDCP_UpOctUl;            //UL业务量（MB）
        public int PDCP_UpOctDl;            //DL业务量（MB）

        public int HO_SuccOutInterEnbS1_1;   //VoLTE切换成功次数
        public int HO_AttOutInterEnbS1_1;    //VoLTE切换请求次数
        public double HO_EnbS1_1Rate;        //VoLTE切换成功率

        public int HO_SuccOutInterFreq;      //ESRVCC切换成功次数
        public int HO_AttOutExecInterFreq;   //ESRVCC切换请求次数	
        public double HO_InterFreqRate;      //ESRVCC切换成功率

        public string timeStart;             //起始时间
        public string timeEnded;             //结束时间
        public string sn;                    //SN号      
    };


    /// <summary>
    /// strPerformance信息的结构体
    /// </summary>
    public struct strPerformanceInfo
    {
        public List<strPerformance> lst;
        public strPerforStat stat;
    }

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


    /// <summary>
    /// 省信息
    /// </summary>
    public struct Province
    {
        public string provice_id;
        public string provice_name;
    };

    
    /// <summary>
    /// 市信息
    /// </summary>
    public struct City
    {
        public string city_id;
        public string city_name;
    };

    
    /// <summary>
    /// 区信息
    /// </summary>
    public struct County
    {
        public string county_id;
        public string county_name;
    };


    /// <summary>
    /// 街道信息
    /// </summary>
    public struct Town
    {
        public string town_id;
        public string town_name;
    };


    /// <summary>
    /// 任务信息表
    /// </summary>
    public struct TaskInfo
    {
        public int id;
        public string actionName;
        public string actionId;
        public int actionType;
        public string actionXmlText;
        public int actionCount;
        public int successCount;
        public int failCount;

        /// <summary>
        /// 动作的起始时间，如'2016-12-23 12:34:56' 
        /// 不过滤时，传入null或者""
        /// </summary>
        public string actionTimeStart;


        /// <summary>
        /// 动作的结束时间，如'2018-06-23 12:34:56' 
        /// 不过滤时，传入null或者""
        /// </summary>
        public string actionTimeEnd;
    }


    /// <summary>
    /// 域表的各个字段
    /// </summary>
    public struct strDomian
    {
        public int id;                //主键ID
        public string name;           //节点的名称
        public int parentId;          //节点的父亲ID
        public string nameFullPath;   //节点的名称全路径
        public int isStation;         //标识是否为站点
        public string longitude;      //站点的经度,2018-12-19
        public string latitude;       //站点的纬度,2018-12-19
        public string des;            //描述
    };


    /// <summary>
    /// alarminfo的结构体
    /// </summary>
    public struct strLogInfo 
    {
        public string time;      //时间 
        public string level;     //等级
        public string username;  //用户名
        public string optype;    //操作类型
        public string sn;        //sn
        public string message;   //信息
    };

    /// <summary>
    /// alarminfo的结构体
    /// </summary>
    public struct strConfig
    {
        public string name;      //配置名称 
        public string value;     //配置值
        public string des;       //配置描述
        public string type;      //配置类型,Server,Client
        public string res1;      //保留字段1
        public string res2;      //保留字段2
    };


    public enum RC    //数据库返回代码
    {
        SUCCESS = 0,  //成功

        EXIST = 1,      //记录已经存在
        NO_EXIST = -6,  //记录不存在

        NO_OPEN = -1,      //数据库尚未打开
        PAR_NULL = -2,     //参数为空
        PAR_LEN_ERR = -3,  //参数长度有误
        OP_FAIL = -4,      //数据库操作失败
        PSW_ERR = -5,      //验证失败，密码有误
        PAR_FMT_ERR = -7,  //参数格式有误

        NO_INS_DEFUSR = -8,   //不能插入默认用户engi,root
        NO_DEL_DEFUSR = -9,   //不能删除默认用户engi,root
        FAIL_NO_USR = -10,    //验证失败，用户不存在
        FAIL_NO_MATCH = -11,  //用户和密码不匹配

        NO_INS_DEFRT = -12,    //不能插入默认角色类型Engineering,SuperAdmin,Administrator,SeniorOperator,Operator
        NO_DEL_DEFRT = -13,    //不能删除默认角色类型Engineering,SuperAdmin,Administrator,SeniorOperator,Operator

        NO_INS_DEFROLE = -14,  //不能插入默认角色RoleEng,RoleSA,RoleAdmin,RoleSO,RoleOP
        NO_DEL_DEFROLE = -15,  //不能删除默认角色RoleEng,RoleSA,RoleAdmin,RoleSO,RoleOP 

        NO_EXIST_RT = -16,      //角色类型不存在
        TIME_FMT_ERR = -17,     //时间格式有误

        USR_NO_EXIST = -18,      //usrName不存在
        ROLE_NO_EXIST = -19,     //roleName不存在

        NO_ROLE_ENG_SA = -20,    //不能指定到RoleEng和RoleSA中
        ID_SET_ERR = -21,        //ID集合有误

        IS_STATION = -22,        //域ID是站点
        IS_NOT_STATION = -23,    //域ID不是站点

        NO_EXIST_PARENT = -24,    //父亲节点不存在
        GET_PARENT_FAIL = -25,    //父亲节点信息获取失败

        ID_SET_FMT_ERR = -26,    //ID集合格式有误

        MODIFIED_EXIST = -27,    //修改后的记录已经存在
        DEV_NO_EXIST = -28,      //设备不存在
        CANNOT_DEL_ROOT = -29,   //不能删除设备的根节点

        IMSI_IMEI_BOTH_NULL = -30,   //IMSI和IMEI都为空
        IMSI_IMEI_BOTH_NOTNULL = -31,   //IMSI和IMEI都不为空

        AP_MODE_ERR = -32,           //AP的制式不对
        CAP_QUERY_INFO_ERR = -33,    //捕号查询信息有误
        TIME_ST_EN_ERR = -34,        //开始时间大于结束时间
        DOMAIN_NO_EXIST = -35,       //域不存在

        CARRY_ERR = -36,             //载波非法   
    }


    #region 批量导入导出


    public struct strBIE_DomainInfo
    {
        public string name;                //站点的名称,如"南山"
        public string parentNameFullPath;  //站点的父节点全路径，如"设备.深圳"
        public string isStation;           //是否为站点，"0":不是，"1":是    
    }

    public struct strBIE_DeviceInfo
    {
        public string name;                //设备的名称,如"电信FDD"
        public string parentNameFullPath;  //设备的父节点全路径，如"设备.深圳.南山"    

        public string sn;
        public string ipAddr;
        public string tac;
        public string earfcn;
        public string aliasName; //2019-02-20
        public string des;
    }
   

    /// <summary>
    /// 批量导入导出的结构体
    /// </summary>
    public struct strBatchImportExport
    {
        public List<strBIE_DomainInfo> lstDomainInfo;
        public List<strBIE_DeviceInfo> lstDeviceInfo;
    }

    #endregion

    #endregion

    public sealed class Logger
    {       
        #region 定义

        private const string LOG_DEBG = "\r\n【DEBG】[{0}][{1}]\r\n";
        private const string LOG_INFO = "\r\n【INFO】[{0}][{1}]\r\n";
        private const string LOG_WARN = "\r\n【WARN】[{0}][{1}]\r\n";
        private const string LOG_EROR = "\r\n【EROR】[{0}][{1}]\r\n";


        private static Object mutex_Logger = new Object();
        private static Queue<string> gQueueLogger = new Queue<string>();

        private static DateTime currentLogFileDate = DateTime.Now;

        private static TextWriterTraceListener twtl;

        private static string logRootDirectory = Application.StartupPath + @"\logInfo";
        //private static string logRootDirectory = @"C:\Apache24\htdocs\server";

        private static string logSubDirectory;

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

        private static long gLogIndex = 0;

        private static LogInfoType _gLogInfoType = LogInfoType.INFO;

        public static LogInfoType gLogInfoType
        {
            get { return Logger._gLogInfoType; }
            set { Logger._gLogInfoType = value; }
        }

        public static bool ThreadInitFlag = false ;

        #endregion

        #region 属性


        /// <summary>
        /// 获取或设置Log的根路径
        /// </summary>
        public static string LogRootDirectory
        {
            get
            {
                return logRootDirectory;
            }

            set
            {
                logRootDirectory = value;

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
        }

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
                //if (twtl == null)
                //{
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

                //MessageBox.Show(GetLogFullPath);
                twtl = new TextWriterTraceListener(GetLogFullPath);
                //}

                return twtl;
            }
        }

        /// <summary>
        /// 是否已经连接上数据库的标识
        /// </summary>
        public static string __INFO__
        {
            get
            {
                string retStr = "__INFO__";
                //System.Diagnostics.StackTrace st = new System.Diagnostics.StackTrace(1, true);

                //retStr += "文件名0 -> " + st.GetFrame(1).GetFileName().ToString() + "\r\n";
                //retStr += "函数名0 -> " + st.GetFrame(1).GetMethod().ToString() + "\r\n";
                //retStr += "所在行0 -> " + st.GetFrame(1).GetFileLineNumber().ToString() + "\r\n";

                //retStr += "文件名1 -> " + st.GetFrame(0).GetFileName().ToString() + "\r\n";
                //retStr += "函数名1 -> " + st.GetFrame(0).GetMethod().ToString() + "\r\n";
                //retStr += "所在行1 -> " + st.GetFrame(0).GetFileLineNumber().ToString() + "\r\n";

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

            if(ThreadInitFlag == false )
            {
                Start();
                ThreadInitFlag = true ;
            }
        }

        #endregion

        #region 方法

        #region trace

        public static void Trace(Exception ex)
        {           
            lock (mutex_Logger)
            {
                gQueueLogger.Enqueue(ex.Message + ex.StackTrace);
            }
        }

        private static string moduleName = "DB";
        private static LogCategory cat = LogCategory.I;

        public static void Trace(string auxiliaryInfo, string logInfo)
        {           
            LogInfoType logInfoType =  LogInfoType.INFO;

            if (auxiliaryInfo == "__DEBG__")
            {
                logInfoType = LogInfoType.DEBG;
            }
            else if (auxiliaryInfo == "__INFO__")
            {
                logInfoType = LogInfoType.INFO;
            }
            else if (auxiliaryInfo == "__WARN__")
            {
                logInfoType = LogInfoType.WARN;
            }
            else if (auxiliaryInfo == "__EROR__")
            {
                logInfoType = LogInfoType.EROR;
            }
            else
            {
                logInfoType = LogInfoType.INFO;   
            }

            if (logInfoType < gLogInfoType)
            {
                return;
            }

            string tmp = "";

            if (logInfoType == LogInfoType.DEBG)
            {
                tmp = string.Format(LOG_DEBG, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff"), ++gLogIndex);
            }
            else if (logInfoType == LogInfoType.INFO)
            {
                tmp = string.Format(LOG_INFO, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff"), ++gLogIndex);
            }
            else if (logInfoType == LogInfoType.WARN)
            {
                tmp = string.Format(LOG_WARN, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff"), ++gLogIndex);
            }
            else if (logInfoType == LogInfoType.EROR)
            {
                tmp = string.Format(LOG_EROR, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff"), ++gLogIndex);
            }
            else
            {
                tmp = string.Format(LOG_INFO, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff"), ++gLogIndex);
            }

            if (cat == LogCategory.R)
            {
                tmp += string.Format("【{0},R】{1}", moduleName, logInfo);
            }
            else if (cat == LogCategory.S)
            {
                tmp += string.Format("【{0},S】{1}", moduleName, logInfo);
            }
            else
            {
                tmp += string.Format("【{0},I】{1}", moduleName, logInfo);
            }        

            lock (mutex_Logger)
            {
                gQueueLogger.Enqueue(logInfo);
            }
        }

        //public static void Trace(LogInfoType logInfoType,
        //                         string logInfo,
        //                         string moduleName,
        //                         LogCategory cat)
        //{

        //    if (logInfoType < gLogInfoType)
        //    {
        //        return;
        //    }

        //    if (string.IsNullOrEmpty(logInfo) || string.IsNullOrEmpty(moduleName))
        //    {
        //        return;
        //    }

        //    string tmp = "";

        //    if (logInfoType == LogInfoType.DEBG)
        //    {
        //        tmp = string.Format(LOG_DEBG, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff"), ++gLogIndex);
        //    }
        //    else if (logInfoType == LogInfoType.INFO)
        //    {
        //        tmp = string.Format(LOG_INFO, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff"), ++gLogIndex);
        //    }
        //    else if (logInfoType == LogInfoType.WARN)
        //    {
        //        tmp = string.Format(LOG_WARN, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff"), ++gLogIndex);
        //    }
        //    else if (logInfoType == LogInfoType.EROR)
        //    {
        //        tmp = string.Format(LOG_EROR, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff"), ++gLogIndex);
        //    }
        //    else
        //    {
        //        tmp = string.Format(LOG_INFO, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff"), ++gLogIndex);
        //    }

        //    /*
        //     * 2018-07-16，区分Log的开始
        //     */
        //    if (logInfo.Contains("今天是个好日子"))
        //    {
        //        tmp = "\r\n\r\n\r\n\r\n" + tmp;
        //        logInfo = string.Format("{0}", logInfo);
        //    }

        //    if (cat == LogCategory.R)
        //    {
        //        tmp += string.Format("【{0},R】{1}", moduleName, logInfo);
        //    }
        //    else if (cat == LogCategory.S)
        //    {
        //        tmp += string.Format("【{0},S】{1}", moduleName, logInfo);
        //    }
        //    else
        //    {
        //        tmp += string.Format("【{0},I】{1}", moduleName, logInfo);
        //    }

        //    lock (mutex_Logger)
        //    {
        //        gQueueLogger.Enqueue(tmp);
        //    }
        //}

        public static void Trace(LogInfoType logInfoType,
                                 string logInfo,
                                 string moduleName,
                                 LogCategory cat,
                                [CallerMemberName] string memberName = "",
                                [CallerFilePath] string filePath = "",
                                [CallerLineNumber] int lineNumber = 0)
        {

            if (logInfoType < gLogInfoType)
            {
                return;
            }

            if (string.IsNullOrEmpty(logInfo) || string.IsNullOrEmpty(moduleName))
            {
                return;
            }

            string tmp = "";

            if (logInfoType == LogInfoType.DEBG)
            {
                tmp = string.Format(LOG_DEBG, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff"), ++gLogIndex, Path.GetFileName(filePath), lineNumber);
            }
            else if (logInfoType == LogInfoType.INFO)
            {
                tmp = string.Format(LOG_INFO, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff"), ++gLogIndex, Path.GetFileName(filePath), lineNumber);
            }
            else if (logInfoType == LogInfoType.WARN)
            {
                tmp = string.Format(LOG_WARN, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff"), ++gLogIndex, Path.GetFileName(filePath), lineNumber);
            }
            else if (logInfoType == LogInfoType.EROR)
            {
                tmp = string.Format(LOG_EROR, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff"), ++gLogIndex, Path.GetFileName(filePath), lineNumber);
            }
            else
            {
                tmp = string.Format(LOG_INFO, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff"), ++gLogIndex, Path.GetFileName(filePath), lineNumber);
            }

            /*
             * 2018-07-16，区分Log的开始
             */
            if (logInfo.Contains("今天是个好日子"))
            {
                tmp = "\r\n\r\n\r\n\r\n" + tmp;
                logInfo = string.Format("{0}", logInfo);
            }

            if (cat == LogCategory.R)
            {
                tmp += string.Format("【{0},R】{1}", moduleName, logInfo);
            }
            else if (cat == LogCategory.S)
            {
                tmp += string.Format("【{0},S】{1}", moduleName, logInfo);
            }
            else
            {
                tmp += string.Format("【{0},I】{1}", moduleName, logInfo);
            }

            lock (mutex_Logger)
            {
                gQueueLogger.Enqueue(tmp);
            }
        }


        #endregion        

        #region helper

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

        public static void Start()
        {
            //通过ParameterizedThreadStart创建线程
            Thread threadLogger = new Thread(new ParameterizedThreadStart(thread_for_logger));

            threadLogger.Priority = ThreadPriority.Lowest;

            //给方法传值
            threadLogger.Start("thread_for_ftp_helper!\n");
            threadLogger.IsBackground = true;
        }

        private static void thread_for_logger(object obj)
        {
            bool noMsg = false;
            string info = "";
            List<string> lstData = new List<string>();

            while (true)
            {
                if (noMsg)
                {
                    //没消息时Sleep一大点
                    Thread.Sleep(100);
                }
                else
                {
                    //有消息时Sleep一小点
                    Thread.Sleep(2);
                }                               

                try
                {
                    #region 保存Logger

                    lock (mutex_Logger)
                    {
                        if (gQueueLogger.Count > 0)
                        {
                            noMsg = false;
                            lstData.Clear();

                            //拷贝数据
                            while (gQueueLogger.Count > 0)
                            {
                                lstData.Add(gQueueLogger.Dequeue());
                            }
                        }
                        else
                        {
                            noMsg = true;
                            continue;
                        }
                    }
                 
                    switch (logOutType)
                    {
                        case LogOutType.FileOnly:
                            {
                                //检测日志日期
                                StrategyLog();

                                info = "";
                                foreach (string str in lstData)
                                {
                                    info += str + "\r\n";
                                }

                                System.Diagnostics.Trace.WriteLine(info);
                                if (fileFlushType == FileFlushType.RightNow)
                                {
                                    System.Diagnostics.Trace.Close();
                                }
                             
                                break;
                            }                        
                    }

                    #endregion
                }
                catch (Exception ee)
                {
                    Logger.Trace(LogInfoType.EROR, ee.Message, "Logger", LogCategory.I);
                    continue;
                }
            }
        }

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

        //省
        private static List<Province> provinceData = new List<Province>();

        //市
        private static List<City> cityData = new List<City>();

        //区
        private static List<County> distractData = new List<County>();

        //街道
        private static List<Town> townData = new List<Town>();

        private Dictionary<int, string> dicRTV = new Dictionary<int, string>();

        //private bool myDbOperFlag = true;

        /// <summary>
        /// 系统启动后或设备有更改后获取该字典到内存中
        /// string = 设备.深圳.福田.中心广场.西北监控.LTE-FDD-B3
        /// strDevice = device的信息
        /// </summary>
        private Dictionary<string, strDevice> gDicDevFullName = new Dictionary<string, strDevice>();
        
        /// <summary>
        /// 2018-10-11
        /// 用于保存每个设备中对应的"站点.设备名称"
        /// </summary>
        private static Dictionary<string, string> gDicDevId_Station_DevName = new Dictionary<string, string>();

        //saf

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
            set
            {
                myDbConnFlag = value;
            }
        }

        public static List<Province> ProvinceData
        {
            get
            {
                return provinceData;
            }

            set
            {
                provinceData = value;
            }
        }

        public static List<City> CityData
        {
            get
            {
                return cityData;
            }

            set
            {
                cityData = value;
            }
        }

        public static List<County> DistractData
        {
            get
            {
                return distractData;
            }

            set
            {
                distractData = value;
            }
        }

        public static List<Town> TownData
        {
            get
            {
                return townData;
            }

            set
            {
                townData = value;
            }
        }
        
        #endregion

        #region 构造函数

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="conString">连接数据库的字符串</param>
        public MySqlDbHelper(string conString)
        {
            conString += ";Convert Zero Datetime=True;Allow Zero Datetime=True";   
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
            conString += ";Convert Zero Datetime=True;Allow Zero Datetime=True";        

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

        private int All_Init()
        {
            int rtv = 0;
           
            rtv = domain_dictionary_info_join_get(ref gDicDevFullName,ref gDicDevId_Station_DevName);
            return rtv;
        }

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
                #region 返回字符串

                dicRTV = new Dictionary<int, string>();

                dicRTV.Add((int)RC.SUCCESS, "成功");
                dicRTV.Add((int)RC.EXIST, "记录已经存在");
                dicRTV.Add((int)RC.NO_EXIST, "记录不存在");

                dicRTV.Add((int)RC.NO_OPEN, "数据库尚未打开");
                dicRTV.Add((int)RC.PAR_NULL, "参数为空");
                dicRTV.Add((int)RC.PAR_LEN_ERR, "参数长度有误");
                dicRTV.Add((int)RC.OP_FAIL, "数据库操作失败");
                dicRTV.Add((int)RC.PSW_ERR, "验证失败，密码有误");
                dicRTV.Add((int)RC.PAR_FMT_ERR, "参数格式有误");

                dicRTV.Add((int)RC.NO_INS_DEFUSR, "不能插入默认用户engi,root");
                dicRTV.Add((int)RC.NO_DEL_DEFUSR, "不能删除默认用户engi,root");
                dicRTV.Add((int)RC.FAIL_NO_USR, "验证失败，用户不存在");
                dicRTV.Add((int)RC.FAIL_NO_MATCH, "用户和密码不匹配");

                dicRTV.Add((int)RC.NO_INS_DEFRT, "不能插入默认角色类型Engineering,SuperAdmin,Administrator,SeniorOperator,Operator");
                dicRTV.Add((int)RC.NO_DEL_DEFRT, "不能删除默认角色类型Engineering,SuperAdmin,Administrator,SeniorOperator,Operator");

                dicRTV.Add((int)RC.NO_INS_DEFROLE, "不能插入默认角色RoleEng,RoleSA,RoleAdmin,RoleSO,RoleOP");
                dicRTV.Add((int)RC.NO_DEL_DEFROLE, "不能删除默认角色RoleEng,RoleSA,RoleAdmin,RoleSO,RoleOP");

                dicRTV.Add((int)RC.NO_EXIST_RT, "角色类型不存在");
                dicRTV.Add((int)RC.TIME_FMT_ERR, "时间格式有误");

                dicRTV.Add((int)RC.USR_NO_EXIST, "usrName不存在");
                dicRTV.Add((int)RC.ROLE_NO_EXIST, "roleName不存在");

                dicRTV.Add((int)RC.NO_ROLE_ENG_SA, "不能指定到RoleEng和RoleSA中");
                dicRTV.Add((int)RC.ID_SET_ERR, "ID集合有误");

                dicRTV.Add((int)RC.IS_STATION, "域ID是站点");
                dicRTV.Add((int)RC.IS_NOT_STATION, "域ID不是站点");

                dicRTV.Add((int)RC.NO_EXIST_PARENT, "父亲节点不存在");
                dicRTV.Add((int)RC.GET_PARENT_FAIL, "父亲节点信息获取失败");

                dicRTV.Add((int)RC.ID_SET_FMT_ERR, "ID集合格式有误");

                dicRTV.Add((int)RC.MODIFIED_EXIST, "修改后的记录已经存在");
                dicRTV.Add((int)RC.DEV_NO_EXIST, "设备不存在");
                dicRTV.Add((int)RC.CANNOT_DEL_ROOT, "不能删除设备的根节点");

                dicRTV.Add((int)RC.IMSI_IMEI_BOTH_NULL, "IMSI和IMEI都为空");
                dicRTV.Add((int)RC.IMSI_IMEI_BOTH_NOTNULL, "IMSI和IMEI都不为空");

                dicRTV.Add((int)RC.AP_MODE_ERR, "AP的制式不对");
                dicRTV.Add((int)RC.CAP_QUERY_INFO_ERR, "捕号查询信息有误");
                dicRTV.Add((int)RC.TIME_ST_EN_ERR, "开始时间大于结束时间");
                dicRTV.Add((int)RC.DOMAIN_NO_EXIST, "域不存在");

                dicRTV.Add((int)RC.CARRY_ERR, "载波非法");

                #endregion

                myDbConn.Open();
                myDbConnFlag = true;

                All_Init();

                return true;
            }
            catch (MySqlException e)
            {
                myDbConnFlag = false;
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

        /// <summary>
        /// 判断数据库连接是否关闭，或者已经异常
        /// </summary>
        /// <returns></returns>
        public bool Conn_Is_Closed_Or_Abnormal()
        {
            if (myDbConn.State == ConnectionState.Closed)
            {
                return true;
            }

            // 2018-08-21
            if (myDbConnFlag == false)
            {
                return true;
            }

            return false;
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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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

        /// <summary>
        /// 通过数据库操作返回码获取对应的字符串
        /// </summary>
        /// <param name="rtCode">数据库操作返回码</param>
        /// <returns>
        /// 成功 ： 非null
        /// 失败 ： null
        /// </returns>
        public string get_rtv_str(int rtCode)
        {
            if (dicRTV.ContainsKey(rtCode))
            {
                return dicRTV[rtCode];
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// 打印设备和全名的相关信息,2018-11-09
        /// </summary>
        /// <param name="title"></param>
        /// <param name="dic"></param>
        public void print_dic_dev_fullname_info(string title, Dictionary<string, strDevice> dic)
        {
            int inx = 1;
            string info = "";

            if (dic == null || dic.Count == 0)
            {
                info = string.Format("{0},gDicDevFullName为空.\r\n", title);        
                Logger.Trace(LogInfoType.WARN, info, "DB", LogCategory.I);
                return;
            }

            info = string.Format("{0},gDicDevFullName.cout = {1}.\r\n", title, dic.Count);
            foreach (KeyValuePair<string, strDevice> kv in dic)
            {
                strDevice dev = kv.Value;
                info += string.Format("inx:{0}", inx.ToString().PadRight(6));
                info += string.Format("FN:{0}", kv.Key.PadRight(40));

                info += string.Format("id:{0}", dev.id.ToString().PadRight(4));
                info += string.Format("name:{0}", dev.bsName.PadRight(16));
                info += string.Format("SN:{0}", dev.station_and_name.PadRight(20));
                info += string.Format("affDomainId:{0}\r\n", dev.affDomainId.ToString().PadRight(8));
                inx++;
            }
           
            Logger.Trace(LogInfoType.INFO, info, "DB", LogCategory.I);

            return;
        }

        #endregion

        #region 01 - domain操作

        /// <summary>
        /// 通过名称全路径获取对应记录的信息
        /// </summary>
        /// <param name="nameFullPath">全路径</param>
        /// <param name="str">成功时返回的记录信息</param>
        /// <returns>
        ///   RC.NO_OPEN  ：数据库尚未打开
        ///   RC.PAR_NULL ：参数为空
        ///   PAR_LEN_ERR ：参数长度有误
        ///   RC.OP_FAIL  ：数据库操作失败 
        ///   RC.NO_EXIST ：记录不存在
        ///   RC.SUCCESS  ：成功 
        /// </returns>
        private int domain_record_get_by_nameFullPath(string nameFullPath, ref strDomian str)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return (int)RC.NO_OPEN;
            }

            if (string.IsNullOrEmpty(nameFullPath))
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.PAR_NULL], "DB", LogCategory.I);
                return (int)RC.PAR_NULL;
            }

            if (nameFullPath.Length > 64)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.PAR_LEN_ERR], "DB", LogCategory.I);
                return (int)RC.PAR_LEN_ERR;
            }

            if ((int)RC.NO_EXIST == domain_record_exist(nameFullPath))
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_EXIST], "DB", LogCategory.I);
                return (int)RC.NO_EXIST;
            }

            str = new strDomian();

            string sql = string.Format("select * from domain where nameFullPath='{0}'", nameFullPath);
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            str.id = Convert.ToInt32(dr["id"]);

                            if (!string.IsNullOrEmpty(dr["name"].ToString()))
                            {
                                str.name = dr["name"].ToString();
                            }

                            if (!string.IsNullOrEmpty(dr["parentId"].ToString()))
                            {
                                str.parentId = Convert.ToInt32(dr["parentId"].ToString());
                            }

                            if (!string.IsNullOrEmpty(dr["nameFullPath"].ToString()))
                            {
                                str.nameFullPath = dr["nameFullPath"].ToString();
                            }

                            if (!string.IsNullOrEmpty(dr["isStation"].ToString()))
                            {
                                str.isStation = Convert.ToInt32(dr["isStation"].ToString());
                            }

                            // 2018-12-19
                            if (!string.IsNullOrEmpty(dr["longitude"].ToString()))
                            {
                                str.longitude = dr["longitude"].ToString();
                            }
                            else
                            {
                                str.longitude = "";
                            }

                            // 2018-12-19
                            if (!string.IsNullOrEmpty(dr["latitude"].ToString()))
                            {
                                str.latitude = dr["latitude"].ToString();
                            }
                            else
                            {
                                str.latitude = "";
                            }

                            if (!string.IsNullOrEmpty(dr["des"].ToString()))
                            {
                                str.des = dr["des"].ToString();
                            }
                            else
                            {
                                str.des = "";
                            }
                        }
                        dr.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(LogInfoType.EROR, e.Message + e.StackTrace, "DB", LogCategory.I);
                dicRTV[(int)RC.OP_FAIL] = string.Format("数据库操作失败:{0}", e.Message + e.StackTrace);     
                return (int)RC.OP_FAIL;
            }

            return (int)RC.SUCCESS;
        }
        
        /// <summary>
        /// 通过名称全路径获取对应记录的信息
        /// </summary>
        /// <param name="id">id</param>
        /// <param name="str">成功时返回的记录信息</param>
        /// <returns>
        ///   RC.NO_OPEN  ：数据库尚未打开
        ///   RC.PAR_NULL ：参数为空
        ///   PAR_LEN_ERR ：参数长度有误
        ///   RC.OP_FAIL  ：数据库操作失败 
        ///   RC.NO_EXIST ：记录不存在
        ///   RC.SUCCESS  ：成功 
        /// </returns>
        private int domain_record_get_by_nameFullPath(int id, ref strDomian str)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return (int)RC.NO_OPEN;
            }

            if ((int)RC.NO_EXIST == domain_record_exist(id))
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_EXIST], "DB", LogCategory.I);
                return (int)RC.NO_EXIST;
            }

            str = new strDomian();

            string sql = string.Format("select * from domain where id = {0}", id);
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            //str.id = Convert.ToInt32(dr[0]);
                            //str.name = dr[1].ToString();
                            //str.parentId = Convert.ToInt32(dr[2]);                           
                            //str.nameFullPath = dr[3].ToString();
                            //str.isStation = Convert.ToInt32(dr[4]);
                            //str.des = dr[5].ToString();

                            str.id = Convert.ToInt32(dr["id"]);

                            if (!string.IsNullOrEmpty(dr["name"].ToString()))
                            {
                                str.name = dr["name"].ToString();
                            }

                            if (!string.IsNullOrEmpty(dr["parentId"].ToString()))
                            {
                                str.parentId = Convert.ToInt32(dr["parentId"].ToString());
                            }

                            if (!string.IsNullOrEmpty(dr["nameFullPath"].ToString()))
                            {
                                str.nameFullPath = dr["nameFullPath"].ToString();
                            }

                            if (!string.IsNullOrEmpty(dr["isStation"].ToString()))
                            {
                                str.isStation = Convert.ToInt32(dr["isStation"].ToString());
                            }

                            // 2018-12-19
                            if (!string.IsNullOrEmpty(dr["longitude"].ToString()))
                            {
                                str.longitude = dr["longitude"].ToString();
                            }
                            else
                            {
                                str.longitude = "";
                            }

                            // 2018-12-19
                            if (!string.IsNullOrEmpty(dr["latitude"].ToString()))
                            {
                                str.latitude = dr["latitude"].ToString();
                            }
                            else
                            {
                                str.latitude = "";
                            }

                            if (!string.IsNullOrEmpty(dr["des"].ToString()))
                            {
                                str.des = dr["des"].ToString();
                            }
                            else
                            {
                                str.des = "";
                            }
                        }
                        dr.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(LogInfoType.EROR, e.Message + e.StackTrace, "DB", LogCategory.I);
                dicRTV[(int)RC.OP_FAIL] = string.Format("数据库操作失败:{0}", e.Message + e.StackTrace); 
                return (int)RC.OP_FAIL;
            }

            return (int)RC.SUCCESS;
        }

        /// <summary>
        /// 通过ID获取全路径
        /// </summary>
        /// <param name="id"></param>
        /// <param name="nameFullPath"></param>
        /// <returns>
        ///   RC.NO_OPEN  ：数据库尚未打开
        ///   RC.OP_FAIL  ：数据库操作失败 
        ///   RC.NO_EXIST ：记录不存在
        ///   RC.SUCCESS  ：成功 
        /// </returns>
        private int domain_get_nameFullPath_by_id(string id, ref string nameFullPath)
        {
            if ((int)RC.NO_EXIST == domain_record_exist(int.Parse(id)))
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_EXIST], "DB", LogCategory.I);
                return (int)RC.NO_EXIST;
            }

            nameFullPath = "";
            string sql = string.Format("select nameFullPath from domain where id = {0}", id);
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            if (!string.IsNullOrEmpty(dr[0].ToString()))
                            {
                                nameFullPath = dr[0].ToString();
                            }
                        }
                        dr.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(LogInfoType.EROR, e.Message + e.StackTrace, "DB", LogCategory.I);
                dicRTV[(int)RC.OP_FAIL] = string.Format("数据库操作失败:{0}", e.Message + e.StackTrace);
                return (int)RC.OP_FAIL;
            }

            return (int)RC.SUCCESS;
        }

        /// <summary>
        /// 通过全路径获取ID
        /// </summary>
        /// <param name="id"></param>
        /// <param name="nameFullPath"></param>
        /// <returns>
        ///   RC.NO_OPEN  ：数据库尚未打开
        ///   RC.OP_FAIL  ：数据库操作失败 
        ///   RC.PAR_NULL ：参数为空
        ///   PAR_LEN_ERR ：参数长度有误
        ///   RC.NO_EXIST ：记录不存在
        ///   RC.SUCCESS  ：成功 
        /// </returns>
        private int domain_get_id_by_nameFullPath(string nameFullPath, ref int id)
        {
            if (string.IsNullOrEmpty(nameFullPath))
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.PAR_NULL], "DB", LogCategory.I);
                return (int)RC.PAR_NULL;
            }

            if (nameFullPath.Length > 64)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.PAR_LEN_ERR], "DB", LogCategory.I);
                return (int)RC.PAR_LEN_ERR;
            }

            if ((int)RC.NO_EXIST == domain_record_exist(nameFullPath))
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_EXIST], "DB", LogCategory.I);
                return (int)RC.NO_EXIST;
            }

            string sql = string.Format("select id from domain where nameFullPath = '{0}'", nameFullPath);
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            if (!string.IsNullOrEmpty(dr[0].ToString()))
                            {
                                id = int.Parse(dr[0].ToString());
                            }
                        }
                        dr.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(LogInfoType.EROR, e.Message + e.StackTrace, "DB", LogCategory.I);
                dicRTV[(int)RC.OP_FAIL] = string.Format("数据库操作失败:{0}", e.Message + e.StackTrace);
                return (int)RC.OP_FAIL;
            }

            return (int)RC.SUCCESS;
        }

        /// <summary>
        /// 检查节点是否存在
        /// </summary>
        /// <param name="id">记录的id</param>
        /// <returns>
        ///   RC.NO_OPEN  ：数据库尚未打开
        ///   RC.OP_FAIL  ：数据库操作失败 
        ///   RC.NO_EXIST ：不存在
        ///   RC.EXIST    ：存在
        /// </returns>
        private int domain_record_exist(int id)
        {
            //UInt32 cnt = 0;

            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return (int)RC.NO_OPEN;
            }

            //string sql = string.Format("select count(*) from domain where id = {0}", id);

            string sql = string.Format("select 1 from domain where id = {0} limit 1", id);

            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        //while (dr.Read())
                        //{
                        //    cnt = Convert.ToUInt32(dr[0]);
                        //}
                        //dr.Close();

                        if (dr.HasRows)
                        {
                            return (int)RC.EXIST;
                        }
                        else
                        {
                            return (int)RC.NO_EXIST;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(LogInfoType.EROR, e.Message + e.StackTrace, "DB", LogCategory.I);
                dicRTV[(int)RC.OP_FAIL] = string.Format("数据库操作失败:{0}", e.Message + e.StackTrace);
                return (int)RC.OP_FAIL;
            }

            //if (cnt > 0)
            //{
            //    return (int)RC.EXIST;
            //}
            //else
            //{
            //    return (int)RC.NO_EXIST;
            //}
        }

        /// <summary>
        /// 检查节点是否存在
        /// </summary>
        /// <param name="nameFullPath">节点的全路径名称</param>
        /// <returns>
        ///   RC.NO_OPEN  ：数据库尚未打开
        ///   RC.PAR_NULL ：参数为空
        ///   PAR_LEN_ERR ：参数长度有误
        ///   RC.OP_FAIL  ：数据库操作失败 
        ///   RC.NO_EXIST ：不存在
        ///   RC.EXIST    ：存在
        /// </returns>
        private int domain_record_exist(string nameFullPath)
        {
            //UInt32 cnt = 0;

            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return (int)RC.NO_OPEN;
            }

            if (string.IsNullOrEmpty(nameFullPath))
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.PAR_NULL], "DB", LogCategory.I);
                return (int)RC.PAR_NULL;
            }

            if (nameFullPath.Length > 64)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.PAR_LEN_ERR], "DB", LogCategory.I);
                return (int)RC.PAR_LEN_ERR;
            }

            //string sql = string.Format("select count(*) from domain where nameFullPath = '{0}'", nameFullPath);

            string sql = string.Format("select 1 from domain where nameFullPath = '{0}' limit 1", nameFullPath);

            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        //while (dr.Read())
                        //{
                        //    cnt = Convert.ToUInt32(dr[0]);
                        //}
                        //dr.Close();

                        if (dr.HasRows)
                        {
                            return (int)RC.EXIST;
                        }
                        else
                        {
                            return (int)RC.NO_EXIST;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(LogInfoType.EROR, e.Message + e.StackTrace, "DB", LogCategory.I);
                dicRTV[(int)RC.OP_FAIL] = string.Format("数据库操作失败:{0}", e.Message + e.StackTrace); 
                return (int)RC.OP_FAIL;
            }

            //if (cnt > 0)
            //{
            //    return (int)RC.EXIST;
            //}
            //else
            //{
            //    return (int)RC.NO_EXIST;
            //}
        }

        /// <summary>
        /// 检查域ID是否为站点
        /// </summary>
        /// <param name="id">节点的id</param>
        /// <returns>
        ///   RC.NO_OPEN        ：数据库尚未打开
        ///   RC.OP_FAIL        ：数据库操作失败 
        ///   RC.IS_STATION     ：域ID是站点
        ///   RC.IS_NOT_STATION ：域ID不是是站点
        /// </returns>
        private int domain_record_is_station(int id)
        {
            UInt32 cnt = 0;

            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return (int)RC.NO_OPEN;
            }

            string sql = string.Format("select count(*) from domain where id = {0} and isStation = {1}", id, 1);
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
                Logger.Trace(LogInfoType.EROR, e.Message + e.StackTrace, "DB", LogCategory.I);
                dicRTV[(int)RC.OP_FAIL] = string.Format("数据库操作失败:{0}", e.Message + e.StackTrace);  
                return (int)RC.OP_FAIL;
            }

            if (cnt > 0)
            {
                return (int)RC.IS_STATION;
            }
            else
            {
                return (int)RC.IS_NOT_STATION;
            }
        }

        /// <summary>
        /// 检查域ID是否为站点
        /// </summary>
        /// <param name="nameFullPath">节点的nameFullPath</param>
        /// <returns>
        ///   RC.NO_OPEN        ：数据库尚未打开
        ///   RC.OP_FAIL        ：数据库操作失败 
        ///   RC.IS_STATION     ：该域是站点
        ///   RC.IS_NOT_STATION ：该域不是是站点
        /// </returns>
        private int domain_record_is_station(string nameFullPath)
        {
            UInt32 cnt = 0;

            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return (int)RC.NO_OPEN;
            }

            string sql = string.Format("select count(*) from domain where nameFullPath = '{0}' and isStation = {1}", nameFullPath, 1);
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
                Logger.Trace(LogInfoType.EROR, e.Message + e.StackTrace, "DB", LogCategory.I);
                dicRTV[(int)RC.OP_FAIL] = string.Format("数据库操作失败:{0}", e.Message + e.StackTrace);
                return (int)RC.OP_FAIL;
            }

            if (cnt > 0)
            {
                return (int)RC.IS_STATION;
            }
            else
            {
                return (int)RC.IS_NOT_STATION;
            }
        }

        /// <summary>
        /// 添加一个节点到域表中
        /// </summary>
        /// <param name="name">节点名称</param>
        /// <param name="parentNameFullPath">节点的父亲全路径</param>
        /// <param name="isStation">是否为站点</param>
        /// <param name="des">描述</param>
        /// <returns>
        ///   RC.NO_OPEN      ：数据库尚未打开
        ///   RC.PAR_NULL     ：参数为空
        ///   PAR_LEN_ERR     ：参数长度有误
        ///   RC.OP_FAIL      ：数据库操作失败 
        ///   RC.EXIST        ：记录已经存在
        ///   NO_EXIST_PARENT ：父亲节点不存在
        ///   GET_PARENT_FAIL ：父亲节点信息获取失败
        ///   RC.SUCCESS      ：成功 
        /// </returns>
        private int domain_record_insert(string name, string parentNameFullPath, int isStation, string des)
        {
            string curNameFullPath = "";

            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return (int)RC.NO_OPEN;
            }

            if (string.IsNullOrEmpty(name) ||
                string.IsNullOrEmpty(parentNameFullPath) ||
                string.IsNullOrEmpty(des))
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.PAR_NULL], "DB", LogCategory.I);
                return (int)RC.PAR_NULL;
            }

            if (name.Length > 64 ||
                des.Length > 256 ||
                parentNameFullPath.Length > 1024)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.PAR_LEN_ERR], "DB", LogCategory.I);
                return (int)RC.PAR_LEN_ERR;
            }

            curNameFullPath = string.Format("{0}.{1}", parentNameFullPath, name);


            //(1)先检查父亲节点是否存在
            if ((int)RC.NO_EXIST == domain_record_exist(parentNameFullPath))
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_EXIST_PARENT], "DB", LogCategory.I);
                return (int)RC.NO_EXIST_PARENT;
            }


            //(2)再检查新增节点是否存在
            if ((int)RC.EXIST == domain_record_exist(curNameFullPath))
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.EXIST], "DB", LogCategory.I);
                return (int)RC.EXIST;
            }

            strDomian str = new strDomian();
            if ((int)RC.SUCCESS != domain_record_get_by_nameFullPath(parentNameFullPath, ref str))
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.GET_PARENT_FAIL], "DB", LogCategory.I);
                return (int)RC.GET_PARENT_FAIL;
            }

            //string sql = string.Format("insert into domain values(NULL,'{0}',{1},'{2}',{3},'{4}')", 
            //    name, str.id,curNameFullPath,isStation,des);

            string sql = string.Format("insert into domain(id,name,parentId,nameFullPath,isStation,des) values(NULL,'{0}',{1},'{2}',{3},'{4}')",
                name, str.id, curNameFullPath, isStation, des);

            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    if (cmd.ExecuteNonQuery() < 0)
                    {
                        Logger.Trace(LogInfoType.WARN, sql, "DB", LogCategory.I);
                        return (int)RC.OP_FAIL;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(LogInfoType.EROR, e.Message + e.StackTrace, "DB", LogCategory.I);
                dicRTV[(int)RC.OP_FAIL] = string.Format("数据库操作失败:{0}", e.Message + e.StackTrace);                
                return (int)RC.OP_FAIL;
            }

            return (int)RC.SUCCESS;
        }

        /// <summary>
        /// 获取一个节点下所有站点的id列表
        /// </summary>
        /// <param name="nameFullPath">节点的全路径名称</param>       
        /// <returns>
        ///   RC.NO_OPEN         ：数据库尚未打开
        ///   RC.PAR_NULL        ：参数为空
        ///   PAR_LEN_ERR        ：参数长度有误
        ///   RC.OP_FAIL         ：数据库操作失败 
        ///   RC.NO_EXIST        ：记录不存在        
        ///   RC.SUCCESS         ：成功
        /// </returns>
        private int domain_record_station_list_get(string nameFullPath, ref List<int> listID)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return (int)RC.NO_OPEN;
            }

            if (string.IsNullOrEmpty(nameFullPath))
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.PAR_NULL], "DB", LogCategory.I);
                return (int)RC.PAR_NULL;
            }

            if (nameFullPath.Length > 1024)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.PAR_LEN_ERR], "DB", LogCategory.I);
                return (int)RC.PAR_LEN_ERR;
            }

            //检查记录是否存在
            if ((int)RC.NO_EXIST == domain_record_exist(nameFullPath))
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_EXIST], "DB", LogCategory.I);
                return (int)RC.NO_EXIST;
            }

            string sql = "";
            listID = new List<int>();

            /*
             *  注意要加上分隔符".",如'{0}.%%'，否则会误操作
             */
            sql = string.Format("select id from domain where (nameFullPath like '{0}.%%' or nameFullPath = '{0}') and isStation = 1", nameFullPath);

            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            if (!string.IsNullOrEmpty(dr[0].ToString()))
                            {
                                listID.Add(int.Parse(dr[0].ToString()));
                            }
                        }
                        dr.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(LogInfoType.EROR, e.Message + e.StackTrace, "DB", LogCategory.I);
                dicRTV[(int)RC.OP_FAIL] = string.Format("数据库操作失败:{0}", e.Message + e.StackTrace);                 
                return (int)RC.OP_FAIL;
            }

            return (int)RC.SUCCESS;
        }

        /// <summary>
        /// 删除一个节点和其下面的所有子孙节点(包括节点本身)
        /// 同时，删除其下面的所有设备
        /// </summary>
        /// <param name="nameFullPath">节点的全路径名称</param>       
        /// <returns>
        ///   RC.NO_OPEN         ：数据库尚未打开
        ///   RC.PAR_NULL        ：参数为空
        ///   PAR_LEN_ERR        ：参数长度有误
        ///   RC.OP_FAIL         ：数据库操作失败 
        ///   RC.NO_EXIST        ：记录不存在
        ///   RC.CANNOT_DEL_ROOT ：不能删除设备的根节点
        ///   RC.SUCCESS         ：成功
        /// </returns>
        private int domain_record_delete(string nameFullPath)
        {
            int rtv = -1;

            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return (int)RC.NO_OPEN;
            }

            if (string.IsNullOrEmpty(nameFullPath))
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.PAR_NULL], "DB", LogCategory.I);
                return (int)RC.PAR_NULL;
            }

            if (nameFullPath.Length > 1024)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.PAR_LEN_ERR], "DB", LogCategory.I);
                return (int)RC.PAR_LEN_ERR;
            }

            //检查记录是否存在
            if ((int)RC.NO_EXIST == domain_record_exist(nameFullPath))
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_EXIST], "DB", LogCategory.I);
                return (int)RC.NO_EXIST;
            }

            //检查是否为设备的根节点
            if (nameFullPath == "设备")
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.CANNOT_DEL_ROOT], "DB", LogCategory.I);
                return (int)RC.CANNOT_DEL_ROOT;
            }

            //获取该域下所有子孙站点的id列表
            List<int> listDomainId = new List<int>();
            rtv = domain_record_station_list_get(nameFullPath, ref listDomainId);
            if (rtv != (int)RC.SUCCESS)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[rtv], "DB", LogCategory.I);
                return rtv;
            }

            string sql = "";

            //先删除节点本身
            sql = string.Format("delete from domain where nameFullPath = '{0}'", nameFullPath);

            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    if (cmd.ExecuteNonQuery() < 0)
                    {
                        Logger.Trace(LogInfoType.EROR, sql, "DB", LogCategory.I);
                        return (int)RC.OP_FAIL;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(LogInfoType.EROR, e.Message + e.StackTrace, "DB", LogCategory.I);
                dicRTV[(int)RC.OP_FAIL] = string.Format("数据库操作失败:{0}", e.Message + e.StackTrace); 
                return (int)RC.OP_FAIL;
            }

            //再删除节点下面的所有子孙节点

            /*
             *  注意要加上分隔符".",如'{0}.%%'，否则会误删除
             */
            sql = string.Format("delete from domain where nameFullPath like '{0}.%%'", nameFullPath);

            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    if (cmd.ExecuteNonQuery() < 0)
                    {
                        Logger.Trace(LogInfoType.EROR, sql, "DB", LogCategory.I);
                        return (int)RC.OP_FAIL;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(LogInfoType.EROR, e.Message + e.StackTrace, "DB", LogCategory.I);
                dicRTV[(int)RC.OP_FAIL] = string.Format("数据库操作失败:{0}", e.Message + e.StackTrace);                 
                return (int)RC.OP_FAIL;
            }

            List<string> listDevId = new List<string>();
            List<string> listDevName = new List<string>();

            //删除该节点下所有站点的设备
            //foreach (int id in listDomainId)
            //{
            //    rtv = device_id_name_get_by_affdomainid(id, ref listDevId, ref listDevName);
            //    if (rtv == (int)RC.SUCCESS)
            //    {
            //        foreach (string devName in listDevName)
            //        {
            //            device_record_delete(id, devName);
            //        }
            //    }
            //}

            return (int)RC.SUCCESS;
        }

        /// <summary>
        /// 重命名节点的名称
        /// </summary>
        /// <param name="oldNameFullPath">修改前节点的全路径名称</param>
        /// <param name="newNameFullPath">修改后节点的全路径名称</param>
        /// <returns>
        ///   RC.NO_OPEN        ：数据库尚未打开
        ///   RC.PAR_NULL       ：参数为空
        ///   PAR_LEN_ERR       ：参数长度有误
        ///   RC.OP_FAIL        ：数据库操作失败 
        ///   RC.NO_EXIST       ：记录不存在(修改前的节点不存在)
        ///   RC.MODIFIED_EXIST ：记录已经存在(修改后的节点已经存在)
        ///   RC.SUCCESS        ：成功
        /// </returns>
        private int domain_record_rename(string oldNameFullPath, string newNameFullPath)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return (int)RC.NO_OPEN;
            }

            if (string.IsNullOrEmpty(oldNameFullPath) || string.IsNullOrEmpty(newNameFullPath))
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.PAR_NULL], "DB", LogCategory.I);
                return (int)RC.PAR_NULL;
            }

            if (oldNameFullPath.Length > 1024 || newNameFullPath.Length > 1024)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.PAR_LEN_ERR], "DB", LogCategory.I);
                return (int)RC.PAR_LEN_ERR;
            }

            string newName = "";
            int j = newNameFullPath.LastIndexOf(".");
            newName = newNameFullPath.Substring(j + 1);

            //检查修改前节点是否存在
            if ((int)RC.NO_EXIST == domain_record_exist(oldNameFullPath))
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_EXIST] + "(修改前的节点不存在)", "DB", LogCategory.I);
                return (int)RC.NO_EXIST;
            }

            //检查修改后的节点是否已经存在
            if ((int)RC.EXIST == domain_record_exist(newNameFullPath))
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.MODIFIED_EXIST], "DB", LogCategory.I);
                return (int)RC.MODIFIED_EXIST;
            }

            //重命名本节点本身
            string sql = string.Format("update domain set name = '{0}',nameFullPath = '{1}' where nameFullPath = '{2}'", newName, newNameFullPath, oldNameFullPath);

            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    if (cmd.ExecuteNonQuery() < 0)
                    {
                        Logger.Trace(LogInfoType.EROR, sql, "DB", LogCategory.I);
                        return (int)RC.OP_FAIL;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(LogInfoType.EROR, e.Message + e.StackTrace, "DB", LogCategory.I);
                dicRTV[(int)RC.OP_FAIL] = string.Format("数据库操作失败:{0}", e.Message + e.StackTrace); 
                return (int)RC.OP_FAIL;
            }

            //重命名本节点本身下的所有子节点           
            sql = string.Format("update domain set nameFullPath = REPLACE(nameFullPath, '{0}.', '{1}.') where nameFullPath like '%%{0}.%%'", oldNameFullPath, newNameFullPath);

            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    if (cmd.ExecuteNonQuery() < 0)
                    {
                        Logger.Trace(LogInfoType.EROR, sql, "DB", LogCategory.I);
                        return (int)RC.OP_FAIL;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(LogInfoType.EROR, e.Message + e.StackTrace, "DB", LogCategory.I);
                dicRTV[(int)RC.OP_FAIL] = string.Format("数据库操作失败:{0}", e.Message + e.StackTrace); 
                return (int)RC.OP_FAIL;
            }

            return (int)RC.SUCCESS;
        }

        /// <summary>
        /// 更新节点的描述
        /// </summary>
        /// <param name="nameFullPath">节点的全路径名称</param>
        /// <param name="newdes">要修改成什么样的描述</param>
        /// <returns>
        ///   RC.NO_OPEN        ：数据库尚未打开
        ///   RC.PAR_NULL       ：参数为空
        ///   PAR_LEN_ERR       ：参数长度有误
        ///   RC.OP_FAIL        ：数据库操作失败 
        ///   RC.NO_EXIST       ：记录不存在(修改前的节点不存在)       
        ///   RC.SUCCESS        ：成功
        /// </returns>
        private int domain_record_update_des(string nameFullPath, string newdes)
        {
            string des = "";

            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return (int)RC.NO_OPEN;
            }

            if (string.IsNullOrEmpty(nameFullPath))
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.PAR_NULL], "DB", LogCategory.I);
                return (int)RC.PAR_NULL;
            }

            if (nameFullPath.Length > 1024)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.PAR_LEN_ERR], "DB", LogCategory.I);
                return (int)RC.PAR_LEN_ERR;
            }

            if (string.IsNullOrEmpty(newdes))
            {
                des = "";
            }
            else
            {
                des = newdes;
            }

            //检查修改前节点是否存在
            if ((int)RC.NO_EXIST == domain_record_exist(nameFullPath))
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_EXIST] + "(修改前的节点不存在)", "DB", LogCategory.I);
                return (int)RC.NO_EXIST;
            }

            //修改节点的描述
            string sql = string.Format("update domain set des = '{0}' where nameFullPath = '{1}'", des, nameFullPath);

            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    if (cmd.ExecuteNonQuery() < 0)
                    {
                        Logger.Trace(LogInfoType.EROR, sql, "DB", LogCategory.I);
                        return (int)RC.OP_FAIL;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(LogInfoType.EROR, e.Message + e.StackTrace, "DB", LogCategory.I);
                dicRTV[(int)RC.OP_FAIL] = string.Format("数据库操作失败:{0}", e.Message + e.StackTrace); 
                return (int)RC.OP_FAIL;
            }

            return (int)RC.SUCCESS;
        }

        /// <summary>
        /// 更新节点的描述
        /// </summary>
        /// <param name="nameFullPath">节点的全路径名称</param>
        /// <param name="str">要修改成什么样的经纬度</param>
        /// <returns>
        ///   RC.NO_OPEN        ：数据库尚未打开
        ///   RC.PAR_NULL       ：参数为空
        ///   PAR_LEN_ERR       ：参数长度有误
        ///   RC.OP_FAIL        ：数据库操作失败 
        ///   RC.NO_EXIST       ：记录不存在(修改前的节点不存在) 
        ///   IS_NOT_STATION    ：域不是站点
        ///   RC.SUCCESS        ：成功
        /// </returns>
        private int domain_record_update_longitude_latitude(string nameFullPath, strDomian str)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return (int)RC.NO_OPEN;
            }

            if (string.IsNullOrEmpty(nameFullPath))
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.PAR_NULL], "DB", LogCategory.I);
                return (int)RC.PAR_NULL;
            }

            if (nameFullPath.Length > 1024)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.PAR_LEN_ERR], "DB", LogCategory.I);
                return (int)RC.PAR_LEN_ERR;
            }

            //检查修改前节点是否存在
            if ((int)RC.NO_EXIST == domain_record_exist(nameFullPath))
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_EXIST] + "(修改前的节点不存在)", "DB", LogCategory.I);
                return (int)RC.NO_EXIST;
            }

            if ((int)RC.IS_NOT_STATION == domain_record_is_station(nameFullPath))
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.IS_NOT_STATION], "DB", LogCategory.I);
                return (int)RC.IS_NOT_STATION;
            }

            if (string.IsNullOrEmpty(str.longitude))
            {
                str.longitude = "";
            }

            if (string.IsNullOrEmpty(str.latitude))
            {
                str.latitude = "";
            }


            //修改节点的经纬度
            string sql = string.Format("update domain set longitude = '{0}',latitude = '{1}' where nameFullPath = '{2}'", str.longitude, str.latitude, nameFullPath);

            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    if (cmd.ExecuteNonQuery() < 0)
                    {
                        Logger.Trace(LogInfoType.EROR, sql, "DB", LogCategory.I);
                        return (int)RC.OP_FAIL;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(LogInfoType.EROR, e.Message + e.StackTrace, "DB", LogCategory.I);
                dicRTV[(int)RC.OP_FAIL] = string.Format("数据库操作失败:{0}", e.Message + e.StackTrace);
                return (int)RC.OP_FAIL;
            }

            return (int)RC.SUCCESS;
        }

        /// <summary>
        /// 获取域表中的各条记录
        /// </summary>
        /// <param name="dt">
        /// 返回的DataTable，包含的列为：id,name,parentId,nameFullPath,isStation,longitude,latitude,des
        /// </param>
        /// <param name="isStationFlag">
        /// 是否只返回站点的记录
        /// 0：所有记录
        /// 1：只返回是站点的记录
        /// </param>
        /// <returns>
        ///   RC.NO_OPEN   ：数据库尚未打开
        ///   RC.OP_FAIL   ：数据库操作失败 
        ///   RC.SUCCESS   ：成功 
        /// </returns>
        private int domain_record_entity_get(ref DataTable dt, int isStationFlag)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return (int)RC.NO_OPEN;
            }

            dt = new DataTable("domain");

            DataColumn column0 = new DataColumn();
            column0.DataType = System.Type.GetType("System.Int32");
            column0.ColumnName = "id";

            DataColumn column1 = new DataColumn();
            column1.DataType = System.Type.GetType("System.String");
            column1.ColumnName = "name";

            DataColumn column2 = new DataColumn();
            column2.DataType = System.Type.GetType("System.Int32");
            column2.ColumnName = "parentId";

            DataColumn column3 = new DataColumn();
            column3.DataType = System.Type.GetType("System.String");
            column3.ColumnName = "nameFullPath";

            DataColumn column4 = new DataColumn();
            column4.DataType = System.Type.GetType("System.Int32");
            column4.ColumnName = "isStation";

            // 2108-12-19
            DataColumn column5 = new DataColumn();
            column5.DataType = System.Type.GetType("System.String");
            column5.ColumnName = "longitude";

            // 2108-12-19
            DataColumn column6 = new DataColumn();
            column6.DataType = System.Type.GetType("System.String");
            column6.ColumnName = "latitude";

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

            string sql = "";

            if (1 == isStationFlag)
            {
                sql = string.Format("select * from domain where isStation = 1");
            }
            else
            {
                sql = string.Format("select * from domain");
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

                            row["id"] = Convert.ToInt32(dr["id"]);

                            if (!string.IsNullOrEmpty(dr["name"].ToString()))
                            {
                                row["name"] = dr["name"].ToString();
                            }

                            row["parentId"] = Convert.ToInt32(dr["parentId"]);

                            if (!string.IsNullOrEmpty(dr["nameFullPath"].ToString()))
                            {
                                row["nameFullPath"] = dr["nameFullPath"].ToString();
                            }

                            row["isStation"] = Convert.ToInt32(dr["isStation"]);

                            // 2018-12-19
                            if (!string.IsNullOrEmpty(dr["longitude"].ToString()))
                            {
                                row["longitude"] = dr["longitude"].ToString();
                            }
                            else
                            {
                                row["longitude"] = "";
                            }

                            // 2018-12-19
                            if (!string.IsNullOrEmpty(dr["latitude"].ToString()))
                            {
                                row["latitude"] = dr["latitude"].ToString();
                            }
                            else
                            {
                                row["latitude"] = "";
                            }


                            if (!string.IsNullOrEmpty(dr["des"].ToString()))
                            {
                                row["des"] = dr["des"].ToString();
                            }
                            else
                            {
                                row["des"] = "";
                            }

                            dt.Rows.Add(row);
                        }
                        dr.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(LogInfoType.EROR, e.Message + e.StackTrace, "DB", LogCategory.I);
                dicRTV[(int)RC.OP_FAIL] = string.Format("数据库操作失败:{0}", e.Message + e.StackTrace); 
                return (int)RC.OP_FAIL;
            }

            return (int)RC.SUCCESS;
        }

        /// <summary>
        /// 获取域表中的各条记录
        /// </summary>
        /// <param name="listSattionIdSet">
        /// 返回的所有站点id集合
        /// </param>
        /// <returns>
        ///   RC.NO_OPEN   ：数据库尚未打开
        ///   RC.OP_FAIL   ：数据库操作失败 
        ///   RC.SUCCESS   ：成功 
        /// </returns>
        private int domain_record_station_id_set_get(ref List<string> listSattionIdSet)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return (int)RC.NO_OPEN;
            }

            listSattionIdSet = new List<string>();
            string sql = string.Format("select id from domain where isStation = 1");

            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            if (!string.IsNullOrEmpty(dr[0].ToString()))
                            {
                                listSattionIdSet.Add(dr[0].ToString());
                            }
                        }
                        dr.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(LogInfoType.EROR, e.Message + e.StackTrace, "DB", LogCategory.I);
                dicRTV[(int)RC.OP_FAIL] = string.Format("数据库操作失败:{0}", e.Message + e.StackTrace); 
                return (int)RC.OP_FAIL;
            }

            return (int)RC.SUCCESS;
        }

        /// <summary>
        /// 用于快速通过设备的全名找到设备对应的各种信息
        /// 如：设备.深圳.福田.中心广场.西北监控.LTE-FDD-B3，其中
        /// 设备.深圳.福田.中心广场.西北监控为域名，LTE-FDD-B3为名称
        /// 系统启动后或设备有更改后获取该字典到内存中
        /// string = 设备.深圳.福田.中心广场.西北监控.LTE-FDD-B3
        /// strDevice = 设备的各个字段
        /// </summary>
        /// <param name="dic">返回的字典</param>
        /// <returns>
        ///   RC.NO_OPEN   ：数据库尚未打开
        ///   RC.OP_FAIL   ：数据库操作失败 
        ///   RC.SUCCESS   ：成功
        /// </returns>
        private int domain_dictionary_info_join_get(ref Dictionary<string, strDevice> dic, ref Dictionary<string, string> dicDevIdStationName)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return (int)RC.NO_OPEN;
            }

            Dictionary<string, strDevice> dicTemp = new Dictionary<string, strDevice>();
            Dictionary<string, string> dicDevISN = new Dictionary<string, string>();
        
            //public int id;
            //public string bsName;
            //public string sn;

            //public string ipAddr;
            //public string type;
            //public string s1Status;
            //public string connHS;
            //public string tac;
            //public string enbId;
            //public string cellId;
            //public string earfcn;
            //public string pci;
            //public string updateMode;
            //public string curVersion;
            //public string curWarnCnt;
            //public string onoffLineTime;
            //public string aliasName;
            //public string des;  

            // 2018-10-09
            string sql = string.Format("SELECT a.name,a.nameFullPath,b.* FROM (select id,name,nameFullPath from domain where isStation = 1) AS a INNER JOIN deviceinfo As b ON a.id = b.affDomainId");

            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {                            
                            if (!string.IsNullOrEmpty(dr["nameFullPath"].ToString()) && !string.IsNullOrEmpty(dr["bsName"].ToString()))
                            {
                                strDevice strDev = new strDevice();
                                string completeName = string.Format("{0}.{1}", dr["nameFullPath"].ToString(), dr["bsName"].ToString());                                
                                
                                if (!string.IsNullOrEmpty(dr["id"].ToString()))
                                {
                                    strDev.id = int.Parse(dr["id"].ToString());
                                }

                                strDev.bsName = dr["bsName"].ToString();

                                // 2018-12-25
                                strDev.Fullname = completeName;

                                // 2018-10-09
                                if (!string.IsNullOrEmpty(dr[0].ToString()))
                                {
                                    strDev.station_and_name = string.Format("{0}.{1}", dr[0].ToString(), dr["bsName"].ToString());
                                }

                                if (!string.IsNullOrEmpty(dr["sn"].ToString()))
                                {
                                    strDev.sn = dr["sn"].ToString();
                                }

                                if (!string.IsNullOrEmpty(dr["ipAddr"].ToString()))
                                {
                                    strDev.ipAddr = dr["ipAddr"].ToString();
                                }

                                if (!string.IsNullOrEmpty(dr["type"].ToString()))
                                {
                                    strDev.type = dr["type"].ToString();
                                }

                                if (!string.IsNullOrEmpty(dr["s1Status"].ToString()))
                                {
                                    strDev.s1Status = dr["s1Status"].ToString();
                                }

                                if (!string.IsNullOrEmpty(dr["connHS"].ToString()))
                                {
                                    strDev.connHS = dr["connHS"].ToString();
                                }

                                if (!string.IsNullOrEmpty(dr["tac"].ToString()))
                                {
                                    strDev.tac = dr["tac"].ToString();
                                }

                                if (!string.IsNullOrEmpty(dr["enbId"].ToString()))
                                {
                                    strDev.enbId = dr["enbId"].ToString();
                                }

                                if (!string.IsNullOrEmpty(dr["cellId"].ToString()))
                                {
                                    strDev.cellId = dr["cellId"].ToString();
                                }

                                if (!string.IsNullOrEmpty(dr["earfcn"].ToString()))
                                {
                                    strDev.earfcn = dr["earfcn"].ToString();
                                }

                                if (!string.IsNullOrEmpty(dr["pci"].ToString()))
                                {
                                    strDev.pci = dr["pci"].ToString();
                                }

                                if (!string.IsNullOrEmpty(dr["updateMode"].ToString()))
                                {
                                    strDev.updateMode = dr["updateMode"].ToString();
                                }

                                if (!string.IsNullOrEmpty(dr["curVersion"].ToString()))
                                {
                                    strDev.curVersion = dr["curVersion"].ToString();
                                }

                                if (!string.IsNullOrEmpty(dr["curVersion"].ToString()))
                                {
                                    strDev.curVersion = dr["curVersion"].ToString();
                                }

                                if (!string.IsNullOrEmpty(dr["onoffLineTime"].ToString()))
                                {
                                    strDev.onoffLineTime = dr["onoffLineTime"].ToString();
                                }

                                if (!string.IsNullOrEmpty(dr["aliasName"].ToString()))
                                {
                                    strDev.aliasName = dr["aliasName"].ToString();
                                }

                                if (!string.IsNullOrEmpty(dr["des"].ToString()))
                                {
                                    strDev.des = dr["des"].ToString();
                                }
                                                                
                                if (!string.IsNullOrEmpty(dr["affDomainId"].ToString()))
                                {
                                    strDev.affDomainId = dr["affDomainId"].ToString();
                                }                                

                                if (!dicTemp.ContainsKey(completeName))
                                {
                                    dicTemp.Add(completeName, strDev);
                                }

                                // 2018-10-11
                                if (!dicDevISN.ContainsKey(strDev.id.ToString()))
                                {
                                    dicDevISN.Add(strDev.id.ToString(), strDev.station_and_name);
                                }
                            }
                        }
                        dr.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(LogInfoType.EROR, e.Message + e.StackTrace, "DB", LogCategory.I);
                dicRTV[(int)RC.OP_FAIL] = string.Format("数据库操作失败:{0}", e.Message + e.StackTrace);
                return (int)RC.OP_FAIL;
            }

            dic = new Dictionary<string, strDevice>();
            dic = dicTemp;

            dicDevIdStationName = new Dictionary<string, string>();
            dicDevIdStationName = dicDevISN;

            return (int)RC.SUCCESS;
        }

        /// <summary>
        /// 用于快速通过设备的全名找到设备对应的各种信息
        /// 如：设备.深圳.福田.中心广场.西北监控.LTE-FDD-B3，其中
        /// 设备.深圳.福田.中心广场.西北监控为域名，LTE-FDD-B3为名称
        /// 系统启动后或设备有更改后获取该字典到内存中
        /// string = 设备.深圳.福田.中心广场.西北监控.LTE-FDD-B3
        /// strDevice = 设备的各个字段
        /// </summary>
        /// <param name="dic">返回的字典</param>
        /// <returns>
        ///   RC.NO_OPEN   ：数据库尚未打开
        ///   RC.OP_FAIL   ：数据库操作失败 
        ///   RC.SUCCESS   ：成功
        /// </returns>
        private int domain_dictionary_info_join_imsi_des_get(ref Dictionary<string, Dictionary<string, string>> dic)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return (int)RC.NO_OPEN;
            }

            List<string> lstId = new List<string>();
            Dictionary<string, Dictionary<string, string>> dicTemp = new Dictionary<string, Dictionary<string, string>>();

            string sql = string.Format("select id from deviceinfo");
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            if (!string.IsNullOrEmpty(dr["id"].ToString()))
                            {
                                lstId.Add(dr["id"].ToString());
                            }
                        }
                        dr.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(LogInfoType.EROR, e.Message + e.StackTrace, "DB", LogCategory.I);
                dicRTV[(int)RC.OP_FAIL] = string.Format("数据库操作失败:{0}", e.Message + e.StackTrace);
                return (int)RC.OP_FAIL;
            }


            #region 获取imsi和des的对应关系

            // 2018-09-10

            //foreach (string id in lstId)
            //{
            //    Dictionary<string, string> dicImsiDes = new Dictionary<string, string>();

            //    if ((int)RC.SUCCESS == bwlist_record_entity_imsi_des_get(int.Parse(id), ref dicImsiDes))
            //    {
            //        if (dicImsiDes.Count > 0)
            //        {
            //            dicTemp.Add(id, dicImsiDes);
            //        }
            //    }
            //}

            #endregion

            dic = new Dictionary<string, Dictionary<string, string>>();
            dic = dicTemp;

            return (int)RC.SUCCESS;
        }

        /// <summary>
        /// 获取域表中的各条叶子节点记录
        /// </summary>
        /// <param name="dt">
        /// 返回的DataTable，包含的列为：id,name,nameFullPath,isStation
        /// </param>
        /// <returns>
        ///   RC.NO_OPEN   ：数据库尚未打开
        ///   RC.OP_FAIL   ：数据库操作失败 
        ///   RC.SUCCESS   ：成功 
        /// </returns>
        private int domain_record_leaf_get(ref DataTable dt)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return (int)RC.NO_OPEN;
            }

            dt = new DataTable("domain");

            DataColumn column0 = new DataColumn();
            column0.DataType = System.Type.GetType("System.Int32");
            column0.ColumnName = "id";

            DataColumn column1 = new DataColumn();
            column1.DataType = System.Type.GetType("System.String");
            column1.ColumnName = "name";

            DataColumn column2 = new DataColumn();
            column2.DataType = System.Type.GetType("System.String");
            column2.ColumnName = "nameFullPath";

            DataColumn column3 = new DataColumn();
            column3.DataType = System.Type.GetType("System.Int32");
            column3.ColumnName = "isStation";

            dt.Columns.Add(column0);
            dt.Columns.Add(column1);
            dt.Columns.Add(column2);
            dt.Columns.Add(column3);

            DataTable dtAll = new DataTable();
            int rv = domain_record_entity_get(ref dtAll, 0);
            if (rv != (int)RC.SUCCESS)
            {
                return rv;
            }

            List<int> idList = new List<int>();
            List<int> parentIdList = new List<int>();
            List<int> leafIdList = new List<int>();

            foreach (DataRow dr in dtAll.Rows)
            {
                int id = int.Parse(dr["id"].ToString());
                int parentId = int.Parse(dr["parentId"].ToString());

                idList.Add(id);
                parentIdList.Add(parentId);
            }

            string subSql = "";
            foreach (int inx in idList)
            {
                if (!parentIdList.Contains(inx))
                {
                    leafIdList.Add(inx);
                    subSql += string.Format("id = {0} or ", inx);
                }
            }

            if (subSql != "")
            {
                subSql = subSql.Remove(subSql.Length - 3, 3);
            }


            string sql = string.Format("select id,name,nameFullPath,isStation from domain where {0}", subSql);

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
                            row[3] = Convert.ToInt32(dr[3]);

                            dt.Rows.Add(row);
                        }
                        dr.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(LogInfoType.EROR, e.Message + e.StackTrace, "DB", LogCategory.I);
                dicRTV[(int)RC.OP_FAIL] = string.Format("数据库操作失败:{0}", e.Message + e.StackTrace); 
                return (int)RC.OP_FAIL;
            }

            return (int)RC.SUCCESS;
        }

        /// <summary>
        /// 获取一个节点下所有站点下所有设备Id的列表
        /// </summary>
        /// <param name="nameFullPath">节点的全路径名称</param>
        /// <param name="listDevId">所有设备Id的列表</param>
        /// <param name="listDevSn">所有设备SN的列表</param>
        /// <returns>
        ///   RC.NO_OPEN         ：数据库尚未打开
        ///   RC.PAR_NULL        ：参数为空
        ///   PAR_LEN_ERR        ：参数长度有误
        ///   RC.OP_FAIL         ：数据库操作失败 
        ///   RC.NO_EXIST        ：记录不存在        
        ///   RC.SUCCESS         ：成功
        /// </returns>
        private int domain_record_device_id_list_get(string nameFullPath, ref List<int> listDevId,ref List<string> listDevSn)
        {
            //int rtv = -1;
            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return (int)RC.NO_OPEN;
            }

            if (string.IsNullOrEmpty(nameFullPath))
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.PAR_NULL], "DB", LogCategory.I);
                return (int)RC.PAR_NULL;
            }

            if (nameFullPath.Length > 1024)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.PAR_LEN_ERR], "DB", LogCategory.I);
                return (int)RC.PAR_LEN_ERR;
            }

            //检查记录是否存在
            if ((int)RC.NO_EXIST == domain_record_exist(nameFullPath))
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_EXIST], "DB", LogCategory.I);
                return (int)RC.NO_EXIST;
            }

            listDevId = new List<int>();
            listDevSn = new List<string>();

            #region 分成两步

            //List<int> listID = new List<int>();

            //rtv = domain_record_station_list_get(nameFullPath, ref listID);
            //if ((int)RC.SUCCESS != rtv)
            //{
            //    return rtv;
            //}

            //string sqlSub = "";
            //for (int i = 0; i < listID.Count; i++)
            //{
            //    if (i == (listID.Count - 1))
            //    {
            //        sqlSub += string.Format("affDomainId = {0} ", listID[i]);
            //    }
            //    else
            //    {
            //        sqlSub += string.Format("affDomainId = {0} or ", listID[i]);
            //    }
            //}


            //string sql = string.Format("select id from device where {0}",sqlSub);

            #endregion

            #region 一步搞定

            string sql = string.Format("SELECT b.id,b.sn FROM (select id from domain where (nameFullPath like '{0}.%%' or nameFullPath = '{0}') and isStation = 1) AS a INNER JOIN deviceinfo As b ON a.id = b.affDomainId", nameFullPath);

            #endregion

            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            if (!string.IsNullOrEmpty(dr["id"].ToString()))
                            {
                                listDevId.Add(int.Parse(dr["id"].ToString()));
                            }

                            if (!string.IsNullOrEmpty(dr["sn"].ToString()))
                            {
                                listDevSn.Add(dr["sn"].ToString());
                            }
                        }
                        dr.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(LogInfoType.EROR, e.Message + e.StackTrace, "DB", LogCategory.I);
                dicRTV[(int)RC.OP_FAIL] = string.Format("数据库操作失败:{0}", e.Message + e.StackTrace);                
                return (int)RC.OP_FAIL;
            }

            return (int)RC.SUCCESS;
        }

        /// <summary>
        /// 获取域表中id和nameFullPath的字典
        /// </summary>
        /// <param name="dic">返回的字典</param>
        /// <returns>
        ///   RC.NO_OPEN   ：数据库尚未打开
        ///   RC.OP_FAIL   ：数据库操作失败 
        ///   RC.SUCCESS   ：成功
        /// </returns>
        private int domain_dictionary_id_nameFullPath_get(ref Dictionary<string, string> dic)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return (int)RC.NO_OPEN;
            }

            string sql = string.Format("select id,nameFullPath from domain where isStation = 1");

            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            if (!string.IsNullOrEmpty(dr["id"].ToString()) && !string.IsNullOrEmpty(dr["nameFullPath"].ToString()))
                            {
                                dic.Add(dr["id"].ToString(), dr["nameFullPath"].ToString());
                            }
                        }
                        dr.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(LogInfoType.EROR, e.Message + e.StackTrace, "DB", LogCategory.I);
                dicRTV[(int)RC.OP_FAIL] = string.Format("数据库操作失败:{0}", e.Message + e.StackTrace);                
                return (int)RC.OP_FAIL;
            }

            return (int)RC.SUCCESS;
        }

        /// <summary>
        /// 清空所有的记录(保留根节点)
        /// </summary>    
        /// <returns>
        ///   RC.NO_OPEN         ：数据库尚未打开
        ///   RC.OP_FAIL         ：数据库操作失败 
        ///   RC.SUCCESS         ：成功
        /// </returns>
        private int domain_record_clear()
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return (int)RC.NO_OPEN;
            }

            string sql = string.Format("delete from domain where name != '设备'");

            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    if (cmd.ExecuteNonQuery() < 0)
                    {
                        Logger.Trace(LogInfoType.EROR, sql, "DB", LogCategory.I);
                        return (int)RC.OP_FAIL;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(LogInfoType.EROR, e.Message + e.StackTrace, "DB", LogCategory.I);
                dicRTV[(int)RC.OP_FAIL] = string.Format("数据库操作失败:{0}", e.Message + e.StackTrace); 
                return (int)RC.OP_FAIL;
            }

            return (int)RC.SUCCESS;
        }



        /// <summary>
        ///  app获取所有的域信息(设备树)请求
        /// </summary>
        /// <param name="lstDomain">返回的域信息列表</param>
        /// <param name="errInfo">返回的错误信息</param>
        /// <returns>
        /// 0   ： 成功
        /// 非0 ： 失败
        /// </returns>
        public int app_all_domain_request(ref List<strDomian> lstDomain,ref string errInfo)
        {
            int rtv = 0;
            DataTable dt = new DataTable();
            rtv = domain_record_entity_get(ref dt, 0);
            if (rtv != 0)
            {
                errInfo = get_rtv_str(rtv);
                return rtv;
            }

            lstDomain = new List<strDomian>();
            foreach (DataRow dr in dt.Rows)
            {
                strDomian record = new strDomian();

                //(1)
                if (!string.IsNullOrEmpty(dr["id"].ToString()))
                {
                    record.id = int.Parse(dr["id"].ToString());
                }
                else
                {
                    continue;
                }

                //(2)
                if (!string.IsNullOrEmpty(dr["name"].ToString()))
                {
                    record.name = dr["name"].ToString();
                }
                else
                {
                    record.name = "";
                }

                //(3)
                if (!string.IsNullOrEmpty(dr["parentId"].ToString()))
                {
                    record.parentId = int.Parse(dr["parentId"].ToString());
                }
                else
                {
                    continue;
                }

                //(4)
                if (!string.IsNullOrEmpty(dr["nameFullPath"].ToString()))
                {
                    record.nameFullPath = dr["nameFullPath"].ToString();
                }
                else
                {
                    record.nameFullPath = "";
                }

                //(5)
                if (!string.IsNullOrEmpty(dr["isStation"].ToString()))
                {
                    record.isStation = int.Parse(dr["isStation"].ToString());
                }
                else
                {
                    continue;
                }

                //(6)
                if (!string.IsNullOrEmpty(dr["longitude"].ToString()))
                {
                    record.longitude = dr["longitude"].ToString();
                }
                else
                {
                    record.longitude = "";
                }

                //(7)
                if (!string.IsNullOrEmpty(dr["latitude"].ToString()))
                {
                    record.latitude = dr["latitude"].ToString();
                }
                else
                {
                    record.latitude = "";
                }

                //(8)
                if (!string.IsNullOrEmpty(dr["des"].ToString()))
                {
                    record.des = dr["des"].ToString();
                }
                else
                {
                    record.des = "";
                }

                lstDomain.Add(record);
            }

            return rtv;
        }
       
        /// <summary>
        /// 添加一个域
        /// </summary>
        /// <param name="name">域名称</param>
        /// <param name="parentNameFullPath">域的父亲全路径</param>
        /// <param name="isStation">是否为站点，0不是，1是</param>
        /// <param name="des">描述</param>
        /// <param name="errInfo">返回的错误信息</param>
        /// <returns>
        /// 0   ： 成功
        /// 非0 ： 失败
        /// </returns>
        public int app_add_domain_request(string name, string parentNameFullPath, int isStation, string des, ref string errInfo)
        {
            int rtv = -1;

            if (string.IsNullOrEmpty(name))
            {
                errInfo = string.Format("name 字段为空.");
                return rtv;
            }

            if (string.IsNullOrEmpty(parentNameFullPath))
            {
                errInfo = string.Format("parentNameFullPath 字段为空.");
                return rtv;
            }

            if (isStation != 0 && isStation != 1)
            {
                errInfo = string.Format("isStation 字段非法.");
                return rtv;
            }

            if (string.IsNullOrEmpty(des))
            {
                des = "des";
            }

            rtv = domain_record_insert(name, parentNameFullPath, isStation, des);
            if (rtv != 0)
            {
                errInfo = get_rtv_str(rtv);
                return rtv;
            }

            return rtv;
        }

        /// <summary>
        /// 删除一个域
        /// </summary>
        /// <param name="nameFullPath">域的全路径</param>
        /// <param name="errInfo">返回的错误信息</param>
        /// <returns>
        /// 0   ： 成功
        /// 非0 ： 失败
        /// </returns>
        public int app_del_domain_request(string nameFullPath, ref string errInfo)
        {
            int rtv = -1;

            if (string.IsNullOrEmpty(nameFullPath))
            {
                errInfo = string.Format("nameFullPath 字段为空.");
                return rtv;
            }

            rtv = domain_record_delete(nameFullPath);
            if (rtv != 0)
            {
                errInfo = get_rtv_str(rtv);
                return rtv;
            }

            #region 重新获取gDicDevFullName

            if (rtv == 0)
            {
                if (0 == domain_dictionary_info_join_get(ref gDicDevFullName, ref gDicDevId_Station_DevName))
                {                    
                    Logger.Trace(LogInfoType.INFO, "gDicDevFullName -> 获取OK！", "DB", LogCategory.I);
                    print_dic_dev_fullname_info("app_del_domain_request", gDicDevFullName);
                }
                else
                {                    
                    Logger.Trace(LogInfoType.INFO, "gDicDevFullName -> 获取FAILED！", "DB", LogCategory.I);
                }
            }

            #endregion

            return rtv;
        }

        #endregion

        #region 02 - apaction操作

        /// <summary>
        /// 清空所有的记录
        /// </summary>    
        /// <returns>
        ///   RC.NO_OPEN         ：数据库尚未打开
        ///   RC.OP_FAIL         ：数据库操作失败 
        ///   RC.SUCCESS         ：成功
        /// </returns>
        private int apaction_record_clear()
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return (int)RC.NO_OPEN;
            }

            string sql = string.Format("delete from apaction");

            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    if (cmd.ExecuteNonQuery() < 0)
                    {
                        Logger.Trace(LogInfoType.EROR, sql, "DB", LogCategory.I);       
                        return (int)RC.OP_FAIL;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(LogInfoType.EROR, e.Message + e.StackTrace, "DB", LogCategory.I);
                dicRTV[(int)RC.OP_FAIL] = string.Format("数据库操作失败:{0}", e.Message + e.StackTrace); 
                return (int)RC.OP_FAIL;
            }

            return (int)RC.SUCCESS;
        }

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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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

            string sql = string.Format("insert into apaction(sn) values('{0}')", sn);
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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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
        /// <param name="upgradeTimer">UpgradTask类型是该字段有效，其他类型的忽略</param>
        /// <returns> 
        ///   -1 ：数据库尚未打开
        ///   -2 ：参数有误
        ///   -3 ：记录不存在
        ///   -4 ：数据库操作失败 
        ///    0 : 更新成功
        /// </returns>
        public int apaction_record_update(string sn, string id, TaskType type, TaskStatus status, string upgradeTimer)
        {
            string sql = "";

            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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
                        sql = string.Format("update apaction set upgradeId = '{0}',upgradeTimer = '{1}',upgradeStartTime = now(),upgradeStatus = '{2}' where sn = '{3}'", id,upgradeTimer, (int)status, sn);
                    }
                    else if ((status == TaskStatus.ReponseOk) || (status == TaskStatus.ReponseFail))
                    {
                        sql = string.Format("update apaction set upgradeId = '{0}',upgradeTimer = '{1}',upgradeEndTime = now(),upgradeStatus = '{2}' where sn = '{3}'", id,upgradeTimer, (int)status, sn);
                    }
                    else
                    {
                        sql = string.Format("update apaction set upgradeId = '{0}',upgradeTimer = '{1}',upgradeStatus = '{2}' where sn = '{3}'", id, upgradeTimer,(int)status, sn);
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
        /// <param name="taskType">返回对应的TaskType</param>
        /// <param name="taskId">返回对应的taskId</param>
        /// <param name="upgradeTimer">
        /// 返回对应的upgradeTimer
        /// UpgradTask类型是该字段有效，其他类型的忽略
        /// </param>
        /// <param name="sn">要获取ap的sn号</param>
        /// <returns>
        /// 成功 ： 对应的XML内容
        /// 失败 ： null
        ///        失败包含数据库没连接，参数有误，数据库操作失败等 
        /// </returns>
        public byte[] GetTaskBySN(ref TaskType taskType, ref string taskId, ref string upgradeTimer, string sn)
        {
            string id = null;
            string status = null;
            List<string> lstID = new List<string>();

            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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
            //string sql = string.Format("select upgradeId,upgradeStatus,getLogId," +
            //    "getLogStatus,getParId,getParStatus,setParId,setParStatus,rebootId,rebootStatus " +
            //    "from apaction where sn = '{0}'", sn);

            string sql = string.Format("select * from apaction where sn = '{0}'", sn);

            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            if (!string.IsNullOrEmpty(dr["upgradeStatus"].ToString()))
                            {
                                if ((TaskStatus)dr["upgradeStatus"] == TaskStatus.SendReqst)
                                {
                                    id = dr["upgradeId"].ToString();
                                    status = dr["upgradeStatus"].ToString();

                                    // 2019-01-17
                                    upgradeTimer = dr["upgradeTimer"].ToString();

                                    DateTime nowTime = DateTime.Now;
                                    DateTime runTime = Convert.ToDateTime(upgradeTimer);

                                    if ((id.Length > 0) && (DateTime.Compare(nowTime, runTime) >= 0)) //break;
                                    {
                                        lstID.Add(id);
                                    }
                                }
                            }

                            if (!string.IsNullOrEmpty(dr["getLogStatus"].ToString()))
                            {
                                if ((TaskStatus)dr["getLogStatus"] == TaskStatus.SendReqst)
                                {
                                    id = dr["getLogId"].ToString();
                                    status = dr["getLogStatus"].ToString();

                                    if (id.Length > 0) //break;
                                    {
                                        lstID.Add(id);
                                    }
                                }
                            }

                            if (!string.IsNullOrEmpty(dr["getParStatus"].ToString()))
                            {
                                if ((TaskStatus)dr["getParStatus"] == TaskStatus.SendReqst)
                                {
                                    id = dr["getParId"].ToString();
                                    status = dr["getParStatus"].ToString();

                                    if (id.Length > 0) //break;
                                    {
                                        lstID.Add(id);
                                    }
                                }
                            }

                            if (!string.IsNullOrEmpty(dr["setParStatus"].ToString()))
                            {
                                if ((TaskStatus)dr["setParStatus"] == TaskStatus.SendReqst)
                                {
                                    id = dr["setParId"].ToString();
                                    status = dr["setParStatus"].ToString();

                                    if (id.Length > 0) //break;
                                    {
                                        lstID.Add(id);
                                    }
                                }
                            }

                            if (!string.IsNullOrEmpty(dr["rebootStatus"].ToString()))
                            {
                                if ((TaskStatus)dr["rebootStatus"] == TaskStatus.SendReqst)
                                {
                                    id = dr["rebootId"].ToString();
                                    status = dr["rebootStatus"].ToString();

                                    if (id.Length > 0) //break;
                                    {
                                        lstID.Add(id);
                                    }
                                }
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


            //if (id != null)
            foreach (string str in lstID)
            {
                sql = string.Format("select actionId,actionType,actionXmlText from schedulerinfo where actionId = '{0}'", str);
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
                            //else
                            //{               
                            //    return null;
                            //}
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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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
                if (str.Equals("upgradeStatus"))
                {
                    sql = string.Format("update apaction set {0}='{1}' where sn='{2}' and {3}='{4}' and upgradeTimer<NOW()",
                        str, (int)TaskStatus.SendReqst, sn, str, (int)TaskStatus.NoSendReqst);
                }
                else
                {
                    sql = string.Format("update apaction set {0}='{1}' where sn='{2}' and {3}='{4}'",
                        str, (int)TaskStatus.SendReqst, sn, str, (int)TaskStatus.NoSendReqst);
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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return null;
            }

            
            string sql = string.Format("SELECT a.sn FROM (select sn from apaction where (upgradeStatus='{0}' AND upgradeTimer<NOW()) or getLogStatus='{0}' or getParStatus='{0}' or setParStatus='{0}' or rebootStatus='{0}') AS a INNER JOIN deviceinfo As b ON a.sn=b.sn AND b.connHS='online'", (int)TaskStatus.NoSendReqst);		

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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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

            #region 获取action的status字段

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

            #endregion

            #region 更新apaction中对应的状态

            string sql = "";
            if (status == TaskStatus.NoSendReqst)
            {
                sql = string.Format("update apaction set {0}='{1}',{2} = now() where sn='{3}'", actionStatusField, (int)status, actionStartTimeField, sn);
            }
            else if ((status == TaskStatus.ReponseOk) || (status == TaskStatus.ReponseFail))
            {
                sql = string.Format("update apaction set {0}='{1}',{2} = now() where sn='{3}'", actionStatusField, (int)status, actionEndTimeField, sn);
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

            if (ret != true)
            {
                return ret;
            }

            #endregion    

            #region 获取actionIdValue

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

            #endregion

            #region 更新successCount的值

            if (status == TaskStatus.ReponseOk)
            {
                UInt32 actionCount = 0;
                UInt32 successCount = 0;
                UInt32 failCount = 0;

                sql = string.Format("select actionCount,successCount,failCount from schedulerinfo where actionId = '{0}'", actionIdValue);

                try
                {
                    using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                    {
                        using (MySqlDataReader dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                // 2019-02-20 用于特殊判断处理
                                if (dr["actionCount"] != null && !string.IsNullOrEmpty(dr["actionCount"].ToString()))
                                {
                                    actionCount = Convert.ToUInt32(dr["actionCount"]);
                                }
                                else
                                {
                                    actionCount = 0;
                                }

                                if (dr["successCount"] != null && !string.IsNullOrEmpty(dr["successCount"].ToString()))
                                {
                                    successCount = Convert.ToUInt32(dr["successCount"]);
                                }
                                else
                                {
                                    successCount = 0;
                                }

                                if (dr["failCount"] != null && !string.IsNullOrEmpty(dr["failCount"].ToString()))
                                {
                                    failCount = Convert.ToUInt32(dr["failCount"]);
                                }
                                else
                                {
                                    failCount = 0;
                                }

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

                if ((successCount + failCount) < actionCount)
                {
                    //正常的更新
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
                else
                {
                    //异常的更新
                    string info = string.Format("actionIdValue = {0},当前: actionCount = {1}, successCount= {2},failCount = {3}\r\n", actionIdValue, actionCount, successCount, failCount);
                    info += string.Format("正要更新successCount时异常，放弃!");
                    Logger.Trace(LogInfoType.WARN, info, "SetApTaskStatusBySN", LogCategory.I);
                }
            }

            #endregion

            #region 更新failCount的值

            if (status == TaskStatus.ReponseFail)
            {
                UInt32 actionCount = 0;
                UInt32 successCount = 0;
                UInt32 failCount = 0;

                sql = string.Format("select actionCount,successCount,failCount from schedulerinfo where actionId = '{0}'", actionIdValue);

                try
                {
                    using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                    {
                        using (MySqlDataReader dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                // 2019-02-20 用于特殊判断处理
                                if (dr["actionCount"] != null && !string.IsNullOrEmpty(dr["actionCount"].ToString()))
                                {
                                    actionCount = Convert.ToUInt32(dr["actionCount"]);
                                }
                                else
                                {
                                    actionCount = 0;
                                }

                                if (dr["successCount"] != null && !string.IsNullOrEmpty(dr["successCount"].ToString()))
                                {
                                    successCount = Convert.ToUInt32(dr["successCount"]);
                                }
                                else
                                {
                                    successCount = 0;
                                }

                                if (dr["failCount"] != null && !string.IsNullOrEmpty(dr["failCount"].ToString()))
                                {
                                    failCount = Convert.ToUInt32(dr["failCount"]);
                                }
                                else
                                {
                                    failCount = 0;
                                }

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

                if ((successCount+failCount) < actionCount)
                {
                    //正常的更新
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
                else
                {
                    //异常的更新
                    string info = string.Format("actionIdValue = {0},当前: actionCount = {1}, successCount = {2},failCount= {3}\r\n", actionIdValue, actionCount, successCount,failCount);
                    info += string.Format("正要更新failCount时异常，放弃!");
                    Logger.Trace(LogInfoType.WARN, info, "SetApTaskStatusBySN", LogCategory.I);
                }
            }

            #endregion

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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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
                    UInt32 actionCount = 0;
                    UInt32 successCount = 0;
                    UInt32 failCount = 0;

                    sql = string.Format("select actionCount,successCount,failCount from schedulerinfo where actionId = '{0}'", actionIdValue);                  

                    try
                    {
                        using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                        {
                            using (MySqlDataReader dr = cmd.ExecuteReader())
                            {
                                while (dr.Read())
                                {
                                    // 2019-02-20 用于特殊判断处理
                                    if (dr["actionCount"] != null && !string.IsNullOrEmpty(dr["actionCount"].ToString()))
                                    {
                                        actionCount = Convert.ToUInt32(dr["actionCount"]);
                                    }
                                    else
                                    {
                                        actionCount = 0;
                                    }

                                    if (dr["successCount"] != null && !string.IsNullOrEmpty(dr["successCount"].ToString()))
                                    {
                                        successCount = Convert.ToUInt32(dr["successCount"]);
                                    }
                                    else
                                    {
                                        successCount = 0;
                                    }

                                    if (dr["failCount"] != null && !string.IsNullOrEmpty(dr["failCount"].ToString()))
                                    {
                                        failCount = Convert.ToUInt32(dr["failCount"]);
                                    }
                                    else
                                    {
                                        failCount = 0;
                                    }

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

                    if ((successCount + failCount) < actionCount)
                    {
                        //正常的更新
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
                    else
                    {
                        //异常的更新
                        string info = string.Format("actionIdValue = {0},当前: actionCount = {1}, successCount= {2},failCount = {3}\r\n", actionIdValue, actionCount, successCount, failCount);
                        info += string.Format("正要更新successCount时异常，放弃!");
                        Logger.Trace(LogInfoType.WARN, info, "SetApTaskStatusBySN", LogCategory.I);
                    }
                }


                if (status == TaskStatus.ReponseFail)
                {
                    UInt32 actionCount = 0;
                    UInt32 successCount = 0;
                    UInt32 failCount = 0;

                    sql = string.Format("select actionCount,successCount,failCount from schedulerinfo where actionId = '{0}'", actionIdValue);

                    try
                    {
                        using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                        {
                            using (MySqlDataReader dr = cmd.ExecuteReader())
                            {
                                while (dr.Read())
                                {
                                    // 2019-02-20 用于特殊判断处理
                                    if (dr["actionCount"] != null && !string.IsNullOrEmpty(dr["actionCount"].ToString()))
                                    {
                                        actionCount = Convert.ToUInt32(dr["actionCount"]);
                                    }
                                    else
                                    {
                                        actionCount = 0;
                                    }

                                    if (dr["successCount"] != null && !string.IsNullOrEmpty(dr["successCount"].ToString()))
                                    {
                                        successCount = Convert.ToUInt32(dr["successCount"]);
                                    }
                                    else
                                    {
                                        successCount = 0;
                                    }

                                    if (dr["failCount"] != null && !string.IsNullOrEmpty(dr["failCount"].ToString()))
                                    {
                                        failCount = Convert.ToUInt32(dr["failCount"]);
                                    }
                                    else
                                    {
                                        failCount = 0;
                                    }

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

                    if ((successCount + failCount) < actionCount)
                    {
                        //正常的更新
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
                    else
                    {
                        //异常的更新
                        string info = string.Format("actionIdValue = {0},当前: actionCount = {1}, successCount = {2},failCount= {3}\r\n", actionIdValue, actionCount, successCount, failCount);
                        info += string.Format("正要更新failCount时异常，放弃!");
                        Logger.Trace(LogInfoType.WARN, info, "SetApTaskStatusBySN", LogCategory.I);
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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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
        /// <param name="upgradeTimer">upgradeTimer  -- 2019-01-17
        /// UpgradTask类型是该字段有效，其他类型的忽略
        /// </param>
        /// <param name="listSN">要执行该命令的一批AP</param>
        /// <returns>
        /// 成功 ： true
        /// 失败 ： false
        /// </returns>
        public bool AddTaskToTable(string name,string actionId, TaskType type, string xml, string upgradeTimer,string[] listSN)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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
                            try
                            {
                                // 2019-01-17
                                DateTime.Parse(upgradeTimer);
                            }
                            catch (Exception ex)
                            {
                                Logger.Trace(LogInfoType.EROR, "upgradeTimer:" + ex.Message, "DB", LogCategory.I);
                                return false;
                            }

                            //判断str是否在apaction中是否存在
                            if (apaction_record_exist(str) < 1)
                            {
                                break;
                            }

                            //更新apaction中相应的记录
                            if (apaction_record_update(str, actionId, TaskType.UpgradTask, TaskStatus.NoSendReqst, upgradeTimer) == 0)
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
                            if (apaction_record_update(str, actionId, TaskType.GetLogTask, TaskStatus.NoSendReqst, upgradeTimer) == 0)
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
                            if (apaction_record_update(str, actionId, TaskType.GetParameterValuesTask, TaskStatus.NoSendReqst, upgradeTimer) == 0)
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
                            if (apaction_record_update(str, actionId, TaskType.SetParameterValuesTask, TaskStatus.NoSendReqst, upgradeTimer) == 0)
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
                            if (apaction_record_update(str, actionId, TaskType.RebootTask, TaskStatus.NoSendReqst, upgradeTimer) == 0)
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
        
       
        /// <summary>
        /// 通过upgradeId获取upgradeTimer
        /// </summary>
        /// <param name="upgradeTimer"></param>
        /// <param name="upgradeId"></param>
        /// <returns> 
        ///   -1 ：数据库尚未打开
        ///   -2 ：参数有误
        ///   -3 ：记录不存在
        ///   -4 ：数据库操作失败 
        ///    0 : 更新成功
        /// </returns>
        public int apaction_record_get_upgradeTimer_by_upgradeId(ref string upgradeTimer, string upgradeId)
        {
            string sql = "";

            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return -1;
            }

            upgradeTimer = "";
            sql = string.Format("select upgradeTimer from apaction where upgradeId = '{0}'", upgradeId);

            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            if (!string.IsNullOrEmpty(dr[0].ToString()))
                            {
                                upgradeTimer = dr[0].ToString();
                            }
                        }
                        dr.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(LogInfoType.EROR, e.Message + e.StackTrace, "DB", LogCategory.I);
                dicRTV[(int)RC.OP_FAIL] = string.Format("数据库操作失败:{0}", e.Message + e.StackTrace);
                return (int)RC.OP_FAIL;
            }

            return 0;
        }

        #endregion
       
        #region 03 - apconninfo操作

        /// <summary>
        /// 清空所有的记录
        /// </summary>    
        /// <returns>
        ///   RC.NO_OPEN         ：数据库尚未打开
        ///   RC.OP_FAIL         ：数据库操作失败 
        ///   RC.SUCCESS         ：成功
        /// </returns>
        private int apconninfo_record_clear()
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return (int)RC.NO_OPEN;
            }

            string sql = string.Format("delete from apconninfo");

            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    if (cmd.ExecuteNonQuery() < 0)
                    {
                        Logger.Trace(LogInfoType.EROR, sql, "DB", LogCategory.I);
                        return (int)RC.OP_FAIL;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(LogInfoType.EROR, e.Message + e.StackTrace, "DB", LogCategory.I);
                dicRTV[(int)RC.OP_FAIL] = string.Format("数据库操作失败:{0}", e.Message + e.StackTrace); 
                return (int)RC.OP_FAIL;
            }

            return (int)RC.SUCCESS;
        }

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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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
        /// 清空所有的记录
        /// </summary>    
        /// <returns>
        ///   RC.NO_OPEN         ：数据库尚未打开
        ///   RC.OP_FAIL         ：数据库操作失败 
        ///   RC.SUCCESS         ：成功
        /// </returns>
        private int aploginfo_record_clear()
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return (int)RC.NO_OPEN;
            }

            string sql = string.Format("delete from aploginfo");

            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    if (cmd.ExecuteNonQuery() < 0)
                    {
                        Logger.Trace(LogInfoType.EROR, sql, "DB", LogCategory.I);              
                        return (int)RC.OP_FAIL;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(LogInfoType.EROR, e.Message + e.StackTrace, "DB", LogCategory.I);
                dicRTV[(int)RC.OP_FAIL] = string.Format("数据库操作失败:{0}", e.Message + e.StackTrace); 
                return (int)RC.OP_FAIL;
            }

            return (int)RC.SUCCESS;
        }
    
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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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
        /// 检查设备记录是否存在
        /// 用域名+设备名来区分，如：设备.深圳.福田.中心广场.西北监控.LTE-FDD
        /// </summary>
        /// <param name="roleName"></param>
        /// <returns>
        ///   RC.NO_OPEN  ：数据库尚未打开
        ///   RC.PAR_NULL ：参数为空
        ///   PAR_LEN_ERR ：参数长度有误
        ///   RC.OP_FAIL  ：数据库操作失败 
        ///   RC.NO_EXIST ：不存在
        ///   RC.EXIST    ：存在
        /// </returns>
        public int deviceinfo_record_exist(int affDomainId, string name)
        {
            //UInt32 cnt = 0;

            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return (int)RC.NO_OPEN;
            }

            if (string.IsNullOrEmpty(name))
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.PAR_NULL], "DB", LogCategory.I);
                return (int)RC.PAR_NULL;
            }

            if (name.Length > 64)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.PAR_LEN_ERR], "DB", LogCategory.I);
                return (int)RC.PAR_LEN_ERR;
            }

            //string sql = string.Format("select count(*) from device where affDomainId = {0} and name = '{1}'", affDomainId,name);
            string sql = string.Format("select 1 from deviceinfo where affDomainId = {0} and bsName = '{1}' limit 1", affDomainId, name);

            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {                       
                        if (dr.HasRows)
                        {
                            return (int)RC.EXIST;
                        }
                        else
                        {
                            return (int)RC.NO_EXIST;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(LogInfoType.EROR, e.Message + e.StackTrace, "DB", LogCategory.I);
                dicRTV[(int)RC.OP_FAIL] = string.Format("数据库操作失败:{0}", e.Message + e.StackTrace);                 
                return (int)RC.OP_FAIL;
            }        
        }

        /// <summary>
        /// 检查设备记录是否存在
        /// </summary>
        /// <param name="devId">设备ID</param>
        /// <returns>
        ///   RC.NO_OPEN  ：数据库尚未打开
        ///   RC.OP_FAIL  ：数据库操作失败 
        ///   RC.NO_EXIST ：不存在
        ///   RC.EXIST    ：存在
        /// </returns>
        public int deviceinfo_record_exist(int devId)
        {
            //UInt32 cnt = 0;

            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return (int)RC.NO_OPEN;
            }

            string sql = string.Format("select 1 from deviceinfo where id = {0} limit 1", devId);

            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {                       
                        if (dr.HasRows)
                        {
                            return (int)RC.EXIST;
                        }
                        else
                        {
                            return (int)RC.NO_EXIST;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(LogInfoType.EROR, e.Message + e.StackTrace, "DB", LogCategory.I);
                dicRTV[(int)RC.OP_FAIL] = string.Format("数据库操作失败:{0}", e.Message + e.StackTrace);  
                return (int)RC.OP_FAIL;
            }          
        }

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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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
        /// <param name="aliasName">别名</param>
        /// <param name="des">描述</param>
        /// <returns>
        ///   -1 ：数据库尚未打开
        ///   -2 ：参数有误
        ///   -3 ：记录已经存在
        ///   -4 ：数据库操作失败 
        ///    0 : 插入成功 
        /// </returns>
        public int deviceinfo_record_insert(int affDomainId, string bsName,string sn, string aliasName, string des)
        {
            string an = "";
            string de = "";

            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return -1;
            }

            if (string.IsNullOrEmpty(bsName))
            {
                Logger.Trace(Logger.__INFO__, "bsName参数为空");
                return -2;
            }

            if (bsName.Length > 64 )
            {
                Logger.Trace(Logger.__INFO__, "bsName参数长度有误");
                return -2;
            }

            if (string.IsNullOrEmpty(sn))
            {
                Logger.Trace(Logger.__INFO__, "sn参数为空");
                return -2;
            }

            if (sn.Length > 16)
            {
                Logger.Trace(Logger.__INFO__, "sn参数长度有误");
                return -2;
            }

            if (string.IsNullOrEmpty(aliasName))
            {
                an = "";
            }
            else
            {
                an = aliasName;
                if (an.Length > 64)
                {
                    Logger.Trace(Logger.__INFO__, "aliasName参数长度有误");
                    return -2;
                }
            }

            if (string.IsNullOrEmpty(des))
            {
                de = "";
            }
            else
            {
                de = des;
                if (des.Length > 1024)
                {
                    Logger.Trace(Logger.__INFO__, "aliasName参数长度有误");
                    return -2;
                }
            }


            //检查域ID是否为站点
            if ((int)RC.IS_NOT_STATION == domain_record_is_station(affDomainId))
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.IS_NOT_STATION], "DB", LogCategory.I);
                return (int)RC.IS_NOT_STATION;
            }

            //检查记录是否存在
            if ((int)RC.EXIST == deviceinfo_record_exist(affDomainId, bsName))
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.EXIST], "DB", LogCategory.I);
                return (int)RC.EXIST;
            }

          //string sql = string.Format("insert into deviceinfo(id,bsName,sn,onoffLineTime,affDomainId) values(NULL,'{0}','{1}',now(),{2})", bsName, sn, affDomainId);
            string sql = string.Format("insert into deviceinfo(id,bsName,sn,onoffLineTime,aliasName,des,affDomainId) values(NULL,'{0}','{1}',now(),'{2}','{3}',{4})", bsName, sn, an, de, affDomainId);

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
        public int deviceinfo_record_delete(int affDomainId, string bsName)
        {
            int ret = 0;
            string sn = "";

            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return -1;
            }

            if (string.IsNullOrEmpty(bsName))
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.PAR_NULL], "DB", LogCategory.I);
                return (int)RC.PAR_NULL;
            }

            if (bsName.Length > 64)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.PAR_LEN_ERR], "DB", LogCategory.I);
                return (int)RC.PAR_LEN_ERR;
            }

            //检查记录是否存在
            if ((int)RC.NO_EXIST == deviceinfo_record_exist(affDomainId, bsName))
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_EXIST], "DB", LogCategory.I);
                return (int)RC.NO_EXIST;
            }

            string sql = string.Format("delete from deviceinfo where affDomainId = {0} and bsName = '{1}'", affDomainId, bsName);
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

            //遍历字典中的值
            foreach (KeyValuePair<string, strDevice> kv in gDicDevFullName)
            {
                if ((kv.Value.bsName == bsName) && (kv.Value.affDomainId == affDomainId.ToString()))
                {
                    sn = kv.Value.sn;
                    break;
                }
            }            
            
            apconninfo_record_delete(sn);
            apaction_record_delete(sn);

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
        public int deviceinfo_record_update(int affDomainId, string bsName, strDevice di)
        {
            int ret = 0;
            string sql = "";
            string sqlSub = "";

            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return -1;
            }

            if (string.IsNullOrEmpty(bsName))
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.PAR_NULL], "DB", LogCategory.I);
                return (int)RC.PAR_NULL;
            }

            if (bsName.Length > 64)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.PAR_LEN_ERR], "DB", LogCategory.I);
                return (int)RC.PAR_LEN_ERR;
            }

            //检查记录是否存在
            if ((int)RC.NO_EXIST == deviceinfo_record_exist(affDomainId, bsName))
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_EXIST], "DB", LogCategory.I);
                return (int)RC.NO_EXIST;
            }

            if (!string.IsNullOrEmpty(di.bsName) && bsName != di.bsName)
            {
                //检查修改后的记录是否存在
                if (!string.IsNullOrEmpty(di.bsName))
                {
                    if ((int)RC.MODIFIED_EXIST == deviceinfo_record_if_rename(affDomainId, bsName, di.bsName))
                    {
                        Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.MODIFIED_EXIST], "DB", LogCategory.I);
                        return (int)RC.MODIFIED_EXIST;
                    }
                }
            }

            ///////////////////////

            //(1)
            if (!string.IsNullOrEmpty(di.bsName))
            {
                if (di.bsName.Length > 64)
                {
                    Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.PAR_LEN_ERR], "DB", LogCategory.I);
                    return (int)RC.PAR_LEN_ERR;
                }
                else
                {
                    sqlSub += string.Format("bsName = '{0}',", di.bsName);
                }
            }

            //(2)
            if (!string.IsNullOrEmpty(di.sn))
            {
                if (di.sn.Length > 32)
                {
                    Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.PAR_LEN_ERR], "DB", LogCategory.I);
                    return (int)RC.PAR_LEN_ERR;
                }
                else
                {
                    sqlSub += string.Format("sn = '{0}',", di.sn);
                }
            }


            //(3)
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

            //(4)
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


            //(5)
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

            //(6)
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


            //(7)
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

            //(8)
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

            //(9)
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

            //(10)
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

            //(11)
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


            //(12)
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

            //(13)
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

            //(14)
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


            //(15)
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


            //(16)
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

            //(17)
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

                //更新deviceinfo表中的信息               
                sql = string.Format("update deviceinfo set {0} where bsName = '{1}' and affDomainId = {2}", sqlSub, bsName, affDomainId);

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
        public int deviceinfo_record_update(string sn, strDevice di)
        {
            int ret = 0;
            string sql = "";
            string sqlSub = "";

            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return -1;
            }

            if (string.IsNullOrEmpty(sn))
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.PAR_NULL], "DB", LogCategory.I);
                return (int)RC.PAR_NULL;
            }

            if (sn.Length > 32)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.PAR_LEN_ERR], "DB", LogCategory.I);
                return (int)RC.PAR_LEN_ERR;
            }

            //检查记录是否存在
            if ((int)RC.NO_EXIST == deviceinfo_record_exist(sn))
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_EXIST], "DB", LogCategory.I);
                return (int)RC.NO_EXIST;
            }           

            ///////////////////////

            //(1)
            //if (!string.IsNullOrEmpty(di.bsName))
            //{
            //    if (di.bsName.Length > 64)
            //    {
            //        Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.PAR_LEN_ERR], "DB", LogCategory.I);
            //        return (int)RC.PAR_LEN_ERR;
            //    }
            //    else
            //    {
            //        sqlSub += string.Format("bsName = '{0}',", di.bsName);
            //    }
            //}

            //(2)
            //if (!string.IsNullOrEmpty(di.sn))
            //{
            //    if (di.sn.Length > 32)
            //    {
            //        Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.PAR_LEN_ERR], "DB", LogCategory.I);
            //        return (int)RC.PAR_LEN_ERR;
            //    }
            //    else
            //    {
            //        sqlSub += string.Format("sn = '{0}',", di.sn);
            //    }
            //}


            //(3)
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

            //(4)
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


            //(5)
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

            //(6)
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


            //(7)
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

            //(8)
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

            //(9)
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

            //(10)
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

            //(11)
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


            //(12)
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

            //(13)
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

            //(14)
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


            //(15)
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


            //(16)
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

            //(17)
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

                //更新deviceinfo表中的信息               
                sql = string.Format("update deviceinfo set {0} where sn = '{1}' ", sqlSub, sn);

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

            return ret;
        }

        /// <summary>
        /// 获取deviceinfo表中的各条记录
        /// </summary>
        /// <param name="dt">
        /// 返回的DataTable，包含的列为：id,bsName,sn,ipAddr,type,s1Status,connHS
        /// tac,enbId,cellId,earfcn,pci,updateMode,curVersion,curWarnCnt,onoffLineTime
        /// aliasName,des
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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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
            column18.DataType = System.Type.GetType("System.Int32");
            column18.ColumnName = "affDomainId";   

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

            //string sql = string.Format("SELECT a.*,b.province,b.city,b.district,b.street FROM (select * from deviceinfo ) AS a INNER JOIN addressinfo As b ON a.sn=b.sn");		

            string sql = string.Format("select * from deviceinfo");		

            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            DataRow row = dt.NewRow();

                            row["id"] = Convert.ToInt32(dr["id"]);
                            row["bsName"] = dr["bsName"].ToString();
                            row["sn"]  = dr["sn"].ToString();
                            row["ipAddr"] = dr["ipAddr"].ToString();
                            row["type"] = dr["type"].ToString();
                            row["s1Status"] = dr["s1Status"].ToString();
                            row["connHS"] = dr["connHS"].ToString();
                            row["tac"]  = dr["tac"].ToString();
                            row["enbId"] = dr["enbId"].ToString();
                            row["cellId"] = dr["cellId"].ToString();
                            row["earfcn"] = dr["earfcn"].ToString();
                            row["pci"] = dr["pci"].ToString();
                            row["updateMode"] = dr["updateMode"].ToString();
                            row["curVersion"] = dr["curVersion"].ToString();

                            //cnt = alarminfo_record_count_get(dr["sn"].ToString());
                            //row["curWarnCnt"] = cnt.ToString();

                            row["curWarnCnt"] = dr["curWarnCnt"].ToString();

                            row["onoffLineTime"] = dr["onoffLineTime"].ToString();
                            row["aliasName"] = dr["aliasName"].ToString();
                            row["des"] = dr["des"].ToString();
                            row["affDomainId"] = Convert.ToInt32(dr["affDomainId"]);

                            dt.Rows.Add(row);
                        }
                        dr.Close();
                    }
                }


                string sn = "";
                int curWarnCnt = -1;
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    sn = dt.Rows[i]["sn"].ToString();

                    curWarnCnt = alarminfo_record_count_get(sn);
                    if (curWarnCnt >= 0)
                    {
                        dt.Rows[i]["curWarnCnt"] = curWarnCnt.ToString();
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
        /// aliasName,des
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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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

            if (!string.IsNullOrEmpty(diq.bsName))
            {
                if (diq.bsName.Length > 64)
                {
                    Logger.Trace(Logger.__INFO__, "diq.bsName参数长度有误");
                    return -2;
                }
                else
                {
                    sqlSub += string.Format(" bsName like '%%{0}%%' and", diq.bsName);
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
                    sqlSub += string.Format(" sn like '%%{0}%%' and", diq.sn);
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
                    sqlSub += string.Format(" ipAddr like '%%{0}%%' and", diq.ipAddr);
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
                    sqlSub += string.Format(" type like '%%{0}%%' and", diq.type);
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
                    sqlSub += string.Format(" s1Status like '%%{0}%%' and", diq.s1Status);
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
                    sqlSub += string.Format(" connHS like '%%{0}%%' and", diq.connHS);
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
                    sqlSub += string.Format(" tac like '%%{0}%%' and", diq.type);
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
                    sqlSub += string.Format(" enbId like '%%{0}%%' and", diq.type);
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
                    sqlSub += string.Format(" cellId like '%%{0}%%' and", diq.cellId);
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
                    sqlSub += string.Format(" earfcn like '%%{0}%%' and", diq.earfcn);
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
                    sqlSub += string.Format(" pci like '%%{0}%%' and", diq.pci);
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
                    sqlSub += string.Format(" updateMode like '%%{0}%%' and", diq.updateMode);
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
                    sqlSub += string.Format(" curVersion like '%%{0}%%' and", diq.curVersion);
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
                    sqlSub += string.Format(" curWarnCnt like '%%{0}%%' and", diq.curWarnCnt);
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

            sqlSub += string.Format(" onoffLineTime >= '{0}' and onoffLineTime <= '{1}' and", startTime, endTime);


            if (!string.IsNullOrEmpty(diq.aliasName))
            {
                if (diq.aliasName.Length > 64)
                {
                    Logger.Trace(Logger.__INFO__, "diq.aliasName参数长度有误");
                    return -2;
                }
                else
                {
                    sqlSub += string.Format(" aliasName like '%%{0}%%' and", diq.aliasName);
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
                    sqlSub += string.Format(" des like '%%{0}%%' and", diq.des);
                }
            }



            //////////////////////////////////////////////////////////////////
            //////////////////////////////////////////////////////////////////


            //if (!string.IsNullOrEmpty(diq.province))
            //{
            //    if (diq.province.Length > 64)
            //    {
            //        Logger.Trace(Logger.__INFO__, "diq.province参数长度有误");
            //        return -2;
            //    }
            //    else
            //    {              
            //        sqlSub += string.Format(" b.province like '%%{0}%%' and", diq.province);
            //    }
            //}

            //if (!string.IsNullOrEmpty(diq.city))
            //{
            //    if (diq.city.Length > 64)
            //    {
            //        Logger.Trace(Logger.__INFO__, "diq.city参数长度有误");
            //        return -2;
            //    }
            //    else
            //    {
            //        sqlSub += string.Format(" b.city like '%%{0}%%' and", diq.city);
            //    }
            //}

            //if (!string.IsNullOrEmpty(diq.district))
            //{
            //    if (diq.district.Length > 64)
            //    {
            //        Logger.Trace(Logger.__INFO__, "diq.district参数长度有误");
            //        return -2;
            //    }
            //    else
            //    {
            //        sqlSub += string.Format(" b.district like '%%{0}%%' and", diq.district);
            //    }
            //}

            //if (!string.IsNullOrEmpty(diq.street))
            //{
            //    if (diq.street.Length > 128)
            //    {
            //        Logger.Trace(Logger.__INFO__, "diq.street参数长度有误");
            //        return -2;
            //    }
            //    else
            //    {
            //        sqlSub += string.Format(" b.street like '%%{0}%%' and", diq.street);
            //    }
            //}


            if (sqlSub != "")
            {
                //去掉最后三个字符"and"
                sqlSub = sqlSub.Remove(sqlSub.Length - 3, 3);
                //sql = string.Format("SELECT a.*,b.province,b.city,b.district,b.street FROM (select * from deviceinfo ) AS a INNER JOIN addressinfo As b ON a.sn=b.sn and {0}", sqlSub);
                sql = string.Format("select * from deviceinfo where {0}", sqlSub);
            }
            else
            {
                //无任何过滤的字段
                Logger.Trace(Logger.__INFO__, "deviceinfo_record_entity_get_by_query,无任何过滤的字段");
                //sql = string.Format("SELECT a.*,b.province,b.city,b.district,b.street FROM (select * from deviceinfo ) AS a INNER JOIN addressinfo As b ON a.sn=b.sn");
                sql = string.Format("select * from deviceinfo");
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

                            // 2019-01-14
                            //cnt = alarminfo_record_count_get(dr[2].ToString());
                            //row[14] = cnt.ToString();

                            row[14] = dr[14].ToString();

                            row[15] = dr[15].ToString();
                            row[16] = dr[16].ToString();
                            row[17] = dr[17].ToString();  

                            dt.Rows.Add(row);
                        }
                        dr.Close();
                    }
                }

                string sn = "";
                int curWarnCnt = -1;
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    sn = dt.Rows[i]["sn"].ToString();

                    curWarnCnt = alarminfo_record_count_get(sn);
                    if (curWarnCnt >= 0)
                    {
                        dt.Rows[i]["curWarnCnt"] = curWarnCnt.ToString();
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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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

        /// <summary>
        /// 检查一下是否能插入记录到设备表中
        /// </summary>
        /// <param name="affDomainId">所属域ID</param>
        /// <param name="name"></param>
        /// <param name="mode">
        /// 制式：GSM,TD-SCDMA,WCDMA,LTE-TDD,LTE-FDD 
        /// </param>
        /// <returns>
        /// true  : 可以插入
        /// false : 不能插入
        /// </returns>
        public bool deviceinfo_record_checkif_can_insert(int affDomainId, string name, ref string errInfo)
        {
            if (false == myDbConnFlag)
            {
                errInfo = string.Format("{0}.", dicRTV[(int)RC.NO_OPEN]);
                Logger.Trace(LogInfoType.EROR, errInfo, "DB", LogCategory.I);
                return false;
            }

            if (string.IsNullOrEmpty(name))
            {
                errInfo = string.Format("name:{0}.", dicRTV[(int)RC.PAR_NULL]);
                Logger.Trace(LogInfoType.EROR, errInfo, "DB", LogCategory.I);
                return false;
            }

            if (name.Length > 64)
            {
                errInfo = string.Format("name:{0}.", dicRTV[(int)RC.PAR_LEN_ERR]);
                Logger.Trace(LogInfoType.EROR, errInfo, "DB", LogCategory.I);
                return false;
            }

            //检查域ID是否为站点
            if ((int)RC.IS_NOT_STATION == domain_record_is_station(affDomainId))
            {
                errInfo = string.Format("affDomainId = {0} : {1}.", affDomainId, dicRTV[(int)RC.IS_NOT_STATION]);
                Logger.Trace(LogInfoType.EROR, errInfo, "DB", LogCategory.I);
                return false;
            }

            //检查记录是否存在
            if ((int)RC.EXIST == deviceinfo_record_exist(affDomainId, name))
            {
                errInfo = string.Format("affDomainId = {0},name = {1} : {2}.", affDomainId, name, dicRTV[(int)RC.EXIST]);
                Logger.Trace(LogInfoType.EROR, errInfo, "DB", LogCategory.I);
                return false;
            }

            /*           
             * 检查记录是否存在，2018-10-31
             * 站点下存在同名设备下也返回true，即覆盖同名的设备，新需求。
             */
            //if ((int)RC.EXIST == device_record_exist(affDomainId, name))
            //{
            //    errInfo = string.Format("覆盖同名设备:affDomainId = {0},name = {1} : 可以插入到设备表中.", affDomainId, name);
            //    Logger.Trace(LogInfoType.EROR, errInfo, "DB", LogCategory.I);
            //    return true;
            //}

            errInfo = string.Format("affDomainId = {0},name = {1} : 可以插入到设备表中.", affDomainId, name);
            return true;
        }

        /// <summary>
        /// 是否可以进行重命名设备的名称
        /// </summary>
        /// <param name="affDomainId">所属域ID</param>
        /// <param name="oldName">旧名称</param>
        /// <param name="newName">新名称</param>
        /// <returns>
        ///   RC.NO_OPEN        ：数据库尚未打开
        ///   RC.PAR_NULL       ：参数为空
        ///   PAR_LEN_ERR       ：参数长度有误
        ///   RC.OP_FAIL        ：数据库操作失败
        ///   RC.IS_NOT_STATION ：域ID不是站点
        ///   RC.NO_EXIST       ：记录不存在
        ///   RC.MODIFIED_EXIST ：修改后的记录已经存在
        ///   RC.SUCCESS        ：成功(可以重命名) 
        /// </returns>
        public int deviceinfo_record_if_rename(int affDomainId, string oldName, string newName)
        {
            UInt32 cnt = 0;

            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return (int)RC.NO_OPEN;
            }

            if (string.IsNullOrEmpty(oldName) || string.IsNullOrEmpty(newName))
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.PAR_NULL], "DB", LogCategory.I);
                return (int)RC.PAR_NULL;
            }

            if (oldName.Length > 64 || newName.Length > 64)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.PAR_LEN_ERR], "DB", LogCategory.I);
                return (int)RC.PAR_LEN_ERR;
            }

            //检查域ID是否为站点
            if ((int)RC.IS_NOT_STATION == domain_record_is_station(affDomainId))
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.IS_NOT_STATION], "DB", LogCategory.I);
                return (int)RC.IS_NOT_STATION;
            }

            //检查记录是否存在
            if ((int)RC.NO_EXIST == deviceinfo_record_exist(affDomainId, oldName))
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.EXIST], "DB", LogCategory.I);
                return (int)RC.EXIST;
            }

            string sql = string.Format("select count(*) from deviceinfo where affDomainId = {0} and bsName != '{1}' and bsName = '{2}'", affDomainId, oldName, newName);
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
                Logger.Trace(LogInfoType.EROR, e.Message + e.StackTrace, "DB", LogCategory.I);
                dicRTV[(int)RC.OP_FAIL] = string.Format("数据库操作失败:{0}", e.Message + e.StackTrace); 
                return (int)RC.OP_FAIL;
            }

            if (cnt > 0)
            {
                return (int)RC.MODIFIED_EXIST;
            }
            else
            {
                return (int)RC.SUCCESS;
            }
        }

        /// <summary>
        /// 清空设备表中的所有记录 
        /// </summary>  
        /// <returns>
        ///   RC.NO_OPEN      ：数据库尚未打开
        ///   RC.OP_FAIL      ：数据库操作失败 
        ///   RC.SUCCESS      ：成功
        /// </returns>
        public int deviceinfo_record_clear()
        {         
            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return (int)RC.NO_OPEN;
            }

            string sql = string.Format("delete from deviceinfo");
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    if (cmd.ExecuteNonQuery() < 0)
                    {
                        Logger.Trace(LogInfoType.EROR, sql, "DB", LogCategory.I);
                        return (int)RC.OP_FAIL;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(LogInfoType.EROR, e.Message + e.StackTrace, "DB", LogCategory.I);
                dicRTV[(int)RC.OP_FAIL] = string.Format("数据库操作失败:{0}", e.Message + e.StackTrace);
                return (int)RC.OP_FAIL;
            }

            return (int)RC.SUCCESS;
        }


        /// <summary>
        /// 获取一个站点下所有的设备列表
        /// </summary>
        /// <param name="parentFullPathName">
        /// (1) 域的全路径(站点或非站点)
        /// (2) 设备的全路径
        /// </param>
        /// <param name="lst">返回的设备列表</param>
        /// <param name="errInfo">返回的出错信息</param>
        /// <returns>
        /// 0   ： 成功
        /// 非0 ： 失败
        /// </returns>
        public int app_all_device_request(string parentFullPathName, ref List<strDevice> lst, ref string errInfo)
        {
            int rtv = -1;

            errInfo = "";
            lst = new List<strDevice>();

            //if (string.IsNullOrEmpty(parentFullPathName))
            //{
            //    errInfo = string.Format("parentFullPathName 字段为空.");
            //    return rtv;
            //}

            ////检查域ID是否为站点
            //rtv = domain_record_is_station(parentFullPathName);
            //if (rtv == (int)RC.IS_NOT_STATION)
            //{
            //    errInfo = get_rtv_str(rtv);
            //    return rtv;
            //}

            //int i;
            //string tmp = "";
            //lst = new List<strDevice>();
            //errInfo = "";

            //foreach (KeyValuePair<string, strDevice> kv in gDicDevFullName)
            //{
            //    i = kv.Key.LastIndexOf(".");
            //    if (i > 0)
            //    {
            //        tmp = kv.Key.Substring(0, i);
            //        if (tmp == parentFullPathName)
            //        {
            //            lst.Add(kv.Value);
            //        }
            //    }                
            //}

            //return 0;

            if (string.IsNullOrEmpty(parentFullPathName))
            {
                errInfo = string.Format("parentFullPathName 字段为空.");
                return rtv;
            }

            #region 重新获取gDicDevFullName

            if (0 == domain_dictionary_info_join_get(ref gDicDevFullName, ref gDicDevId_Station_DevName))
            {
                Logger.Trace(LogInfoType.INFO, "gDicDevFullName -> 获取OK！", "DB", LogCategory.I);
            }
            else
            {
                Logger.Trace(LogInfoType.INFO, "gDicDevFullName -> 获取FAILED！", "DB", LogCategory.I);
            }

            #endregion

            // 2019-02-14
            if (gDicDevFullName.ContainsKey(parentFullPathName))
            {
                lst.Add(gDicDevFullName[parentFullPathName]);
                return 0;
            }

            List<int> lstDevId = new List<int>();
            List<string> lstDevSn = new List<string>();

            rtv = domain_record_device_id_list_get(parentFullPathName, ref lstDevId, ref lstDevSn);
            if ((int)RC.SUCCESS != rtv)
            {
                errInfo = get_rtv_str(rtv);
                return rtv;
            }            
          
            foreach (strDevice str in gDicDevFullName.Values)
            {
                if (lstDevId.Contains(str.id))
                {
                    lst.Add(str);
                }
            }

            return 0;
        }

        /// <summary>
        /// 添加一个设备
        /// </summary>
        /// <param name="parentFullPathName">设备挂载的站点</param>
        /// <param name="name">设备名称</param>
        /// <param name="sn">设备SN</param>
        /// <param name="aliasName">别名</param>
        /// <param name="des">描述</param>
        /// <param name="errInfo">返回的出错信息</param>
        /// <returns>
        /// 0   ： 成功
        /// 非0 ： 失败
        /// </returns>
        public int app_add_device_request(string parentFullPathName, string name, string sn, string aliasName, string des, ref string errInfo)
        {
            int rtv = -1;
            int affDomainId = -1;

            if (string.IsNullOrEmpty(parentFullPathName))
            {
                errInfo = string.Format("parentFullPathName 字段为空.");
                return rtv;
            }

            if (string.IsNullOrEmpty(name))
            {
                errInfo = string.Format("name 字段为空.");
                return rtv;
            }

            if (name.Length > 64)            
            {
                errInfo = string.Format("name 的长度={0},非法.", name.Length);
                return rtv;
            }

            if (string.IsNullOrEmpty(sn))
            {
                errInfo = string.Format("sn 字段为空.");
                return rtv;
            }

            if (sn.Length > 32)
            {
                errInfo = string.Format("sn 的长度={0},非法.", sn.Length);
                return rtv;
            }

            // 2019-01-11，新增判断条件
            if (1 == deviceinfo_record_exist(sn))
            {
                errInfo = string.Format("sn={0},已经存在系统中.", sn);
                return rtv;
            }

            //检查域ID是否为站点
            rtv = domain_record_is_station(parentFullPathName);
            if (rtv == (int)RC.IS_NOT_STATION)
            {
                errInfo = get_rtv_str(rtv);
                return rtv;
            }

            rtv = domain_get_id_by_nameFullPath(parentFullPathName, ref affDomainId);
            if (rtv != (int)RC.SUCCESS)
            {
                errInfo = get_rtv_str(rtv);
                return rtv;
            }

            rtv = deviceinfo_record_insert(affDomainId,name,sn,aliasName,des);
            if (rtv != (int)RC.SUCCESS)
            {
                errInfo = get_rtv_str(rtv);
                return rtv;
            }

            #region 重新获取gDicDevFullName

            if (rtv == 0)
            {
                if (0 == domain_dictionary_info_join_get(ref gDicDevFullName, ref gDicDevId_Station_DevName))
                {
                    Logger.Trace(LogInfoType.INFO, "gDicDevFullName -> 获取OK！", "DB", LogCategory.I);
                    print_dic_dev_fullname_info("app_del_domain_request", gDicDevFullName);
                }
                else
                {
                    Logger.Trace(LogInfoType.INFO, "gDicDevFullName -> 获取FAILED！", "DB", LogCategory.I);
                }
            }

            #endregion

            return rtv;
        }

        /// <summary>
        /// 删除一个设备
        /// </summary>
        /// <param name="parentFullPathName">设备挂载的站点</param>
        /// <param name="name">设备名称</param>
        /// <param name="errInfo">返回的出错信息</param>
        /// <returns>
        /// 0   ： 成功
        /// 非0 ： 失败
        /// </returns>
        public int app_del_device_request(string parentFullPathName, string name, ref string errInfo)
        {
            int rtv = -1;
            int affDomainId = -1;

            if (string.IsNullOrEmpty(parentFullPathName))
            {
                errInfo = string.Format("parentFullPathName 字段为空.");
                return rtv;
            }

            if (string.IsNullOrEmpty(name))
            {
                errInfo = string.Format("name 字段为空.");
                return rtv;
            }

            //检查域ID是否为站点
            rtv = domain_record_is_station(parentFullPathName);
            if (rtv == (int)RC.IS_NOT_STATION)
            {
                errInfo = get_rtv_str(rtv);
                return rtv;
            }

            rtv = domain_get_id_by_nameFullPath(parentFullPathName, ref affDomainId);
            if (rtv != (int)RC.SUCCESS)
            {
                errInfo = get_rtv_str(rtv);
                return rtv;
            }


            rtv = deviceinfo_record_delete(affDomainId, name);
            if (rtv != (int)RC.SUCCESS)
            {
                errInfo = get_rtv_str(rtv);
                return rtv;
            }

            #region 重新获取gDicDevFullName

            if (rtv == 0)
            {
                if (0 == domain_dictionary_info_join_get(ref gDicDevFullName, ref gDicDevId_Station_DevName))
                {
                    Logger.Trace(LogInfoType.INFO, "gDicDevFullName -> 获取OK！", "DB", LogCategory.I);
                    print_dic_dev_fullname_info("app_del_domain_request", gDicDevFullName);
                }
                else
                {
                    Logger.Trace(LogInfoType.INFO, "gDicDevFullName -> 获取FAILED！", "DB", LogCategory.I);
                }
            }

            #endregion

            return rtv;
        }

        /// <summary>
        /// 更新一个设备的信息
        /// </summary>
        /// <param name="parentFullPathName">设备挂载的站点</param>
        /// <param name="name">设备名称</param>
        /// <param name="di">要更新的信息</param>
        /// <param name="errInfo">返回的出错信息</param>
        /// <returns>
        /// 0   ： 成功
        /// 非0 ： 失败
        /// </returns>
        public int app_update_device_request(string parentFullPathName, string name, strDevice di,ref string errInfo)
        {
            int rtv = -1;
            int affDomainId = -1;

            if (string.IsNullOrEmpty(parentFullPathName))
            {
                errInfo = string.Format("parentFullPathName 字段为空.");
                return rtv;
            }

            if (string.IsNullOrEmpty(name))
            {
                errInfo = string.Format("name 字段为空.");
                return rtv;
            }

            //检查域ID是否为站点
            rtv = domain_record_is_station(parentFullPathName);
            if (rtv == (int)RC.IS_NOT_STATION)
            {
                errInfo = get_rtv_str(rtv);
                return rtv;
            }

            rtv = domain_get_id_by_nameFullPath(parentFullPathName, ref affDomainId);
            if (rtv != (int)RC.SUCCESS)
            {
                errInfo = get_rtv_str(rtv);
                return rtv;
            }

            rtv = deviceinfo_record_update(affDomainId, name,di);
            if (rtv != (int)RC.SUCCESS)
            {
                errInfo = get_rtv_str(rtv);
                return rtv;
            }

            #region 重新获取gDicDevFullName

            if (rtv == 0)
            {
                if (0 == domain_dictionary_info_join_get(ref gDicDevFullName, ref gDicDevId_Station_DevName))
                {
                    Logger.Trace(LogInfoType.INFO, "gDicDevFullName -> 获取OK！", "DB", LogCategory.I);
                    print_dic_dev_fullname_info("app_del_domain_request", gDicDevFullName);
                }
                else
                {
                    Logger.Trace(LogInfoType.INFO, "gDicDevFullName -> 获取FAILED！", "DB", LogCategory.I);
                }
            }

            #endregion

            return rtv;
        }

        /// <summary>
        /// 注册一个设备(包含一系列动作)
        /// </summary>
        /// <param name="parentFullPathName">设备挂载的站点</param>
        /// <param name="name">设备名称</param>
        /// <param name="sn">设备SN</param>
        /// <param name="aliasName">别名</param>
        /// <param name="des">描述</param>
        /// <param name="errInfo">返回的出错信息</param>
        /// <returns>
        /// 0   ： 成功
        /// 非0 ： 失败
        /// </returns>
        public int app_register_device_request(string parentFullPathName, string name, string sn, string aliasName, string des, ref string errInfo)
        {
            int rtv = -1;
            int affDomainId = -1;

            if (string.IsNullOrEmpty(parentFullPathName))
            {
                errInfo = string.Format("parentFullPathName 字段为空.");
                return rtv;
            }

            if (string.IsNullOrEmpty(name))
            {
                errInfo = string.Format("name 字段为空.");
                return rtv;
            }

            if (name.Length > 64)
            {
                errInfo = string.Format("name 的长度={0},非法.", name.Length);
                return rtv;
            }

            if (string.IsNullOrEmpty(sn))
            {
                errInfo = string.Format("sn 字段为空.");
                return rtv;
            }

            if (sn.Length > 32)
            {
                errInfo = string.Format("sn 的长度={0},非法.", sn.Length);
                return rtv;
            }

            // 2019-01-11，新增判断条件
            if (1 == deviceinfo_record_exist(sn))
            {
                errInfo = string.Format("sn={0},已经存在系统中.", sn);
                return rtv;
            }

            //检查域ID是否为站点
            rtv = domain_record_is_station(parentFullPathName);
            if (rtv == (int)RC.IS_NOT_STATION)
            {
                errInfo = get_rtv_str(rtv);
                return rtv;
            }

            rtv = domain_get_id_by_nameFullPath(parentFullPathName, ref affDomainId);
            if (rtv != (int)RC.SUCCESS)
            {
                errInfo = get_rtv_str(rtv);
                return rtv;
            }

            rtv = deviceinfo_record_insert(affDomainId, name, sn,aliasName,des);
            if (rtv != (int)RC.SUCCESS)
            {
                errInfo = get_rtv_str(rtv);
                return rtv;
            }

            #region 重新获取gDicDevFullName

            if (rtv == 0)
            {
                if (0 == domain_dictionary_info_join_get(ref gDicDevFullName, ref gDicDevId_Station_DevName))
                {
                    Logger.Trace(LogInfoType.INFO, "gDicDevFullName -> 获取OK！", "DB", LogCategory.I);
                    print_dic_dev_fullname_info("app_del_domain_request", gDicDevFullName);
                }
                else
                {
                    Logger.Trace(LogInfoType.INFO, "gDicDevFullName -> 获取FAILED！", "DB", LogCategory.I);
                }
            }

            #endregion

            rtv = apaction_record_insert(sn);
            if (rtv != (int)RC.SUCCESS)
            {
                errInfo = get_rtv_str(rtv);
                return rtv;
            }

            rtv = apconninfo_record_insert(sn, "null", "null", "null");
            if (rtv != (int)RC.SUCCESS)
            {
                errInfo = get_rtv_str(rtv);
                return rtv;
            }

            return rtv;
        }

        /// <summary>
        /// 将所有设备设置为下线
        /// </summary>
        /// <returns>
        ///   RC.NO_OPEN        ：数据库尚未打开
        ///   RC.OP_FAIL        ：数据库操作失败 
        ///   RC.SUCCESS        ：成功 
        /// </returns>
        public int deviceinfo_record_clear_online()
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return (int)RC.NO_OPEN;
            }


            string sql = string.Format("update deviceinfo set s1Status = 'offline',connHS = 'offline'");

            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    if (cmd.ExecuteNonQuery() < 0)
                    {
                        Logger.Trace(LogInfoType.WARN, sql, "DB", LogCategory.I);                    
                        return (int)RC.OP_FAIL;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(LogInfoType.EROR, e.Message + e.StackTrace, "DB", LogCategory.I);
                return (int)RC.OP_FAIL;
            }

            return (int)RC.SUCCESS;
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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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

        ///// <summary>
        ///// 插入记录到userloginfo表中
        ///// 后续上层使用update接口进行更新
        ///// </summary>
        ///// <param name="userName"></param>
        ///// <param name="sn"></param>
        ///// <param name="bsName"></param>
        ///// <param name="executeStatus"></param>
        ///// <param name="executeResult"></param>
        ///// <param name="cmdType"></param>
        ///// <param name="cmdStartTime"></param>
        ///// <param name="cmdEndTime"></param>
        ///// <param name="ipAddr"></param>
        ///// <param name="cmdContent"></param>
        ///// <returns>
        /////   -1 ：数据库尚未打开
        /////   -2 ：参数有误
        /////   -3 ：记录已经存在
        /////   -4 ：数据库操作失败 
        /////   -5 ：用户名不存在
        /////    0 : 插入成功
        ///// </returns>
        //public int userloginfo_record_insert(string userName, string sn, string bsName, string executeStatus, string executeResult, string cmdType, string cmdStartTime, string cmdEndTime, string ipAddr, string cmdContent)
        //{
        //    if (false == myDbConnFlag)
        //    {
        //        Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
        //        return -1;
        //    }

        //    string sql = string.Format("insert into loginfo values(NULL,'{0}','{1}','{2}','{3}','{4}','{5}','{6}','{7}','{8}','{9}')", userName, sn, bsName, executeStatus, executeResult, cmdType, cmdStartTime, cmdEndTime, ipAddr, cmdContent);
        //    try
        //    {
        //        using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
        //        {
        //            if (cmd.ExecuteNonQuery() < 0)
        //            {
        //                Logger.Trace(Logger.__INFO__, sql);
        //                return -4;
        //            }
        //        }
        //    }
        //    catch (Exception e)
        //    {
        //        Logger.Trace(e);
        //        return -4;
        //    }

        //    return 0;
        //}

        ///// <summary>
        ///// 
        ///// </summary>
        ///// <param name="sn"></param>
        ///// <param name="bsName"></param>
        ///// <param name="cmdStartTime"></param>
        ///// <param name="cmdEndTime"></param>
        ///// <param name="ipAddr"></param>
        ///// <returns>
        ///// -1 ：数据库尚未打开
        ///// -2 ：参数有误
        ///// -3 ：用户不存在
        ///// -4 ：数据库操作失败 
        /////  0 : 删除成功 
        ///// </returns>
        //public int userloginfo_record_delete(string sn, string bsName, string cmdStartTime, string cmdEndTime, string ipAddr)
        //{
        //    if (false == myDbConnFlag)
        //    {
        //        Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
        //        return -1;
        //    }

        //    string sql = string.Format("delete from loginfo where sn = '{0}' and  bsName= '{1}' and cmdStartTime>='{2}' and  cmdEndTime<= '{3}' and  ipAddr= '{4}'", sn, bsName, cmdStartTime, cmdEndTime, ipAddr);
        //    try
        //    {
        //        using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
        //        {
        //            if (cmd.ExecuteNonQuery() < 0)
        //            {
        //                Logger.Trace(Logger.__INFO__, sql);
        //                return -4;
        //            }
        //        }
        //    }
        //    catch (Exception e)
        //    {
        //        Logger.Trace(e);
        //        return -4;
        //    }

        //    return 0;
        //}

        ///// <summary>
        ///// 返回aploginfo表中的所有available字段为1（可用）的记录
        ///// </summary>
        ///// <param name="dt">返回的DataTable</param>
        ///// <returns>
        /////   -1 ：数据库尚未打开
        /////   -4 ：数据库操作失败 
        /////    0 : 获取成功          
        ///// </returns>
        //public int userloginfo_record_entity_get(ref DataTable dt)
        //{
        //    if (false == myDbConnFlag)
        //    {
        //        Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
        //        return -1;
        //    }

        //    dt = new DataTable("loginfo");

        //    DataColumn column0 = new DataColumn();
        //    column0.DataType = System.Type.GetType("System.Int32");
        //    column0.ColumnName = "id";

        //    DataColumn column1 = new DataColumn();
        //    column1.DataType = System.Type.GetType("System.String");
        //    column1.ColumnName = "userName";

        //    DataColumn column2 = new DataColumn();
        //    column2.DataType = System.Type.GetType("System.String");
        //    column2.ColumnName = "sn";

        //    DataColumn column3 = new DataColumn();
        //    column3.DataType = System.Type.GetType("System.String");
        //    column3.ColumnName = "bsName";

        //    DataColumn column4 = new DataColumn();
        //    column4.DataType = System.Type.GetType("System.String");
        //    column4.ColumnName = "executeStatus";

        //    DataColumn column5 = new DataColumn();
        //    column5.DataType = System.Type.GetType("System.String");
        //    column5.ColumnName = "executeResult";

        //    DataColumn column6 = new DataColumn();
        //    column6.DataType = System.Type.GetType("System.String");
        //    column6.ColumnName = "cmdType";

        //    DataColumn column7 = new DataColumn();
        //    column7.DataType = System.Type.GetType("System.String");
        //    column7.ColumnName = "cmdStartTime";

        //    DataColumn column8 = new DataColumn();
        //    column8.DataType = System.Type.GetType("System.String");
        //    column8.ColumnName = "cmdEndTime";

        //    DataColumn column9 = new DataColumn();
        //    column9.DataType = System.Type.GetType("System.String");
        //    column9.ColumnName = "ipAddr";

        //    DataColumn column10 = new DataColumn();
        //    column10.DataType = System.Type.GetType("System.String");
        //    column10.ColumnName = "cmdContent";

        //    dt.Columns.Add(column0);
        //    dt.Columns.Add(column1);
        //    dt.Columns.Add(column2);
        //    dt.Columns.Add(column3);
        //    dt.Columns.Add(column4);
        //    dt.Columns.Add(column5);
        //    dt.Columns.Add(column6);
        //    dt.Columns.Add(column7);
        //    dt.Columns.Add(column8);
        //    dt.Columns.Add(column9);
        //    dt.Columns.Add(column10);

        //    string sql = "select * from loginfo";
        //    try
        //    {
        //        using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
        //        {
        //            using (MySqlDataReader dr = cmd.ExecuteReader())
        //            {
        //                while (dr.Read())
        //                {
        //                    DataRow row = dt.NewRow();
        //                    row[0] = Int32.Parse(dr[0].ToString());
        //                    row[1] = dr[1].ToString();
        //                    row[2] = dr[2].ToString();
        //                    row[3] = dr[3].ToString();
        //                    row[4] = dr[4].ToString();
        //                    row[5] = dr[5].ToString();
        //                    row[6] = dr[6].ToString();
        //                    row[7] = dr[7].ToString();
        //                    row[8] = dr[8].ToString();
        //                    row[9] = dr[9].ToString();
        //                    row[10] = dr[10].ToString();
        //                    dt.Rows.Add(row);
        //                }
        //                dr.Close();
        //            }
        //        }
        //    }
        //    catch (Exception e)
        //    {
        //        Logger.Trace(e);
        //        return -4;
        //    }
        //    return 0;
        //}

        /// <summary>
        /// 清空所有的记录
        /// </summary>    
        /// <returns>
        ///   RC.NO_OPEN         ：数据库尚未打开
        ///   RC.OP_FAIL         ：数据库操作失败 
        ///   RC.SUCCESS         ：成功
        /// </returns>
        private int loginfo_record_clear()
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return (int)RC.NO_OPEN;
            }

            string sql = string.Format("delete from loginfo");

            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    if (cmd.ExecuteNonQuery() < 0)
                    {
                        Logger.Trace(LogInfoType.EROR, sql, "DB", LogCategory.I);
                        return (int)RC.OP_FAIL;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(LogInfoType.EROR, e.Message + e.StackTrace, "DB", LogCategory.I);
                dicRTV[(int)RC.OP_FAIL] = string.Format("数据库操作失败:{0}", e.Message + e.StackTrace);
                return (int)RC.OP_FAIL;
            }

            return (int)RC.SUCCESS;
        }

        /// <summary>
        /// 插入记录到Log信息表中
        /// </summary>
        /// <param name="level"></param>
        /// <param name="username"></param>
        /// <param name="optype"></param>
        /// <param name="sn"></param>
        /// <param name="message"></param>
        /// <returns
        /// 成功 ： 0
        /// 失败 ： -1
        /// ></returns>
        public int loginfo_record_insert(string level, string username, string optype, string sn, string message,ref string errInfo)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                errInfo = dicRTV[(int)RC.NO_OPEN];
                return -1;
            }

            if (level != "Warning" && level != "Info" && level != "Debug")
            {
                errInfo = string.Format("level = {0},非法.", level);
                return -1;
            }

            if (1 != userinfo_record_exist(username))
            {
                errInfo = string.Format("username = {0},非法.", username);
                return -1;
            }

            if (optype != "sysinfo" && optype != "devinfo" && optype != "usrinfo")
            {
                errInfo = string.Format("optype = {0},非法.", optype);
                return -1;
            }

            errInfo = "";
            string sql = string.Format("insert into loginfo values(NULL,'{0}','{1}','{2}','{3}','{4}','{5}')", DateTime.Now.ToString(),level,username,optype,sn,message);
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    if (cmd.ExecuteNonQuery() < 0)
                    {
                        Logger.Trace(Logger.__INFO__, sql);
                        errInfo = string.Format("ExecuteNonQuery失败");
                        return -4;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                errInfo = e.Message + e.StackTrace;
                return -4;
            }

            return 0;
        }


        /// <summary>
        /// 删除指定的用户Log记录
        /// </summary>
        /// <param name="timeStart">起始时间，如2019-03-12 12:34:56,不过滤时传入""</param>
        /// <param name="timeEnded">结束时间，如2019-03-13 12:34:56,不过滤时传入""</param>
        /// <param name="level">等级，不过滤时传入""</param>
        /// <param name="username">用户名，不过滤时传入""</param>
        /// <param name="optype">操作类型，不过滤时传入""</param>
        /// <param name="sn">SN号，不过滤时传入""</param>
        /// <param name="errInfo"></param>
        /// <returns></returns>
        public int loginfo_record_delete(string timeStart,string timeEnded,string level, string username, string optype, string sn, ref string errInfo)
        {
            DateTime t1;
            DateTime t2;
            string level_db = "";
            string username_db = "";
            string optype_db = "";
            string sn_db = "";

            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                errInfo = dicRTV[(int)RC.NO_OPEN];
                return -1;
            }

            #region 时间校验

            if (string.IsNullOrEmpty(timeStart))
            {
                t1 = DateTime.Parse("1970-01-01 00:00:00");
            }
            else
            {
                if (false == DateTime.TryParse(timeStart, out t1))
                {
                    errInfo = string.Format("timeStart = {0},非法.", timeStart);
                    return -1;
                }
            }

            if (string.IsNullOrEmpty(timeEnded))
            {
                t2 = DateTime.Parse("2970-01-01 00:00:00");
            }
            else
            {
                if (false == DateTime.TryParse(timeEnded, out t2))
                {
                    errInfo = string.Format("timeEnded = {0},非法.", timeEnded);
                    return -1;
                }
            }

            if (DateTime.Compare(t1, t2) >= 0)
            {
                errInfo = string.Format("timeStart:{0} >= timeEnded={1}", timeStart,timeEnded);
                return -1;
            }

            #endregion

            #region 其他校验

            if (string.IsNullOrEmpty(level))
            {
                level_db = "";
            }
            else
            {
                if (level != "Warning" && level != "Info" && level != "Debug")
                {
                    errInfo = string.Format("level = {0},非法.", level);
                    return -1;
                }

                level_db = level;
            }

            if (string.IsNullOrEmpty(username))
            {
                username_db = "";
            }
            else
            {
                username_db = username;
            }

            if (string.IsNullOrEmpty(optype))
            {
                optype_db = "";
            }
            else
            {
                if (optype != "sysinfo" && optype != "devinfo" && optype != "usrinfo")
                {
                    errInfo = string.Format("optype = {0},非法.", optype);
                    return -1;
                }

                optype_db = optype;
            }

            if (string.IsNullOrEmpty(sn))
            {
                sn_db = "";
            }
            else
            {
                sn_db = sn;
            }

            #endregion

            errInfo = "";
            string sql = string.Format("delete from loginfo where time >= '{0}' and time <= '{1}' and level like '%%{2}%%' and username like '%%{3}%%' and optype like '%%{4}%%' and sn like '%%{5}%%'", 
                t1,t2,level_db,username_db,optype_db,sn_db);
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    if (cmd.ExecuteNonQuery() < 0)
                    {
                        Logger.Trace(Logger.__INFO__, sql);
                        errInfo = string.Format("ExecuteNonQuery失败");
                        return -4;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                errInfo = e.Message + e.StackTrace;
                return -4;
            }

            return 0;
        }
       

        /// <summary>
        /// 获取记录集合
        /// </summary>
        /// <param name="lstLogInfo"></param>
        /// <param name="timeStart">起始时间，如2019-03-12 12:34:56,不过滤时传入""</param>
        /// <param name="timeEnded">结束时间，如2019-03-13 12:34:56,不过滤时传入""</param>
        /// <param name="level">等级，不过滤时传入""</param>
        /// <param name="username">用户名，不过滤时传入""</param>
        /// <param name="optype">操作类型，不过滤时传入""</param>
        /// <param name="sn">SN号，不过滤时传入""</param>
        /// <param name="errInfo"></param>
        /// <returns></returns>
        public int loginfo_record_entity_get(ref List<strLogInfo> lstLogInfo, string timeStart, string timeEnded, string level, string username, string optype, string sn, ref string errInfo)
        {
            DateTime t1;
            DateTime t2;
            string level_db = "";
            string username_db = "";
            string optype_db = "";
            string sn_db = "";

            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                errInfo = dicRTV[(int)RC.NO_OPEN];
                return -1;
            }

            #region 时间校验

            if (string.IsNullOrEmpty(timeStart))
            {
                t1 = DateTime.Parse("1970-01-01 00:00:00");
            }
            else
            {
                if (false == DateTime.TryParse(timeStart, out t1))
                {
                    errInfo = string.Format("timeStart = {0},非法.", timeStart);
                    return -1;
                }
            }

            if (string.IsNullOrEmpty(timeEnded))
            {
                t2 = DateTime.Parse("2970-01-01 00:00:00");
            }
            else
            {
                if (false == DateTime.TryParse(timeEnded, out t2))
                {
                    errInfo = string.Format("timeEnded = {0},非法.", timeEnded);
                    return -1;
                }
            }

            if (DateTime.Compare(t1, t2) >= 0)
            {
                errInfo = string.Format("timeStart:{0} >= timeEnded={1}", timeStart, timeEnded);
                return -1;
            }

            #endregion

            #region 其他校验

            if (string.IsNullOrEmpty(level))
            {
                level_db = "";
            }
            else
            {
                if (level != "Warning" && level != "Info" && level != "Debug")
                {
                    errInfo = string.Format("level = {0},非法.", level);
                    return -1;
                }

                level_db = level;
            }

            if (string.IsNullOrEmpty(username))
            {
                username_db = "";
            }
            else
            {
                username_db = username;
            }

            if (string.IsNullOrEmpty(optype))
            {
                optype_db = "";
            }
            else
            {
                if (optype != "sysinfo" && optype != "devinfo" && optype != "usrinfo")
                {
                    errInfo = string.Format("optype = {0},非法.", optype);
                    return -1;
                }

                optype_db = optype;
            }

            if (string.IsNullOrEmpty(sn))
            {
                sn_db = "";
            }
            else
            {
                sn_db = sn;
            }

            #endregion

            errInfo = "";
            lstLogInfo = new List<strLogInfo>();

            string sql = string.Format("select * from loginfo where time >= '{0}' and time <= '{1}' and level like '%%{2}%%' and username like '%%{3}%%' and optype like '%%{4}%%' and sn like '%%{5}%%'",
                t1, t2, level_db, username_db, optype_db, sn_db);

            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            strLogInfo str = new strLogInfo();

                            #region 获取记录

                            //(1)
                            if (!string.IsNullOrEmpty(dr["time"].ToString()))
                            {
                                str.time = dr["time"].ToString();
                            }
                            else
                            {
                                str.time = "";
                            }

                            //(2)
                            if (!string.IsNullOrEmpty(dr["level"].ToString()))
                            {
                                str.level = dr["level"].ToString();
                            }
                            else
                            {
                                str.level = "";
                            }

                            //(3)
                            if (!string.IsNullOrEmpty(dr["username"].ToString()))
                            {
                                str.username = dr["username"].ToString();
                            }
                            else
                            {
                                str.username = "";
                            }

                            //(4)
                            if (!string.IsNullOrEmpty(dr["optype"].ToString()))
                            {
                                str.optype = dr["optype"].ToString();
                            }
                            else
                            {
                                str.optype = "";
                            }

                            //(5)
                            if (!string.IsNullOrEmpty(dr["sn"].ToString()))
                            {
                                str.sn = dr["sn"].ToString();
                            }
                            else
                            {
                                str.sn = "";
                            }

                            //(6)
                            if (!string.IsNullOrEmpty(dr["message"].ToString()))
                            {
                                str.message = dr["message"].ToString();
                            }
                            else
                            {
                                str.message = "";
                            }

                            #endregion

                            lstLogInfo.Add(str);
                        }
                        dr.Close();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                errInfo = e.Message + e.StackTrace;
                return -4;
            }

            return 0;
        }

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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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


        /// <summary>
        /// 返回schedulerinfo表中的所有记录
        /// </summary>
        /// <param name="dt">返回的DataTable</param>
        /// <returns>
        ///   -1 ：数据库尚未打开
        ///   -4 ：数据库操作失败 
        ///    0 : 获取成功          
        /// </returns>
        public int schedulerinfo_record_entity_get(ref DataTable dt)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return -1;
            }

            TaskType tp = new TaskType ();
            string upgradeTimer  = "";

            dt = new DataTable("schedulerinfo");

            DataColumn column0 = new DataColumn();
            column0.DataType = System.Type.GetType("System.Int32");
            column0.ColumnName = "id";

            DataColumn column1 = new DataColumn();
            column1.DataType = System.Type.GetType("System.String");
            column1.ColumnName = "actionName";

            DataColumn column2 = new DataColumn();
            column2.DataType = System.Type.GetType("System.String");
            column2.ColumnName = "actionId";

            DataColumn column3 = new DataColumn();
            column3.DataType = System.Type.GetType("System.Int32");
            column3.ColumnName = "actionType";

            DataColumn column4 = new DataColumn();
            column4.DataType = System.Type.GetType("System.String");
            column4.ColumnName = "actionXmlText";

            DataColumn column5 = new DataColumn();
            column5.DataType = System.Type.GetType("System.Int32");
            column5.ColumnName = "actionCount";

            DataColumn column6 = new DataColumn();
            column6.DataType = System.Type.GetType("System.Int32");
            column6.ColumnName = "successCount";

            DataColumn column7 = new DataColumn();
            column7.DataType = System.Type.GetType("System.Int32");
            column7.ColumnName = "failCount";

            DataColumn column8 = new DataColumn();
            column8.DataType = System.Type.GetType("System.String");
            column8.ColumnName = "actionTime";

            // 2019-01-17
            DataColumn column9 = new DataColumn();
            column9.DataType = System.Type.GetType("System.String");
            column9.ColumnName = "upgradeTimer";

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

            string sql = string.Format("select * from schedulerinfo");
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        if (dr.HasRows)
                        {
                            while (dr.Read())
                            {
                                DataRow row = dt.NewRow();

                                row[0] = int.Parse(dr[0].ToString().Trim());

                                row[1] = dr[1].ToString().Trim();
                                row[2] = dr[2].ToString().Trim();

                                row[3] = int.Parse(dr[3].ToString().Trim());
                                tp = (TaskType)row[3];

                                row[4] = dr[4].ToString().Trim();

                                row[5] = int.Parse(dr[5].ToString().Trim());
                                row[6] = int.Parse(dr[6].ToString().Trim());
                                row[7] = int.Parse(dr[7].ToString().Trim());

                                row[8] = Convert.ToString(dr[8].ToString());
                                row[9] = "";                               

                                dt.Rows.Add(row);
                            }
                            dr.Close();
                        }
                    }
                }

                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    tp = (TaskType)dt.Rows[i]["actionType"];
                    if (tp == TaskType.UpgradTask)
                    {
                        if (0 == apaction_record_get_upgradeTimer_by_upgradeId(ref upgradeTimer, dt.Rows[i]["actionId"].ToString().Trim()))
                        {
                            dt.Rows[i]["upgradeTimer"] = upgradeTimer;                           
                        }              
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
        /// 通过taskinfo中的过滤信息，获取对应的记录集合
        /// </summary>
        /// <param name="dt">
        /// 返回的DataTable，包含的列为：id,actionName,actionId,actionType,
        /// actionXmlText,actionCount,successCount,failCount,actionTime
        /// </param>
        /// <param name="taskinfo">
        ///  要过滤的各种字段，字段为null或者""时表示不过滤该字段
        /// </param>
        /// <returns>
        /// -1 ：数据库尚未打开
        /// -4 ：数据库操作失败 
        ///  0 : 查询成功 
        /// </returns>
        public int schedulerinfo_record_entity_query(ref DataTable dt, TaskInfo taskinfo)
        {
            string sqlSub = "";
            string sql = "";

            string startTime = "";
            string endTime = "";

            if(false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return -1;
            }

            TaskType tp = new TaskType();
            string upgradeTimer = "";

            dt = new DataTable("schedulerinfo");

            DataColumn column0 = new DataColumn();
            column0.DataType = System.Type.GetType("System.Int32");
            column0.ColumnName = "id";

            DataColumn column1 = new DataColumn();
            column1.DataType = System.Type.GetType("System.String");
            column1.ColumnName = "actionName";

            DataColumn column2 = new DataColumn();
            column2.DataType = System.Type.GetType("System.String");
            column2.ColumnName = "actionId";

            DataColumn column3 = new DataColumn();
            column3.DataType = System.Type.GetType("System.Int32");
            column3.ColumnName = "actionType";

            DataColumn column4 = new DataColumn();
            column4.DataType = System.Type.GetType("System.String");
            column4.ColumnName = "actionXmlText";

            DataColumn column5 = new DataColumn();
            column5.DataType = System.Type.GetType("System.Int32");
            column5.ColumnName = "actionCount";

            DataColumn column6 = new DataColumn();
            column6.DataType = System.Type.GetType("System.Int32");
            column6.ColumnName = "successCount";

            DataColumn column7 = new DataColumn();
            column7.DataType = System.Type.GetType("System.Int32");
            column7.ColumnName = "failCount";

            DataColumn column8 = new DataColumn();
            column8.DataType = System.Type.GetType("System.String");
            column8.ColumnName = "actionTime";

            // 2019-01-17
            DataColumn column9 = new DataColumn();
            column9.DataType = System.Type.GetType("System.String");
            column9.ColumnName = "upgradeTimer";

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

            if (!string.IsNullOrEmpty(taskinfo.actionName))
            {
                if (taskinfo.actionName.Length > 64)
                {
                    Logger.Trace(Logger.__INFO__, "taskinfo.actionName参数长度有误");
                    return -2;
                }
                else
                {
                    sqlSub += string.Format(" actionName like '%{0}%' and", taskinfo.actionName);
                }                
            }

            if (!string.IsNullOrEmpty(taskinfo.actionId))
            {
                if (taskinfo.actionId.Length > 64)
                {
                    Logger.Trace(Logger.__INFO__, "taskinfo.actionId参数长度有误");
                    return -2;
                }
                else
                {         
                    sqlSub += string.Format(" actionId like '%{0}%' and", taskinfo.actionId);
                }                     
            }

            if (taskinfo.actionType > 0)
            {
                sqlSub += string.Format(" actionType={0} and", taskinfo.actionType);
            }

            if (!string.IsNullOrEmpty(taskinfo.actionXmlText))
            {
                if (taskinfo.actionXmlText.Length > 8192)
                {
                    Logger.Trace(Logger.__INFO__, "taskinfo.actionXmlText参数长度有误");
                    return -2;
                }
                else
                {  
                    sqlSub += string.Format(" actionXmlText like '%{0}%' and", taskinfo.actionXmlText);
                }                   
            }

            if (taskinfo.actionCount > 0)
            {
                sqlSub += string.Format(" actionCount={0} and", taskinfo.actionCount);
            }

            if (taskinfo.successCount > 0)
            {
                sqlSub += string.Format(" successCount={0} and", taskinfo.successCount);
            }

            if (taskinfo.failCount > 0)
            {
                sqlSub += string.Format(" failCount={0} and", taskinfo.failCount);
            }

            //日期时间
            //if (!taskinfo.actionTimeStart.Equals("") && !taskinfo.actionTimeEnd.Equals(""))
            //{
            //    startTime = taskinfo.actionTimeStart;
            //    endTime = taskinfo.actionTimeEnd;
            //    sqlSub += string.Format(" actionTime >= '{0}' and  actionTime <= '{1}' and", startTime, endTime);
            //}

            if (string.IsNullOrEmpty(taskinfo.actionTimeStart))
            {
                startTime = "";
            }
            else
            {
                try
                {
                    DateTime.Parse(taskinfo.actionTimeStart);
                }
                catch
                {
                    Logger.Trace(Logger.__INFO__, "taskinfo.actionTimeStart参数格式有误");
                    return -2;
                }

                startTime = taskinfo.actionTimeStart;
            }


            if (string.IsNullOrEmpty(taskinfo.actionTimeEnd))
            {
                //赋一个很大的值
                endTime = "2100-01-01 12:34:56";
            }
            else
            {
                try
                {
                    DateTime.Parse(taskinfo.actionTimeEnd);
                }
                catch
                {
                    Logger.Trace(Logger.__INFO__, "taskinfo.actionTimeEnd参数格式有误");
                    return -2;
                }

                endTime = taskinfo.actionTimeEnd;
            }

            sqlSub += string.Format(" actionTime >= '{0}' and  actionTime <= '{1}' and", startTime, endTime);

            //去除多余项
            if (sqlSub.Length > 0)
            {
                sqlSub = sqlSub.Remove(sqlSub.Length - 3, 3);
            }
            //////////////////////////////////////////////////////////////////
            //////////////////////////////////////////////////////////////////

            if (sqlSub != "")
            {
                sql = string.Format("select * from schedulerinfo where {0}", sqlSub);
            }
            else
            {
                //无任何过滤的字段
                Logger.Trace(Logger.__INFO__, "schedulerinfo_record_entity_query条件查询");
                sql = string.Format("select * from schedulerinfo");
            }

            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        if (dr.HasRows)
                        {
                            while (dr.Read())
                            {
                                DataRow row = dt.NewRow();

                                row[0] = int.Parse(dr[0].ToString().Trim());

                                row[1] = dr[1].ToString().Trim();
                                row[2] = dr[2].ToString().Trim();

                                row["actionType"] = int.Parse(dr["actionType"].ToString().Trim());

                                row[4] = dr[4].ToString().Trim();

                                row[5] = int.Parse(dr[5].ToString().Trim());
                                row[6] = int.Parse(dr[6].ToString().Trim());
                                row[7] = int.Parse(dr[7].ToString().Trim());

                                row[8] = Convert.ToString(dr[8].ToString());

                                dt.Rows.Add(row);
                            }
                            dr.Close();
                        }
                    }
                }

                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    tp = (TaskType)dt.Rows[i]["actionType"];
                    if (tp == TaskType.UpgradTask)
                    {
                        if (0 == apaction_record_get_upgradeTimer_by_upgradeId(ref upgradeTimer, dt.Rows[i]["actionId"].ToString().Trim()))
                        {
                            dt.Rows[i]["upgradeTimer"] = upgradeTimer;
                        }
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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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
                            row[4] = dr[4].ToString();
                            row[5] = dr[5].ToString();

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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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
                            row[4] = dr[4].ToString();
                            row[5] = dr[5].ToString();

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
        /// 判断sn,actionId和name对应的记录是否在tmpvalue表中
        /// </summary>
        /// <param name="sn">AP的SN号</param>
        /// <param name="apactionId">AP的action ID</param>
        /// <param name="name">参数名称</param>
        /// <returns> 
        ///   -1 ：数据库尚未打开
        ///   -2 ：参数有误
        ///   -4 ：数据库操作失败 
        ///    0 : 不存在
        ///    1 ：存在
        /// </returns>
        public int tmpvalue_record_exist(string sn, string apactionId, string name)
        {
            UInt32 cnt = 0;

            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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

            if (string.IsNullOrEmpty(apactionId))
            {
                Logger.Trace(Logger.__INFO__, "apactionId参数为空");
                return -2;
            }

            if (apactionId.Length > 128)
            {
                Logger.Trace(Logger.__INFO__, "apactionId参数长度有误");
                return -2;
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

            string sql = string.Format("select count(*) from tmpvalue where sn = '{0}' and apactionId = '{1}' and name = '{2}'", sn,apactionId,name);
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
        /// 向tmpvalue表中插入记录
        /// </summary>
        /// <param name="sn"></param>
        /// <param name="actionId"></param>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <returns> 
        ///   -1 ：数据库尚未打开
        ///   -2 ：参数有误
        ///   -3 ：记录已经存在
        ///   -4 ：数据库操作失败 
        ///   -5 ：用户名不存在
        ///    0 : 插入成功
        /// </returns>
        public int tmpvalue_record_entity_insert(string sn, string actionId, string name, string value, string type)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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

            if (actionId.Length > 128)
            {
                Logger.Trace(Logger.__INFO__, "actionId参数长度有误");
                return -2;
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


            if (string.IsNullOrEmpty(value))
            {
                Logger.Trace(Logger.__INFO__, "value参数为空");
                return -2;
            }

            if (value.Length > 256)
            {
                Logger.Trace(Logger.__INFO__, "value参数长度有误");
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

            // 判断记录是否已经存在
            if (1 == tmpvalue_record_exist(sn,actionId,name))
            {
                Logger.Trace(Logger.__INFO__, "tmpvalue记录已经存在");
                return -3;
            }

            string sql = string.Format("insert into tmpvalue values(NULL,'{0}','{1}','{2}','{3}','{4}','{5}')",
                sn, actionId, name, value, type, DateTime.Now.ToString());

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
        /// <returns>
        /// -1 ：数据库尚未打开
        /// -4 ：数据库操作失败 
        ///  0 : 查询成功 
        /// </returns>
        public int tmpvalue_record_entity_get_by_query(ref DataTable dt, string sn, string apactionId)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return -1;
            }

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

            string sql = string.Format("select * from tmpvalue where sn='{0}' and apactionId='{1}'", sn, apactionId);

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
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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

        #region 14 - 省市区街道操作

        /// <summary>
        /// 获取省
        /// </summary>
        /// <param name="province">返回省列表</param>
        /// <returns>
        ///   -1 ：数据库尚未打开
        ///   -4 ：数据库操作失败 
        ///    0 : 获取成功      
        /// </returns>
        public int db_getProvince_info(ref List<Province> province)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return -1;
            }

            string sql = string.Format("select provice_id,provice_name from j_position_provice");
            Province provinceVlues = new Province();
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        if (dr.HasRows)
                        {
                            while (dr.Read())
                            {
                                provinceVlues.provice_id = dr.GetString(0);
                                provinceVlues.provice_name = dr.GetString(1);
                                province.Add(provinceVlues);
                            }
                        }
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
        /// 通过省Id号获取对应的城市列表
        /// </summary>
        /// <param name="city">返回的省Id对应城市列表</param>
        /// <param name="provinceId">省Id</param>
        /// <returns>
        ///   -1 ：数据库尚未打开
        ///   -4 ：数据库操作失败 
        ///    0 : 获取成功      
        /// </returns>
        public int db_getCity_info(ref List<City> city, string provinceId)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return -1;
            }

            string sql = string.Format("select city_id,city_name from j_position_city where province_id='{0}'", provinceId);
            City cityVlues = new City();

            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        if (dr.HasRows)
                        {
                            while (dr.Read())
                            {
                                cityVlues.city_id = dr.GetString(0);
                                cityVlues.city_name = dr.GetString(1);
                                city.Add(cityVlues);
                            }
                        }
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
        /// 通过城市Id号获取对应的区/县列表
        /// </summary>
        /// <param name="distract">返回的城市Id号对应的区/县列表</param>
        /// <param name="cityId">城市Id</param>
        /// <returns>
        ///   -1 ：数据库尚未打开
        ///   -4 ：数据库操作失败 
        ///    0 : 获取成功      
        /// </returns>
        public int db_getCounty_info(ref List<County> distract, string cityId)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return -1;
            }

            string sql = string.Format("select county_id,county_name from j_position_county where city_id='{0}'", cityId);
            County countyVlues = new County();

            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        if (dr.HasRows)
                        {
                            while (dr.Read())
                            {
                                countyVlues.county_id = dr.GetString(0);
                                countyVlues.county_name = dr.GetString(1);
                                distract.Add(countyVlues);
                            }
                        }
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
        /// 通过区Id号获取对应的街道列表
        /// </summary>
        /// <param name="town">返回的街道列表</param>
        /// <param name="cityId">城市Id</param>
        /// <returns>
        ///   -1 ：数据库尚未打开
        ///   -4 ：数据库操作失败 
        ///    0 : 获取成功      
        /// </returns>
        public int db_getTown_info(ref List<Town> town, string countyId)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return -1;
            }

            string sql = string.Format("select town_id,town_name from j_position_town where county_id='{0}'", countyId);
            Town townVlues = new Town();

            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        if (dr.HasRows)
                        {
                            while (dr.Read())
                            {
                                townVlues.town_id = dr.GetString(0);
                                townVlues.town_name = dr.GetString(1);
                                town.Add(townVlues);
                            }
                        }
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

        #region 15 - alarminfo操作

        /// <summary>
        /// 判断对应sn的记录是否存在alarminfo表中
        /// </summary>
        /// <param name="sn">AP的SN号</param>
        /// <returns> 
        ///   -1 ：数据库尚未打开
        ///   -2 ：参数有误
        ///   -4 ：数据库操作失败 
        ///    0 : 不存在
        ///    1 ：存在
        /// </returns>
        private int alarminfo_record_exist(string sn)
        {
            UInt32 cnt = 0;

            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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

            string sql = string.Format("select count(*) from alarminfo where sn = '{0}'", sn);
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
        /// 判断对应sn的记录是否存在alarminfo表中
        /// </summary>
        /// <param name="sn">AP的SN号</param>
        /// <param name="flag">告警标识</param>
        /// <returns> 
        ///   -1 ：数据库尚未打开
        ///   -2 ：参数有误
        ///   -4 ：数据库操作失败 
        ///    0 : 不存在
        ///    1 ：存在
        /// </returns>
        private int alarminfo_record_exist(string sn, string flag)
        {
            UInt32 cnt = 0;

            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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

            if (string.IsNullOrEmpty(flag))
            {
                Logger.Trace(Logger.__INFO__, "flag参数为空");
                return -2;
            }

            if (flag.Length > 32)
            {
                Logger.Trace(Logger.__INFO__, "flag参数长度有误");
                return -2;
            }

            string sql = string.Format("select count(*) from alarminfo where sn = '{0}' and flag = '{1}'", sn, flag);
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
        /// 判断对应sn的记录是否存在alarminfo表中
        /// </summary>
        /// <param name="noticeType">noticeType</param>
        /// <param name="flag">告警标识</param>
        /// <param name="sn">AP的SN号</param>
        /// <returns> 
        ///    0 : 不存在
        ///    1 ：存在
        /// </returns>
        private int alarminfo_record_exist(string noticeType, string flag,string sn)
        {
            UInt32 cnt = 0;

            string sql = string.Format("select count(*) from alarminfo where noticeType = '{0}' and flag = '{1}' and sn = '{2}'", noticeType, flag, sn);
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
        /// 插入记录到alarminfo表中
        /// SN和flag组成记录的关键字
        /// </summary> 
        /// <param name="sn">AP的SN号</param>
        /// <param name="flag">告警标识</param>
        /// <returns>
        ///   -1 ：数据库尚未打开
        ///   -2 ：参数有误
        ///   -3 ：记录已经存在
        ///   -4 ：数据库操作失败 
        ///  -28 ：设备不存在       
        ///    0 : 插入成功 
        /// </returns>
        private int alarminfo_record_insert(string sn, strAlarm ai)
        {
            string sqlSub = "";

            //(1)
            if (string.IsNullOrEmpty(ai.vendor))
            {
                sqlSub += string.Format("'{0}',","");
            }
            else
            {
                sqlSub += string.Format("'{0}',",ai.vendor);
            }

            //(2)
            if (string.IsNullOrEmpty(ai.level))
            {
                sqlSub += string.Format("'{0}',", "");
            }
            else
            {
                sqlSub += string.Format("'{0}',", ai.level);
            }

            //(3),(4),alarmTime,clearTime
            sqlSub += string.Format("now(),'0',");

            //(5)
            if (string.IsNullOrEmpty(ai.noticeType))
            {
                sqlSub += string.Format("'{0}',", "");
            }
            else
            {
                sqlSub += string.Format("'{0}',", ai.noticeType);
            }

            //(6)
            if (string.IsNullOrEmpty(ai.cause))
            {
                sqlSub += string.Format("'{0}',", "");
            }
            else
            {
                sqlSub += string.Format("'{0}',", ai.cause);
            }

            //(7)
            if (string.IsNullOrEmpty(ai.flag))
            {
                sqlSub += string.Format("'{0}',", "");
            }
            else
            {
                sqlSub += string.Format("'{0}',", ai.flag);
            }

            //(8)
            if (string.IsNullOrEmpty(ai.des))
            {
                sqlSub += string.Format("'{0}',", "");
            }
            else
            {
                sqlSub += string.Format("'{0}',", ai.des);
            }

            //(9)
            if (string.IsNullOrEmpty(ai.addDes))
            {
                sqlSub += string.Format("'{0}',", "");
            }
            else
            {
                sqlSub += string.Format("'{0}',", ai.addDes);
            }

            //(10)
            if (string.IsNullOrEmpty(ai.addInfo))
            {
                sqlSub += string.Format("'{0}',", "");
            }
            else
            {
                sqlSub += string.Format("'{0}',", ai.addInfo);
            }

            //(11)
            if (string.IsNullOrEmpty(ai.res1))
            {
                sqlSub += string.Format("'{0}',", "");
            }
            else
            {
                sqlSub += string.Format("'{0}',", ai.res1);
            }

            //(12)
            if (string.IsNullOrEmpty(ai.res2))
            {
                sqlSub += string.Format("'{0}',", "");
            }
            else
            {
                sqlSub += string.Format("'{0}',", ai.res2);
            }

            //(13)
            if (string.IsNullOrEmpty(sn))
            {
                sqlSub += string.Format("'{0}'", "");
            }
            else
            {
                sqlSub += string.Format("'{0}'", sn);
            }

            string sql = string.Format("insert into alarminfo values(NULL,{0})", sqlSub);
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
        /// 通过SN，更新结构体中的各种字段
        /// </summary>
        /// <param name="sn">AP的SN号</param>
        /// <param name="di">
        /// 要更新的各种字段，字段为null或者""时表示不更新
        /// 可更新的字段如下：
        //  public string vendor;      //厂商
        //  public string level;       //告警级别,Critical,Major,Minor,Warning
        //  public string noticeType;  //通知类型,NewAlarm,ClearAlarm

        //  public string cause;       //告警原因        
        //  public string des;         //告警描述
        //  public string addDes;      //附加描述
        //  public string addInfo;     //附加信息
        //  public string res1;        //保留字段1
        //  public string res2;        //保留字段2
        /// </param>
        /// <returns>
        /// 返回值 ：
        /// -1 ：数据库尚未打开
        /// -2 ：参数有误
        /// -3 ：SN不存在
        /// -6 : 记录不存在
        /// -4 ：数据库操作失败 
        ///  0 : 更新成功  
        /// </returns>
        private int alarminfo_record_update(string sn,strAlarm ai)
        {
            int ret = 0;
            string sql = "";
            string sqlSub = "";                      

            #region 构造SQL语句

            //(1)
            if (!string.IsNullOrEmpty(ai.vendor))
            {
                if (ai.vendor.Length > 32)
                {
                    Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.PAR_LEN_ERR], "DB", LogCategory.I);
                    return (int)RC.PAR_LEN_ERR;
                }
                else
                {
                    sqlSub += string.Format("vendor = '{0}',", ai.vendor);
                }
            }

            //(2)
            if (!string.IsNullOrEmpty(ai.level))
            {
                if (ai.level != "Critical" &&
                    ai.level != "Major" &&
                    ai.level != "Minor" &&
                    ai.level != "Warning")
                {
                    Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.PAR_FMT_ERR], "DB", LogCategory.I);
                    return (int)RC.PAR_FMT_ERR;
                }

                if (ai.level.Length > 32)
                {
                    Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.PAR_LEN_ERR], "DB", LogCategory.I);
                    return (int)RC.PAR_LEN_ERR;
                }
                else
                {
                    sqlSub += string.Format("level = '{0}',", ai.level);
                }
            }


            //(3)
            if (!string.IsNullOrEmpty(ai.noticeType))
            {
                if (ai.noticeType != "ClearAlarm" && ai.noticeType != "NewAlarm")
                {
                    Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.PAR_FMT_ERR], "DB", LogCategory.I);
                    return (int)RC.PAR_FMT_ERR;
                }

                if (ai.vendor.Length > 32)
                {
                    Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.PAR_LEN_ERR], "DB", LogCategory.I);
                    return (int)RC.PAR_LEN_ERR;
                }
                else
                {
                    sqlSub += string.Format("noticeType = '{0}',", ai.noticeType);

                    if (ai.noticeType == "NewAlarm")
                    {
                        sqlSub += string.Format("alarmTime = now(),");
                    }
                    else
                    {
                        sqlSub += string.Format("clearTime = now(),");
                    }
                }
            }

            //(4)
            if (!string.IsNullOrEmpty(ai.cause))
            {
                if (ai.cause.Length > 256)
                {
                    Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.PAR_LEN_ERR], "DB", LogCategory.I);
                    return (int)RC.PAR_LEN_ERR;
                }
                else
                {
                    sqlSub += string.Format("cause = '{0}',", ai.cause);
                }
            }

            //(5)
            if (!string.IsNullOrEmpty(ai.des))
            {
                if (ai.des.Length > 256)
                {
                    Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.PAR_LEN_ERR], "DB", LogCategory.I);
                    return (int)RC.PAR_LEN_ERR;
                }
                else
                {
                    sqlSub += string.Format("des = '{0}',", ai.des);
                }
            }

            //(6)
            if (!string.IsNullOrEmpty(ai.addDes))
            {
                if (ai.addDes.Length > 256)
                {
                    Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.PAR_LEN_ERR], "DB", LogCategory.I);
                    return (int)RC.PAR_LEN_ERR;
                }
                else
                {
                    sqlSub += string.Format("addDes = '{0}',", ai.addDes);
                }
            }

            //(7)
            if (!string.IsNullOrEmpty(ai.addInfo))
            {
                if (ai.addInfo.Length > 256)
                {
                    Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.PAR_LEN_ERR], "DB", LogCategory.I);
                    return (int)RC.PAR_LEN_ERR;
                }
                else
                {
                    sqlSub += string.Format("addInfo = '{0}',", ai.addInfo);
                }
            }

            //(8)
            if (!string.IsNullOrEmpty(ai.res1))
            {
                if (ai.res1.Length > 32)
                {
                    Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.PAR_LEN_ERR], "DB", LogCategory.I);
                    return (int)RC.PAR_LEN_ERR;
                }
                else
                {
                    sqlSub += string.Format("res1 = '{0}',", ai.res1);
                }
            }

            //(9)
            if (!string.IsNullOrEmpty(ai.res2))
            {
                if (ai.res2.Length > 32)
                {
                    Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.PAR_LEN_ERR], "DB", LogCategory.I);
                    return (int)RC.PAR_LEN_ERR;
                }
                else
                {
                    sqlSub += string.Format("res2 = '{0}',", ai.res2);
                }
            }

            #endregion

            if (sqlSub != "")
            {               
                //去掉最后一个字符
                sqlSub = sqlSub.Remove(sqlSub.Length - 1, 1);

                //更新deviceinfo表中的信息               
                sql = string.Format("update alarminfo set {0} where flag = '{1}' and sn = '{2}'", sqlSub, ai.flag, sn);

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
                Logger.Trace(Logger.__INFO__, "alarminfo_record_update,无需更新alarminfo表");
            }

            return ret;
        }

        /// <summary>
        /// 将NewAlarm，flag和sn对应的记录修改成ClearAlarm
        /// </summary>
        /// <param name="flag">flag</param>       
        /// <param name="sn">AP的SN号</param>     
        /// </param>
        /// <returns>
        /// 返回值 ：
        /// -1 ：数据库尚未打开
        /// -2 ：参数有误
        /// -3 ：SN不存在
        /// -6 : 记录不存在
        /// -4 ：数据库操作失败 
        ///  0 : 更新成功  
        /// </returns>
        private int alarminfo_record_set_2_clearalarm(string flag, string sn)
        {
            int ret = 0;
            string sql = "";
            
            sql = string.Format("update alarminfo set clearTime=now(),noticeType='ClearAlarm' where noticeType='NewAlarm' and flag = '{0}' and sn = '{1}'", flag, sn);

            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    if (cmd.ExecuteNonQuery() < 0)
                    {
                        string errInfo = string.Format("出错:{0}", sql);
                        Logger.Trace(LogInfoType.EROR, errInfo, "DB", LogCategory.I);                              
                        return -4;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(e);
                return -4;
            }

            return ret;
        }

        /// <summary>
        /// 清空所有的记录
        /// </summary>    
        /// <returns>
        ///   RC.NO_OPEN         ：数据库尚未打开
        ///   RC.OP_FAIL         ：数据库操作失败 
        ///   RC.SUCCESS         ：成功
        /// </returns>
        private int alarminfo_record_clear()
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return (int)RC.NO_OPEN;
            }

            string sql = string.Format("delete from alarminfo");

            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    if (cmd.ExecuteNonQuery() < 0)
                    {
                        Logger.Trace(LogInfoType.EROR, sql, "DB", LogCategory.I);
                        return (int)RC.OP_FAIL;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(LogInfoType.EROR, e.Message + e.StackTrace, "DB", LogCategory.I);
                dicRTV[(int)RC.OP_FAIL] = string.Format("数据库操作失败:{0}", e.Message + e.StackTrace); 
                return (int)RC.OP_FAIL;
            }

            return (int)RC.SUCCESS;
        }        

        /// <summary>
        /// 生成alarm相关的CSV文件
        /// </summary>
        /// <param name="fileFullPath"></param>
        /// <param name="capList"></param>
        /// <param name="errInfo"></param>
        /// <returns></returns>
        private int alarm_record_generate_csv_file(string fileFullPath, List<strAlarm> capList,ref string errInfo)
        {
            if (string.IsNullOrEmpty(fileFullPath))
            {
                errInfo = string.Format("fileFullPath is NULL");
                Logger.Trace(LogInfoType.EROR, errInfo, "DB", LogCategory.I);
                return -1;
            }
            
            if (capList == null || capList.Count == 0)
            {
                errInfo = string.Format("capList is empty.");
                Logger.Trace(LogInfoType.EROR, errInfo, "DB", LogCategory.I);
                return -1;
            }

           
            //public int id;             //id
            //public string vendor;      //厂商
            //public string level;       //告警级别,Critical,Major,Minor,Warning
            //public string alarmTime;   //告警时间,输入时不需要带入
            //public string clearTime;   //清除时间,输入时不需要带入     
            //public string noticeType;  //通知类型,NewAlarm,ClearAlarm
            //public string cause;       //告警原因
            //public string flag;        //告警标识，100000,72,54,34等
            //public string des;         //告警描述
            //public string addDes;      //附加描述
            //public string addInfo;     //附加信息
            //public string res1;        //保留字段1
            //public string res2;        //保留字段2
            //public string sn;          //外键，PK

            try
            {
                if (File.Exists(fileFullPath))
                {
                    File.Delete(fileFullPath);
                }

                byte[] data = null;
                FileStream fs = new FileStream(fileFullPath, FileMode.Create);

                string title = string.Format("序号,厂商,告警级别,告警时间,清除时间,通知类型,告警原因,告警标识,告警描述,附加描述,附加信息,SN\n");

                data = System.Text.Encoding.Default.GetBytes(title);
                fs.Write(data, 0, data.Length);

                int index = 1;
                foreach (strAlarm cap in capList)
                {
                    string sqlSub = "";

                    #region 构造字符串

                    sqlSub += string.Format("{0},", index++);

                    //(1)
                    sqlSub += string.Format("{0},", cap.vendor);
                   
                    //(2)
                    sqlSub += string.Format("{0},", cap.level);

                    //(3)
                    sqlSub += string.Format("{0},", cap.alarmTime);

                    //(4)
                    sqlSub += string.Format("{0},", cap.clearTime);

                    //(5)
                    sqlSub += string.Format("{0},", cap.noticeType);

                    //(6)
                    sqlSub += string.Format("{0},", cap.cause);

                    //(7)
                    sqlSub += string.Format("{0},", cap.flag);

                    //(8)
                    sqlSub += string.Format("{0},", cap.des);

                    //(9)
                    sqlSub += string.Format("{0},", cap.addDes);

                    //(10)
                    sqlSub += string.Format("{0},", cap.addInfo);

                    //(11)
                    sqlSub += string.Format("{0}", cap.sn);

                    #endregion

                    data = System.Text.Encoding.Default.GetBytes(sqlSub + "\n");
                    fs.Write(data, 0, data.Length);
                }

                //清空缓冲区、关闭流
                fs.Flush();
                fs.Close();
                fs.Dispose();
                fs = null;
            }
            catch (Exception e)
            {
                errInfo = e.Message + e.StackTrace;
                Logger.Trace(LogInfoType.EROR, e.Message + e.StackTrace, "Main", LogCategory.I);
                return -1;
            }

            return 0;
        }       


        /// <summary>
        /// 通过SN，创建或更新一条告警记录
        /// </summary>
        /// <param name="sn">AP的SN号</param>
        /// <param name="di">
        /// 要更新的各种字段，字段为null或者""时表示不更新
        /// 可更新的字段如下：
        //  public string vendor;      //厂商
        //  public string level;       //告警级别,Critical,Major,Minor,Warning
        //  public string noticeType;  //通知类型,NewAlarm,ClearAlarm
        //  public string cause;       //告警原因        
        //  public string des;         //告警描述
        //  public string addDes;      //附加描述
        //  public string addInfo;     //附加信息
        //  public string res1;        //保留字段1
        //  public string res2;        //保留字段2
        /// </param>
        /// <returns>
        /// 返回值 ：
        /// 非0 ：失败
        ///  0  : 成功  
        /// </returns>
        public int alarminfo_record_create(string sn, strAlarm ai,ref string errInfo)
        {       
            if (false == myDbConnFlag)
            {
                errInfo = get_rtv_str((int)RC.NO_OPEN);
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return -1;
            }

            if (string.IsNullOrEmpty(sn) || (sn.Length > 32))
            {
                errInfo = string.Format("sn={0},参数非法",sn);
                Logger.Trace(LogInfoType.EROR,errInfo, "DB", LogCategory.I);               
                return -1;
            }  
         
            if (string.IsNullOrEmpty(ai.flag) || (ai.flag.Length > 32))
            {
                errInfo = string.Format("ai.flag={0},参数非法",ai.flag);
                Logger.Trace(LogInfoType.EROR,errInfo, "DB", LogCategory.I);               
                return -1;
            }

            if (string.IsNullOrEmpty(ai.noticeType) || (ai.noticeType != "NewAlarm" && ai.noticeType != "ClearAlarm"))
            {
                errInfo = string.Format("ai.noticeType={0},参数非法", ai.noticeType);
                Logger.Trace(LogInfoType.EROR, errInfo, "DB", LogCategory.I);
                return -1;
            }


            if (string.IsNullOrEmpty(ai.level) || (ai.level.Length > 32))
            {
                errInfo = string.Format("ai.level={0},参数非法",ai.level);
                Logger.Trace(LogInfoType.EROR,errInfo, "DB", LogCategory.I);               
                return -1;
            }
            else
            {
                if (ai.level != "Critical" && ai.level != "Major" && ai.level != "Minor" && ai.level != "Warning")
                {
                    errInfo = string.Format("ai.level={0},参数非法", ai.level);
                    Logger.Trace(LogInfoType.EROR, errInfo, "DB", LogCategory.I);
                    return -1;
                }
            }
                               
            //检查设备是否存在
            if (0 == deviceinfo_record_exist(sn))
            {
                errInfo = get_rtv_str((int)RC.DEV_NO_EXIST);
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.DEV_NO_EXIST], "DB", LogCategory.I);
                return (int)RC.DEV_NO_EXIST;
            }

            if (ai.noticeType == "NewAlarm")
            {
                //插入处理

                //(1)先判断NewAlarm,flag,sn对应的记录是否存在
                if (1 == alarminfo_record_exist("NewAlarm", ai.flag, sn))
                {
                    errInfo = string.Format("{0},{1},{2}对应的记录已经存在.", "NewAlarm", ai.flag, sn);
                    Logger.Trace(LogInfoType.EROR, errInfo, "DB", LogCategory.I);
                    return -1;
                }

                if (0 != alarminfo_record_insert(sn, ai))
                {
                    errInfo = string.Format("alarminfo_record_insert出错.");
                    Logger.Trace(LogInfoType.EROR, errInfo, "DB", LogCategory.I);
                    return -1;
                }

                return 0;
            }
            else
            {
                //更新clearalarm处理

                //(1)先判断NewAlarm,flag,sn对应的记录是否存在
                if (0 == alarminfo_record_exist("NewAlarm", ai.flag, sn))
                {
                    errInfo = string.Format("{0},{1},{2}对应的记录不存在.", ai.noticeType,ai.flag,sn);
                    Logger.Trace(LogInfoType.EROR, errInfo, "DB", LogCategory.I);
                    return -1;
                }

                //(2)更新记录             
                if (0 != alarminfo_record_set_2_clearalarm(ai.flag, sn))
                {
                    errInfo = string.Format("alarminfo_record_set_2_clearalarm出错.");
                    Logger.Trace(LogInfoType.EROR, errInfo, "DB", LogCategory.I);
                    return -1;
                }

                return 0;
            }           
        }

        /// <summary>
        /// 在alarminfo表中删除指定的SN
        /// </summary>
        /// <param name="sn">AP的SN号</param>
        /// <param name="flag">告警标识：
        /// ""时表示删除SN下所有的告警信息
        /// 不为""时表示删除SN下该flag
        /// </param>
        /// <returns>
        /// -1 ：数据库尚未打开
        /// -2 ：参数有误
        /// -6 ：记录不存在
        /// -4 ：数据库操作失败 
        ///-28 ：设备不存在
        ///  0 : 删除成功 
        /// </returns>
        public int alarminfo_record_delete(string sn, string flag)
        {
            int ret = 0;
            string sql = "";
            string flagTmp = "";

            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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

            if (string.IsNullOrEmpty(flag))
            {
                flagTmp = "";
            }
            else
            {
                flagTmp = flag;
            }

            if (flag.Length > 32)
            {
                Logger.Trace(Logger.__INFO__, "flag参数长度有误");
                return -2;
            }


            //检查设备是否存在
            if (0 == deviceinfo_record_exist(sn))
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.DEV_NO_EXIST], "DB", LogCategory.I);
                return (int)RC.DEV_NO_EXIST;
            }

            if (flagTmp != "")
            {
                //检查记录是否存在
                if (0 == alarminfo_record_exist(sn, flag))
                {
                    Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_EXIST], "DB", LogCategory.I);
                    return (int)RC.NO_EXIST;
                }

                sql = string.Format("delete from alarminfo where flag = '{0}' and sn = '{1}'", flag, sn);
            }
            else
            {
                sql = string.Format("delete from alarminfo where sn = '{0}'",sn);
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
            
            return ret;
        }        

        /// <summary>
        /// 获取alarminfo表中的各条记录
        /// </summary>
        /// <param name="dt">返回的记录</param>
        /// <param name="sn">
        /// ""时返回所有的记录
        /// 不为""时返回该sn下所有的记录
        /// </param>
        /// <returns>
        /// -1 ：数据库尚未打开
        /// -4 ：数据库操作失败 
        ///  0 : 查询成功 
        /// </returns>
        public int alarminfo_record_entity_get(ref DataTable dt,string sn)
        {
            string sql = "";

            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return -1;
            }

            if (sn.Length > 32)
            {
                Logger.Trace(Logger.__INFO__, "sn参数长度有误");
                return -2;
            }

            dt = new DataTable("alarminfo");

            DataColumn column0 = new DataColumn("id",System.Type.GetType("System.Int32"));
            DataColumn column1 = new DataColumn("vendor", System.Type.GetType("System.String"));
            DataColumn column2 = new DataColumn("level", System.Type.GetType("System.String"));
            DataColumn column3 = new DataColumn("alarmTime", System.Type.GetType("System.String"));
            DataColumn column4 = new DataColumn("clearTime", System.Type.GetType("System.String"));
            DataColumn column5 = new DataColumn("noticeType", System.Type.GetType("System.String"));
            DataColumn column6 = new DataColumn("cause", System.Type.GetType("System.String"));
            DataColumn column7 = new DataColumn("flag", System.Type.GetType("System.String"));
            DataColumn column8 = new DataColumn("des", System.Type.GetType("System.String"));
            DataColumn column9 = new DataColumn("addDes", System.Type.GetType("System.String"));
            DataColumn column10 = new DataColumn("addInfo", System.Type.GetType("System.String"));
            DataColumn column11 = new DataColumn("res1", System.Type.GetType("System.String"));
            DataColumn column12 = new DataColumn("res2", System.Type.GetType("System.String"));
            DataColumn column13 = new DataColumn("sn", System.Type.GetType("System.String"));

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
            
            if (string.IsNullOrEmpty(sn))
            {
                sql = string.Format("select * from alarminfo");
            }
            else
            {
                sql = string.Format("select * from alarminfo where sn = '{0}'",sn);
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

                            row["id"] = Convert.ToInt32(dr["id"]);                            

                            if (!string.IsNullOrEmpty(dr["vendor"].ToString()))
                            {
                                row["vendor"] = dr["vendor"].ToString();
                            }
                            else
                            {
                                row["vendor"] = "";
                            }

                            if (!string.IsNullOrEmpty(dr["level"].ToString()))
                            {
                                row["level"] = dr["level"].ToString();
                            }
                            else
                            {
                                row["level"] = "";
                            }

                            if (!string.IsNullOrEmpty(dr["alarmTime"].ToString()))
                            {
                                row["alarmTime"] = dr["alarmTime"].ToString();
                            }
                            else
                            {
                                row["alarmTime"] = "";
                            }

                            if (!string.IsNullOrEmpty(dr["clearTime"].ToString()))
                            {
                                row["clearTime"] = dr["clearTime"].ToString();
                            }
                            else
                            {
                                row["clearTime"] = "";
                            }

                            if (!string.IsNullOrEmpty(dr["noticeType"].ToString()))
                            {
                                row["noticeType"] = dr["noticeType"].ToString();
                            }
                            else
                            {
                                row["noticeType"] = "";
                            }

                            if (!string.IsNullOrEmpty(dr["cause"].ToString()))
                            {
                                row["cause"] = dr["cause"].ToString();
                            }
                            else
                            {
                                row["cause"] = "";
                            }

                            if (!string.IsNullOrEmpty(dr["flag"].ToString()))
                            {
                                row["flag"] = dr["flag"].ToString();
                            }
                            else
                            {
                                row["flag"] = "";
                            }

                            if (!string.IsNullOrEmpty(dr["des"].ToString()))
                            {
                                row["des"] = dr["des"].ToString();
                            }
                            else
                            {
                                row["des"] = "";
                            }

                            if (!string.IsNullOrEmpty(dr["addDes"].ToString()))
                            {
                                row["addDes"] = dr["addDes"].ToString();
                            }
                            else
                            {
                                row["addDes"] = "";
                            }

                            if (!string.IsNullOrEmpty(dr["addInfo"].ToString()))
                            {
                                row["addInfo"] = dr["addInfo"].ToString();
                            }
                            else
                            {
                                row["addInfo"] = "";
                            }

                            if (!string.IsNullOrEmpty(dr["res1"].ToString()))
                            {
                                row["res1"] = dr["res1"].ToString();
                            }
                            else
                            {
                                row["res1"] = "";
                            }

                            if (!string.IsNullOrEmpty(dr["res2"].ToString()))
                            {
                                row["res2"] = dr["res2"].ToString();
                            }
                            else
                            {
                                row["res2"] = "";
                            }

                            if (!string.IsNullOrEmpty(dr["sn"].ToString()))
                            {
                                row["sn"] = dr["sn"].ToString();
                            }
                            else
                            {
                                row["sn"] = "";
                            }                           

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
        /// 通过该SN下所有活动告警设置为历史告警
        /// </summary>
        /// <param name="sn">AP的SN号</param>        
        /// <returns>
        /// 返回值 ：
        /// -1 ：数据库尚未打开
        /// -2 ：参数有误
        /// -3 ：SN不存在
        /// -6 : 记录不存在
        /// -4 ：数据库操作失败 
        ///  0 : 更新成功  
        /// </returns>
        public int alarminfo_record_set_2_history(string sn)
        {
            int ret = 0;
            string sql = "";

            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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

            //检查设备是否存在
            if (0 == deviceinfo_record_exist(sn))
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.DEV_NO_EXIST], "DB", LogCategory.I);
                return (int)RC.DEV_NO_EXIST;
            }

            //检查记录是否存在
            if (0 == alarminfo_record_exist(sn))
            {
                Logger.Trace(LogInfoType.EROR, "没有当前记录：" + dicRTV[(int)RC.NO_EXIST], "DB", LogCategory.I);
                //return (int)RC.NO_EXIST;

                return 0;
            }

            //更新deviceinfo表中的信息               
            sql = string.Format("update alarminfo set noticeType = 'ClearAlarm',clearTime = now() where noticeType = 'NewAlarm' and sn = '{0}'", sn);

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


            return ret;
        }
                
        /// <summary>
        /// 通过查询条件query获取alarminfo表中的各条记录
        /// </summary>
        /// <param name="lst">返回的记录列表</param>
        /// <param name="query">查询条件</param>
        /// <param name="errInfo">错误信息</param>
        /// <returns>
        ///   RC.NO_OPEN   ：数据库尚未打开
        ///   RC.OP_FAIL   ：数据库操作失败 
        ///   DEV_NO_EXIST ：设备不存在
        ///   RC.SUCCESS   ：成功 
        /// </returns>
        public int alarminfo_record_entity_get(ref List<strAlarm> lst, strAlarmQuery query, ref string errInfo)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return (int)RC.NO_OPEN;
            }

            //public string timeStart;    /* 
            //                             * ""表示不过滤，开始时间
            //                             * noticeType="",表示NewAlarm和ClearAlarm的开始时间
            //                             * noticeType="NewAlarm",表示NewAlarm的开始时间
            //                             * noticeType="ClearAlarm",表示ClearAlarm的开始时间                                     
            //                             */ 

            //public string timeEnded;    /*
            //                             * ""表示不过滤，结束时间  
            //                             * noticeType="",表示NewAlarm和ClearAlarm的结束时间
            //                             * noticeType="NewAlarm",表示NewAlarm的结束时间
            //                             * noticeType="ClearAlarm",表示ClearAlarm的结束时间
            //                             */ 

            //public string vendor;       // ""表示不过滤，或过滤字符串
            //public string level;        // ""表示不过滤，或过滤字符串
            //public string noticeType;   // ""表示不过滤，"NewAlarm"或者"ClearAlarm"

            //public string cause;        // ""表示不过滤，或过滤字符串
            //public string flag;         // ""表示不过滤，或过滤字符串
            //public string des;          // ""表示不过滤，或过滤字符串
            //public string addDes;       // ""表示不过滤，或过滤字符串
            //public string addInfo;      // ""表示不过滤，或过滤字符串
            //public string sn;           // ""表示不过滤，或过滤字符串

            string sql = "";
            string sqlSub = "";

            #region 时间校验

            if (string.IsNullOrEmpty(query.timeStart))
            {
                query.timeStart = "1970-01-01 12:34:56";  //无指定时默认开始时间
            }

            if (string.IsNullOrEmpty(query.timeEnded))
            {
                query.timeEnded = "2970-01-01 12:34:56";  //无指定时默认结束时间
            }

            try
            {
                DateTime.Parse(query.timeStart);
                DateTime.Parse(query.timeEnded);
            }
            catch (Exception e)
            {
                errInfo = e.Message;
                errInfo += string.Format("timeStart = {0},timeEnded = {1},时间格式有误.", query.timeStart, query.timeEnded);
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.TIME_ST_EN_ERR], "DB", LogCategory.I);
                return (int)RC.TIME_ST_EN_ERR;
            }

            //if (string.Compare(query.timeStart, query.timeEnded) > 0)
            if (DateTime.Compare(Convert.ToDateTime(query.timeStart), Convert.ToDateTime(query.timeEnded))  > 0 )            
            {
                errInfo = string.Format("timeStart = {0} > timeEnded = {1},有误.", query.timeStart, query.timeEnded);
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.TIME_ST_EN_ERR], "DB", LogCategory.I);
                return (int)RC.TIME_ST_EN_ERR;
            }

            #endregion

            #region 构造SQL语句

            if (!string.IsNullOrEmpty(query.vendor))
            {
                sqlSub += string.Format("vendor like '%%{0}%%' and ", query.vendor);
            }

            if (!string.IsNullOrEmpty(query.level))
            {
                sqlSub += string.Format("level like '%%{0}%%' and ", query.level);
            }            

            if (string.IsNullOrEmpty(query.noticeType))
            {
                //活动和历史的告警                          
                sqlSub += string.Format("(alarmTime>='{0}' and alarmTime<='{1}') and ", query.timeStart, query.timeEnded);
                sqlSub += string.Format("(clearTime>='{0}' and clearTime<='{1}') and ", query.timeStart, query.timeEnded);
            }
            else
            {
                if (query.noticeType == "NewAlarm")
                { 
                    //活动告警
                    sqlSub += string.Format("noticeType = '{0}' and ", query.noticeType);
                    sqlSub += string.Format("(alarmTime>='{0}' and alarmTime<='{1}') and ",query.timeStart, query.timeEnded);
                }
                else if (query.noticeType == "ClearAlarm")
                {
                    //历史告警
                    sqlSub += string.Format("noticeType = '{0}' and ", query.noticeType);
                    sqlSub += string.Format("(clearTime>='{0}' and clearTime<='{1}') and ", query.timeStart, query.timeEnded);
                }
                else
                {
                    errInfo = string.Format("noticeType = {0},格式有误.", query.noticeType);
                    Logger.Trace(LogInfoType.EROR, errInfo, "DB", LogCategory.I);
                    return (int)RC.PAR_FMT_ERR;
                }
            }

            if (!string.IsNullOrEmpty(query.cause))
            {
                sqlSub += string.Format("cause like '%%{0}%%' and ", query.cause);
            }

            if (!string.IsNullOrEmpty(query.flag))
            {
                sqlSub += string.Format("flag like '%%{0}%%' and ", query.flag);
            }

            if (!string.IsNullOrEmpty(query.des))
            {
                sqlSub += string.Format("des like '%%{0}%%' and ", query.des);
            }

            if (!string.IsNullOrEmpty(query.addDes))
            {
                sqlSub += string.Format("addDes like '%%{0}%%' and ", query.addDes);
            }

            if (!string.IsNullOrEmpty(query.addInfo))
            {
                sqlSub += string.Format("addInfo like '%%{0}%%' and ", query.addInfo);
            }

            if (!string.IsNullOrEmpty(query.sn))
            {
                sqlSub += string.Format("sn like '%%{0}%%' and ", query.sn);
            }

            if (sqlSub != "")
            {
                sqlSub = sqlSub.Remove(sqlSub.Length - 4, 4);
            }

            #endregion

            if (sqlSub == "")
            {
                sql = string.Format("select * from alarminfo");
            }
            else
            {
                sql = string.Format("select * from alarminfo where {0} ", sqlSub);
            }

            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            strAlarm str = new strAlarm();

                            #region 获取记录

                            str.id = Convert.ToInt32(dr["id"]);

                            if (!string.IsNullOrEmpty(dr["vendor"].ToString()))
                            {
                                str.vendor = dr["vendor"].ToString();
                            }
                            else
                            {
                                str.vendor = "";
                            }

                            if (!string.IsNullOrEmpty(dr["level"].ToString()))
                            {
                                str.level = dr["level"].ToString();
                            }
                            else
                            {
                                str.level = "";
                            }

                            if (!string.IsNullOrEmpty(dr["alarmTime"].ToString()))
                            {
                                str.alarmTime = dr["alarmTime"].ToString();
                            }
                            else
                            {
                                str.alarmTime = "";
                            }

                            if (!string.IsNullOrEmpty(dr["clearTime"].ToString()))
                            {
                                str.clearTime = dr["clearTime"].ToString();
                            }
                            else
                            {
                                str.clearTime = "";
                            }

                            if (!string.IsNullOrEmpty(dr["noticeType"].ToString()))
                            {
                                str.noticeType = dr["noticeType"].ToString();
                            }
                            else
                            {
                                str.noticeType = "";
                            }

                            if (!string.IsNullOrEmpty(dr["cause"].ToString()))
                            {
                                str.cause = dr["cause"].ToString();
                            }
                            else
                            {
                                str.cause = "";
                            }

                            if (!string.IsNullOrEmpty(dr["flag"].ToString()))
                            {
                                str.flag = dr["flag"].ToString();
                            }
                            else
                            {
                                str.flag = "";
                            }

                            if (!string.IsNullOrEmpty(dr["des"].ToString()))
                            {
                                str.des = dr["des"].ToString();
                            }
                            else
                            {
                                str.des = "";
                            }

                            if (!string.IsNullOrEmpty(dr["addDes"].ToString()))
                            {
                                str.addDes = dr["addDes"].ToString();
                            }
                            else
                            {
                                str.addDes = "";
                            }

                            if (!string.IsNullOrEmpty(dr["addInfo"].ToString()))
                            {
                                str.addInfo = dr["addInfo"].ToString();
                            }
                            else
                            {
                                str.addInfo = "";
                            }

                            if (!string.IsNullOrEmpty(dr["res1"].ToString()))
                            {
                                str.res1 = dr["res1"].ToString();
                            }
                            else
                            {
                                str.res1 = "";
                            }

                            if (!string.IsNullOrEmpty(dr["res2"].ToString()))
                            {
                                str.res2 = dr["res2"].ToString();
                            }
                            else
                            {
                                str.res2 = "";
                            }

                            if (!string.IsNullOrEmpty(dr["sn"].ToString()))
                            {
                                str.sn = dr["sn"].ToString();
                            }
                            else
                            {
                                str.sn = "";
                            }

                            #endregion

                            lst.Add(str);
                        }
                        dr.Close();
                    }
                }
            }
            catch (Exception e)
            {
                errInfo = e.Message + e.StackTrace;
                Logger.Trace(LogInfoType.EROR, e.Message + e.StackTrace, "DB", LogCategory.I);
                dicRTV[(int)RC.OP_FAIL] = string.Format("数据库操作失败:{0}", e.Message + e.StackTrace); 
                return (int)RC.OP_FAIL;
            }

            return (int)RC.SUCCESS;
        }

        /// <summary>
        /// 通过查询条件query获取alarminfo表中的各条记录,并保存成csv文件
        /// </summary>
        /// <param name="filePath">保存文件的绝对路径</param>
        /// <param name="query">查询条件</param>
        /// <param name="errInfo">错误信息</param>
        /// <returns>
        ///   RC.NO_OPEN   ：数据库尚未打开
        ///   RC.OP_FAIL   ：数据库操作失败 
        ///   DEV_NO_EXIST ：设备不存在
        ///   RC.SUCCESS   ：成功 
        /// </returns>
        public int alarminfo_record_entity_get(string filePath, strAlarmQuery query, ref string errInfo)
        {
            int ret = -1;

            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return (int)RC.NO_OPEN;
            }

            string DirectoryName = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(DirectoryName))
            {
                errInfo = string.Format("路径={0},不存在.", DirectoryName);
                return -40;
            }

            string extension = System.IO.Path.GetExtension(filePath);
            if (extension != ".csv")
            {
                errInfo = string.Format("{0}:", "后缀名不为csv.", extension);
                Logger.Trace(LogInfoType.EROR, errInfo, "Main", LogCategory.I);
                return -42;

            }

            List<strAlarm> lst = new List<strAlarm>();
            ret = alarminfo_record_entity_get(ref lst, query, ref errInfo);
            if (ret != 0)
            {
                return ret;
            }

            ret = alarm_record_generate_csv_file(filePath, lst, ref errInfo);

            return ret;
        }

        /// <summary>
        /// 获取sn对应的活动告警个数
        /// </summary>
        /// <param name="sn">AP的SN号</param>
        /// <returns> 
        ///   -1  ：数据库尚未打开
        ///   -2  ：参数有误
        ///   -4  ：数据库操作失败 
        ///   >=0 : 返回的个数
        /// </returns>
        public int alarminfo_record_count_get(string sn)
        {
            UInt32 cnt = 0;

            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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

            string sql = string.Format("select count(*) from alarminfo where noticeType = 'NewAlarm' and sn = '{0}'", sn);
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

            return (int)cnt;
        }

        #endregion

        #region 16 - performanceInfo操作

        /// <summary>
        /// 清空所有的记录
        /// </summary>    
        /// <returns>
        ///   RC.NO_OPEN         ：数据库尚未打开
        ///   RC.OP_FAIL         ：数据库操作失败 
        ///   RC.SUCCESS         ：成功
        /// </returns>
        private int performanceInfo_record_clear()
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return (int)RC.NO_OPEN;
            }

            string sql = string.Format("delete from performanceInfo");

            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    if (cmd.ExecuteNonQuery() < 0)
                    {
                        Logger.Trace(LogInfoType.EROR, sql, "DB", LogCategory.I);       
                        return (int)RC.OP_FAIL;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Trace(LogInfoType.EROR, e.Message + e.StackTrace, "DB", LogCategory.I);
                dicRTV[(int)RC.OP_FAIL] = string.Format("数据库操作失败:{0}", e.Message + e.StackTrace);
                return (int)RC.OP_FAIL;
            }

            return (int)RC.SUCCESS;
        }

        /// <summary>
        /// 判断对应sn的记录是否存在alarminfo表中
        /// </summary>
        /// <param name="sn">AP的SN号</param>
        /// <returns> 
        ///   -1 ：数据库尚未打开
        ///   -2 ：参数有误
        ///   -4 ：数据库操作失败 
        ///    0 : 不存在
        ///    1 ：存在
        /// </returns>
        private int performanceInfo_record_exist(string sn)
        {
            UInt32 cnt = 0;

            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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

            string sql = string.Format("select count(*) from performanceInfo where sn = '{0}'", sn);
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
        /// 生产performance相关的CSV文件
        /// </summary>
        /// <param name="fileFullPath"></param>
        /// <param name="PI"></param>
        /// <param name="errInfo"></param>
        /// <returns></returns>
        private int performance_record_generate_csv_file(string fileFullPath,strPerformanceInfo PI, ref string errInfo)
        {
            if (string.IsNullOrEmpty(fileFullPath))
            {
                errInfo = string.Format("fileFullPath is NULL");
                Logger.Trace(LogInfoType.EROR, errInfo, "DB", LogCategory.I);
                return -1;
            }
           
            if (PI.lst == null || PI.lst.Count == 0)
            {
                errInfo = string.Format("记录列表为空.");
                Logger.Trace(LogInfoType.EROR, errInfo, "DB", LogCategory.I);
                return -1;
            }


            //public int RRC_SuccConnEstab;      //RRC建立成功次数
            //public int RRC_AttConnEstab;       //RRC建立请求次数 
            //public double RRC_EstabRate;       //RRC建立成功率

            //public int ERAB_NbrSuccEstab;       //E-RAB建立成功次数
            //public int ERAB_NbrAttEstab;        //E-RAB建立请求次数
            //public double ERAB_EstabRate;       //E-RAB建立成功率

            //public int ERAB_NbrSuccEstab_1;     //QCI=1的E-RAB建立成功次数
            //public int ERAB_NbrAttEstab_1;      //QCI=1的E-RAB建立请求次数
            //public double ERAB_Estab_1Rate;     //QCI=1的E-RAB建立成功率

            //public int RRC_ConnMax;             //当前UE的接入数

            //public int HO_SuccOutInterEnbS1;    //LTE切换成功次数
            //public int HO_AttOutInterEnbS1;     //LTE切换请求次数
            //public double HO_EnbS1Rate;         //LTE切换成功率

            //public int RRC_ConnReleaseCsfb;     //CSFB次数

            //public int PDCP_UpOctUl;            //UL业务量（MB）
            //public int PDCP_UpOctDl;            //DL业务量（MB）

            //public int HO_SuccOutInterEnbS1_1;   //VoLTE切换成功次数
            //public int HO_AttOutInterEnbS1_1;    //VoLTE切换请求次数
            //public double HO_EnbS1_1Rate;        //VoLTE切换成功率

            //public int HO_SuccOutInterFreq;      //ESRVCC切换成功次数
            //public int HO_AttOutExecInterFreq;   //ESRVCC切换请求次数	
            //public double HO_InterFreqRate;      //ESRVCC切换成功率

            //public string timeStart;             //起始时间
            //public string timeEnded;             //结束时间
            //public string sn;                    //SN号      
           

            try
            {
                if (File.Exists(fileFullPath))
                {
                    File.Delete(fileFullPath);
                }

                byte[] data = null;
                FileStream fs = new FileStream(fileFullPath, FileMode.Create);

                string title = "";
                title += string.Format("RRC建立成功次数,RRC建立请求次数,RRC建立成功率,");
                title += string.Format("E-RAB建立成功次数,E-RAB建立请求次数,E-RAB建立成功率,");
                title += string.Format("QCI=1的E-RAB建立成功次数,QCI=1的E-RAB建立请求次数,QCI=1的E-RAB建立成功率,");
                title += string.Format("UE的接入数,");
                title += string.Format("LTE切换成功次数,LTE切换请求次数,LTE切换成功率,");
                title += string.Format("CSFB次数,");
                title += string.Format("UL业务量（MB）,DL业务量（MB）,");
                title += string.Format("VoLTE切换成功次数,VoLTE切换请求次数,VoLTE切换成功率,");
                title += string.Format("ESRVCC切换成功次数,ESRVCC切换请求次数,ESRVCC切换成功率,");
                title += string.Format("起始时间,结束时间,SN号\n");

                data = System.Text.Encoding.Default.GetBytes(title);
                fs.Write(data, 0, data.Length);
          
                string sqlSub = "";

                #region 构造字符串

                //(1)
                sqlSub += string.Format("{0},{1},{2},", PI.stat.RRC_SuccConnEstab, PI.stat.RRC_AttConnEstab,PI.stat.RRC_EstabRate); 

                //(2)
                sqlSub += string.Format("{0},{1},{2},", PI.stat.ERAB_NbrSuccEstab, PI.stat.ERAB_NbrAttEstab, PI.stat.ERAB_EstabRate);

                //(3)
                sqlSub += string.Format("{0},{1},{2},", PI.stat.ERAB_NbrSuccEstab_1, PI.stat.ERAB_NbrAttEstab_1, PI.stat.ERAB_Estab_1Rate);

                //(4)
                sqlSub += string.Format("{0},", PI.stat.RRC_ConnMax);

                //(5)
                sqlSub += string.Format("{0},{1},{2},", PI.stat.HO_SuccOutInterEnbS1, PI.stat.HO_AttOutInterEnbS1, PI.stat.HO_EnbS1Rate);        

                //(6)
                sqlSub += string.Format("{0},", PI.stat.RRC_ConnReleaseCsfb);
              
                //(7)
                sqlSub += string.Format("{0},{1},", PI.stat.PDCP_UpOctUl, PI.stat.PDCP_UpOctDl);

                //(8)
                sqlSub += string.Format("{0},{1},{2},", PI.stat.HO_SuccOutInterEnbS1_1, PI.stat.HO_AttOutInterEnbS1_1, PI.stat.HO_EnbS1_1Rate);

                //(9)
                sqlSub += string.Format("{0},{1},{2},", PI.stat.HO_SuccOutInterFreq, PI.stat.HO_AttOutExecInterFreq, PI.stat.HO_InterFreqRate);

                //(10)
                sqlSub += string.Format("{0},{1},{2}", PI.stat.timeStart, PI.stat.timeEnded, PI.stat.sn);

                #endregion

                data = System.Text.Encoding.Default.GetBytes(sqlSub + "\n");
                fs.Write(data, 0, data.Length);
             

                //清空缓冲区、关闭流
                fs.Flush();
                fs.Close();
                fs.Dispose();
                fs = null;
            }
            catch (Exception e)
            {
                errInfo = e.Message + e.StackTrace;
                Logger.Trace(LogInfoType.EROR, e.Message + e.StackTrace, "Main", LogCategory.I);
                return -1;
            }

            return 0;
        }       



        /// <summary>
        /// 插入记录
        /// </summary>
        /// <param name="sn">AP的SN号</param>
        /// <param name="pi">各个参数</param>
        /// <returns>
        /// 返回值 ：
        /// -1 ：数据库尚未打开
        /// -2 ：参数有误
        /// -3 ：SN不存在
        /// -6 : 记录不存在
        /// -4 ：数据库操作失败 
        ///  0 : 更新成功  
        /// </returns>
        public int performanceInfo_record_insert(string sn, strPerformance pi)
        {
            int ret = 0;
            string sql = "";
            string sqlSub = "";

            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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

            //检查设备是否存在
            if (0 == deviceinfo_record_exist(sn))
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.DEV_NO_EXIST], "DB", LogCategory.I);
                return (int)RC.DEV_NO_EXIST;
            }

            ///////////////////////

            //public int RRC_SuccConnEstab;      //RRC建立成功次数
            //public int RRC_AttConnEstab;       //RRC建立请求次数 

            //public int ERAB_NbrSuccEstab;       //E-RAB建立成功次数
            //public int ERAB_NbrAttEstab;        //E-RAB建立请求次数

            //public int ERAB_NbrSuccEstab_1;     //QCI=1的E-RAB建立成功次数
            //public int ERAB_NbrAttEstab_1;      //QCI=1的E-RAB建立请求次数

            //public int RRC_ConnMax;             //当前UE的接入数

            //public int HO_SuccOutInterEnbS1;    //LTE切换成功次数
            //public int HO_AttOutInterEnbS1;     //LTE切换请求次数

            //public int RRC_ConnReleaseCsfb;     //CSFB次数

            //public int PDCP_UpOctUl;            //UL业务量（MB）
            //public int PDCP_UpOctDl;            //DL业务量（MB）

            //public int HO_SuccOutInterEnbS1_1;   //VoLTE切换成功次数
            //public int HO_AttOutInterEnbS1_1;    //VoLTE切换请求次数

            //public int HO_SuccOutInterFreq;      //ESRVCC切换成功次数
            //public int HO_AttOutExecInterFreq;   //ESRVCC切换请求次数	

            //public int res1;                     //保留字段1
            //public int res2;                     //保留字段2

            #region 时间校验

            if (string.IsNullOrEmpty(pi.timeStart))
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.TIME_FMT_ERR], "DB", LogCategory.I);
                return (int)RC.TIME_FMT_ERR;
            }

            if (string.IsNullOrEmpty(pi.timeEnded))
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.TIME_FMT_ERR], "DB", LogCategory.I);
                return (int)RC.TIME_FMT_ERR;
            }

            try
            {
                DateTime.Parse(pi.timeStart);
                DateTime.Parse(pi.timeEnded);
            }
            catch (Exception e)
            {
                //errInfo = e.Message;
                //errInfo += string.Format("timeStart = {0},timeEnded = {1},时间格式有误.", pi.timeStart, pi.timeEnded);
                Logger.Trace(LogInfoType.EROR, e.Message + dicRTV[(int)RC.TIME_ST_EN_ERR], "DB", LogCategory.I);
                return (int)RC.TIME_ST_EN_ERR;
            }

            //if (string.Compare(pi.timeStart, pi.timeEnded) > 0)
            if (DateTime.Compare(Convert.ToDateTime(pi.timeStart), Convert.ToDateTime(pi.timeEnded)) > 0)
            {
                //errInfo = string.Format("timeStart = {0} > timeEnded = {1},有误.", pi.timeStart, pi.timeEnded);
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.TIME_ST_EN_ERR], "DB", LogCategory.I);
                return (int)RC.TIME_ST_EN_ERR;
            }

            #endregion

            #region 构造SQL语句

            //(1)
            if (pi.RRC_SuccConnEstab >= 0)
            {
                sqlSub += string.Format("{0},", pi.RRC_SuccConnEstab);
            }
            else
            {
                sqlSub += string.Format("0,");
            }

            //(2)
            if (pi.RRC_AttConnEstab >= 0)
            {
                sqlSub += string.Format("{0},", pi.RRC_AttConnEstab);
            }
            else
            {
                sqlSub += string.Format("0,");
            }

            //(3)
            if (pi.ERAB_NbrSuccEstab >= 0)
            {
                sqlSub += string.Format("{0},", pi.ERAB_NbrSuccEstab);
            }
            else
            {
                sqlSub += string.Format("0,");
            }

            //(4)
            if (pi.ERAB_NbrAttEstab >= 0)
            {
                sqlSub += string.Format("{0},", pi.ERAB_NbrAttEstab);
            }
            else
            {
                sqlSub += string.Format("0,");
            }

            //(5)
            if (pi.ERAB_NbrSuccEstab_1 >= 0)
            {
                sqlSub += string.Format("{0},", pi.ERAB_NbrSuccEstab_1);
            }
            else
            {
                sqlSub += string.Format("0,");
            }

            //(6)
            if (pi.ERAB_NbrAttEstab_1 >= 0)
            {
                sqlSub += string.Format("{0},", pi.ERAB_NbrAttEstab_1);
            }
            else
            {
                sqlSub += string.Format("0,");
            }

            //(7)
            if (pi.RRC_ConnMax >= 0)
            {
                sqlSub += string.Format("{0},", pi.RRC_ConnMax);
            }
            else
            {
                sqlSub += string.Format("0,");
            }

            //(8)
            if (pi.HO_SuccOutInterEnbS1 >= 0)
            {
                sqlSub += string.Format("{0},", pi.HO_SuccOutInterEnbS1);
            }
            else
            {
                sqlSub += string.Format("0,");
            }

            //(9)
            if (pi.HO_AttOutInterEnbS1 >= 0)
            {
                sqlSub += string.Format("{0},", pi.HO_AttOutInterEnbS1);
            }
            else
            {
                sqlSub += string.Format("0,");
            }

            //(10)
            if (pi.RRC_ConnReleaseCsfb >= 0)
            {
                sqlSub += string.Format("{0},", pi.RRC_ConnReleaseCsfb);
            }
            else
            {
                sqlSub += string.Format("0,");
            }

            //(11)
            if (pi.PDCP_UpOctUl >= 0)
            {
                sqlSub += string.Format("{0},", pi.PDCP_UpOctUl);
            }
            else
            {
                sqlSub += string.Format("0,");
            }

            //(12)
            if (pi.PDCP_UpOctDl >= 0)
            {
                sqlSub += string.Format("{0},", pi.PDCP_UpOctDl);
            }
            else
            {
                sqlSub += string.Format("0,");
            }

            //(13)
            if (pi.HO_SuccOutInterEnbS1_1 >= 0)
            {
                sqlSub += string.Format("{0},", pi.HO_SuccOutInterEnbS1_1);
            }
            else
            {
                sqlSub += string.Format("0,");
            }

            //(14)
            if (pi.HO_AttOutInterEnbS1_1 >= 0)
            {
                sqlSub += string.Format("{0},", pi.HO_AttOutInterEnbS1_1);
            }
            else
            {
                sqlSub += string.Format("0,");
            }

            //(15)
            if (pi.HO_SuccOutInterFreq >= 0)
            {
                sqlSub += string.Format("{0},", pi.HO_SuccOutInterFreq);
            }
            else
            {
                sqlSub += string.Format("0,");
            }

            //(16)
            if (pi.HO_AttOutExecInterFreq >= 0)
            {
                sqlSub += string.Format("{0},", pi.HO_AttOutExecInterFreq);
            }
            else
            {
                sqlSub += string.Format("0,");
            }

            //(17)
            if (pi.res1 >= 0)
            {
                sqlSub += string.Format("{0},", pi.res1);
            }
            else
            {
                sqlSub += string.Format("0,");
            }

            //(18)
            if (pi.res2 >= 0)
            {
                sqlSub += string.Format("{0},", pi.res2);
            }
            else
            {
                sqlSub += string.Format("0,");
            }

            sqlSub += string.Format("'{0}','{1}'",pi.timeStart,pi.timeEnded);

            sqlSub = string.Format("NULL,{0},'{1}'", sqlSub, sn);

            #endregion

            //更新deviceinfo表中的信息               
            sql = string.Format("insert into performanceInfo VALUES({0})", sqlSub);

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

            return ret;
        }

        /// <summary>
        /// 在alarminfo表中删除指定的SN
        /// </summary>
        /// <param name="sn">AP的SN号</param>
        /// <param name="flag">告警标识：
        /// ""时表示删除SN下所有的告警信息
        /// 不为""时表示删除SN下该flag
        /// </param>
        /// <returns>
        /// -1 ：数据库尚未打开
        /// -2 ：参数有误
        /// -6 ：记录不存在
        /// -4 ：数据库操作失败 
        ///-28 ：设备不存在
        ///  0 : 删除成功 
        /// </returns>
        public int performanceInfo_record_delete(string sn)
        {
            int ret = 0;
            string sql = "";

            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
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
            
            //检查设备是否存在
            if (0 == deviceinfo_record_exist(sn))
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.DEV_NO_EXIST], "DB", LogCategory.I);
                return (int)RC.DEV_NO_EXIST;
            }

            sql = string.Format("delete from performanceInfo where sn = '{0}'", sn);
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

            return ret;
        }
       
        /// <summary>
        /// 通过sn,起始时间，结束时间获取performanceInfo表中的各条记录
        /// </summary>
        /// <param name="PI">返回的各条记录和统计信息</param>
        /// <param name="sn">查询条件：SN</param>
        /// <param name="timeStart">查询条件：起始时间，""表示不过滤该条件</param>
        /// <param name="timeEnded">查询条件：结束时间，""表示不过滤该条件</param>
        /// <param name="errInfo">错误信息</param>
        /// <returns>
        /// 0   ： 成功
        /// 非0 ： 失败
        /// </returns>
        public int performanceInfo_record_entity_get(ref strPerformanceInfo PI,string sn,string timeStart,string timeEnded,ref string errInfo)
        {
            if (false == myDbConnFlag)
            {
                errInfo = get_rtv_str((int)RC.NO_OPEN);
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return (int)RC.NO_OPEN;
            }           

            string sql = "";

            if (string.IsNullOrEmpty(sn))
            {
                errInfo = string.Format("SN为空.");
                Logger.Trace(LogInfoType.EROR,errInfo, "DB", LogCategory.I);
                return -1;
            }

            if(0 == deviceinfo_record_exist(sn))
            {
                errInfo = string.Format("sn = {0},不存在该设备.",sn);
                Logger.Trace(LogInfoType.EROR,errInfo, "DB", LogCategory.I);
                return -1;
            }


            #region 时间校验

            if (string.IsNullOrEmpty(timeStart))
            {
                timeStart = "1970-01-01 12:34:56";  //无指定时默认开始时间
            }

            if (string.IsNullOrEmpty(timeEnded))
            {
                timeEnded = "2970-01-01 12:34:56";  //无指定时默认结束时间
            }

            try
            {
                DateTime.Parse(timeStart);
                DateTime.Parse(timeEnded);
            }
            catch (Exception e)
            {
                errInfo = e.Message;
                errInfo += string.Format("timeStart = {0},timeEnded = {1},时间格式有误.", timeStart, timeEnded);
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.TIME_ST_EN_ERR], "DB", LogCategory.I);
                return (int)RC.TIME_ST_EN_ERR;
            }

            //if (string.Compare(timeStart, timeEnded) > 0)
            if (DateTime.Compare(Convert.ToDateTime(timeStart), Convert.ToDateTime(timeEnded)) > 0)
            {
                errInfo = string.Format("timeStart = {0} > timeEnded = {1},有误.", timeStart, timeEnded);
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.TIME_ST_EN_ERR], "DB", LogCategory.I);
                return (int)RC.TIME_ST_EN_ERR;
            }

            #endregion

            sql = string.Format("select * from performanceInfo where timeStart >='{0}' and timeEnded <= '{1}' and sn = '{2}'", timeStart, timeEnded, sn);

            PI = new strPerformanceInfo();
            PI.lst = new List<strPerformance>();
            PI.stat = new strPerforStat();
          
            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {

                        while (dr.Read())
                        {
                            strPerformance str = new strPerformance();

                            #region 获取记录

                            //(1)
                            if (!string.IsNullOrEmpty(dr["RRC_SuccConnEstab"].ToString()))
                            {
                                str.RRC_SuccConnEstab = int.Parse(dr["RRC_SuccConnEstab"].ToString());
                            }
                            else
                            {
                                str.RRC_SuccConnEstab = 0;
                            }
                            PI.stat.RRC_SuccConnEstab += str.RRC_SuccConnEstab;


                            //(2)
                            if (!string.IsNullOrEmpty(dr["RRC_AttConnEstab"].ToString()))
                            {
                                str.RRC_AttConnEstab = int.Parse(dr["RRC_AttConnEstab"].ToString());
                            }
                            else
                            {
                                str.RRC_AttConnEstab = 0;
                            }
                            PI.stat.RRC_AttConnEstab += str.RRC_AttConnEstab;


                            //(3)
                            if (!string.IsNullOrEmpty(dr["ERAB_NbrSuccEstab"].ToString()))
                            {
                                str.ERAB_NbrSuccEstab = int.Parse(dr["ERAB_NbrSuccEstab"].ToString());
                            }
                            else
                            {
                                str.ERAB_NbrSuccEstab = 0;
                            }
                            PI.stat.ERAB_NbrSuccEstab += str.ERAB_NbrSuccEstab;


                            //(4)
                            if (!string.IsNullOrEmpty(dr["ERAB_NbrAttEstab"].ToString()))
                            {
                                str.ERAB_NbrAttEstab = int.Parse(dr["ERAB_NbrAttEstab"].ToString());
                            }
                            else
                            {
                                str.ERAB_NbrAttEstab = 0;
                            }
                            PI.stat.ERAB_NbrAttEstab += str.ERAB_NbrAttEstab;


                            //(5)
                            if (!string.IsNullOrEmpty(dr["ERAB_NbrSuccEstab_1"].ToString()))
                            {
                                str.ERAB_NbrSuccEstab_1 = int.Parse(dr["ERAB_NbrSuccEstab_1"].ToString());
                            }
                            else
                            {
                                str.ERAB_NbrSuccEstab_1 = 0;
                            }
                            PI.stat.ERAB_NbrSuccEstab_1 += str.ERAB_NbrSuccEstab_1;


                            //(6)
                            if (!string.IsNullOrEmpty(dr["ERAB_NbrAttEstab_1"].ToString()))
                            {
                                str.ERAB_NbrAttEstab_1 = int.Parse(dr["ERAB_NbrAttEstab_1"].ToString());
                            }
                            else
                            {
                                str.ERAB_NbrAttEstab_1 = 0;
                            }
                            PI.stat.ERAB_NbrAttEstab_1 += str.ERAB_NbrAttEstab_1;


                            //(7)
                            if (!string.IsNullOrEmpty(dr["RRC_ConnMax"].ToString()))
                            {
                                str.RRC_ConnMax = int.Parse(dr["RRC_ConnMax"].ToString());
                            }
                            else
                            {
                                str.RRC_ConnMax = 0;
                            }
                            PI.stat.RRC_ConnMax += str.RRC_ConnMax;


                            //(8)
                            if (!string.IsNullOrEmpty(dr["HO_SuccOutInterEnbS1"].ToString()))
                            {
                                str.HO_SuccOutInterEnbS1 = int.Parse(dr["HO_SuccOutInterEnbS1"].ToString());
                            }
                            else
                            {
                                str.HO_SuccOutInterEnbS1 = 0;
                            }
                            PI.stat.HO_SuccOutInterEnbS1 += str.HO_SuccOutInterEnbS1;


                            //(9)
                            if (!string.IsNullOrEmpty(dr["HO_AttOutInterEnbS1"].ToString()))
                            {
                                str.HO_AttOutInterEnbS1 = int.Parse(dr["HO_AttOutInterEnbS1"].ToString());
                            }
                            else
                            {
                                str.HO_AttOutInterEnbS1 = 0;
                            }
                            PI.stat.HO_AttOutInterEnbS1 += str.HO_AttOutInterEnbS1;


                            //(10)
                            if (!string.IsNullOrEmpty(dr["RRC_ConnReleaseCsfb"].ToString()))
                            {
                                str.RRC_ConnReleaseCsfb = int.Parse(dr["RRC_ConnReleaseCsfb"].ToString());
                            }
                            else
                            {
                                str.RRC_ConnReleaseCsfb = 0;
                            }
                            PI.stat.RRC_ConnReleaseCsfb += str.RRC_ConnReleaseCsfb;


                            //(11)
                            if (!string.IsNullOrEmpty(dr["PDCP_UpOctUl"].ToString()))
                            {
                                str.PDCP_UpOctUl = int.Parse(dr["PDCP_UpOctUl"].ToString());
                            }
                            else
                            {
                                str.PDCP_UpOctUl = 0;
                            }
                            PI.stat.PDCP_UpOctUl += str.PDCP_UpOctUl;


                            //(12)
                            if (!string.IsNullOrEmpty(dr["PDCP_UpOctDl"].ToString()))
                            {
                                str.PDCP_UpOctDl = int.Parse(dr["PDCP_UpOctDl"].ToString());
                            }
                            else
                            {
                                str.PDCP_UpOctDl = 0;
                            }
                            PI.stat.PDCP_UpOctDl += str.PDCP_UpOctDl;


                            //(13)
                            if (!string.IsNullOrEmpty(dr["HO_SuccOutInterEnbS1_1"].ToString()))
                            {
                                str.HO_SuccOutInterEnbS1_1 = int.Parse(dr["HO_SuccOutInterEnbS1_1"].ToString());
                            }
                            else
                            {
                                str.HO_SuccOutInterEnbS1_1 = 0;
                            }
                            PI.stat.HO_SuccOutInterEnbS1_1 += str.HO_SuccOutInterEnbS1_1;


                            //(14)
                            if (!string.IsNullOrEmpty(dr["HO_AttOutInterEnbS1_1"].ToString()))
                            {
                                str.HO_AttOutInterEnbS1_1 = int.Parse(dr["HO_AttOutInterEnbS1_1"].ToString());
                            }
                            else
                            {
                                str.HO_AttOutInterEnbS1_1 = 0;
                            }
                            PI.stat.HO_AttOutInterEnbS1_1 += str.HO_AttOutInterEnbS1_1;


                            //(15)
                            if (!string.IsNullOrEmpty(dr["HO_SuccOutInterFreq"].ToString()))
                            {
                                str.HO_SuccOutInterFreq = int.Parse(dr["HO_SuccOutInterFreq"].ToString());
                            }
                            else
                            {
                                str.HO_SuccOutInterFreq = 0;
                            }
                            PI.stat.HO_SuccOutInterFreq += str.HO_SuccOutInterFreq;


                            //(16)
                            if (!string.IsNullOrEmpty(dr["HO_AttOutExecInterFreq"].ToString()))
                            {
                                str.HO_AttOutExecInterFreq = int.Parse(dr["HO_AttOutExecInterFreq"].ToString());
                            }
                            else
                            {
                                str.HO_AttOutExecInterFreq = 0;
                            }
                            PI.stat.HO_AttOutExecInterFreq += str.HO_AttOutExecInterFreq;


                            //(17)
                            if (!string.IsNullOrEmpty(dr["sn"].ToString()))
                            {
                                str.sn = dr["sn"].ToString();
                            }

                            //(18)
                            if (!string.IsNullOrEmpty(dr["timeStart"].ToString()))
                            {
                                str.timeStart = dr["timeStart"].ToString();
                            }


                            //(19)
                            if (!string.IsNullOrEmpty(dr["timeEnded"].ToString()))
                            {
                                str.timeEnded = dr["timeEnded"].ToString();
                            }

                            #endregion

                            PI.lst.Add(str);
                        }
                        dr.Close();

                        PI.stat.RRC_EstabRate = Math.Round((double)PI.stat.RRC_SuccConnEstab / PI.stat.RRC_AttConnEstab, 2);
                        PI.stat.ERAB_EstabRate = Math.Round((double)PI.stat.ERAB_NbrSuccEstab / PI.stat.ERAB_NbrAttEstab, 2);

                        PI.stat.ERAB_Estab_1Rate = Math.Round((double)PI.stat.ERAB_NbrSuccEstab_1 / PI.stat.ERAB_NbrAttEstab_1, 2);
                        PI.stat.HO_EnbS1Rate = Math.Round((double)PI.stat.HO_SuccOutInterEnbS1 / PI.stat.HO_AttOutInterEnbS1, 2);

                        PI.stat.HO_EnbS1_1Rate = Math.Round((double)PI.stat.HO_SuccOutInterEnbS1_1 / PI.stat.HO_AttOutInterEnbS1_1, 2);
                        PI.stat.HO_InterFreqRate = Math.Round((double)PI.stat.HO_SuccOutInterFreq / PI.stat.HO_AttOutExecInterFreq, 2);

                        PI.stat.timeStart = timeStart;
                        PI.stat.timeEnded = timeEnded;
                        PI.stat.sn = sn;
                    }
                }
            }
            catch (Exception e)
            {
                errInfo = e.Message + e.StackTrace;
                Logger.Trace(LogInfoType.EROR, e.Message + e.StackTrace, "DB", LogCategory.I);
                dicRTV[(int)RC.OP_FAIL] = string.Format("数据库操作失败:{0}", e.Message + e.StackTrace);
                return (int)RC.OP_FAIL;
            }

            return (int)RC.SUCCESS;
        }

        /// <summary>
        /// 通过sn,起始时间，结束时间获取performanceInfo表中的各条记录,并保存成csv文件
        /// </summary>
        /// <param name="filePath">保存文件的绝对路径</param>
        /// <param name="sn">查询条件：SN</param>
        /// <param name="timeStart">查询条件：起始时间，""表示不过滤该条件</param>
        /// <param name="timeEnded">查询条件：结束时间，""表示不过滤该条件</param>
        /// <param name="errInfo">错误信息</param>
        /// <returns>
        /// 0   ： 成功
        /// 非0 ： 失败
        /// </returns>
        public int performanceInfo_record_entity_get(string filePath, string sn, string timeStart, string timeEnded, ref string errInfo)
        {
            int rtv = -1;

            string DirectoryName = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(DirectoryName))
            {
                errInfo = string.Format("路径={0},不存在.", DirectoryName);
                return -40;
            }

            string extension = System.IO.Path.GetExtension(filePath);
            if (extension != ".csv")
            {
                errInfo = string.Format("{0}:", "后缀名不为csv.", extension);
                Logger.Trace(LogInfoType.EROR, errInfo, "Main", LogCategory.I);
                return -42;

            }

            strPerformanceInfo PI = new strPerformanceInfo ();
            rtv = performanceInfo_record_entity_get(ref PI, sn, timeStart, timeEnded, ref errInfo);
            if(rtv != 0)
            {
                return rtv;
            }

            rtv = performance_record_generate_csv_file(filePath, PI, ref errInfo);
            return rtv;
        }

        /// <summary>
        /// 通过全路径,起始时间，结束时间获取performanceInfo表中的各条记录
        /// </summary>
        /// <param name="lstPI">返回的信息</param>
        /// <param name="fullPathName">全路径，可以是设备或者域</param>
        /// <param name="timeStart">查询条件：起始时间，""表示不过滤该条件</param>
        /// <param name="timeEnded">查询条件：结束时间，""表示不过滤该条件</param>
        /// <param name="errInfo">错误信息</param>
        /// <returns>
        /// 0   ： 成功
        /// 非0 ： 失败
        /// </returns>
        public int performanceInfo_record_entity_get(ref List<strPerformanceInfo> lstPI, string fullPathName, string timeStart, string timeEnded, ref string errInfo)
        {
            int rtv;
            List<string> lstDevSn = new List<string>();

            if (false == myDbConnFlag)
            {
                errInfo = get_rtv_str((int)RC.NO_OPEN);
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return (int)RC.NO_OPEN;
            }

            if (string.IsNullOrEmpty(fullPathName))
            {
                errInfo = string.Format("fullPathName为空！");
                return -1;
            }

            if (gDicDevFullName.ContainsKey(fullPathName))
            {
                //fullPathName为设备类型，而且匹配上
                lstDevSn.Add(gDicDevFullName[fullPathName].sn);
            }
            else
            {
                //fullPathName为域类型
                List<int> lstDevId = new List<int>();
                rtv = domain_record_device_id_list_get(fullPathName, ref lstDevId, ref lstDevSn);
                if ((int)RC.SUCCESS != rtv)
                {
                    errInfo = get_rtv_str(rtv);
                    return rtv;
                }
            }           

            #region 时间校验

            if (string.IsNullOrEmpty(timeStart))
            {
                timeStart = "1970-01-01 12:34:56";  //无指定时默认开始时间
            }

            if (string.IsNullOrEmpty(timeEnded))
            {
                timeEnded = "2970-01-01 12:34:56";  //无指定时默认结束时间
            }

            try
            {
                DateTime.Parse(timeStart);
                DateTime.Parse(timeEnded);
            }
            catch (Exception e)
            {
                errInfo = e.Message;
                errInfo += string.Format("timeStart = {0},timeEnded = {1},时间格式有误.", timeStart, timeEnded);
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.TIME_ST_EN_ERR], "DB", LogCategory.I);
                return (int)RC.TIME_ST_EN_ERR;
            }

            //if (string.Compare(timeStart, timeEnded) > 0)
            if (DateTime.Compare(Convert.ToDateTime(timeStart), Convert.ToDateTime(timeEnded)) > 0)
            {
                errInfo = string.Format("timeStart = {0} > timeEnded = {1},有误.", timeStart, timeEnded);
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.TIME_ST_EN_ERR], "DB", LogCategory.I);
                return (int)RC.TIME_ST_EN_ERR;
            }

            #endregion

            lstPI = new List<strPerformanceInfo>();
            foreach (string sn in lstDevSn)
            { 
                strPerformanceInfo pi = new strPerformanceInfo ();
                if (0 == performanceInfo_record_entity_get(ref pi, sn, timeStart, timeEnded, ref errInfo))
                {
                    lstPI.Add(pi);
                }
            }

            return 0;
        }

        #endregion

        #region 17 - 设备的批量导入导出

        /// <summary>
        /// 批量导入之前，清空各种相关的表
        /// </summary>
        /// <returns></returns>
        private int BIE_clear_tables_involved()
        {
            int rtv = 0;

            try
            {
                // (1)
                rtv += domain_record_clear();

                // (2)
                rtv += deviceinfo_record_clear();

                // (3)
                rtv += apaction_record_clear();

                // (4)
                rtv += apconninfo_record_clear();

                // (5)
                rtv += aploginfo_record_clear();

                // (6)
                rtv += loginfo_record_clear();

                // (7)
                rtv += alarminfo_record_clear();

                // (8)
                rtv += performanceInfo_record_clear();
                             
            }
            catch (Exception ee)
            {               
                Logger.Trace(LogInfoType.EROR, ee.Message + ee.StackTrace, "Main", LogCategory.I);
                return -1;
            }

            return rtv;
        }

        /// <summary>
        /// 从数据库中获取所有域的信息
        /// </summary>
        /// <param name="lst">域信息</param>
        /// <param name="errInfo">错误信息</param>
        /// <returns></returns>
        private int BIE_get_all_domain(ref List<strBIE_DomainInfo> lst, ref string errInfo)
        {
            int rtv = -1;
            errInfo = "";
            DataTable dt = new DataTable();

            try
            {
                rtv = domain_record_entity_get(ref dt, 0);
                if (rtv != 0)
                {
                    errInfo = get_rtv_str(rtv);                   
                    Logger.Trace(LogInfoType.EROR, errInfo, "Main", LogCategory.I);
                    return -1;
                }

                lst = new List<strBIE_DomainInfo>();
                foreach (DataRow dr in dt.Rows)
                {
                    strBIE_DomainInfo str = new strBIE_DomainInfo();

                    if (string.IsNullOrEmpty(dr["name"].ToString()))
                    {
                        str.name = "";
                    }
                    else
                    {
                        str.name = dr["name"].ToString();
                    }

                    if (string.IsNullOrEmpty(dr["nameFullPath"].ToString()))
                    {
                        str.parentNameFullPath = "";
                    }
                    else
                    {
                        int i = dr["nameFullPath"].ToString().LastIndexOf(".");
                        if (i > 0)
                        {
                            str.parentNameFullPath = dr["nameFullPath"].ToString().Substring(0, i);
                        }
                        else
                        {
                            str.parentNameFullPath = "";
                        }
                    }

                    if (string.IsNullOrEmpty(dr["isStation"].ToString()))
                    {
                        str.isStation = "";
                    }
                    else
                    {
                        str.isStation = dr["isStation"].ToString();
                    }

                    if (str.name == "设备" && str.parentNameFullPath == "")
                    {
                        //去掉根节点
                        continue;
                    }

                    lst.Add(str);
                }
            }
            catch (Exception ee)
            {                
                Logger.Trace(LogInfoType.EROR, ee.Message + ee.StackTrace, "Main", LogCategory.I);
                return -1;
            }

            return rtv;
        }

        /// <summary>
        /// 从数据库中获取所有设备的信息
        /// </summary>
        /// <param name="lst">设备信息</param>
        /// <param name="errInfo">错误信息</param>
        /// <returns></returns>
        private int BIE_get_all_device(ref List<strBIE_DeviceInfo> lst, ref string errInfo)
        {
            errInfo = "";
            string affDomainId = "";
            DataTable dt = new DataTable();

            try
            {
                int rtv = deviceinfo_record_entity_get(ref dt);
                if (rtv != 0)
                {
                    errInfo = get_rtv_str(rtv);                    
                    Logger.Trace(LogInfoType.EROR, errInfo, "Main", LogCategory.I);
                    return -1;
                }

                lst = new List<strBIE_DeviceInfo>();
                foreach (DataRow dr in dt.Rows)
                {
                    strBIE_DeviceInfo str = new strBIE_DeviceInfo();

                    if (string.IsNullOrEmpty(dr["bsName"].ToString()))
                    {
                        str.name = "";
                    }
                    else
                    {
                        str.name = dr["bsName"].ToString();
                    }

                    if (!string.IsNullOrEmpty(dr["affDomainId"].ToString()))
                    {
                        affDomainId = dr["affDomainId"].ToString();

                        rtv = domain_get_nameFullPath_by_id(affDomainId, ref str.parentNameFullPath);
                        if (rtv != 0)
                        {
                            errInfo = get_rtv_str(rtv);
                            continue;
                        }
                    }

                    //public string sn;
                    //public string ipAddr;
                    //public string tac;
                    //public string earfcn;
                    //public string des;

                    if (string.IsNullOrEmpty(dr["sn"].ToString()))
                    {
                        str.sn = "";
                    }
                    else
                    {
                        str.sn = dr["sn"].ToString();
                    }

                    if (string.IsNullOrEmpty(dr["ipAddr"].ToString()))
                    {
                        str.ipAddr = "";
                    }
                    else
                    {
                        str.ipAddr = dr["ipAddr"].ToString();
                    }

                    if (string.IsNullOrEmpty(dr["tac"].ToString()))
                    {
                        str.tac = "";
                    }
                    else
                    {
                        str.tac = dr["tac"].ToString();
                    }

                    if (string.IsNullOrEmpty(dr["earfcn"].ToString()))
                    {
                        str.earfcn = "";
                    }
                    else
                    {
                        str.earfcn = dr["earfcn"].ToString();
                    }

                    if (string.IsNullOrEmpty(dr["aliasName"].ToString()))
                    {
                        str.aliasName = "";
                    }
                    else
                    {
                        str.aliasName = dr["aliasName"].ToString();
                    }

                    if (string.IsNullOrEmpty(dr["des"].ToString()))
                    {
                        str.des = "";
                    }
                    else
                    {
                        str.des = dr["des"].ToString();
                    }

                    lst.Add(str);
                }
            }
            catch (Exception ee)
            {               
                Logger.Trace(LogInfoType.EROR, ee.Message + ee.StackTrace, "Main", LogCategory.I);
                return -1;
            }

            return 0;
        }

        /// <summary>
        /// 从数据库中获取所有的批量导出信息
        /// </summary>
        /// <param name="bie">批量的导出信息</param>
        /// <param name="errInfo">错误信息</param>
        /// <returns></returns>
        private int BIE_read_from_DB(ref strBatchImportExport bie, ref string errInfo, ref string JsonText)
        {
            errInfo = "";
            JsonText = "";

            int rtv = -1;
            string info = "";
            try
            {
                //实例化
                bie = new strBatchImportExport();

                bie.lstDomainInfo = new List<strBIE_DomainInfo>();
                bie.lstDeviceInfo = new List<strBIE_DeviceInfo>();       

                rtv = BIE_get_all_domain(ref bie.lstDomainInfo, ref errInfo);
                if (rtv != 0)
                {
                    return -1;
                }

                info = string.Format("(1/2)BIE_read_from_DB，生成域列表成功.\r\n");
                Logger.Trace(LogInfoType.INFO, info, "Main", LogCategory.I);


                rtv = BIE_get_all_device(ref bie.lstDeviceInfo, ref errInfo);
                if (rtv != 0)
                {
                    return -1;
                }

                info = string.Format("(2/2)BIE_read_from_DB，生成设备列表成功.\r\n");               
                Logger.Trace(LogInfoType.INFO, info, "Main", LogCategory.I);               

                JsonText = JsonConvert.SerializeObject(bie,Newtonsoft.Json.Formatting.Indented);

                #region 格式化输出格式

                //if (JsonText.Contains("\"lstDomainInfo\":["))
                //{
                //    JsonText = JsonText.Replace("\"lstDomainInfo\":[", "\r\n\r\n\"lstDomainInfo\":[\r\n");
                //}

                //if (JsonText.Contains("\"lstDeviceInfo\":["))
                //{
                //    JsonText = JsonText.Replace("\"lstDeviceInfo\":[", "\r\n\r\n\"lstDeviceInfo\":[\r\n");
                //}

                //if (JsonText.Contains("\"lstLTE\":["))
                //{
                //    JsonText = JsonText.Replace("\"lstLTE\":[", "\r\n\r\n\"lstLTE\":[\r\n");
                //}

                //if (JsonText.Contains("\"lstGSM_ZYF\":["))
                //{
                //    JsonText = JsonText.Replace("\"lstGSM_ZYF\":[", "\r\n\r\n\"lstGSM_ZYF\":[\r\n");
                //}

                //if (JsonText.Contains("\"lstCDMA_ZYF\":["))
                //{
                //    JsonText = JsonText.Replace("\"lstCDMA_ZYF\":[", "\r\n\r\n\"lstCDMA_ZYF\":[\r\n");
                //}

                //if (JsonText.Contains("\"lstBwList\":["))
                //{
                //    JsonText = JsonText.Replace("\"lstBwList\":[", "\r\n\"lstBwList\":[\r\n");
                //}

                //if (JsonText.Contains("\"bieSys0\""))
                //{
                //    JsonText = JsonText.Replace("\"bieSys0\"", "\r\n\"bieSys0\"");
                //}

                //if (JsonText.Contains("\"bieSys1\""))
                //{
                //    JsonText = JsonText.Replace("\"bieSys1\"", "\r\n\"bieSys1\"");
                //}

                //if (JsonText.Contains("},{"))
                //{
                //    JsonText = JsonText.Replace("},{", "},\r\n{");
                //}

                #endregion
            }
            catch (Exception ee)
            {
                errInfo = ee.Message + ee.StackTrace;
                Logger.Trace(LogInfoType.EROR, ee.Message + ee.StackTrace, "Main", LogCategory.I);
                return -1;
            }


            return 0;
        }

        /// <summary>
        /// 将批量导入信息写入数据库中
        /// </summary>
        /// <param name="bie">批量的导入信息</param>
        /// <param name="errInfo">错误信息</param>
        /// <returns></returns>
        private int BIE_write_2_DB(strBatchImportExport bie, ref string errInfo)
        {            
            int rtv = -1;
            int id = -1;
            int successCnt = 0;

            string outInfo = "";

            try
            {
                #region 清空相关的表

                rtv = BIE_clear_tables_involved();
                if (rtv != 0)
                {
                    string info = string.Format("BIE_write_2_DB,BIE_clear_tables_involved{0}", get_rtv_str(rtv));                    
                    Logger.Trace(LogInfoType.EROR, info, "Main", LogCategory.I);
                }

                outInfo = string.Format("(1/3)BIE_write_2_DB，清空相关表成功.\r\n");               
                Logger.Trace(LogInfoType.INFO, outInfo, "Main", LogCategory.I);

                #endregion

                #region 处理域信息

                if (bie.lstDomainInfo.Count > 0)
                {
                    successCnt = 0;
                    foreach (strBIE_DomainInfo str in bie.lstDomainInfo)
                    {
                        rtv = domain_record_insert(str.name, str.parentNameFullPath, int.Parse(str.isStation), "bie");
                        if (rtv != 0)
                        {
                            string info = string.Format("BIE_write_2_DB,domain_record_insert{0}", get_rtv_str(rtv));                   
                            Logger.Trace(LogInfoType.EROR, info, "Main", LogCategory.I);
                        }

                        if (rtv == 0)
                        {
                            successCnt++;
                        }
                    }

                    if (successCnt > 0)
                    {
                        outInfo = string.Format("(2/3)BIE_write_2_DB，写入域列表成功.\r\n");                       
                        Logger.Trace(LogInfoType.INFO, outInfo, "Main", LogCategory.I);
                    }
                }

                #endregion

                #region 处理设备信息

                if (bie.lstDeviceInfo.Count > 0)
                {
                    successCnt = 0;
                    foreach (strBIE_DeviceInfo str in bie.lstDeviceInfo)
                    {
                        rtv = domain_get_id_by_nameFullPath(str.parentNameFullPath, ref id);
                        if (rtv != 0)
                        {
                            string info = string.Format("BIE_write_2_DB,domain_get_id_by_nameFullPath:{0}", get_rtv_str(rtv));                           
                            Logger.Trace(LogInfoType.EROR, info, "Main", LogCategory.I);
                            continue;
                        }

                        rtv = deviceinfo_record_insert(id,str.name, str.sn,str.aliasName,str.des);
                        if (rtv != 0)
                        {
                            string info = string.Format("BIE_write_2_DB,device_record_insert:{0}", get_rtv_str(rtv));
                            Logger.Trace(LogInfoType.EROR, info, "Main", LogCategory.I);
                            continue;
                        }
                        else
                        {         
                            strDevice di = new strDevice();
                            di.ipAddr = str.ipAddr;
                            di.tac = str.tac;
                            di.earfcn = str.earfcn;
                            di.aliasName = str.aliasName;
                            di.des = str.des;

                            rtv = deviceinfo_record_update(id, str.name, di);
                            if (rtv == 0)
                            {
                                successCnt++;
                            }
                        }

                        rtv = apaction_record_insert(str.sn);
                        if (rtv != (int)RC.SUCCESS)
                        {
                            errInfo = get_rtv_str(rtv);
                            return rtv;
                        }

                        rtv = apconninfo_record_insert(str.sn, "null", "null", "null");
                        if (rtv != (int)RC.SUCCESS)
                        {
                            errInfo = get_rtv_str(rtv);
                            return rtv;
                        }
                    }

                    if (successCnt > 0)
                    {
                        outInfo = string.Format("(3/3)BIE_write_2_DB，写入设备列表成功.\r\n");                       
                        Logger.Trace(LogInfoType.INFO, outInfo, "Main", LogCategory.I);
                    }
                }

                #endregion

                #region 重新获取gDicDevFullName

                if (rtv == 0)
                {
                    if (0 == domain_dictionary_info_join_get(ref gDicDevFullName, ref gDicDevId_Station_DevName))
                    {
                        Logger.Trace(LogInfoType.INFO, "gDicDevFullName -> 获取OK！", "DB", LogCategory.I);
                        print_dic_dev_fullname_info("app_del_domain_request", gDicDevFullName);
                    }
                    else
                    {
                        Logger.Trace(LogInfoType.INFO, "gDicDevFullName -> 获取FAILED！", "DB", LogCategory.I);
                    }
                }

                #endregion
            }
            catch (Exception ee)
            {                
                Logger.Trace(LogInfoType.EROR, ee.Message + ee.StackTrace, "Main", LogCategory.I);
                return -1;
            }

            return 0;
        }

        /// <summary>
        /// 生成alarm相关的CSV文件
        /// </summary>
        /// <param name="fileFullPath"></param>
        /// <param name="bie"></param>
        /// <param name="errInfo"></param>
        /// <returns></returns>
        private int BIE_generate_csv_file(string fileFullPath, strBatchImportExport bie, ref string errInfo)
        {
            if (string.IsNullOrEmpty(fileFullPath))
            {
                errInfo = string.Format("fileFullPath is NULL");
                Logger.Trace(LogInfoType.EROR, errInfo, "DB", LogCategory.I);
                return -1;
            }

            if (bie.lstDomainInfo.Count == 0 || bie.lstDeviceInfo.Count == 0)
            {
                errInfo = string.Format("bie is empty.");
                Logger.Trace(LogInfoType.EROR, errInfo, "DB", LogCategory.I);
                return -1;
            }

            try
            {
                if (File.Exists(fileFullPath))
                {
                    File.Delete(fileFullPath);
                }

                byte[] data = null;
                FileStream fs = new FileStream(fileFullPath, FileMode.Create);

                string title = string.Format("\n注意事项:,单元格不要包含英文的逗号\n");

                data = System.Text.Encoding.Default.GetBytes(title);
                fs.Write(data, 0, data.Length);

                data = System.Text.Encoding.Default.GetBytes("\n\n\n");
                fs.Write(data, 0, data.Length);


                title = string.Format("域序号,域全路径,是否为站点\n");

                data = System.Text.Encoding.Default.GetBytes(title);
                fs.Write(data, 0, data.Length);

                int index = 1;
                foreach (strBIE_DomainInfo str in bie.lstDomainInfo)
                {
                    string sqlSub = "";

                    #region 构造字符串

                    sqlSub += string.Format("{0},", index++);

                    //(1)
                    sqlSub += string.Format("{0}.{1},", str.parentNameFullPath, str.name);

                    //(2)
                    if (str.isStation == "0")
                    {
                        sqlSub += string.Format("否");
                    }
                    else
                    {
                        sqlSub += string.Format("是");
                    }

                    #endregion

                    data = System.Text.Encoding.Default.GetBytes(sqlSub + "\n");
                    fs.Write(data, 0, data.Length);
                }

                data = System.Text.Encoding.Default.GetBytes("\n\n\n");
                fs.Write(data, 0, data.Length);


                title = string.Format("设备序号,所属域,设备名称,SN,IP地址,TAC,EARFCN,别名,描述\n");

                data = System.Text.Encoding.Default.GetBytes(title);
                fs.Write(data, 0, data.Length);

                index = 1;
                foreach (strBIE_DeviceInfo str in bie.lstDeviceInfo)
                {
                    string sqlSub = "";

                    #region 构造字符串

                    sqlSub += string.Format("{0},", index++);

                    //(1)
                    sqlSub += string.Format("{0},", str.parentNameFullPath);

                    //(2)
                    sqlSub += string.Format("{0},", str.name);

                    //(3)
                    sqlSub += string.Format("{0},", str.sn);

                    //(4)
                    sqlSub += string.Format("{0},", str.ipAddr);

                    //(5)
                    sqlSub += string.Format("{0},", str.tac);

                    //(6)
                    sqlSub += string.Format("{0},", str.earfcn);

                    //(7)
                    sqlSub += string.Format("{0},", str.aliasName);

                    //(8)
                    sqlSub += string.Format("{0}", str.des);

                    #endregion

                    data = System.Text.Encoding.Default.GetBytes(sqlSub + "\n");
                    fs.Write(data, 0, data.Length);
                }

                data = System.Text.Encoding.Default.GetBytes("\n\n\n");
                fs.Write(data, 0, data.Length);

                //清空缓冲区、关闭流
                fs.Flush();
                fs.Close();
                fs.Dispose();
                fs = null;
            }
            catch (Exception e)
            {
                errInfo = e.Message + e.StackTrace;
                Logger.Trace(LogInfoType.EROR, e.Message + e.StackTrace, "Main", LogCategory.I);
                return -1;
            }

            return 0;
        }

        /// <summary>
        /// 从CSV文件中获取相关的信息
        /// </summary>
        /// <param name="filePath">CSV的绝对路径</param>
        /// <param name="bie">获取到的结构体</param>
        /// <param name="errInfo">出错信息</param>
        /// <returns>
        /// 0  ： 成功
        /// -1 ： 失败
        /// </returns>
        private int Get_Info_From_CSV_File(string filePath,ref strBatchImportExport bie, ref string errInfo)
        {
            bool flag = false;
            int domainInx1 = 0, domainInx2 = 0;
            int deviceInx1 = 0, deviceInx2 = 0;

            try
            {
                string[] lines = System.IO.File.ReadAllLines(filePath, Encoding.Default);
                if (lines.Length <= 0)
                {
                    errInfo = string.Format("filePath = {0},为空文件.", filePath);
                    return -1;
                }

                #region 获取域信息的下标范围

                flag = false;
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].Contains("域序号") && lines[i].Contains("域全路径"))
                    {
                        flag = true;
                        domainInx1 = i;
                    }

                    if (flag)
                    {
                        if (lines[i] == "")
                        {
                            domainInx2 = i;
                            break;
                        }
                    }
                }

                if (domainInx1 >= domainInx2)
                {
                    errInfo = string.Format("domainInx1 = {0},domainInx2 = {1},找不到域相关的信息", domainInx1, domainInx2);
                    return -1;
                }

                #endregion

                #region 获取设备信息的下标范围

                flag = false;
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].Contains("设备序号") && lines[i].Contains("所属域"))
                    {
                        flag = true;
                        deviceInx1 = i;
                    }

                    if (flag)
                    {
                        if (lines[i] == "")
                        {
                            deviceInx2 = i;
                            break;
                        }
                        else if (i == (lines.Length - 1))
                        {
                            deviceInx2 = i + 1;
                            break;
                        }
                        else
                        {

                        }
                    }
                }

                if (deviceInx1 >= deviceInx2)
                {
                    errInfo = string.Format("deviceInx1 = {0},deviceInx2 = {1},找不到设备相关的信息", deviceInx1, deviceInx2);
                    return -1;
                }

                #endregion

                #region 获取域信息

                bie = new strBatchImportExport();
                bie.lstDomainInfo = new List<strBIE_DomainInfo>();
                bie.lstDeviceInfo = new List<strBIE_DeviceInfo>();

                for (int i = (domainInx1 + 1); i <= (domainInx2 - 1); i++)
                {
                    strBIE_DomainInfo item = new strBIE_DomainInfo();
                    string[] s = lines[i].Split(new char[] { ',' });

                    if (s.Length != 3)
                    {
                        errInfo = string.Format("{0},格式不对", lines[i]);
                        return -1;
                    }

                    if (!s[1].Contains("."))
                    {
                        errInfo = string.Format("{0},格式不对", s[1]);
                        return -1;
                    }

                    if (s[2] != "否" && s[2] != "是")
                    {
                        errInfo = string.Format("{0},格式不对", s[2]);
                        return -1;
                    }

                    int j = s[1].LastIndexOf(".");
                    item.parentNameFullPath = s[1].Substring(0, j);
                    item.name = s[1].Substring(j + 1);

                    if (s[2] == "否")
                    {
                        item.isStation = "0";
                    }
                    else
                    {
                        item.isStation = "1";
                    }

                    bie.lstDomainInfo.Add(item);
                }

                #endregion

                #region 获取设备信息

                for (int i = (deviceInx1 + 1); i <= (deviceInx2 - 1); i++)
                {
                    strBIE_DeviceInfo item = new strBIE_DeviceInfo();
                    string[] s = lines[i].Split(new char[] { ',' });

                    if (s.Length != 9)
                    {
                        errInfo = string.Format("{0},格式不对", lines[i]);
                        return -1;
                    }

                    if (!s[1].Contains("."))
                    {
                        errInfo = string.Format("{0},格式不对", s[1]);
                        return -1;
                    }

                    if (string.IsNullOrEmpty(s[2]))
                    {
                        errInfo = string.Format("名称为空,格式不对");
                        return -1;
                    }

                    item.parentNameFullPath = s[1];
                    item.name = s[2];
                    item.sn = s[3];
                    item.ipAddr = s[4];
                    item.tac = s[5];
                    item.earfcn = s[6];
                    item.aliasName = s[7];
                    item.des = s[8];

                    //public string sn;
                    //public string ipAddr;
                    //public string tac;
                    //public string earfcn;
                    //public string des;

                    bie.lstDeviceInfo.Add(item);
                }

                #endregion
            }
            catch (Exception e)
            {
                errInfo = e.Message + e.StackTrace;
                Logger.Trace(LogInfoType.EROR, e.Message + e.StackTrace, "Main", LogCategory.I);
                return -1;
            }

            return 0;
        }


        /// <summary>
        /// 从数据库中获取所有的批量导出信息
        /// </summary>
        /// <param name="filePath">导出文件的绝对路径</param>
        /// <param name="errInfo">错误信息</param>
        /// <returns>
        /// 0  : 成功
        /// -1 ：失败
        /// </returns>
        public int BIE_get_all_config_from_DB_JSON(string filePath,ref string errInfo)
        {
            int rtv = -1;

            if (string.IsNullOrEmpty(filePath))
            {
                errInfo = string.Format("filePath为空.");
                return -1;
            }

            if (File.Exists(filePath))
            {
                errInfo = string.Format("filePath={0},文件已经存在.", filePath);
                return -1;
            }

            string DirectoryName = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(DirectoryName))
            {
                errInfo = string.Format("路径={0},不存在.", DirectoryName);
                return -1;
            }

            string JsonText = "";
            strBatchImportExport bie = new strBatchImportExport();

            rtv = BIE_read_from_DB(ref bie, ref errInfo, ref JsonText);
            if (rtv != 0)
            {
                return -1;
            }

            System.IO.File.WriteAllText(filePath, JsonText, Encoding.Default);
            return 0;
        }

        /// <summary>
        /// 将配置文件导入到数据库中
        /// </summary>
        /// <param name="filePath">导入文件的绝对路径</param>
        /// <param name="errInfo">错误信息</param>
        /// <returns>
        /// 0  : 成功
        /// -1 ：失败
        /// </returns>
        public int BIE_set_all_config_2_DB_JSON(string filePath, ref string errInfo)
        {
            int rtv = -1;

            if (string.IsNullOrEmpty(filePath))
            {
                errInfo = string.Format("filePath为空.");
                return -1;
            }

            if (!File.Exists(filePath))
            {
                errInfo = string.Format("filePath={0},文件不存在.", filePath);
                return -1;
            }

            string JsonText = System.IO.File.ReadAllText(filePath, Encoding.Default);
            strBatchImportExport bie = JsonConvert.DeserializeObject<strBatchImportExport>(JsonText);

            rtv = BIE_write_2_DB(bie, ref errInfo);
            if (rtv != 0)
            {
                return -1;
            }
            
            return 0;
        }


        /// <summary>
        /// 从数据库中获取所有的批量导出信息
        /// new,2019-02-14
        /// </summary>
        /// <param name="filePath">导出csv文件的绝对路径,如F:\abc.csv</param>
        /// <param name="errInfo">错误信息</param>
        /// <returns>
        /// 0  : 成功
        /// -1 ：失败
        /// </returns>
        public int BIE_get_all_config_from_DB_CSV(string filePath, ref string errInfo)
        {
            int rtv = -1;

            if (string.IsNullOrEmpty(filePath))
            {
                errInfo = string.Format("filePath为空.");
                return -1;
            }

            string DirectoryName = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(DirectoryName))
            {
                errInfo = string.Format("路径={0},不存在.", DirectoryName);
                return -1;
            }

            string extension = System.IO.Path.GetExtension(filePath);
            if (extension != ".csv")
            {
                errInfo = string.Format("{0}:", "后缀名不为csv.", extension);
                Logger.Trace(LogInfoType.EROR, errInfo, "Main", LogCategory.I);
                return -1;

            }

            if (File.Exists(filePath))
            {
                errInfo = string.Format("filePath={0},文件已经存在.", filePath);
                return -1;
            }           

            string JsonText = "";
            strBatchImportExport bie = new strBatchImportExport();

            rtv = BIE_read_from_DB(ref bie, ref errInfo, ref JsonText);
            if (rtv != 0)
            {
                return -1;
            }

            rtv = BIE_generate_csv_file(filePath, bie, ref errInfo);
            if (rtv != 0)
            {
                return -1;
            }

            return 0;
        }

        /// <summary>
        /// 将配置文件导入到数据库中
        /// </summary>
        /// <param name="filePath">导入文件的绝对路径,如F:\123.csv</param>
        /// <param name="errInfo">错误信息</param>
        /// <returns>
        /// 0  : 成功
        /// -1 ：失败
        /// </returns>
        public int BIE_set_all_config_2_DB_CSV(string filePath, ref string errInfo)
        {
            int rtv = -1;      

            if (string.IsNullOrEmpty(filePath))
            {
                errInfo = string.Format("filePath为空.");
                return -1;
            }

            if (!File.Exists(filePath))
            {
                errInfo = string.Format("filePath={0},文件不存在.", filePath);
                return -1;
            }

            string extension = System.IO.Path.GetExtension(filePath);
            if (extension != ".csv")
            {
                errInfo = string.Format("{0}:", "后缀名不为csv.", extension);
                Logger.Trace(LogInfoType.EROR, errInfo, "Main", LogCategory.I);
                return -1;

            }

            strBatchImportExport bie = new strBatchImportExport();
            rtv = Get_Info_From_CSV_File(filePath, ref bie, ref errInfo);
            if (rtv != 0)
            {
                return -1;
            }

            rtv = BIE_write_2_DB(bie, ref errInfo);
            if (rtv != 0)
            {
                return -1;
            }

            return 0;
        }

        #endregion

        #region 18 - configinfo操作

        /// <summary>
        /// 判断对应sn的记录是否存在alarminfo表中
        /// </summary>
        /// <param name="name">配置名称</param>
        /// <param name="type">配置类型</param>
        /// <returns> 
        ///   -1 ：数据库尚未打开
        ///   -2 ：参数有误
        ///   -4 ：数据库操作失败 
        ///    0 : 不存在
        ///    1 ：存在
        /// </returns>
        private int configinfo_record_exist(string name, string type)
        {
            UInt32 cnt = 0;

            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return -1;
            }

            if (string.IsNullOrEmpty(name))
            {
                Logger.Trace(Logger.__INFO__, "name参数为空.");
                return -2;
            }

            if (name.Length > 128)
            {
                Logger.Trace(Logger.__INFO__, "name参数长度有误.");
                return -2;
            }

            if (string.IsNullOrEmpty(type))
            {
                Logger.Trace(Logger.__INFO__, "type参数为空.");
                return -2;
            }

            if (type != "Server" && type != "Client")
            {
                Logger.Trace(Logger.__INFO__, "type的值非法.");
                return -2;
            }

            string sql = string.Format("select count(*) from configinfo where name = '{0}' and type = '{1}'", name, type);
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
        /// 插入记录到alarminfo表中
        /// SN和flag组成记录的关键字
        /// </summary> 
        /// <param name="cfg">记录结构体</param>
        /// <returns>
        ///   -1 ：数据库尚未打开
        ///   -2 ：参数有误
        ///   -3 ：记录已经存在
        ///   -4 ：数据库操作失败
        ///    0 : 插入成功 
        /// </returns>
        private int configinfo_record_insert(strConfig cfg)
        {
            string sqlSub = "";

            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return -1;
            }

            if (string.IsNullOrEmpty(cfg.name))
            {
                Logger.Trace(Logger.__INFO__, "name参数为空.");
                return -2;
            }

            if (cfg.name.Length > 128)
            {
                Logger.Trace(Logger.__INFO__, "name参数长度有误.");
                return -2;
            }

            if (string.IsNullOrEmpty(cfg.type))
            {
                Logger.Trace(Logger.__INFO__, "type参数为空.");
                return -2;
            }

            if (cfg.type != "Server" && cfg.type != "Client")
            {
                Logger.Trace(Logger.__INFO__, "type的值非法.");
                return -2;
            }

            if (1 == configinfo_record_exist(cfg.name, cfg.type))
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.EXIST], "DB", LogCategory.I);
                return -3;
            }

            //(1)
            sqlSub += string.Format("'{0}',",cfg.name);

            //(2)
            if (string.IsNullOrEmpty(cfg.value))
            {
                sqlSub += string.Format("'{0}',", "");
            }
            else
            {
                sqlSub += string.Format("'{0}',", cfg.value);
            }

            //(3)
            if (string.IsNullOrEmpty(cfg.des))
            {
                sqlSub += string.Format("'{0}',",DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            }
            else
            {
                sqlSub += string.Format("'{0}',", cfg.des);
            }

            //(4)
            sqlSub += string.Format("'{0}',", cfg.type);

            //(5)
            if (string.IsNullOrEmpty(cfg.res1))
            {
                sqlSub += string.Format("'{0}',", "");
            }
            else
            {
                sqlSub += string.Format("'{0}',", cfg.res1);
            }

            //(6)
            if (string.IsNullOrEmpty(cfg.res2))
            {
                sqlSub += string.Format("'{0}'", "");
            }
            else
            {
                sqlSub += string.Format("'{0}'", cfg.res2);
            }

            string sql = string.Format("insert into configinfo values(NULL,{0})", sqlSub);
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
        /// 通过cfg中的name和type，更新结构体中的其他字段
        /// </summary>
        /// <param name="cfg">配置结构体
        /// value;     //配置值
        /// des;       //配置描述
        /// res1;      //保留字段1
        /// res2;      //保留字段2
        /// </param>
        /// <returns>
        /// 返回值 ：
        /// -1 ：数据库尚未打开
        /// -2 ：参数有误
        /// -3 ：SN不存在
        /// -6 : 记录不存在
        /// -4 ：数据库操作失败 
        ///  0 : 更新成功  
        /// </returns>
        private int configinfo_record_update(strConfig cfg)
        {
            int ret = 0;
            string sql = "";
            string sqlSub = "";

            #region 构造SQL语句

            //(1)
            if (!string.IsNullOrEmpty(cfg.value))
            {
                if (cfg.value.Length > 128)
                {
                    Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.PAR_LEN_ERR], "DB", LogCategory.I);
                    return (int)RC.PAR_LEN_ERR;
                }
                else
                {
                    sqlSub += string.Format("value = '{0}',", cfg.value);
                }
            }

            //(2)
            if (!string.IsNullOrEmpty(cfg.des))
            {
                if (cfg.des.Length > 128)
                {
                    Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.PAR_LEN_ERR], "DB", LogCategory.I);
                    return (int)RC.PAR_LEN_ERR;
                }
                else
                {
                    sqlSub += string.Format("des = '{0}',", cfg.des);
                }
            }


            //(3)
            if (!string.IsNullOrEmpty(cfg.res1))
            {
                if (cfg.res1.Length > 32)
                {
                    Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.PAR_LEN_ERR], "DB", LogCategory.I);
                    return (int)RC.PAR_LEN_ERR;
                }
                else
                {
                    sqlSub += string.Format("res1 = '{0}',", cfg.res1);
                }
            }

            //(4)
            if (!string.IsNullOrEmpty(cfg.res2))
            {
                if (cfg.res2.Length > 32)
                {
                    Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.PAR_LEN_ERR], "DB", LogCategory.I);
                    return (int)RC.PAR_LEN_ERR;
                }
                else
                {
                    sqlSub += string.Format("res2 = '{0}',", cfg.res2);
                }
            }
            

            #endregion

            if (sqlSub != "")
            {
                //去掉最后一个字符
                sqlSub = sqlSub.Remove(sqlSub.Length - 1, 1);

                //更新configinfo表中的信息               
                sql = string.Format("update configinfo set {0} where name = '{1}' and type = '{2}'", sqlSub,cfg.name,cfg.type);

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
                Logger.Trace(Logger.__INFO__, "configinfo_record_update,无需更新configinfo表");
            }

            return ret;
        }


        /// <summary>
        /// 通过cfg，创建或更新一条告警记录
        /// </summary>
        /// <param name="cfg">结构体</param>
        /// <param name="errInfo">出错时返回的信息</param>
        /// <returns>
        /// 返回值 ：
        /// 非0 ：失败
        ///  0  : 成功  
        /// </returns>
        public int configinfo_record_create(strConfig cfg, ref string errInfo)
        {
            int rtv = -1;
            if (false == myDbConnFlag)
            {
                errInfo = get_rtv_str((int)RC.NO_OPEN);
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return -1;
            }

            if (string.IsNullOrEmpty(cfg.name) || (cfg.name.Length > 128))
            {
                errInfo = string.Format("cfg.name={0},参数非法", cfg.name);
                Logger.Trace(LogInfoType.EROR, errInfo, "DB", LogCategory.I);
                return -1;
            }

            if (string.IsNullOrEmpty(cfg.type) || (cfg.type.Length > 128))
            {
                errInfo = string.Format("cfg.type={0},参数非法", cfg.type);
                Logger.Trace(LogInfoType.EROR, errInfo, "DB", LogCategory.I);
                return -1;
            }
            else
            {
                if (cfg.type != "Server" && cfg.type != "Client")
                {
                    errInfo = string.Format("cfg.type={0},参数非法", cfg.type);
                    Logger.Trace(LogInfoType.EROR, errInfo, "DB", LogCategory.I);
                    return -1;
                }
            }

            rtv = configinfo_record_exist(cfg.name, cfg.type);
            if (rtv == 0)
            {
                //记录不存在，插入处理

                if (0 != configinfo_record_insert(cfg))
                {
                    errInfo = string.Format("configinfo_record_insert出错.");
                    Logger.Trace(LogInfoType.EROR, errInfo, "DB", LogCategory.I);
                    return -1;
                }

                return 0;
            }
            else if (rtv == 1)
            {
                //记录存在，更新处理
                              
                if (0 != configinfo_record_update(cfg))
                {
                    errInfo = string.Format("configinfo_record_update出错.");
                    Logger.Trace(LogInfoType.EROR, errInfo, "DB", LogCategory.I);
                    return -1;
                }

                return 0;
            }
            else
            {
                errInfo = string.Format("configinfo_record_exist出错.");
                //出错处理
                return rtv;
            }
        }

        /// <summary>
        /// 在configinfo表中删除指定的记录
        /// </summary>
        /// <param name="name">名称:
        /// (1) 特定的name
        /// (2) "",表示所有的name
        /// </param>
        /// <param name="type">类型:
        /// (1) Server
        /// (2) Client
        /// (3) "",表示Server和Client
        /// </param>
        /// <returns>
        /// -1 ：数据库尚未打开
        /// -2 ：参数有误
        /// -6 ：记录不存在
        /// -4 ：数据库操作失败     
        ///  0 : 删除成功 
        /// </returns>
        public int configinfo_record_delete(string name, string type)
        {
            int ret = 0;
            string sql = "";

            string nameTmp = "";
            string typeTmp = "";

            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return -1;
            }

            if (string.IsNullOrEmpty(name))
            {
                nameTmp = "";
            }
            else
            {
                if (name.Length > 128)
                {
                    Logger.Trace(Logger.__INFO__, "name参数长度有误");
                    return -2;
                }
                else
                {
                    nameTmp = name;
                }
            }


            if (string.IsNullOrEmpty(type))
            {
                typeTmp = "";
            }
            else
            {
                if (type != "Server" && type != "Client")
                {
                    Logger.Trace(Logger.__INFO__, "type的值非法.");
                    return -2;
                }
                else
                {
                    typeTmp = type;
                }
            }

            if (nameTmp == "")
            {
                if (typeTmp == "")
                {
                    sql = string.Format("delete from configinfo");
                }
                else
                {
                    sql = string.Format("delete from configinfo where type = '{0}'", typeTmp);
                }
            }
            else
            {
                if (typeTmp == "")
                {
                    sql = string.Format("delete from configinfo where name = '{0}'",nameTmp);
                }
                else
                {
                    sql = string.Format("delete from configinfo where name = '{0}' and type = '{1}'", nameTmp,typeTmp);
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

            return ret;
        }

        /// <summary>
        /// 通过查询条件query获取alarminfo表中的各条记录
        /// </summary>
        /// <param name="lst">返回的记录列表</param>
        /// <param name="type">过滤条件：
        /// （1） Server，只返回服务器的记录
        /// （2） Client，只返回客户端的记录
        /// （3） Both，  返回服务器和客户端的记录
        /// </param>
        /// <param name="errInfo">错误信息</param>
        /// <returns>
        ///  0: 成功
        /// -1：失败
        /// </returns>
        public int configinfo_record_entity_get(ref List<strConfig> lst, string type, ref string errInfo)
        {
            if (false == myDbConnFlag)
            {
                Logger.Trace(LogInfoType.EROR, dicRTV[(int)RC.NO_OPEN], "DB", LogCategory.I);
                return (int)RC.NO_OPEN;
            }

            string sql = "";          

            if (type != "Server" && type != "Client" && type != "Both")
            { 
                errInfo = string.Format("type = {0},非法.",type);
                return -1;
            }

            if (type == "Server")
            {
                sql = string.Format("select * from configinfo where type = 'Server'");
            }
            else if (type == "Client")
            {
                sql = string.Format("select * from configinfo where type = 'Client'");
            }
            else
            {
                sql = string.Format("select * from configinfo");
            }

            try
            {
                using (MySqlCommand cmd = new MySqlCommand(sql, myDbConn))
                {
                    using (MySqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            strConfig str = new strConfig();

                            #region 获取记录

                            if (!string.IsNullOrEmpty(dr["name"].ToString()))
                            {
                                str.name = dr["name"].ToString();
                            }
                            else
                            {
                                str.name = "";
                            }

                            if (!string.IsNullOrEmpty(dr["value"].ToString()))
                            {
                                str.value = dr["value"].ToString();
                            }
                            else
                            {
                                str.value = "";
                            }

                            if (!string.IsNullOrEmpty(dr["des"].ToString()))
                            {
                                str.des = dr["des"].ToString();
                            }
                            else
                            {
                                str.des = "";
                            }

                            if (!string.IsNullOrEmpty(dr["type"].ToString()))
                            {
                                str.type = dr["type"].ToString();
                            }
                            else
                            {
                                str.type = "";
                            }

                            if (!string.IsNullOrEmpty(dr["res1"].ToString()))
                            {
                                str.res1 = dr["res1"].ToString();
                            }
                            else
                            {
                                str.res1 = "";
                            }

                            if (!string.IsNullOrEmpty(dr["res2"].ToString()))
                            {
                                str.res2 = dr["res2"].ToString();
                            }
                            else
                            {
                                str.res2 = "";
                            }                           

                            #endregion

                            lst.Add(str);
                        }
                        dr.Close();
                    }
                }
            }
            catch (Exception e)
            {
                errInfo = e.Message + e.StackTrace;
                Logger.Trace(LogInfoType.EROR, e.Message + e.StackTrace, "DB", LogCategory.I);
                dicRTV[(int)RC.OP_FAIL] = string.Format("数据库操作失败:{0}", e.Message + e.StackTrace);
                return (int)RC.OP_FAIL;
            }

            return (int)RC.SUCCESS;
        }

        #endregion
    }
}

