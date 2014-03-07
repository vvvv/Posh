using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Web.UI;

namespace Posh
{
	/// <summary>
	/// Description of SVGTerminalServer.
	/// </summary>
	public static class WebServer
	{
		const int CMaxThreads = 3;
		private static readonly Thread FListenerThread;
		private static readonly Thread[] FWorkers;
		private static readonly ManualResetEvent FStop, FReady;
		private static Queue<HttpListenerContext> FQueue;
		private static int FLastPort = 2000;
		private static HttpListener FHTTPListener = new HttpListener();
		public static string TerminalPath;
		public static Dictionary<string, int> URLPort = new Dictionary<string, int>();

		public static event Action<HttpListenerContext> ProcessRequest;
		public static Action<string> UnknownURL;
		public static Action<string> OpenURL;
		
		static WebServer()
		{
			FWorkers = new Thread[CMaxThreads];
			FQueue = new Queue<HttpListenerContext>();
			FStop = new ManualResetEvent(false);
			FReady = new ManualResetEvent(false);
			FHTTPListener = new HttpListener();
			FListenerThread = new Thread(HandleRequests);
			
			//consider a default localtion for the data
			TerminalPath = Path.Combine(Path.GetDirectoryName(Assembly.GetCallingAssembly().Location), @"..\..\posh\");
			
			//can only run public server if run as admin
			if (IsAdministrator())
				FHTTPListener.Prefixes.Add("http://*:4444/");
			else
				FHTTPListener.Prefixes.Add("http://localhost:4444/");
			
			FHTTPListener.Start();
			
			FListenerThread.Start();
			
			for (int i = 0; i < FWorkers.Length; i++)
			{
				FWorkers[i] = new Thread(Worker);
				FWorkers[i].Start();
			}
			
			ProcessRequest += ListenerCallback;
		}
		
		public static void Stop()
		{
			FStop.Set();
			FListenerThread.Join(1000);
			foreach (Thread worker in FWorkers)
				worker.Join(1000);
			FHTTPListener.Stop();
		}
		
		#region utils
		static bool IsAdministrator()
		{
			var identity = WindowsIdentity.GetCurrent();
			var principal = new WindowsPrincipal(identity);
			return principal.IsInRole (WindowsBuiltInRole.Administrator);
		}
		
		static bool IsLocalHost(string ip)
		{
			return IPAddress.IsLoopback(IPAddress.Parse(ip));
		}
		
		static string GetLocalIP()
		{
			var localIP = "?";
			var host = Dns.GetHostEntry(Dns.GetHostName());
			foreach (var ip in host.AddressList)
			{
			    if (ip.AddressFamily == AddressFamily.InterNetwork)
			    {
			        localIP = ip.ToString();
			    }
			}
			return localIP;
		}
		#endregion

		#region threading
		private static void HandleRequests()
		{
			while (FHTTPListener.IsListening)
			{
				var context = FHTTPListener.BeginGetContext(ContextReady, null);

				if (0 == WaitHandle.WaitAny(new[] { FStop, context.AsyncWaitHandle }))
					return;
			}
		}

		private static void ContextReady(IAsyncResult ar)
		{
			try
			{
				lock (FQueue)
				{
					FQueue.Enqueue(FHTTPListener.EndGetContext(ar));
					FReady.Set();
				}
			}
			catch { return; }
		}

		private static void Worker()
		{
			WaitHandle[] wait = new[] { FReady, FStop };
			while (0 == WaitHandle.WaitAny(wait))
			{
				HttpListenerContext context;
				lock (FQueue)
				{
					if (FQueue.Count > 0)
						context = FQueue.Dequeue();
					else
					{
						FReady.Reset();
						continue;
					}
				}

				try
				{
					ProcessRequest(context);
				}
				catch (Exception e)
				{
					Console.Error.WriteLine(e);
				}
			}
		}
		#endregion

		#region urlhandling
		public static string AddURL(string url)
		{
			return AddURL(url, FLastPort++);
		}
		
		public static string AddURL(string url, int port)
		{
			while (URLPort.ContainsKey(url))
				url += "v";
			
			URLPort.Add(url, port);
			
			return url;
		}
		
		public static void RemoveURL(string url)
		{
			if (URLPort.ContainsKey(url))
				URLPort.Remove(url);
		}
		
		public static string RenameURL(string from, string to)
		{
			if (URLPort.ContainsKey(from))
			{
				var port = URLPort[from];
				RemoveURL(from);
				return AddURL(to, port);
			}
			else
				return "";
		}
		#endregion
		
		public static void ListenerCallback(HttpListenerContext context)
		{
			var request = context.Request;
			
			// Obtain a response object.
			var response = context.Response;
			
			// Construct a response.
			var responseString = "";
			
			//if requested url already has a port, just return posh
			if (URLPort.ContainsKey(request.RawUrl.ToString().Substring(1)))
			{
				using (var sr = new StreamReader(Path.Combine(TerminalPath, "posh.html")))
					responseString = sr.ReadToEnd();
			}
			//request for root returns listing of all files
			else if (request.RawUrl == "/root")
			{
				var stringWriter = new StringWriter();
				// Put HtmlTextWriter in using block because it needs to call Dispose.
				using (var writer = new HtmlTextWriter(stringWriter))
				{
					writer.RenderBeginTag(HtmlTextWriterTag.Html);
					writer.RenderBeginTag(HtmlTextWriterTag.Head);
					writer.AddAttribute(HtmlTextWriterAttribute.Rel, "stylesheet");
					writer.AddAttribute(HtmlTextWriterAttribute.Type, "text/css");
					writer.AddAttribute(HtmlTextWriterAttribute.Href, "posh.css");
					writer.RenderBeginTag(HtmlTextWriterTag.Link);
					writer.RenderEndTag(); 
					writer.RenderEndTag(); 
					
					writer.RenderBeginTag(HtmlTextWriterTag.Body); 		
					
					writer.AddAttribute(HtmlTextWriterAttribute.Id, "rootlist");
					writer.RenderBeginTag(HtmlTextWriterTag.Div);
				    // Loop over some strings.
				    foreach (var file in Directory.EnumerateFiles(TerminalPath, "*.xml"))
				    {
				    	var f = Path.GetFileNameWithoutExtension(file);
						writer.RenderBeginTag(HtmlTextWriterTag.P);
						writer.AddAttribute(HtmlTextWriterAttribute.Href, "/open/" + f);
						writer.RenderBeginTag(HtmlTextWriterTag.A); 
						writer.Write(f);
						writer.RenderEndTag(); 
						writer.RenderEndTag();						
				    }
				    writer.RenderEndTag();
				    writer.RenderEndTag();
				    writer.RenderEndTag();
				}
				// Return the result.
				responseString = stringWriter.ToString();
			}
			//if requested url exists as .xml on disk, open it
			else if (File.Exists(Path.Combine(TerminalPath, request.RawUrl.Split('/').ToList().Last() + ".xml")))
			{
				//this will add url if not already open
				var url = request.RawUrl.Split('/').ToList().Last();
				OpenURL(url);
				response.Redirect("/" + url);
			}
			//if request is for non .xml document, serve it
			else if (File.Exists(Path.Combine(TerminalPath, request.RawUrl.Split('/').ToList().Last())))
			{				
				using (var sr = new StreamReader(Path.Combine(TerminalPath, request.RawUrl.Split('/').ToList().Last())))
					responseString = sr.ReadToEnd();
				
				if (request.RawUrl == "/posh.js")
				{
					var port = URLPort[request.UrlReferrer.PathAndQuery.Substring(1)];
					responseString = responseString.Replace("LOCALIP", GetLocalIP()).Replace("WEBSOCKETPORT", port.ToString());
				}
				
				if (request.RawUrl.EndsWith(".css"))
					response.ContentType = "text/css";
				else if (request.RawUrl.EndsWith(".js"))
					response.ContentType = "text/javascript";
			}
			//unknown urls open new document
			else if (UnknownURL != null)
			{
				//this may add an url
				UnknownURL(request.RawUrl.Split('/').ToList().Last());
				
				//so now try returning posh again
				using (var sr = new StreamReader(Path.Combine(TerminalPath, "posh.html")))
					responseString = sr.ReadToEnd();
			}
			
			var buffer = Encoding.UTF8.GetBytes(responseString);
			
			// Get a response stream and write the response to it.
			response.ContentLength64 = buffer.Length;
			var output = response.OutputStream;
			output.Write(buffer, 0, buffer.Length);
			
			// You must close the output stream.
			output.Close();
		}

	}
}
