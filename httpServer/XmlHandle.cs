using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

namespace httpServer
{
    #region  TR069协议结构
    enum UploadFileType
    {
        VendorConfigurationFile,
        VendorLogFile,
        //VendorPerformanceFile,
        //VendorAlarmLogFile,
        //VendorRunLogFile,
        //VendorCoreLogFile
    }

    enum DownloadFileType
    {
        FirmwareUpgradeImage,
        //WebContent,
        //VendorConfigurationFile,
        //EmergencyFirmwareUpgradeImage
    }

          
    public struct InformEventCode
    {
        public static String BOOTSTRAP = "0 BOOTSTRAP";
        public static String BOOT = "1 BOOT";
        public static String PERIODIC = "2 PERIODIC";
        public static String SCHEDULED = "3 SCHEDULED";
        public static String VALUE_CHANGE = "4 VALUE CHANGE";
        public static String KICKED = "5 KICKED";
        public static String CONNECTION_REQUEST = "6 CONNECTION REQUEST";
        public static String TRANSFER_COMPLETE = "7 TRANSFER COMPLETE";
        public static String DIAGNOSTICS_COMPLETE = "8 DIAGNOSTICS COMPLETE";
        public static String REQUEST_DOWNLOAD = "9 REQUEST DOWNLOAD";
        public static String AUTONOMOUS_TRANSFER_COMPLETE = "10 AUTONOMOUS TRANSFER COMPLETE";
        public static String DU_STATE_CHANGE_COMPLETE = "11 DU STATE CHANGE COMPLETE";
        public static String AUTONOMOUS_DU_STATE_CHANGE_COMPLETE = "12 AUTONOMOUS DU STATE CHANGE COMPLETE";
        public static String WAKEUP = "13 WAKEUP";
        public static String M_Reboot = "M Reboot";
    }

    public struct RPCMethod
    {
        public static String Inform = "Inform";
        public static String InformResponse = "InformResponse";
        public static String GetParameterName = "GetParameterName";
        public static String GetParameterNamesResponse = "GetParameterNamesResponse";
        public static String GetParameterValues = "GetParameterValues";
        public static String GetParameterValuesResponse = "GetParameterValuesResponse";
        public static String SetParameterValues = "SetParameterValues";
        public static String SetParameterValuesResponse = "SetParameterValuesResponse";
        public static String Upload = "Upload";
        public static String UploadResponse = "UploadResponse";
        public static String Download = "Download";
        public static String DownloadResponse = "DownloadResponse";
        public static String Reboot = "Reboot";
        public static String RebootResponse = "RebootResponse";
        public static String TransferComplete = "TransferComplete";
        public static String TransferCompleteResponse = "TransferCompleteResponse";
        public static String FactoryReset = "FactoryReset";
        public static String FactoryResetResponse = "FactoryResetResponse";
        public static String AutonomousTransferComplete = "AutonomousTransferComplete";
        public static String AutonomousTransferCompleteResponse = "AutonomousTransferCompleteResponse";
        public static String Fault = "SOAP-ENV:Fault";
    }

    #endregion 

    #region XML消息解析结构
    class XmlParameter  
    {
        public String name;
        public String value;
        public String valueType;

        public XmlParameter(string name, string value, string valueType)
        {
            this.name = name;
            this.value = value;
            this.valueType = valueType;
        }
        public XmlParameter(string name, string value):this(name,value,"")
        { }
        public XmlParameter() : this("", "", "")
        { }
    };

    class XmlValueChange
    {
        public List<XmlParameter> valueChangeNode;
        public XmlValueChange()
        {
            valueChangeNode = new List<XmlParameter>();
        }
    };

    class XmlInform
    {
        public String Manufacturer;
        public String OUI;
        public String ProductClass;
        public String SN;
        public String Imsi;
        public String CurrentTime;
        [MarshalAs(UnmanagedType.AnsiBStr, SizeConst = 16, ArraySubType = UnmanagedType.AnsiBStr)]
        public String [] EventCode;

        public XmlValueChange ValueChange;
        public String ConnectionRequestURL;
        public XmlInform()
        {
            EventCode = new String[8];
            ValueChange = new XmlValueChange();
        }
    }

    class XmlTransferComplete
    {
        public String CommandKey;
        public int FaultCode;  //0：成功；other：失败
        public String FaultString;
        public String StartTime;
        public String CompleteTime;
    }

    class XmlFault
    {
        public int FaultCode;  //0：成功；other：失败
        public String FaultString;
    }

    class XmlParameterStruct
    {
        public String ID;
        public String Method;
        public XmlInform xmlInform;
        public List<XmlParameter> parameterNode;
        public XmlTransferComplete transferComplete;
        public XmlFault xmlFalut;

        public XmlParameterStruct()
        {
            xmlInform = new XmlInform();
            parameterNode = new List<XmlParameter>();
            transferComplete = new XmlTransferComplete();
            xmlFalut = new XmlFault();
        }
    }

    class XmlMethodStruct
    {
        public String ID;
        public String Method;
        public XmlNode BodyNode;
    }
    #endregion 

