using System;

namespace Mmzkworks.muValidation
{
    /// <summary>
    /// Base class of validation attributes. Put subclasses on a Component / ScriptableObject class,
    /// or on a field of one. Several can be combined on the same target.
    /// To add a new kind of validation, derive from this class and implement <see cref="Validate"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Field, AllowMultiple = true, Inherited = true)]
    public abstract class ValidationAttribute : Attribute
    {
        /// <summary>
        /// Severity of the problems this attribute reports. Error by default.
        /// </summary>
        public ValidationSeverity Severity { get; set; } = ValidationSeverity.Error;

        /// <summary>
        /// True if the result can change when other GameObjects change (names, existence, ...),
        /// not only the target itself. Such validations are re-run after any change in the scene,
        /// so override this only when needed.
        /// </summary>
        public virtual bool DependsOnOtherObjects => false;

        /// <summary>
        /// Checks the target and reports each problem with <see cref="ValidationContext.Report(string)"/>.
        /// Called in the Editor only. Keep it free of side effects; results are cached.
        /// For a class attribute <see cref="ValidationContext.Field"/> is null;
        /// for a field attribute it is the field and <see cref="ValidationContext.Value"/> is its value.
        /// </summary>
        public abstract void Validate(ValidationContext context);
    }
}
