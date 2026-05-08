using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace MapEditor.Services
{
    /// <summary>
    /// 负责从文件夹加载900张tile图
    /// 支持 png/jpg/bmp，按文件名数字排序
    /// </summary>
    public class TileLoader : IDisposable
    {
        private readonly List<Image> _tiles = new List<Image>();
        private bool _disposed;

        public IReadOnlyList<Image> Tiles => _tiles.AsReadOnly();
        public int Count => _tiles.Count;
        public int TileWidth { get; private set; } = 32;
        public int TileHeight { get; private set; } = 32;

        /// <summary>
        /// 从指定文件夹加载tile图
        /// </summary>
        public LoadResult Load(string folderPath)
        {
            if (!Directory.Exists(folderPath))
                return new LoadResult(false, $"文件夹不存在: {folderPath}");

            // 支持的图片格式
            var extensions = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp" };
            var files = new List<string>();

            foreach (var ext in extensions)
                files.AddRange(Directory.GetFiles(folderPath, ext));

            if (files.Count == 0)
                return new LoadResult(false, "文件夹内没有找到图片文件");

            // 按文件名中的数字排序（tile_0.png, tile_1.png ... 或 0.png, 1.png ...）
            files.Sort((a, b) =>
            {
                int na = ExtractNumber(Path.GetFileNameWithoutExtension(a));
                int nb = ExtractNumber(Path.GetFileNameWithoutExtension(b));
                return na.CompareTo(nb);
            });

            // 释放旧数据
            foreach (var img in _tiles) img.Dispose();
            _tiles.Clear();

            int loaded = 0;
            var errors = new List<string>();

            foreach (var file in files)
            {
                try
                {
                    var bmp = new Bitmap(file);
                    _tiles.Add(bmp);

                    // 用第一张图确定tile尺寸
                    if (loaded == 0)
                    {
                        TileWidth = bmp.Width;
                        TileHeight = bmp.Height;
                    }
                    loaded++;
                }
                catch (Exception ex)
                {
                    errors.Add($"{Path.GetFileName(file)}: {ex.Message}");
                }
            }

            string msg = $"成功加载 {loaded} 张tile图 ({TileWidth}×{TileHeight}px)";
            if (errors.Count > 0)
                msg += $"\n失败 {errors.Count} 张";

            return new LoadResult(loaded > 0, msg, loaded);
        }

        public Image GetTile(int index)
        {
            if (index < 0 || index >= _tiles.Count) return null;
            return _tiles[index];
        }

        private static int ExtractNumber(string name)
        {
            // 从文件名末尾提取数字，如 "tile_042" → 42
            int i = name.Length - 1;
            while (i >= 0 && char.IsDigit(name[i])) i--;
            string numPart = name.Substring(i + 1);
            return int.TryParse(numPart, out int n) ? n : 0;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                foreach (var img in _tiles) img?.Dispose();
                _tiles.Clear();
                _disposed = true;
            }
        }
    }

    public class LoadResult
    {
        public bool Success { get; }
        public string Message { get; }
        public int Count { get; }

        public LoadResult(bool success, string message, int count = 0)
        {
            Success = success;
            Message = message;
            Count = count;
        }
    }
}