    class XmlHandle
    { 
        static private String XMLNS_SOAPENV = "http://schemas.xmlsoap.org/soap/envelope/";
        static private String XMLNS_SOAPENC = "http://schemas.xmlsoap.org/soap/encoding/" ;
        static private String XMLNS_CWMP = "urn:dslforum-org:cwmp-1-0";
        static private String XMLNS_XSD = "http://www.w3.org/2001/XMLSchema";
        static private String XMLNS_XSI = "http://www.w3.org/2001/XMLSchema-instance";

        static  public String SetParameterValueFor1Boot = "SetParameterValueFor1Boot";

        #region 收到消息处理

        /// <summary>
        /// 判断某事件是否在事件列表中
        /// </summary>
        /// <param name="EventList">事件列表</param>
        /// <param name="EventCode">事件</param>
        /// <returns>在事件列表中返回true,否则返回false</returns>
        static public bool GetEventInList(String[] EventList, String EventCode)
        {
            bool isIn = false;
            foreach (String code in EventList)
            {
                if (string.IsNullOrEmpty(code)) break;
                if (String.Compare(code, EventCode, true) == 0)
                {
                    isIn = true;
                    break;
                }
            }
            return isIn;
        }

        public void recvApMessage(String msg)
        {
           // MessageBox.Show(msg);
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(msg);
            var root = xmlDoc.DocumentElement;//取到根结点

            //XmlElement element = (XmlElement)xmlDoc.SelectSingleNode("Header");
            //string name = element.GetAttribute("Header");
            //MessageBox.Show(name);
        }

        static private XmlNode Get_Node_By_NodeName(XmlNodeList listNodes, String NodeName)
        {
            if (listNodes == null) return null;
            if (string.IsNullOrEmpty(NodeName)) return null;

            foreach (XmlNode node in listNodes)
            {
                if (String.Compare(node.Name, NodeName, true) == 0)
                {
                    return node;
                }
            }
            return null;
        }

        /// <summary>
        /// 解析收到的AP消息，从中获取AP发过来的参数
        /// </summary>
        /// <param name="msg">AP发过来的xml消息</param>
        /// <returns>解析后的参数</returns>
        public XmlParameterStruct HandleRecvApMsg(String msg)
        {
            XmlParameterStruct parameterStruct = new XmlParameterStruct();

            XmlMethodStruct xmlStruct = Get_Xml_Msg_Method(msg);

            parameterStruct.ID = xmlStruct.ID;
            parameterStruct.Method = xmlStruct.Method;
            if (String.Compare(xmlStruct.Method, RPCMethod.Inform, true) == 0)
            {
                parameterStruct.xmlInform = Get_Xml_Msg_Inform(xmlStruct);
            }
            else if (String.Compare(xmlStruct.Method, RPCMethod.GetParameterValuesResponse, true) == 0)
            {
                parameterStruct.parameterNode = Get_Xml_Msg_GetParameterValuesResponse(xmlStruct);
            }
            else if (String.Compare(xmlStruct.Method, RPCMethod.TransferComplete, true) == 0)
            {
                parameterStruct.transferComplete = Get_Xml_Msg_TransferComplete(xmlStruct);
            }
            else if (String.Compare(xmlStruct.Method, RPCMethod.Fault, true) == 0)
            {
                parameterStruct.xmlFalut = Get_Xml_Msg_Fault(xmlStruct);
            }
            return parameterStruct;
        }

        static private string ReplaceStr(string str, string key, string value, bool IgnoreCase)
        {
            string newstr = str.Replace(key, value);

            int i = newstr.IndexOf(key, StringComparison.OrdinalIgnoreCase);

            if (i > 0 && IgnoreCase)
            {
                key = newstr.Substring(i, key.Length);
                return ReplaceStr(newstr, key, value, IgnoreCase);
            }
            else
            {
                return newstr;
            }

        }

        /// <summary>
        /// 从xml中获取RPC方法
        /// </summary>
        /// <param name="msg">AP发过来的xml消息</param>
        /// <returns>RPC方法及附带的内容xml节点</returns>
        static private XmlMethodStruct Get_Xml_Msg_Method(String msg)
        {
            String Id = "";
            XmlMethodStruct xmlStruct = new XmlMethodStruct();

            // MessageBox.Show(msg);
            XmlDocument xmlDoc = new XmlDocument();
            if (string.IsNullOrEmpty(msg))
                xmlDoc.Load("d:\\data.xml");
            else
                xmlDoc.LoadXml(msg);

            var root = xmlDoc.DocumentElement;//取到根结点
            XmlNodeList listNodes = null;
            listNodes = root.ChildNodes;

            XmlNode HeaderNode = Get_Node_By_NodeName(listNodes, "SOAP-ENV:Header");
            if (HeaderNode == null) return null;
            XmlNode IdNode = null;

            XmlNamespaceManager xnm = new XmlNamespaceManager(xmlDoc.NameTable);
            xnm.AddNamespace("cwmp", XMLNS_CWMP);
            IdNode = HeaderNode.SelectSingleNode("cwmp:ID", xnm);
            if (IdNode != null) Id = IdNode.InnerText;

            XmlNode BodyNode = Get_Node_By_NodeName(listNodes, "SOAP-ENV:Body");
            if (BodyNode == null) return null;

            xmlStruct.BodyNode = BodyNode;

            XmlNode BodyInformNode = BodyNode.FirstChild;
            if (BodyInformNode == null) return null;

            String method = BodyInformNode.Name;
            xmlStruct.Method = ReplaceStr(method, "cwmp:", "", true).Trim();
            xmlStruct.ID = Id;

            return xmlStruct;
        }

