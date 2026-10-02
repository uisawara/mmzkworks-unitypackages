using System.Collections.Generic;

namespace Mmzkworks.muValidation.Editor
{
    /// <summary>
    /// Validation outcome for one asset or GameObject: the most severe level and a message for tooltips.
    /// </summary>
    public readonly struct ValidationSummary
    {
        public static readonly ValidationSummary Valid = default;

        public readonly ValidationSeverity Severity;

        /// <summary>
        /// Message lines for display. Null when valid.
        /// </summary>
        public readonly string Message;

        public ValidationSummary(ValidationSeverity severity, string message)
        {
            Severity = string.IsNullOrEmpty(message) ? ValidationSeverity.None : severity;
            Message = Severity == ValidationSeverity.None ? null : message;
        }

        public bool IsValid => Severity == ValidationSeverity.None;

        public static ValidationSummary Error(string message) => new ValidationSummary(ValidationSeverity.Error, message);

        public static ValidationSummary From(ValidationResult result)
        {
            if (result.Severity == ValidationSeverity.None) return Valid;

            var lines = new List<string>(result.Messages.Count);
            foreach (var message in result.Messages)
            {
                lines.Add(message.Severity == ValidationSeverity.Warning ? "Warning: " + message.Text : message.Text);
            }

            return new ValidationSummary(result.Severity, string.Join("\n", lines));
        }

        /// <summary>
        /// Takes the more severe level and joins both messages.
        /// </summary>
        public static ValidationSummary Combine(ValidationSummary a, ValidationSummary b)
        {
            if (a.IsValid) return b;
            if (b.IsValid) return a;
            return new ValidationSummary(a.Severity > b.Severity ? a.Severity : b.Severity, a.Message + "\n" + b.Message);
        }
    }
}
