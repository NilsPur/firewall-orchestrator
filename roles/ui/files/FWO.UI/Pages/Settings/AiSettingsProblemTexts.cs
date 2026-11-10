using FWO.Data.Ai;

namespace FWO.Ui.Pages.Settings
{
    /// <summary>
    /// Text keys of the messages the AI settings page shows for the problems of <see cref="AiSettings.Validate"/>.
    /// </summary>
    public static class AiSettingsProblemTexts
    {
        private static readonly Dictionary<AiSettingsProblem, string> kTextKeys = new()
        {
            [AiSettingsProblem.ModelMissing] = "E5302",
            [AiSettingsProblem.CredentialMissing] = "E5303",
            [AiSettingsProblem.EndpointInvalid] = "E5304",
            [AiSettingsProblem.EndpointNotSecure] = "E5305",
            [AiSettingsProblem.ContextWindowInvalid] = "E5306",
            [AiSettingsProblem.MaxOutputTokensInvalid] = "E5307",
            [AiSettingsProblem.MaxIterationsInvalid] = "E5308",
            [AiSettingsProblem.MaxConsecutiveErrorsInvalid] = "E5309",
            [AiSettingsProblem.CompactionReserveInvalid] = "E5310"
        };

        /// <summary>
        /// Returns the text key of the message of a problem.
        /// </summary>
        /// <param name="problem">Problem found by the validation</param>
        /// <returns>The text key in the txt table.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The problem has no message.</exception>
        public static string GetTextKey(AiSettingsProblem problem)
        {
            return kTextKeys.TryGetValue(problem, out string? textKey)
                ? textKey
                : throw new ArgumentOutOfRangeException(nameof(problem), problem, "The problem has no message.");
        }
    }
}