        /// <summary>
        /// RPC方法为Inform时，获取Inform消息内容
        /// </summary>
        /// <param name="xmlStruct"></param>
        /// <returns></returns>
        static private XmlInform Get_Xml_Msg_Inform(XmlMethodStruct xmlStruct)
        {
            if (xmlStruct == null) return null;

            if (String.Compare(xmlStruct.Method, RPCMethod.Inform, true) != 0) return null;

            XmlNode BodyNode = xmlStruct.BodyNode;
            if (BodyNode == null) return null;

            XmlNode BodyInformNode = BodyNode.FirstChild;
            if (BodyInformNode == null) return null;

            XmlInform xmlInform = new XmlInform();

            /*获取DeviceId信息*/
            XmlNode DeviceIdNode = null;
            DeviceIdNode = BodyInformNode.SelectSingleNode("DeviceId/OUI");
            if (DeviceIdNode != null)
                xmlInform.OUI = DeviceIdNode.InnerText;
            DeviceIdNode = BodyInformNode.SelectSingleNode("DeviceId/Manufacturer");
            if (DeviceIdNode != null)
                xmlInform.Manufacturer = DeviceIdNode.InnerText;
            DeviceIdNode = BodyInformNode.SelectSingleNode("DeviceId/ProductClass");
            if (DeviceIdNode != null)
                xmlInform.ProductClass = DeviceIdNode.InnerText;
            DeviceIdNode = BodyInformNode.SelectSingleNode("DeviceId/SerialNumber");
            if (DeviceIdNode != null)
                xmlInform.Imsi = DeviceIdNode.InnerText;

            XmlNode CurrentTimeNode = Get_Node_By_NodeName(BodyInformNode.ChildNodes, "CurrentTime");
            if (CurrentTimeNode != null)
                xmlInform.CurrentTime = CurrentTimeNode.InnerText;

            /*获取Event信息*/
            XmlNodeList listEventNodes = null;
            listEventNodes = Get_Node_By_NodeName(BodyInformNode.ChildNodes, "Event").ChildNodes;
            if (listEventNodes != null)
            {
                int i = 0;
                foreach (XmlNode node in listEventNodes)
                {
                    xmlInform.EventCode[i] = node.SelectSingleNode("EventCode").InnerText.Trim();
                    i = i + 1;
                }
            }

            XmlNodeList ParameterListNodes = null;
            ParameterListNodes = Get_Node_By_NodeName(BodyInformNode.ChildNodes, "ParameterList").ChildNodes;
            if (ParameterListNodes != null)
            {
                foreach (XmlNode node in ParameterListNodes)
                {
                    String strName = node.SelectSingleNode("Name").InnerText;
                    String strValue = node.SelectSingleNode("Value").InnerText; ;

                    XmlParameter parameter = new XmlParameter();
                    parameter.name = strName.Replace(GlobalParameter.XmlRootNode,"");
                    parameter.value = strValue;
                    xmlInform.ValueChange.valueChangeNode.Add(parameter);

                    if (String.Compare(strName, GlobalParameter.XmlRootNode + "DeviceInfo.SerialNumber", true) == 0)
                    {
                        xmlInform.SN = strValue;
                    }
                    else if (String.Compare(strName, GlobalParameter.XmlRootNode + "ManagementServer.ConnectionRequestURL", true) == 0)
                    {
                        xmlInform.ConnectionRequestURL = strValue;
                    }
                }
            }

            return xmlInform;

        }

        static private List<XmlParameter> Get_Xml_Msg_GetParameterValuesResponse(XmlMethodStruct xmlStruct)
        {
            if (xmlStruct == null) return null;

            if (String.Compare(xmlStruct.Method, RPCMethod.GetParameterValuesResponse, true) != 0) return null;

            XmlNode BodyNode = xmlStruct.BodyNode;
            if (BodyNode == null) return null;

            XmlNode BodyInformNode = BodyNode.FirstChild;
            if (BodyInformNode == null) return null;

            List<XmlParameter> parameterNode = new List<XmlParameter>();

            XmlNodeList listEventNodes = null;
            listEventNodes = Get_Node_By_NodeName(BodyInformNode.ChildNodes, "ParameterList").ChildNodes;

            foreach (XmlNode node in listEventNodes)
            {
                XmlParameter xmlParameter = new XmlParameter();

                XmlNode tmpNode;
                tmpNode = null;
                tmpNode = node.SelectSingleNode("Name");
                if (tmpNode != null)
                    xmlParameter.name = tmpNode.InnerText.Replace(GlobalParameter.XmlRootNode, "");

                tmpNode = null;
                tmpNode = node.SelectSingleNode("Value");
                if (tmpNode != null)
                    xmlParameter.value = tmpNode.InnerText;
                    xmlParameter.valueType = tmpNode.Attributes["xsi:type"].Value.Replace("xsd:", "");

                parameterNode.Add(xmlParameter);
            }

            return parameterNode;

        }

