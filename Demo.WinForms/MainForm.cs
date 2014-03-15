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
		PoshServer FPoshServer;
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
			FPoshServer = new PoshServer(port);
			FPoshServer.OnDump += PoshGraphDump;
			FPoshServer.OnSessionCreated += PoshSessionCreated;
			FPoshServer.OnSessionClosed += PoshSessionClosed;

			//setup keyboard handlers
//			FWAMPServer.OnKeyDown += KeyDownHandler;
//			FWAMPServer.OnKeyUp += KeyUpHandler;
//			FWAMPServer.OnKeyPress += KeyPressHandler;
			
			//create an svg document
			ViewRoot = new SvgDocument();

			//hand the svg document a custom idmanager that talks to the WampServer
			var manager = new SvgIdManager(ViewRoot, FPoshServer.SvgEventCaller, FPoshServer.RemoteContext);
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
					FPoshServer.Dispose();
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
			FPoshServer.RemoteContext.ClearAll();
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

        private VMouseEventArgs ConvertMouseArg(MouseArg arg)
        {
            var button = MouseButtons.None;
            switch (arg.Button)
	        {
                case 1:
                    button = MouseButtons.Left;
                    break;
                case 2:
                    button = MouseButtons.Middle;
                    break;
                case 3:
                    button = MouseButtons.Right;
                    break;
		        default:
                    break;
	        }
            return new VMouseEventArgs(button, arg.ClickCount, arg.x, arg.y, arg.AltKey, arg.ShiftKey, arg.CtrlKey, arg.SessionID);
        }

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
				handler = new RectangleSizeHandler(newRect, e.SessionID, FRectGroup.Transforms[0].Matrix);
			}
			else
			{
				handler = new MoveAllRectsHandler(FRectGroup, e.SessionID);
			}

            FSessionParams[e.SessionID].MouseHandler = handler.MouseDown(sender, ConvertMouseArg(e));
			
		}

		//move on background
		void background_MouseMove(object sender, MouseArg e)
		{
            HandlerDispatch(e.SessionID, handler => handler.MouseMove(sender, ConvertMouseArg(e)));
		}

		//click in background
		void background_MouseUp(object sender, MouseArg e)
		{
            HandlerDispatch(e.SessionID, handler => handler.MouseUp(sender, ConvertMouseArg(e)));
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

                FSessionParams[e.SessionID].MouseHandler = handler.MouseDown(sender, ConvertMouseArg(e));
			}
			else //remove
			{
				//removing stuff
				FRectGroup.Children.Remove(sender as SvgRectangle);
				FRects.Remove(sender as SvgRectangle);
			}
		}
		
		//move on rect
		void rect_MouseMove(object sender, MouseArg e)
		{
            HandlerDispatch(e.SessionID, handler => handler.MouseMove(sender, ConvertMouseArg(e)));
		}
		
		//rect mouse up
		void rect_MouseUp(object sender, MouseArg e)
		{
            HandlerDispatch(e.SessionID, handler => handler.MouseUp(sender, ConvertMouseArg(e)));
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
		void PoshSessionCreated(string sessionID)
		{
			Log("session created " + sessionID);
			var param = new SessionParameters(sessionID);
			FSessionParams[sessionID] = param;
			ViewRoot.Children.Add(param.SelectionRect);
			ViewRoot.Children.Add(param.Label);
		}

		//session closed
		void PoshSessionClosed(string sessionID)
		{
			Log("session closed " + sessionID);
			foreach (var rect in FSessionParams[sessionID].SelectedRects) 
			{
				rect.Unselect();
			}
			ViewRoot.Children.Remove(FSessionParams[sessionID].SelectionRect);
			FSessionParams.Remove(sessionID);
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
			: base(selectionRect, sessionID)
		{
			FQuads = quads;
			FSelectedQuads = selected;
			FRectTransform = rectTransform;
			FLabel = label;
		}

        public override IMouseEventHandler MouseDown(object sender, VMouseEventArgs arg)
		{
			foreach (var rect in FSelectedQuads) 
			{
				rect.Unselect();
			}
			FSelectedQuads.Clear();
			FLabel.X = arg.X;
			FLabel.Y = arg.Y;
			return base.MouseDown(sender, arg);
		}
		
		RectangleF FLastSelection;
		public override void MouseSelection(object sender, RectangleF selection)
		{
			Instance.SetRectangle(selection);
			FLastSelection = selection;
		}

        public override IMouseEventHandler MouseUp(object sender, VMouseEventArgs arg)
		{
			FLastSelection.Location = FRectTransform.Matrix.TransformPoint(FLastSelection.Location);
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
		public RectangleSizeHandler(SvgRectangle rect, string sessionID, Matrix groupTransform)
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
			: base(g, sessionID)
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
			: base(rect, sessionID)
		{
			FSelectedQuads = selected;
		}

        public override IMouseEventHandler MouseDown(object sender, VMouseEventArgs arg)
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
	#endregion handlers
}
