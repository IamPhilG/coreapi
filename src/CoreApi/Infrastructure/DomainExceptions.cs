namespace CoreApi.Infrastructure;

/// <summary>Requested AD object does not exist. Mapped to 404 by <see cref="ProblemDetailsExceptionHandler"/>.</summary>
public sealed class NotFoundException(string message) : Exception(message);

/// <summary>Requested AD object already exists. Mapped to 409 by <see cref="ProblemDetailsExceptionHandler"/>.</summary>
public sealed class ConflictException(string message) : Exception(message);
