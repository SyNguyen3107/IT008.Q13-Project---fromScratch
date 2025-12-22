using EasyFlips.Interfaces;
using EasyFlips.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;

namespace EasyFlips.ViewModels
{
    public partial class MemberGameViewModel : ObservableObject
    {
        // Binding IsInputEnable
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SubmitAnswerCommand))] // Tự cập nhật trạng thái nút bấm
        private bool isInputEnabled;

        [ObservableProperty]
        private string userAnswer;

        public MemberGameViewModel()
        {
            // Mặc định là KHÓA khi mới vào (chưa có câu hỏi)
            IsInputEnabled = false;
        }
        // Gọi hàm này khi nhận tín hiệu "Question" từ Server/Host
        public void OnQuestionReceived()
        {
            UserAnswer = "";        // Xóa đáp án cũ
            IsInputEnabled = true;  // MỞ KHÓA: Cho phép nhập và nhấn Enter
        }
        // Hàm xử lý nộp bài(Dùng cho cả Nút Submit và Phím Enter)
        [RelayCommand(CanExecute = nameof(CanSubmit))]
        private void SubmitAnswer()
        {
            // Xử lý logic nộp bài ở đây
            MessageBox.Show($"Đã nộp đáp án: {UserAnswer}");

            // Sau khi nộp xong thì KHÓA lại ngay
            IsInputEnabled = false;
        }
        // Điều kiện để được phép nộp (Chỉ nộp được khi IsInputEnabled = true)
        private bool CanSubmit()
        {
            return IsInputEnabled;
        }
    }
}
