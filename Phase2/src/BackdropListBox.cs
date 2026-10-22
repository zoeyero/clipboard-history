using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
namespace IrisClipboardNext {
    // Draw the complete list into one buffer; native owner-drawn ListBox erased
    // the image background separately on each drag event, causing visible flashes.
    class BackdropListBox : Control {
        public readonly List<object> Items=new List<object>();
        public Action<Graphics,Rectangle> PaintBackdrop;
        public event DrawItemEventHandler DrawItem;
        public DrawMode DrawMode {get;set;}
        public BorderStyle BorderStyle {get;set;}
        public bool IntegralHeight {get;set;}
        VScrollBar scrollbar=new VScrollBar();int top,itemHeight=112,selected=-1;bool updating;
        public BackdropListBox(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.Selectable,true);TabStop=true;Controls.Add(scrollbar);scrollbar.ValueChanged+=(s,e)=>{top=scrollbar.Value;Invalidate();};}
        public int ItemHeight{get{return itemHeight;}set{itemHeight=Math.Max(1,value);UpdateScroll();Invalidate();}}
        public int SelectedIndex{get{return selected;}set{selected=value;Invalidate();}}
        public int TopIndex{get{return top;}set{top=Math.Max(0,Math.Min(value,Math.Max(0,Items.Count-VisibleRows)));UpdateScroll();Invalidate();}}
        int VisibleRows{get{return Math.Max(1,ClientSize.Height/itemHeight);}}
        public void BeginUpdate(){updating=true;}
        public void EndUpdate(){updating=false;selected=Math.Min(selected,Items.Count-1);UpdateScroll();Invalidate();}
        void UpdateScroll(){if(scrollbar==null)return;top=Math.Max(0,Math.Min(top,Math.Max(0,Items.Count-VisibleRows)));scrollbar.Visible=Items.Count*itemHeight>ClientSize.Height;scrollbar.SetBounds(Math.Max(0,ClientSize.Width-18),0,18,ClientSize.Height);scrollbar.Minimum=0;scrollbar.LargeChange=VisibleRows;scrollbar.Maximum=Math.Max(0,Items.Count-1);scrollbar.Value=top;}
        public Rectangle GetItemRectangle(int index){return new Rectangle(0,(index-top)*itemHeight,Math.Max(1,ClientSize.Width-(scrollbar.Visible?18:0)),itemHeight);}
        public int IndexFromPoint(Point p){if(p.X<0||p.Y<0||p.Y>=ClientSize.Height||p.X>=ClientSize.Width-(scrollbar.Visible?18:0))return -1;int i=top+p.Y/itemHeight;return i<Items.Count?i:-1;}
        protected override void OnResize(EventArgs e){base.OnResize(e);UpdateScroll();}
        protected override void OnPaintBackground(PaintEventArgs e){}
        protected override void OnPaint(PaintEventArgs e){if(updating)return;if(PaintBackdrop!=null)PaintBackdrop(e.Graphics,ClientRectangle);else e.Graphics.Clear(BackColor);for(int i=top;i<Items.Count&&GetItemRectangle(i).Top<ClientSize.Height;i++){Rectangle row=GetItemRectangle(i);if(!row.IntersectsWith(e.ClipRectangle))continue;var state=e.Graphics.Save();e.Graphics.SetClip(Rectangle.Intersect(row,e.ClipRectangle));if(DrawItem!=null)DrawItem(this,new DrawItemEventArgs(e.Graphics,Font,row,i,i==selected?DrawItemState.Selected:DrawItemState.None));e.Graphics.Restore(state);}base.OnPaint(e);}
        protected override void OnMouseDown(MouseEventArgs e){Focus();selected=IndexFromPoint(e.Location);base.OnMouseDown(e);}
        protected override void OnMouseWheel(MouseEventArgs e){TopIndex-=Math.Sign(e.Delta)*3;base.OnMouseWheel(e);}
        protected override bool IsInputKey(Keys keyData){return keyData==Keys.Up||keyData==Keys.Down||base.IsInputKey(keyData);}
        protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Down||e.KeyCode==Keys.Up){selected=Math.Max(0,Math.Min(Items.Count-1,selected+(e.KeyCode==Keys.Down?1:-1)));if(selected<top)TopIndex=selected;if(selected>=top+VisibleRows)TopIndex=selected-VisibleRows+1;e.Handled=true;Invalidate();}base.OnKeyDown(e);}
        internal void TestPointerDown(Point p){OnMouseDown(new MouseEventArgs(MouseButtons.Left,1,p.X,p.Y,0));}
        internal void TestPointerMove(Point p){OnMouseMove(new MouseEventArgs(MouseButtons.Left,0,p.X,p.Y,0));}
        internal void TestPointerUp(Point p){OnMouseUp(new MouseEventArgs(MouseButtons.Left,1,p.X,p.Y,0));}
        internal DragDropEffects TestDrop(IDataObject data,Point p){Point screen=PointToScreen(p);var args=new DragEventArgs(data,1,screen.X,screen.Y,DragDropEffects.Copy,DragDropEffects.None);OnDragEnter(args);OnDragOver(args);OnDragDrop(args);return args.Effect;}
        internal bool IsBuffered{get{return GetStyle(ControlStyles.OptimizedDoubleBuffer)&&GetStyle(ControlStyles.AllPaintingInWmPaint);}}
    }
}
