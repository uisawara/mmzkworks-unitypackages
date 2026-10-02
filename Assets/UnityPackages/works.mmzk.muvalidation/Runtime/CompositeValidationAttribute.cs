using System;
using System.Collections.Generic;
using System.Linq;

namespace Mmzkworks.muValidation
{
    public enum CompositeMode
    {
        /// <summary>Every validation must pass. Each failure is reported.</summary>
        All,

        /// <summary>At least one validation must pass. Otherwise one problem listing all failures is reported.</summary>
        Any,
    }

    /// <summary>
    /// Combines other validations into one attribute. Derive from it and return the validations to combine:
    /// <code>
    /// public class UIRootAttribute : CompositeValidationAttribute
    /// {
    ///     protected override IEnumerable&lt;ValidationAttribute&gt; CreateValidations()
    ///     {
    ///         yield return new GameObjectNameAttribute("^UI_");
    ///         yield return new SceneRootOnlyAttribute();
    ///     }
    /// }
    /// </code>
    /// In All mode, each problem keeps the severity of its validation, capped at this attribute's Severity.
    /// In Any mode, the combined problem uses this attribute's Severity.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Field, AllowMultiple = true, Inherited = true)]
    public abstract class CompositeValidationAttribute : ValidationAttribute
    {
        private ValidationAttribute[] _validations;

        public CompositeMode Mode { get; set; } = CompositeMode.All;

        protected abstract IEnumerable<ValidationAttribute> CreateValidations();

        private ValidationAttribute[] Validations =>
            _validations ??= (CreateValidations() ?? Enumerable.Empty<ValidationAttribute>()).Where(v => v != null).ToArray();

        public override bool DependsOnOtherObjects => Validations.Any(v => v.DependsOnOtherObjects);

        public override void Validate(ValidationContext context)
        {
            if (Mode == CompositeMode.All)
            {
                foreach (var validation in Validations)
                {
                    var severity = validation.Severity < Severity ? validation.Severity : Severity;
                    validation.Validate(context.With(severity));
                }

                return;
            }

            var failures = new List<string>();
            foreach (var validation in Validations)
            {
                var result = new ValidationResult();
                validation.Validate(context.With(validation.Severity, result));
                if (result.Severity == ValidationSeverity.None) return;
                failures.AddRange(result.Messages.Select(m => m.Text));
            }

            if (failures.Count > 0) context.Report("None of the conditions is met: " + string.Join(" / ", failures));
        }
    }
}
