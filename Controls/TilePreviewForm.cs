using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using MapEditor.Data;
using MapEditor.Models;

namespace MapEditor.Controls
{
    /// <summary>
    /// 双击tile弹出的大图预览窗口
    /// 支持：鼠标拖拽平移、滚轮缩放、ESC/Enter关闭
    /// </summary>
    public class TilePreviewForm : Form
    {
        private readonly Image  _image;
        private readonly MapTile _tile;
        private readonly string  _terrainName;
        private readonly int[,] _draftTerrainIds;
        private readonly HashSet<Point> _modifiedCells = new HashSet<Point>();

        // 变换状态
        private float _zoom = 1f;
        private PointF _offset = PointF.Empty;   // 图片在canvas中的偏移
        private Point  _dragStart;
        private PointF _offsetAtDrag;
        private bool   _dragging;

        // 控件
        private Panel   _canvas;
        private Label   _lblInfo;
        private Label   _lblHint;
        private Panel   _terrainInfoPanel;
        private Label   _lblTerrainCell;
        private Label   _lblTerrainName;
        private Label   _lblCanExtend;
        private Label   _lblViewThrough;
        private Label   _lblGraphicLayer;
        private Label   _lblFireDmg;
        private Label   _lblFoodDeposit;
        private Label   _lblFoodRegain;
        private Label   _lblFoodSeason;
        private Label   _lblRoutewayBuild;
        private Label   _lblRoutewayActive;
        private Label   _lblRoutewayConsume;
        private PictureBox _terrainColorBox;
        private ComboBox _terrainSelector;
        private Button _btnApplyCell;
        private Button _btnRevertCell;
        private Button _btnSubmit;
        private Label _lblEditState;

        private const int TerrainCellsPerTile = 10;
        private int _selectedTerrainX = -1;
        private int _selectedTerrainY = -1;

        public TilePreviewForm(Image image, MapTile tile, string terrainName)
        {
            _image       = image;
            _tile        = tile;
            _terrainName = terrainName;
            _draftTerrainIds = CloneTerrainIds(tile.TerrainIds);

            BuildUI();
            FitToWindow();   // 初始适应窗口
        }

        private void BuildUI()
        {
            Text            = $"Tile 预览  —  #{_tile.TileImageIndex}  ({_tile.Col}, {_tile.Row})";
            Size            = new Size(820, 780);
            MinimumSize     = new Size(400, 400);
            BackColor       = Color.FromArgb(18, 18, 26);
            ForeColor       = Color.FromArgb(210, 210, 220);
            Font            = new Font("微软雅黑", 9f);
            StartPosition   = FormStartPosition.CenterParent;
            KeyPreview      = true;
            KeyDown        += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };

            // ── 底部信息栏 ────────────────────────────────────
            var bottomBar = new Panel
            {
                Dock      = DockStyle.Bottom,
                Height    = 36,
                BackColor = Color.FromArgb(28, 28, 40),
                Padding   = new Padding(10, 0, 10, 0)
            };

            _lblInfo = new Label
            {
                Dock      = DockStyle.Left,
                AutoSize  = false,
                Width     = 500,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(190, 200, 215),
                Font      = new Font("微软雅黑", 9f)
            };
            _lblHint = new Label
            {
                Dock      = DockStyle.Right,
                AutoSize  = false,
                Width     = 260,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Color.FromArgb(100, 110, 130),
                Font      = new Font("微软雅黑", 8.5f),
                Text      = "滚轮缩放  ·  拖拽平移  ·  ESC关闭"
            };

            bottomBar.Controls.Add(_lblInfo);
            bottomBar.Controls.Add(_lblHint);

            UpdateInfoLabel();

            // ── 工具栏 ────────────────────────────────────────
            var toolbar = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 32,
                BackColor = Color.FromArgb(28, 28, 40),
                Padding   = new Padding(6, 3, 6, 3)
            };

            var flow = new FlowLayoutPanel
            {
                Dock          = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents  = false,
                BackColor     = Color.Transparent
            };

            Button MakeBtn(string text, Action onClick)
            {
                var b = new Button
                {
                    Text      = text,
                    AutoSize  = false,
                    Width     = 56,
                    Height    = 24,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(45, 45, 62),
                    ForeColor = Color.FromArgb(200, 210, 220),
                    Font      = new Font("微软雅黑", 8.5f),
                    Margin    = new Padding(0, 0, 4, 0)
                };
                b.FlatAppearance.BorderColor = Color.FromArgb(60, 60, 80);
                b.Click += (s, e) => onClick();
                return b;
            }

