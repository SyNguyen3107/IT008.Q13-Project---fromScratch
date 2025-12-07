using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using FuzzySharp;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace EasyFlips.Services
{
    public class ComparisonService
    {
        /// <summary>
        /// Hàm 3: Chấm điểm bằng FuzzySharp
        /// </summary>
        public bool IsAnswerAcceptable(string input, string target, int threshold = 80)
        {
           if (string.IsNullOrEmpty(input)) return false;
           int score = Fuzz.WeightedRatio(input.ToLower(), target.ToLower());
           return score >= threshold;
        }
        public int SmartScore(string input, string target)
        {
            if (string.IsNullOrWhiteSpace(input)) return 0;

            input = NormalizeText(input);
            target = NormalizeText(target);

            var inputWords = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var targetWords = target.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            int totalWords = targetWords.Length;
            double totalScore = 0;
            int mismatchCount = 0;

            int len = Math.Min(inputWords.Length, targetWords.Length);

            for (int i = 0; i < len; i++)
            {
                string inWord = inputWords[i];
                string tarWord = targetWords[i];

                // Tỉ lệ ký tự đúng
                int maxLen = Math.Max(inWord.Length, tarWord.Length);
                int correctChars = 0;
                for (int j = 0; j < Math.Min(inWord.Length, tarWord.Length); j++)
                {
                    if (inWord[j] == tarWord[j]) correctChars++;
                }
                double charRatio = (double)correctChars / maxLen;

                int wordScore;
                if (charRatio < 0.4) // quá loạn xạ → 0 điểm
                {
                    wordScore = 0;
                    mismatchCount++;
                }
                else
                {
                    wordScore = (int)(charRatio * 100); // scale 0–100
                    if (charRatio < 0.85) mismatchCount++; // typo nhẹ cũng tính mismatch
                }

                totalScore += wordScore;
            }

            // Từ dư/thiếu → trừ nặng và đều nhau
            int extraWords = inputWords.Length - targetWords.Length;
            if (extraWords > 0)
            {
                totalScore -= extraWords * 50; // trừ nặng từ dư
            }
            else if (extraWords < 0)
            {
                totalScore -= -extraWords * 50; // trừ nặng từ thiếu
            }

            // Nếu >20% từ mismatch → trừ thêm mạnh
            double mismatchRate = (double)mismatchCount / totalWords;
            if (mismatchRate > 0.2)
            {
                totalScore -= mismatchRate * 50;
            }

            double finalScore = totalScore / totalWords;
            return (int)Math.Clamp(finalScore, 0, 100);
        }





        public List<DiffPiece> GetWordDiff(string input, string target)
        {
            input = NormalizeText(input);
            target = NormalizeText(target);

            // Tách từ + giữ khoảng trắng
            var inputWords = SplitWordsWithSpaces(input);
            var targetWords = SplitWordsWithSpaces(target);

            // Nối bằng ký tự đặc biệt để diff theo "word + space"
            string inputForDiff = string.Join("\n", inputWords);
            string targetForDiff = string.Join("\n", targetWords);

            var diffBuilder = new InlineDiffBuilder(new Differ());
            var diff = diffBuilder.BuildDiffModel(inputForDiff, targetForDiff);

            var wordDiffs = new List<DiffPiece>();
            foreach (var line in diff.Lines)
            {
                wordDiffs.Add(new DiffPiece
                {
                    Text = line.Text.Replace("\n", ""), // xóa ký tự nối
                    Type = line.Type,
                    SubPieces = line.SubPieces
                });
            }

            return wordDiffs;
        }

        // Hàm tách từ nhưng giữ khoảng trắng sau từ
        private string[] SplitWordsWithSpaces(string text)
        {
            var list = new List<string>();
            int i = 0;
            while (i < text.Length)
            {
                int start = i;
                // tìm hết từ
                while (i < text.Length && !char.IsWhiteSpace(text[i])) i++;
                // lấy từ
                int endWord = i;

                // tìm hết khoảng trắng ngay sau từ
                while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
                int endSpace = i;

                list.Add(text[start..endSpace]); // từ + khoảng trắng
            }
            return list.ToArray();
        }


        public List<DiffPiece> GetCharDiff(string input, string target)
        {
            var diffBuilder = new InlineDiffBuilder(new Differ());

            string normInput = NormalizeText(input);
            string normTarget = NormalizeText(target);

            string inputChars = string.Join("\n", normInput.ToCharArray());
            string targetChars = string.Join("\n", normTarget.ToCharArray());

            var diff = diffBuilder.BuildDiffModel(inputChars, targetChars);

            var pieces = new List<DiffPiece>();
            foreach (var line in diff.Lines)
            {
                if (line.SubPieces != null && line.SubPieces.Count > 0)
                    pieces.AddRange(line.SubPieces);
                else
                    pieces.Add(line);
            }

            return pieces;
        }


        private static readonly Dictionary<string, string> _normalizeCache = new();

        private string NormalizeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";

            if (_normalizeCache.TryGetValue(text, out string cached))
                return cached;

            string lower = text.ToLowerInvariant();
            string normalized = lower.Normalize(NormalizationForm.FormD);
            var filtered = new string(normalized
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                .ToArray());

            filtered = filtered.Normalize(NormalizationForm.FormC);

            _normalizeCache[text] = filtered;
            return filtered;
        }

    }
}
