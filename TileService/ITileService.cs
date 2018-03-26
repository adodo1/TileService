using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;
using System.ServiceModel.Web;
using System.IO;

namespace WCFService
{
    // 注意: 使用“重构”菜单上的“重命名”命令，可以同时更改代码和配置文件中的接口名“IService”。
    [ServiceContract]
    public interface ITileService
    {
        [OperationContract]
        [WebInvoke(Method = "GET", UriTemplate = "{name}/{x}/{y}/{z}")]
        Stream GetTile(string name, string x, string y, string z);

    }
}


