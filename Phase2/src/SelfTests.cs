using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace IrisClipboardNext {
    static class SelfTests {
        public static int Startup(string folder) {
            Directory.CreateDirectory(folder);
            AppContext context = null;
            try {
                TaskbarIdentity.Verify();
                context = new AppContext(Path.Combine(Path.GetFullPath(folder), "startup-" + Guid.NewGuid().ToString("N")), true);
                Application.DoEvents();
                if (!context.Panel.Visible || context.Panel.WindowState != FormWindowState.Normal || !context.Panel.ShowInTaskbar) throw new Exception("Startup panel was not visible.");
                if (!Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(context.Panel.Bounds))) throw new Exception("Startup panel outside all screens.");
                context.Collapse(); Application.DoEvents(); if (context.Panel.Visible) throw new Exception("Collapse failed.");
                context.OpenPanel(); Application.DoEvents(); if (!context.Panel.Visible) throw new Exception("Reopen failed.");
                context.Panel.WindowState = FormWindowState.Minimized; context.OpenPanel(); Application.DoEvents();
                if (context.Panel.WindowState != FormWindowState.Normal) throw new Exception("Minimized window failed to restore.");
                VerifyRounded(context.Panel);
                ResizeTests.Verify(context.Panel);ResizeTests.VerifyChildLifecycle();
                using(var settings = context.BuildSettings()) {
                    settings.StartPosition = FormStartPosition.Manual; settings.Location = new Point(-10000,-10000); settings.Show(); Application.DoEvents();
                    ResizeTests.Verify((ThemeForm)settings);
                    var sizeControl = (NumericUpDown)settings.Controls.Find("fontSize",true).Single(); sizeControl.Value = 22; Application.DoEvents();
                    using(var bitmap = new Bitmap(settings.Width,settings.Height)) { settings.DrawToBitmap(bitmap,new Rectangle(Point.Empty,settings.Size)); bitmap.Save(Path.Combine(folder,"settings-preview.png"),ImageFormat.Png); }
                    ((Button)settings.Controls.Find("saveSettings",true).Single()).PerformClick(); Application.DoEvents();
                    if (context.Panel.Font.Size != 22 || new Store(context.Storage.Root,false).Settings.FontSize != 22) throw new Exception("Font setting save/apply failed.");
                }
                using(var appearance=context.BuildPhaseSettings()){appearance.StartPosition=FormStartPosition.Manual;appearance.Location=new Point(-10000,-10000);appearance.Show();Application.DoEvents();using(var bitmap=new Bitmap(appearance.Width,appearance.Height)){appearance.DrawToBitmap(bitmap,new Rectangle(Point.Empty,appearance.Size));bitmap.Save(Path.Combine(folder,"appearance-preview.png"),ImageFormat.Png);}appearance.Close();}
                using(var example=new Bitmap(400,240)){using(var g=Graphics.FromImage(example)){g.Clear(Color.LightSkyBlue);g.FillEllipse(Brushes.SeaGreen,180,30,180,180);}using(var crop=new CropDialog(example,1)){crop.StartPosition=FormStartPosition.Manual;crop.Location=new Point(-10000,-10000);crop.Show();Application.DoEvents();using(var bitmap=new Bitmap(crop.Width,crop.Height)){crop.DrawToBitmap(bitmap,new Rectangle(Point.Empty,crop.Size));bitmap.Save(Path.Combine(folder,"crop-preview.png"),ImageFormat.Png);}crop.Close();}}
                using(var bg=new Bitmap(80,120)){using(var g=Graphics.FromImage(bg))g.Clear(Color.SeaGreen);context.Storage.Settings.BackgroundId=context.Storage.SaveAsset(bg);context.Storage.Settings.BackgroundOriginalId=context.Storage.Settings.BackgroundId;context.Storage.SaveSettings();}
                string backgroundId=context.Storage.Settings.BackgroundId;
                foreach(string[] preset in Theme.Presets)using(var settings=context.BuildSettings()) {
                    settings.StartPosition=FormStartPosition.Manual;settings.Location=new Point(-10000,-10000);settings.Show();Application.DoEvents();
                    ((Button)settings.Controls.Find("settingsTab1",true).Single()).PerformClick();
                    var choice=(RadioButton)settings.Controls.Find("theme-"+preset[0],true).Single();choice.Checked=true;
                    for(int i=0;i<5;i++){((Button)settings.Controls.Find("settingsTab"+i,true).Single()).PerformClick();Application.DoEvents();if(!settings.Controls.Find("settingsPage"+i,true).Single().Visible)throw new Exception("Sidebar page did not open.");}
                    ((Button)settings.Controls.Find("settingsTab1",true).Single()).PerformClick();if(!choice.Checked)throw new Exception("Theme draft lost on tab switch.");
                    ((Button)settings.Controls.Find("saveSettings",true).Single()).PerformClick();Application.DoEvents();
                    var saved=new Store(context.Storage.Root,false).Settings;
                    if(saved.ThemePreset!=preset[0]||saved.BackgroundId!=backgroundId||saved.BackgroundOriginalId!=backgroundId)throw new Exception("Theme persistence or background preservation failed.");
                }
                using(var settings=context.BuildPhaseSettings()) {
                    settings.StartPosition=FormStartPosition.Manual;settings.Location=new Point(-10000,-10000);settings.Show();Application.DoEvents();
                    if(!((RadioButton)settings.Controls.Find("theme-peach",true).Single()).Checked)throw new Exception("Saved theme not selected on reopen.");
                    settings.Size=new Size(900,650);Application.DoEvents();var page=(System.Windows.Forms.Panel)settings.Controls.Find("settingsPage1",true).Single();page.AutoScrollPosition=new Point(0,700);Application.DoEvents();
                    using(var bitmap=new Bitmap(settings.Width,settings.Height)){settings.DrawToBitmap(bitmap,new Rectangle(Point.Empty,settings.Size));bitmap.Save(Path.Combine(folder,"settings-background-controls.png"),ImageFormat.Png);}
                    ((RadioButton)settings.Controls.Find("theme-auto",true).Single()).Checked=true;settings.Close();
                    if(context.Storage.Settings.ThemePreset!="peach")throw new Exception("Cancelled draft was applied.");
                }
                using(var settings=context.BuildPhaseSettings()) {
                    settings.StartPosition=FormStartPosition.Manual;settings.Location=new Point(-10000,-10000);settings.Show();Application.DoEvents();VerifyRounded(settings);
                    ((RadioButton)settings.Controls.Find("theme-ocean",true).Single()).Checked=true;
                    ((NumericUpDown)settings.Controls.Find("fontSize",true).Single()).Value=20;
                    ((Button)settings.Controls.Find("applySettings",true).Single()).PerformClick();Application.DoEvents();
                    if(settings.IsDisposed||!settings.Visible||context.Panel.Font.Size!=20||settings.Font.Size!=20||new Store(context.Storage.Root,false).Settings.ThemePreset!="ocean")throw new Exception("Apply must persist and refresh without closing settings.");
                    if(context.Storage.Settings.BackgroundId!=backgroundId)throw new Exception("Apply lost background.");
                    using(var bitmap=new Bitmap(settings.Width,settings.Height)){settings.DrawToBitmap(bitmap,new Rectangle(Point.Empty,settings.Size));bitmap.Save(Path.Combine(folder,"settings-applied.png"),ImageFormat.Png);}
                    ((RadioButton)settings.Controls.Find("theme-lavender",true).Single()).Checked=true;
                    ((NumericUpDown)settings.Controls.Find("fontSize",true).Single()).Value=18;
                    settings.Close();
                    if(context.Storage.Settings.ThemePreset!="ocean"||context.Storage.Settings.FontSize!=20)throw new Exception("Edits after Apply mutated saved preferences before committing.");
                    settings.Dispose();
                }
                using(var settings=context.BuildSettings()) {
                    settings.StartPosition=FormStartPosition.Manual;settings.Location=new Point(-10000,-10000);settings.Show();Application.DoEvents();
                    ((NumericUpDown)settings.Controls.Find("fontSize",true).Single()).Value=22;
                    ((Button)settings.Controls.Find("applySettings",true).Single()).PerformClick();Application.DoEvents();
                    ((Button)settings.Controls.Find("settingsTab1",true).Single()).PerformClick();
                    ((RadioButton)settings.Controls.Find("theme-garden",true).Single()).Checked=true;
                    ((Button)settings.Controls.Find("applySettings",true).Single()).PerformClick();Application.DoEvents();
                    if(context.Panel.Font.Size!=22||new Store(context.Storage.Root,false).Settings.ThemePreset!="garden")throw new Exception("Repeated Apply failed.");
                    settings.Size=new Size(960,700);Application.DoEvents();VerifyRounded(settings);
                    settings.WindowState=FormWindowState.Maximized;Application.DoEvents();if(settings.Region!=null)throw new Exception("Maximized window should fill screen corners.");
                    settings.WindowState=FormWindowState.Normal;Application.DoEvents();VerifyRounded(settings);settings.Close();
                }
                Theme.FromImage(null);
                context.Quit(); context = null;
                File.WriteAllText(Path.Combine(folder, "startup-smoke.txt"), "PASS: startup, collapse/reopen, font save, sidebar navigation, all themes persist with original background, Apply keeps settings open, repeated Apply and cancelled subsequent drafts, rounded windows resize/maximize/restore, clean exit. Isolated data; clipboard listener and registry writes disabled."); return 0;
            } catch (Exception ex) { File.WriteAllText(Path.Combine(folder, "startup-smoke.txt"), ex.ToString()); return 1; }
            finally { if (context != null) context.Quit(); }
        }
        static List<string> report = new List<string>();
        static void VerifyRounded(Form form){if(form.Region==null||form.Region.IsVisible(0,0)||form.Region.IsVisible(form.ClientSize.Width-1,0)||!form.Region.IsVisible(form.ClientSize.Width/2,2)||!form.Region.IsVisible(form.ClientSize.Width/2,form.ClientSize.Height/2))throw new Exception("Window rounded region invalid.");}
        static void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); report.Add("PASS: " + name); }
        public static int Run(string folder) {
            Directory.CreateDirectory(folder); string isolated = Path.Combine(Path.GetFullPath(folder), "run-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(isolated);
            try {
                var store = new Store(Path.Combine(isolated, "source"), true);
                string longText = "标题\r\n\tif (a) {\n    return 42;\n}\n" + new string('文', 180000);
                var a = store.AddText(longText); var b = store.AddText("Alpha beta"); store.AddText("other"); store.AddText("Alpha beta");
                Check(store.Items.Count == 3 && store.Items[0].Id == b.Id, "text deduplication and move to front");
                Check(store.Search("ALPHA", 1).Count == 1 && store.Search("ALPHA", 2).Count == 0, "case-insensitive search with type intersection");
                string cellText = "a\tb\r\nc\td"; store.AddText(cellText);
                using (var bitmap = new Bitmap(32, 24)) {
                    bitmap.SetPixel(2, 3, Color.FromArgb(255, 13, 89, 145)); store.AddImage(bitmap); store.AddImage(bitmap);
                }
                Check(store.Items.Count == 5, "image deduplication");
                store.Settings.Width = 555; store.Settings.AutoCollapse = true; store.Settings.FontSize = 22; store.SaveSettings();
                var reopened = new Store(store.Root, false);
                Check(reopened.Items.Single(c => c.Id == a.Id).Text == longText, "long Unicode text and indentation survive reopen");
                Check(reopened.Items.Any(c => c.Text == cellText), "tabular delimiters survive persistence");
                Check(reopened.Settings.Width == 555 && reopened.Settings.AutoCollapse, "preferences survive reopen");
                Check(reopened.Settings.FontSize == 22, "font size persists across reopen");
                string oldSettingsPath = Path.Combine(isolated,"legacy-settings.xml");
                File.WriteAllText(oldSettingsPath,"<Preferences xmlns=\"http://schemas.datacontract.org/2004/07/IrisClipboardNext\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><AutoCollapse>false</AutoCollapse></Preferences>");
                Check(Disk.Read<Preferences>(oldSettingsPath).FontSize == 22, "legacy settings receive new default font size 22");
                using (var image = new Bitmap(reopened.ImagePath(reopened.Items.Single(c => c.Kind == "image")))) Check(image.GetPixel(2, 3).ToArgb() == Color.FromArgb(255, 13, 89, 145).ToArgb(), "image pixels survive reopen");
                string unrelated = Path.Combine(store.Root, "personal.txt"); File.WriteAllText(unrelated, "do not remove");
                var migrated = reopened.CopyVerified(Path.Combine(isolated, "destination"));
                Check(migrated.Items.Count == reopened.Items.Count && migrated.Settings.Width == 555, "migration content and preferences verified");
                Check(File.Exists(unrelated) && new Store(reopened.Root, false).Items.Count == 5, "copy does not alter original");
                bool refused = false; try { reopened.CopyVerified(Path.Combine(isolated, "destination")); } catch (IOException) { refused = true; }
                Check(refused && new Store(reopened.Root, false).Items.Count == 5, "existing destination rejected without source loss");
                refused = false; try { reopened.CopyVerified(Path.Combine(reopened.Root, "nested")); } catch (IOException) { refused = true; }
                Check(refused, "nested migration path rejected");
                reopened.CleanMigratedCopy(); Check(File.ReadAllText(unrelated) == "do not remove", "cleanup preserves unrelated files");
                Check(!File.Exists(Path.Combine(reopened.Root, "application.marker")), "cleanup removes owned marker after records");
                string imageId = migrated.Items.Single(c => c.Kind == "image").Id; migrated.Delete(new[] { imageId });
                Check(!File.Exists(Path.Combine(migrated.Root, "images", imageId + ".png")) && new Store(migrated.Root, false).Items.Count == 4, "delete persists and cleans image");
                string marker = Path.Combine(migrated.Root, "application.marker"); File.Move(marker, marker + ".offline");
                refused = false; try { migrated.AddText("must fail"); } catch (IOException) { refused = true; }
                Check(refused && migrated.Items.Count == 4, "unavailable storage fails without false saved record");
                File.Move(marker + ".offline", marker);
                using (var form = new MainForm(null)) {
                    form.TestStore = migrated; form.Location = new Point(-10000, -10000); form.StartPosition = FormStartPosition.Manual;
                    form.Show(); form.RefreshItems(); Application.DoEvents(); form.VerifyInteractionRules(); form.Hide();
                }
                Check(true, "UI search, filtered selection, frozen incoming updates, selection reset, and preview return");
                report.Add("RESULT: " + report.Count + " checks passed. No system clipboard or user data touched.");
                File.WriteAllLines(Path.Combine(folder, "self-test.txt"), report.ToArray()); return 0;
            } catch (Exception ex) { report.Add(ex.ToString()); File.WriteAllLines(Path.Combine(folder, "self-test.txt"), report.ToArray()); return 1; }
        }
        public static int Render(string folder) {
            Directory.CreateDirectory(folder);
            try {
                var store = new Store(Path.Combine(Path.GetFullPath(folder), "ui-" + Guid.NewGuid().ToString("N")), true);
                store.AddText("产品会议记录\r\n先保存，再整理。让灵感和资料都能轻松找回。");
                store.AddText("def remember(content):\r\n    history.save(content)\r\n    return content");
                using (var bitmap = new Bitmap(280, 150)) {
                    using (var g = Graphics.FromImage(bitmap)) { g.Clear(Color.FromArgb(214, 235, 219)); g.FillEllipse(Brushes.Teal, 60, 20, 110, 110); } store.AddImage(bitmap);
                }
                using (var form = new MainForm(null)) {
                    form.TestStore = store; form.Size = new Size(500, 680); form.RefreshItems(); form.CreateControl();
                    form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-10000, -10000);
                    form.Show(); Application.DoEvents();
                    using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size)); bitmap.Save(Path.Combine(folder, "ui-preview.png"), ImageFormat.Png); }
                    form.ApplyFontSize(22); Application.DoEvents();
                    using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size)); bitmap.Save(Path.Combine(folder, "ui-preview-large.png"), ImageFormat.Png); }
                    form.Hide();
                }
                File.WriteAllText(Path.Combine(folder, "ui-smoke.txt"), "PASS: form created, sample rows rendered, and disposed. Native foreground paste and mixed-DPI not covered."); return 0;
            } catch (Exception ex) { File.WriteAllText(Path.Combine(folder, "ui-smoke.txt"), ex.ToString()); return 1; }
        }
    }
}
