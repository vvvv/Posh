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
			doc.ChildAdded += element_ChildAdded;
		}
		
		public override bool AddAndForceUniqueID(SvgElement element, SvgElement sibling, bool autoForceUniqueID, Action<SvgElement, string, string> logElementOldIDNewID)
		{
			//register events
			element.AttributeChanged += element_AttributeChanged;
			element.ContentChanged += element_ContentChanged;
			element.ChildAdded += element_ChildAdded;
			
			if(element is SvgVisualElement)
			{
				//check id
				if(string.IsNullOrWhiteSpace(element.ID))
				{
					if(element.Parent != null)
						element.SetAndForceUniqueID(element.Parent.ID + "_" + IDGenerator.NewID, true, null);
					else
						element.SetAndForceUniqueID(RandomString(16), true, null);
				}
			}
			
			return base.AddAndForceUniqueID(element, sibling, true, logElementOldIDNewID);
		}
		
		//any atrribute changed
		void element_AttributeChanged(object sender, AttributeEventArgs e)
		{
			//setting "inserBeforeID" should not trigger an attributeupdate
			//it is specially used for adding elements at a specific place in the DOM
			if (e.Attribute != "insertBeforeID")
			{
				var elem = sender as SvgElement;
				var val = TypeDescriptor.GetConverter(e.Value).ConvertToString(null, CultureInfo.InvariantCulture, e.Value);
				//System.Diagnostics.Debug.WriteLine(elem.ID + " " + e.Attribute + " " + val);
				RemoteContext.AddAttributeUpdate((sender as SvgElement).ID, e.Attribute, e.Value);
			}
		}

		//content of element changed
		void element_ContentChanged(object sender, ContentEventArgs e)
		{
			RemoteContext.AddContentUpdate(sender as SvgElement);
		}

		//if child was added
		void element_ChildAdded(object sender, ChildAddedEventArgs e)
		{
			var parent = sender as SvgElement;
			if(e.NewChild is SvgVisualElement)
			{
				var newChild = e.NewChild;
				if((!newChild.ID.StartsWith(parent.ID + "_") || parent is SvgDocument) && !(parent is SvgDefinitionList))
				{
					newChild.ApplyRecursive( elem => 
					                        {
					                        	var oldID = elem.ID.Substring(elem.ID.LastIndexOf("_") + 1);
					                        	elem.SetAndForceUniqueID(elem.Parent.ID + "_" + oldID);
					                        	elem.RegisterEvents(FCaller);
					                        });
				}
				else
				{
					newChild.RegisterEvents(FCaller);
				}
			}
			
			//add to remote context
			if(e.NewChild.OwnerDocument != null)
			{
				if(e.BeforeSibling == null)
				{
					RemoteContext.AddElement(e.NewChild);
				}
				else
				{
					RemoteContext.InsertElementBefore(e.NewChild, e.BeforeSibling);
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
				RemoteContext.AddRemoveElement(element);
			
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