        static private XmlTransferComplete Get_Xml_Msg_TransferComplete(XmlMethodStruct xmlStruct)
        {
            if (xmlStruct == null) return null;

            if (String.Compare(xmlStruct.Method, RPCMethod.TransferComplete, true) != 0) return null;

            XmlNode BodyNode = xmlStruct.BodyNode;
            if (BodyNode == null) return null;

            XmlNode BodyInformNode = BodyNode.FirstChild;
            if (BodyInformNode == null) return null;

            XmlTransferComplete transferComplete = new XmlTransferComplete();

            XmlNode tempNode = null;
            tempNode = BodyInformNode.SelectSingleNode("CommandKey");
            if (tempNode != null)
                transferComplete.CommandKey = tempNode.InnerText;

            tempNode = BodyInformNode.SelectSingleNode("StartTime");
            if (tempNode != null)
                transferComplete.StartTime = tempNode.InnerText;

            tempNode = BodyInformNode.SelectSingleNode("CompleteTime");
            if (tempNode != null)
                transferComplete.CompleteTime = tempNode.InnerText;

            tempNode = BodyInformNode.SelectSingleNode("FaultStruct/FaultCode");
            if (tempNode != null)
                transferComplete.FaultCode = Convert.ToInt32(tempNode.InnerText);

            tempNode = BodyInformNode.SelectSingleNode("FaultStruct/FaultString");
            if (tempNode != null)
                transferComplete.FaultString = tempNode.InnerText;

            return transferComplete;
        }

        static private XmlFault Get_Xml_Msg_Fault(XmlMethodStruct xmlStruct)
        {
            if (xmlStruct == null) return null;

            if (String.Compare(xmlStruct.Method, RPCMethod.Fault, true) != 0) return null;

            XmlNode BodyNode = xmlStruct.BodyNode;
            if (BodyNode == null) return null;

            XmlNode BodyInformNode = BodyNode.FirstChild;
            if (BodyInformNode == null) return null;

            XmlFault xmlFault = new XmlFault();

            XmlNode detailNode = null;
            detailNode = BodyInformNode.SelectSingleNode("detail");
            if (detailNode == null) return null;

            XmlNode faultNode = null;
            faultNode = detailNode.FirstChild;
            if (faultNode == null) return null;

            XmlNode tempNode = null;
            tempNode = faultNode.SelectSingleNode("faultcode");
            if (tempNode != null)
                xmlFault.FaultCode = Convert.ToInt32(tempNode.InnerText);

            tempNode = faultNode.SelectSingleNode("faultstring");
            if (tempNode != null)
                xmlFault.FaultString = tempNode.InnerText;

            return xmlFault;
        }

        #endregion

        #region 创建要发送的消息
        static public XmlDocument CreateRootNode(String Id)
        {
            //初始化一个xml实例
            XmlDocument myXmlDoc = new XmlDocument();
            try
            {
                //创建xml的根节点
                XmlElement rootElement = myXmlDoc.CreateElement("SOAP-ENV:Envelope", XMLNS_SOAPENV);
                rootElement.SetAttribute("xmlns:SOAP-ENV", XMLNS_SOAPENV);
                rootElement.SetAttribute("xmlns:SOAP-ENC", XMLNS_SOAPENC);
                rootElement.SetAttribute("xmlns:cwmp", XMLNS_CWMP);
                rootElement.SetAttribute("xmlns:xsd", XMLNS_XSD);
                rootElement.SetAttribute("xmlns:xsi", XMLNS_XSI);
                //将根节点加入到xml文件中（AppendChild）
                myXmlDoc.AppendChild(rootElement);

                //初始化第一层的第一个子节点
                XmlElement levelElement1 = myXmlDoc.CreateElement("SOAP-ENV:Header", XMLNS_SOAPENV);
                //将第一层的第一个子节点加入到根节点下
                rootElement.AppendChild(levelElement1);

                //初始化第二层的第一个子节点
                XmlElement levelElement11 = myXmlDoc.CreateElement("cwmp", "ID", XMLNS_CWMP);
                levelElement11.SetAttribute("mustUnderstand", XMLNS_SOAPENV, "1");
                //填充第二层的第一个子节点的值（InnerText）
                levelElement11.InnerText = Id;
                levelElement1.AppendChild(levelElement11);

                XmlElement levelElement12 = myXmlDoc.CreateElement("cwmp", "NoMoreRequests", XMLNS_CWMP);
                levelElement12.InnerText = "0";
                levelElement1.AppendChild(levelElement12);
            }
            catch (Exception ex)
            {
                //Console.WriteLine(ex.ToString());
                String err = ex.ToString();
                return null;
            }

            return myXmlDoc;
        }

