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
		public RemoteContext MainLoopUpdateContext = new RemoteContext();
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
			WampListener.CreateTopic("update");
			WampListener.CreateTopic("remove");
			WampListener.SessionCreated += SessionCreated;
			WampListener.SessionClosed += SessionClosed;
			
			//publish all stuff aufter each call from remote
			WampListener.CallInvoked += PublishAll;
			
			WampListener.RegisterFunc<string>("dump", Dump);
			WampListener.RegisterFunc<string, string, string>("setSessionName", SetSessionName);
            WampListener.RegisterAction<bool, bool, bool, int>("keydown", KeyDown);
            WampListener.RegisterAction<bool, bool, bool, int>("keyup", KeyUp);
            WampListener.RegisterAction<bool, bool, bool, char>("keypress", KeyPress);

			EventCaller = new SvgEventCaller(WampListener);
		}
		
		public void SetActiveSession(string sessionID)
		{
			if (SessionNames.ContainsKey(sessionID))
				RemoteContext.SessionName = SessionNames[sessionID];
			else
				RemoteContext.SessionName = "";
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
					WampListener.CallInvoked -= PublishUpdate;
					
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
			if(RemoteContext.HasUpdates())
			{
				var json = RemoteContext.GetUpdateJson();
				WampListener.Publish("update", "listener", json, null, null, false);
				RemoteContext.ClearUpdate();
			}
		}
		
		//add messages
		public void PublishAdd(object sender, EventArgs notInUse)
		{
			if(RemoteContext.HasAddElements())
			{
				var xml = RemoteContext.GetAddXML();
				WampListener.Publish("add", "listener", xml, null, null, false);
				RemoteContext.ClearAdd();
			}
		}
		
		//remove messages
		public void PublishRemove(object sender, EventArgs e)
		{
			if(RemoteContext.HasRemoveElements())
			{
				var json = RemoteContext.GetRemoveJson();
				WampListener.Publish("remove", "listener", json, null, null, false);
				RemoteContext.ClearRemove();
			}
		}
		
		public void PublishMainLoopAttributes()
		{
			if(MainLoopUpdateContext.HasUpdates())
			{
				MainLoopUpdateContext.SessionName = "mainloop";
				var json = MainLoopUpdateContext.GetUpdateJson();
				
	            WampListener.Publish("update", "listener", json, null, null, false);
	
	            MainLoopUpdateContext.ClearUpdate();
			}
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
		
		public void PublishAll(object sender, EventArgs notInUse)
		{
			PublishAdd(sender, notInUse);
			PublishUpdate(sender, notInUse);
			PublishRemove(sender, notInUse);
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
	
	#region RemoteContext
	
	/// <summary>
	/// Helper class for attribute updates
	/// </summary>
	public class AttributeUpdate
	{
		public string id;
		public object attributes;
		public AttributeUpdate(string id, object attributes)
		{
			this.id = id;
			this.attributes = attributes;
		}
	}
	
	/// <summary>
	/// Helper class for removes
	/// </summary>
	public class RemoveJson
	{
		public string SessionName;
		public List<string> RemoveIDList = new List<string>();
	}
	
	public class RemoteContext
	{
		#region update
		public string SessionName;
		public List<AttributeUpdate> Updates = new List<AttributeUpdate>();
		private Dictionary<string, Dictionary<string, object>> FUpdates = new Dictionary<string, Dictionary<string, object>>();
		
		public bool HasUpdates()
		{
			return FUpdates.Count > 0;
		}
		
		public string GetUpdateJson()
		{
			var ret = "";
			lock(FUpdates)
			{
				//copy and convert dictionary
				foreach (var attrs in FUpdates)
				{
					var dic = new Dictionary<string, string>();
					
					foreach (var att in FUpdates[attrs.Key])
					{
						//convert to string
						var converter = att.Key == "visibility" ? new SvgBoolConverter() : TypeDescriptor.GetConverter(att.Value);
						dic[att.Key] = converter.ConvertToString(null, CultureInfo.InvariantCulture, att.Value);
					}

					Updates.Add(new AttributeUpdate(attrs.Key, dic));
				}
				
				ret = JsonConvert.SerializeObject(this);
			}
			
			return ret;
			
		}
		
		public void AddAttribute(string ID, string name, object value)
		{
			lock(FUpdates)
			{
				//create new dictionary?
				if(!FUpdates.ContainsKey(ID))
				{
					FUpdates[ID] = new Dictionary<string, object>();
				}
				
				//insert value
				FUpdates[ID][name] = value;
			}
		}
		
		public void ClearUpdate()
		{
			Updates.Clear();
			FUpdates.Clear();
		}
		#endregion update
		
		#region add/insert
		private List<SvgElement> AddElements = new List<SvgElement>();
		
		public bool HasAddElements()
		{
			return AddElements.Count > 0;
		}
		
		public void Add(SvgElement element)
		{
			if(ExistsAddElement(element))
				return;
			
			AddElements.Add(element);
		}
		
		public void InsertBefore(SvgElement element, SvgElement sibling)
		{
			if(ExistsAddElement(element))
				return;
			
			element.CustomAttributes["insertBeforeID"] = sibling.ID;
			
			AddElements.Add(element);
		}
		
		protected bool ExistsAddElement(SvgElement elem)
		{
			if(string.IsNullOrEmpty(elem.ID))
				return true;
			
			foreach (var parent in AddElements)
			{
				if(string.IsNullOrEmpty(parent.ID))
				   continue;
				
				   
				if(elem.ID.StartsWith(parent.ID))
					return true;
			}
			
			return false;
		}
		
		public void ClearAdd()
		{
			AddElements.Clear();
		}
		
		public string GetAddXML()
		{
			//add items to svg group
			var svgString = "<g id=\"add\" sessionName=\"" + SessionName + "\">";
			foreach (var item in AddElements)
			{
				//fix id?
				if(item.Parent != null && !item.ID.StartsWith(item.Parent.ID + "/"))
				{
					item.SetAndFixID(item.Parent.ID + "/" + item.ID);
				}
				svgString += item.GetXML();
            }

			//make full svg string
			return "<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" version=\"1.1\">" + svgString + "</g></svg>";
		}
		#endregion add/insert
		
		#region remove
		private List<string> RemoveIDList = new List<string>();
		
		public bool HasRemoveElements()
		{
			return RemoveIDList.Count > 0;
		}
		
		public void AddRemoveID(string id)
		{
			if(ExistsRemoveID(id))
				return;
			
			RemoveIDList.Add(id);
		}
		
		protected bool ExistsRemoveID(string id)
		{
			if(string.IsNullOrEmpty(id))
				return true;
			
			foreach (var parentID in RemoveIDList)
			{
				if(string.IsNullOrEmpty(parentID))
				   continue;
				
				   
				if(id.StartsWith(parentID))
					return true;
			}
			
			return false;
		}
		
		public void ClearRemove()
		{
			RemoveIDList.Clear();
		}
		
		public string GetRemoveJson()
		{
			var remover = new RemoveJson();
			remover.SessionName = SessionName;
			remover.RemoveIDList = RemoveIDList;
			return JsonConvert.SerializeObject(remover);
		}
		#endregion remove
		
		public void ClearAll()
		{
			ClearAdd();
			ClearUpdate();
			ClearRemove();
		}
	}
	
	#endregion RemoteContext
}
