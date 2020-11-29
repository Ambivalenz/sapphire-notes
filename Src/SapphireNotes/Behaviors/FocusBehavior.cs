﻿using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace SapphireNotes.Behaviors
{
    public class FocusBehavior : AvaloniaObject
    {
        public static readonly AttachedProperty<bool> InitialFocusProperty = AvaloniaProperty.RegisterAttached<FocusBehavior, Interactive, bool>(
            "InitialFocus", default, false, BindingMode.OneTime, ValidateInitialFocus);

        private static bool ValidateInitialFocus(Interactive element, bool initialFocus)
        {
            if (initialFocus)
            {
                var textBox = element as TextBox;
                textBox.AttachedToVisualTree += TextBox_AttachedToVisualTree;
            }
            
            return initialFocus;
        }

        private static void TextBox_AttachedToVisualTree(object sender, VisualTreeAttachmentEventArgs e)
        {
            var textBox = sender as TextBox;
            textBox.Focus();
        }

        public static void SetInitialFocus(AvaloniaObject element, bool value)
        {
            element.SetValue(InitialFocusProperty, value);
        }

        public static bool GetInitialFocus(AvaloniaObject element)
        {
            return element.GetValue(InitialFocusProperty);
        }
    }
}