            var btnFit    = MakeBtn("适应",  FitToWindow);
            var btnOrigin = MakeBtn("原始",  ZoomOriginal);
            var btnZoomIn = MakeBtn("放大 +", () => ZoomAround(Center, _zoom * 1.25f));
            var btnZoomOut= MakeBtn("缩小 −", () => ZoomAround(Center, _zoom / 1.25f));

            flow.Controls.Add(btnFit);
            flow.Controls.Add(btnOrigin);
            flow.Controls.Add(btnZoomIn);
            flow.Controls.Add(btnZoomOut);
            toolbar.Controls.Add(flow);

            // ── 画布 ──────────────────────────────────────────
            _canvas = new Panel
            {
                Dock        = DockStyle.Fill,
                BackColor   = Color.FromArgb(14, 14, 20),
                Cursor      = Cursors.SizeAll
            };
            _canvas.Paint           += Canvas_Paint;
            _canvas.MouseDown       += Canvas_MouseDown;
            _canvas.MouseMove       += Canvas_MouseMove;
            _canvas.MouseUp         += Canvas_MouseUp;
            _canvas.MouseWheel      += Canvas_MouseWheel;
            _canvas.Resize          += (s, e) => { FitToWindow(); };

            _terrainInfoPanel = BuildTerrainInfoPanel();

