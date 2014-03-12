using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Net;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Svg;
using WampSharp;
using WampSharp.PubSub.Server;
using WampSharp.Rpc;
using WampSharp.Rpc.Server;

namespace Posh
{
	public interface IPoshRPCs
    {
        [WampRpcMethod("dump")]
        string Dump();
        
        [WampRpcMethod("setSessionName")]
        string SetSessionName(string sessionID, string sessionName);
		
        [WampRpcMethod("keyDown")]
		void KeyDown(bool ctrl, bool shift, bool alt, int keyCode);
		
        [WampRpcMethod("keyUp")]
		void KeyUp(bool ctrl, bool shift, bool alt, int keyCode);
		
        [WampRpcMethod("keyPress")]
		void KeyPress(bool ctrl, bool shift, bool alt, char key);
    }
    
	/// <summary>
	/// The Wamp Server
	/// </summary>
	public class PoshServer: IDisposable, IPoshRPCs
	{
		private bool FDisposed = false;
		
		public ISvgEventCaller EventCaller;
		
		public Dictionary<string, string> SessionNames = new Dictionary<string, string>();
		public RemoteContext RemoteContext = new RemoteContext();
		
		public Action<bool, bool, bool, int> OnKeyDown;
		public Action<bool, bool, bool, int> OnKeyUp;
		public Action<bool, bool, bool, char> OnKeyPress;
		
		public Action<string> OnSessionCreated;
		public Action<string> OnSessionClosed;
		public Func<string> OnDump;
		
		//network connection
		private DefaultWampHost FWampHost;
		
		//posh topics
		private IWampTopic FAddTopic;
		private IWampTopic FUpdateAttributeTopic;
		private IWampTopic FUpdateContentTopic;
		private IWampTopic FRemoveTopic;
		
