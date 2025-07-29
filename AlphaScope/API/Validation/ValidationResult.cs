using System;
using System.Collections.Generic;
using System.Linq;

namespace AlphaScope.API.Validation
{
    /// <summary>
    /// Represents a single validation error with details about the property and error message
    /// </summary>
    public class ValidationError
    {
        /// <summary>
        /// The name of the property that failed validation
        /// </summary>
        public string PropertyName { get; set; } = string.Empty;

        /// <summary>
        /// The error message describing the validation failure
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// The attempted value that caused the validation failure
        /// </summary>
        public object? AttemptedValue { get; set; }

        /// <summary>
        /// Error code for programmatic handling
        /// </summary>
        public string? ErrorCode { get; set; }

        public ValidationError() { }

        public ValidationError(string propertyName, string errorMessage, object? attemptedValue = null, string? errorCode = null)
        {
            PropertyName = propertyName;
            ErrorMessage = errorMessage;
            AttemptedValue = attemptedValue;
            ErrorCode = errorCode;
        }

        public override string ToString()
        {
            return $"{PropertyName}: {ErrorMessage}";
        }
    }

    /// <summary>
    /// Represents the result of a validation operation with success status and any validation errors
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// Indicates whether the validation was successful
        /// </summary>
        public bool IsValid { get; private set; }

        /// <summary>
        /// Collection of validation errors encountered during validation
        /// </summary>
        public IReadOnlyList<ValidationError> Errors { get; private set; }

        /// <summary>
        /// Gets the first error message, or empty string if validation succeeded
        /// </summary>
        public string FirstErrorMessage => Errors.FirstOrDefault()?.ErrorMessage ?? string.Empty;

        /// <summary>
        /// Gets all error messages concatenated with semicolons
        /// </summary>
        public string AllErrorMessages => string.Join("; ", Errors.Select(e => e.ErrorMessage));

        private ValidationResult(bool isValid, IReadOnlyList<ValidationError> errors)
        {
            IsValid = isValid;
            Errors = errors;
        }

        /// <summary>
        /// Creates a successful validation result
        /// </summary>
        public static ValidationResult Success()
        {
            return new ValidationResult(true, new List<ValidationError>());
        }

        /// <summary>
        /// Creates a failed validation result with a single error
        /// </summary>
        public static ValidationResult Failure(string propertyName, string errorMessage, object? attemptedValue = null, string? errorCode = null)
        {
            var error = new ValidationError(propertyName, errorMessage, attemptedValue, errorCode);
            return new ValidationResult(false, new List<ValidationError> { error });
        }

        /// <summary>
        /// Creates a failed validation result with multiple errors
        /// </summary>
        public static ValidationResult Failure(IEnumerable<ValidationError> errors)
        {
            var errorList = errors.ToList();
            return new ValidationResult(false, errorList);
        }

        /// <summary>
        /// Creates a failed validation result from another validation result
        /// </summary>
        public static ValidationResult Failure(ValidationResult other)
        {
            return new ValidationResult(false, other.Errors);
        }

        /// <summary>
        /// Combines multiple validation results into a single result
        /// </summary>
        public static ValidationResult Combine(params ValidationResult[] results)
        {
            var allErrors = new List<ValidationError>();
            var allValid = true;

            foreach (var result in results)
            {
                if (!result.IsValid)
                {
                    allValid = false;
                    allErrors.AddRange(result.Errors);
                }
            }

            return allValid ? Success() : new ValidationResult(false, allErrors);
        }

        /// <summary>
        /// Adds an error to this validation result, making it invalid
        /// </summary>
        public ValidationResult AddError(string propertyName, string errorMessage, object? attemptedValue = null, string? errorCode = null)
        {
            var newError = new ValidationError(propertyName, errorMessage, attemptedValue, errorCode);
            var newErrors = new List<ValidationError>(Errors) { newError };
            return new ValidationResult(false, newErrors);
        }

        /// <summary>
        /// Adds multiple errors to this validation result, making it invalid
        /// </summary>
        public ValidationResult AddErrors(IEnumerable<ValidationError> errors)
        {
            var newErrors = new List<ValidationError>(Errors);
            newErrors.AddRange(errors);
            return new ValidationResult(false, newErrors);
        }

        /// <summary>
        /// Returns a string representation of all validation errors
        /// </summary>
        public override string ToString()
        {
            if (IsValid)
                return "Validation succeeded";

            return $"Validation failed: {AllErrorMessages}";
        }
    }
}