using System;
using System.Diagnostics;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
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

        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(FadeTrimming), new PropertyMetadata(false, HandleIsEnabledChanged));

        public static readonly DependencyProperty ForegroundColorProperty =
            DependencyProperty.RegisterAttached("ForegroundColor", typeof(Color), typeof(FadeTrimming), new PropertyMetadata(Colors.Transparent));

        public static readonly DependencyProperty ShowTextInToolTipWhenTrimmedProperty =
            DependencyProperty.RegisterAttached("ShowTextInToolTipWhenTrimmed", typeof(bool), typeof(FadeTrimming), new PropertyMetadata(false));

        private static readonly DependencyProperty FaderProperty =
            DependencyProperty.RegisterAttached("Fader", typeof(Fader), typeof(FadeTrimming), new PropertyMetadata(null));

        public static bool GetIsEnabled(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsEnabledProperty);
        }

        public static void SetIsEnabled(DependencyObject obj, bool value)
        {
            obj.SetValue(IsEnabledProperty, value);
        }

        public static bool GetShowTextInToolTipWhenTrimmed(DependencyObject obj)
        {
            return (bool)obj.GetValue(ShowTextInToolTipWhenTrimmedProperty);
        }

        public static void SetShowTextInToolTipWhenTrimmed(DependencyObject obj, bool value)
        {
            obj.SetValue(ShowTextInToolTipWhenTrimmedProperty, value);
        }

        public static Color GetForegroundColor(DependencyObject obj)
        {
            return (Color)obj.GetValue(ForegroundColorProperty);
        }

        public static void SetForegroundColor(DependencyObject obj, Color value)
        {
            obj.SetValue(ForegroundColorProperty, value);
        }

        private static Fader GetFader(DependencyObject obj)
        {
            return (Fader)obj.GetValue(FaderProperty);
        }

        private static void SetFader(DependencyObject obj, Fader value)
        {
            obj.SetValue(FaderProperty, value);
        }

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
            private LinearGradientBrush _brush;
            private Color _foregroundColor;
            private bool _isClipped;

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

                parent.SizeChanged += UpdateForegroundBrush;
                _textBlock.SizeChanged += UpdateForegroundBrush;

                _foregroundColor = DetermineForegroundColor(_textBlock);
                UpdateForegroundBrush(_textBlock, EventArgs.Empty);

                _isAttached = true;
            }

            public void Detach()
            {
                _textBlock.SizeChanged -= UpdateForegroundBrush;

                var parent = VisualTreeHelper.GetParent(_textBlock) as FrameworkElement;
                if (parent != null)
                {
                    parent.SizeChanged -= UpdateForegroundBrush;
                }

                _textBlock.ClearValue(TextBlock.ForegroundProperty);
                _isAttached = false;
            }

            private Color DetermineForegroundColor(TextBlock textBlock)
            {
                // if an explicit foreground color has been set, use that
                if (GetForegroundColor(textBlock) != Colors.Transparent)
                {
                    return GetForegroundColor(textBlock);
                }
                else if (textBlock.Foreground is SolidColorBrush)
                {
                    return (textBlock.Foreground as SolidColorBrush).Color;
                }
                else
                {
                    return Colors.Black;
                }
            }

            private void UpdateForegroundBrush(object sender, EventArgs e)
            {
                var layoutClip = LayoutInformation.GetLayoutClip(_textBlock);
                bool needsClipping = layoutClip != null
                    && layoutClip.Bounds.Width > 0
                    && layoutClip.Bounds.Width < _textBlock.ActualWidth;

                if (_isClipped && !needsClipping)
                {
                    if (GetShowTextInToolTipWhenTrimmed(_textBlock))
                    {
                        _textBlock.ClearValue(ToolTipService.ToolTipProperty);
                    }

                    _textBlock.Foreground = new SolidColorBrush() { Color = _foregroundColor };
                    _brush = null;
                    _isClipped = false;
                }

                if (!_isClipped && needsClipping)
                {
                    if (GetShowTextInToolTipWhenTrimmed(_textBlock))
                    {
                        BindingOperations.SetBinding(_textBlock, ToolTipService.ToolTipProperty,
                                                     new Binding("Text") { Source = _textBlock });
                    }
                }

                if (needsClipping)
                {
                    var visibleWidth = layoutClip.Bounds.Width;

                    if (_brush == null)
                    {
                        _brush = new LinearGradientBrush
                        {
                            MappingMode = BrushMappingMode.Absolute,
                            StartPoint = new Point(0, 0),
                            EndPoint = new Point(visibleWidth, 0),
                            GradientStops =
                                             {
                                                 new GradientStop()
                                                     {Color = _foregroundColor, Offset = 0},
                                                 new GradientStop()
                                                     {
                                                         Color = _foregroundColor,
                                                         Offset = (visibleWidth - FadeWidth)/visibleWidth
                                                     },
                                                 new GradientStop()
                                                     {
                                                         Color = Color.FromArgb(0, _foregroundColor.R, _foregroundColor.G, _foregroundColor.B),
                                                         Offset = 1
                                                     }
                                             }
                        };
                        _textBlock.Foreground = _brush;
                    }
                    else if (BrushNeedsUpdating(_brush, visibleWidth))
                    {
                        _brush.EndPoint = new Point(visibleWidth, 0);
                        _brush.GradientStops[1].Offset = (visibleWidth - FadeWidth) / visibleWidth;
                    }

                    _isClipped = true;
                }
            }
        }

        private static bool BrushNeedsUpdating(LinearGradientBrush brush, double visibleWidth)
        {
            const double epsilon = 0.00001;
            return brush.EndPoint.X < visibleWidth - epsilon || brush.EndPoint.X > visibleWidth + epsilon;
        }
    }
}
