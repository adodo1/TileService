using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;

namespace WCFService
{
    public class BundlxClass
    {
        private string _fname = null;
        private Dictionary<string, long> _tile_pos_dic = new Dictionary<string, long>();

        /// <summary>
        /// bundlx文件完整路径
        /// </summary>
        /// <param name="fname"></param>
        public BundlxClass(string fname)
        {
            _fname = fname;
        }
        /// <summary>
        /// 计算row行col列图像的偏移量
        /// </summary>
        /// <param name="row">row 总体行号</param>
        /// <param name="col">col 总体列号</param>
        /// <returns></returns>
        public long GetTilePosition(int row, int col)
        {
            string key = string.Format("{0},{1}", row, col);
            // 如果字典里有偏移量就直接返回
            if (_tile_pos_dic.ContainsKey(key)) return _tile_pos_dic[key];
            // 打开索引文件读取偏移量
            long position = GetIndexPostion(row, col);
            FileStream stream = new FileStream(_fname, FileMode.Open, FileAccess.Read, FileShare.Read);
            stream.Seek(position, SeekOrigin.Begin);
            BinaryReader reader = new BinaryReader(stream);
            byte[] value = reader.ReadBytes(5);     // 读取5个字节
            reader.Close();
            stream.Close();

            long result = HexToInt(value);
            _tile_pos_dic[key] = result;
            return result;
        }
        /// <summary>
        /// 计算row行col列在索引文件bundlx的偏移量
        /// </summary>
        /// <param name="row">row 总体行号</param>
        /// <param name="col">col 总体列号</param>
        /// <returns></returns>
        public long GetIndexPostion(int row, int col)
        {
            row = row % 128;
            col = col % 128;
            int base_pos = 16 + col * 5 * 128;
            int offset = row * 5;
            int position = base_pos + offset;
            return position;
        }
        /// <summary>
        /// 字节转整形
        /// 例如: 0xFF00000000
        /// 反序: 0x00000000FF
        /// 再转成整数: 255
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public int HexToInt(byte[] value)
        {
            int result = (value[4] & 0xFF) << 32 |
               (value[3] & 0xFF) << 24 |
               (value[2] & 0xFF) << 16 |
               (value[1] & 0xFF) << 8 |
               (value[0] & 0xFF);
            return result;
        }
        /// <summary>
        /// 整形转5字节
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public byte[] IntToHex(int value)
        {
            byte[] bytes = System.BitConverter.GetBytes(value);
            byte[] result = new byte[bytes.Length + 1];
            for (int i = 0; i < bytes.Length; i++) {
                result[i] = bytes[i];
            }
            result[result.Length - 1] = 0;
            return result;
        }
        /// <summary>
        /// 创建新的索引文件
        /// </summary>
        public void CreateNew()
        {
            using (FileStream stream = new FileStream(_fname, FileMode.Create)) {
                using (BinaryWriter writer = new BinaryWriter(stream)) {
                    writer.Write(new byte[] { 0x03, 0x00, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x00, 0x40, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00 });
                    for (int n = 0; n < 128 * 128; n++) {
                        int offset = 60 + n * 4;
                        byte[] bytes = IntToHex(offset);
                        writer.Write(bytes);
                    }
                    writer.Write(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
                    writer.Flush();
                    writer.Close();
                    stream.Close();
                }
            }
        }
        /// <summary>
        /// 写入索引
        /// </summary>
        /// <param name="row"></param>
        /// <param name="col"></param>
        /// <param name="offset"></param>
        public void InsertData(int row, int col, int offset)
        {
            long position = GetIndexPostion(row, col);
            FileStream stream = new FileStream(_fname, FileMode.Append);
            BinaryWriter writer = new BinaryWriter(stream);
            stream.Seek(position, SeekOrigin.Begin);
            byte[] value = IntToHex(offset);
            writer.Write(value);
            writer.Flush();
            writer.Close();
            stream.Close();
        }

    }
}