            Controls.Add(_canvas);
            Controls.Add(_terrainInfoPanel);
            Controls.Add(toolbar);
            Controls.Add(bottomBar);
        }

        private Panel BuildTerrainInfoPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Right,
                Width = 210,
                BackColor = Color.FromArgb(24, 24, 36),
                Padding = new Padding(12)
            };

            var title = new Label
            {
                Text = "地形信息",
                Dock = DockStyle.Top,
                Height = 30,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.FromArgb(42, 42, 58),
                ForeColor = Color.FromArgb(190, 200, 215),
                Font = new Font("微软雅黑", 9f, FontStyle.Bold)
            };

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Color.Transparent,
                Padding = new Padding(4, 12, 4, 4)
            };

            _lblTerrainCell = MakeInfoLabel("单元：—");
            _terrainColorBox = new PictureBox
            {
                Size = new Size(176, 8),
                BackColor = Color.FromArgb(50, 50, 65),
                BorderStyle = BorderStyle.None,
                Margin = new Padding(0, 4, 0, 6)
            };
            _lblTerrainName = MakeInfoLabel("—", bold: true, fontSize: 11f);
            _lblCanExtend = MakeInfoLabel("可扩展：—");
            _lblViewThrough = MakeInfoLabel("视线穿透：—");
            _lblGraphicLayer = MakeInfoLabel("图形层次：—");
            _lblFireDmg = MakeInfoLabel("火焰伤害率：—");
            _lblFoodDeposit = MakeInfoLabel("粮草蕴藏量：—");
            _lblFoodRegain = MakeInfoLabel("粮草恢复天数：—");
            _lblFoodSeason = MakeInfoLabel("粮草系数（春/夏/秋/冬）：—", isDesc: true);
            _lblRoutewayBuild = MakeInfoLabel("粮道开通（资金/工量）：—", isDesc: true);
            _lblRoutewayActive = MakeInfoLabel("粮道维持资金：—");
            _lblRoutewayConsume = MakeInfoLabel("粮草消耗率：—");
            _lblEditState = MakeInfoLabel("待提交修改：0");

            _terrainSelector = new ComboBox
            {
                Width = 176,
                Height = 26,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(36, 36, 50),
                ForeColor = Color.FromArgb(220, 225, 235),
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 4, 0, 6)
            };
            foreach (var terrain in TerrainRepository.All)
                _terrainSelector.Items.Add(terrain);

            _btnApplyCell = MakeActionButton("修改单元", ApplySelectedTerrainToCell);
            _btnRevertCell = MakeActionButton("撤销单元", RevertSelectedCell);
            _btnSubmit = MakeActionButton("提交修改", SubmitChanges);
            _btnSubmit.BackColor = Color.FromArgb(56, 92, 68);
            _btnSubmit.Enabled = false;

            flow.Controls.Add(_lblTerrainCell);
            flow.Controls.Add(MakeSeparator());
            flow.Controls.Add(_terrainColorBox);
            flow.Controls.Add(_lblTerrainName);
            flow.Controls.Add(_terrainSelector);
            flow.Controls.Add(_btnApplyCell);
            flow.Controls.Add(_btnRevertCell);
            flow.Controls.Add(_btnSubmit);
            flow.Controls.Add(_lblEditState);
            flow.Controls.Add(MakeSeparator());
            flow.Controls.Add(_lblCanExtend);
            flow.Controls.Add(_lblViewThrough);
            flow.Controls.Add(_lblGraphicLayer);
            flow.Controls.Add(_lblFireDmg);
            flow.Controls.Add(MakeSeparator());
            flow.Controls.Add(_lblFoodDeposit);
            flow.Controls.Add(_lblFoodRegain);
            flow.Controls.Add(_lblFoodSeason);
            flow.Controls.Add(MakeSeparator());
            flow.Controls.Add(_lblRoutewayBuild);
            flow.Controls.Add(_lblRoutewayActive);
            flow.Controls.Add(_lblRoutewayConsume);

            panel.Controls.Add(flow);
            panel.Controls.Add(title);
            UpdateTerrainInfo(-1, -1);
            return panel;
        }

        // ── 变换辅助 ─────────────────────────────────────────
        private PointF Center => new PointF(_canvas.Width / 2f, _canvas.Height / 2f);

        private void FitToWindow()
        {
            if (_image == null || _canvas.Width == 0 || _canvas.Height == 0) return;
            float scaleX = (_canvas.Width  - 20f) / _image.Width;
            float scaleY = (_canvas.Height - 20f) / _image.Height;
            _zoom   = Math.Min(scaleX, scaleY);
            _offset = new PointF(
                (_canvas.Width  - _image.Width  * _zoom) / 2f,
                (_canvas.Height - _image.Height * _zoom) / 2f);
            UpdateInfoLabel();
            _canvas.Invalidate();
        }

        private void ZoomOriginal()
        {
            _zoom   = 1f;
            _offset = new PointF(
                (_canvas.Width  - _image.Width)  / 2f,
                (_canvas.Height - _image.Height) / 2f);
            UpdateInfoLabel();
            _canvas.Invalidate();
        }

        private void ZoomAround(PointF pivot, float newZoom)
        {
            newZoom = Math.Max(0.05f, Math.Min(8f, newZoom));
            float scale = newZoom / _zoom;
            _offset = new PointF(
                pivot.X + (_offset.X - pivot.X) * scale,
                pivot.Y + (_offset.Y - pivot.Y) * scale);
            _zoom = newZoom;
            UpdateInfoLabel();
            _canvas.Invalidate();
        }

        private void UpdateInfoLabel()
        {
            if (_image == null) return;
            _lblInfo.Text = $"#{_tile.TileImageIndex}  |  地形: {_terrainName}  |  " +
                            $"原始: {_image.Width}×{_image.Height}px  |  " +
                            $"缩放: {_zoom * 100:F0}%";
        }

        // ── 画布绘制 ──────────────────────────────────────────
        private void Canvas_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(_canvas.BackColor);

            if (_image == null) return;

            // 棋盘格背景（透明区域提示）
            DrawCheckerboard(g);

            g.InterpolationMode = _zoom >= 1f
                ? InterpolationMode.NearestNeighbor
                : InterpolationMode.HighQualityBilinear;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            var destRect = new RectangleF(
                _offset.X, _offset.Y,
                _image.Width * _zoom,
                _image.Height * _zoom);

            g.DrawImage(_image, destRect);
            DrawTerrainOverlay(g, destRect);
            DrawTerrainGrid(g, destRect);

            // 图片边框
            using var borderPen = new Pen(Color.FromArgb(60, 180, 180, 220), 1f);
            g.DrawRectangle(borderPen,
                destRect.X - 1, destRect.Y - 1,
                destRect.Width + 2, destRect.Height + 2);

            if (_selectedTerrainX >= 0 && _selectedTerrainY >= 0)
                DrawSelectedTerrainCell(g, destRect);
        }

        private void DrawCheckerboard(Graphics g)
        {
            var destRect = new RectangleF(
                _offset.X, _offset.Y,
                _image.Width * _zoom,
                _image.Height * _zoom);

            // 只在图片区域内画棋盘
            var region = Rectangle.Truncate(destRect);
            region.Intersect(new Rectangle(0, 0, _canvas.Width, _canvas.Height));
            if (region.IsEmpty) return;

            int cs = 12;
            using var c1 = new SolidBrush(Color.FromArgb(30, 30, 40));
            using var c2 = new SolidBrush(Color.FromArgb(22, 22, 32));
            for (int cy = region.Top; cy < region.Bottom; cy += cs)
                for (int cx = region.Left; cx < region.Right; cx += cs)
                {
                    bool odd = ((cx / cs) + (cy / cs)) % 2 == 0;
                    g.FillRectangle(odd ? c1 : c2,
                        cx, cy,
                        Math.Min(cs, region.Right - cx),
                        Math.Min(cs, region.Bottom - cy));
                }
        }

        // ── 拖拽平移 ─────────────────────────────────────────
        private void Canvas_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (TrySelectTerrainCell(e.Location))
                {
                    _canvas.Invalidate();
                    return;
                }

                _dragging = true;
                _dragStart = e.Location;
                _offsetAtDrag = _offset;
                _canvas.Cursor = Cursors.Hand;
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragging) return;
            _offset = new PointF(
                _offsetAtDrag.X + (e.X - _dragStart.X),
                _offsetAtDrag.Y + (e.Y - _dragStart.Y));
            _canvas.Invalidate();
        }

        private void Canvas_MouseUp(object sender, MouseEventArgs e)
        {
            _dragging      = false;
            _canvas.Cursor = Cursors.SizeAll;
        }

        // ── 滚轮缩放 ─────────────────────────────────────────
        private void Canvas_MouseWheel(object sender, MouseEventArgs e)
        {
            float factor = e.Delta > 0 ? 1.15f : 1f / 1.15f;
            ZoomAround(new PointF(e.X, e.Y), _zoom * factor);
        }

        private bool TrySelectTerrainCell(Point location)
        {
            var imageRect = CurrentImageRect;
            if (!imageRect.Contains(location)) return false;

            int cellX = Math.Min(TerrainCellsPerTile - 1,
                Math.Max(0, (int)((location.X - imageRect.Left) * TerrainCellsPerTile / imageRect.Width)));
            int cellY = Math.Min(TerrainCellsPerTile - 1,
                Math.Max(0, (int)((location.Y - imageRect.Top) * TerrainCellsPerTile / imageRect.Height)));

            _selectedTerrainX = cellX;
            _selectedTerrainY = cellY;
            UpdateTerrainInfo(cellX, cellY);
            return true;
        }

        private RectangleF CurrentImageRect => new RectangleF(
            _offset.X, _offset.Y,
            _image.Width * _zoom,
            _image.Height * _zoom);

        private static void DrawTerrainGrid(Graphics g, RectangleF rect)
        {
            using var pen = new Pen(Color.FromArgb(190, 255, 230, 80), 1f);
            for (int i = 0; i <= TerrainCellsPerTile; i++)
            {
                float x = rect.Left + rect.Width * i / TerrainCellsPerTile;
                float y = rect.Top + rect.Height * i / TerrainCellsPerTile;
                g.DrawLine(pen, x, rect.Top, x, rect.Bottom);
                g.DrawLine(pen, rect.Left, y, rect.Right, y);
            }
        }

        private void DrawSelectedTerrainCell(Graphics g, RectangleF imageRect)
        {
            float x1 = imageRect.Left + imageRect.Width * _selectedTerrainX / TerrainCellsPerTile;
            float y1 = imageRect.Top + imageRect.Height * _selectedTerrainY / TerrainCellsPerTile;
            float x2 = imageRect.Left + imageRect.Width * (_selectedTerrainX + 1) / TerrainCellsPerTile;
            float y2 = imageRect.Top + imageRect.Height * (_selectedTerrainY + 1) / TerrainCellsPerTile;

            using var fill = new SolidBrush(Color.FromArgb(45, 255, 235, 90));
            using var pen = new Pen(Color.Yellow, 2f);
            var rect = new RectangleF(x1, y1, x2 - x1, y2 - y1);
            g.FillRectangle(fill, rect);
            g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
        }

        private void DrawTerrainOverlay(Graphics g, RectangleF imageRect)
        {
            for (int localY = 0; localY < TerrainCellsPerTile; localY++)
            {
                float y1 = imageRect.Top + imageRect.Height * localY / TerrainCellsPerTile;
                float y2 = imageRect.Top + imageRect.Height * (localY + 1) / TerrainCellsPerTile;

                for (int localX = 0; localX < TerrainCellsPerTile; localX++)
                {
                    var terrain = TerrainRepository.GetById(_draftTerrainIds[localX, localY]);
                    if (terrain == null) continue;

                    float x1 = imageRect.Left + imageRect.Width * localX / TerrainCellsPerTile;
                    float x2 = imageRect.Left + imageRect.Width * (localX + 1) / TerrainCellsPerTile;
                    using var brush = new SolidBrush(Color.FromArgb(terrain.MapColor.A,
                        terrain.MapColor.R, terrain.MapColor.G, terrain.MapColor.B));
                    g.FillRectangle(brush, x1, y1, Math.Max(1f, x2 - x1), Math.Max(1f, y2 - y1));

                    if (_modifiedCells.Contains(new Point(localX, localY)))
                    {
                        using var pen = new Pen(Color.FromArgb(230, 255, 245, 120), 2f);
                        g.DrawRectangle(pen, x1 + 1, y1 + 1, Math.Max(1f, x2 - x1 - 2), Math.Max(1f, y2 - y1 - 2));
                    }
                }
            }
        }

        private void UpdateTerrainInfo(int cellX, int cellY)
        {
            if (cellX < 0 || cellY < 0)
            {
                _lblTerrainCell.Text = "单元：—";
                _terrainColorBox.BackColor = Color.FromArgb(50, 50, 65);
                _lblTerrainName.Text = "请选择地形单元";
                _lblCanExtend.Text = "可扩展：—";
                _lblViewThrough.Text = "视线穿透：—";
                _lblGraphicLayer.Text = "图形层次：—";
                _lblFireDmg.Text = "火焰伤害率：—";
                _lblFoodDeposit.Text = "粮草蕴藏量：—";
                _lblFoodRegain.Text = "粮草恢复天数：—";
                _lblFoodSeason.Text = "粮草系数（春/夏/秋/冬）：—";
                _lblRoutewayBuild.Text = "粮道开通（资金/工量）：—";
                _lblRoutewayActive.Text = "粮道维持资金：—";
                _lblRoutewayConsume.Text = "粮草消耗率：—";
                _terrainSelector.SelectedIndex = -1;
                _btnApplyCell.Enabled = false;
                _btnRevertCell.Enabled = false;
                return;
            }

            int terrainId = _draftTerrainIds[cellX, cellY];
            var terrain = TerrainRepository.GetById(terrainId);
            bool modified = _modifiedCells.Contains(new Point(cellX, cellY));
            _lblTerrainCell.Text = $"单元：({cellX}, {cellY}){(modified ? "  *已修改" : "")}";
            if (terrain == null)
            {
                _lblTerrainName.Text = $"未知地形 (ID {terrainId})";
                return;
            }

            SelectTerrainInCombo(terrainId);
            _btnApplyCell.Enabled = true;
            _btnRevertCell.Enabled = modified;
            _terrainColorBox.BackColor = Color.FromArgb(255,
                terrain.MapColor.R, terrain.MapColor.G, terrain.MapColor.B);
            _lblTerrainName.Text = $"{terrain.Name}  (ID {terrain.ID})";
            _lblCanExtend.Text = $"可扩展：{(terrain.CanExtendInto ? "是" : "否")}";
            _lblViewThrough.Text = $"视线穿透：{(terrain.ViewThrough ? "是" : "否")}";
            _lblGraphicLayer.Text = $"图形层次：{terrain.GraphicLayer}";
            _lblFireDmg.Text = $"火焰伤害率：{terrain.FireDamageRate:F2}";
            _lblFoodDeposit.Text = $"粮草蕴藏量：{terrain.FoodDeposit:#,0}";
            _lblFoodRegain.Text = $"粮草恢复天数：{terrain.FoodRegainDays}";
            _lblFoodSeason.Text = $"粮草系数（春/夏/秋/冬）：\n{terrain.FoodSpringRate}/{terrain.FoodSummerRate}/{terrain.FoodAutumnRate}/{terrain.FoodWinterRate}";
            _lblRoutewayBuild.Text = $"粮道开通（资金/工量）：\n{terrain.RoutewayBuildFundCost} / {terrain.RoutewayBuildWorkCost}";
            _lblRoutewayActive.Text = $"粮道维持资金：{terrain.RoutewayActiveFundCost}";
            _lblRoutewayConsume.Text = $"粮草消耗率：{terrain.RoutewayConsumptionRate:F3}";
        }

        public bool HasChanges => _modifiedCells.Count > 0;

        public int[,] EditedTerrainIds => CloneTerrainIds(_draftTerrainIds);

        private void ApplySelectedTerrainToCell()
        {
            if (_selectedTerrainX < 0 || _selectedTerrainY < 0) return;
            if (!(_terrainSelector.SelectedItem is TerrainData terrain)) return;

            _draftTerrainIds[_selectedTerrainX, _selectedTerrainY] = terrain.ID;
            var point = new Point(_selectedTerrainX, _selectedTerrainY);
            if (_tile.TerrainIds != null && _tile.TerrainIds[_selectedTerrainX, _selectedTerrainY] == terrain.ID)
                _modifiedCells.Remove(point);
            else
                _modifiedCells.Add(point);

            UpdateEditState();
            UpdateTerrainInfo(_selectedTerrainX, _selectedTerrainY);
            _canvas.Invalidate();
        }

        private void RevertSelectedCell()
        {
            if (_selectedTerrainX < 0 || _selectedTerrainY < 0 || _tile.TerrainIds == null) return;

            _draftTerrainIds[_selectedTerrainX, _selectedTerrainY] = _tile.TerrainIds[_selectedTerrainX, _selectedTerrainY];
            _modifiedCells.Remove(new Point(_selectedTerrainX, _selectedTerrainY));
            UpdateEditState();
            UpdateTerrainInfo(_selectedTerrainX, _selectedTerrainY);
            _canvas.Invalidate();
        }

        private void SubmitChanges()
        {
            if (!HasChanges)
            {
                DialogResult = DialogResult.Cancel;
                Close();
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private void UpdateEditState()
        {
            _lblEditState.Text = $"待提交修改：{_modifiedCells.Count}";
            _btnSubmit.Enabled = HasChanges;
        }

        private void SelectTerrainInCombo(int terrainId)
        {
            for (int i = 0; i < _terrainSelector.Items.Count; i++)
            {
                if (((TerrainData)_terrainSelector.Items[i]).ID == terrainId)
                {
                    _terrainSelector.SelectedIndex = i;
                    return;
                }
            }

            _terrainSelector.SelectedIndex = -1;
        }

        private static int[,] CloneTerrainIds(int[,] terrainIds)
        {
            var clone = new int[TerrainCellsPerTile, TerrainCellsPerTile];
            if (terrainIds == null) return clone;

            for (int y = 0; y < TerrainCellsPerTile; y++)
                for (int x = 0; x < TerrainCellsPerTile; x++)
                    clone[x, y] = terrainIds[x, y];

            return clone;
        }

        private static Button MakeActionButton(string text, Action onClick)
        {
            var button = new Button
            {
                Text = text,
                Width = 176,
                Height = 26,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(45, 45, 62),
                ForeColor = Color.FromArgb(220, 225, 235),
                Font = new Font("微软雅黑", 8.5f),
                Margin = new Padding(0, 0, 0, 4)
            };
            button.FlatAppearance.BorderColor = Color.FromArgb(65, 65, 85);
            button.Click += (s, e) => onClick();
            return button;
        }

        private static Label MakeInfoLabel(string text, bool bold = false, float fontSize = 9f, bool isDesc = false)
        {
            return new Label
            {
                Text = text,
                AutoSize = false,
                Width = 176,
                Height = isDesc ? 48 : 22,
                Font = new Font("微软雅黑", fontSize, bold ? FontStyle.Bold : FontStyle.Regular),
                ForeColor = isDesc
                    ? Color.FromArgb(150, 160, 175)
                    : Color.FromArgb(200, 210, 220),
                Padding = new Padding(0, 2, 0, 2)
            };
        }

        private static Panel MakeSeparator()
        {
            return new Panel
            {
                Height = 1,
                Width = 176,
                BackColor = Color.FromArgb(55, 55, 75),
                Margin = new Padding(0, 6, 0, 6)
            };
        }
    }
}
