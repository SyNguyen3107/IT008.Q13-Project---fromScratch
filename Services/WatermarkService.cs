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
                control.Loaded += (s, ev) =>
                {
                    var layer = AdornerLayer.GetAdornerLayer(control);
                    if (layer != null)
                    {
                        layer.Add(new WatermarkAdorner(control, e.NewValue?.ToString()));
                    }
                };
            }
        }
    }

    public class WatermarkAdorner : Adorner
    {
        private readonly string _watermark;

        public WatermarkAdorner(UIElement adornedElement, string watermark)
            : base(adornedElement)
        {
            _watermark = watermark;
            IsHitTestVisible = false;

            if (adornedElement is TextBox tb)
                tb.TextChanged += (s, e) => InvalidateVisual();

            if (adornedElement is PasswordBox pb)
                pb.PasswordChanged += (s, e) => InvalidateVisual();
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
                var formattedText = new FormattedText(
                    _watermark,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    (AdornedElement as Control)?.FontSize ?? 12,
                    Brushes.Gray);

                double y = (AdornedElement.RenderSize.Height - formattedText.Height) / 2;
                drawingContext.DrawText(formattedText, new Point(5, y));
            }
        }
    }

}
