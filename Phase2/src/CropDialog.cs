using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace ClipboardNext {
    class CropCanvas : Control {
        Image source;RectangleF fit,selection,startRect;PointF anchor;int mode;float aspect;
        public CropCanvas(Image image,float ratio){source=image;aspect=ratio;DoubleBuffered=true;BackColor=Theme.Background;Cursor=Cursors.Cross;Resize+=(s,e)=>ResetSelection();}
        void ResetSelection(){float scale=Math.Min((Width-24f)/source.Width,(Height-24f)/source.Height);if(scale<=0)return;fit=new RectangleF((Width-source.Width*scale)/2,(Height-source.Height*scale)/2,source.Width*scale,source.Height*scale);float w=fit.Width,h=w/aspect;if(h>fit.Height){h=fit.Height;w=h*aspect;}selection=new RectangleF(fit.X+(fit.Width-w)/2,fit.Y+(fit.Height-h)/2,w,h);Invalidate();}
        public Rectangle PixelSelection{get{if(fit.Width<=0)ResetSelection();int x=Math.Max(0,(int)((selection.X-fit.X)/fit.Width*source.Width)),y=Math.Max(0,(int)((selection.Y-fit.Y)/fit.Height*source.Height));return new Rectangle(x,y,Math.Max(1,Math.Min(source.Width-x,(int)(selection.Width/fit.Width*source.Width))),Math.Max(1,Math.Min(source.Height-y,(int)(selection.Height/fit.Height*source.Height))));}}
        public Bitmap Crop(){Rectangle r=PixelSelection;var result=new Bitmap(r.Width,r.Height);using(var g=Graphics.FromImage(result)){g.CompositingMode=CompositingMode.SourceCopy;g.DrawImage(source,new Rectangle(0,0,result.Width,result.Height),r,GraphicsUnit.Pixel);}return result;}
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button!=MouseButtons.Left||!fit.Contains(e.Location))return;Capture=true;anchor=e.Location;startRect=selection;mode=Math.Abs(e.X-selection.Right)<18&&Math.Abs(e.Y-selection.Bottom)<18?2:selection.Contains(e.Location)?1:3;}
        protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(!Capture)return;if(mode==1){float x=startRect.X+e.X-anchor.X,y=startRect.Y+e.Y-anchor.Y;selection=new RectangleF(Math.Max(fit.X,Math.Min(fit.Right-startRect.Width,x)),Math.Max(fit.Y,Math.Min(fit.Bottom-startRect.Height,y)),startRect.Width,startRect.Height);}else{float x=mode==2?startRect.X:anchor.X,y=mode==2?startRect.Y:anchor.Y;float maxW=Math.Min(fit.Right-x,(fit.Bottom-y)*aspect);float w=Math.Min(maxW,Math.Max(8,Math.Max(e.X-x,(e.Y-y)*aspect)));selection=new RectangleF(x,y,Math.Max(1,w),Math.Max(1,w/aspect));}Invalidate();}
        protected override void OnMouseUp(MouseEventArgs e){Capture=false;mode=0;base.OnMouseUp(e);}
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);if(fit.Width<=0)ResetSelection();e.Graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;e.Graphics.DrawImage(source,fit);using(var shade=new SolidBrush(Color.FromArgb(135,Color.Black)))using(var region=new Region(fit)){region.Exclude(selection);e.Graphics.FillRegion(shade,region);}using(var pen=new Pen(Color.White,2))e.Graphics.DrawRectangle(pen,selection.X,selection.Y,selection.Width,selection.Height);using(var pen=new Pen(Color.FromArgb(130,Color.White))){for(int i=1;i<3;i++){e.Graphics.DrawLine(pen,selection.X+selection.Width*i/3,selection.Y,selection.X+selection.Width*i/3,selection.Bottom);e.Graphics.DrawLine(pen,selection.X,selection.Y+selection.Height*i/3,selection.Right,selection.Y+selection.Height*i/3);}}e.Graphics.FillRectangle(Brushes.White,selection.Right-7,selection.Bottom-7,14,14);}
        internal void TestSelect(Point from,Point to){OnMouseDown(new MouseEventArgs(MouseButtons.Left,1,from.X,from.Y,0));OnMouseMove(new MouseEventArgs(MouseButtons.Left,1,to.X,to.Y,0));OnMouseUp(new MouseEventArgs(MouseButtons.Left,1,to.X,to.Y,0));}
    }
    class CropDialog : ThemeForm {
        CropCanvas canvas;public Bitmap Result;
        public CropDialog(Image image,float aspect){Text=L.T("调整图片选区");Size=new Size(760,720);MinimumSize=new Size(520,500);Font=new Font("Microsoft YaHei UI",22,GraphicsUnit.Pixel);TopMost=true;StartPosition=FormStartPosition.CenterParent;
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=3,ColumnCount=1,Padding=new Padding(16)};layout.RowStyles.Add(new RowStyle(SizeType.Absolute,72));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,52));Controls.Add(layout);
            layout.Controls.Add(new Label{Text=L.T("拖动选区移动，拖动右下角调整大小。\n在选区外向右下拖动，可重新框选。"),Dock=DockStyle.Fill},0,0);canvas=new CropCanvas(image,aspect){Dock=DockStyle.Fill};layout.Controls.Add(canvas,0,1);
            var footer=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};var use=new SoftButton{Text=L.T("使用选区"),Width=160,Height=44};var cancel=new SoftButton{Text=L.T("取消"),Width=100,Height=44};footer.Controls.Add(use);footer.Controls.Add(cancel);layout.Controls.Add(footer,0,2);cancel.Click+=(s,e)=>{DialogResult=DialogResult.Cancel;Close();};use.Click+=(s,e)=>{Result=canvas.Crop();DialogResult=DialogResult.OK;Close();};
        }
    }
}
