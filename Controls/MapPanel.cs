using MapEditor.Models;
using MapEditor.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace MapEditor.Controls
{
    /// <summary>
    /// 地图画布控件：渲染tile网格，支持滚轮缩放，单击选中，双击大图预览
    /// </summary>
    public class MapPanel : Panel
    {
        // ── 数据 ──────────────────────────────────────────────
        private MapTile[,] _map;
        private TileLoader _tileLoader;
        private int _cols = 30;
        private int _rows = 30;
        private const int TerrainCellsPerTile = 10;
        private static readonly Dictionary<int, Color> TerrainColorCache = BuildTerrainColorCache();

        // 原始tile尺寸（图片实际px，1000×1000）
        private int _srcTileW = 64;
        private int _srcTileH = 64;

        // ── 缩放 ──────────────────────────────────────────────
        // 显示尺寸 = 原始 × _zoom，范围 0.02~0.5（对应 20px~500px）
        private float _zoom = 0.064f;          // 默认在 1000px 图上显示约 64px
        private const float ZoomMin = 0.02f;
        private const float ZoomMax = 0.5f;
        private const float ZoomStep = 1.15f;  // 每格滚轮倍率

        // 渲染用的整数tile尺寸（像素）
        private int TileW => Math.Max(1, (int)(_srcTileW * _zoom));
        private int TileH => Math.Max(1, (int)(_srcTileH * _zoom));

        public float Zoom => _zoom;

        // ── 选中状态 ──────────────────────────────────────────
        private int _selectedCol = -1;
        private int _selectedRow = -1;

        // ── 事件 ──────────────────────────────────────────────
        public event Action<MapTile> TileClicked;
        public event Action<MapTile> TileDoubleClicked;
        public event Action<float> ZoomChanged;

        // ── 外观 ─────────────────────────────────────────────
        private static readonly Pen GridPen        = new Pen(Color.FromArgb(60, 0, 0, 0), 1f);
        private static readonly Pen SelectPen      = new Pen(Color.FromArgb(220, 255, 220, 0), 2f);
        private static readonly SolidBrush TerrainBrush = new SolidBrush(Color.Transparent);
        private static readonly SolidBrush EmptyBrush   = new SolidBrush(Color.FromArgb(180, 50, 50, 50));
        private static readonly Font CoordFont = new Font("Consolas", 6f);
        private static readonly SolidBrush CoordBrush = new SolidBrush(Color.FromArgb(120, 255, 255, 255));

        public bool ShowGrid           { get; set; } = true;
        public bool ShowCoords         { get; set; } = false;
        public bool ShowTerrainOverlay { get; set; } = true;

        public MapPanel()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(20, 20, 30);
            AutoScroll = true;
            MouseClick       += OnMouseClick;
            MouseDoubleClick += OnMouseDoubleClick;
            MouseWheel       += OnMouseWheel;
        }

        // ── 初始化地图 ────────────────────────────────────────
        public void InitMap(int cols, int rows, TileLoader loader)
        {
            _cols = cols;
            _rows = rows;
            _tileLoader = loader;

            if (loader != null && loader.Count > 0)
            {
                _srcTileW = loader.TileWidth;
                _srcTileH = loader.TileHeight;
                // 初始缩放：让tile在屏幕上约 64px
                _zoom = Math.Max(ZoomMin, Math.Min(ZoomMax, 64f / _srcTileW));
            }
            else
            {
                _srcTileW = _srcTileH = 64;
                _zoom = 1f;
            }

            _map = new MapTile[cols, rows];
            var terrains = GetTerrains();
            int tileIndex = 0;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    var tileTerrains = BuildTileTerrains(terrains, cols, c, r);
                    _map[c, r] = new MapTile
                    {
                        Col = c,
                        Row = r,
                        TileImageIndex = (loader != null && loader.Count > 0) ? tileIndex % loader.Count : 0,
                        TerrainId = GetRepresentativeTerrain(tileTerrains),
                        TerrainIds = tileTerrains
                    };
                    tileIndex++;
                }
            }

            UpdateScrollSize();
            ZoomChanged?.Invoke(_zoom);
            Invalidate();
        }

        private List<int> GetTerrains()
        {
            string directoryPath = AppContext.BaseDirectory;

            List<int> numbers = new List<int>();

            // 获取目录下所有 txt 文件
            string[] files = Directory.GetFiles(directoryPath, "MapData.txt");

            foreach (string file in files)
            {
                // 读取整个文件内容
                string content = File.ReadAllText(file);

                // 按空格拆分
                string[] parts = content.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                foreach (string part in parts)
                {
                    if (int.TryParse(part, out int n))
                    {
                        numbers.Add(n);
                    }
                    else
                    {
                        Console.WriteLine($"无法解析为整数: {part}");
                    }
                }
            }

            return numbers;
        }

        private static int[,] BuildTileTerrains(IReadOnlyList<int> terrains, int tileCols, int tileCol, int tileRow)
        {
            var tileTerrains = new int[TerrainCellsPerTile, TerrainCellsPerTile];
            int terrainCols = tileCols * TerrainCellsPerTile;

            for (int localY = 0; localY < TerrainCellsPerTile; localY++)
            {
                int globalY = tileRow * TerrainCellsPerTile + localY;
                for (int localX = 0; localX < TerrainCellsPerTile; localX++)
                {
                    int globalX = tileCol * TerrainCellsPerTile + localX;
                    int terrainIndex = globalY * terrainCols + globalX;
                    tileTerrains[localX, localY] = GetTerrainOrDefault(terrains, terrainIndex);
                }
            }

            return tileTerrains;
        }

        private static int GetTerrainOrDefault(IReadOnlyList<int> terrains, int index)
        {
            if (terrains == null || terrains.Count == 0) return 0;
            if (index < 0) return 0;
            if (index >= terrains.Count) return terrains[terrains.Count - 1];
            return terrains[index];
        }

        private static int GetRepresentativeTerrain(int[,] terrainIds)
        {
            return terrainIds[TerrainCellsPerTile / 2, TerrainCellsPerTile / 2];
        }

        // ── 缩放（外部调用，如工具栏按钮） ───────────────────
        public void ZoomIn()  => ApplyZoom(_zoom * ZoomStep);
        public void ZoomOut() => ApplyZoom(_zoom / ZoomStep);
        public void ZoomReset() => ApplyZoom(Math.Max(ZoomMin, Math.Min(ZoomMax, 64f / _srcTileW)));

        private void ApplyZoom(float newZoom)
        {
            _zoom = Math.Max(ZoomMin, Math.Min(ZoomMax, newZoom));
            UpdateScrollSize();
            ZoomChanged?.Invoke(_zoom);
            Invalidate();
        }

        private void UpdateScrollSize()
        {
            AutoScrollMinSize = new Size(_cols * TileW, _rows * TileH);
        }

        // ── 设置指定tile地形 ──────────────────────────────────
        public void SetTileTerrain(int col, int row, int terrainId)
        {
            if (_map == null || col < 0 || col >= _cols || row < 0 || row >= _rows) return;
            _map[col, row].TerrainId = terrainId;
            if (_map[col, row].TerrainIds == null)
                _map[col, row].TerrainIds = new int[TerrainCellsPerTile, TerrainCellsPerTile];

            for (int y = 0; y < TerrainCellsPerTile; y++)
            {
                for (int x = 0; x < TerrainCellsPerTile; x++)
                {
                    _map[col, row].TerrainIds[x, y] = terrainId;
                }
            }

            Invalidate(TileScreenRect(col, row));
        }

        // ── 鼠标滚轮缩放 ──────────────────────────────────────
        private void OnMouseWheel(object sender, MouseEventArgs e)
        {
            if (_map == null) return;

            // 以鼠标位置为中心缩放（保持鼠标指向的tile不移动）
            int ox = AutoScrollPosition.X;
            int oy = AutoScrollPosition.Y;
            float mapX = e.X - ox;   // 鼠标在地图坐标系中的位置
            float mapY = e.Y - oy;

            float oldZoom = _zoom;
            float newZoom = e.Delta > 0 ? _zoom * ZoomStep : _zoom / ZoomStep;
            newZoom = Math.Max(ZoomMin, Math.Min(ZoomMax, newZoom));
            if (Math.Abs(newZoom - oldZoom) < 0.0001f) return;

            _zoom = newZoom;
            UpdateScrollSize();

            // 调整滚动位置，使鼠标所指tile保持原位
            float scale = newZoom / oldZoom;
            int newScrollX = (int)(mapX * scale - e.X);
            int newScrollY = (int)(mapY * scale - e.Y);
            AutoScrollPosition = new Point(
                Math.Max(0, -newScrollX),
                Math.Max(0, -newScrollY));

            ZoomChanged?.Invoke(_zoom);
            Invalidate();
        }

        // ── 鼠标单击 ──────────────────────────────────────────
        private void OnMouseClick(object sender, MouseEventArgs e)
        {
            if (_map == null || e.Button != MouseButtons.Left) return;
            var (col, row) = ScreenToTile(e.X, e.Y);
            if (col < 0) return;

            _selectedCol = col;
            _selectedRow = row;
            Invalidate();
            TileClicked?.Invoke(_map[col, row]);
        }

        // ── 鼠标双击 → 大图预览 ───────────────────────────────
        private void OnMouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (_map == null || e.Button != MouseButtons.Left) return;
            var (col, row) = ScreenToTile(e.X, e.Y);
            if (col < 0) return;

            TileDoubleClicked?.Invoke(_map[col, row]);
        }

        // ── 坐标转换 ──────────────────────────────────────────
        private (int col, int row) ScreenToTile(int screenX, int screenY)
        {
            int ox = AutoScrollPosition.X;
            int oy = AutoScrollPosition.Y;
            int col = (screenX - ox) / TileW;
            int row = (screenY - oy) / TileH;
            if (col < 0 || col >= _cols || row < 0 || row >= _rows) return (-1, -1);
            return (col, row);
        }

        // ── 绘制 ──────────────────────────────────────────────
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (_map == null)
            {
                DrawPlaceholder(e.Graphics);
                return;
            }

            var g = e.Graphics;
            // 缩小时用双线性，放大时用最近邻保持像素感
            g.InterpolationMode = _zoom >= 1f
                ? InterpolationMode.NearestNeighbor
                : InterpolationMode.HighQualityBilinear;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            int ox  = AutoScrollPosition.X;
            int oy  = AutoScrollPosition.Y;
            int tw  = TileW;
            int th  = TileH;

            // 只绘制可视区域
            var clip = e.ClipRectangle;
            int cMin = Math.Max(0, (clip.Left   - ox) / tw);
            int rMin = Math.Max(0, (clip.Top    - oy) / th);
            int cMax = Math.Min(_cols - 1, (clip.Right  - ox) / tw);
            int rMax = Math.Min(_rows - 1, (clip.Bottom - oy) / th);

            for (int c = cMin; c <= cMax; c++)
            {
                for (int r = rMin; r <= rMax; r++)
                {
                    var tile = _map[c, r];
                    int x    = c * tw + ox;
                    int y    = r * th + oy;
                    var rect = new Rectangle(x, y, tw, th);

                    // 1. tile 图片
                    var img = _tileLoader?.GetMapTile(tile.TileImageIndex);
                    if (img != null)
                        g.DrawImage(img, rect);
                    else
                        g.FillRectangle(EmptyBrush, rect);

                    // 2. 地形叠加色
                    if (ShowTerrainOverlay)
                    {
                        DrawTerrainOverlay(g, tile, rect);
                    }

                    // 3. 图片网格线
                    if (ShowGrid)
                        g.DrawRectangle(GridPen, rect);

                    // 4. 坐标（tile足够大时才显示）
                    if (ShowCoords && tw >= 32)
                        g.DrawString($"{c},{r}", CoordFont, CoordBrush, x + 2, y + 2);
                }
            }

            // 5. 选中高亮
            if (_selectedCol >= 0 && _selectedRow >= 0)
            {
                int x = _selectedCol * tw + ox;
                int y = _selectedRow * th + oy;
                g.DrawRectangle(SelectPen, x + 1, y + 1, tw - 2, th - 2);

                int cs = Math.Max(4, Math.Min(8, tw / 6));
                using var cp = new Pen(Color.Yellow, 2f);
                // TL
                g.DrawLine(cp, x+1,    y+1,    x+1+cs, y+1);
                g.DrawLine(cp, x+1,    y+1,    x+1,    y+1+cs);
                // TR
                g.DrawLine(cp, x+tw-1, y+1,    x+tw-1-cs, y+1);
                g.DrawLine(cp, x+tw-1, y+1,    x+tw-1, y+1+cs);
                // BL
                g.DrawLine(cp, x+1,    y+th-1, x+1+cs, y+th-1);
                g.DrawLine(cp, x+1,    y+th-1, x+1,    y+th-1-cs);
                // BR
                g.DrawLine(cp, x+tw-1, y+th-1, x+tw-1-cs, y+th-1);
                g.DrawLine(cp, x+tw-1, y+th-1, x+tw-1, y+th-1-cs);
            }
        }

        private void DrawPlaceholder(Graphics g)
        {
            using var f = new Font("Consolas", 14f);
            using var b = new SolidBrush(Color.FromArgb(80, 80, 100));
            var msg = "请先加载 Tile 图片文件夹";
            var sz = g.MeasureString(msg, f);
            g.DrawString(msg, f, b, (Width - sz.Width) / 2, (Height - sz.Height) / 2);
        }

        private static void DrawTerrainOverlay(Graphics g, MapTile tile, Rectangle rect)
        {
            if (tile.TerrainIds == null)
            {
                if (!TerrainColorCache.TryGetValue(tile.TerrainId, out var color)) return;

                TerrainBrush.Color = color;
                g.FillRectangle(TerrainBrush, rect);
                return;
            }

            for (int localY = 0; localY < TerrainCellsPerTile; localY++)
            {
                int y1 = rect.Top + rect.Height * localY / TerrainCellsPerTile;
                int y2 = rect.Top + rect.Height * (localY + 1) / TerrainCellsPerTile;

                for (int localX = 0; localX < TerrainCellsPerTile; localX++)
                {
                    if (!TerrainColorCache.TryGetValue(tile.TerrainIds[localX, localY], out var color)) continue;

                    int x1 = rect.Left + rect.Width * localX / TerrainCellsPerTile;
                    int x2 = rect.Left + rect.Width * (localX + 1) / TerrainCellsPerTile;

                    TerrainBrush.Color = color;
                    g.FillRectangle(TerrainBrush, x1, y1, Math.Max(1, x2 - x1), Math.Max(1, y2 - y1));
                }
            }
        }

        private static Dictionary<int, Color> BuildTerrainColorCache()
        {
            var colors = new Dictionary<int, Color>();
            foreach (var terrain in MapEditor.Data.TerrainRepository.All)
                colors[terrain.ID] = terrain.MapColor;
            return colors;
        }

        private Rectangle TileScreenRect(int col, int row)
        {
            int ox = AutoScrollPosition.X;
            int oy = AutoScrollPosition.Y;
            return new Rectangle(col * TileW + ox, row * TileH + oy, TileW, TileH);
        }
    }
}
