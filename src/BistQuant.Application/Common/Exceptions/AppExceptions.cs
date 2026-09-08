namespace BistQuant.Application.Common.Exceptions;

public class AppException : Exception
{
    public string ErrorCode { get; }

    public AppException(string message, string errorCode = "INTERNAL_ERROR") : base(message)
    {
        ErrorCode = errorCode;
    }
}

public class NotFoundException : AppException
{
    public NotFoundException(string entityName, object key)
        : base($"{entityName} with key ({key}) was not found.", "NOT_FOUND") { }
}

public class ValidationException : AppException
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException(IDictionary<string, string[]> errors)
        : base("One or more validation failures have occurred.", "VALIDATION_ERROR")
    {
        Errors = errors;
    }
}

public class BusinessException : AppException
{
    public BusinessException(string message) : base(message, "BUSINESS_RULE_VIOLATION") { }
}
