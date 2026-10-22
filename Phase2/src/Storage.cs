using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
namespace ClipboardNext {
    [DataContract(Namespace="http://schemas.datacontract.org/2004/07/IrisClipboardNext")] public class Clip {
        [DataMember] public string Id;
        [DataMember] public string Kind;
        [DataMember] public string Text;
        [DataMember] public DateTime CopiedUtc;
        [DataMember] public DateTime LastUsedUtc;
        [DataMember] public string Note;
        [DataMember] public bool Pinned;
        [DataMember] public bool Favorite;
        [DataMember] public long HistoryRank;
        [DataMember] public long FavoriteRank;
        public string Summary { get { return Kind=="image" ? (String.IsNullOrEmpty(Note)?L.T("图片 · 查看预览"):Note) : (Text??"").Replace("\t","    "); } }
        public Clip Clone() { return (Clip)MemberwiseClone(); }
    }
    [DataContract(Namespace="http://schemas.datacontract.org/2004/07/IrisClipboardNext")] public class Preferences {
        [DataMember] public int Width=520;
        [DataMember] public int Height=700;
        [DataMember] public int BallX=-1;
        [DataMember] public int BallY=-1;
        [DataMember] public bool AutoCollapse;
        [DataMember] public bool HotkeyOnly;
        [DataMember] public int CaptureModifiers=6;
        [DataMember] public int CaptureKey=67;
        [DataMember] public string Language="zh-CN";
        [DataMember] public int FontSize=22;
        [DataMember] public int FontRevision=1;
        [DataMember] public int CleanupDays;
        [DataMember] public string BackgroundId;
        [DataMember] public string ThemePreset;
        [DataMember] public string BallImageId;
        [DataMember] public string BackgroundOriginalId;
        [DataMember] public string BallOriginalId;
        [DataMember] public int BackgroundOpacity=25;
        [DataMember] public int BackgroundShade=10;
        [OnDeserializing] void Defaults(StreamingContext c) { FontSize=22;FontRevision=0; BackgroundOpacity=25; BackgroundShade=10;CaptureModifiers=6;CaptureKey=67; }
        [OnDeserialized] void Validate(StreamingContext c) {if(FontRevision<1){FontSize=22;FontRevision=1;} FontSize=Math.Max(12,Math.Min(22,FontSize)); CleanupDays=Math.Max(0,Math.Min(36500,CleanupDays)); BackgroundOpacity=Math.Max(0,Math.Min(100,BackgroundOpacity)); BackgroundShade=Math.Max(0,Math.Min(100,BackgroundShade)); }
    }
    [DataContract(Namespace="http://schemas.datacontract.org/2004/07/IrisClipboardNext")] public class Locator { [DataMember] public string DataPath; }
    [DataContract(Namespace="http://schemas.datacontract.org/2004/07/IrisClipboardNext")] public class Catalog { [DataMember] public int Version=2; [DataMember] public List<Clip> Records=new List<Clip>(); }
    [DataContract(Namespace="http://schemas.datacontract.org/2004/07/IrisClipboardNext")] public class BackupImage { [DataMember] public string Id; [DataMember] public byte[] Bytes; }
    [DataContract(Namespace="http://schemas.datacontract.org/2004/07/IrisClipboardNext")] public class Backup {
        [DataMember] public int Version=2;
        [DataMember] public List<Clip> Records=new List<Clip>();
        [DataMember] public List<BackupImage> Images=new List<BackupImage>();
    }
    public class ImportResult { public int Added,Merged; }
    public static class Disk {
        static DataContractSerializer Serializer(Type t) { return new DataContractSerializer(t,new DataContractSerializerSettings { MaxItemsInObjectGraph=10000000 }); }
        public static void Write<T>(string path,T value) {
            string temp=path+".pending";
            using(var f=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)) { Serializer(typeof(T)).WriteObject(f,value); f.Flush(true); }
            if(File.Exists(path))File.Replace(temp,path,null); else File.Move(temp,path);
        }
        public static T Read<T>(string path) { using(var f=File.OpenRead(path))return (T)Serializer(typeof(T)).ReadObject(f); }
        public static string Hash(byte[] bytes) { using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant(); }
        public static string Canonical(string path) { return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar); }
    }
    public class Store {
        public const string Marker="iris-clipboard-next-v2";
        public string Root { get; private set; }
        public List<Clip> Items=new List<Clip>();
        public Preferences Settings=new Preferences();
        public Store(string root,bool create) {
            Root=Disk.Canonical(root);
            if(!Directory.Exists(Root)) { if(!create)throw new IOException(L.English?"Storage folder unavailable.":"保存目录不可用。"); Directory.CreateDirectory(Root); }
            string marker=Path.Combine(Root,"application.marker");
            if(!File.Exists(marker)) {
                if(!create || Directory.EnumerateFileSystemEntries(Root).Any())throw new IOException(L.English?"Choose an empty folder. Next does not modify older version data.":"请选择空目录。Next 不会修改旧版数据。");
                File.WriteAllText(marker,Marker,Encoding.UTF8);
            }
            EnsureAvailable(); Directory.CreateDirectory(Path.Combine(Root,"images")); Directory.CreateDirectory(Path.Combine(Root,"assets"));
            string index=Path.Combine(Root,"catalog.xml");
            if(File.Exists(index)) {
                var catalog=Disk.Read<Catalog>(index); if(catalog.Version!=2 || catalog.Records==null)throw new IOException(L.T("数据版本无效。"));
                ValidateRecords(catalog.Records); Items=catalog.Records;
                foreach(var clip in Items.Where(c=>c.Kind=="image"))if(!File.Exists(ImagePath(clip)))throw new IOException(L.T("图片文件缺失，原数据已保留。"));
            }
            string settings=Path.Combine(Root,"settings.xml"); if(File.Exists(settings))Settings=Disk.Read<Preferences>(settings); Sort();
        }
        public void EnsureAvailable() { string marker=Path.Combine(Root,"application.marker"); if(!File.Exists(marker)||File.ReadAllText(marker,Encoding.UTF8)!=Marker)throw new IOException(L.T("保存位置不可用，已停止记录。请重试。")); }
        static bool ValidId(string id) { return id!=null && id.Length==64 && id.All(c=>"0123456789abcdef".Contains(c)); }
        public static string TextId(string value) { return Disk.Hash(Encoding.UTF8.GetBytes("text\0"+value)); }
        static void ValidateRecords(List<Clip> list) {
            var ids=new HashSet<string>();
            foreach(var c in list) {
                if(c==null || !ValidId(c.Id) || !ids.Add(c.Id) || (c.Kind!="text" && c.Kind!="image") || (c.Kind=="text" && (String.IsNullOrEmpty(c.Text)||TextId(c.Text)!=c.Id)))throw new IOException(L.T("记录无效或内容校验失败。"));
                if(c.LastUsedUtc==default(DateTime))c.LastUsedUtc=c.CopiedUtc;
            }
        }
        void Sort() { Items=Items.OrderByDescending(c=>c.Pinned).ThenByDescending(c=>c.HistoryRank).ThenBy(c=>c.Id).ToList(); }
        List<Clip> Snapshot() { return Items.Select(c=>c.Clone()).ToList(); }
        void Commit(List<Clip> records) { EnsureAvailable(); Disk.Write(Path.Combine(Root,"catalog.xml"),new Catalog { Records=records }); Items=records; Sort(); }
        long NextRank(bool favorite) { return Items.Count==0?1:Items.Max(c=>favorite?c.FavoriteRank:c.HistoryRank)+1; }
        public string ImagePath(Clip c) { if(!ValidId(c.Id))throw new IOException(L.T("图片标识无效。")); return Path.Combine(Root,"images",c.Id+".png"); }
        public string AssetPath(string id) { if(!ValidId(id))throw new IOException(L.T("外观图片标识无效。")); return Path.Combine(Root,"assets",id+".png"); }
        public void SaveSettings() { EnsureAvailable(); Disk.Write(Path.Combine(Root,"settings.xml"),Settings); }
        public Clip AddText(string value) { if(String.IsNullOrEmpty(value))return null; return Save(new Clip { Id=TextId(value),Kind="text",Text=value },null); }
        public Clip CreateMemo(string text,string note) {
            if(String.IsNullOrEmpty(text))throw new IOException(L.T("正文不能为空。"));
            string id=TextId(text);var next=Snapshot();var item=next.FirstOrDefault(c=>c.Id==id);
            if(item==null){item=new Clip { Id=id,Kind="text",Text=text };next.Add(item);}
            item.Note=MergeNotes(item.Note,note);item.CopiedUtc=item.LastUsedUtc=DateTime.UtcNow;item.HistoryRank=NextRank(false);Commit(next);return Items.Single(c=>c.Id==id);
        }
        static byte[] Png(Image image) {
            using(var bitmap=new Bitmap(image.Width,image.Height,PixelFormat.Format32bppArgb)) {
                using(var g=Graphics.FromImage(bitmap)) { g.CompositingMode=System.Drawing.Drawing2D.CompositingMode.SourceCopy; g.DrawImageUnscaled(image,0,0); }
                using(var stream=new MemoryStream()) { bitmap.Save(stream,ImageFormat.Png); return stream.ToArray(); }
            }
        }
        public Clip AddImage(Image image) { var bytes=Png(image); return Save(new Clip { Id=Disk.Hash(bytes),Kind="image" },bytes); }
        void WriteBytes(string path,byte[] bytes) {
            if(File.Exists(path)) { if(Disk.Hash(File.ReadAllBytes(path))!=Disk.Hash(bytes))throw new IOException(L.T("现有图片校验失败，未覆盖。")); return; }
            using(var f=new FileStream(path+".pending",FileMode.Create,FileAccess.Write)) { f.Write(bytes,0,bytes.Length); f.Flush(true); }
            File.Move(path+".pending",path);
        }
        Clip Save(Clip content,byte[] bytes) {
            EnsureAvailable(); if(bytes!=null)WriteBytes(ImagePath(content),bytes);
            var next=Snapshot(); var existing=next.FirstOrDefault(c=>c.Id==content.Id);
            if(existing==null) { existing=content; next.Add(existing); }
            existing.CopiedUtc=existing.LastUsedUtc=DateTime.UtcNow; existing.HistoryRank=NextRank(false);
            Commit(next); return Items.First(c=>c.Id==content.Id);
        }
        public List<Clip> Search(string keyword,int kind) { return Search(keyword,kind,false); }
        public List<Clip> Search(string keyword,int kind,bool favorites) {
            var result=Items.Where(c=>(!favorites||c.Favorite) && (kind==0||c.Kind==(kind==1?"text":"image")) && (String.IsNullOrEmpty(keyword)||(c.Text??"").IndexOf(keyword,StringComparison.OrdinalIgnoreCase)>=0||(c.Note??"").IndexOf(keyword,StringComparison.OrdinalIgnoreCase)>=0));
            return favorites?result.OrderByDescending(c=>c.FavoriteRank).ThenBy(c=>c.Id).ToList():result.ToList();
        }
        public void SetFlags(string id,bool pinned,bool favorite) {
            var next=Snapshot(); var c=next.Single(x=>x.Id==id);
            if(favorite&&!c.Favorite)c.FavoriteRank=NextRank(true); if(pinned!=c.Pinned)c.HistoryRank=NextRank(false);
            c.Pinned=pinned; c.Favorite=favorite; Commit(next);
        }
        public void Touch(string id) { var next=Snapshot(); var c=next.FirstOrDefault(x=>x.Id==id); if(c==null)return; c.LastUsedUtc=DateTime.UtcNow; Commit(next); }
        public static string MergeNotes(string first,string second) {
            if(String.IsNullOrEmpty(first))return second??""; if(String.IsNullOrEmpty(second)||first==second)return first;
            if(first.Contains(second))return first;
            return first+"\n\n—— 合并备注 ——\n\n"+second;
        }
        public Clip Edit(string id,string text,string note) {
            var next=Snapshot(); var source=next.Single(c=>c.Id==id);
            if(source.Kind=="text"&&String.IsNullOrEmpty(text))throw new IOException(L.T("正文不能为空。"));
            string newId=source.Kind=="text"?TextId(text):id; var target=next.FirstOrDefault(c=>c.Id==newId&&c.Id!=id);
            source.Note=note??""; source.LastUsedUtc=DateTime.UtcNow;
            if(target!=null) { target.Note=MergeNotes(target.Note,source.Note); target.Pinned|=source.Pinned; if(!target.Favorite&&source.Favorite)target.FavoriteRank=source.FavoriteRank; target.Favorite|=source.Favorite; target.LastUsedUtc=source.LastUsedUtc; next.Remove(source); }
            else { source.Id=newId; if(source.Kind=="text")source.Text=text; target=source; }
            Commit(next); return Items.Single(c=>c.Id==target.Id);
        }
        public void Reorder(string moving,string before,bool favorites) {
            Reorder(moving,before,favorites,false);
        }
        public void Reorder(string moving,string before,bool favorites,bool after) {
            if(moving==before)return; var next=Snapshot(); var source=next.Single(c=>c.Id==moving); var target=next.Single(c=>c.Id==before);
            if(favorites&&(!source.Favorite||!target.Favorite))throw new IOException(L.T("仅可排序收藏记录。"));
            if(!favorites&&source.Pinned!=target.Pinned)throw new IOException(L.T("请在同一个置顶或普通区域内排序。"));
            var group=next.Where(c=>favorites?c.Favorite:c.Pinned==source.Pinned).OrderByDescending(c=>favorites?c.FavoriteRank:c.HistoryRank).ToList();
            group.Remove(source); group.Insert(group.IndexOf(target)+(after?1:0),source); long rank=NextRank(favorites)+group.Count;
            foreach(var c in group) { if(favorites)c.FavoriteRank=rank--; else c.HistoryRank=rank--; } Commit(next);
        }
        public void Delete(IEnumerable<string> ids) {
            var remove=new HashSet<string>(ids); var removed=Items.Where(c=>remove.Contains(c.Id)).ToList(); Commit(Snapshot().Where(c=>!remove.Contains(c.Id)).ToList());
            foreach(var c in removed.Where(c=>c.Kind=="image"))File.Delete(ImagePath(c));
        }
        public List<Clip> Expired(DateTime now,int days) { return days<=0?new List<Clip>():Items.Where(c=>!c.Pinned&&!c.Favorite&&c.LastUsedUtc<now.AddDays(-days)).ToList(); }
        public int Cleanup(DateTime now) { var expired=Expired(now,Settings.CleanupDays); if(expired.Count>0)Delete(expired.Select(c=>c.Id)); return expired.Count; }
        public string SaveAsset(string filename) { EnsureAvailable(); using(var image=Image.FromFile(filename)) { byte[] bytes=Png(image); string id=Disk.Hash(bytes); WriteBytes(AssetPath(id),bytes); return id; } }
        public string SaveAsset(Image image){EnsureAvailable();byte[] bytes=Png(image);string id=Disk.Hash(bytes);WriteBytes(AssetPath(id),bytes);return id;}
        public void Export(string path) {
            EnsureAvailable(); var backup=new Backup { Records=Snapshot() };
            foreach(var c in Items.Where(c=>c.Kind=="image")) { byte[] bytes=File.ReadAllBytes(ImagePath(c)); if(Disk.Hash(bytes)!=c.Id)throw new IOException(L.T("图片损坏，未导出备份。")); backup.Images.Add(new BackupImage { Id=c.Id,Bytes=bytes }); }
            Disk.Write(path,backup);
        }
        public ImportResult Import(string path) {
            EnsureAvailable(); if(new FileInfo(path).Length>512L*1024*1024)throw new IOException(L.T("当前测试版单次导入最大 512 MB，请拆分备份。"));
            var backup=Disk.Read<Backup>(path); if(backup.Version!=2||backup.Records==null||backup.Images==null)throw new IOException(L.T("不是有效的 Next 备份。"));
            ValidateRecords(backup.Records); var images=new Dictionary<string,byte[]>();
            foreach(var image in backup.Images) {
                if(image==null||!ValidId(image.Id)||image.Bytes==null||Disk.Hash(image.Bytes)!=image.Id||images.ContainsKey(image.Id))throw new IOException(L.T("备份图片校验失败。"));
                using(var stream=new MemoryStream(image.Bytes))using(var decoded=Image.FromStream(stream)) { if(decoded.Width<1||decoded.Height<1)throw new IOException(L.T("图片无效。")); }
                images.Add(image.Id,image.Bytes);
            }
            foreach(var c in backup.Records.Where(c=>c.Kind=="image"))if(!images.ContainsKey(c.Id))throw new IOException(L.T("备份缺少图片。"));
            var next=Snapshot(); var result=new ImportResult(); long rank=next.Count==0?0:next.Min(c=>c.HistoryRank)-1; var added=new List<Clip>();
            foreach(var incoming in backup.Records.OrderByDescending(c=>c.HistoryRank)) {
                var current=next.FirstOrDefault(c=>c.Id==incoming.Id);
                if(current==null) { current=incoming.Clone(); current.HistoryRank=rank--; next.Add(current); added.Add(current); result.Added++; }
                else { bool wasFavorite=current.Favorite; current.Pinned|=incoming.Pinned; current.Favorite|=incoming.Favorite; current.Note=MergeNotes(current.Note,incoming.Note); current.LastUsedUtc=current.LastUsedUtc>incoming.LastUsedUtc?current.LastUsedUtc:incoming.LastUsedUtc; if(!wasFavorite&&current.Favorite)added.Add(current); result.Merged++; }
            }
            long favoriteRank=Items.Count==0?0:Items.Min(c=>c.FavoriteRank)-1;
            foreach(var c in added.Where(c=>c.Favorite).OrderByDescending(c=>backup.Records.Single(b=>b.Id==c.Id).FavoriteRank))c.FavoriteRank=favoriteRank--;
            foreach(var c in backup.Records.Where(c=>c.Kind=="image"))WriteBytes(ImagePath(c),images[c.Id]);
            Commit(next); return result;
        }
        public static string TargetRoot(string selected) { return Path.Combine(Disk.Canonical(selected),"ClipboardNextData"); }
        IEnumerable<string> ManagedFiles() {
            yield return "application.marker"; yield return "catalog.xml"; yield return "settings.xml";
            foreach(var c in Items.Where(c=>c.Kind=="image"))yield return Path.Combine("images",c.Id+".png");
            foreach(string id in new[]{Settings.BackgroundId,Settings.BallImageId,Settings.BackgroundOriginalId,Settings.BallOriginalId}.Where(id=>!String.IsNullOrEmpty(id)).Distinct()) { AssetPath(id); yield return Path.Combine("assets",id+".png"); }
        }
        public Store CopyVerified(string selected) {
            EnsureAvailable(); SaveSettings(); Commit(Snapshot()); string target=TargetRoot(selected); string canonical=Disk.Canonical(target);
            if(canonical.Equals(Root,StringComparison.OrdinalIgnoreCase)||canonical.StartsWith(Root+"\\",StringComparison.OrdinalIgnoreCase)||Root.StartsWith(canonical+"\\",StringComparison.OrdinalIgnoreCase))throw new IOException(L.T("源目录与目标目录不可互相包含。"));
            if(Directory.Exists(target)||File.Exists(target))throw new IOException(L.T("目标已有 Next 数据目录，请选择其他位置。"));
            Directory.CreateDirectory(target);
            foreach(string relative in ManagedFiles()) { string dest=Path.Combine(target,relative); Directory.CreateDirectory(Path.GetDirectoryName(dest)); File.Copy(Path.Combine(Root,relative),dest,false); if(Disk.Hash(File.ReadAllBytes(dest))!=Disk.Hash(File.ReadAllBytes(Path.Combine(Root,relative))))throw new IOException(L.T("迁移校验失败，原数据保留。")); }
            return new Store(target,false);
        }
        public void CleanMigratedCopy() { EnsureAvailable(); foreach(string relative in ManagedFiles().Where(f=>f!="application.marker"))File.Delete(Path.Combine(Root,relative)); File.Delete(Path.Combine(Root,"application.marker")); }
    }
}
