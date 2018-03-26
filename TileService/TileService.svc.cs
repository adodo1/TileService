using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;

namespace WCFService
{
    /// <summary>
    /// 
    /// </summary>
    public class TileService : ITileService
    {
        private static string BASE_DIR = AppDomain.CurrentDomain.BaseDirectory + "/Tiles/";
        private static Dictionary<string, Dictionary<string, string>> _headers = new Dictionary<string, Dictionary<string, string>>();

        /// <summary>
        /// 获取瓦片
        /// </summary>
        /// <param name="name">离线地图包名称</param>
        /// <param name="x">瓦片X</param>
        /// <param name="y">瓦片Y</param>
        /// <param name="z">等级Z</param>
        /// <returns></returns>
        public Stream GetTile(string name, string x, string y, string z)
        {
            // 添加响应的头部消息
            // Content-Type: image/jpeg
            // Content-Encoding: gzip
            Dictionary<string, string> headers = GetHeaders(name);
            if (headers != null) {
                foreach (KeyValuePair<string, string> header in headers) {
                    WebOperationContext.Current.OutgoingResponse.Headers[header.Key] = header.Value;
                }
            }

            //MemoryStream stream = new MemoryStream(new byte[] { 10, 13, 65 });
            //return stream;
            int xx, yy, zz;
            if (int.TryParse(x, out xx) == false ||
                int.TryParse(y, out yy) == false ||
                int.TryParse(z, out zz) == false) return null;
            //
            string mbtiles = string.Format("{0}{1}.mbtiles", BASE_DIR, name);
            if (System.IO.File.Exists(mbtiles) == false) return null;
            SQLiteHelper.SetConnectionString = string.Format("Data Source=\"{0}\"", mbtiles);
            //
            string sql = "select TILE_DATA from TILES where ZOOM_LEVEL=@ZOOM_LEVEL and TILE_COLUMN=@TILE_COLUMN and TILE_ROW=@TILE_ROW";
            SQLiteParameter[] args = new SQLiteParameter[3];
            args[0] = new SQLiteParameter("@ZOOM_LEVEL", zz);
            args[1] = new SQLiteParameter("@TILE_COLUMN", yy);
            args[2] = new SQLiteParameter("@TILE_ROW", xx);
            object value = SQLiteHelper.ExecuteScalar(CommandType.Text, sql, args);
            if (value == null || Convert.IsDBNull(value)) return null;
            //
            byte[] data = (byte[])value;
            if (data[0] == 0x1f && data[1] == 0x8b) {
                // 如果是压缩数据就算没有headers定义也添加一条GZIP的头部
                // 浏览器前端回去解压的
                WebOperationContext.Current.OutgoingResponse.Headers["Content-Encoding"] = "gzip";
            }
            //
            MemoryStream stream = new MemoryStream((byte[])value);
            return stream;
        }
        /// <summary>
        /// 从mbtile数据库中读取读取headers表
        /// 如果读取失败返回空
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private Dictionary<string, string> GetHeaders(string name)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            try {
                if (_headers.TryGetValue(name, out result)) return result;
                // 
                string mbtiles = string.Format("{0}{1}.mbtiles", BASE_DIR, name);
                if (System.IO.File.Exists(mbtiles) == false) return null;
                SQLiteHelper.SetConnectionString = string.Format("Data Source=\"{0}\"", mbtiles);
                string sql = "select NAME, VAL from HEADERS";
                DataSet dataset = SQLiteHelper.ExecuteDataset(CommandType.Text, sql);
                foreach (DataRow row in dataset.Tables[0].Rows) {
                    string n = Convert.ToString(row[0]);
                    string v = Convert.ToString(row[1]);
                    result[n] = v;
                }
                _headers[name] = result;
                return result;
            }
            catch (Exception ex) {
                _headers[name] = result;
                return result;
            }
        }



    }
}
