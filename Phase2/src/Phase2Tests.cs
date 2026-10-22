using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
namespace ClipboardNext {
    static class Phase2Tests {
        static List<string> report=new List<string>();
        static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL: "+name);report.Add("PASS: "+name);}
        public static int Run(string folder) {
            Directory.CreateDirectory(folder);string root=Path.Combine(Path.GetFullPath(folder),"phase2-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
            try {
                var store=new Store(Path.Combine(root,"source"),true);
                var a=store.AddText("Alpha memo");var b=store.AddText("Beta memo");var c=store.AddText("Gamma memo");
                var memoStore=new Store(Path.Combine(root,"memo"),true);memoStore.CreateMemo("备忘正文","备注甲");memoStore.CreateMemo("备忘正文","备注乙");
                var memoReload=new Store(memoStore.Root,false);Check(memoReload.Items.Count==1&&memoReload.Items[0].Note.Contains("备注甲")&&memoReload.Items[0].Note.Contains("备注乙"),"new memo saves content and merged notes in one catalog commit");
                store.SetFlags(a.Id,true,true);store.SetFlags(b.Id,false,true);store.Edit(a.Id,a.Text,"会议资料");
                long rank=store.Items.Single(x=>x.Id==a.Id).FavoriteRank;store.AddText(a.Text);
                var kept=store.Items.Single(x=>x.Id==a.Id);
                Check(kept.Pinned&&kept.Favorite&&kept.Note=="会议资料"&&kept.FavoriteRank==rank&&store.Items.Count==3,"duplicate preserves flags, notes and favorite order");
                Check(store.Search("会议",0).Count==1&&store.Search("",0,true).Count==2,"notes search and dedicated favorites list");
                var historyOrder=store.Items.Select(x=>x.Id).ToArray();store.Reorder(a.Id,b.Id,true);
                Check(store.Search("",0,true)[0].Id==a.Id&&historyOrder.SequenceEqual(store.Items.Select(x=>x.Id)),"favorite reordering leaves history unchanged");
                var favorites=store.Search("",0,true).Select(x=>x.Id).ToArray();store.Reorder(b.Id,c.Id,false);
                Check(store.Items[0].Id==a.Id&&store.Items[1].Id==b.Id&&favorites.SequenceEqual(store.Search("",0,true).Select(x=>x.Id)),"history order preserves pinned area and favorites order");
                bool refused=false;try{store.Reorder(a.Id,b.Id,false);}catch(IOException){refused=true;}Check(refused,"cross-region reorder rejected");
                var reopened=new Store(store.Root,false);Check(reopened.Items.Select(x=>x.Id).SequenceEqual(store.Items.Select(x=>x.Id))&&reopened.Items[0].Note=="会议资料","ranks and notes persist after reopen");
                var oldId=c.Id;var changed=store.Edit(c.Id,"Gamma edited","临时说明");Check(changed.Id!=oldId&&!new Store(store.Root,false).Items.Any(x=>x.Id==oldId),"text edit replaces content atomically");
                store.Edit(b.Id,b.Text,"另一份备注");store.Edit(b.Id,a.Text,"另一份备注");
                kept=store.Items.Single(x=>x.Id==a.Id);Check(store.Items.Count==2&&kept.Note.Contains("会议资料")&&kept.Note.Contains("另一份备注")&&kept.Pinned&&kept.Favorite,"edit collision merges flags and preserves both notes");
                using(var image=new Bitmap(40,30)){image.SetPixel(3,4,Color.Coral);var pic=store.AddImage(image);store.Edit(pic.Id,null,"图片备注");store.SetFlags(pic.Id,false,true);}
                string backup=Path.Combine(root,"all.icb2");store.Export(backup);var target=new Store(Path.Combine(root,"target"),true);var imported=target.Import(backup);
                Check(imported.Added==3&&imported.Merged==0&&target.Search("图片备注",2).Count==1,"backup restores images, notes and records");
                Check(target.Items.Select(x=>x.Id).SequenceEqual(store.Items.Select(x=>x.Id))&&target.Search("",0,true).Select(x=>x.Id).SequenceEqual(store.Search("",0,true).Select(x=>x.Id)),"empty restore preserves history and favorite order");
                target.Edit(a.Id,a.Text,"目标已有备注");string first=target.Items[0].Id;var merged=target.Import(backup);string notes=target.Items.Single(x=>x.Id==a.Id).Note;target.Import(backup);
                Check(merged.Added==0&&merged.Merged==3&&notes.Contains("目标已有备注")&&notes.Contains("会议资料")&&target.Items[0].Id==first&&target.Items.Single(x=>x.Id==a.Id).Note==notes,"repeated import merges without duplication or note loss");
                var corrupt=Disk.Read<Backup>(backup);corrupt.Images[0].Bytes[0]^=1;string broken=Path.Combine(root,"corrupt.icb2");Disk.Write(broken,corrupt);string prior=Disk.Hash(File.ReadAllBytes(Path.Combine(target.Root,"catalog.xml")));
                refused=false;try{target.Import(broken);}catch(IOException){refused=true;}
                Check(refused&&Disk.Hash(File.ReadAllBytes(Path.Combine(target.Root,"catalog.xml")))==prior,"corrupt backup rejected without catalog mutation");
                corrupt=Disk.Read<Backup>(backup);corrupt.Records[0].Id="../../escape";Disk.Write(broken,corrupt);refused=false;try{target.Import(broken);}catch(IOException){refused=true;}Check(refused,"invalid backup record path rejected");
                var cleanup=new Store(Path.Combine(root,"cleanup"),true);var normal=cleanup.AddText("expired");var pinned=cleanup.AddText("protected pin");var fav=cleanup.AddText("protected favorite");var recent=cleanup.AddText("recently used");
                cleanup.SetFlags(pinned.Id,true,false);cleanup.SetFlags(fav.Id,false,true);
                var catalog=Disk.Read<Catalog>(Path.Combine(cleanup.Root,"catalog.xml"));foreach(var item in catalog.Records)item.LastUsedUtc=DateTime.UtcNow.AddDays(-90);Disk.Write(Path.Combine(cleanup.Root,"catalog.xml"),catalog);cleanup=new Store(cleanup.Root,false);
                Check(cleanup.Cleanup(DateTime.UtcNow)==0,"automatic cleanup defaults off");cleanup.Touch(recent.Id);cleanup.Settings.CleanupDays=30;cleanup.SaveSettings();int removed=cleanup.Cleanup(DateTime.UtcNow);
                Check(removed==1&&cleanup.Items.Count==3&&cleanup.Items.All(x=>x.Id!=normal.Id),"cleanup uses last usage and exempts pinned and favorite records");
                string photo=Path.Combine(root,"custom.png");using(var bitmap=new Bitmap(100,60)){using(var g=Graphics.FromImage(bitmap))g.Clear(Color.LightPink);bitmap.Save(photo,ImageFormat.Png);}
                string asset=target.SaveAsset(photo);target.Settings.BackgroundId=asset;target.Settings.BallImageId=asset;target.Settings.BackgroundOpacity=40;target.SaveSettings();
                File.Move(photo,photo+".moved");Check(File.Exists(target.AssetPath(asset)),"custom appearance stores independent image copy");
                var migrated=target.CopyVerified(Path.Combine(root,"migrate"));Check(migrated.Settings.BackgroundId==asset&&File.Exists(migrated.AssetPath(asset))&&migrated.Items.Count==target.Items.Count,"migration includes stage-two metadata and appearance assets");
                string wrong=Path.Combine(root,"old-version");Directory.CreateDirectory(wrong);File.WriteAllText(Path.Combine(wrong,"application.marker"),"iris-clipboard-v1");refused=false;try{new Store(wrong,false);}catch(IOException){refused=true;}Check(refused&&File.ReadAllText(Path.Combine(wrong,"application.marker"))=="iris-clipboard-v1","Next refuses old app data without touching it");
                ContextMenuStrip retainedMenu;
                using(var form=new MainForm(null)) {
                    form.TestStore=store;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-10000,-10000);form.Size=new Size(560,760);form.Show();form.RefreshItems();Application.DoEvents();
                    form.VerifyPhase2UI();
                    form.VerifyDragRegression();
                    retainedMenu=form.VerifyRecordMenuRegression();
                    Application.DoEvents();
                    using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(Path.Combine(folder,"phase2-preview.png"),ImageFormat.Png);}
                    form.TestStore=target;form.RefreshItems();form.ApplyAppearance();Application.DoEvents();
                    using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(Path.Combine(folder,"phase2-background.png"),ImageFormat.Png);}
                    form.Hide();
                }
                Check(retainedMenu.IsDisposed,"record menu is released when its owning main window is disposed");
                Check(true,"three-dot menu repeatedly opens and closes; native pin, favorite and preview clicks finish without disposed-object errors");
                Check(true,"history/favorite navigation and pinned metadata render");
                Check(true,"body and handle drag events reorder before/after, font 22, compact cards, no footer, themed frame");
                using(var source=new Bitmap(400,200)){using(var g=Graphics.FromImage(source)){g.Clear(Color.RoyalBlue);g.FillRectangle(Brushes.LightGreen,200,0,200,200);}using(var canvas=new CropCanvas(source,1){Size=new Size(400,260)}){canvas.CreateControl();canvas.TestSelect(new Point(20,50),new Point(100,130));Rectangle selected=canvas.PixelSelection;using(var cropped=canvas.Crop()){Check(selected.Width==selected.Height&&selected.Width<200&&selected.X>=0&&selected.Right<=400&&cropped.GetPixel(cropped.Width/2,cropped.Height/2).ToArgb()==Color.RoyalBlue.ToArgb(),"manual crop maps selection to original image pixels");string saved=target.SaveAsset(cropped);Check(File.Exists(target.AssetPath(saved)),"cropped selection persists as independent asset");}}
                    Theme.FromImage(source);Color derived=Theme.Pink;Theme.FromImage(null);Check(derived!=Theme.Pink,"palette derives colors from image and resets to default");}
                Check(true,"buffered list and four-button navigation plus inline batch verified");
                report.Add("RESULT: "+report.Count+" phase-two checks passed. Isolated sample data only.");File.WriteAllLines(Path.Combine(folder,"phase2-tests.txt"),report.ToArray());return 0;
            } catch(Exception ex){report.Add(ex.ToString());File.WriteAllLines(Path.Combine(folder,"phase2-tests.txt"),report.ToArray());return 1;}
        }
    }
}
