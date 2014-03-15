using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Posh;
using PoshDemo;
using Svg;
using Svg.Transforms;

namespace PoshDemo
{

	public static class Drawing2DExtentions
    {
		/// <summary>
		/// Applies the transformation to a PointF
		/// </summary>
		/// <param name="t">A Matrix</param>
		/// <param name="p">The point to transform by the matrix t</param>
		/// <returns></returns>
        public static PointF TransformPoint(this Matrix t, PointF p)
        {
            var pts = new PointF[] { p };
            t.TransformPoints(pts);
            return pts[0];
        }
    }
	
	
	/// <summary>
	/// Mouse event handler interface
	/// </summary>
	public interface IMouseEventHandler
	{
		string UserID { get; }
		IMouseEventHandler MouseDown(object sender, MouseEventArgs arg);
		IMouseEventHandler MouseMove(object sender, MouseEventArgs arg);
		IMouseEventHandler MouseUp(object sender, MouseEventArgs arg);
	}

	/// <summary>
	/// Keyboard event handler interface
	/// </summary>
	public interface IKeyEventHandler
	{
		string UserID { get; }
		IKeyEventHandler KeyDown(object sender, KeyEventArgs arg);
		IKeyEventHandler KeyUp(object sender, KeyEventArgs arg);
		IKeyEventHandler KeyPress(object sender, KeyPressEventArgs arg);
	}
	
	/// <summary>
	/// Does basic mouse event hadling
	/// </summary>
	public abstract class MouseHandlerBase<TView> : IMouseEventHandler where TView : SvgElement
	{
		bool pressed;
		MouseButtons Button;
		PointF StartPoint;
		PointF LastPoint;
		int DragCallCounter = 0;
		protected TView Instance;
		protected Matrix MouseTransform;
		protected Matrix MouseTransformInverse;
		public string UserID { get; protected set; }
		
		public MouseHandlerBase(TView view, string userID, Matrix mouseTransform)
		{
			Instance = view;
			UserID = userID;
			MouseTransform = mouseTransform;
			if (MouseTransform.IsInvertible)
			{
				MouseTransformInverse = MouseTransform.Clone();
				MouseTransformInverse.Invert();
			}
		}
		
		public MouseHandlerBase(TView view, string userID)
			: this(view, userID, new Matrix(1, 0, 0, 1, 0, 0))
		{
		}
		
		public virtual IMouseEventHandler MouseDown(object sender, MouseEventArgs arg)
		{
			pressed = true;
			Button = arg.Button;
			StartPoint = MouseTransformInverse.TransformPoint(new PointF(arg.X, arg.Y));
			LastPoint = StartPoint;
			return this;
		}
		
		public virtual IMouseEventHandler MouseMove(object sender, MouseEventArgs arg)
		{
			if(pressed)
			{
				var point = MouseTransformInverse.TransformPoint(new PointF(arg.X, arg.Y));
				MouseDrag(sender, point, new PointF(point.X - LastPoint.X, point.Y - LastPoint.Y), DragCallCounter);
				
				var rect = new RectangleF(StartPoint, new SizeF(point.X - StartPoint.X, point.Y - StartPoint.Y));
				var x = rect.X;
				var y = rect.Y;
				var w = Math.Abs(rect.Width);
				var h = Math.Abs(rect.Height);
				
				if (rect.Width < 0)
					x = x + rect.Width;
				if (rect.Height < 0)
					y = y + rect.Height;
				
				MouseSelection(sender, new RectangleF(x, y, w, h));
				LastPoint = point;
				DragCallCounter++;
			}
			
			return this;
		}
		
		public virtual void MouseDrag(object sender, PointF arg, PointF delta, int dragCall)
		{
			
		}
		
		public virtual void MouseSelection(object sender, RectangleF selection)
		{
			
		}
		
		public virtual void MouseClick(object sender, MouseEventArgs arg)
		{
			
		}
		
		public virtual IMouseEventHandler MouseUp(object sender, MouseEventArgs arg)
		{
			pressed = false;
			MouseClick(sender, arg);
			return null;
		}
	}
	
	/// <summary>
	/// Does basic key event handling
	/// </summary>
	public abstract class KeyHandlerBase : IKeyEventHandler
	{

		public KeyHandlerBase(string userID)
		{
			UserID = userID;
		}

		public string UserID
		{
			get;
			private set;
		}

		//key char
		protected bool FAnyKeyPressed;
		protected int FPressedKeyValue;
		protected Keys FPressedKeyCode;
		public virtual IKeyEventHandler KeyDown(object sender, KeyEventArgs arg)
		{
			FPressedKeyCode = arg.KeyCode;
			FPressedKeyValue = arg.KeyValue;
			FAnyKeyPressed = true;
			return this;
		}

		public virtual IKeyEventHandler KeyUp(object sender, KeyEventArgs arg)
		{
			FAnyKeyPressed = false;
			return null;
		}

		protected char FPressedKeyChar;
		public virtual IKeyEventHandler KeyPress(object sender, KeyPressEventArgs arg)
		{
			FPressedKeyChar = arg.KeyChar;
			return this;
		}
	}
}
