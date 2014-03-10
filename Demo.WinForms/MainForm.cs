using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using NWamp;
using Posh;
using PoshDemo;
using Svg;
using Svg.Transforms;

namespace PoshDemo
{
	public static class SvgExtentions
	{
		public static void Select(this SvgRectangle rect)
		{
			rect.Stroke = new SvgColourServer(Color.Red);
			rect.StrokeWidth = 2;
		}
		
		public static void Unselect(this SvgRectangle rect)
		{
			rect.Stroke = new SvgColourServer(Color.Transparent);
		}
	}
	
	//per session stuff
	public class SessionParameters
	{
		public SessionParameters(string id)
		{
			//selection rect
			SelectionRect = new SvgRectangle();
			SelectionRect.FillOpacity = 0.1f;
			SelectionRect.Stroke = new SvgColourServer(Color.Black);
			SelectionRect.StrokeWidth = 1;
			SelectionRect.CustomAttributes["pointer-events"] = "none";
			
			Label = new SvgText();
			Label.FontSize = 12;
			Label.FontFamily = "Lucida Sans Unicode";
			Label.Text = id;
			Label.X = -1000;
			Label.CustomAttributes["pointer-events"] = "none";
			Label.Fill = new SvgColourServer(Color.Black);
		}
		
		public IMouseEventHandler MouseHandler;
		public List<SvgRectangle> SelectedRects = new List<SvgRectangle>();
		public SvgRectangle SelectionRect;
		public SvgText Label;
	}
	
	/// <summary>
	/// Description of MainForm.
	/// </summary>
	public partial class MainForm : Form
	{
		WAMPServer FWAMPServer;
		Action<string> Log;
		SvgDocument ViewRoot;
		SvgGroup FRectGroup;
		
		List<SvgRectangle> FRects = new List<SvgRectangle>();
		Dictionary<string, SessionParameters> FSessionParams = new Dictionary<string, SessionParameters>();
		
		public MainForm()
		{
			// The InitializeComponent() call is required for Windows Forms designer support.
			InitializeComponent();
			
			Log = x => Console.WriteLine(x);

			//register a url and receive a unique websocketport for it
            var url = WebServer.AddURL("poshdemo");
			var port = WebServer.URLPort[url];
			
			//create a wampserver/websocket on the given port
			FWAMPServer = new WAMPServer(port);
			FWAMPServer.OnDump += PoshGraphDump;
			FWAMPServer.OnSessionCreated += PoshSessionCreated;
			FWAMPServer.OnSessionClosed += PoshSessionClosed;

			//setup keyboard handlers
//			FWAMPServer.OnKeyDown += KeyDownHandler;
//			FWAMPServer.OnKeyUp += KeyUpHandler;
//			FWAMPServer.OnKeyPress += KeyPressHandler;
			
			//create an svg document
			ViewRoot = new SvgDocument();

			//hand the svg document a custom idmanager that talks to the WampServer
			var manager = new SvgIdManager(ViewRoot, FWAMPServer.EventCaller, FWAMPServer.RemoteContext);
            ViewRoot.OverwriteIdManager(manager);
                       
			//fill the svg document
			SetupInitialView();
			
			//open the canvas: the window showing the view is a webbrowser navigating to the given url on localhost
			webBrowser1.Navigate("about:blank");
			webBrowser1.Navigate(new Uri("http://localhost:4444/" + url));	

			//dispose web- and wampserver
			this.Disposed += (s, e) => 
				{
					WebServer.Stop();
					FWAMPServer.Dispose();
				};
		}
		
		void SetupInitialView()
		{
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
			
			//group containing all rects
			FRectGroup = new SvgGroup();
			FRectGroup.ID = "RectGroup";
			FRectGroup.Transforms.Add(new Svg.Transforms.SvgTranslate(0, 0));
			ViewRoot.Children.Add(FRectGroup);
			
			AddSomeRects();
			
			//clear context as initial stuff will come via dump already
			FWAMPServer.RemoteContext.ClearAll();
		}
		
		void AddSomeRects()
		{
			var count = 50;
			for (int i = 0; i < count; i++)
			{
				for (int j = 0; j< count; j++)
				{
					var newRect = new SvgRectangle();
					newRect.X = i * 15 + 40;
					newRect.Y = j * 15 + 40;
					newRect.Width = 10;
					newRect.Height = 10;
					newRect.MouseDown += rect_MouseDown;
					newRect.MouseMove += rect_MouseMove;
					newRect.MouseUp += rect_MouseUp;
					newRect.ID = FRectGroup.ID + "/" + (i * count + j).ToString();
					FRectGroup.Children.Add(newRect);
					FRects.Add(newRect);
				}
			}
		}

