using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Net;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NWamp;
using Svg;

namespace Posh
{
	/// <summary>
	/// The Wamp Server
	/// </summary>
	public class WAMPServer: IDisposable
	{
		private bool FDisposed = false;
		
		public ISvgEventCaller EventCaller;
		
		public Dictionary<string, string> SessionNames = new Dictionary<string, string>();
		//contexts
		//public RemoteContext MainLoopUpdateContext = new RemoteContext();
		public RemoteContext RemoteContext = new RemoteContext();
		
		public Action<bool, bool, bool, int> OnKeyDown;
		public Action<bool, bool, bool, int> OnKeyUp;
		public Action<bool, bool, bool, char> OnKeyPress;
		
		public Action<object, SessionEventArgs> OnSessionCreated;
		public Action<object, SessionEventArgs> OnSessionClosed;
		public Func<string> OnDump;
		
		//network connection
		private SuperWebSocketWampListener WampListener;
		
		private static object Map(object source, Type sourceType, Type destinationType)
        {
            if (sourceType == destinationType) return source;
            if (source == null) return source;
            
            if(source is JToken)
            {
		        var jobj = source as JToken;
		        return jobj.ToObject(destinationType);
            }
            else
            {
            	return Convert.ChangeType(source, destinationType);
            }
        }
		
		public WAMPServer(int port)
		{
			WampListener = new SuperWebSocketWampListener(IPAddress.Any, port, JsonConvert.SerializeObject, JsonConvert.DeserializeObject<object[]>, Map);
			WampListener.Listen();
			WampListener.FixedTopics = true;
			WampListener.CreateTopic("add");
			WampListener.CreateTopic("updateattribute");
			WampListener.CreateTopic("updatecontent");
			WampListener.CreateTopic("remove");
			WampListener.SessionCreated += SessionCreated;
			WampListener.SessionClosed += SessionClosed;
			WampListener.CallInvoked += PublishAll;
			
			//publish all stuff aufter each call from remote
			AutoPublishAllAfterRemoteCall = true;
			
			WampListener.RegisterFunc<string>("dump", Dump);
			WampListener.RegisterFunc<string, string, string>("setSessionName", SetSessionName);
            WampListener.RegisterAction<bool, bool, bool, int>("keydown", KeyDown);
            WampListener.RegisterAction<bool, bool, bool, int>("keyup", KeyUp);
            WampListener.RegisterAction<bool, bool, bool, char>("keypress", KeyPress);

			//create event caller for svg            
			EventCaller = new SvgEventCaller(WampListener);
		}
		
		private bool FAutoPublishAfterRemoteCall;
		public bool AutoPublishAllAfterRemoteCall
		{
			get
			{
				return FAutoPublishAfterRemoteCall;
			}
			set
			{
				if(value != FAutoPublishAfterRemoteCall)
				{
					FAutoPublishAfterRemoteCall = value;
					if(FAutoPublishAfterRemoteCall)
					{
						WampListener.CallInvoked += PublishAll;
					}
					else
					{
						WampListener.CallInvoked -= PublishAll;
					}
				}
			}
		}
		

		#region destructor
		// Implementing IDisposable's Dispose method.
		// Do not make this method virtual.
		// A derived class should not be able to override this method.
		public void Dispose()
		{
			Dispose(true);
			// Take yourself off the Finalization queue
			// to prevent finalization code for this object
			// from executing a second time.
			GC.SuppressFinalize(this);
		}
		
		// Dispose(bool disposing) executes in two distinct scenarios.
		// If disposing equals true, the method has been called directly
		// or indirectly by a user's code. Managed and unmanaged resources
		// can be disposed.
		// If disposing equals false, the method has been called by the
		// runtime from inside the finalizer and you should not reference
		// other objects. Only unmanaged resources can be disposed.
		protected virtual void Dispose(bool disposing)
		{
			// Check to see if Dispose has already been called.
			if(!FDisposed)
			{
				if(disposing)
				{
					// Dispose managed resources.
					WampListener.SessionCreated -= SessionCreated;
					WampListener.SessionClosed -= SessionClosed;
					WampListener.CallInvoked -= PublishAll;
					
					WampListener.Dispose();
					WampListener = null;
				}
				// Release unmanaged resources. If disposing is false,
				// only the following code is executed.
				
				
				// Note that this is not thread safe.
				// Another thread could start disposing the object
				// after the managed resources are disposed,
				// but before the disposed flag is set to true.
				// If thread safety is necessary, it must be
				// implemented by the client.
			}
			FDisposed = true;
		}

		// Use C# destructor syntax for finalization code.
		// This destructor will run only if the Dispose method
		// does not get called.
		// It gives your base class the opportunity to finalize.
		// Do not provide destructors in types derived from this class.
		~WAMPServer()
		{ 
			// Do not re-create Dispose clean-up code here.
			// Calling Dispose(false) is optimal in terms of
			// readability and maintainability.
			Dispose(false);
		}
		#endregion destructor
		
