using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace IrisClipboardNext {
    partial class AppContext {
        internal Form BuildSettingsPage(int selectedPage) {
            var dialog=new ThemeForm { Text=L.T("设置 · clipboard历史剪切板 · v")+Application.ProductVersion,Size=new Size(1000,790),MinimumSize=new Size(900,650),Font=Panel.Font,ForeColor=Theme.Ink,StartPosition=FormStartPosition.CenterScreen,TopMost=true,Icon=Theme.AppIcon };
            var draft=CopyPreferences(Storage==null?new Preferences():Storage.Settings);
            var body=new Panel { Dock=DockStyle.Fill };
            var sidebar=new FlowLayoutPanel { Dock=DockStyle.Left,Width=208,Padding=new Padding(14,24,8,0),FlowDirection=FlowDirection.TopDown,WrapContents=false,BackColor=Color.White };
            var content=new Panel { Dock=DockStyle.Fill,Padding=new Padding(24,20,20,8) };
            var footer=new Panel { Dock=DockStyle.Bottom,Height=68,Padding=new Padding(18,10,22,12) };
            var save=new SoftButton { Name="saveSettings",Text=L.T("保存设置"),Dock=DockStyle.Right,Width=160,BackColor=Theme.Pink,Enabled=Storage!=null };
            var apply=new SoftButton { Name="applySettings",Text=L.T("应用设置"),Dock=DockStyle.Right,Width=160,Enabled=Storage!=null };
            var hint=new Label { Text=L.T("切换分类会保留未保存的修改"),Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,ForeColor=Theme.Muted };
            footer.Controls.Add(hint);footer.Controls.Add(apply);footer.Controls.Add(new Panel{Dock=DockStyle.Right,Width=12});footer.Controls.Add(save);body.Controls.Add(content);body.Controls.Add(sidebar);dialog.Controls.Add(body);dialog.Controls.Add(footer);
            string[] titles={L.T("常规"),L.T("主题与背景"),L.T("存储位置"),L.T("备份与恢复"),L.T("自动清理"),L.T("记录与快捷键")};
            var pages=new Panel[titles.Length];var tabs=new SoftButton[titles.Length];int currentPage=selectedPage;
            for(int i=0;i<titles.Length;i++) {
                int index=i;pages[i]=new Panel { Dock=DockStyle.Fill,AutoScroll=true,Visible=false,Name="settingsPage"+i };content.Controls.Add(pages[i]);
                pages[i].Controls.Add(new Label { Text=titles[i],Left=0,Top=0,Width=640,Height=40 });
                tabs[i]=new SoftButton { Name="settingsTab"+i,Text=titles[i],Width=182,Height=52,Margin=new Padding(0,0,0,10) };sidebar.Controls.Add(tabs[i]);
                tabs[i].Click+=(s,e)=>{currentPage=index;for(int j=0;j<pages.Length;j++){pages[j].Visible=j==index;tabs[j].BackColor=j==index?Theme.Pink:Color.White;}pages[index].BringToFront();};
            }
            Action<Control,int,int,int,int> place=(control,page,x,y,width)=>{control.Left=x;control.Top=y;control.Width=width;pages[page].Controls.Add(control);};
            Action<int,string,int> note=(page,text,y)=>place(new Label{Text=text,Height=66,ForeColor=Theme.Muted},page,0,y,660);
            Func<string,int,int,int,SoftButton> button=(text,page,x,y)=>{var b=new SoftButton{Text=text,Height=44};place(b,page,x,y,206);return b;};
            var startup=new CheckBox{Text=L.T("随 Windows 启动"),AutoSize=true,Checked=AutoStart()};place(startup,0,0,64,600);
            place(new Label{Text="语言 / Language",Height=36},0,0,388,230);
            var language=new ComboBox{Name="interfaceLanguage",DropDownStyle=ComboBoxStyle.DropDownList};
            language.Items.AddRange(new object[]{"简体中文","English"});language.SelectedIndex=draft.Language=="en"?1:0;place(language,0,240,384,250);
            place(new Label{Text="应用后立即切换 / Apply to switch",Height=66,ForeColor=Theme.Muted},0,0,440,640);
            var collapse=new CheckBox{Text=L.T("粘贴后自动收起"),AutoSize=true,Checked=draft.AutoCollapse};place(collapse,0,0,112,600);
            place(new Label{Text=L.T("界面字体大小"),Height=36},0,0,180,220);
            var fontSize=new NumericUpDown{Name="fontSize",Minimum=12,Maximum=22,Value=draft.FontSize};place(fontSize,0,240,178,96);
            place(new Label{Text="px（12–22）",Height=36,ForeColor=Theme.Muted},0,350,180,230);
            var sample=new Label{Text=L.T("预览：让内容清晰易读。\nAa 123 · 文字与列表同步调整"),Height=120,Padding=new Padding(16),BackColor=Theme.Mint};place(sample,0,0,240,630);
            Font sampleFont=null;Action refreshSample=()=>{var old=sampleFont;sampleFont=new Font("Microsoft YaHei UI",(int)fontSize.Value,GraphicsUnit.Pixel);sample.Font=sampleFont;if(old!=null)old.Dispose();};fontSize.ValueChanged+=(s,e)=>refreshSample();refreshSample();dialog.Disposed+=(s,e)=>{if(sampleFont!=null)sampleFont.Dispose();};

            note(1,L.T("基础主题与背景图片可搭配使用，保存后统一应用。"),45);
            var choices=new System.Collections.Generic.List<RadioButton>();
            for(int i=0;i<Theme.Presets.Length;i++) {
                string[] preset=Theme.Presets[i];int x=(i%2)*322,y=104+(i/2)*86;
                var choice=new RadioButton{Text=preset[1],Height=34,Tag=preset[0],Checked=draft.ThemePreset==preset[0],Name="theme-"+preset[0]};place(choice,1,x,y,308);choices.Add(choice);
                choice.CheckedChanged+=(s,e)=>{if(choice.Checked)draft.ThemePreset=(string)choice.Tag;};
                for(int k=0;k<4;k++)place(new Panel{Height=22,BackColor=ColorTranslator.FromHtml(preset[k+2])},1,x+26+k*67,y+38,62);
            }
            var auto=new RadioButton{Text=L.T("图片自动取色"),Height=34,Checked=String.IsNullOrEmpty(draft.ThemePreset)||draft.ThemePreset=="auto",Name="theme-auto"};place(auto,1,322,362,310);choices.Add(auto);auto.CheckedChanged+=(s,e)=>{if(auto.Checked)draft.ThemePreset="auto";};
            place(new Label{Text=L.T("没有图片时使用奶油薄荷"),Height=32,ForeColor=Theme.Muted},1,348,402,290);
            var info=new Label {Text=String.IsNullOrEmpty(draft.BackgroundId)?L.T("尚未选择背景图片"):L.T("已保留当前背景图片与裁剪选区"),Height=40,ForeColor=Theme.Muted};place(info,1,0,462,660);
            Action<bool,bool> choose=(isBall,recrop)=>{
                if(Storage==null)return;
                string original=isBall?(draft.BallOriginalId??draft.BallImageId):(draft.BackgroundOriginalId??draft.BackgroundId),path;
                if(recrop){if(String.IsNullOrEmpty(original)){MessageBox.Show(dialog,L.T("请先选择图片。"));return;}path=Storage.AssetPath(original);}
                else using(var picker=new OpenFileDialog{Filter=L.T("图片|*.png;*.jpg;*.jpeg;*.bmp;*.gif")}){if(picker.ShowDialog(dialog)!=DialogResult.OK)return;path=picker.FileName;}
                try{using(var image=Image.FromFile(path))using(var crop=new CropDialog(image,isBall?1:Panel.Width/(float)Panel.Height)){if(crop.ShowDialog(dialog)!=DialogResult.OK)return;using(crop.Result){string source=recrop?original:Storage.SaveAsset(image),id=Storage.SaveAsset(crop.Result);if(isBall){draft.BallImageId=id;draft.BallOriginalId=source;}else{draft.BackgroundId=id;draft.BackgroundOriginalId=source;}info.Text=L.T("图片选区已准备好，保存设置后生效。");}}}catch(Exception ex){MessageBox.Show(dialog,L.T("图片未能载入：")+ex.Message);}
            };
            button(L.T("选择背景图片…"),1,0,512).Click+=(s,e)=>choose(false,false);
            button(L.T("调整背景选区"),1,222,512).Click+=(s,e)=>choose(false,true);
            button(L.T("移除背景图片"),1,444,512).Click+=(s,e)=>{draft.BackgroundId=draft.BackgroundOriginalId=null;info.Text=L.T("保存后移除背景，仍使用所选主题。");};
            place(new Label{Text=L.T("图片不透明度"),Height=36},1,0,582,220);
            var opacity=new NumericUpDown{Minimum=0,Maximum=100,Value=draft.BackgroundOpacity};place(opacity,1,250,578,100);
            place(new Label{Text=L.T("%（0 为隐藏图片）"),Height=36,ForeColor=Theme.Muted},1,364,582,280);
            place(new Label{Text=L.T("遮罩深浅"),Height=36},1,0,638,220);
            var shade=new NumericUpDown{Minimum=0,Maximum=100,Value=draft.BackgroundShade};place(shade,1,250,634,100);
            button(L.T("选择悬浮图标…"),1,0,704).Click+=(s,e)=>choose(true,false);
            button(L.T("调整图标选区"),1,222,704).Click+=(s,e)=>choose(true,true);
            button(L.T("恢复默认图标"),1,444,704).Click+=(s,e)=>{draft.BallImageId=draft.BallOriginalId=null;info.Text=L.T("保存后恢复默认悬浮图标。");};
            note(1,L.T("图片保存为本地副本，原图移动后仍可使用。"),766);

            note(2,L.T("历史记录和设置仅保存在本机。当前保存位置："),58);
            var location=new TextBox{Text=Storage==null?L.T("保存位置不可用"):Storage.Root,ReadOnly=true,Multiline=true,Height=92};place(location,2,0,126,630);
            button(L.T("更改位置…"),2,0,248).Click+=(s,e)=>{
                if(Storage==null||blocked){MessageBox.Show(dialog,L.T("请先恢复原位置并重试。"));return;}
                using(var picker=new FolderBrowserDialog{Description=L.T("选择父文件夹，将创建独立的 IrisClipboardNextData 子文件夹。")}){if(picker.ShowDialog(dialog)!=DialogResult.OK)return;if(MessageBox.Show(dialog,L.T("迁移期间暂停记录，完成并校验后切换位置，清理原应用数据。请先保存其他设置修改。"),L.T("迁移数据"),MessageBoxButtons.OKCancel)!=DialogResult.OK)return;dialog.Close();Migrate(picker.SelectedPath);}
            };
            button(L.T("重试存储"),2,222,248).Click+=(s,e)=>{if(RetryStorage()){dialog.Close();ShowSettings();}};
            note(3,L.T("备份包含文字、图片、备注、收藏、置顶和排序。\n导入时合并相同内容，保留现有记录。"),58);
            button(L.T("导出备份…"),3,0,166).Click+=(s,e)=>{dialog.Close();BackupFile(false);};
            button(L.T("导入备份…"),3,222,166).Click+=(s,e)=>{dialog.Close();BackupFile(true);};
            note(3,L.T("备份操作会关闭设置页，请先保存其他修改。"),234);
            var cleanup=new CheckBox{Text=L.T("开启自动清理普通历史"),AutoSize=true,Checked=draft.CleanupDays>0};place(cleanup,4,0,70,640);
            place(new Label{Text=L.T("未使用超过多少天"),Height=36},4,0,132,265);
            var days=new NumericUpDown{Minimum=1,Maximum=36500,Value=draft.CleanupDays>0?draft.CleanupDays:30,Enabled=cleanup.Checked};place(days,4,290,130,110);cleanup.CheckedChanged+=(s,e)=>days.Enabled=cleanup.Checked;
            note(4,L.T("从最后复制、编辑或使用计时。\n置顶和收藏始终保留；清理为永久删除。"),206);
            var automatic=new RadioButton{Name="captureAutomatic",Text=L.T("自动记录所有复制内容"),Checked=!draft.HotkeyOnly,Height=38};place(automatic,5,0,64,640);
            note(5,L.T("Ctrl+C、右键复制、截图复制等都会进入历史。"),110);
            var selective=new RadioButton{Name="captureSelective",Text=L.T("仅快捷键记录"),Checked=draft.HotkeyOnly,Height=38};place(selective,5,0,184,640);
            note(5,L.T("普通复制仅进入系统剪贴板，不保存到历史。\n选中内容，按下并松开专用快捷键，即复制并保存。"),230);
            place(new Label{Text=L.T("记录快捷键"),Height=36},5,0,324,170);
            var shortcut=new TextBox{Name="captureShortcut",ReadOnly=true,Text=CaptureHotkey.Format(draft.CaptureModifiers,draft.CaptureKey)};place(shortcut,5,182,320,330);
            var hotkeyInfo=new Label{Height=100,ForeColor=Theme.Muted};place(hotkeyInfo,5,0,454,650);
            Action shortcutInfo=()=>{shortcut.Enabled=selective.Checked;hotkeyInfo.Text=draft.CaptureModifiers==2&&draft.CaptureKey==88?L.T("Ctrl+X 将用于复制并保存，不会剪切原文。\n仅快捷键模式启用期间，原来的 Ctrl+X 剪切被替代。"):L.T("点击输入框后按组合键。\n启用期间会占用该组合键；被其他软件占用时会提示。");};
            shortcut.KeyDown+=(s,e)=>{e.SuppressKeyPress=true;e.Handled=true;if(e.KeyCode==Keys.ControlKey||e.KeyCode==Keys.ShiftKey||e.KeyCode==Keys.Menu)return;draft.CaptureModifiers=(e.Control?2:0)|(e.Alt?1:0)|(e.Shift?4:0);draft.CaptureKey=(int)e.KeyCode;shortcut.Text=CaptureHotkey.Format(draft.CaptureModifiers,draft.CaptureKey);shortcutInfo();};
            button(L.T("恢复默认快捷键"),5,0,388).Click+=(s,e)=>{draft.CaptureModifiers=6;draft.CaptureKey=67;shortcut.Text=CaptureHotkey.Format(6,67);shortcutInfo();};
            selective.CheckedChanged+=(s,e)=>{draft.HotkeyOnly=selective.Checked;shortcutInfo();};shortcutInfo();
            place(new Label{Name="screenshotCaptureHelp",Text=L.T("截图记录：框选区域后，按下并松开记录快捷键，\n再立即点击截图工具的确定按钮。\n截图工具需将结果复制到系统剪贴板。\n当前需在 1.8 秒内确认，且截图窗口保持前台；\n不兼容的截图工具可使用自动记录模式。"),Height=170,ForeColor=Theme.Muted},5,0,568,660);
            note(5,L.T("软件完全退出后，专用快捷键即释放。\n本功能需要目标软件支持复制；不支持时不会保存旧内容。"),754);
            Action<bool> commit=closeAfter=>{
                if(Storage==null||migrating)return;
                int retention=cleanup.Checked?(int)days.Value:0;
                if(retention>0&&retention!=Storage.Settings.CleanupDays&&MessageBox.Show(dialog,L.T("将持续永久清理超过 ")+retention+L.T(" 天未使用的普通历史。\n当前符合条件：")+Storage.Expired(DateTime.UtcNow,retention).Count+L.T(" 条。\n置顶和收藏不受影响。"),L.T("开启自动清理"),MessageBoxButtons.OKCancel,MessageBoxIcon.Warning)!=DialogResult.OK)return;
                var old=Storage.Settings;bool oldStartup=AutoStart();
                // Preserve live window size and floating-ball position while editing settings.
                draft.Width=old.Width;draft.Height=old.Height;draft.BallX=old.BallX;draft.BallY=old.BallY;
                draft.FontSize=(int)fontSize.Value;draft.AutoCollapse=collapse.Checked;draft.CleanupDays=retention;draft.BackgroundOpacity=(int)opacity.Value;draft.BackgroundShade=(int)shade.Value;
                draft.Language=language.SelectedIndex==1?"en":"zh-CN";
                bool languageChanged=draft.Language!=L.Language;
                try{ConfigureCapture(draft);SetAutoStart(startup.Checked);Storage.Settings=CopyPreferences(draft);Storage.SaveSettings();}
                catch(Exception ex){Storage.Settings=old;try{ConfigureCapture(old);SetAutoStart(oldStartup);}catch{}MessageBox.Show(dialog,L.T("设置未能保存：")+ex.Message);return;}
                if(languageChanged){dialog.Close();ApplyLanguage();if(!closeAfter)Panel.BeginInvoke((Action)(()=>ShowSettings()));return;}
                Panel.ApplyFontSize(draft.FontSize);Panel.ApplyAppearance();ball.ReloadAppearance();
                if(closeAfter){dialog.Close();return;}
                dialog.Font=Panel.Font;dialog.ApplyChromePalette();
                Action<Control> recolor=null;recolor=parent=>{foreach(Control child in parent.Controls){if(child is SoftButton)child.BackColor=Theme.Mint;recolor(child);}};recolor(body);
                for(int j=0;j<tabs.Length;j++)tabs[j].BackColor=j==currentPage?Theme.Pink:Color.White;
                save.BackColor=Theme.Pink;apply.BackColor=Theme.Mint;sample.BackColor=Theme.Mint;
                info.Text=String.IsNullOrEmpty(draft.BackgroundId)?L.T("尚未选择背景图片"):L.T("已保留当前背景图片与裁剪选区");
                hint.Text=L.T("已应用，后续修改请再次应用或保存");dialog.Invalidate(true);
            };
            save.Click+=(s,e)=>commit(true);apply.Click+=(s,e)=>commit(false);
            dialog.Shown+=(s,e)=>tabs[selectedPage].PerformClick();
            return dialog;
        }
    }
}
