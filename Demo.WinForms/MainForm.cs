using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using System.Linq;

using NWamp;
using Svg;
using Posh;

namespace PoshDemo
{
	/// <summary>
	/// Description of MainForm.
	/// </summary>
	public partial class MainForm : Form
	{
		WAMPServer FWAMPServer;
		Action<string> Log;
		SvgDocument ViewRoot;
		SvgRectangle SelectionRect;
		
		List<SvgRectangle> FQuads = new List<SvgRectangle>();
		IMouseEventHandler FMouseHandler = null;
		
		public MainForm()
		{
			
			// The InitializeComponent() call is required for Windows Forms designer support.
			InitializeComponent();
			
			this.Disposed += new EventHandler(MainForm_Disposed);
			Log = x => Console.WriteLine(x);

			//set the path to the html/js files
            WebServer.TerminalPath = Path.Combine(Path.GetDirectoryName(Assembly.GetCallingAssembly().Location), @"web");
            
			//register a url and receive a unique websocketport for it
            var url = WebServer.AddURL("poshdemo");
			var port = WebServer.URLPort[url];
			
			//create a wampserver/websocket on the given port
			FWAMPServer = new WAMPServer(port);
			FWAMPServer.OnDump += PoshGraphDump;
			FWAMPServer.OnSessionCreated += PoshSessionCreated;
			FWAMPServer.OnSessionClosed += PoshSessionClosed;
			
			//keyboard
//			FWAMPServer.OnKeyDown += FSVGTerminalProtocol_OnKeyDown;
//			FWAMPServer.OnKeyUp += FSVGTerminalProtocol_OnKeyUp;
//			FWAMPServer.OnKeyPress += FSVGTerminalProtocol_OnKeyPress;
				
			//the svg document gets a custom idmanager
			ViewRoot = new SvgDocument();
			var manager = new SvgIdManager(ViewRoot, FWAMPServer.EventCaller, FWAMPServer.RemoteContext);
            ViewRoot.OverwriteIdManager(manager);
            
			//background rect
            var background = new SvgRectangle();
			background.Width = Screen.PrimaryScreen.WorkingArea.Width;
			background.Height = Screen.PrimaryScreen.WorkingArea.Height;
			
			background.Fill = new SvgColourServer(Color.White);
			background.MouseMove += background_MouseMove;
			background.MouseDown += background_MouseDown;
			background.MouseUp += background_MouseUp;
			
			//add background to svg doc
			ViewRoot.Children.Add(background);
			
			//selection rect
			SelectionRect = new SvgRectangle();
			SelectionRect.FillOpacity = 0.1f;
			SelectionRect.Stroke = new SvgColourServer(Color.Black);
			SelectionRect.StrokeWidth = 1;
			SelectionRect.CustomAttributes["pointer-events"] = "none";
			
			ViewRoot.Children.Add(SelectionRect);
			
            FWAMPServer.RemoteContext.ClearAll();
			
			//the window showing the view is a webbrowser navigating to the given url on localhost
			webBrowser1.Navigate("about:blank");
			webBrowser1.Navigate(new Uri("http://localhost:4444/" + url));
		}

		//selection rect or new rect
		void background_MouseDown(object sender, MouseArg e)
		{
			if(FMouseHandler != null) return;
			
			if(e.Button == 1)
			{
				if(FMouseHandler == null)
				{
					FMouseHandler = new SelectionRectangleHandler(SelectionRect, e.SessionID);
				}
			}
			else
			{
				if(FMouseHandler == null)
				{
					var newRect = new SvgRectangle();
					newRect.MouseDown += rect_MouseDown;
					newRect.MouseMove += rect_MouseMove;
					newRect.MouseUp += rect_MouseUp;
					ViewRoot.Children.Add(newRect);
					FQuads.Add(newRect);
					FMouseHandler = new RectangleSizeHandler(newRect, e.SessionID);
				}
			}
			
			FMouseHandler = FMouseHandler.MouseDown(sender, e);
			
			FWAMPServer.PublishAdd(this, null);
		}

		//move on background
		void background_MouseMove(object sender, PointArg e)
		{
			if(CheckMouseHandler(e))
			{
				FMouseHandler = FMouseHandler.MouseMove(sender, e);
			}
			
		}

		//click in background
		void background_MouseUp(object sender, MouseArg e)
		{
			if(CheckMouseHandler(e))
			{
				FMouseHandler = FMouseHandler.MouseUp(sender, e);
			}
		}

