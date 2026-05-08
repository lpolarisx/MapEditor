using System;
using System.Drawing;
using System.Windows.Forms;
using MapEditor.Controls;
using MapEditor.Data;
using MapEditor.Models;
using MapEditor.Services;

namespace MapEditor
{
    public class MainForm : Form
    {
        // ── 服务 ──────────────────────────────────────────────
        private readonly TileLoader _tileLoader = new TileLoader();

        // ── 控件 ──────────────────────────────────────────────
        private MapPanel _mapPanel;
        private ListBox _terrainList;
        private Panel _infoPanel;
        private Label _lblTilePos;
        private Label _lblTileIndex;
        private Label _lblTerrainName;
        private Label _lblCanExtend;
        private Label _lblViewThrough;
        private Label _lblGraphicLayer;
        private Label _lblFireDmg;
        private Label _lblFoodDeposit;
        private Label _lblFoodRegain;
        private Label _lblFoodSeason;
        private Label _lblRoutewayBuild;
        private Label _lblRoutewayActive;
        private Label _lblRoutewayConsume;
        private PictureBox _terrainColorBox;
        private ToolStrip _toolbar;
        private StatusStrip _statusBar;
        private ToolStripStatusLabel _statusLabel;
        private ToolStripStatusLabel _statusZoom;

        public MainForm()
        {
            InitializeUI();
            PopulateTerrainList();
        }

