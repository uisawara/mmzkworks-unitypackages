using System.Collections.Generic;

namespace Mmzkworks.muValidation
{
    public readonly struct ValidationMessage
    {
        public readonly ValidationSeverity Severity;
        public readonly string Text;

        public ValidationMessage(ValidationSeverity severity, string text)
        {
            Severity = severity;
            Text = text;
        }
    }

    /// <summary>
    /// Collects the messages reported by validations.
    /// </summary>
    public sealed class ValidationResult
    {
        private readonly List<ValidationMessage> _messages = new List<ValidationMessage>();

        public IReadOnlyList<ValidationMessage> Messages => _messages;

        /// <summary>
        /// The most severe level reported so far. None if nothing was reported.
        /// </summary>
        public ValidationSeverity Severity { get; private set; }

        public void AddError(string message) => Add(ValidationSeverity.Error, message);

        public void AddWarning(string message) => Add(ValidationSeverity.Warning, message);

        public void Add(ValidationSeverity severity, string message)
        {
            if (severity == ValidationSeverity.None) return;
            _messages.Add(new ValidationMessage(severity, message));
            if (severity > Severity) Severity = severity;
        }
    }
}
