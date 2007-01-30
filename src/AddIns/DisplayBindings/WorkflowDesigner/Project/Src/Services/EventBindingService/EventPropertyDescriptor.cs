/*
 * Created by SharpDevelop.
 * User: Russell Wilkins
 * Date: 30/01/2007
 * Time: 12:17
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */

#region Using
using System;
using System.ComponentModel.Design;
using System.ComponentModel;
using System.Workflow.ComponentModel.Serialization;
using System.Workflow.ComponentModel;
using System.Collections;
#endregion

namespace WorkflowDesigner
{
	/// <summary>
	/// Description of EventPropertyDescriptor.
	/// </summary>
	public class EventPropertyDescriptor : PropertyDescriptor
	{
		internal EventDescriptor eventDescriptor;
		private IServiceProvider provider;
		
		public EventPropertyDescriptor(IServiceProvider provider,  EventDescriptor eventDescriptor) : base(eventDescriptor)
		{
			this.eventDescriptor = eventDescriptor;
			this.provider = provider;
		}
		
		public override Type ComponentType {
			get {
				return eventDescriptor.ComponentType;
			}
		}
		
		public override bool IsReadOnly {
			get {
				return false;
			}
		}
		
		public override Type PropertyType {
			get {
				return eventDescriptor.EventType;
			}
		}
		
		public override bool CanResetValue(object component)
		{
			return false;
		}
		
		public override object GetValue(object component)
		{
			Activity activity = component as Activity;
			if (component == null)
				throw new ArgumentException("component must be derived from Activity");
			
			string value = string.Empty;
			
			// Find method name associated with the EventDescriptor.
			Hashtable events = activity.GetValue(WorkflowMarkupSerializer.EventsProperty) as Hashtable;
			
			if (events != null) {
				if (events.ContainsKey(this.eventDescriptor.Name))
					value = events[this.eventDescriptor.Name] as string;
			}
			
			return value;
		}
		
		public override void ResetValue(object component)
		{
			SetValue(component, null);
		}
		
		public override void SetValue(object component, object value)
		{
			// Validate the parameters.
			Activity activity = component as Activity;
			if (component == null)
				throw new ArgumentException("component must be derived from Activity");
			
			// Get the event list form the dependency object.
			Hashtable events = activity.GetValue(WorkflowMarkupSerializer.EventsProperty) as Hashtable;

			if (events == null) {
				events = new Hashtable();
				activity.SetValue(WorkflowMarkupSerializer.EventsProperty, events);
			}

			string oldValue = events[this.eventDescriptor.Name] as string;
			
			// Value not changed need go no further.
			if (oldValue != null) {
				if (oldValue.CompareTo(value) == 0)
					return;				
			}
			
			IComponentChangeService componentChangedService = provider.GetService(typeof(IComponentChangeService)) as  IComponentChangeService;
			componentChangedService.OnComponentChanging(component, this.eventDescriptor);

			// Update to new value.
			events[this.eventDescriptor.Name] = value;
			
			componentChangedService.OnComponentChanged(component, this.eventDescriptor, oldValue, value);
		}

		
		public override bool ShouldSerializeValue(object component)
		{
			if (GetValue (component) == null) return false;
			return true;
		}
	}

}
