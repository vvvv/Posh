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
		
		PointF FLastPoint;
		List<SvgRectangle> FDragElements = new List<SvgRectangle>();
		List<SvgRectangle> FQuads = new List<SvgRectangle>();
		
		public MainForm()
		{
			//
			// The InitializeComponent() call is required for Windows Forms designer support.
			//
			InitializeComponent();
			
			this.Disposed += new EventHandler(MainForm_Disposed);
			Log = x => Console.WriteLine(x);

			//set the path to the html/js files
            WebServer.TerminalPath = Path.Combine(Path.GetDirectoryName(Assembly.GetCallingAssembly().Location), @"web");
			//register a url and receive a unique websocketport for it
            var url = WebServer.AddURL("myWebSiteName");
			var port = WebServer.URLPort[url];
			
			//create a wampserver/websocket on the given port
			FWAMPServer = new WAMPServer(port);
			FWAMPServer.OnDump += Dump;
			FWAMPServer.OnSessionCreated += SessionCreated;
			FWAMPServer.OnSessionClosed += SessionClosed;
//			FWAMPServer.OnKeyDown += FSVGTerminalProtocol_OnKeyDown;
//			FWAMPServer.OnKeyUp += FSVGTerminalProtocol_OnKeyUp;
//			FWAMPServer.OnKeyPress += FSVGTerminalProtocol_OnKeyPress;
//			FWAMPServer.OnAfterPublishAttributes += new Action(FSVGTerminalProtocol_OnAfterPublishAttributes);
				
			//the svg document gets a custom idmanager
			ViewRoot = new SvgDocument();
			var manager = new SvgIdManager(ViewRoot, FWAMPServer.EventCaller, FWAMPServer.RemoteContext);
            ViewRoot.OverwriteIdManager(manager);
//            ViewRoot.MouseMove += MouseMove;
//            ViewRoot.MouseUp += MouseUp;
            //creating some view
            
            var bg = new SvgRectangle();
			bg.Width = Screen.PrimaryScreen.WorkingArea.Width;
			bg.Height = Screen.PrimaryScreen.WorkingArea.Height;
			
			var c = new SvgColourServer(Color.FromArgb(int.MaxValue));
			bg.Fill = c;
			bg.MouseMove += MouseMove;
			bg.MouseUp += MouseUp;
			ViewRoot.Children.Add(bg);
            
            var r = new Random();
            for (int i = 0; i < 10; i++)
            {
            	var g = new SvgGroup();
            	
				var rect = new SvgRectangle();
				rect.X = i * 110;
				rect.Y = 10;
				rect.Width = 100;
				rect.Height = 100;
				rect.Fill = new SvgColourServer(Color.LightGray);
				rect.MouseDown += rect_MouseDown;
				FQuads.Add(rect);
//				rect.MouseMove += rect_MouseMove;
//				rect.MouseUp += rect_MouseUp;
//				
//				var top = new SvgRectangle();
//				top.X = rect.X;
//				top.Y = rect.Y;				
//				top.Width = rect.Width;
//				top.Height = 5;
//				top.Fill = new SvgColourServer(Color.DarkGray);
//				
//				var bottom = new SvgRectangle();
//				bottom.X = rect.X;
//				bottom.Width = rect.Width;
//				bottom.Height = 5;
//				bottom.Y = rect.Y + rect.Height - bottom.Height;
//				bottom.Fill = new SvgColourServer(Color.DarkGray);
//				
//				g.Children.Add(rect);
//				g.Children.Add(top);
//				g.Children.Add(bottom);
				
				ViewRoot.Children.Add(rect);
            }
			
            FWAMPServer.RemoteContext.ClearAll();
			
			//the window showing the view is a webbrowser navigating to the given url on localhost
			webBrowser1.Navigate("about:blank");
			webBrowser1.Navigate(new Uri("http://localhost:4444/" + url));
		}

		void rect_MouseDown(object sender, MouseArg e)
		{
			if (e.Button == 1)
			{
				var index = ViewRoot.Children.ToList().FindIndex(r => (r as SvgRectangle).X > e.x);
				var rect = new SvgRectangle();
				rect.X = (ViewRoot.Children[index] as SvgRectangle).X - 50;
				rect.Y = 20;
				rect.Width = 100;
				rect.Height = 100;
				rect.MouseDown += rect_MouseDown;
				var rd = new Random();
				rect.Fill = new SvgColourServer(Color.FromArgb(rd.Next(int.MaxValue)));
				
				ViewRoot.Children.Insert(index, rect);
				FWAMPServer.PublishAdd(null, null);
				
//				FDragElements.Add((sender as SvgRectangle).Parent as SvgRectangle);
//				
////				var r = new Random();
////				for (int i = 0; i < 10; i++)
////					FDragElements.Add(ViewRoot.Children[r.Next(ViewRoot.Children.Count-1) + 1] as SvgRectangle);
//				
//				foreach (var el in FDragElements)
//				{
//					ViewRoot.Children.Remove(el);
//					ViewRoot.Children.Add(el);
//				}
//				
//				FWAMPServer.PublishRemove(null, null);
//				FWAMPServer.PublishAdd(null, null);
				
				FLastPoint = new PointF(e.x, e.y);
			}
			else
			{
				//removing stuff
				ViewRoot.Children.Remove(sender as SvgRectangle);
				FWAMPServer.PublishRemove(null, null);
			}
		}
		
		void rect_MouseMove(object sender, PointArg e)
		{
			MouseMove(sender, e);
		}
		
		void rect_MouseUp(object sender, MouseArg e)
		{
			MouseUp(sender, e);
		}
		
		void MouseMove(object sender, PointArg e)
		{
			if (FDragElements.Count > 0)
			{
				var deltaX = FLastPoint.X - e.x;
				var deltaY = FLastPoint.Y - e.y;
				
				foreach (var el in FDragElements)
				{
					el.X -= deltaX;
					el.Y -= deltaY;
				}
				
				FLastPoint = new PointF(e.x, e.y);
			}
		}
		
		void MouseUp(object sender, MouseArg e)
		{
			FDragElements.Clear();
		}

		void MainForm_Disposed(object sender, EventArgs e)
		{
			WebServer.Stop();
			
			FWAMPServer.Dispose();
			FWAMPServer = null;
		}
		
		void SessionCreated(object sender, SessionEventArgs e)
		{
			Log("session created " + e.SessionId);
		}

		void SessionClosed(object sender, SessionEventArgs e)
		{
			Log("session closed " + e.SessionId);
		}
		
		string Dump()
		{
			Log("dumping");
			return ViewRoot.GetXML();
		}
	}
}
