using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
namespace IrisClipboardNext {
    static class LanguageTests {
        static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
        public static int Run(string folder){
            Directory.CreateDirectory(folder);AppContext app=null;
            try {
                string home=Path.Combine(Path.GetFullPath(folder),"language-"+Guid.NewGuid().ToString("N"));
                app=new AppContext(home,true);
                var clip=app.Storage.AddText("原文 remains unchanged — English and 中文");
                string id=clip.Id, original=clip.Text;
                using(var settings=app.BuildSettingsPage(0)) {
                    settings.Show();Application.DoEvents();
                    ((ComboBox)settings.Controls.Find("interfaceLanguage",true).Single()).SelectedIndex=1;
                    ((Button)settings.Controls.Find("saveSettings",true).Single()).PerformClick();
                }
                Check(L.English&&app.Panel.Text=="Clipboard History","Immediate switch failed");
                Check(new Store(app.Storage.Root,false).Settings.Language=="en","Language not persisted");
                Check(app.Storage.Items.Single(c=>c.Id==id).Text==original,"Record was translated");
                Check(Theme.Presets[0][1]=="Cream mint","Theme names not translated");
                using(var bitmap=new Bitmap(app.Panel.Width,app.Panel.Height)){app.Panel.DrawToBitmap(bitmap,new Rectangle(Point.Empty,app.Panel.Size));bitmap.Save(Path.Combine(folder,"english-main.png"));}
                for(int i=0;i<6;i++)using(var settings=app.BuildSettingsPage(i)) {
                    settings.Show();Application.DoEvents();
                    using(var bitmap=new Bitmap(settings.Width,settings.Height)){settings.DrawToBitmap(bitmap,new Rectangle(Point.Empty,settings.Size));bitmap.Save(Path.Combine(folder,"english-settings-"+i+".png"));}
                    if(i==0)((ComboBox)settings.Controls.Find("interfaceLanguage",true).Single()).SelectedIndex=0;
                    settings.Close();
                }
                Check(L.English&&app.Storage.Settings.Language=="en","Cancelled draft changed language");
                app.Quit();app=new AppContext(home,true);
                Check(L.English&&app.Panel.Text=="Clipboard History","Language not restored on restart");
                app.Storage.Settings.Language="zh-CN";app.Storage.SaveSettings();app.ApplyLanguage();
                Check(app.Panel.Text=="clipboard历史剪切板"&&Theme.Presets[0][1]=="奶油薄荷","Switch back failed");
                Check(app.Storage.Items.Single(c=>c.Id==id).Text==original,"Record changed after restart");
                File.WriteAllText(Path.Combine(folder,"language-tests.txt"),"PASS: immediate switch, persistence/restart, cancel, all settings pages rendered, dynamic theme names, switch back, user content unchanged. Isolated test data.");return 0;
            }catch(Exception ex){File.WriteAllText(Path.Combine(folder,"language-tests.txt"),ex.ToString());return 1;}
            finally{if(app!=null)app.Quit();L.Language="zh-CN";}
        }
    }
}
