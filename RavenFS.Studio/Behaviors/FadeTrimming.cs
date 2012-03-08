using System;
using System.Diagnostics;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace RavenFS.Studio.Behaviors
{
    public static class FadeTrimming
    {
        private const double FadeWidth = 10.0;
 
        public static bool GetIsEnabled(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsEnabledProperty);
        }

        public static void SetIsEnabled(DependencyObject obj, bool value)
        {
            obj.SetValue(IsEnabledProperty, value);
        }

        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(FadeTrimming), new PropertyMetadata(false, HandleIsEnabledChanged));



        public static bool GetShowTextInToolTipWhenTrimmed(DependencyObject obj)
        {
            return (bool)obj.GetValue(ShowTextInToolTipWhenTrimmedProperty);
        }

        public static void SetShowTextInToolTipWhenTrimmed(DependencyObject obj, bool value)
        {
            obj.SetValue(ShowTextInToolTipWhenTrimmedProperty, value);
        }

        public static readonly DependencyProperty ShowTextInToolTipWhenTrimmedProperty =
            DependencyProperty.RegisterAttached("ShowTextInToolTipWhenTrimmed", typeof(bool), typeof(FadeTrimming), new PropertyMetadata(false));

        private static Fader GetFader(DependencyObject obj)
        {
            return (Fader)obj.GetValue(FaderProperty);
        }

        private static void SetFader(DependencyObject obj, Fader value)
        {
            obj.SetValue(FaderProperty, value);
        }

        private static readonly DependencyProperty FaderProperty =
            DependencyProperty.RegisterAttached("Fader", typeof(Fader), typeof(FadeTrimming), new PropertyMetadata(null));

        
        private static void HandleIsEnabledChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
        {
            var textBlock = source as TextBlock;
            if (textBlock == null)
            {
                return;
            }

            var fader = GetFader(textBlock);
            if (fader != null)
            {
                fader.Detach();
                SetFader(textBlock, null);
            }

            textBlock.Loaded -= HandleTextBlockLoaded;
            textBlock.Unloaded -= HandleTextBlockUnloaded;

            if ((bool)e.NewValue)
            {
                textBlock.Loaded += HandleTextBlockLoaded;
                textBlock.Unloaded += HandleTextBlockUnloaded;

                fader = new Fader(textBlock);
                SetFader(textBlock, fader);
                fader.Attach();
            }
        }

        private static void HandleTextBlockUnloaded(object sender, RoutedEventArgs e)
        {
            var fader = GetFader(sender as DependencyObject);
            fader.Detach();
        }

        private static void HandleTextBlockLoaded(object sender, RoutedEventArgs e)
        {
            var fader = GetFader(sender as DependencyObject);
            fader.Attach();
        }

        private class Fader
        {
            private readonly TextBlock _textBlock;
            private bool _isAttached;

            public Fader(TextBlock textBlock)
            {
                _textBlock = textBlock;
            }

            public void Attach()
            {
                var parent = VisualTreeHelper.GetParent(_textBlock) as FrameworkElement;  
                if (parent == null || _isAttached)
                {
                    return;
                }

                parent.SizeChanged += UpdateMask;
                _textBlock.SizeChanged += UpdateMask;
                _isAttached = true;
            }

            public void Detach()
            {
                _textBlock.SizeChanged -= UpdateMask;
                var parent = VisualTreeHelper.GetParent(_textBlock) as FrameworkElement;
                if (parent != null)
                {
                    parent.SizeChanged -= UpdateMask;
                }

                _isAttached = false;
            }

            private void UpdateMask(object sender, EventArgs e)
            {
                var layoutClip = LayoutInformation.GetLayoutClip(_textBlock);

                if (layoutClip != null && layoutClip.Bounds.Width > 0)
                {
                    var visibleWidth = layoutClip.Bounds.Width;

                    var opacityBrush = new LinearGradientBrush
                                           {
                                               MappingMode = BrushMappingMode.Absolute,
                                               StartPoint = new Point(0, 0),
                                               EndPoint = new Point(visibleWidth, 0),
                                               GradientStops =
                                                   {
                                                       new GradientStop()
                                                           {Color = Color.FromArgb(255, 0, 0, 0), Offset = 0},
                                                       new GradientStop()
                                                           {
                                                               Color = Color.FromArgb(255, 0, 0, 0),
                                                               Offset = (visibleWidth - FadeWidth)/visibleWidth
                                                           },
                                                       new GradientStop()
                                                           {
                                                               Color = Color.FromArgb(0, 0, 0, 0),
                                                               Offset = 1
                                                           }
                                                   }
                                           };

                    if (GetShowTextInToolTipWhenTrimmed(_textBlock))
                    {
                        ToolTipService.SetToolTip(_textBlock, _textBlock.Text);
                    }
                    _textBlock.OpacityMask = opacityBrush;
                }
                else
                {
                    if (GetShowTextInToolTipWhenTrimmed(_textBlock))
                    {
                        ToolTipService.SetToolTip(_textBlock, null);
                    }
                    _textBlock.OpacityMask = null;
                }
            }
        }
    }
}