		public PoshServer(int port)
		{
			string location = "ws://localhost:" + port +"/";
			FWampHost = new DefaultWampHost(location);
			FWampHost.Open();
			
			// Use this in order to publish events to subscribers.
			IWampTopicContainer topicContainer = FWampHost.TopicContainer;
			FAddTopic = topicContainer.CreateTopicByUri("add", true);
			FUpdateAttributeTopic = topicContainer.CreateTopicByUri("updateattribute", true);
			FUpdateContentTopic = topicContainer.CreateTopicByUri("updatecontent", true);
			FRemoveTopic = topicContainer.CreateTopicByUri("remove", true);
			
			FWampHost.HostService(this, "");
			
			FWampHost.Listener.SessionCreated += SessionCreated;
			FWampHost.Listener.SessionClosed += SessionClosed;
			FWampHost.Listener.CallInvoked += PublishAll;
			
			//publish all stuff aufter each call from remote
			AutoPublishAllAfterRemoteCall = true;
			
			//create event caller for svg            
			EventCaller = new SvgEventCaller(FWampHost);
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
						FWampHost.Listener.CallInvoked += PublishAll;
					}
					else
					{
						FWampHost.Listener.CallInvoked -= PublishAll;
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
//					WampListener.SessionCreated -= SessionCreated;
//					WampListener.SessionClosed -= SessionClosed;
//					WampListener.CallInvoked -= PublishAll;
					FWampHost.Dispose();
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
		~PoshServer()
		{ 
			// Do not re-create Dispose clean-up code here.
			// Calling Dispose(false) is optimal in terms of
			// readability and maintainability.
			Dispose(false);
		}
		#endregion destructor
		
		//publish json massage with updated attributes
		void PublishUpdate()
		{
			if(RemoteContext.HasAttributeUpdates())
			{
				var json = RemoteContext.GetAttributeUpdateJson();
				FUpdateAttributeTopic.OnNext(json);
			}
		}
		
		//publish json massage with updated attributes
		void PublishContent()
		{
			if(RemoteContext.HasContentUpdates())
			{
				var json = RemoteContext.GetContentUpdateJson();
				FUpdateContentTopic.OnNext(json);
			}
		}
		
		//add messages
		public void PublishAdd()
		{
			if(RemoteContext.HasAddElements())
			{
				var xml = RemoteContext.GetAddXML();
				FAddTopic.OnNext(xml);
			}
		}
		
		//remove messages
		public void PublishRemove()
		{
			if(RemoteContext.HasRemoveElements())
			{
				var json = RemoteContext.GetRemoveJson();
				FRemoveTopic.OnNext(json);
			}
		}
		
		//publish all
		public void PublishAll(string sessionID, string rpcID)
		{
			PublishAdd();
			PublishUpdate();
			PublishContent();
			PublishRemove();
		}
		
		private void SessionCreated(string sessionID)
		{
			if (SessionNames.ContainsKey(sessionID))
				return; //todo: log an error

			SessionNames.Add(sessionID, sessionID);

            if (OnSessionCreated != null)
			    OnSessionCreated(sessionID);
		}
		
		private void SessionClosed(string sessionID)
		{
			if (OnSessionClosed != null)
			    OnSessionClosed(sessionID);
			
			if (SessionNames.ContainsKey(sessionID))
				SessionNames.Remove(sessionID);
		}
		
		public string Dump()
		{
			return OnDump();
		}
		
		public string SetSessionName(string sessionID, string sessionName)
		{
			//note: we want sessionNames to be unique
			if (SessionNames.ContainsValue(sessionName))
				while (SessionNames.ContainsValue(sessionName))
					sessionName += "v";
	
			SessionNames[sessionID] = sessionName;
						
			return sessionName;
		}
		
		public void KeyDown(bool ctrl, bool shift, bool alt, int keyCode)
		{
            if (OnKeyDown != null)
			    OnKeyDown(ctrl, shift, alt, keyCode);
		}
		
		public void KeyUp(bool ctrl, bool shift, bool alt, int keyCode)
		{
            if (OnKeyUp != null)
                OnKeyUp(ctrl, shift, alt, keyCode);
		}
		
		public void KeyPress(bool ctrl, bool shift, bool alt, char key)
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
		private DefaultWampHost FWampHost;
		private Dictionary<string, DynamicRPC> FDynamicRPCs = new Dictionary<string, DynamicRPC>();
		
		public SvgEventCaller(DefaultWampHost host)
		{
			FWampHost = host;
		}
		
		public void RegisterAction(string rpcID, Action action)
		{
			var rpc = new DynamicRPC(rpcID);
			rpc.SetAction(action);
			FWampHost.Register(rpc);
			FDynamicRPCs.Add(rpcID, rpc);
		}
		
		public void RegisterAction<T1>(string rpcID, Action<T1> action)
		{
			var rpc = new DynamicRPC(rpcID);
			rpc.SetAction(action);
			FWampHost.Register(rpc);
			FDynamicRPCs.Add(rpcID, rpc);
		}
		
		public void RegisterAction<T1, T2>(string rpcID, Action<T1, T2> action)
		{
			var rpc = new DynamicRPC(rpcID);
			rpc.SetAction(action);
			FWampHost.Register(rpc);
			FDynamicRPCs.Add(rpcID, rpc);
		}
		
		public void RegisterAction<T1, T2, T3>(string rpcID, Action<T1, T2, T3> action)
		{
			var rpc = new DynamicRPC(rpcID);
			rpc.SetAction(action);
			FWampHost.Register(rpc);
			FDynamicRPCs.Add(rpcID, rpc);
		}
		
		public void RegisterAction<T1, T2, T3, T4>(string rpcID, Action<T1, T2, T3, T4> action)
		{
			var rpc = new DynamicRPC(rpcID);
			rpc.SetAction(action);
			FWampHost.Register(rpc);
			FDynamicRPCs.Add(rpcID, rpc);
		}
		
		public void RegisterAction<T1, T2, T3, T4, T5>(string rpcID, Action<T1, T2, T3, T4, T5> action)
		{
			var rpc = new DynamicRPC(rpcID);
			rpc.SetAction(action);
			FWampHost.Register(rpc);
			FDynamicRPCs.Add(rpcID, rpc);
		}
		
		public void UnregisterAction(string rpcID)
		{
			if (FDynamicRPCs.ContainsKey(rpcID))
			{
				FWampHost.Unregister(FDynamicRPCs[rpcID]);
				FDynamicRPCs.Remove(rpcID);
			}			
		}		
	}
	#endregion SvgEventCaller
	
}
