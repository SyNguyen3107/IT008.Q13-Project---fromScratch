using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace EasyFlips.Services
{
    public static class WatermarkService
    {
        public static readonly DependencyProperty WatermarkProperty =
            DependencyProperty.RegisterAttached(
                "Watermark",
                typeof(string),
                typeof(WatermarkService),
                new FrameworkPropertyMetadata(string.Empty, OnWatermarkChanged));

        public static void SetWatermark(UIElement element, string value)
        {
            element.SetValue(WatermarkProperty, value);
        }

        public static string GetWatermark(UIElement element)
        {
            return (string)element.GetValue(WatermarkProperty);
        }

        private static void OnWatermarkChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Control control)
            {
                control.Loaded += (s, ev) => ApplyWatermark(control);
            }
        }

        private static void ApplyWatermark(Control control)
        {
            var layer = AdornerLayer.GetAdornerLayer(control);
            if (layer == null) return;

            // Xóa adorner cũ nếu có
            var adorners = layer.GetAdorners(control);
            if (adorners != null)
            {
                foreach (var ad in adorners.OfType<WatermarkAdorner>())
                    layer.Remove(ad);
            }

            double size = GetWatermarkSize(control);
            string text = GetWatermark(control);
            layer.Add(new WatermarkAdorner(control, text, size));
        }


        // 
        public static readonly DependencyProperty WatermarkSizeProperty =
            DependencyProperty.RegisterAttached(
                "WatermarkSize",
                typeof(double),
                typeof(WatermarkService),
                new FrameworkPropertyMetadata(12.0));

        public static void SetWatermarkSize(UIElement element, double value)
        {
            element.SetValue(WatermarkSizeProperty, value);
        }

        public static double GetWatermarkSize(UIElement element)
        {
            return (double)element.GetValue(WatermarkSizeProperty);
        }
    }

    public class WatermarkAdorner : Adorner
    {
        private readonly string _watermark;
        private readonly double _size;

        public WatermarkAdorner(UIElement adornedElement, string watermark, double size = 12)
     : base(adornedElement)
        {
            _watermark = watermark;
            _size = size;
            IsHitTestVisible = false;

            if (adornedElement is TextBox tb)
                tb.TextChanged += (s, e) => InvalidateVisual();

            if (adornedElement is PasswordBox pb)
                pb.PasswordChanged += (s, e) => InvalidateVisual();

            if (adornedElement is FrameworkElement fe)
                fe.SizeChanged += (s, e) => InvalidateVisual();

        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            string text = null;

            if (AdornedElement is TextBox tb)
                text = tb.Text;

            if (AdornedElement is PasswordBox pb)
                text = pb.Password;

            if (string.IsNullOrEmpty(text))
            {
                var control = AdornedElement as Control;
                var typeface = new Typeface(control.FontFamily, control.FontStyle, control.FontWeight, control.FontStretch);

                var formattedText = new FormattedText(
                    _watermark,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    _size,
                    Brushes.Gray,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                double y = (AdornedElement.RenderSize.Height - formattedText.Height) / 2;
                drawingContext.DrawText(formattedText, new Point(5, y));
            }
        }

    }
}