		//publish json massage with updated attributes
		void PublishUpdate(object sender, EventArgs e)
		{
			if(RemoteContext.HasAttributeUpdates())
			{
				var json = RemoteContext.GetAttributeUpdateJson();
				WampListener.Publish("updateattribute", "listener", json, null, null, false);
			}
		}
		
		//publish json massage with updated attributes
		void PublishContent(object sender, EventArgs e)
		{
			if(RemoteContext.HasContentUpdates())
			{
				var json = RemoteContext.GetContentUpdateJson();
				WampListener.Publish("updatecontent", "listener", json, null, null, false);
			}
		}
		
		//add messages
		public void PublishAdd(object sender, EventArgs notInUse)
		{
			if(RemoteContext.HasAddElements())
			{
				var xml = RemoteContext.GetAddXML();
				WampListener.Publish("add", "listener", xml, null, null, false);
			}
		}
		
		//remove messages
		public void PublishRemove(object sender, EventArgs e)
		{
			if(RemoteContext.HasRemoveElements())
			{
				var json = RemoteContext.GetRemoveJson();
				WampListener.Publish("remove", "listener", json, null, null, false);
			}
		}
		
		//publish all
		public void PublishAll(object sender, EventArgs notInUse)
		{
			PublishAdd(sender, notInUse);
			PublishUpdate(sender, notInUse);
			PublishContent(sender, notInUse);
			PublishRemove(sender, notInUse);
		}
		
		private void SessionCreated(object sender, SessionEventArgs e)
		{
			if (SessionNames.ContainsKey(e.SessionId))
				return; //todo: log an error

			SessionNames.Add(e.SessionId, e.SessionId);

            if (OnSessionCreated != null)
			    OnSessionCreated(sender, e);
		}
		
		private void SessionClosed(object sender, SessionEventArgs e)
		{
			if (OnSessionClosed != null)
			    OnSessionClosed(sender, e);
			
			if (SessionNames.ContainsKey(e.SessionId))
				SessionNames.Remove(e.SessionId);
		}
		
		private string Dump()
		{
			return OnDump();
		}
		
		private string SetSessionName(string sessionID, string sessionName)
		{
			//note: we want sessionNames to be unique
			if (SessionNames.ContainsValue(sessionName))
				while (SessionNames.ContainsValue(sessionName))
					sessionName += "v";
	
			SessionNames[sessionID] = sessionName;
						
			return sessionName;
		}
		
		private void KeyDown(bool ctrl, bool shift, bool alt, int keyCode)
		{
            if (OnKeyDown != null)
			    OnKeyDown(ctrl, shift, alt, keyCode);
		}
		
		private void KeyUp(bool ctrl, bool shift, bool alt, int keyCode)
		{
            if (OnKeyUp != null)
                OnKeyUp(ctrl, shift, alt, keyCode);
		}
		
		private void KeyPress(bool ctrl, bool shift, bool alt, char key)
		{
            if (OnKeyPress != null)
                OnKeyPress(ctrl, shift, alt, key);
		}
	}
	
	#region IDGenerator
	public static class IDGenerator
	{
		static int ID = 0;
		public static string NewID
		{
			get
			{
				return (++ID).ToString();
			}
		}
	}
	#endregion
	
	#region SvgEventCaller
	public class SvgEventCaller: ISvgEventCaller
	{
		private WampListener FListener;
		
		public SvgEventCaller(WampListener l)
		{
			this.FListener = l;
		}
		
		public void RegisterAction(string rpcID, Action action)
		{
			FListener.RegisterAction(rpcID, action);
		}
		
		public void RegisterAction<T1>(string rpcID, Action<T1> action)
		{
			FListener.RegisterAction(rpcID, action);
		}
		
		public void RegisterAction<T1, T2>(string rpcID, Action<T1, T2> action)
		{
			FListener.RegisterAction(rpcID, action);
		}
		
		public void RegisterAction<T1, T2, T3>(string rpcID, Action<T1, T2, T3> action)
		{
			FListener.RegisterAction(rpcID, action);
		}
		
		public void RegisterAction<T1, T2, T3, T4>(string rpcID, Action<T1, T2, T3, T4> action)
		{
			FListener.RegisterAction(rpcID, action);
		}
		
		public void UnregisterAction(string rpcID)
		{
			FListener.UnregisterRpcAction(rpcID);
		}
		
		public void RegisterAction<T1, T2, T3, T4, T5>(string rpcID, Action<T1, T2, T3, T4, T5> action)
		{
			FListener.RegisterAction(rpcID, action);
		}
	}
	#endregion SvgEventCaller
	
}
