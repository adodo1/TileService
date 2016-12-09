using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WCFService
{
    public class TileData
    {
        private Dictionary<string, BundleClass> _bundles = new Dictionary<string, BundleClass>();
        private Dictionary<string, BundlxClass> _bundlxs = new Dictionary<string, BundlxClass>();
        private string _tiledir = null;

        public TileData(string tiledir)
        {
            _tiledir = tiledir;
        }
        /// <summary>
        /// 通过等级 行号 列号获取集合名字
        /// </summary>
        /// <param name="row"></param>
        /// <param name="col"></param>
        /// <param name="level"></param>
        /// <returns></returns>
        public string GetBundleName(int row, int col, int level)
        {
            row = (int)(row / 128);
            row = row * 128;
            col = (int)(col / 128);
            col = col * 128;
            string rowstr = row.ToString("0000");
            string colstr = col.ToString("0000");
            string filename = string.Format("R{0}C{1}", rowstr, colstr);
            string dirname = string.Format("L{0}", level.ToString("00"));

            string bundlename = dirname + "/" + filename;
            return bundlename;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="row"></param>
        /// <param name="col"></param>
        /// <returns></returns>
        public int[] GetBundleRowCol(int row, int col)
        {
            row = (int)(row / 128);
            row = row * 128;
            col = (int)(col / 128);
            col = col * 128;
            return new int[] { row, col };
        }
        /// <summary>
        /// 读取瓦片数据
        /// </summary>
        /// <param name="row">row 总体行号</param>
        /// <param name="col">col 总体列号</param>
        /// <param name="level">level 等级</param>
        /// <returns></returns>
        public byte[] ReadTile(int row, int col, int level)
        {
            string name = GetBundleName(row, col, level);
            string bundlename = _tiledir + "/" + name + ".bundle";
            string bundlxname = _tiledir + "/" + name + ".bundlx";

            bundlename = System.IO.Path.GetFullPath(bundlename);
            bundlxname = System.IO.Path.GetFullPath(bundlxname);

            if (System.IO.File.Exists(bundlename) == false ||
                System.IO.File.Exists(bundlxname) == false) return null;

            BundleClass bundleClass = null;
            BundlxClass bundlxClass = null;
            if (_bundles.TryGetValue(bundlename, out bundleClass) == false) {
                bundleClass = new BundleClass(bundlename);
                _bundles[bundlename] = bundleClass;
            }
            if (_bundlxs.TryGetValue(bundlxname, out bundlxClass) == false) {
                bundlxClass = new BundlxClass(bundlxname);
                _bundlxs[bundlxname] = bundlxClass;
            }

            // 
            long position = bundlxClass.GetTilePosition(row, col);
            byte[] image = bundleClass.GetTileImage(position);
            return image;
        }
        /// <summary>
        /// 写入瓦片数据
        /// </summary>
        /// <param name="row"></param>
        /// <param name="col"></param>
        /// <param name="level"></param>
        /// <param name="data"></param>
        public void WriteTile(int row, int col, int level, byte[] data)
        {
            string name = GetBundleName(row, col, level);
            string bundlename = _tiledir + "/" + name + ".bundle";
            string bundlxname = _tiledir + "/" + name + ".bundlx";

            string basedir = System.IO.Path.GetDirectoryName(bundlename);
            if (System.IO.Directory.Exists(basedir) == false) {
                System.IO.Directory.CreateDirectory(basedir);
            }

            BundleClass bundle_class = null;
            BundlxClass bundlx_class = null;
            if (_bundles.TryGetValue(bundlename, out bundle_class) == false) {
                bundle_class = new BundleClass(bundlename);
                _bundles[bundlename] = bundle_class;
            }
            if (_bundlxs.TryGetValue(bundlxname, out bundlx_class) == false) {
                bundlx_class = new BundlxClass(bundlxname);
                _bundlxs[bundlxname] = bundlx_class;
            }

            if (System.IO.File.Exists(bundlename) == false) {
                // 创建新瓦片集
                int[] starts = GetBundleRowCol(row, col);
                bundle_class.CreateNew(starts[0], starts[1]);
            }
            if (System.IO.File.Exists(bundlxname) == false) {
                // 创建新的索引
                bundlx_class.CreateNew();
            }

            // 先写瓦片
            int offset = (int)bundle_class.InsertData(data);
            // 修改索引
            bundlx_class.InsertData(row, col, offset);
        }

    }
}