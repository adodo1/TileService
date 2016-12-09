using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;

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

    public class BundleClass
    {
        private string _fname = null;
        /// <summary>
        /// bundle文件完整路径
        /// </summary>
        /// <param name="fname"></param>
        public BundleClass(string fname)
        {
            _fname = fname;
        }
        /// <summary>
        /// 获取单张瓦片图像
        /// </summary>
        /// <param name="position">偏移量</param>
        /// <returns></returns>
        public byte[] GetTileImage(long position)
        {
            using (FileStream stream = new FileStream(_fname, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                stream.Seek(position, SeekOrigin.Begin);
                using (BinaryReader reader = new BinaryReader(stream)) {
                    int size = reader.ReadInt32();
                    byte[] result = reader.ReadBytes(size);
                    reader.Close();
                    stream.Close();
                    return result;
                }
            }
        }
        /// <summary>
        /// 创建新的瓦片存储文件
        /// </summary>
        /// <param name="start_row"></param>
        /// <param name="start_col"></param>
        public void CreateNew(int start_row, int start_col)
        {
            using (FileStream stream = new FileStream(_fname, FileMode.Create)) {
                using (BinaryWriter writer = new BinaryWriter(stream)) {
                    writer.Write(new byte[] { 0x03, 0x00, 0x00, 0x00, 0x00, 0x40, 0x00, 0x00 });
                    writer.Write(new byte[] { 0x00, 0x00, 0x00, 0x00 });
                    writer.Write(new byte[] { 0x05, 0x00, 0x00, 0x00 });
                    writer.Write(new byte[] { 0x00, 0x00, 0x00, 0x00 });
                    writer.Write(new byte[] { 0x00, 0x00, 0x00, 0x00 });
                    writer.Write((int)(60 + 4 * 128 * 128));
                    writer.Write(new byte[] { 0x00, 0x00, 0x00, 0x00 });
                    writer.Write(new byte[] { 0x28, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00 });
                    writer.Write((int)start_row);
                    writer.Write((int)(start_row + 128 - 1));
                    writer.Write((int)start_col);
                    writer.Write((int)(start_col + 128 - 1));
                    writer.Write(new byte[4 * 128 * 128]);

                    writer.Close();
                    stream.Close();
                }
            }
        }
        /// <summary>
        ///  插入图片 并返回插入位置
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
        public long InsertData(byte[] image)
        {
            int size = image.Length;
            long file_size = new FileInfo(_fname).Length + size + 4;
            using (FileStream stream = new FileStream(_fname, FileMode.Append)) {
                BinaryReader reader = new BinaryReader(stream);
                BinaryWriter writer = new BinaryWriter(stream);
                // 处理最大数据大小
                stream.Seek(8, SeekOrigin.Begin);
                int maxsize = reader.ReadInt32();
                if (maxsize < size) { maxsize = size; }
                stream.Seek(8, SeekOrigin.Begin);
                writer.Write(maxsize);
                // 处理非空数量
                stream.Seek(16, SeekOrigin.Begin);
                int nonullcount = reader.ReadInt32();
                nonullcount = ((nonullcount / 4) + 1) * 4;
                stream.Seek(16, SeekOrigin.Begin);
                writer.Write(nonullcount);
                // 处理文件大小
                stream.Seek(24, SeekOrigin.Begin);
                writer.Write((int)file_size);

                // 文件末尾写入图片数据
                long position = stream.Seek(0, SeekOrigin.End);
                writer.Write(size);
                writer.Write(image);

                writer.Flush();
                writer.Close();
                reader.Close();
                stream.Close();

                return position;
            }
        }
    }
}