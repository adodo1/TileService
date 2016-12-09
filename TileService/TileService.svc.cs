using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;

namespace WCFService
{
    //# PS:
    //# 瓦片索引从左上角开始(0,0)
    //# 先列 再行

    //# 参考:
    //# https://github.com/andrewmagill/unbundler
    //# http://www.cnblogs.com/yuantf/p/3320876.html
    //# https://github.com/sainsb/tilecannon > TileController.cs
    //# https://github.com/F-Sidney/SharpMapTileLayer > LocalTileCacheLayer.cs

    //# bundle文件
    //# 前60字节
    //# 00-07: 固定 0300000000400000
    //# 08-11: 最大的一块瓦片大小
    //# 12-15: 固定 05000000
    //# 16-19: 非空瓦片数量 * 4
    //# 20-23: 未知 00000000
    //# 24-27: 文件大小
    //# 28-31: 未知 00000000
    //# 32-43: 固定 280000000000000010000000
    //# 44-47: 开始行
    //# 48-51: 结束行
    //# 52-55: 开始列
    //# 56-59: 结束列
    //# 中间伪索引 全0
    //# 4字节 * 128行 * 128列
    //# 4字节图片大小
    //# 图片数据
    //# 4字节图片大小
    //# 图片数据
    //# 4字节图片大小
    //# 图片数据
    //# ...

    //# bundlx文件
    //# 前16字节 03000000100000000040000005000000
    //# 中间每个瓦片对应偏移量 5字节 * 128行 * 128列
    //# 后16字节 00000000100000001000000000000000

    public class TileService : ITileService
    {
        private static string SEARCH_INDEX_PATH = AppDomain.CurrentDomain.BaseDirectory + "TILES/";
        private const int PACKET_SIZE = 128;    // 包大小

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
            //MemoryStream stream = new MemoryStream(new byte[] { 10, 13, 65 });
            //return stream;

            TileData tileData = new TileData(SEARCH_INDEX_PATH + name);
            int row, col, level;
            if (int.TryParse(y, out row) == false ||
                int.TryParse(x, out col) == false ||
                int.TryParse(z, out level) == false) {
                // 没有找到影像用空白影像代替
                return GetNullTile();
            }

            // 找到影像
            byte[] data = tileData.ReadTile(row, col, level);
            if (data == null || data.Length == 0) return GetNullTile();
            MemoryStream stream = new MemoryStream(data);
            return stream;
        }
        /// <summary>
        /// 返回空白的影像
        /// </summary>
        /// <returns></returns>
        private Stream GetNullTile()
        {
            string resourceName = GetType().Namespace + ".res.null.png";
            Stream stream = GetType().Assembly.GetManifestResourceStream(resourceName);
            return stream;
        }


    }
}