		#region event-delegation
		//selection rect or new rect
		void background_MouseDown(object sender, MouseArg e)
		{
			if(!FSessionParams.ContainsKey(e.SessionID)) return;
			var handler = GetHandler(e.SessionID);
			if(handler != null) return;
			
			if(e.Button == 1) //select
			{
				handler = new SelectionRectangleHandler(FRects, FSessionParams[e.SessionID].SelectedRects, FRectGroup.Transforms[0],FSessionParams[e.SessionID].Label, FSessionParams[e.SessionID].SelectionRect, e.SessionID);
			}
			else if(e.Button == 3) //create new rect
			{
				var newRect = new SvgRectangle();
				newRect.MouseDown += rect_MouseDown;
				newRect.MouseMove += rect_MouseMove;
				newRect.MouseUp += rect_MouseUp;
				FRectGroup.Children.Add(newRect);
				FRects.Add(newRect);
				handler = new RectangleSizeHandler(newRect, e.SessionID, FRectGroup.Transforms[0]);
			}
			else
			{
				handler = new MoveAllRectsHandler(FRectGroup, e.SessionID);
			}
			
			FSessionParams[e.SessionID].MouseHandler = handler.MouseDown(sender, e);
			
		}

		//move on background
		void background_MouseMove(object sender, PointArg e)
		{
			HandlerDispatch(e.SessionID, handler => handler.MouseMove(sender, e));
		}

		//click in background
		void background_MouseUp(object sender, MouseArg e)
		{
			HandlerDispatch(e.SessionID, handler => handler.MouseUp(sender, e));
		}

		//click rect
		void rect_MouseDown(object sender, MouseArg e)
		{
			if(!FSessionParams.ContainsKey(e.SessionID)) return;
			var handler = GetHandler(e.SessionID);
			if(handler != null) return;
			
			if (e.Button == 1) //drag
			{
				handler = new SelectedRectsMoveHandler(FSessionParams[e.SessionID].SelectedRects, sender as SvgRectangle, e.SessionID);
				
				FSessionParams[e.SessionID].MouseHandler = handler.MouseDown(sender, e);
			}
			else //remove
			{
				//removing stuff
				FRectGroup.Children.Remove(sender as SvgRectangle);
				FRects.Remove(sender as SvgRectangle);
			}
		}
		
		//move on rect
		void rect_MouseMove(object sender, PointArg e)
		{
			HandlerDispatch(e.SessionID, handler => handler.MouseMove(sender, e));
		}
		
		//rect mouse up
		void rect_MouseUp(object sender, MouseArg e)
		{
			HandlerDispatch(e.SessionID, handler => handler.MouseUp(sender, e));
		}
		
		//rect mouse up
		void HandlerDispatch(string id, Func<IMouseEventHandler, IMouseEventHandler> func)
		{
			if(FSessionParams.ContainsKey(id))
			{
				var handler = FSessionParams[id].MouseHandler;
				if(handler != null)
				{
					FSessionParams[id].MouseHandler = func(handler);
				}
			}
		}
		
		IMouseEventHandler GetHandler(string id)
		{
			if(FSessionParams.ContainsKey(id))
			{
				return FSessionParams[id].MouseHandler;
			}
			else
			{
				return null;
			}
		}
		#endregion event-delegation

		#region posh		
		//new session/client connected
		void PoshSessionCreated(object sender, SessionEventArgs e)
		{
			Log("session created " + e.SessionId);
			var param = new SessionParameters(e.SessionId);
			FSessionParams[e.SessionId] = param;
			ViewRoot.Children.Add(param.SelectionRect);
			ViewRoot.Children.Add(param.Label);
		}

		//session closed
		void PoshSessionClosed(object sender, SessionEventArgs e)
		{
			Log("session closed " + e.SessionId);
			foreach (var rect in FSessionParams[e.SessionId].SelectedRects) 
			{
				rect.Unselect();
			}
			ViewRoot.Children.Remove(FSessionParams[e.SessionId].SelectionRect);
			FSessionParams.Remove(e.SessionId);
		}
		
		//dump whole SVG scene graph
		string PoshGraphDump()
		{
			Log("dumping");
			return ViewRoot.GetXML();
		}
		#endregion posh
	}
	
	#region handlers
	
	//selection rect
	public class SelectionRectangleHandler : MouseHandlerBase<SvgRectangle>
	{
		List<SvgRectangle> FQuads;
		List<SvgRectangle> FSelectedQuads;
		SvgTransform FRectTransform;
		SvgText FLabel;
		
