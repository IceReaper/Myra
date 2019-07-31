﻿using System.ComponentModel;
using System.Linq;
using System.Xml.Serialization;
using Myra.Attributes;
using Myra.Graphics2D.UI.Styles;

namespace Myra.Graphics2D.UI
{
	public class RadioButton : ImageTextButton
	{
		[HiddenInEditor]
		[XmlIgnore]
		[EditCategory("Behavior")]
		[DefaultValue(true)]
		public override bool Toggleable
		{
			get
			{
				return base.Toggleable;
			}
			set
			{
				base.Toggleable = value;
			}
		}

		protected override bool CanChangePressedOnTouchUp
		{
			get
			{
				// At least one radio button should be pressed
				return !IsPressed;
			}
		}

		public RadioButton(ImageTextButtonStyle bs) : base(bs)
		{
			Toggleable = true;
		}

		public RadioButton(Stylesheet stylesheet, string style) : this(stylesheet.RadioButtonStyles[style])
		{
		}

		public RadioButton(Stylesheet stylesheet) : this(stylesheet.RadioButtonStyle)
		{
		}

		public RadioButton(string style) : this(Stylesheet.Current, style)
		{
		}

		public RadioButton() : this(Stylesheet.Current)
		{
		}

		public override void OnPressedChanged()
		{
			base.OnPressedChanged();

			if (IsPressed)
			{
				foreach (var child in Parent.ChildrenCopy)
				{
					var asRadio = child as RadioButton;

					if (asRadio == null || asRadio == this)
					{
						continue;
					}

					asRadio.IsPressed = false;
				}
			}
		}

		protected override void SetStyleByName(Stylesheet stylesheet, string name)
		{
			ApplyImageTextButtonStyle(stylesheet.RadioButtonStyles[name]);
		}

		internal override string[] GetStyleNames(Stylesheet stylesheet)
		{
			return stylesheet.RadioButtonStyles.Keys.ToArray();
		}
	}
}