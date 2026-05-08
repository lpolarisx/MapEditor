using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MapEditor.Services
{
    /// <summary>
    /// 负责从文件夹加载900张tile图
    /// 支持 png/jpg/bmp，按文件名数字排序
    /// </summary>
    public class TileLoader : IDisposable
    {
        private const int MapTileCacheSize = 256;
        private readonly List<string> _tileFiles = new List<string>();
        private readonly Dictionary<int, Image> _mapTileCache = new Dictionary<int, Image>();
        private readonly Dictionary<int, Image> _previewCache = new Dictionary<int, Image>();
        private readonly object _syncRoot = new object();
        private bool _disposed;

        public IReadOnlyList<Image> Tiles
        {
            get
            {
                lock (_syncRoot)
                    return new List<Image>(_mapTileCache.Values).AsReadOnly();
            }
        }
        public int Count
        {
            get
            {
                lock (_syncRoot)
                    return _tileFiles.Count;
            }
        }
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

            int loaded = 0;
            var errors = new List<string>();
            var loadedFiles = new List<string>();
            int tileWidth = TileWidth;
            int tileHeight = TileHeight;

            foreach (var file in files)
            {
                try
                {
                    // 用第一张图确定tile尺寸
                    if (loaded == 0)
                    {
                        using var original = new Bitmap(file);
                        tileWidth = original.Width;
                        tileHeight = original.Height;
                    }

                    loadedFiles.Add(file);
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

            if (loaded > 0)
            {
                lock (_syncRoot)
                {
                    ClearImages();
                    _tileFiles.AddRange(loadedFiles);
                    TileWidth = tileWidth;
                    TileHeight = tileHeight;
                }
            }

            return new LoadResult(loaded > 0, msg, loaded);
        }

        public Image GetTile(int index)
        {
            string file;
            lock (_syncRoot)
            {
                if (index < 0 || index >= _tileFiles.Count) return null;
                if (_previewCache.TryGetValue(index, out var cached)) return cached;
                file = _tileFiles[index];
            }

            try
            {
                var image = new Bitmap(file);
                lock (_syncRoot)
                {
                    if (_previewCache.TryGetValue(index, out var cached))
                    {
                        image.Dispose();
                        return cached;
                    }

                    _previewCache[index] = image;
                }
                return image;
            }
            catch
            {
                return null;
            }
        }

        public Image GetMapTile(int index)
        {
            string file;
            lock (_syncRoot)
            {
                if (index < 0 || index >= _tileFiles.Count) return null;
                if (_mapTileCache.TryGetValue(index, out var cached)) return cached;
                file = _tileFiles[index];
            }

            try
            {
                using var original = new Bitmap(file);
                var image = CreateMapTile(original);
                lock (_syncRoot)
                {
                    if (_mapTileCache.TryGetValue(index, out var cached))
                    {
                        image.Dispose();
                        return cached;
                    }

                    _mapTileCache[index] = image;
                }
                return image;
            }
            catch
            {
                return null;
            }
        }

        public Task PreloadMapTilesAsync(CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                int count = Count;
                for (int i = 0; i < count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    GetMapTile(i);
                }
            }, cancellationToken);
        }

        private static Image CreateMapTile(Image original)
        {
            int maxSide = Math.Max(original.Width, original.Height);
            if (maxSide <= MapTileCacheSize)
                return new Bitmap(original);

            float scale = MapTileCacheSize / (float)maxSide;
            int width = Math.Max(1, (int)Math.Round(original.Width * scale));
            int height = Math.Max(1, (int)Math.Round(original.Height * scale));

            var bitmap = new Bitmap(width, height);
            using var g = Graphics.FromImage(bitmap);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            g.DrawImage(original, new Rectangle(0, 0, width, height));
            return bitmap;
        }

        private void ClearImages()
        {
            foreach (var img in _mapTileCache.Values) img.Dispose();
            foreach (var img in _previewCache.Values) img.Dispose();

            _tileFiles.Clear();
            _mapTileCache.Clear();
            _previewCache.Clear();
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
                lock (_syncRoot)
                    ClearImages();
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
