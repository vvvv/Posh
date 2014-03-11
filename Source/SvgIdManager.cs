using System;
using System.ComponentModel;
using System.Globalization;

using Svg;

namespace Posh
{
	/// <summary>
	/// Custom IdManager to handle the events and attribute updates
	/// </summary>
	public class SvgIdManager: SvgElementIdManager
	{
		protected ISvgEventCaller FCaller;
		protected RemoteContext RemoteContext;
		
		public SvgIdManager(SvgDocument doc, ISvgEventCaller caller, RemoteContext remoteContext)
			: base(doc)
		{
			FCaller = caller;
			RemoteContext = remoteContext;
		}
		
		public override bool AddAndFixID(SvgElement element, SvgElement sibling, bool autoFixID, Action<SvgElement, string, string> logElementOldIDNewID)
		{
			element.AttributeChanged += element_AttributeChanged;
			element.ContentChanged += element_ContentChanged;
			element.ChildAdded += element_ChildAdded;
			
			if(element is SvgVisualElement)
			{
				//register events
				if(string.IsNullOrWhiteSpace(element.ID))
				{
					if(element.Parent != null)
						element.SetAndFixID(element.Parent.ID + "/" + IDGenerator.NewID, true, null);
					else
						element.SetAndFixID(RandomString(16), true, null);
				}
				
				element.RegisterEvents(FCaller);
			}
			
			if(sibling == null)
			{
				RemoteContext.AddElement(element);
			}
			else
			{
				RemoteContext.InsertElementBefore(element, sibling);
			}
			
			return base.AddAndFixID(element, sibling, true, logElementOldIDNewID);
			
		}
		
		//any atrribute changed
		void element_AttributeChanged(object sender, AttributeEventArgs e)
		{
			var elem = sender as SvgElement;
			if(elem.ID != "TimeBar")
			{
				var val = TypeDescriptor.GetConverter(e.Value).ConvertToString(null, CultureInfo.InvariantCulture, e.Value);
				System.Diagnostics.Debug.WriteLine(elem.ID + " " + e.Attribute + " " + val);
				RemoteContext.AddAttributeUpdate((sender as SvgElement).ID, e.Attribute, e.Value);
			}
		}

		//content of element changed
		void element_ContentChanged(object sender, ContentEventArgs e)
		{
			RemoteContext.AddContentUpdate(sender as SvgElement);
		}

		void element_ChildAdded(object sender, ChildAddedEventArgs e)
		{
			if(!(sender is SvgDocument) && e.NewChild is SvgVisualElement)
			{
				var newChild = e.NewChild;
				if(!string.IsNullOrWhiteSpace(newChild.Parent.ID) && !newChild.ID.StartsWith(newChild.Parent.ID + "/"))
				{
					newChild.ApplyRecursive( elem => 
					                        {
					                        	var oldID = elem.ID.Substring(elem.ID.LastIndexOf("/") + 1);
					                        	elem.SetAndFixID(newChild.Parent.ID + "/" + oldID);
					                        });
				}
			}
		}
		
		public override void Remove(SvgElement element)
		{
			element.AttributeChanged -= element_AttributeChanged;
			element.ContentChanged -= element_ContentChanged;
			element.ChildAdded -= element_ChildAdded;
			
			if(element is SvgVisualElement)
				element.UnregisterEvents(FCaller);
			
			if(!string.IsNullOrWhiteSpace(element.ID))
			{
				RemoteContext.AddRemoveID(element.ID);
			}
			
			base.Remove(element);
		}
		
		private const string _chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
		private Random _rng = new Random();
		private string RandomString(int size)
		{
			char[] buffer = new char[size];

			for (int i = 0; i < size; i++)
			{
				buffer[i] = _chars[_rng.Next(_chars.Length)];
			}
			return new string(buffer);
		}
	}
}
