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
			string result;
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
				
				result = JsonConvert.SerializeObject(this);
				ClearUpdate();
			}
			
			return result;
			
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
			lock(AddElements)
			{
				if(!ExistsAddElement(element))
				{
					AddElements.Add(element);
				}
			}
		}
		
		public void InsertBefore(SvgElement element, SvgElement sibling)
		{
			lock(AddElements)
			{
				if(!ExistsAddElement(element))
				{
					element.CustomAttributes["insertBeforeID"] = sibling.ID;
					
					AddElements.Add(element);
				}
			}
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
			string result;
			lock(AddElements)
			{
				//add items to svg group
				var svgString = "<g id=\"add\" sessionName=\"" + SessionName + "\">";
				foreach (var item in AddElements)
				{
					svgString += item.GetXML();
				}
	
				//make full svg string
				result = "<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" version=\"1.1\">" + svgString + "</g></svg>";
				ClearAdd();
			}
			return result;
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
			lock(RemoveIDList)
			{
				if(ExistsRemoveID(id))
					return;
				
				RemoveIDList.Add(id);
			}
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
			string result;
			lock(RemoveIDList)
			{
				var remover = new RemoveJson();
				remover.SessionName = SessionName;
				remover.RemoveIDList = RemoveIDList;
				result = JsonConvert.SerializeObject(remover);
				ClearRemove();
			}
			return result;
		}
		#endregion remove
		
		public void ClearAll()
		{
			ClearAdd();
			ClearUpdate();
			ClearRemove();
		}
	}
}
