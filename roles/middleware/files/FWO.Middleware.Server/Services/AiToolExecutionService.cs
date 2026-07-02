using Microsoft.Extensions.AI;
using System.ComponentModel;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace FWO.Middleware.Server.Services
{
    /// <summary>
    /// Provides FWO tools.
    /// </summary>
    public class AiToolExecutionService
    {
        private const int kMaxResultCount = 5;
        private const int kMaxExcerptLength = 500;
        private const string kTextSqlRelativePath = "database/files/sql/idempotent/fworch-texts.sql";
        private static readonly Regex LocalizedTextLookupRegex = new(
            @"@?\(?\s*(?:Html\.Raw\(\s*)?userConfig\.GetText\(\s*""(?<key>[^""]+)""\s*\)\s*\)?\s*\)?",
            RegexOptions.Compiled);
        private static readonly Regex SqlTextInsertRegex = new(
            @"INSERT\s+INTO\s+txt\s+VALUES\s*\(\s*'(?<key>(?:''|[^'])*)'\s*,\s*'(?<language>(?:''|[^'])*)'\s*,\s*'(?<text>(?:''|[^'])*)'\s*\)\s*;",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        /// <summary>
        /// Create all tools available to be used by the FWO agents.
        /// </summary>
        /// <returns>Complete tool list</returns>
        public IList<AITool> CreateTools()
        {
            return
            [
                AIFunctionFactory.Create(this.read_fwo_documentation),
            ];
        }

        /// <summary>
        /// Searches FWO UI help.
        /// </summary>
        [Description("Search and read Firewall Orchestrator UI help by query text.")]
        public Task<string> read_fwo_documentation([Description("User-facing search text or lookup term.")] string query)
        {
            string? helpDirectory = ResolveHelpDirectory();
            if (helpDirectory == null)
            {
                return Task.FromResult("FWO help files are not available on this middleware host.");
            }
            return Task.FromResult(SearchHelpDocumentation(query, helpDirectory));
        }

        /// <summary>
        /// Searches a concrete help directory. Exposed for unit tests and for future callers that
        /// already know the deployed UI help path.
        /// </summary>
        public static string SearchHelpDocumentation(string query, string helpDirectory)
        {
            return SearchHelpDocumentation(query, helpDirectory, ResolveLocalizedTextsFile(helpDirectory));
        }

        /// <summary>
        /// Searches a concrete help directory using a known localized text SQL file.
        /// </summary>
        public static string SearchHelpDocumentation(string query, string helpDirectory, string? localizedTextsFile)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return "Please provide a help search query.";
            }
            if (!Directory.Exists(helpDirectory))
            {
                return "FWO help files are not available on this middleware host.";
            }

            List<string> terms = ExtractTerms(query);
            Dictionary<string, List<LocalizedText>> localizedTexts = LoadLocalizedTexts(localizedTextsFile);
            List<HelpSearchResult> results = Directory.EnumerateFiles(helpDirectory, "*.cshtml", SearchOption.TopDirectoryOnly)
                .Select(file => BuildSearchResult(file, query, terms, localizedTexts))
                .Where(result => result.Score > 0)
                .OrderByDescending(result => result.Score)
                .ThenBy(result => result.Title, StringComparer.OrdinalIgnoreCase)
                .Take(kMaxResultCount)
                .ToList();

            if (results.Count == 0)
            {
                return $"No FWO help page matched '{query}'.";
            }

            StringBuilder builder = new();
            builder.AppendLine($"FWO help matches for '{query}':");
            foreach (HelpSearchResult result in results)
            {
                builder.AppendLine();
                builder.Append("- ");
                builder.Append(result.Title);
                builder.Append(": ");
                builder.Append(result.Excerpt);
            }
            return builder.ToString();
        }

        private static string? ResolveHelpDirectory()
        {
            foreach (string candidate in BuildHelpDirectoryCandidates())
            {
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
            }
            return null;
        }

        private static string? ResolveLocalizedTextsFile(string helpDirectory)
        {
            foreach (string candidate in BuildLocalizedTextsFileCandidates(helpDirectory))
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            return null;
        }

        private static IEnumerable<string> BuildLocalizedTextsFileCandidates(string helpDirectory)
        {
            yield return Path.GetFullPath(Path.Combine(helpDirectory, "..", "..", "..", "..", "..", kTextSqlRelativePath));
            yield return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", kTextSqlRelativePath));
            yield return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", kTextSqlRelativePath));
            yield return "/usr/local/fworch/database/files/sql/idempotent/fworch-texts.sql";
        }

        private static IEnumerable<string> BuildHelpDirectoryCandidates()
        {
            string currentDirectory = Directory.GetCurrentDirectory();
            string baseDirectory = AppContext.BaseDirectory;
            yield return Path.GetFullPath(Path.Combine(currentDirectory, "..", "..", "..", "ui", "files", "FWO.UI", "Pages", "Help"));
            yield return Path.GetFullPath(Path.Combine(currentDirectory, "..", "..", "..", "..", "..", "ui", "files", "FWO.UI", "Pages", "Help"));
            yield return "/usr/local/fworch/ui/files/FWO.UI/Pages/Help";
            yield return Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "Pages", "Help"));
            yield return Path.GetFullPath(Path.Combine(baseDirectory, "..", "..", "..", "..", "..", "..", "ui", "files", "FWO.UI", "Pages", "Help"));
        }

        private static HelpSearchResult BuildSearchResult(string file, string query, List<string> terms,
            Dictionary<string, List<LocalizedText>> localizedTexts)
        {
            string title = BuildTitle(file);
            string text = ExtractPlainText(ResolveLocalizedTextReferences(File.ReadAllText(file), localizedTexts));
            string searchable = $"{title} {text}".ToUpperInvariant();
            string normalizedQuery = query.Trim().ToUpperInvariant();
            int score = searchable.Contains(normalizedQuery) ? 20 : 0;
            foreach (string term in terms)
            {
                score += CountOccurrences(searchable, term.ToUpperInvariant());
                if (title.Contains(term, StringComparison.OrdinalIgnoreCase))
                {
                    score += 5;
                }
            }
            return new HelpSearchResult(title, score, BuildExcerpt(text, terms));
        }

        private static Dictionary<string, List<LocalizedText>> LoadLocalizedTexts(string? localizedTextsFile)
        {
            Dictionary<string, List<LocalizedText>> localizedTexts = new(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(localizedTextsFile) || !File.Exists(localizedTextsFile))
            {
                return localizedTexts;
            }
            foreach (Match match in SqlTextInsertRegex.Matches(File.ReadAllText(localizedTextsFile)))
            {
                string key = UnescapeSqlText(match.Groups["key"].Value);
                if (!localizedTexts.TryGetValue(key, out List<LocalizedText>? values))
                {
                    values = [];
                    localizedTexts[key] = values;
                }
                values.Add(new LocalizedText(
                    UnescapeSqlText(match.Groups["language"].Value),
                    UnescapeSqlText(match.Groups["text"].Value)));
            }
            return localizedTexts;
        }

        private static string ResolveLocalizedTextReferences(string content, Dictionary<string, List<LocalizedText>> localizedTexts)
        {
            return LocalizedTextLookupRegex.Replace(content, match =>
            {
                string key = match.Groups["key"].Value;
                return localizedTexts.TryGetValue(key, out List<LocalizedText>? values)
                    ? JoinLocalizedTexts(values)
                    : key;
            });
        }

        private static string JoinLocalizedTexts(List<LocalizedText> values)
        {
            return string.Join(" ", values
                .OrderBy(value => value.Language.Equals("English", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(value => value.Language, StringComparer.OrdinalIgnoreCase)
                .Select(value => value.Text));
        }

        private static string UnescapeSqlText(string value)
        {
            return value.Replace("''", "'");
        }

        private static List<string> ExtractTerms(string query)
        {
            return Regex.Matches(query, @"[\p{L}\p{N}_-]{2,}")
                .Select(match => match.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string ExtractPlainText(string content)
        {
            string withoutCodeBlocks = Regex.Replace(content, @"@\{[\s\S]*?\}", " ");
            string withoutTags = Regex.Replace(withoutCodeBlocks, "<[^>]+>", " ");
            string decoded = WebUtility.HtmlDecode(withoutTags);
            return Regex.Replace(decoded, @"\s+", " ").Trim();
        }

        private static string BuildTitle(string file)
        {
            string title = Path.GetFileNameWithoutExtension(file);
            if (title.StartsWith("Help", StringComparison.Ordinal))
            {
                title = title[4..];
            }
            return Regex.Replace(title, "([a-z])([A-Z])", "$1 $2");
        }

        private static int CountOccurrences(string text, string term)
        {
            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(term, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += term.Length;
            }
            return count;
        }

        private static string BuildExcerpt(string text, List<string> terms)
        {
            if (text.Length <= kMaxExcerptLength)
            {
                return text;
            }
            int firstMatch = terms.Select(term => text.IndexOf(term, StringComparison.OrdinalIgnoreCase))
                .Where(index => index >= 0)
                .DefaultIfEmpty(0)
                .Min();
            int start = Math.Max(0, firstMatch - kMaxExcerptLength / 3);
            int length = Math.Min(kMaxExcerptLength, text.Length - start);
            string prefix = start == 0 ? "" : "... ";
            string suffix = start + length >= text.Length ? "" : " ...";
            return $"{prefix}{text.Substring(start, length).Trim()}{suffix}";
        }

        private sealed record LocalizedText(string Language, string Text);

        private sealed record HelpSearchResult(string Title, int Score, string Excerpt);
    }
}
