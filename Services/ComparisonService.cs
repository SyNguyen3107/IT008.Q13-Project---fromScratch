using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using FuzzySharp;
using Microsoft.EntityFrameworkCore.Metadata;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;

namespace EasyFlips.Services
{
    public class AiReviewResult
    {
        public int semantic_score { get; set; }
        public bool meaning_match { get; set; }
        public string summary { get; set; }

    }
    public class ComparisonService
    {
        /// <summary>
        /// Hàm 3: Chấm điểm bằng FuzzySharp
        /// </summary>
        private readonly HttpClient _httpClient;
        private const string ApiKey = AppConfig.GeminiApiKey;
        public ComparisonService()
        {
            _httpClient = new HttpClient();
           
        }
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


        //Chấm điểm bằng AI, trả về kết quả và giải thích:

        public async Task<AiReviewResult> CheckSemanticMeaningAsync(string studentAnswer, string correctAnswer)
        {
            if (string.IsNullOrWhiteSpace(studentAnswer) || string.IsNullOrWhiteSpace(correctAnswer))
            {
                System.Diagnostics.Debug.WriteLine("[DEBUG] Student or correct answer is empty");
                return new AiReviewResult
                {
                    semantic_score = 0,
                    meaning_match = false,
                    summary = "Empty input"
                };
            }

            var requestBody = new
            {
                contents = new[]
                {
            new {
                parts = new[]
                {
                    new {
                        text =
                        $"Evaluate the student's answer fairly and objectively compared to the correct answer.\n" +
                        $"Student: {studentAnswer}\n" +
                        $"Correct: {correctAnswer}\n" +
                        $"Return ONLY a JSON object with the following fields:\n" +
                        $"- score (numeric, 0-100, reflecting how correct the student's answer is)\n" +
                        $"- summary (short explanation of why you gave this score)\n" +
                        $"Rules:\n" +
                        $"- Give a high score if the meaning is correct, even if there are minor typos.\n" +
                        $"- Give a low score if the meaning is wrong.\n" +
                        $"- Do not return any text outside the JSON object.\n" +
                        $"- Be fair and unbiased."
                    }
                }
            }
        }
            };

            string jsonBody = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={ApiKey}";

            System.Diagnostics.Debug.WriteLine("[DEBUG] Request JSON: " + jsonBody);
            System.Diagnostics.Debug.WriteLine("[DEBUG] Request URL: " + url);

            var response = await _httpClient.PostAsync(url, content);
            string responseJson = await response.Content.ReadAsStringAsync();

            System.Diagnostics.Debug.WriteLine("[DEBUG] Response JSON: " + responseJson);

            try
            {
                var resultObj = JObject.Parse(responseJson);
                string aiText = resultObj["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();

                if (string.IsNullOrWhiteSpace(aiText))
                {
                    return new AiReviewResult
                    {
                        semantic_score = 0,
                        meaning_match = false,
                        summary = "Empty AI response"
                    };
                }

                // Loại bỏ code block nếu có
                aiText = Regex.Replace(aiText, @"^```json\s*|```$", "", RegexOptions.IgnoreCase).Trim();

                // Nếu AI chỉ trả về số, parse thành int và gán vào semantic_score
                if (int.TryParse(aiText, out int score))
                {
                    return new AiReviewResult
                    {
                        semantic_score = score,
                        meaning_match = score >= 80, // ví dụ: >=80 nghĩa là đúng ý
                        summary = $"Score from AI prompt: {score}"
                    };
                }

                // fallback: nếu AI trả về dạng khác
                return new AiReviewResult
                {
                    semantic_score = 0,
                    meaning_match = false,
                    summary = aiText
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[DEBUG] Exception parsing AI response: " + ex.Message);
                return new AiReviewResult
                {
                    semantic_score = 0,
                    meaning_match = false,
                    summary = responseJson
                };
            }
        }

        // Chấm điểm bằng AI, chỉ trả về điểm số

        public async Task<int> CheckSemanticScoreAsync(string studentAnswer, string correctAnswer)
        {
            if (string.IsNullOrWhiteSpace(studentAnswer) || string.IsNullOrWhiteSpace(correctAnswer))
                return 0;

            var requestBody = new
            {
                contents = new[]
                {
            new {
                parts = new[]
                {
                    new {
                        text = $"Evaluate the student's answer and return only a single integer score 0-100.\n" +
                               $"Student: {studentAnswer}\n" +
                               $"Correct: {correctAnswer}\n"
                    //Cần có câu lệnh promt mới để trả về kết quả tương quan nhất
                    }
                }
            }
        }
            };

            string jsonBody = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={ApiKey}";

            var response = await _httpClient.PostAsync(url, content);
            string responseJson = await response.Content.ReadAsStringAsync();

            // Lấy text từ candidates
            try
            {
                var resultObj = JObject.Parse(responseJson);
                string aiText = resultObj["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();

                if (int.TryParse(aiText, out int score))
                    return Math.Clamp(score, 0, 100);

                return 0;
            }
            catch
            {
                return 0;
            }
        }




        //public List<DiffPiece> GetWordDiff(string input, string target)
        //{
        //    input = NormalizeText(input);
        //    target = NormalizeText(target);

        //    // Tách từ + giữ khoảng trắng
        //    var inputWords = SplitWordsWithSpaces(input);
        //    var targetWords = SplitWordsWithSpaces(target);

        //    // Nối bằng ký tự đặc biệt để diff theo "word + space"
        //    string inputForDiff = string.Join("\n", inputWords);
        //    string targetForDiff = string.Join("\n", targetWords);

        //    var diffBuilder = new InlineDiffBuilder(new Differ());
        //    var diff = diffBuilder.BuildDiffModel(inputForDiff, targetForDiff);

        //    var wordDiffs = new List<DiffPiece>();
        //    foreach (var line in diff.Lines)
        //    {
        //        wordDiffs.Add(new DiffPiece
        //        {
        //            Text = line.Text.Replace("\n", ""), // xóa ký tự nối
        //            Type = line.Type,
        //            SubPieces = line.SubPieces
        //        });
        //    }

        //    return wordDiffs;
        //}

        // Hàm tách từ nhưng giữ khoảng trắng sau từ
        //private string[] SplitWordsWithSpaces(string text)
        //{
        //    var list = new List<string>();
        //    int i = 0;
        //    while (i < text.Length)
        //    {
        //        int start = i;
        //        // tìm hết từ
        //        while (i < text.Length && !char.IsWhiteSpace(text[i])) i++;
        //        // lấy từ
        //        int endWord = i;

        //        // tìm hết khoảng trắng ngay sau từ
        //        while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
        //        int endSpace = i;

        //        list.Add(text[start..endSpace]); // từ + khoảng trắng
        //    }
        //    return list.ToArray();
        //}






    }
}
