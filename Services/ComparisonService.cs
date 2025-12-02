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

        public List<DiffPiece> GetVisualDiff(string input, string target)
        {
            var diffBuilder = new InlineDiffBuilder(new Differ());
            var diff = diffBuilder.BuildDiffModel(input ?? "", target ?? "");

            if (diff.Lines.Count > 0)
            {
                return diff.Lines[0].SubPieces;
            }
            return new List<DiffPiece>();
        }

        public List<DiffPiece> GetCharDiff(string input, string target)
        {
            var diffBuilder = new InlineDiffBuilder(new Differ());

            string normalizedInput = NormalizeText(input);
            string normalizedTarget = NormalizeText(target);

            // Mỗi ký tự là một dòng
            string inputChars = string.Join("\n", normalizedInput.ToCharArray());
            string targetChars = string.Join("\n", normalizedTarget.ToCharArray());

            // So sánh từng dòng (ký tự)
            var diff = diffBuilder.BuildDiffModel(oldText: inputChars, newText: targetChars);


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





        // Hàm chuẩn hóa chuỗi: bỏ dấu, chuyển về chữ thường
        private string NormalizeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";

            string lower = text.ToLowerInvariant();
            string normalized = lower.Normalize(NormalizationForm.FormD);
            var filtered = new string(normalized
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                .ToArray());

            return filtered.Normalize(NormalizationForm.FormC);
        }

    }
}