        static public byte[] CreateInformResponseXmlFile()
        {

            byte[] data;
            try
            {
                XmlDocument myXmlDoc = CreateRootNode("inform");
                if (myXmlDoc == null) return Encoding.UTF8.GetBytes(""); 

                XmlElement rootElement = myXmlDoc.DocumentElement;

                XmlElement levelElement2 = myXmlDoc.CreateElement("SOAP-ENV:Body", XMLNS_SOAPENV);
                rootElement.AppendChild(levelElement2);

                XmlElement levelElement21 = myXmlDoc.CreateElement("cwmp", RPCMethod.InformResponse, XMLNS_CWMP);
                levelElement2.AppendChild(levelElement21);
                XmlElement levelElement211 = myXmlDoc.CreateElement("MaxEnvelopes");
                levelElement211.InnerText = "1";
                levelElement21.AppendChild(levelElement211);

                //将xml文件保存到指定的路径下
                //myXmlDoc.Save("d://data2.xml");

                MemoryStream ms = new MemoryStream();
                myXmlDoc.Save(ms);
                data = ms.ToArray();

            }
            catch (Exception ex)
            {
                //Console.WriteLine(ex.ToString());
                String err = ex.ToString();
                data = Encoding.UTF8.GetBytes("");
            }
            return data;
        }

        static public byte[] CreateTransferCompleteResponseXmlFile()
        {

            byte[] data;
            try
            {
                XmlDocument myXmlDoc = CreateRootNode("inform");
                if (myXmlDoc == null) return Encoding.UTF8.GetBytes("");

                XmlElement rootElement = myXmlDoc.DocumentElement;

                XmlElement levelElement2 = myXmlDoc.CreateElement("SOAP-ENV:Body", XMLNS_SOAPENV);
                rootElement.AppendChild(levelElement2);

                XmlElement levelElement21 = myXmlDoc.CreateElement("cwmp", RPCMethod.TransferCompleteResponse, XMLNS_CWMP);
                levelElement2.AppendChild(levelElement21);
                //XmlElement levelElement211 = myXmlDoc.CreateElement("MaxEnvelopes");
                //levelElement211.InnerText = "1";
                //levelElement21.AppendChild(levelElement211);

                //将xml文件保存到指定的路径下
                //myXmlDoc.Save("d://data2.xml");

                MemoryStream ms = new MemoryStream();
                myXmlDoc.Save(ms);
                data = ms.ToArray();

            }
            catch (Exception ex)
            {
                //Console.WriteLine(ex.ToString());
                String err = ex.ToString();
                data = Encoding.UTF8.GetBytes("");
            }
            return data;
        }

        static public byte[] Create_1BOOT_InformResponse(List<XmlParameter> inList)
        {
            byte[] data;

            String Id = SetParameterValueFor1Boot;
            List<XmlParameter> parameterList = new List<XmlParameter>(inList);
            //XmlParameter xmlParameter1 = new XmlParameter("FAP.PerfMgmt.Config.1.Enable", "1");
            //parameterList.Add(xmlParameter1);
            //XmlParameter xmlParameter2 = new XmlParameter("FAP.PerfMgmt.Config.1.URL", GlobalParameter.UploadServerUrl);
            //parameterList.Add(xmlParameter2);
            //XmlParameter xmlParameter3 = new XmlParameter("FAP.PerfMgmt.Config.1.Username", GlobalParameter.UploadServerUser);
            //parameterList.Add(xmlParameter3);
            //XmlParameter xmlParameter4 = new XmlParameter("FAP.PerfMgmt.Config.1.Password", GlobalParameter.UploadServerPasswd);
            //parameterList.Add(xmlParameter4);

            //XmlParameter xmlParameter5 = new XmlParameter("FAP.PerfMgmt.Config.1.PeriodicUploadInterval", "900");
            //parameterList.Add(xmlParameter5);
            XmlParameter xmlParameter6 = new XmlParameter("ManagementServer.PeriodicInformEnable", "1");
            parameterList.Add(xmlParameter6);
            XmlParameter xmlParameter7 = new XmlParameter("ManagementServer.PeriodicInformInterval", "180");
            parameterList.Add(xmlParameter7);
            XmlParameter xmlParameter8 = new XmlParameter("ManagementServer.ConnectionRequestUsername", GlobalParameter.ConnectionRequestUsername);
            parameterList.Add(xmlParameter8);
            XmlParameter xmlParameter9 = new XmlParameter("ManagementServer.ConnectionRequestPassword", GlobalParameter.ConnectionRequestPassWd);
            parameterList.Add(xmlParameter9);
            XmlParameter xmlParameter10 = new XmlParameter("Time.NTPServer1", GlobalParameter.Ntp1ServerPath);
            parameterList.Add(xmlParameter10);
            XmlParameter xmlParameter11 = new XmlParameter("Time.NTPServer2", GlobalParameter.Ntp2ServerPath);
            parameterList.Add(xmlParameter11);
            
            data = XmlHandle.CreateSetParameterValuesXmlFile(Id, parameterList);

            return data;
        }