        // ─────────────────────────────────────────────────────
        // UI 构建
        // ─────────────────────────────────────────────────────
        private void InitializeUI()
        {
            Text = "地图编辑器";
            Size = new Size(1200, 750);
            MinimumSize = new Size(900, 600);
            BackColor = Color.FromArgb(28, 28, 38);
            ForeColor = Color.FromArgb(210, 210, 220);
            Font = new Font("微软雅黑", 9f);

            // ── 工具栏 ────────────────────────────────────────
            _toolbar = new ToolStrip
            {
                BackColor = Color.FromArgb(38, 38, 52),
                ForeColor = Color.FromArgb(200, 200, 215),
                GripStyle = ToolStripGripStyle.Hidden,
                Padding = new Padding(4, 2, 4, 2),
                Height = 36,
                RenderMode = ToolStripRenderMode.System
            };

            var btnLoad = new ToolStripButton("📂  加载Tile文件夹") { ToolTipText = "选择包含tile图的文件夹" };
            btnLoad.Click += BtnLoad_Click;

            var btnGrid = new ToolStripButton("⊞  网格") { CheckOnClick = true, Checked = true, ToolTipText = "显示/隐藏网格" };
            btnGrid.CheckedChanged += (s, e) => { _mapPanel.ShowGrid = btnGrid.Checked; _mapPanel.Invalidate(); };

            var btnOverlay = new ToolStripButton("🎨  地形叠加") { CheckOnClick = true, Checked = true, ToolTipText = "显示地形颜色叠加" };
            btnOverlay.CheckedChanged += (s, e) => { _mapPanel.ShowTerrainOverlay = btnOverlay.Checked; _mapPanel.Invalidate(); };

            var btnCoords = new ToolStripButton("🔢  坐标") { CheckOnClick = true, Checked = false, ToolTipText = "显示tile坐标" };
            btnCoords.CheckedChanged += (s, e) => { _mapPanel.ShowCoords = btnCoords.Checked; _mapPanel.Invalidate(); };

            // 缩放按钮
            var btnZoomIn  = new ToolStripButton("＋") { ToolTipText = "放大 (滚轮向上)" };
            var btnZoomOut = new ToolStripButton("－") { ToolTipText = "缩小 (滚轮向下)" };
            var btnZoomRst = new ToolStripButton("⟳  重置") { ToolTipText = "恢复默认缩放" };
            btnZoomIn.Click  += (s, e) => _mapPanel.ZoomIn();
            btnZoomOut.Click += (s, e) => _mapPanel.ZoomOut();
            btnZoomRst.Click += (s, e) => _mapPanel.ZoomReset();

            _toolbar.Items.Add(btnLoad);
            _toolbar.Items.Add(new ToolStripSeparator());
            _toolbar.Items.Add(btnGrid);
            _toolbar.Items.Add(btnOverlay);
            _toolbar.Items.Add(btnCoords);
            _toolbar.Items.Add(new ToolStripSeparator());
            _toolbar.Items.Add(btnZoomOut);
            _toolbar.Items.Add(btnZoomIn);
            _toolbar.Items.Add(btnZoomRst);

            // ── 状态栏 ────────────────────────────────────────
            _statusBar = new StatusStrip { BackColor = Color.FromArgb(38, 38, 52) };
            _statusLabel = new ToolStripStatusLabel("就绪 — 请加载Tile文件夹") { ForeColor = Color.FromArgb(150, 150, 170), Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            _statusZoom  = new ToolStripStatusLabel("缩放: —") { ForeColor = Color.FromArgb(120, 130, 150) };
            _statusBar.Items.Add(_statusLabel);
            _statusBar.Items.Add(_statusZoom);

            // ── 左侧：地形列表 ────────────────────────────────
            var leftPanel = new Panel
            {
                Width = 170,
                Dock = DockStyle.Left,
                BackColor = Color.FromArgb(32, 32, 45),
                Padding = new Padding(0, 4, 0, 0)
            };

            var lblTerrainTitle = new Label
            {
                Text = "地形列表",
                Dock = DockStyle.Top,
                Height = 30,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.FromArgb(45, 45, 62),
                ForeColor = Color.FromArgb(180, 180, 200),
                Font = new Font("微软雅黑", 9f, FontStyle.Bold)
            };

            _terrainList = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(32, 32, 45),
                ForeColor = Color.FromArgb(200, 210, 220),
                BorderStyle = BorderStyle.None,
                ItemHeight = 28,
                Font = new Font("微软雅黑", 9f)
            };
            _terrainList.DrawMode = DrawMode.OwnerDrawFixed;
            _terrainList.DrawItem += TerrainList_DrawItem;
            _terrainList.SelectedIndexChanged += TerrainList_SelectedIndexChanged;

            leftPanel.Controls.Add(_terrainList);
            leftPanel.Controls.Add(lblTerrainTitle);

            // ── 右侧：地形信息面板 ────────────────────────────
            _infoPanel = new Panel
            {
                Width = 200,
                Dock = DockStyle.Right,
                BackColor = Color.FromArgb(28, 28, 40),
                Padding = new Padding(12, 12, 12, 12)
            };

            var lblInfoTitle = new Label
            {
                Text = "Tile 信息",
                Dock = DockStyle.Top,
                Height = 30,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.FromArgb(45, 45, 62),
                ForeColor = Color.FromArgb(180, 180, 200),
                Font = new Font("微软雅黑", 9f, FontStyle.Bold)
            };

            var infoContent = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10, 10, 10, 10)
            };

