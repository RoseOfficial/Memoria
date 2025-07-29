using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AlphaScope.API.Validation
{
    /// <summary>
    /// Extension methods for common validation patterns with fluent API
    /// </summary>
    public static class ValidationExtensions
    {
        #region String Validation Extensions

        /// <summary>
        /// Validates that a string is not null or empty
        /// </summary>
        public static ValidationResult NotNullOrEmpty(this string? value, string propertyName, string? customMessage = null)
        {
            if (string.IsNullOrEmpty(value))
            {
                var message = customMessage ?? $"{propertyName} is required and cannot be empty";
                return ValidationResult.Failure(propertyName, message, value, "REQUIRED");
            }
            return ValidationResult.Success();
        }

        /// <summary>
        /// Validates that a string is not null, empty, or whitespace
        /// </summary>
        public static ValidationResult NotNullOrWhiteSpace(this string? value, string propertyName, string? customMessage = null)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                var message = customMessage ?? $"{propertyName} is required and cannot be empty or whitespace";
                return ValidationResult.Failure(propertyName, message, value, "REQUIRED");
            }
            return ValidationResult.Success();
        }

        /// <summary>
        /// Validates string length constraints
        /// </summary>
        public static ValidationResult Length(this string? value, string propertyName, int? minLength = null, int? maxLength = null, string? customMessage = null)
        {
            if (value == null && (minLength == null || minLength == 0))
                return ValidationResult.Success();

            var actualLength = value?.Length ?? 0;

            if (minLength.HasValue && actualLength < minLength.Value)
            {
                var message = customMessage ?? $"{propertyName} must be at least {minLength} characters long";
                return ValidationResult.Failure(propertyName, message, value, "MIN_LENGTH");
            }

            if (maxLength.HasValue && actualLength > maxLength.Value)
            {
                var message = customMessage ?? $"{propertyName} must be no more than {maxLength} characters long";
                return ValidationResult.Failure(propertyName, message, value, "MAX_LENGTH");
            }

            return ValidationResult.Success();
        }

        /// <summary>
        /// Validates string against a regular expression pattern
        /// </summary>
        public static ValidationResult Matches(this string? value, string propertyName, string pattern, string? customMessage = null)
        {
            if (value == null)
                return ValidationResult.Success();

            if (!Regex.IsMatch(value, pattern, RegexOptions.Compiled))
            {
                var message = customMessage ?? $"{propertyName} format is invalid";
                return ValidationResult.Failure(propertyName, message, value, "INVALID_FORMAT");
            }

            return ValidationResult.Success();
        }

        /// <summary>
        /// Validates that string contains only alphanumeric characters and allowed special characters
        /// </summary>
        public static ValidationResult IsValidPlayerName(this string? value, string propertyName, string? customMessage = null)
        {
            if (string.IsNullOrEmpty(value))
                return ValidationResult.Success(); // Handled by NotNullOrEmpty if required

            // FFXIV player names can contain letters, apostrophes, hyphens, and spaces
            var pattern = @"^[a-zA-Z\s'\-]+$";
            if (!Regex.IsMatch(value, pattern))
            {
                var message = customMessage ?? $"{propertyName} can only contain letters, spaces, apostrophes, and hyphens";
                return ValidationResult.Failure(propertyName, message, value, "INVALID_PLAYER_NAME");
            }

            return ValidationResult.Success();
        }

        #endregion

        #region Numeric Validation Extensions

        /// <summary>
        /// Validates that a numeric value is within a specified range
        /// </summary>
        public static ValidationResult InRange<T>(this T? value, string propertyName, T? min = null, T? max = null, string? customMessage = null) 
            where T : struct, IComparable<T>
        {
            if (!value.HasValue)
                return ValidationResult.Success();

            var actualValue = value.Value;

            if (min.HasValue && actualValue.CompareTo(min.Value) < 0)
            {
                var message = customMessage ?? $"{propertyName} must be at least {min}";
                return ValidationResult.Failure(propertyName, message, value, "MIN_VALUE");
            }

            if (max.HasValue && actualValue.CompareTo(max.Value) > 0)
            {
                var message = customMessage ?? $"{propertyName} must be no more than {max}";
                return ValidationResult.Failure(propertyName, message, value, "MAX_VALUE");
            }

            return ValidationResult.Success();
        }

        /// <summary>
        /// Validates that a numeric value is greater than zero
        /// </summary>
        public static ValidationResult GreaterThanZero<T>(this T? value, string propertyName, string? customMessage = null) 
            where T : struct, IComparable<T>
        {
            if (!value.HasValue)
                return ValidationResult.Success();

            var zero = (T)Convert.ChangeType(0, typeof(T));
            if (value.Value.CompareTo(zero) <= 0)
            {
                var message = customMessage ?? $"{propertyName} must be greater than zero";
                return ValidationResult.Failure(propertyName, message, value, "GREATER_THAN_ZERO");
            }

            return ValidationResult.Success();
        }

        /// <summary>
        /// Validates that a numeric value is positive (greater than or equal to zero)
        /// </summary>
        public static ValidationResult PositiveOrZero<T>(this T? value, string propertyName, string? customMessage = null) 
            where T : struct, IComparable<T>
        {
            if (!value.HasValue)
                return ValidationResult.Success();

            var zero = (T)Convert.ChangeType(0, typeof(T));
            if (value.Value.CompareTo(zero) < 0)
            {
                var message = customMessage ?? $"{propertyName} must be zero or positive";
                return ValidationResult.Failure(propertyName, message, value, "POSITIVE_OR_ZERO");
            }

            return ValidationResult.Success();
        }

        /// <summary>
        /// Validates that a value is a valid FFXIV World ID
        /// </summary>
        public static ValidationResult IsValidWorldId(this ushort? value, string propertyName, string? customMessage = null)
        {
            if (!value.HasValue)
                return ValidationResult.Success();

            // FFXIV World IDs are typically in ranges like 1-100 for regular worlds
            // This is a reasonable range that covers all known worlds
            if (value.Value < 1 || value.Value > 10000)
            {
                var message = customMessage ?? $"{propertyName} must be a valid World ID";
                return ValidationResult.Failure(propertyName, message, value, "INVALID_WORLD_ID");
            }

            return ValidationResult.Success();
        }

        /// <summary>
        /// Validates that a value is a valid FFXIV Job ID
        /// </summary>
        public static ValidationResult IsValidJobId(this byte? value, string propertyName, string? customMessage = null)
        {
            if (!value.HasValue)
                return ValidationResult.Success();

            // FFXIV Job IDs range from 1-40+ (including combat and non-combat jobs)
            if (value.Value < 1 || value.Value > 50)
            {
                var message = customMessage ?? $"{propertyName} must be a valid Job ID";
                return ValidationResult.Failure(propertyName, message, value, "INVALID_JOB_ID");
            }

            return ValidationResult.Success();
        }

        /// <summary>
        /// Validates that a value is a valid FFXIV Job Level
        /// </summary>
        public static ValidationResult IsValidJobLevel(this short? value, string propertyName, string? customMessage = null)
        {
            if (!value.HasValue)
                return ValidationResult.Success();

            // FFXIV Job Levels range from 1-90 (as of current expansion)
            if (value.Value < 1 || value.Value > 100)
            {
                var message = customMessage ?? $"{propertyName} must be between 1 and 100";
                return ValidationResult.Failure(propertyName, message, value, "INVALID_JOB_LEVEL");
            }

            return ValidationResult.Success();
        }

        #endregion

        #region Collection Validation Extensions

        /// <summary>
        /// Validates that a collection is not null or empty
        /// </summary>
        public static ValidationResult NotNullOrEmpty<T>(this IEnumerable<T>? collection, string propertyName, string? customMessage = null)
        {
            if (collection == null || !collection.Any())
            {
                var message = customMessage ?? $"{propertyName} cannot be null or empty";
                return ValidationResult.Failure(propertyName, message, collection, "REQUIRED");
            }
            return ValidationResult.Success();
        }

        /// <summary>
        /// Validates collection size constraints
        /// </summary>
        public static ValidationResult Count<T>(this IEnumerable<T>? collection, string propertyName, int? minCount = null, int? maxCount = null, string? customMessage = null)
        {
            if (collection == null && (minCount == null || minCount == 0))
                return ValidationResult.Success();

            var actualCount = collection?.Count() ?? 0;

            if (minCount.HasValue && actualCount < minCount.Value)
            {
                var message = customMessage ?? $"{propertyName} must contain at least {minCount} items";
                return ValidationResult.Failure(propertyName, message, collection, "MIN_COUNT");
            }

            if (maxCount.HasValue && actualCount > maxCount.Value)
            {
                var message = customMessage ?? $"{propertyName} must contain no more than {maxCount} items";
                return ValidationResult.Failure(propertyName, message, collection, "MAX_COUNT");
            }

            return ValidationResult.Success();
        }

        /// <summary>
        /// Validates that all items in a collection are unique
        /// </summary>
        public static ValidationResult AllUnique<T>(this IEnumerable<T>? collection, string propertyName, string? customMessage = null)
        {
            if (collection == null)
                return ValidationResult.Success();

            var items = collection.ToList();
            var distinctItems = items.Distinct().ToList();

            if (items.Count != distinctItems.Count)
            {
                var message = customMessage ?? $"{propertyName} must contain only unique values";
                return ValidationResult.Failure(propertyName, message, collection, "DUPLICATE_VALUES");
            }

            return ValidationResult.Success();
        }

        #endregion

        #region DateTime Validation Extensions

        /// <summary>
        /// Validates that a DateTime is not in the future
        /// </summary>
        public static ValidationResult NotInFuture(this DateTime? value, string propertyName, string? customMessage = null)
        {
            if (!value.HasValue)
                return ValidationResult.Success();

            if (value.Value > DateTime.UtcNow)
            {
                var message = customMessage ?? $"{propertyName} cannot be in the future";
                return ValidationResult.Failure(propertyName, message, value, "FUTURE_DATE");
            }

            return ValidationResult.Success();
        }

        /// <summary>
        /// Validates that a DateTime is within a reasonable range for created timestamps
        /// </summary>
        public static ValidationResult IsReasonableTimestamp(this DateTime? value, string propertyName, string? customMessage = null)
        {
            if (!value.HasValue)
                return ValidationResult.Success();

            var minDate = new DateTime(2010, 1, 1, 0, 0, 0, DateTimeKind.Utc); // FFXIV launch era
            var maxDate = DateTime.UtcNow.AddDays(1); // Allow slight future dates for clock skew

            if (value.Value < minDate || value.Value > maxDate)
            {
                var message = customMessage ?? $"{propertyName} must be a reasonable timestamp";
                return ValidationResult.Failure(propertyName, message, value, "INVALID_TIMESTAMP");
            }

            return ValidationResult.Success();
        }

        #endregion

        #region Object Validation Extensions

        /// <summary>
        /// Validates that an object is not null
        /// </summary>
        public static ValidationResult NotNull<T>(this T? value, string propertyName, string? customMessage = null) where T : class
        {
            if (value == null)
            {
                var message = customMessage ?? $"{propertyName} is required";
                return ValidationResult.Failure(propertyName, message, value, "REQUIRED");
            }
            return ValidationResult.Success();
        }

        /// <summary>
        /// Validates an object using a custom validation function
        /// </summary>
        public static ValidationResult Custom<T>(this T value, string propertyName, Func<T, bool> predicate, string errorMessage, string? errorCode = null)
        {
            if (!predicate(value))
            {
                return ValidationResult.Failure(propertyName, errorMessage, value, errorCode);
            }
            return ValidationResult.Success();
        }

        #endregion

        #region Fluent Validation Builder

        /// <summary>
        /// Starts a fluent validation chain for a property
        /// </summary>
        public static PropertyValidator<T> ValidateProperty<T>(T value, string propertyName)
        {
            return new PropertyValidator<T>(value, propertyName);
        }

        #endregion
    }

    /// <summary>
    /// Fluent validation builder for chaining multiple validation rules
    /// </summary>
    public class PropertyValidator<T>
    {
        private readonly T _value;
        private readonly string _propertyName;
        private readonly List<ValidationError> _errors = new();

        internal PropertyValidator(T value, string propertyName)
        {
            _value = value;
            _propertyName = propertyName;
        }

        /// <summary>
        /// Adds a validation rule to the chain
        /// </summary>
        public PropertyValidator<T> Must(Func<T, bool> predicate, string errorMessage, string? errorCode = null)
        {
            if (!predicate(_value))
            {
                _errors.Add(new ValidationError(_propertyName, errorMessage, _value, errorCode));
            }
            return this;
        }

        /// <summary>
        /// Adds a validation rule with custom validation result
        /// </summary>
        public PropertyValidator<T> Must(Func<T, ValidationResult> validator)
        {
            var result = validator(_value);
            if (!result.IsValid)
            {
                _errors.AddRange(result.Errors);
            }
            return this;
        }

        /// <summary>
        /// Returns the final validation result
        /// </summary>
        public ValidationResult Build()
        {
            return _errors.Any() 
                ? ValidationResult.Failure(_errors) 
                : ValidationResult.Success();
        }

        /// <summary>
        /// Implicitly converts to ValidationResult
        /// </summary>
        public static implicit operator ValidationResult(PropertyValidator<T> validator)
        {
            return validator.Build();
        }
    }
}