        static public byte[] CreateDownloadXmlFile(String id,String CommandKey,int DelaySeconds, long fileSize,String FileName, DownloadFileType fileType)
        {
            byte[] data;
            try
            {
                //String filePath = GlobalParameter.UploadServerRootPath + FileName; //升级包存放路径。

                //FileInfo fileInfo = new FileInfo(filePath);
                //long fileSize = fileInfo.Length;

                if (fileSize <= 0)
                {
                    Log.WriteError("升级文件大小为(" + fileSize + ")错误。");
                    return Encoding.UTF8.GetBytes("");
                }

                if (string.IsNullOrEmpty(FileName))
                {
                    Log.WriteError("升级文件名为空。");
                    return Encoding.UTF8.GetBytes("");
                }

                String url = GlobalParameter.UploadServerUrl;
                if (url.Substring(url.Length - 1).Equals("/"))
                {
                    url = url + "patch/" + FileName;
                }
                else
                {
                    url = url = url + "/patch/" + FileName;
                }

                XmlDocument myXmlDoc = CreateRootNode(id);
                if (myXmlDoc == null) return Encoding.UTF8.GetBytes(""); ;

                XmlElement rootElement = myXmlDoc.DocumentElement;

                XmlElement levelElement2 = myXmlDoc.CreateElement("SOAP-ENV:Body", XMLNS_SOAPENV);
                rootElement.AppendChild(levelElement2);

                XmlElement levelElement21 = myXmlDoc.CreateElement("cwmp", RPCMethod.Download, XMLNS_CWMP);
                levelElement2.AppendChild(levelElement21);

                List<XmlParameter> parList = new List<XmlParameter>();
                parList.Add(new XmlParameter("CommandKey", CommandKey));
                if (fileType == DownloadFileType.FirmwareUpgradeImage)
                {
                    parList.Add(new XmlParameter("FileType", "1 Firmware Upgrade Image"));
                }
                else
                {
                    Log.WriteWarning("升级文件类型(" + fileType + ")不支持！");
                    return Encoding.UTF8.GetBytes("");
                }
                parList.Add(new XmlParameter("URL", url));
                parList.Add(new XmlParameter("Username", GlobalParameter.UploadServerUser));
                parList.Add(new XmlParameter("Password", GlobalParameter.UploadServerPasswd));
                parList.Add(new XmlParameter("FileSize", fileSize.ToString()));
                parList.Add(new XmlParameter("DelaySeconds", DelaySeconds.ToString()));
                parList.Add(new XmlParameter("TargetFileName", ""));
                parList.Add(new XmlParameter("SuccessURL", ""));
                parList.Add(new XmlParameter("FailureURL", ""));

                foreach (XmlParameter par in parList)
                {
                    XmlElement levelElement211 = myXmlDoc.CreateElement(par.name);
                    if (!string.IsNullOrEmpty(par.value))
                    {
                        levelElement211.InnerText = par.value;
                    }
                    levelElement21.AppendChild(levelElement211);
                }
          
                //将xml文件保存到指定的路径下
                //myXmlDoc.Save("d://data2.xml");

                MemoryStream ms = new MemoryStream();
                myXmlDoc.Save(ms);
                data = ms.ToArray();

            }
            catch (Exception ex)
            {
                //Console.WriteLine(ex.ToString());
                String err = ex.ToString();
                data = Encoding.UTF8.GetBytes("");
            }
            return data;
        }

