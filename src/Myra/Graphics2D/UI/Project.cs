﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Myra.Attributes;
using Myra.Graphics2D.UI.Styles;
using Myra.Utility;
using System.Xml.Linq;
using System.Globalization;
using System.Xml.Serialization;

#if !XENKO
using Microsoft.Xna.Framework;
#else
using Xenko.Core.Mathematics;
#endif

namespace Myra.Graphics2D.UI
{
	public class ExportOptions
	{
		public string Namespace { get; set; }
		public string Class { get; set; }
		public string OutputPath { get; set; }
	}

	public class Project
	{
		private static readonly Dictionary<string, string> LegacyNames = new Dictionary<string, string>
		{
			{ "Button", "ImageTextButton" }
		};

		private readonly ExportOptions _exportOptions = new ExportOptions();

		[HiddenInEditor]
		public ExportOptions ExportOptions
		{
			get { return _exportOptions; }
		}

		[HiddenInEditor]
		public Widget Root { get; set; }

		[HiddenInEditor]
		public string StylesheetPath
		{
			get; set;
		}

		[HiddenInEditor]
		[XmlIgnore]
		public Stylesheet Stylesheet { get; set; }

		public Project()
		{
			Stylesheet = Stylesheet.Current;
		}

		public string Save()
		{
			var root = InternalSave(this);

			var xDoc = new XDocument(root);

			return xDoc.ToString();
		}

		private static readonly Type[] SerializableTypes = new Type[]
		{
			typeof(IItemWithId),
			typeof(ExportOptions),
			typeof(Grid.Proportion)
		};

		private static void ParseProperties(Type type, out List<PropertyInfo> complexProperties, out List<PropertyInfo> simpleProperties)
		{
			complexProperties = new List<PropertyInfo>();
			simpleProperties = new List<PropertyInfo>();

			var allProperties = type.GetRuntimeProperties();
			foreach (var property in allProperties)
			{
				if (property.GetMethod == null ||
					!property.GetMethod.IsPublic ||
					property.GetMethod.IsStatic)
				{
					continue;
				}

				var attr = property.FindAttribute<XmlIgnoreAttribute>();
				if (attr != null)
				{
					continue;
				}

				if ((from t in SerializableTypes where t.IsAssignableFrom(property.PropertyType) select t).FirstOrDefault() != null)
				{
					complexProperties.Add(property);
				}
				else
				{
					var propertyType = property.PropertyType;
					if (typeof(IList).IsAssignableFrom(propertyType) && propertyType.IsGenericType &&
						(from t in SerializableTypes where t.IsAssignableFrom(propertyType.GenericTypeArguments[0]) select t).FirstOrDefault() != null)
					{
						complexProperties.Add(property);
					}
					else
					{
						simpleProperties.Add(property);
					}
				}
			}
		}

		private XElement InternalSave(object obj, bool skipComplex = false)
		{
			var type = obj.GetType();

			List<PropertyInfo> complexProperties, simpleProperties;
			ParseProperties(type, out complexProperties, out simpleProperties);

			var el = new XElement(type.Name);

			foreach (var property in simpleProperties)
			{
				if (!ShouldSerializeProperty(obj, property))
				{
					continue;
				}

				// Obsolete properties ignored only on save(and not ignored on load)
				var attr = property.FindAttribute<ObsoleteAttribute>();
				if (attr != null)
				{
					continue;
				}

				var value = property.GetValue(obj);
				if (value != null)
				{
					string str;

					if (property.PropertyType == typeof(Color?))
					{
						str = ((Color?)value).Value.ToHexString();
					}
					else
					if (property.PropertyType == typeof(Color))
					{
						str = ((Color)value).ToHexString();
					}
					else
					{
						str = Convert.ToString(value, CultureInfo.InvariantCulture);
					}

					el.Add(new XAttribute(property.Name, str));
				}
			}

			if (!skipComplex)
			{
				foreach (var property in complexProperties)
				{
					var value = property.GetValue(obj);

					if (value == null)
					{
						continue;
					}

					var asList = value as IList;
					if (asList == null)
					{
						el.Add(InternalSave(value));
					}
					else
					{
						var collectionRoot = el;
						if (!typeof(IItemWithId).IsAssignableFrom(property.PropertyType.GenericTypeArguments[0]))
						{
							collectionRoot = new XElement(property.Name);
							el.Add(collectionRoot);
						}

						foreach (var comp in asList)
						{
							collectionRoot.Add(InternalSave(comp));
						}
					}
				}
			}

			return el;
		}