            _lblTilePos = MakeInfoLabel("位置：—");
            _lblTileIndex = MakeInfoLabel("图片编号：—");
            var sep1 = MakeSeparator();
            _terrainColorBox = new PictureBox
            {
                Size = new Size(176, 8),
                BackColor = Color.FromArgb(50, 50, 65),
                BorderStyle = BorderStyle.None,
                Margin = new Padding(0, 4, 0, 6)
            };
            _lblTerrainName   = MakeInfoLabel("—", bold: true, fontSize: 11f);
            var sep2 = MakeSeparator();
            _lblCanExtend     = MakeInfoLabel("可扩展：—");
            _lblViewThrough   = MakeInfoLabel("视线穿透：—");
            _lblGraphicLayer  = MakeInfoLabel("图形层次：—");
            _lblFireDmg       = MakeInfoLabel("火焰伤害率：—");
            var sep3 = MakeSeparator();
            _lblFoodDeposit   = MakeInfoLabel("粮草蕴藏量：—");
            _lblFoodRegain    = MakeInfoLabel("粮草恢复天数：—");
            _lblFoodSeason    = MakeInfoLabel("粮草系数（春/夏/秋/冬）：—", isDesc: true);
            var sep4 = MakeSeparator();
            _lblRoutewayBuild   = MakeInfoLabel("粮道开通（资金/工量）：—", isDesc: true);
            _lblRoutewayActive  = MakeInfoLabel("粮道维持资金：—");
            _lblRoutewayConsume = MakeInfoLabel("粮草消耗率：—");

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Color.Transparent,
                Padding = new Padding(4)
            };

            flow.Controls.Add(_lblTilePos);
            flow.Controls.Add(_lblTileIndex);
            flow.Controls.Add(sep1);
            flow.Controls.Add(_terrainColorBox);
            flow.Controls.Add(_lblTerrainName);
            flow.Controls.Add(sep2);
            flow.Controls.Add(_lblCanExtend);
            flow.Controls.Add(_lblViewThrough);
            flow.Controls.Add(_lblGraphicLayer);
            flow.Controls.Add(_lblFireDmg);
            flow.Controls.Add(sep3);
            flow.Controls.Add(_lblFoodDeposit);
            flow.Controls.Add(_lblFoodRegain);
            flow.Controls.Add(_lblFoodSeason);
            flow.Controls.Add(sep4);
            flow.Controls.Add(_lblRoutewayBuild);
            flow.Controls.Add(_lblRoutewayActive);
            flow.Controls.Add(_lblRoutewayConsume);

            _infoPanel.Controls.Add(flow);
            _infoPanel.Controls.Add(lblInfoTitle);

            // ── 地图画布 ──────────────────────────────────────
            _mapPanel = new MapPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(20, 20, 30)
            };
            _mapPanel.TileClicked       += OnTileClicked;
            _mapPanel.TileDoubleClicked += OnTileDoubleClicked;
            _mapPanel.ZoomChanged       += z => _statusZoom.Text = $"缩放: {z * 100:F0}%";

            // ── 布局组装 ──────────────────────────────────────
            var mainArea = new Panel { Dock = DockStyle.Fill };
            mainArea.Controls.Add(_mapPanel);
            mainArea.Controls.Add(leftPanel);
            mainArea.Controls.Add(_infoPanel);

            Controls.Add(mainArea);
            Controls.Add(_toolbar);
            Controls.Add(_statusBar);

            // 初始化空地图（无tile图）
            _mapPanel.InitMap(30, 30, null);
        }

        // ─────────────────────────────────────────────────────
        // 填充地形列表
        // ─────────────────────────────────────────────────────
        private void PopulateTerrainList()
        {
            _terrainList.Items.Clear();
            foreach (var t in TerrainRepository.All)
                _terrainList.Items.Add(t);
        }

        private void TerrainList_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            var terrain = (TerrainData)_terrainList.Items[e.Index];

            bool selected = (e.State & DrawItemState.Selected) != 0;
            e.Graphics.FillRectangle(
                new SolidBrush(selected ? Color.FromArgb(60, 80, 120) : Color.FromArgb(32, 32, 45)),
                e.Bounds);

            // 颜色方块
            var dotRect = new Rectangle(e.Bounds.X + 6, e.Bounds.Y + 7, 14, 14);
            using var solidColor = new SolidBrush(Color.FromArgb(255, terrain.MapColor.R, terrain.MapColor.G, terrain.MapColor.B));            e.Graphics.FillRectangle(solidColor, dotRect);
            e.Graphics.DrawRectangle(Pens.Gray, dotRect);

            // 名称
            e.Graphics.DrawString(terrain.Name,
                new Font("微软雅黑", 9f),
                new SolidBrush(selected ? Color.White : Color.FromArgb(200, 210, 220)),
                e.Bounds.X + 26, e.Bounds.Y + 6);
        }

        private void TerrainList_SelectedIndexChanged(object sender, EventArgs e)
        {
            // 可扩展：选中地形后，点击地图设置地形
        }

        // ─────────────────────────────────────────────────────
        // 加载Tile文件夹
        // ─────────────────────────────────────────────────────
        private void BtnLoad_Click(object sender, EventArgs e)
        {
            using var dlg = new FolderBrowserDialog
            {
                Description = "选择Tile图片文件夹（需包含图片文件）",
                ShowNewFolderButton = false
            };

            if (dlg.ShowDialog() != DialogResult.OK) return;

            _statusLabel.Text = "正在加载...";
            Application.DoEvents();

            var result = _tileLoader.Load(dlg.SelectedPath);

            if (!result.Success)
            {
                MessageBox.Show(result.Message, "加载失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _statusLabel.Text = "加载失败";
                return;
            }

            // 重新初始化地图（30×30，使用加载的tile）
            _mapPanel.InitMap(30, 30, _tileLoader);
            _statusLabel.Text = $"✓ {result.Message}  |  地图: 30×30";
        }

        // ─────────────────────────────────────────────────────
        // tile 双击 → 大图预览
        // ─────────────────────────────────────────────────────
        private void OnTileDoubleClicked(MapTile tile)
        {
            var img = _tileLoader.GetTile(tile.TileImageIndex);
            if (img == null)
            {
                MessageBox.Show("该tile没有图片，请先加载Tile文件夹。", "无图片",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var terrain = TerrainRepository.GetById(tile.TerrainId);
            string terrainName = terrain?.Name ?? "未知";

            using var preview = new Controls.TilePreviewForm(img, tile, terrainName);
            preview.ShowDialog(this);
        }

        // ─────────────────────────────────────────────────────
        // tile 单击 → 右侧面板显示地形信息
        // ─────────────────────────────────────────────────────
        private void OnTileClicked(MapTile tile)
        {
            _lblTilePos.Text   = $"位置：({tile.Col}, {tile.Row})";
            _lblTileIndex.Text = $"图片编号：#{tile.TileImageIndex}";

            var terrain = TerrainRepository.GetById(tile.TerrainId);
            if (terrain == null) return;

            _terrainColorBox.BackColor = Color.FromArgb(255,
                terrain.MapColor.R, terrain.MapColor.G, terrain.MapColor.B);

            _lblTerrainName.Text    = $"{terrain.Name}  (ID {terrain.ID})";
            _lblCanExtend.Text      = $"可扩展：{(terrain.CanExtendInto ? "是" : "否")}";
            _lblViewThrough.Text    = $"视线穿透：{(terrain.ViewThrough ? "是" : "否")}";
            _lblGraphicLayer.Text   = $"图形层次：{terrain.GraphicLayer}";
            _lblFireDmg.Text        = $"火焰伤害率：{terrain.FireDamageRate:F2}";

            _lblFoodDeposit.Text    = $"粮草蕴藏量：{terrain.FoodDeposit:#,0}";
            _lblFoodRegain.Text     = $"粮草恢复天数：{terrain.FoodRegainDays}";
            _lblFoodSeason.Text     = $"粮草系数（春/夏/秋/冬）：\n{terrain.FoodSpringRate}/{terrain.FoodSummerRate}/{terrain.FoodAutumnRate}/{terrain.FoodWinterRate}";

            _lblRoutewayBuild.Text   = $"粮道开通（资金/工量）：\n{terrain.RoutewayBuildFundCost} / {terrain.RoutewayBuildWorkCost}";
            _lblRoutewayActive.Text  = $"粮道维持资金：{terrain.RoutewayActiveFundCost}";
            _lblRoutewayConsume.Text = $"粮草消耗率：{terrain.RoutewayConsumptionRate:F3}";

            // 地形列表联动高亮
            for (int i = 0; i < _terrainList.Items.Count; i++)
            {
                if (((TerrainData)_terrainList.Items[i]).ID == terrain.ID)
                {
                    _terrainList.SelectedIndex = i;
                    break;
                }
            }

            _statusLabel.Text = $"选中 ({tile.Col},{tile.Row})  |  地形: {terrain.Name}  |  图形层: {terrain.GraphicLayer}  |  图片: #{tile.TileImageIndex}";
            _infoPanel.Refresh();
        }

        // ─────────────────────────────────────────────────────
        // 辅助：创建信息标签
        // ─────────────────────────────────────────────────────
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

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _tileLoader.Dispose();
            base.OnFormClosed(e);
        }
    }
}