        static public byte[] CreateUploadXmlFile(String id, String CommandKey, int DelaySeconds, String FileName, UploadFileType fileType)
        {
            byte[] data;
            try
            {
                String url =  GlobalParameter.UploadServerUrl;
                if (url.Substring(url.Length-1).Equals("/"))
                {
                    url = url + "log/" + FileName;
                }
                else
                {
                    url = url = url + "/log/" + FileName;
                }
                XmlDocument myXmlDoc = CreateRootNode(id);
                if (myXmlDoc == null)
                {
                    Log.WriteWarning("CreateUploadXmlFile获取根节点失败！");
                    return Encoding.UTF8.GetBytes("");
                }

                XmlElement rootElement = myXmlDoc.DocumentElement;

                XmlElement levelElement2 = myXmlDoc.CreateElement("SOAP-ENV:Body", XMLNS_SOAPENV);
                rootElement.AppendChild(levelElement2);

                XmlElement levelElement21 = myXmlDoc.CreateElement("cwmp",RPCMethod.Upload, XMLNS_CWMP);
                levelElement2.AppendChild(levelElement21);
                XmlElement levelElement211 = myXmlDoc.CreateElement("CommandKey");
                levelElement211.InnerText = CommandKey;
                levelElement21.AppendChild(levelElement211);
                
                XmlElement levelElement212 = myXmlDoc.CreateElement("FileType");
                if (fileType == UploadFileType.VendorConfigurationFile)
                {
                    levelElement212.InnerText = "1 Vendor Configuration File";
                }
                else if (fileType == UploadFileType.VendorLogFile)
                {
                    levelElement212.InnerText = "2 Vendor Log File";
                }
                else
                {
                    Log.WriteWarning("获取日志类型("+  fileType +")不支持！");
                    return Encoding.UTF8.GetBytes("");
                }
                levelElement21.AppendChild(levelElement212);
                XmlElement levelElement213 = myXmlDoc.CreateElement("URL");
                levelElement213.InnerText = url;
                levelElement21.AppendChild(levelElement213);
                XmlElement levelElement214 = myXmlDoc.CreateElement("Username");
                levelElement214.InnerText = GlobalParameter.UploadServerUser;
                levelElement21.AppendChild(levelElement214);
                XmlElement levelElement215 = myXmlDoc.CreateElement("Password");
                levelElement215.InnerText = GlobalParameter.UploadServerPasswd;
                levelElement21.AppendChild(levelElement215);
                XmlElement levelElement217 = myXmlDoc.CreateElement("DelaySeconds");
                levelElement217.InnerText = DelaySeconds.ToString();
                levelElement21.AppendChild(levelElement217);

                //将xml文件保存到指定的路径下
                //myXmlDoc.Save("d://data2.xml");

                MemoryStream ms = new MemoryStream();
                myXmlDoc.Save(ms);
                data = ms.ToArray();

            }
            catch (Exception ex)
            {
                //Console.WriteLine(ex.ToString());
                String err = ex.ToString();
                data = Encoding.UTF8.GetBytes("");
            }
            return data;
        }

        static public byte[] CreateGetParameterValuesXmlFile(String id, String[] ParameterName)
        {
            byte[] data;
            try
            {
                XmlDocument myXmlDoc = CreateRootNode(id);
                //XmlDocument myXmlDoc = CreateRootNode("ID:intrnl.unset.id.GetParameterValues1503549144995.1742124437");
                if (myXmlDoc == null) return Encoding.UTF8.GetBytes(""); ;

                XmlElement rootElement = myXmlDoc.DocumentElement;

                XmlElement levelElement2 = myXmlDoc.CreateElement("SOAP-ENV:Body", XMLNS_SOAPENV);
                rootElement.AppendChild(levelElement2);

                XmlElement levelElement21 = myXmlDoc.CreateElement("cwmp", RPCMethod.GetParameterValues, XMLNS_CWMP);
                levelElement2.AppendChild(levelElement21);
                XmlElement levelElement211 = myXmlDoc.CreateElement("ParameterNames");
                levelElement211.SetAttribute("arrayType", XMLNS_SOAPENC, "xsd:string[" + ParameterName.Length + "]");
                levelElement21.AppendChild(levelElement211);
                
                foreach (String name in ParameterName)
                {
                    XmlElement levelElement2111 = myXmlDoc.CreateElement("string");
                    levelElement2111.InnerText = GlobalParameter.XmlRootNode + name;
                    levelElement211.AppendChild(levelElement2111);
                }


                //将xml文件保存到指定的路径下
                //myXmlDoc.Save("d://data2.xml");

                MemoryStream ms = new MemoryStream();
                myXmlDoc.Save(ms);
                data = ms.ToArray();

            }
            catch (Exception ex)
            {
                //Console.WriteLine(ex.ToString());
                String err = ex.ToString();
                data = Encoding.UTF8.GetBytes(""); 
            }
            return data;
        }

        static public byte[] CreateSetParameterValuesXmlFile(String id, List<XmlParameter> parameterList)
        {
            byte[] data;
            try
            {
                XmlDocument myXmlDoc = CreateRootNode(id);
                if (myXmlDoc == null) return Encoding.UTF8.GetBytes(""); 

                XmlElement rootElement = myXmlDoc.DocumentElement;

                XmlElement levelElement2 = myXmlDoc.CreateElement("SOAP-ENV:Body", XMLNS_SOAPENV);
                rootElement.AppendChild(levelElement2);

                XmlElement levelElement21 = myXmlDoc.CreateElement("cwmp", RPCMethod.SetParameterValues, XMLNS_CWMP);
                levelElement2.AppendChild(levelElement21);
                XmlElement levelElement211 = myXmlDoc.CreateElement("ParameterList");
                levelElement211.SetAttribute("arrayType", XMLNS_SOAPENC, "cwmp:ParameterValueStruct[" + parameterList.Count + "]");
                levelElement21.AppendChild(levelElement211);

                foreach (XmlParameter parameter in parameterList)
                {
                    XmlElement levelElement_struct = myXmlDoc.CreateElement("ParameterValueStruct");
                    levelElement211.AppendChild(levelElement_struct);
                    XmlElement levelElement_name = myXmlDoc.CreateElement("Name");
                    //levelElement_name.SetAttribute("type", XMLNS_XSI, "xsd:string");
                    levelElement_name.InnerText = GlobalParameter.XmlRootNode + parameter.name;
                    levelElement_struct.AppendChild(levelElement_name);
                    XmlElement levelElement_Value = myXmlDoc.CreateElement("Value");
                    levelElement_Value.SetAttribute("type", XMLNS_XSI, "string");
                    levelElement_Value.InnerText = parameter.value;
                    levelElement_struct.AppendChild(levelElement_Value);
                }

                XmlElement levelElement212 = myXmlDoc.CreateElement("ParameterKey");
                //levelElement212.SetAttribute("type", XMLNS_XSI, "xsd:string");
                levelElement212.InnerText = "unsetCommandKey";
                levelElement21.AppendChild(levelElement212);

                //将xml文件保存到指定的路径下
                //myXmlDoc.Save("d://data2.xml");

                MemoryStream ms = new MemoryStream();
                myXmlDoc.Save(ms);
                data = ms.ToArray();

            }
            catch (Exception ex)
            {
                //Console.WriteLine(ex.ToString());
                String err = ex.ToString();
                data = Encoding.UTF8.GetBytes("");
            }
            return data;
        }