		public SelectionRectangleHandler(List<SvgRectangle> quads, List<SvgRectangle> selected, SvgTransform rectTransform, SvgText label, SvgRectangle selectionRect, string sessionID)
			: base(selectionRect, sessionID, null)
		{
			FQuads = quads;
			FSelectedQuads = selected;
			FRectTransform = rectTransform;
			FLabel = label;
		}
		
		public override IMouseEventHandler MouseDown(object sender, MouseArg arg)
		{
			foreach (var rect in FSelectedQuads) 
			{
				rect.Unselect();
			}
			FSelectedQuads.Clear();
			FLabel.X = arg.x;
			FLabel.Y = arg.y;
			return base.MouseDown(sender, arg);
		}
		
		RectangleF FLastSelection;
		public override void MouseSelection(object sender, RectangleF selection)
		{
			Instance.SetRectangle(selection);
			FLastSelection = selection;
		}
		
		public override IMouseEventHandler MouseUp(object sender, MouseArg arg)
		{
			FLastSelection.Location = TransformPointInverse(FRectTransform.Matrix, FLastSelection.Location);
			foreach (var rect in FQuads) 
			{
				if(FLastSelection.Contains(rect.GetRectangle()))
				{
					rect.Select();
					FSelectedQuads.Add(rect);
				}
			}
			Instance.SetRectangle(new RectangleF(-100, -100, 0, 0));
			FLabel.X = -1000;
			return base.MouseUp(sender, arg);
		}
	}
	
	//new rectangle created
	public class RectangleSizeHandler : MouseHandlerBase<SvgRectangle>
	{
		public RectangleSizeHandler(SvgRectangle rect, string sessionID, SvgTransform groupTransform)
			: base(rect, sessionID, groupTransform)
		{
		}
		
		public override void MouseSelection(object sender, RectangleF selection)
		{
			Instance.SetRectangle(selection);
		}
	}
	
	//drag all quads
	public class MoveAllRectsHandler : MouseHandlerBase<SvgGroup>
	{
		public MoveAllRectsHandler(SvgGroup g, string sessionID)
			: base(g, sessionID, null)
		{
		}
		
		public override void MouseDrag(object sender, PointF arg, PointF delta, int dragCall)
		{
			var transform = Instance.Transforms[0].Matrix;
			Instance.Transforms[0] = new Svg.Transforms.SvgTranslate(transform.OffsetX + delta.X, transform.OffsetY + delta.Y);
		}
		
		
	}
	
	//drag selected rectangles
	public class SelectedRectsMoveHandler : MouseHandlerBase<SvgRectangle>
	{
		List<SvgRectangle> FSelectedQuads;
		public SelectedRectsMoveHandler(List<SvgRectangle> selected, SvgRectangle rect, string sessionID)
			: base(rect, sessionID, null)
		{
			FSelectedQuads = selected;
		}
		
		public override IMouseEventHandler MouseDown(object sender, MouseArg arg)
		{
			
			if(!FSelectedQuads.Contains(Instance))
			{
				foreach (var rect in FSelectedQuads) 
				{
					rect.Unselect();
				}
				FSelectedQuads.Clear();
				Instance.Select();
				FSelectedQuads.Add(Instance);
			}
			return base.MouseDown(sender, arg);
		}
		
		public override void MouseDrag(object sender, PointF arg, PointF delta, int dragCall)
		{
			foreach (var rect in FSelectedQuads)
			{
				rect.X += delta.X;
				rect.Y += delta.Y;
			}
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
		protected SvgTransform MouseTransform;
		public string SessionID { get; protected set; }

		public MouseHandlerBase(TView view, string sessionID, SvgTransform mouseTransform)
		{
			Instance = view;
			SessionID = sessionID;
			MouseTransform = mouseTransform == null ? new SvgTranslate(0, 0) : mouseTransform;
		}
		
		protected PointF TransformPointInverse(Matrix t, PointF p)
		{
			var pts = new PointF[] { p };
			t.Invert();
			t.TransformPoints(pts);
			return pts[0];
		}

		public virtual IMouseEventHandler MouseDown(object sender, MouseArg arg)
		{
			pressed = true;
			Button = arg.Button;
			StartPoint = TransformPointInverse(MouseTransform.Matrix, new PointF(arg.x, arg.y));
			LastPoint = StartPoint;
			return this;
		}

		public virtual IMouseEventHandler MouseMove(object sender, PointArg arg)
		{
			if(pressed)
			{
				var point = TransformPointInverse(MouseTransform.Matrix, new PointF(arg.x, arg.y));
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
	
	#endregion handlers
}
