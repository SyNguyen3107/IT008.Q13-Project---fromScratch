using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using FuzzySharp;
using System.Text;

namespace EasyFlips.Services
{
   
    public class ComparisonService
    {
        public bool IsAnswerAcceptable(string input, string target, int threshold = 80)
        {
           if (string.IsNullOrEmpty(input)) return false;
           int score = SmartScore(input, target);
           return score >= threshold;
        }

        public static int LevenshteinDistance(string s, string t)
        {
            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            for (int i = 0; i <= n; i++) d[i, 0] = i;
            for (int j = 0; j <= m; j++) d[0, j] = j;

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (s[i - 1] == t[j - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost
                    );
                }
            }

            return d[n, m];
        }

        public int SmartScore(string input, string target)
        {
            if (string.IsNullOrWhiteSpace(input)) return 0;

            input = NormalizeText(input);
            target = NormalizeText(target);

            int dist = LevenshteinDistance(input, target);
            int maxLen = Math.Max(input.Length, target.Length);

            double similarity = 1.0 - (double)dist / maxLen;
            int score = (int)(similarity * 100);

            return Math.Clamp(score, 0, 100);
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

            // Chỉ đưa về chữ thường, không bỏ dấu
            string lower = text.ToLowerInvariant();

            // Chuẩn hóa Unicode về FormC để tránh ký tự lạ
            string filtered = lower.Normalize(NormalizationForm.FormC);

            _normalizeCache[text] = filtered;
            return filtered;
        }


        public List<DiffPiece> GetWordDiff(string input, string target)
        {
            input = NormalizeText(input);
            target = NormalizeText(target);

            var inputWords = SplitWordsWithSpaces(input);
            var targetWords = SplitWordsWithSpaces(target);


            string inputForDiff = string.Join("\n", inputWords);
            string targetForDiff = string.Join("\n", targetWords);

            var diffBuilder = new InlineDiffBuilder(new Differ());
            var diff = diffBuilder.BuildDiffModel(inputForDiff, targetForDiff);

            var wordDiffs = new List<DiffPiece>();
            foreach (var line in diff.Lines)
            {
                wordDiffs.Add(new DiffPiece
                {
                    Text = line.Text.Replace("\n", ""), 
                    Type = line.Type,
                    SubPieces = line.SubPieces
                });
            }

            return wordDiffs;
        }

       
        private string[] SplitWordsWithSpaces(string text)
        {
            var list = new List<string>();
            int i = 0;
            while (i < text.Length)
            {
                int start = i;

                while (i < text.Length && !char.IsWhiteSpace(text[i])) i++;

                int endWord = i;


                while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
                int endSpace = i;

                list.Add(text[start..endSpace]); 
            }
            return list.ToArray();
        }
    }
}