		public static Project LoadFromXml(XDocument xDoc, Stylesheet stylesheet)
		{
			var result = new Project
			{
				Stylesheet = stylesheet
			};
			InternalLoad(result, xDoc.Root, stylesheet);

			return result;
		}

		public static Project LoadFromXml(string data, Stylesheet stylesheet)
		{
			return LoadFromXml(XDocument.Parse(data), stylesheet);
		}

		public static Project LoadFromXml(string data)
		{
			return LoadFromXml(data, Stylesheet.Current);
		}

		public static object LoadObjectFromXml(string data, Stylesheet stylesheet)
		{
			XDocument xDoc = XDocument.Parse(data);

			Type itemType;
			if (xDoc.Root.Name != "Proportion")
			{

				var itemNamespace = typeof(Widget).Namespace;

				var widgetName = xDoc.Root.Name.ToString();
				string newName;
				if (LegacyNames.TryGetValue(widgetName, out newName))
				{
					widgetName = newName;
				}

				itemType = typeof(Widget).Assembly.GetType(itemNamespace + "." + widgetName);
			}
			else
			{
				itemType = typeof(Grid.Proportion);
			}

			var item = CreateItem(itemType, stylesheet);
			InternalLoad(item, xDoc.Root, stylesheet);

			return item;
		}

		public static object LoadObjectFromXml(string data)
		{
			return LoadObjectFromXml(data, Stylesheet.Current);
		}

		public string SaveObjectToXml(object obj)
		{
			return InternalSave(obj, true).ToString();
		}

		private static object CreateItem(Type type, Stylesheet stylesheet)
		{
			// Check whether item has constructor with stylesheet param
			var acceptsStylesheet = false;
			foreach (var c in type.GetConstructors())
			{
				var p = c.GetParameters();
				if (p != null && p.Length == 1)
				{
					if (p[0].ParameterType == typeof(Stylesheet))
					{
						acceptsStylesheet = true;
						break;
					}
				}
			}

			if (acceptsStylesheet)
			{
				return Activator.CreateInstance(type, stylesheet);
			}

			return Activator.CreateInstance(type);
		}

		private static void InternalLoad(object obj, XElement el, Stylesheet stylesheet)
		{
			var type = obj.GetType();

			List<PropertyInfo> complexProperties, simpleProperties;
			ParseProperties(type, out complexProperties, out simpleProperties);

			foreach (var attr in el.Attributes())
			{
				var property = (from p in simpleProperties where p.Name == attr.Name select p).FirstOrDefault();

				if (property != null)
				{
					object value = null;

					if (property.PropertyType.IsEnum)
					{
						value = Enum.Parse(property.PropertyType, attr.Value);
					}
					else if (property.PropertyType == typeof(Color) ||
						property.PropertyType == typeof(Color?))
					{
						value = attr.Value.FromName();
					}
					else
					{
						var type2 = property.PropertyType;
						if (property.PropertyType.IsNullablePrimitive())
						{
							type2 = property.PropertyType.GetNullableType();
						}

						value = Convert.ChangeType(attr.Value, type2, CultureInfo.InvariantCulture);
					}
					property.SetValue(obj, value);
				}
			}

			var widgetNamespace = typeof(Widget).Namespace;
			foreach (var child in el.Elements())
			{
				// Find property
				var property = (from p in complexProperties where p.Name == child.Name select p).FirstOrDefault();
				if (property != null)
				{
					if (property.SetMethod == null)
					{
						// Readonly property
						var value = property.GetValue(obj);
						var asCollection = value as IList;
						if (asCollection != null)
						{
							foreach (var child2 in child.Elements())
							{
								var item = CreateItem(property.PropertyType.GenericTypeArguments[0], stylesheet);
								InternalLoad(item, child2, stylesheet);
								asCollection.Add(item);
							}
						}
						else
						{
							InternalLoad(value, child, stylesheet);
						}
					}
					else
					{
						var value = CreateItem(property.PropertyType, stylesheet);
						InternalLoad(value, child, stylesheet);
						property.SetValue(obj, value);
					}
				}
				else
				{
					// Property not found
					// Should be widget class name then
					var widgetName = child.Name.ToString();
					string newName;
					if (LegacyNames.TryGetValue(widgetName, out newName))
					{
						widgetName = newName;
					}

					var itemType = typeof(Widget).Assembly.GetType(widgetNamespace + "." + widgetName);
					if (itemType != null)
					{
						var item = (IItemWithId)CreateItem(itemType, stylesheet);
						InternalLoad(item, child, stylesheet);

						if (obj is ComboBox)
						{
							((ComboBox)obj).Items.Add((ListItem)item);
						}
						else
						if (obj is ListBox)
						{
							((ListBox)obj).Items.Add((ListItem)item);
						}
						else
						if (obj is TabControl)
						{
							((TabControl)obj).Items.Add((TabItem)item);
						}
						else
						if (obj is MenuItem)
						{
							((MenuItem)obj).Items.Add((IMenuItem)item);
						}
						else if (obj is Menu)
						{
							((Menu)obj).Items.Add((IMenuItem)item);
						}
						else if (obj is IContent)
						{
							((IContent)obj).Content = (Widget)item;
						}
						else if (obj is MultipleItemsContainer)
						{
							((MultipleItemsContainer)obj).Widgets.Add((Widget)item);
						}
						else if (obj is SplitPane)
						{
							((SplitPane)obj).Widgets.Add((Widget)item);
						}
						else if (obj is Project)
						{
							((Project)obj).Root = (Widget)item;
						}
					}
					else
					{
						throw new Exception(string.Format("Could not resolve tag '{0}'", widgetName));
					}
				}
			}
		}

