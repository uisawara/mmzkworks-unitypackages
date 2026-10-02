using System;
using System.Text.RegularExpressions;

namespace Mmzkworks.muValidation
{
    /// <summary>
    /// The GameObject name must match the regex. Use ^...$ for a full match.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public class GameObjectNameAttribute : ValidationAttribute
    {
        public string Pattern { get; }

        private Regex _regex;
        private string _patternError;

        public GameObjectNameAttribute(string pattern)
        {
            Pattern = pattern ?? "";
        }

        public override void Validate(ValidationContext context)
        {
            var go = context.GameObject;
            if (go == null) return;

            if (_regex == null && _patternError == null)
            {
                try
                {
                    _regex = new Regex(Pattern);
                }
                catch (ArgumentException e)
                {
                    _patternError = e.Message;
                }
            }

            if (_patternError != null)
            {
                context.Report(ValidationSeverity.Error, $"Invalid regex /{Pattern}/: {_patternError}");
                return;
            }

            if (!_regex.IsMatch(go.name)) context.Report($"GameObject name \"{go.name}\" must match /{Pattern}/");
        }
    }
}
