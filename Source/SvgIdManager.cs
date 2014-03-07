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
			
			if(element is SvgVisualElement)
			{
				//register events
				if(string.IsNullOrWhiteSpace(element.ID))
				{
					if(element.Parent != null)
						element.ID = element.Parent.ID + "/" + IDGenerator.NewID;
					else
						element.ID = RandomString(16);
				}
				
				element.RegisterEvents(FCaller);
			}
			
			if(sibling == null)
			{
				RemoteContext.Add(element);
			}
			else
			{
				RemoteContext.InsertBefore(element, sibling);
			}
			
			return base.AddAndFixID(element, sibling, autoFixID, logElementOldIDNewID);
			
		}
		
		public override void Remove(SvgElement element)
		{
			element.AttributeChanged -= element_AttributeChanged;
			
			if(element is SvgVisualElement)
				element.UnregisterEvents(FCaller);
			
			if(!string.IsNullOrWhiteSpace(element.ID))
			{
				RemoteContext.AddRemoveID(element.ID);
			}
			
			base.Remove(element);
		}
		
		void element_AttributeChanged(object sender, AttributeEventArgs e)
		{
			var elem = sender as SvgElement;
			if(elem.ID != "TimeBar")
			{
				var val = TypeDescriptor.GetConverter(e.Value).ConvertToString(null, CultureInfo.InvariantCulture, e.Value);
				System.Diagnostics.Debug.WriteLine(elem.ID + " " + e.Attribute + " " + val);
				RemoteContext.AddAttribute((sender as SvgElement).ID, e.Attribute, e.Value);
			}
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
