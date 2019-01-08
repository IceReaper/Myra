﻿using System;
using System.ComponentModel;
using Microsoft.Xna.Framework;
using Myra.Attributes;
using Myra.Graphics2D.UI.Styles;
using Myra.Utility;
using Newtonsoft.Json;

namespace Myra.Graphics2D.UI
{
	public abstract class Slider : SingleItemContainer<ImageButton>
	{
		private float _value;
		private int? _mousePos;

		[HiddenInEditor]
		[JsonIgnore]
		public abstract Orientation Orientation
		{
			get;
		}

		[EditCategory("Behavior")]
		[DefaultValue(0.0f)]
		public float Minimum
		{
			get; set;
		}

		[EditCategory("Behavior")]
		[DefaultValue(100.0f)]
		public float Maximum
		{
			get; set;
		}

		[EditCategory("Behavior")]
		[DefaultValue(0.0f)]
		public float Value
		{
			get
			{
				return _value;
			}
			set
			{
				if (value > Maximum)
				{
					//could throw error instead?
					value = Maximum;
				}

				if (value < Minimum)
				{
					//could throw error instead?
					value = Minimum;
				}

				if (_value == value)
				{
					return;
				}

				var oldValue = _value;
				_value = value;

				SyncHintWithValue();

				var ev = ValueChanged;
				if (ev != null)
				{
					ev(this, new ValueChangedEventArgs<float>(oldValue, value));
				}
			}
		}

		private int Hint
		{
			get
			{
				return Orientation == Orientation.Horizontal ? InternalChild.Left : InternalChild.Top;
			}

			set
			{
				if (Hint == value)
				{
					return;
				}

				if (Orientation == Orientation.Horizontal)
				{
					InternalChild.Left = value;
				}
				else
				{
					InternalChild.Top = value;
				}
			}
		}

		private int MaxHint
		{
			get
			{
				return Orientation == Orientation.Horizontal
					? Bounds.Width - InternalChild.Bounds.Width
					: Bounds.Height - InternalChild.Bounds.Height;
			}
		}

		public override Desktop Desktop
		{
			get
			{
				return base.Desktop;
			}
			set
			{
				if (Desktop != null)
				{
					Desktop.MouseMoved -= DesktopMouseMoved;
				}

				base.Desktop = value;

				if (Desktop != null)
				{
					Desktop.MouseMoved += DesktopMouseMoved;
				}
			}
		}

		/// <summary>
		/// Fires when the value had been changed
		/// </summary>
		public event EventHandler<ValueChangedEventArgs<float>> ValueChanged;

		/// <summary>
		/// Fires only when the value had been changed by user(doesnt fire if it had been assigned through code)
		/// </summary>
		public event EventHandler<ValueChangedEventArgs<float>> ValueChangedByUser;

		protected Slider(SliderStyle sliderStyle)
		{
			InternalChild = new ImageButton((ImageButtonStyle)null)
			{
				ReleaseOnMouseLeft = false
			};
			InternalChild.PressedChanged += WidgetOnPressedChanged;
			if (sliderStyle != null)
			{
				ApplySliderStyle(sliderStyle);
			}

			Maximum = 100;
		}

		private void WidgetOnPressedChanged(object sender, EventArgs eventArgs)
		{
			if (!InternalChild.IsPressed)
			{
				_mousePos = null;
			}
			else
			{
				_mousePos = GetMousePos();
			}
		}

		private int GetMousePos()
		{
			return Orientation == Orientation.Horizontal ? Desktop.MousePosition.X : Desktop.MousePosition.Y;
		}

		public void ApplySliderStyle(SliderStyle style)
		{
			ApplyWidgetStyle(style);

			InternalChild.ApplyImageButtonStyle(style.KnobStyle);
		}

		private void SyncHintWithValue()
		{
			Hint = (int)(MaxHint * (_value / Maximum));
		}

		public override void Arrange()
		{
			base.Arrange();

			SyncHintWithValue();
		}

		private void DesktopMouseMoved(object sender, EventArgs args)
		{
			if (_mousePos == null)
			{
				return;
			}

			var mousePos = GetMousePos();
			var delta = mousePos - _mousePos.Value;

			if (delta == 0)
			{
				return;
			}

			var hint = Hint;
			hint += delta;

			if (hint < 0)
			{
				hint = 0;
			}

			if (hint > MaxHint)
			{
				hint = MaxHint;
			}

			var oldValue = _value;
			var valueChanged = false;
			// Sync Value with Hint
			if (MaxHint != 0)
			{
				var d = Maximum - Minimum;

				var newValue = Minimum + hint * d / MaxHint;
				if (_value != newValue)
				{
					_value = newValue;
					valueChanged = true;
				}
			}

			Hint = hint;

			if (valueChanged)
			{
				var ev = ValueChanged;
				if (ev != null)
				{
					ev(this, new ValueChangedEventArgs<float>(oldValue, _value));
				}

				ev = ValueChangedByUser;
				if (ev != null)
				{
					ev(this, new ValueChangedEventArgs<float>(oldValue, _value));
				}
			}

			_mousePos = mousePos;
		}
	}
}