        static public byte[] CreateRebootXmlFile(String id)
        {
            byte[] data;
            try
            {
                XmlDocument myXmlDoc = CreateRootNode(id);
                if (myXmlDoc == null) return Encoding.UTF8.GetBytes(""); ;

                XmlElement rootElement = myXmlDoc.DocumentElement;

                XmlElement levelElement2 = myXmlDoc.CreateElement("SOAP-ENV:Body", XMLNS_SOAPENV);
                rootElement.AppendChild(levelElement2);

                XmlElement levelElement21 = myXmlDoc.CreateElement("cwmp", RPCMethod.Reboot, XMLNS_CWMP);
                levelElement2.AppendChild(levelElement21);
                XmlElement levelElement211 = myXmlDoc.CreateElement("CommandKey");
                levelElement211.InnerText = id;
                levelElement21.AppendChild(levelElement211);

                //将xml文件保存到指定的路径下
                //myXmlDoc.Save("d://data2.xml");

                MemoryStream ms = new MemoryStream();
                myXmlDoc.Save(ms);
                data = ms.ToArray();

            }
            catch (Exception ex)
            {
                //Console.WriteLine(ex.ToString());
                String err = ex.ToString();
                data = Encoding.UTF8.GetBytes("");
            }
            return data;
        }

        #endregion

        #region 测试消息

        static public byte[] XmlGetLocalFile()
        {
            XmlMethodStruct xmlStruct = new XmlMethodStruct();

            // MessageBox.Show(msg);
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load("d:\\boot.xml");

            MemoryStream ms = new MemoryStream();
            xmlDoc.Save(ms);
            return ms.ToArray();
        }

        static public String XmlDecodeTest()
        {
            String str="";
  
            XmlMethodStruct xmlStruct = Get_Xml_Msg_Method(null);

            //MessageBox.Show("Method:" + xmlStruct.Method);

            if (String.Compare(xmlStruct.Method, RPCMethod.Inform, true) == 0)
            {
                XmlInform xmlInform = Get_Xml_Msg_Inform(xmlStruct);

                str = str + xmlInform.OUI + "\r\n";
                str = str + xmlInform.SN + "\r\n";
                str = str + xmlInform.Imsi + "\r\n";
                str = str + xmlInform.ProductClass + "\r\n";
                str = str + xmlInform.Manufacturer + "\r\n";
                str = str + xmlInform.CurrentTime + "\r\n";
                foreach (String eventCode in xmlInform.EventCode)
                {
                    str = str + eventCode + "\r\n";
                }
            }
            else if (String.Compare(xmlStruct.Method, RPCMethod.GetParameterValuesResponse, true) == 0)
            {
                List<XmlParameter> parameterNode = Get_Xml_Msg_GetParameterValuesResponse(xmlStruct);

                foreach (XmlParameter temp in parameterNode)
                {
                    str = str + temp.name + " " + temp.value + " " + temp.valueType + " " + "\r\n";
                }
            }
            else if (String.Compare(xmlStruct.Method, RPCMethod.TransferComplete, true) == 0)
            {
                XmlTransferComplete transferComplete = Get_Xml_Msg_TransferComplete(xmlStruct);

                str = str + transferComplete.CommandKey + "\r\n";
                str = str + transferComplete.StartTime + "\r\n";
                str = str + transferComplete.CompleteTime + "\r\n";
                str = str + transferComplete.FaultCode + "\r\n";
                str = str + transferComplete.FaultString + "\r\n";
            }
            else if (String.Compare(xmlStruct.Method, RPCMethod.Fault, true) == 0)
            {
                XmlFault xmlFault = Get_Xml_Msg_Fault(xmlStruct);

                str = str + xmlFault.FaultCode + "\r\n";
                str = str + xmlFault.FaultString + "\r\n";
            }
            Log.WriteInfo("str");
            return str;
        }

        #endregion


    }
}