		//click rect
		void rect_MouseDown(object sender, MouseArg e)
		{
			if(FMouseHandler != null) return;
			
			if (e.Button == 1)
			{
				if(FMouseHandler == null)
				{
					FMouseHandler = new RectDragHandler(sender as SvgRectangle, e.SessionID);
				}
				
				FMouseHandler = FMouseHandler.MouseDown(sender, e);
			}
			else
			{
				//removing stuff
				ViewRoot.Children.Remove(sender as SvgRectangle);
				FQuads.Remove(sender as SvgRectangle);
				FWAMPServer.PublishRemove(null, null);
			}
		}
		
		//move on rect
		void rect_MouseMove(object sender, PointArg e)
		{
			if(CheckMouseHandler(e))
			{
				FMouseHandler = FMouseHandler.MouseMove(sender, e);
			}
		}
		
		//rect mouse up
		void rect_MouseUp(object sender, MouseArg e)
		{
			if(CheckMouseHandler(e))
			{
				FMouseHandler = FMouseHandler.MouseUp(sender, e);
			}
		}
		
		//check mouse handler condition
		protected bool CheckMouseHandler(SVGArg arg)
        {
        	return FMouseHandler != null && FMouseHandler.SessionID == arg.SessionID;
        }

		//websocket stuff
		
		//new session/client connected
		void PoshSessionCreated(object sender, SessionEventArgs e)
		{
			Log("session created " + e.SessionId);
		}

		//session closed
		void PoshSessionClosed(object sender, SessionEventArgs e)
		{
			Log("session closed " + e.SessionId);
		}
		
		//dump whole SVG scene graph
		string PoshGraphDump()
		{
			Log("dumping");
			return ViewRoot.GetXML();
		}
		
		//cleanup
		void MainForm_Disposed(object sender, EventArgs e)
		{
			WebServer.Stop();
			
			FWAMPServer.Dispose();
			FWAMPServer = null;
		}
	}
	
	//selection rect
	public class SelectionRectangleHandler : RectangleSizeHandler
	{
		public SelectionRectangleHandler(SvgRectangle rect, string sessionID)
			: base(rect, sessionID)
		{
		}
		
		public override IMouseEventHandler MouseUp(object sender, MouseArg arg)
		{
			Instance.SetRectangle(new RectangleF(-100, -100, 0, 0));
			return base.MouseUp(sender, arg);
		}
	}
	
	//new rectangle created
	public class RectangleSizeHandler : MouseHandlerBase<SvgRectangle>
	{
		public RectangleSizeHandler(SvgRectangle rect, string sessionID)
			: base(rect, sessionID)
		{
		}
		
		public override void MouseSelection(object sender, RectangleF selection)
		{
			Instance.SetRectangle(selection);
		}
	}
	
	//drag a rectangle
	public class RectDragHandler : MouseHandlerBase<SvgRectangle>
	{
		
		public RectDragHandler(SvgRectangle rect, string sessionID)
			: base(rect, sessionID)
		{
		}
		
		public override void MouseDrag(object sender, PointF arg, PointF delta, int dragCall)
		{
			Instance.X += delta.X;
			Instance.Y += delta.Y;
		}
	}
	
	/// <summary>
	/// Mouse event handler interface
	/// </summary>
	public interface IMouseEventHandler
	{
		IMouseEventHandler MouseDown(object sender, MouseArg arg);
		IMouseEventHandler MouseMove(object sender, PointArg arg);
		IMouseEventHandler MouseUp(object sender, MouseArg arg);
		string SessionID { get; }
	}

	/// <summary>
	/// Does basic mouse event hadling
	/// </summary>
	public abstract class MouseHandlerBase<TView> : IMouseEventHandler where TView : SvgElement
	{
		bool pressed;
		int Button;
		PointF StartPoint;
		PointF LastPoint;
		int DragCallCounter = 0;
		protected TView Instance;
		public string SessionID { get; protected set; }

		public MouseHandlerBase(TView view, string sessionID)
		{
			Instance = view;
			SessionID = sessionID;
		}

		public virtual IMouseEventHandler MouseDown(object sender, MouseArg arg)
		{
			pressed = true;
			Button = arg.Button;
			StartPoint = new PointF(arg.x, arg.y);
			LastPoint = StartPoint;
			return this;
		}

		public virtual IMouseEventHandler MouseMove(object sender, PointArg arg)
		{
			if(pressed)
			{
				var point = new PointF(arg.x, arg.y);
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

		public virtual void MouseClick(object sender, MouseArg arg)
		{

		}

		public virtual IMouseEventHandler MouseUp(object sender, MouseArg arg)
		{
			pressed = false;
			MouseClick(sender, arg);
			return null;
		}
	}
}