		public bool ShouldSerializeProperty(Object w, PropertyInfo property)
		{
			var value = property.GetValue(w);
			if (property.HasDefaultValue(value))
			{
				return false;
			}

			var asWidget = w as Widget;
			if (asWidget != null && HasStylesheetValue(asWidget, property))
			{
				return false;
			}

			return true;
		}

		private bool HasStylesheetValue(Widget w, PropertyInfo property)
		{
			if (Stylesheet == null)
			{
				return false;
			}

			var styleName = w.StyleName;
			if (string.IsNullOrEmpty(styleName))
			{
				styleName = Stylesheet.DefaultStyleName;
			}

			var stylesheet = Stylesheet;

			// Find styles dict of that widget
			var typeName = w.GetType().Name;
			if (typeName == "ImageTextButton" || typeName == "ImageButton" || typeName == "TextButton")
			{
				// Small hack
				// ImageTextButton styles are stored in Stylesheet.ButtonStyles
				typeName = "Button";
			}

			var stylesDictPropertyName = typeName + "Styles";
			var stylesDictProperty = stylesheet.GetType().GetRuntimeProperty(stylesDictPropertyName);
			if (stylesDictProperty == null)
			{
				return false;
			}

			var stylesDict = (IDictionary)stylesDictProperty.GetValue(stylesheet);
			if (stylesDict == null)
			{
				return false;
			}

			// Fetch style from the dict
			object obj = stylesDict[styleName];

			// Now find corresponding property
			PropertyInfo styleProperty = null;

			var stylePropertyPathAttribute = property.FindAttribute<StylePropertyPathAttribute>();
			if (stylePropertyPathAttribute != null)
			{
				var path = stylePropertyPathAttribute.Name;
				if (path.StartsWith("/"))
				{
					obj = stylesheet;
					path = path.Substring(1);
				}

				var parts = path.Split('/');
				for (var i = 0; i < parts.Length; ++i)
				{
					styleProperty = obj.GetType().GetRuntimeProperty(parts[i]);

					if (i < parts.Length - 1)
					{
						obj = styleProperty.GetValue(obj);
					}
				}
			}
			else
			{
				styleProperty = obj.GetType().GetRuntimeProperty(property.Name);
			}

			if (styleProperty == null)
			{
				return false;
			}

			// Compare values
			var styleValue = styleProperty.GetValue(obj);
			var value = property.GetValue(w);
			if (!Equals(styleValue, value))
			{
				return false;
			}

			return true;
		}
	}
}