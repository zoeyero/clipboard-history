using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace IrisClipboardNext {
    class BallForm : Form {
        readonly AppContext app; Point down, origin; bool dragging;
        Image customIcon;
        public BallForm(AppContext app) {
            this.app = app; Text = L.T("clipboard历史剪切板悬浮球"); FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual;
            TopMost = true; Size = new Size(56, 56); DoubleBuffered = true; Cursor = Cursors.Hand;
            Icon = Theme.AppIcon;
            using (var path = Theme.Rounded(new RectangleF(0, 0, 56, 56), 16)) Region = new Region(path);
            ReloadAppearance();
            MouseDown += (s, e) => { if (e.Button != MouseButtons.Left) return; down = Cursor.Position; origin = Location; dragging = false; Capture = true; };
            MouseMove += (s, e) => {
                if (!Capture || e.Button != MouseButtons.Left) return;
                Point p = Cursor.Position; if (Math.Abs(p.X - down.X) + Math.Abs(p.Y - down.Y) > 5) dragging = true;
                if (dragging) Location = new Point(origin.X + p.X - down.X, origin.Y + p.Y - down.Y);
            };
            MouseUp += (s, e) => {
                if (e.Button != MouseButtons.Left) return; Capture = false;
                if (dragging) { EnsureVisible(); Remember(); } else if (app.Panel.Visible) app.Collapse(); else app.OpenPanel();
            };
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams { get { var p = base.CreateParams; p.ExStyle |= 0x08000000 | 0x80; return p; } }
        protected override void OnPaint(PaintEventArgs e) {
            e.Graphics.Clear(Theme.Pink); if(customIcon==null)Theme.Logo(e.Graphics, Width); else { e.Graphics.InterpolationMode=InterpolationMode.HighQualityBicubic; e.Graphics.DrawImage(customIcon,ClientRectangle); }
        }
        public void ReloadAppearance() {
            if(customIcon!=null){customIcon.Dispose();customIcon=null;}
            if(app.Storage!=null&&!String.IsNullOrEmpty(app.Storage.Settings.BallImageId))try{using(var image=Image.FromFile(app.Storage.AssetPath(app.Storage.Settings.BallImageId)))customIcon=new Bitmap(image);}catch{customIcon=null;}
            Invalidate();
        }
        protected override void Dispose(bool disposing){if(disposing&&customIcon!=null)customIcon.Dispose();base.Dispose(disposing);}
        public void RestorePosition() {
            var settings = app.Storage != null ? app.Storage.Settings : new Preferences();
            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            Location = settings.BallX == -1 && settings.BallY == -1 ? new Point(area.Right - 90, area.Top + 240) : new Point(settings.BallX, settings.BallY); EnsureVisible();
        }
        public void EnsureVisible() { Rectangle area = Screen.FromRectangle(Bounds).WorkingArea; Location = new Point(Math.Max(area.Left, Math.Min(Left, area.Right - Width)), Math.Max(area.Top, Math.Min(Top, area.Bottom - Height))); }
        void Remember() { if (app.Storage == null || app.Busy) return; app.Storage.Settings.BallX = Left; app.Storage.Settings.BallY = Top; app.SavePreferences(); }
    }
    partial class MainForm : ThemeForm {
        readonly AppContext app;
        public Store TestStore;
        Store Data { get { return app == null ? TestStore : app.Storage; } }
        TextBox search = new TextBox(); ComboBox filter = new ComboBox();
        BackdropListBox list = new BackdropListBox(); Label status = new Label(), count = new Label();
        Button batch = new Button(), all = new Button(), delete = new Button();
        Panel content = new Panel(), preview = new Panel(), operations = new Panel(), searchRow = new Panel();
        HashSet<string> chosen = new HashSet<string>(); List<Clip> visible = new List<Clip>(); bool batching;
        int previousTop;
        TableLayoutPanel layout;
        
        Font interfaceFont, detailFont;
        Label busyOverlay=new Label { Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,BackColor=Theme.Background,Visible=false };
        Dictionary<string, Image> thumbnails = new Dictionary<string, Image>();
        public MainForm(AppContext app) {
            this.app = app; Text = L.T("clipboard历史剪切板"); Font = new Font("Microsoft YaHei UI", 22f, GraphicsUnit.Pixel);
            BackColor = Theme.Background; ForeColor = Theme.Ink; Icon = Theme.AppIcon; TopMost = true; ShowInTaskbar = true; StartPosition = FormStartPosition.Manual;
            Size = Data != null ? new Size(Data.Settings.Width, Data.Settings.Height) : new Size(500, 680); MinimumSize = new Size(420, 430);
            layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(16) };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
            Controls.Add(layout);
            var header = new Panel { Dock = DockStyle.Fill };
            layout.Controls.Add(header,0,0);
            SetupPhase2(header);
            searchRow.Dock = DockStyle.Fill; search.BorderStyle = BorderStyle.FixedSingle; search.Location = new Point(0, 7); search.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            filter.DropDownStyle = ComboBoxStyle.DropDownList; filter.Items.AddRange(new object[] { L.T("全部"), L.T("文字"), L.T("图片") }); filter.SelectedIndex = 0; filter.Width = 78;
            filter.DrawMode=DrawMode.OwnerDrawFixed;
            filter.DrawItem+=(s,e)=>{int i=e.Index<0?filter.SelectedIndex:e.Index;using(var brush=new SolidBrush(Color.White))e.Graphics.FillRectangle(brush,e.Bounds);if(i>=0)TextRenderer.DrawText(e.Graphics,filter.Items[i].ToString(),filter.Font,e.Bounds,Theme.Ink,TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine);};
            var clear = Button(L.T("清除"), () => { search.Clear(); filter.SelectedIndex = 0; }); clear.Width = 58;
            searchRow.Controls.AddRange(new Control[] { search, filter, clear });
            searchRow.Resize += (s, e) => { search.Width = Math.Max(80, searchRow.Width - 233); filter.Location = new Point(searchRow.Width - 225, 5); clear.Location = new Point(searchRow.Width - 140, 3);batch.Location=new Point(searchRow.Width-76,3); };
            search.AccessibleName = L.T("搜索历史文字"); search.TextChanged += (s, e) => SearchChanged(); filter.SelectedIndexChanged += (s, e) => SearchChanged(); layout.Controls.Add(searchRow, 0, 1);
            operations.Dock = DockStyle.Fill; count.AutoSize = true; count.Location = new Point(0, 3);
            batch = Button(L.T("批量"), () => { batching = !batching; chosen.Clear(); RefreshItems(true); });
            all = Button(L.T("全选"), () => { foreach (var c in visible) chosen.Add(c.Id); UpdateActions(); list.Invalidate(); });
            delete = Button(L.T("删除"), ConfirmDelete);
            operations.Controls.AddRange(new Control[] { count, all, delete });searchRow.Controls.Add(batch);
            operations.Resize += (s, e) => { delete.Location = new Point(operations.Width - 64, 3); all.Location = new Point(operations.Width - 134, 3);  };
            batch.Width = 74; all.Width = delete.Width = 64; layout.Controls.Add(operations, 0, 2);
            delete.BackColor = Theme.Pink;
            content.Dock = DockStyle.Fill; list.Dock = DockStyle.Fill; list.BorderStyle = BorderStyle.None; list.BackColor = BackColor;
            list.DrawMode = DrawMode.OwnerDrawFixed; list.ItemHeight = 110; list.IntegralHeight = false;
            list.DrawItem += DrawItem; list.MouseUp += ListClick; list.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter && list.SelectedIndex >= 0) { ActivateItem(list.SelectedIndex, false); e.Handled = true; } };
            content.Controls.Add(list); preview.Dock = DockStyle.Fill; preview.Visible = false; preview.BackColor = BackColor; content.Controls.Add(preview); layout.Controls.Add(content, 0, 3);
            content.Controls.Add(busyOverlay);
            status.Dock = DockStyle.Fill; status.Padding = new Padding(0, 12, 0, 0); status.Font = new Font(Font.FontFamily, 12, GraphicsUnit.Pixel); status.ForeColor = Color.FromArgb(85, 109, 103);
            status.Text = L.T("复制文字或图片后，将自动显示在这里。"); status.Visible=false;
            FormClosing += (s, e) => { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; if (app != null) app.Collapse(); else Hide(); } };
            ResizeEnd += (s, e) => { EnsureVisible(); if (Data != null && (app == null || !app.Busy)) { Data.Settings.Width = Width; Data.Settings.Height = Height; if (app != null) app.SavePreferences(); } };
            ApplyFontSize(Data == null ? 22 : Data.Settings.FontSize); RefreshItems(); ApplyAppearance();
        }
        public void ApplyFontSize(int pixels) {
            pixels = Math.Max(12, Math.Min(22, pixels));
            Font old = interfaceFont, oldDetail = detailFont;
            interfaceFont = new Font("Microsoft YaHei UI", pixels, GraphicsUnit.Pixel);
            detailFont = new Font("Microsoft YaHei UI", pixels, GraphicsUnit.Pixel);
            Font = interfaceFont; status.Font = detailFont;
            ApplyNavigationFont(interfaceFont);
            filter.ItemHeight=Font.Height+2;
            foreach (Control c in preview.Controls) c.Font = interfaceFont;
            list.ItemHeight = 112;
            layout.RowStyles[1].Height = Math.Max(46, Font.Height + 20);
            layout.RowStyles[2].Height = Math.Max(36, Font.Height + 8);
            layout.RowStyles[4].Height = 0;
            MinimumSize = new Size(pixels > 18 ? 520 : 420, 430);
            list.Invalidate(); PerformLayout();
            if(old != null) old.Dispose(); if(oldDetail != null) oldDetail.Dispose();
        }
        Button Button(string text, Action click) {
            var button = new SoftButton { Text = text, Height = 34, BackColor = Theme.Mint, Cursor = Cursors.Hand, TabStop = true };
            button.FlatAppearance.BorderColor = Color.FromArgb(210, 224, 219); button.Click += (s, e) => click(); return button;
        }
        public void Status(string text, bool error = false) { status.Text=text;if(busyOverlay.Visible)busyOverlay.Text=text; if(app!=null&&(error||text.StartsWith(L.T("导入完成"))||text.StartsWith(L.T("备份已导出"))))app.NotifyProblem(text); }
        public void SetBusy(bool busy) { list.Enabled=preview.Enabled=operations.Enabled=searchRow.Enabled=!busy;busyOverlay.Visible=busy;if(busy)busyOverlay.BringToFront(); }
        void SearchChanged() { chosen.Clear(); RefreshItems(true); }
        public void RefreshItems() { RefreshItems(false); }
        void RefreshItems(bool force) {
            if ((batching || sortingRecord) && !force) return;
            visible = Data == null ? new List<Clip>() : Data.Search(search.Text, filter.SelectedIndex, favoritesView);
            int top = list.TopIndex; list.BeginUpdate(); list.Items.Clear(); foreach (var c in visible) list.Items.Add(c); list.EndUpdate();
            if (top < list.Items.Count && top >= 0) list.TopIndex = top;
            UpdateActions(); list.Invalidate();
            if (visible.Count == 0) Status(search.Text.Length > 0 || filter.SelectedIndex != 0 ? L.T("没有匹配结果。可点击“清除”查看全部历史。") : L.T("复制文字或图片后，将自动显示在这里。"));
            else if (status.Text == L.T("复制文字或图片后，将自动显示在这里。") || status.Text.StartsWith(L.T("没有匹配结果"))) Status(L.T("点击记录粘贴 · 预览查看完整内容 · 数据仅保存在本机"));
        }
        void UpdateActions() { count.Text = batching ? L.T("已选 ") + chosen.Count + L.T(" 条") : (L.English&&visible.Count==1?"1 record":visible.Count + L.T(" 条记录")); batch.Text = batching ? L.T("完成") : L.T("批量"); all.Visible = delete.Visible = batching; delete.Enabled = chosen.Count > 0; }
        public void EndBatch() { batching = false; chosen.Clear(); RefreshItems(true); }
        internal void VerifyInteractionRules() {
            Action<bool, string> assert = (ok, name) => { if (!ok) throw new Exception("UI state failed: " + name); };
            search.Text = "ALPHA"; filter.SelectedIndex = 1;
            assert(visible.Count == 1, "combined search");
            batch.PerformClick(); all.PerformClick(); assert(chosen.Count == 1, "select only matching results");
            Data.AddText("Alpha new record"); RefreshItems();
            assert(visible.Count == 1 && chosen.Count == 1, "incoming records freeze during selection");
            search.Text = "Alpha new"; assert(chosen.Count == 0 && visible.Count == 1, "search change clears selection");
            all.PerformClick(); filter.SelectedIndex = 2; assert(chosen.Count == 0 && visible.Count == 0, "filter change clears selection");
            EndBatch(); search.Clear(); filter.SelectedIndex = 0;
            assert(visible.Count == Data.Items.Count, "batch exit refreshes list");
            var text = visible.First(c => c.Kind == "text"); ShowPreview(text);
            assert(preview.Visible && !list.Visible && !operations.Enabled, "preview is inside panel");
            ((Button)preview.Controls[1]).PerformClick();
            assert(!preview.Visible && list.Visible && operations.Enabled, "return restores list");
        }
        void ConfirmDelete() {
            if (chosen.Count == 0) return;
            int pinned=Data.Items.Count(c=>chosen.Contains(c.Id)&&c.Pinned), saved=Data.Items.Count(c=>chosen.Contains(c.Id)&&c.Favorite);
            if (MessageBox.Show(this, L.T("永久删除选中的 ") + chosen.Count + L.T(" 条记录？\n其中置顶 ")+pinned+L.T(" 条、收藏 ")+saved+L.T(" 条（可重叠）。\n删除后无法恢复。"), L.T("确认删除"), MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK && app != null) app.Delete(chosen.ToArray());
        }
        void ListClick(object sender, MouseEventArgs e) {
            if(dragged) { dragged=false; return; }
            if (e.Button != MouseButtons.Left) return; int i = list.IndexFromPoint(e.Location); if (i < 0) return;
            if(!batching && e.X>list.ClientSize.Width-74 && e.Y-list.GetItemRectangle(i).Y>52) { RecordMenu(visible[i],e.Location); return; }
            if(!batching && e.X<36)return;
            ActivateItem(i, e.X > list.ClientSize.Width - 74);
        }
        void ActivateItem(int i, bool view) {
            Clip clip = visible[i];
            if (batching) { if (!chosen.Add(clip.Id)) chosen.Remove(clip.Id); UpdateActions(); list.Invalidate(); }
            else if (view) ShowPreview(clip); else if (app != null) app.Use(clip);
        }
        void DrawItem(object sender, DrawItemEventArgs e) {
            if (e.Index < 0 || e.Index >= visible.Count) return; var c = visible[e.Index]; Rectangle r = e.Bounds; r.Inflate(-1, -4);
            PaintListBackground(e.Graphics,e.Bounds); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using(var path = Theme.Rounded(r,16))
            using (var brush = new SolidBrush(chosen.Contains(c.Id) ? Theme.Mint : Color.FromArgb(customBackground==null?255:224,Color.White)))
            using (var pen = new Pen(Theme.Border)) { e.Graphics.FillPath(brush,path); e.Graphics.DrawPath(pen,path); }
            using(var stripe = new SolidBrush(c.Kind == "image" ? Theme.Mint : Theme.Pink))
            using(var path = Theme.Rounded(new RectangleF(r.X+12,r.Bottom-32,56,28),7)) e.Graphics.FillPath(stripe,path);
            int x = r.X + 30;
            if(!batching) TextRenderer.DrawText(e.Graphics,"≡",Font,new Point(r.X+6,r.Y+12),Theme.Muted);
            if (batching) { TextRenderer.DrawText(e.Graphics, chosen.Contains(c.Id) ? "☑" : "☐", Font, new Point(x, r.Y + 12), ForeColor); x += 26; }
            if (c.Kind == "image") {
                try {
                    Image thumbnail;
                    if (!thumbnails.TryGetValue(c.Id, out thumbnail)) {
                        if (thumbnails.Count >= 40) { foreach (var img in thumbnails.Values) img.Dispose(); thumbnails.Clear(); }
                        using (var source = Image.FromFile(Data.ImagePath(c))) {
                            double ratio = Math.Min(78.0 / source.Width, 60.0 / source.Height);
                            thumbnail = new Bitmap(source, new Size(Math.Max(1, (int)(source.Width * ratio)), Math.Max(1, (int)(source.Height * ratio))));
                        } thumbnails[c.Id] = thumbnail;
                    }
                    e.Graphics.DrawImage(thumbnail, x, r.Y + 12); x += 86;
                } catch { TextRenderer.DrawText(e.Graphics, L.T("图片不可用"), Font, new Point(x, r.Y + 12), Color.Firebrick); x += 86; }
            }
            TextRenderer.DrawText(e.Graphics, c.Summary, Font, new Rectangle(x, r.Y + 10, Math.Max(20, r.Right - x - 83), r.Height-42), ForeColor, TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(e.Graphics,(c.Kind=="text"?L.T("文字"):L.T("图片"))+"  "+c.CopiedUtc.ToLocalTime().ToString("HH:mm")+(c.Pinned?" ↑":"")+(c.Favorite?" ★":""),Font,new Rectangle(r.X+12,r.Bottom-32,r.Width-94,30),Theme.Muted,TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis);
            if (!batching) TextRenderer.DrawText(e.Graphics, L.T("预览"), Font, new Rectangle(r.Right - 73, r.Y + 12, 68, 32), Theme.AccentInk, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
            if(!batching) TextRenderer.DrawText(e.Graphics,"···",Font,new Rectangle(r.Right-73,r.Y+51,68,24),Theme.AccentInk,TextFormatFlags.HorizontalCenter);
            PaintDropMarker(e.Graphics,e.Bounds,e.Index);
        }
        void ShowPreview(Clip clip) {
            previousTop = list.TopIndex; ClearPreview();
            var back = Button(L.T("‹ 返回列表"), () => { ClearPreview(); preview.Visible = false; list.Visible = true; searchRow.Enabled = operations.Enabled = true; if (previousTop < list.Items.Count && previousTop >= 0) list.TopIndex = previousTop; }); back.Dock = DockStyle.Top; back.Height = 40;
            Control view;
            if (clip.Kind == "text") view = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, WordWrap = true, Text = clip.Text, Dock = DockStyle.Fill, Font = Font, BackColor = Color.White };
            else {
                try { using (var original = Image.FromFile(Data.ImagePath(clip))) view = new PictureBox { Image = new Bitmap(original), Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.White }; }
                catch (Exception ex) { Status(L.T("图片读取失败：") + ex.Message, true); return; }
            }
            preview.Controls.Add(view); preview.Controls.Add(back); list.Visible = false; preview.Visible = true; preview.BringToFront(); searchRow.Enabled = operations.Enabled = false;
        }
        void ClearPreview() { foreach (Control c in preview.Controls.Cast<Control>().ToArray()) { var picture = c as PictureBox; if (picture != null && picture.Image != null) picture.Image.Dispose(); c.Dispose(); } preview.Controls.Clear(); }
        public void OpenNear(Rectangle ball) {
            Rectangle area = Screen.FromRectangle(ball).WorkingArea; Width = Math.Min(Width, area.Width); Height = Math.Min(Height, area.Height);
            int x = ball.Right + 8; if (x + Width > area.Right) x = ball.Left - Width - 8;
            Location = new Point(Math.Max(area.Left, x), Math.Max(area.Top, Math.Min(ball.Top, area.Bottom - Height))); Show(); Activate();
        }
        public void EnsureVisible() { Rectangle area = Screen.FromRectangle(Bounds).WorkingArea; Location = new Point(Math.Max(area.Left, Math.Min(Left, area.Right - Width)), Math.Max(area.Top, Math.Min(Top, area.Bottom - Height))); }
        protected override void Dispose(bool disposing) { if (disposing) { if(customBackground!=null)customBackground.Dispose();if(windowBackdrop!=null)windowBackdrop.Dispose(); ClearPreview(); foreach (var image in thumbnails.Values) image.Dispose(); thumbnails.Clear(); } base.Dispose(disposing); if(disposing) { if(interfaceFont != null) interfaceFont.Dispose(); if(detailFont != null) detailFont.Dispose(); } }
    }
}
