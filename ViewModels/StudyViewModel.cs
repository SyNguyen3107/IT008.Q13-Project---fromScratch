using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging; // Cần cho IMessenger
using DiffPlex.DiffBuilder.Model;
using EasyFlips.Messages; // Cần cho các Message
using EasyFlips.Models;
using EasyFlips.Services;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;

namespace EasyFlips.ViewModels
{
    public partial class StudyViewModel : ObservableObject
    {
        private readonly StudyService _studyService;
        private readonly AudioService _audioService;
        private readonly IMessenger _messenger; // [NEW] Dùng để gửi thông báo hoàn thành

        private string _currentDeckId;
        private Card? _currentCard;

        // Giả sử ComparisonService được new trực tiếp (hoặc inject nếu bạn đã đăng ký DI)
        private readonly ComparisonService _comparisonService = new ComparisonService();

        // --- Properties ---
        [ObservableProperty] private string _questionText = string.Empty;
        [ObservableProperty] private string _answerText = string.Empty;
        [ObservableProperty] private string _correctAnswer = string.Empty;

        [ObservableProperty] private string? _frontImagePath;
        [ObservableProperty] private string? _backImagePath;
        [ObservableProperty] private string? _frontAudioPath;
        [ObservableProperty] private string? _backAudioPath;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsInputRequired))]
        private bool _isAnswerVisible = false;

        public bool IsInputRequired => !IsAnswerVisible;
       
        [ObservableProperty] private string _userInputText = string.Empty;
        [ObservableProperty] private bool _hasCards = true;

        [ObservableProperty] private string _totalScore = string.Empty;

        public ObservableCollection<DiffPiece> ComparisonPieces { get; } = new();

        // --- Commands ---
        public IAsyncRelayCommand AgainCommand { get; }
        public IAsyncRelayCommand HardCommand { get; }
        public IAsyncRelayCommand GoodCommand { get; }
        public IAsyncRelayCommand EasyCommand { get; }

        // Inject thêm IMessenger
        public StudyViewModel(StudyService studyService, AudioService audioService, IMessenger messenger)
        {
            _studyService = studyService;
            _audioService = audioService;
            _messenger = messenger;

            AgainCommand = new AsyncRelayCommand(() => ProcessReview(ReviewOutcome.Again), () => HasCards);
            HardCommand = new AsyncRelayCommand(() => ProcessReview(ReviewOutcome.Hard), () => HasCards);
            GoodCommand = new AsyncRelayCommand(() => ProcessReview(ReviewOutcome.Good), () => HasCards);
            EasyCommand = new AsyncRelayCommand(() => ProcessReview(ReviewOutcome.Easy), () => HasCards);
        }

        public async Task InitializeAsync(string deckId)
        {
            _currentDeckId = deckId;
            await LoadNextCardAsync();
        }

        private async Task LoadNextCardAsync()
        {
            try
            {
                IsAnswerVisible = false;
                UserInputText = string.Empty;
                ComparisonPieces.Clear();

                _currentCard = await _studyService.GetNextCardToReviewAsync(_currentDeckId);
                HasCards = _currentCard != null;

                if (_currentCard != null)
                {
                    QuestionText = _currentCard.FrontText ?? "";
                    AnswerText = _currentCard.BackText ?? "";
                    CorrectAnswer = _currentCard.Answer ?? "";

                    FrontImagePath = _currentCard.FrontImagePath;
                    BackImagePath = _currentCard.BackImagePath;
                    FrontAudioPath = _currentCard.FrontAudioPath;
                    BackAudioPath = _currentCard.BackAudioPath;
                }
                else
                {
                    // Hết thẻ -> Hiển thị chúc mừng
                    QuestionText = "Congratulations!";
                    AnswerText = "You have finished studying this deck for now.";
                    CorrectAnswer = "";
                    FrontImagePath = null; BackImagePath = null;
                    FrontAudioPath = null; BackAudioPath = null;
                    
                    IsAnswerVisible = true; // Hiện text chúc mừng

                    // [FIX]: Truyền _currentDeckId vào message để khớp với Constructor
                    _messenger.Send(new StudySessionCompletedMessage(_currentDeckId));
                }

                // Cập nhật trạng thái nút bấm
                AgainCommand.NotifyCanExecuteChanged();
                HardCommand.NotifyCanExecuteChanged();
                GoodCommand.NotifyCanExecuteChanged();
                EasyCommand.NotifyCanExecuteChanged();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading card: {ex.Message}", "Error");
            }
        }

        [RelayCommand]
        private void ShowAnswer()
        {
            IsAnswerVisible = true;
            GenerateComparison();
        }

        private void GenerateComparison()
        {
            ComparisonPieces.Clear();
            if (string.IsNullOrEmpty(CorrectAnswer)) return;

            int score = _comparisonService.SmartScore(UserInputText, CorrectAnswer);
            TotalScore = score.ToString();

            List<DiffPiece> pieces;
            if (score < 50)
            {
                pieces = _comparisonService.GetWordDiff(UserInputText, CorrectAnswer);
            }
            else
            {
                pieces = _comparisonService.GetCharDiff(UserInputText, CorrectAnswer);
            }

            if (pieces.Count == 0 && !string.IsNullOrEmpty(UserInputText))
            {
                ComparisonPieces.Add(new DiffPiece(UserInputText, ChangeType.Unchanged));
            }
            else
            {
                foreach (var piece in pieces) ComparisonPieces.Add(piece);
            }
        }
        


        // --- MEDIA ---
        [RelayCommand]
        private void PlayAudio(string? path)
        {
            if (!string.IsNullOrEmpty(path)) _audioService.PlayAudio(path);
        }

        public void StopAudio() => _audioService.StopAudio();

        private async Task ProcessReview(ReviewOutcome outcome)
        {
            try
            {
                if (_currentCard == null) return;

                // Xử lý logic SM-2 và lưu DB
                await _studyService.ProcessReviewAsync(_currentCard, outcome);

                // Load thẻ tiếp theo
                await LoadNextCardAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error processing review:\n{ex.Message}\n\nTrace:\n{ex.StackTrace}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        
    }
}