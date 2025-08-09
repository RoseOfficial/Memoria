using System;

namespace AlphaScope.API.Client
{
    /// <summary>
    /// Represents the result of an operation that can either succeed or fail
    /// </summary>
    /// <typeparam name="T">The type of the success value</typeparam>
    public class Result<T>
    {
        /// <summary>
        /// Gets a value indicating whether the operation was successful
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// Gets a value indicating whether the operation failed
        /// </summary>
        public bool IsFailure => !IsSuccess;

        /// <summary>
        /// Gets the success value (only valid when IsSuccess is true)
        /// </summary>
        public T? Value { get; }

        /// <summary>
        /// Gets the error message (only valid when IsSuccess is false)  
        /// </summary>
        public string? Error { get; }

        /// <summary>
        /// Gets the exception that caused the failure (optional)
        /// </summary>
        public Exception? Exception { get; }

        private Result(T value)
        {
            IsSuccess = true;
            Value = value;
            Error = null;
            Exception = null;
        }

        private Result(string error, Exception? exception = null)
        {
            IsSuccess = false;
            Value = default(T);
            Error = error ?? throw new ArgumentNullException(nameof(error));
            Exception = exception;
        }

        /// <summary>
        /// Creates a successful result with the specified value
        /// </summary>
        /// <param name="value">The success value</param>
        /// <returns>A successful result</returns>
        public static Result<T> Success(T value) => new Result<T>(value);

        /// <summary>
        /// Creates a failed result with the specified error message
        /// </summary>
        /// <param name="error">The error message</param>
        /// <returns>A failed result</returns>
        public static Result<T> Failure(string error) => new Result<T>(error);

        /// <summary>
        /// Creates a failed result with the specified error message and exception
        /// </summary>
        /// <param name="error">The error message</param>
        /// <param name="exception">The exception that caused the failure</param>
        /// <returns>A failed result</returns>
        public static Result<T> Failure(string error, Exception? exception) => new Result<T>(error, exception);

        /// <summary>
        /// Creates a failed result from an exception
        /// </summary>
        /// <param name="exception">The exception that caused the failure</param>
        /// <returns>A failed result</returns>
        public static Result<T> Failure(Exception exception) => new Result<T>(exception.Message, exception);

        /// <summary>
        /// Implicitly converts a value to a successful result
        /// </summary>
        /// <param name="value">The value</param>
        public static implicit operator Result<T>(T value) => Success(value);

        /// <summary>
        /// Gets the value or throws an exception if the result is a failure
        /// </summary>
        /// <returns>The success value</returns>
        /// <exception cref="InvalidOperationException">Thrown when the result is a failure</exception>
        public T GetValueOrThrow()
        {
            if (IsFailure)
            {
                throw new InvalidOperationException($"Cannot get value from failed result: {Error}", Exception);
            }
            return Value!;
        }

        /// <summary>
        /// Gets the value or returns the default value if the result is a failure
        /// </summary>
        /// <param name="defaultValue">The default value to return on failure</param>
        /// <returns>The success value or the default value</returns>
        public T? GetValueOrDefault(T? defaultValue = default(T))
        {
            return IsSuccess ? Value : defaultValue;
        }
    }

    /// <summary>
    /// Represents the result of an operation that can either succeed or fail (without a return value)
    /// </summary>
    public class Result
    {
        /// <summary>
        /// Gets a value indicating whether the operation was successful
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// Gets a value indicating whether the operation failed
        /// </summary>
        public bool IsFailure => !IsSuccess;

        /// <summary>
        /// Gets the error message (only valid when IsSuccess is false)
        /// </summary>
        public string? Error { get; }

        /// <summary>
        /// Gets the exception that caused the failure (optional)
        /// </summary>
        public Exception? Exception { get; }

        private Result(bool isSuccess, string? error = null, Exception? exception = null)
        {
            IsSuccess = isSuccess;
            Error = error;
            Exception = exception;
        }

        /// <summary>
        /// Creates a successful result
        /// </summary>
        /// <returns>A successful result</returns>
        public static Result Success() => new Result(true);

        /// <summary>
        /// Creates a failed result with the specified error message
        /// </summary>
        /// <param name="error">The error message</param>
        /// <returns>A failed result</returns>
        public static Result Failure(string error) => new Result(false, error);

        /// <summary>
        /// Creates a failed result with the specified error message and exception
        /// </summary>
        /// <param name="error">The error message</param>
        /// <param name="exception">The exception that caused the failure</param>
        /// <returns>A failed result</returns>
        public static Result Failure(string error, Exception? exception) => new Result(false, error, exception);

        /// <summary>
        /// Creates a failed result from an exception
        /// </summary>
        /// <param name="exception">The exception that caused the failure</param>
        /// <returns>A failed result</returns>
        public static Result Failure(Exception exception) => new Result(false, exception.Message, exception);
    }
}