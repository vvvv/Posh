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
	/// Helper class for content updates
	/// </summary>
	public class ContentUpdate
	{
		public string id;
		public string content;
		public ContentUpdate(string id, string content)
		{
			this.id = id;
			this.content = content;
		}
	}
	
	/// <summary>
	/// Helper class for content updates
	/// </summary>
	public class ContentUpdateJsonObject
	{
		public string SessionName;
		public List<ContentUpdate> Updates = new List<ContentUpdate>();
	}
	
	/// <summary>
	/// Helper class for removes
	/// </summary>
	public class RemoveJsonObject
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
		
		public bool HasAttributeUpdates()
		{
			return FUpdates.Count > 0;
		}
		
		public string GetAttributeUpdateJson()
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
				ClearAttributeUpdates();
			}
			
			return result;
			
		}
		
		public void AddAttributeUpdate(string ID, string name, object value)
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
		
		public void ClearAttributeUpdates()
		{
			Updates.Clear();
			FUpdates.Clear();
		}
		#endregion update
		
		#region content
		private List<SvgElement> ContentElements = new List<SvgElement>();
		
		public bool HasContentUpdates()
		{
			return ContentElements.Count > 0;
		}
		
		public void AddContentUpdate(SvgElement element)
		{
			lock(ContentElements)
			{
				if(!ExistsContentElement(element))
				{
					ContentElements.Add(element);
				}
			}
		}
		
		protected bool ExistsContentElement(SvgElement elem)
		{
			//needs id
			if(string.IsNullOrEmpty(elem.ID))
				return true;
			
			return ContentElements.Contains(elem);
		}
		
		public string GetContentUpdateJson()
		{
			string result;
			lock(ContentElements)
			{
				var contender = new ContentUpdateJsonObject();
				contender.SessionName = SessionName;
				foreach (var element in ContentElements) 
				{
					contender.Updates.Add(new ContentUpdate(element.ID, element.Content));
				}
				result = JsonConvert.SerializeObject(contender);
				ClearContentUpdates();
			}
			return result;
		}
		
		void ClearContentUpdates()
		{
			ContentElements.Clear();
		}
		
		#endregion content
		
		#region add/insert
		private List<SvgElement> AddElements = new List<SvgElement>();
		
		public bool HasAddElements()
		{
			return AddElements.Count > 0;
		}
		
		public bool RemoveAddElementIfExists(SvgElement element)
		{
			return AddElements.Remove(element);
		}
		
		public void AddElement(SvgElement element)
		{
			lock(AddElements)
			{
				if(!ExistsAddElement(element))
				{
					AddElements.Add(element);
				}
			}
		}
		
		public void InsertElementBefore(SvgElement element, SvgElement sibling)
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
			
			if (AddElements.Contains(elem))
				return true;
			
			foreach (var possibleparent in AddElements)
			{
				if(string.IsNullOrEmpty(possibleparent.ID))
				   continue;
				   
				if(elem.ID.StartsWith(possibleparent.ID))
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
			
			foreach (var possibleparentID in RemoveIDList)
			{
				if(string.IsNullOrEmpty(possibleparentID))
				   continue;
				
				   
				if(id.StartsWith(possibleparentID))
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
				var remover = new RemoveJsonObject();
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
			ClearAttributeUpdates();
			ClearContentUpdates();
			ClearRemove();
		}
		
	}
}
