using System.Windows;
using System.Windows.Controls;

namespace EasyFlips.Helpers
{
    public static class PasswordBoxHelper
    {
        // 1. Khai báo Attached Property: BoundPassword
        public static readonly DependencyProperty BoundPasswordProperty =
            DependencyProperty.RegisterAttached("BoundPassword", typeof(string), typeof(PasswordBoxHelper),
                new FrameworkPropertyMetadata(string.Empty, OnBoundPasswordChanged));

        // 2. Khai báo Attached Property: BindPassword (Cờ để kích hoạt tính năng)
        public static readonly DependencyProperty BindPasswordProperty =
            DependencyProperty.RegisterAttached("BindPassword", typeof(bool), typeof(PasswordBoxHelper),
                new PropertyMetadata(false, OnBindPasswordChanged));

        private static readonly DependencyProperty UpdatingPasswordProperty =
            DependencyProperty.RegisterAttached("UpdatingPassword", typeof(bool), typeof(PasswordBoxHelper),
                new PropertyMetadata(false));

        public static void SetBoundPassword(DependencyObject dp, string value) => dp.SetValue(BoundPasswordProperty, value);
        public static string GetBoundPassword(DependencyObject dp) => (string)dp.GetValue(BoundPasswordProperty);

        public static void SetBindPassword(DependencyObject dp, bool value) => dp.SetValue(BindPasswordProperty, value);
        public static bool GetBindPassword(DependencyObject dp) => (bool)dp.GetValue(BindPasswordProperty);

        // Xử lý khi Property thay đổi từ ViewModel -> Cập nhật lên PasswordBox
        private static void OnBoundPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PasswordBox box)
            {
                if ((bool)box.GetValue(UpdatingPasswordProperty)) return; // Tránh vòng lặp vô tận

                box.Password = (string)e.NewValue;
            }
        }

        // Kích hoạt lắng nghe sự kiện PasswordChanged
        private static void OnBindPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PasswordBox box)
            {
                bool wasBound = (bool)e.OldValue;
                bool needToBind = (bool)e.NewValue;

                if (wasBound) box.PasswordChanged -= HandlePasswordChanged;
                if (needToBind) box.PasswordChanged += HandlePasswordChanged;
            }
        }

        // Xử lý khi người dùng gõ phím -> Cập nhật xuống ViewModel
        private static void HandlePasswordChanged(object sender, RoutedEventArgs e)
        {
            PasswordBox box = sender as PasswordBox;
            box.SetValue(UpdatingPasswordProperty, true); // Đặt cờ đang cập nhật
            SetBoundPassword(box, box.Password);
            box.SetValue(UpdatingPasswordProperty, false); // Tắt cờ
        }
    }
}