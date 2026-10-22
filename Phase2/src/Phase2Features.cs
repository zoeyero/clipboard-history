using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace IrisClipboardNext {
    partial class MainForm {
        bool favoritesView,dragged,sortingRecord;
        int dropIndex=-1;bool dropAfter;
        FlowLayoutPanel navigation;
        Point dragStart;
        string dragId;
        internal Func<DataObject,DragDropEffects> TestDragRunner;
        Button historyTab,favoriteTab;
        long lastDragScroll;
        Image customBackground;
        Bitmap windowBackdrop;
        ContextMenuStrip recordMenu;
        string menuRecordId;
        public bool IsBatching { get { return batching; } }
        internal void VerifyPhase2UI() {
            SwitchShelf(true); if(visible.Any(c=>!c.Favorite))throw new Exception("Favorites contains unsaved record.");
            SwitchShelf(false); if(visible.Count!=Data.Items.Count||!visible[0].Pinned)throw new Exception("History/pin navigation failed.");
        }
        void SetupPhase2(Panel header) {
            Disposed+=(s,e)=>{if(recordMenu!=null){recordMenu.Dispose();recordMenu=null;}};
            FontChanged+=(s,e)=>{if(recordMenu!=null&&!recordMenu.IsDisposed)recordMenu.Font=Font;};
            list.PaintBackdrop=PaintListBackground;
            navigation=new FlowLayoutPanel { Dock=DockStyle.Fill,WrapContents=false,BackColor=Color.Transparent,Padding=new Padding(0,2,0,0) };
            historyTab=Button(L.T("历史"),()=>SwitchShelf(false));favoriteTab=Button(L.T("收藏"),()=>SwitchShelf(true));
            var add=Button(L.T("新建"),()=>EditRecord(null));var settings=Button(L.T("设置"),()=>{if(app!=null)app.ShowSettings();});
            foreach(var button in new[]{historyTab,favoriteTab,add,settings}){button.Width=100;button.Height=40;button.Margin=new Padding(0,0,10,0);navigation.Controls.Add(button);}header.Controls.Add(navigation);
            list.AllowDrop=true;
            list.MouseDown+=(s,e)=> {
                int index=list.IndexFromPoint(e.Location);dragId=index>=0&&index<visible.Count?visible[index].Id:null;dragStart=e.Location;dragged=false;
                if(e.Button==MouseButtons.Right&&dragId!=null&&!batching)RecordMenu(visible[index],e.Location);
            };
            list.MouseMove+=(s,e)=> {
                if(e.Button!=MouseButtons.Left||dragId==null||batching||dragged||(app!=null&&app.Busy)||Math.Abs(e.X-dragStart.X)+Math.Abs(e.Y-dragStart.Y)<8)return;
                if(dragStart.X>list.ClientSize.Width-74)return;
                dragged=true;var clip=Data.Items.FirstOrDefault(c=>c.Id==dragId);if(clip==null)return;
                sortingRecord=true;Image image=null;
                try {
                    var payload=new DataObject();payload.SetData("IrisClipboardNext.Record",clip.Id);
                    if(clip.Kind=="text")payload.SetData(DataFormats.UnicodeText,clip.Text);
                    else { using(var source=Image.FromFile(Data.ImagePath(clip)))image=new Bitmap(source);payload.SetData(DataFormats.Bitmap,image);payload.SetData(DataFormats.FileDrop,new[]{Data.ImagePath(clip)}); }
                    if(TestDragRunner==null)list.DoDragDrop(payload,DragDropEffects.Copy);else TestDragRunner(payload);
                } catch(Exception ex){Status(L.T("拖动失败：")+ex.Message,true);}
                finally{if(image!=null)image.Dispose();sortingRecord=false;dragId=null;dropIndex=-1;RefreshItems(true);list.Invalidate();}
            };
            DragEventHandler update=(s,e)=> {
                int previous=dropIndex;bool previousAfter=dropAfter;int previousTop=list.TopIndex;
                bool allowed=!batching&&search.Text.Length==0&&filter.SelectedIndex==0&&e.Data.GetDataPresent("IrisClipboardNext.Record");
                var point=list.PointToClient(new Point(e.X,e.Y));int i=list.IndexFromPoint(point);
                if(i<0&&visible.Count>0&&point.Y>=0)i=visible.Count-1;
                var moving=allowed?Data.Items.FirstOrDefault(c=>c.Id==(string)e.Data.GetData("IrisClipboardNext.Record")):null;
                allowed=allowed&&moving!=null&&i>=0&&(favoritesView?moving.Favorite:moving.Pinned==visible[i].Pinned);
                e.Effect=allowed?DragDropEffects.Copy:DragDropEffects.None;dropIndex=allowed?i:-1;
                if(allowed){Rectangle row=list.GetItemRectangle(i);dropAfter=point.Y>=row.Top+row.Height/2;}
                if(Environment.TickCount-lastDragScroll>120){if(point.Y<18&&list.TopIndex>0){list.TopIndex--;lastDragScroll=Environment.TickCount;}else if(point.Y>list.ClientSize.Height-18&&list.TopIndex<visible.Count-1){list.TopIndex++;lastDragScroll=Environment.TickCount;}}
                if(previous!=dropIndex||previousAfter!=dropAfter||previousTop!=list.TopIndex){if(previous>=0)list.Invalidate(list.GetItemRectangle(previous),false);if(dropIndex>=0)list.Invalidate(list.GetItemRectangle(dropIndex),false);}
            };
            list.DragEnter+=update;list.DragOver+=update;list.DragLeave+=(s,e)=>{dropIndex=-1;list.Invalidate();};
            list.DragDrop+=(s,e)=> {
                if(dropIndex<0||e.Effect==DragDropEffects.None)return;
                try { ApplyDrop((string)e.Data.GetData("IrisClipboardNext.Record"),dropIndex,dropAfter); }
                catch(Exception ex){Status(ex.Message,true);}
                finally{dropIndex=-1;list.Invalidate();}
            };
        }
        void ApplyNavigationFont(Font font){if(navigation==null)return;navigation.Font=font;foreach(Control c in navigation.Controls)c.Font=font;}
        void PaintDropMarker(Graphics g,Rectangle row,int i) {if(dropIndex!=i)return;using(var pen=new Pen(Theme.AccentInk,3))g.DrawLine(pen,8,dropAfter?row.Bottom-2:row.Top+2,row.Right-8,dropAfter?row.Bottom-2:row.Top+2);}
        void ApplyDrop(string id,int index,bool after) {Data.Reorder(id,visible[index].Id,favoritesView,after);}
        internal void VerifyDragRegression() {
            SwitchShelf(false);search.Clear();filter.SelectedIndex=0;EndBatch();
            var normal=visible.Where(c=>!c.Pinned).ToList();if(normal.Count<2)throw new Exception("Need two normal records for drag regression.");
            string moving=normal[0].Id,target=normal[1].Id;
            Action<string,string,bool,int> drag=(source,destination,after,x)=>{
                int from=visible.FindIndex(c=>c.Id==source),to=visible.FindIndex(c=>c.Id==destination);list.TopIndex=0;
                Rectangle a=list.GetItemRectangle(from),b=list.GetItemRectangle(to);Point start=new Point(x,a.Top+14),end=new Point(100,after?b.Bottom-8:b.Top+8);
                TestDragRunner=payload=>list.TestDrop(payload,end);
                list.TestPointerDown(start);list.TestPointerMove(new Point(start.X+12,start.Y+12));list.TestPointerUp(start);TestDragRunner=null;
            };
            drag(moving,target,true,120);
            if(visible.FindIndex(c=>c.Id==moving)<=visible.FindIndex(c=>c.Id==target))throw new Exception("Body drag to after target failed.");
            drag(moving,target,false,12);
            if(visible.FindIndex(c=>c.Id==moving)>=visible.FindIndex(c=>c.Id==target))throw new Exception("Handle drag to before target failed.");
            if(navigation.Font.Size!=22||list.ItemHeight!=112||layout.RowStyles[4].Height!=0||FormBorderStyle!=FormBorderStyle.None)throw new Exception("UI revision regression.");
            if(!list.IsBuffered||navigation.Controls.Count!=4||navigation.Controls[0].Text!=L.T("历史")||navigation.Controls[1].Text!=L.T("收藏")||navigation.Controls[2].Text!=L.T("新建")||navigation.Controls[3].Text!=L.T("设置")||batch.Parent!=searchRow)throw new Exception("Navigation or buffer regression.");
        }

        void SwitchShelf(bool favorite) {
            if(app!=null&&app.Busy)return;
            favoritesView=favorite; historyTab.BackColor=favorite?Theme.Mint:Theme.Pink;favoriteTab.BackColor=favorite?Theme.Pink:Theme.Mint;
            ClearPreview(); preview.Visible=false; list.Visible=true; searchRow.Enabled=operations.Enabled=true;
            EndBatch(); Status(favorite?L.T("收藏列表 · 右键记录可取消收藏。"):L.T("历史列表 · 置顶在前；左侧 ≡ 拖动排序，正文可拖出。"));
        }
        void RecordMenu(Clip original,Point point) {
            if(IsDisposed||Disposing||Data==null||(app!=null&&app.Busy))return;
            var clip=Data.Items.FirstOrDefault(c=>c.Id==original.Id); if(clip==null)return;
            if(recordMenu==null) {
                // Closed is raised while ToolStrip is still dispatching the click.
                // Keep one owner-held menu alive through that dispatch and reuse it;
                // the main form's Disposed event releases it and all of its items.
                recordMenu=new ContextMenuStrip { Font=Font,ShowImageMargin=false,Renderer=new ToolStripProfessionalRenderer(new RecordMenuColors()) };
                recordMenu.Items.Add(L.T("置顶"),null,(s,e)=>RunRecordMenuAction(c=>Data.SetFlags(c.Id,!c.Pinned,c.Favorite)));
                recordMenu.Items.Add(L.T("收藏"),null,(s,e)=>RunRecordMenuAction(c=>Data.SetFlags(c.Id,c.Pinned,!c.Favorite)));
                recordMenu.Items.Add(L.T("编辑正文 / 备注…"),null,(s,e)=>RunRecordMenuAction(EditRecord));
                recordMenu.Items.Add(L.T("查看完整内容"),null,(s,e)=>RunRecordMenuAction(ShowPreview));
                recordMenu.Items.Add(new ToolStripSeparator());
                recordMenu.Items.Add(L.T("永久删除…"),null,(s,e)=>RunRecordMenuAction(c=>{chosen.Clear();chosen.Add(c.Id);try{ConfirmDelete();}finally{chosen.Clear();}}));
            }
            if(recordMenu.Visible)recordMenu.Close();
            menuRecordId=clip.Id;recordMenu.Font=Font;recordMenu.BackColor=Theme.Background;recordMenu.ForeColor=Theme.Ink;
            recordMenu.Items[0].Text=clip.Pinned?L.T("取消置顶"):L.T("置顶");
            recordMenu.Items[1].Text=clip.Favorite?L.T("取消收藏"):L.T("收藏");
            recordMenu.Items[2].Text=clip.Kind=="text"?L.T("编辑正文 / 备注…"):L.T("编辑备注…");
            recordMenu.Show(list,point);
        }
        void RunRecordMenuAction(Action<Clip> action) {
            if(IsDisposed||Disposing||Data==null||(app!=null&&app.Busy))return;
            var clip=Data.Items.FirstOrDefault(c=>c.Id==menuRecordId);if(clip==null)return;
            try{action(clip);if(!IsDisposed&&!Disposing)RefreshItems(true);}catch(Exception ex){Status(ex.Message,true);}
        }
        sealed class RecordMenuColors:ProfessionalColorTable {
            public override Color ToolStripDropDownBackground {get{return Theme.Background;}}
            public override Color MenuItemSelected {get{return Theme.Mint;}}
            public override Color MenuItemBorder {get{return Theme.Border;}}
            public override Color MenuBorder {get{return Theme.Border;}}
            public override Color SeparatorDark {get{return Theme.Border;}}
            public override Color SeparatorLight {get{return Theme.Background;}}
        }
        void EditRecord(Clip clip) {
            if(Data==null||(app!=null&&app.Busy))return;
            using(var dialog=new ThemeForm { Text=clip==null?L.T("新建备忘"):L.T("编辑记录"),Icon=Theme.AppIcon,Size=new Size(600,560),MinimumSize=new Size(480,460),Font=Font,BackColor=Theme.Background,StartPosition=FormStartPosition.CenterParent,TopMost=true }) {
                var grid=new TableLayoutPanel { Dock=DockStyle.Fill,Padding=new Padding(16),ColumnCount=1,RowCount=5 };
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute,32)); grid.RowStyles.Add(new RowStyle(SizeType.Percent,65)); grid.RowStyles.Add(new RowStyle(SizeType.Absolute,30)); grid.RowStyles.Add(new RowStyle(SizeType.Percent,35)); grid.RowStyles.Add(new RowStyle(SizeType.Absolute,52)); dialog.Controls.Add(grid);
                grid.Controls.Add(new Label { Text=clip!=null&&clip.Kind=="image"?L.T("图片正文不可编辑"):L.T("正文（粘贴时只使用正文）"),AutoSize=true },0,0);
                var body=new TextBox { Dock=DockStyle.Fill,Multiline=true,ScrollBars=ScrollBars.Both,AcceptsReturn=true,AcceptsTab=true,Text=clip==null?"":clip.Text,ReadOnly=clip!=null&&clip.Kind=="image" };
                var note=new TextBox { Dock=DockStyle.Fill,Multiline=true,ScrollBars=ScrollBars.Vertical,Text=clip==null?"":clip.Note };
                grid.Controls.Add(body,0,1);grid.Controls.Add(new Label { Text=L.T("备注（可搜索，不附带粘贴）"),AutoSize=true },0,2);grid.Controls.Add(note,0,3);
                var save=new SoftButton { Text=L.T("保存"),Dock=DockStyle.Right,Width=110,BackColor=Theme.Pink }; var footer=new Panel { Dock=DockStyle.Fill,Padding=new Padding(0,8,0,0) };footer.Controls.Add(save);grid.Controls.Add(footer,0,4);
                save.Click+=(s,e)=> {
                    try {
                        if(clip==null && String.IsNullOrEmpty(body.Text)) { MessageBox.Show(dialog,L.T("请输入正文。"));return; }
                        string id=clip==null||clip.Kind=="text"?Store.TextId(body.Text):clip.Id;
                        if(Data.Items.Any(c=>c.Id==id&&(clip==null||c.Id!=clip.Id)) && MessageBox.Show(dialog,L.T("存在相同正文。保存后将合并为一条，保留双方置顶、收藏和不同备注。是否继续？"),L.T("合并重复记录"),MessageBoxButtons.OKCancel)!=DialogResult.OK)return;
                        if(clip==null) Data.CreateMemo(body.Text,note.Text);
                        else Data.Edit(clip.Id,body.Text,note.Text);
                        EndBatch();Status(L.T("已保存正文和备注。"));dialog.Close();
                    } catch(Exception ex) { MessageBox.Show(dialog,L.T("未能保存：")+ex.Message); }
                };
                if(app!=null)app.FeatureDialogOpen=true;
                try{dialog.ShowDialog(this);}finally{if(app!=null)app.FeatureDialogOpen=false;}
            }
        }
        public void ApplyAppearance() {
            if(customBackground!=null){customBackground.Dispose();customBackground=null;}
            if(Data!=null&&!String.IsNullOrEmpty(Data.Settings.BackgroundId))try{using(var image=Image.FromFile(Data.AssetPath(Data.Settings.BackgroundId)))customBackground=new Bitmap(image);}catch(Exception ex){Status(L.T("背景读取失败：")+ex.Message,true);}
            Theme.Apply(Data==null?null:Data.Settings.ThemePreset,customBackground);ApplyChromePalette();layout.BackColor=Color.Transparent;ApplyPalette(layout);list.BackColor=Theme.Background;
            historyTab.BackColor=favoritesView?Theme.Mint:Theme.Pink;favoriteTab.BackColor=favoritesView?Theme.Pink:Theme.Mint;
            RebuildWallpaper();list.Invalidate();
        }
        void ApplyPalette(Control parent){foreach(Control c in parent.Controls){if(c is SoftButton)c.BackColor=Theme.Mint;else if(c is Panel||c is TableLayoutPanel||c is Label)c.BackColor=Color.Transparent;ApplyPalette(c);} }
        void RebuildWallpaper(){
            if(ClientSize.Width<=0||ClientSize.Height<=0)return;
            var next=new Bitmap(ClientSize.Width,ClientSize.Height);
            using(var g=Graphics.FromImage(next)){g.Clear(Theme.Background);if(customBackground!=null&&Data!=null){using(var attributes=new ImageAttributes()){var matrix=new ColorMatrix();matrix.Matrix33=Data.Settings.BackgroundOpacity/100f;attributes.SetColorMatrix(matrix);float scale=Math.Max(Width/(float)customBackground.Width,Height/(float)customBackground.Height);int w=(int)(customBackground.Width*scale),h=(int)(customBackground.Height*scale);g.DrawImage(customBackground,new Rectangle((Width-w)/2,(Height-h)/2,w,h),0,0,customBackground.Width,customBackground.Height,GraphicsUnit.Pixel,attributes);}using(var shade=new SolidBrush(Color.FromArgb(Math.Min(255,(int)(Data.Settings.BackgroundShade*2.55)),Color.Black)))g.FillRectangle(shade,ClientRectangle);}}
            var old=windowBackdrop;windowBackdrop=next;BackgroundImage=next;BackgroundImageLayout=ImageLayout.None;if(old!=null)old.Dispose();Invalidate(true);
        }
        protected override void OnResize(EventArgs e){base.OnResize(e);if(layout!=null&&windowBackdrop!=null)RebuildWallpaper();}
        void PaintListBackground(Graphics g,Rectangle bounds){
            if(windowBackdrop==null){using(var brush=new SolidBrush(Theme.Background))g.FillRectangle(brush,bounds);return;}
            Point offset=Point.Empty;for(Control c=list;c!=null&&c!=this;c=c.Parent){offset.X+=c.Left;offset.Y+=c.Top;}
            g.DrawImage(windowBackdrop,bounds,new Rectangle(bounds.X+offset.X,bounds.Y+offset.Y,bounds.Width,bounds.Height),GraphicsUnit.Pixel);
        }
    }

    partial class AppContext {
        internal bool FeatureDialogOpen;
        void RunCleanup() {
            if(migrating||blocked||Storage==null||Panel.IsBatching||FeatureDialogOpen)return;
            try{int count=Storage.Cleanup(DateTime.UtcNow);if(count>0){Panel.RefreshItems();Panel.Status(L.T("按设置自动清理了 ")+count+L.T(" 条普通历史；置顶和收藏保留。"));}}
            catch(Exception ex){Fail(L.T("自动清理未完成：")+ex.Message);}
        }
        public void BackupFile(bool import) {
            if(migrating||Storage==null)return; string path;
            if(import)using(var dialog=new OpenFileDialog { Filter=L.T("clipboard历史剪切板 备份|*.icb2"),Title=L.T("选择备份（只合并，不清空现有数据）") }) { if(dialog.ShowDialog()!=DialogResult.OK)return;path=dialog.FileName; }
            else using(var dialog=new SaveFileDialog { Filter=L.T("clipboard历史剪切板 备份|*.icb2"),FileName=L.T("拾贴Next-")+DateTime.Now.ToString("yyyyMMdd-HHmm")+".icb2",DefaultExt="icb2",AddExtension=true }) { if(dialog.ShowDialog()!=DialogResult.OK)return;path=dialog.FileName; }
            if(Disk.Canonical(path).StartsWith(Storage.Root+"\\",StringComparison.OrdinalIgnoreCase)) { MessageBox.Show(L.T("请选择应用数据目录之外的备份位置。"));return; }
            if(import&&MessageBox.Show(L.T("导入会合并相同内容，保留双方收藏和置顶；不同备注拼接保留。现有顺序保留，新增记录追加在后面。"),L.T("合并备份"),MessageBoxButtons.OKCancel)!=DialogResult.OK)return;
            migrating=true;clipboardRetry.Stop();Panel.SetBusy(true);Panel.Status(import?L.T("正在校验并合并备份… 暂停记录。"):L.T("正在导出备份… 暂停记录。"));
            var worker=new BackgroundWorker();worker.DoWork+=(s,e)=>{if(import)e.Result=Storage.Import(path);else Storage.Export(path);};
            worker.RunWorkerCompleted+=(s,e)=> {
                migrating=false;baseline=Native.GetClipboardSequenceNumber();Panel.SetBusy(false);Panel.EndBatch();
                if(e.Error!=null)Panel.Status(L.T("备份操作失败：")+e.Error.Message,true);
                else if(import){var r=(ImportResult)e.Result;Panel.Status(L.T("导入完成：新增 ")+r.Added+L.T(" 条，合并 ")+r.Merged+L.T(" 条，失败 0 条。"));}
                else Panel.Status(L.T("备份已导出。包含正文、图片、备注、置顶、收藏和排序。"));worker.Dispose();
            };worker.RunWorkerAsync();
        }
        public void ShowPhaseSettings() {
            if(migrating||Storage==null)return;
            using(var dialog=BuildPhaseSettings()){FeatureDialogOpen=true;try{dialog.ShowDialog();}finally{FeatureDialogOpen=false;}}RunCleanup();
        }
        internal Form BuildPhaseSettings() { return BuildSettingsPage(1); }
        static Preferences CopyPreferences(Preferences p) { return new Preferences { Language=p.Language,HotkeyOnly=p.HotkeyOnly,CaptureModifiers=p.CaptureModifiers,CaptureKey=p.CaptureKey,ThemePreset=p.ThemePreset,Width=p.Width,Height=p.Height,BallX=p.BallX,BallY=p.BallY,AutoCollapse=p.AutoCollapse,FontSize=p.FontSize,CleanupDays=p.CleanupDays,BackgroundId=p.BackgroundId,BallImageId=p.BallImageId,BackgroundOpacity=p.BackgroundOpacity,BackgroundShade=p.BackgroundShade,BackgroundOriginalId=p.BackgroundOriginalId,BallOriginalId=p.BallOriginalId }; }
    }
}
