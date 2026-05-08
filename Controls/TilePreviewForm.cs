using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
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

        public TilePreviewForm(Image image, MapTile tile, string terrainName)
        {
            _image       = image;
            _tile        = tile;
            _terrainName = terrainName;

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
            KeyDown        += (s, e) => { if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Return) Close(); };

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

            Controls.Add(_canvas);
            Controls.Add(toolbar);
            Controls.Add(bottomBar);
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

            // 图片边框
            using var borderPen = new Pen(Color.FromArgb(60, 180, 180, 220), 1f);
            g.DrawRectangle(borderPen,
                destRect.X - 1, destRect.Y - 1,
                destRect.Width + 2, destRect.Height + 2);
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
                _dragging      = true;
                _dragStart     = e.Location;
                _offsetAtDrag  = _offset;
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
    }
}
