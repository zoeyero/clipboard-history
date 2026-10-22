using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
using ClipboardNext;
class IconMaker {
    static void Main(string[] args) {
        int[] sizes = {16,32,48,64,256}; var images = new List<byte[]>();
        foreach(int size in sizes) using(var bitmap = new Bitmap(size,size)) {
            using(var g = Graphics.FromImage(bitmap)) Theme.Logo(g,size);
            using(var stream = new MemoryStream()) { bitmap.Save(stream,ImageFormat.Png); images.Add(stream.ToArray()); }
        }
        using(var writer = new BinaryWriter(File.Create(args[0]))) {
            writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length); int offset = 6 + 16 * sizes.Length;
            for(int i=0;i<sizes.Length;i++) { writer.Write((byte)(sizes[i]==256 ? 0 : sizes[i])); writer.Write((byte)(sizes[i]==256 ? 0 : sizes[i])); writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)32); writer.Write(images[i].Length); writer.Write(offset); offset += images[i].Length; }
            foreach(var bytes in images) writer.Write(bytes);
        }
    }
}
