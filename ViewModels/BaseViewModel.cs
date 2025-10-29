using System.ComponentModel; // chứa interface INotifyPropertyChanged, giúp WPF biết khi dữ liệu trong ViewModel thay đổi
using System.Runtime.CompilerServices; // cung cấp [CallerMemberName] - một attribute giúo tự động lấy tên thuộc tính đang gọi hàm

namespace IT008.Q13_Project___fromScratch.ViewModels
{
    public class BaseViewModel : INotifyPropertyChanged //Kế thừa Interface INotifyPropertyChanged để đảm bảo nó phải triển khai sự kiện PropertyChanged
    {
        // Sự kiện mà WPF listen. Khi thay đổi DL, ViewModel sẽ invoke sự kiện này
        public event PropertyChangedEventHandler? PropertyChanged;
        // Hàm kích họat sự kiện PropertyChanged
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        // Hàm SetProperty giúp giảm lặp code
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) // So sánh giá trị cũ và mới
                return false; // giống nhau thì không làm gì cả

            field = value; // gán giá trị mới 
            OnPropertyChanged(propertyName); // gọi để UI cập nhật lại
            return true;
        }
    }
}
