namespace CoreApi.Infrastructure;

/// <summary>Requested AD object does not exist. Mapped to 404 by <see cref="ProblemDetailsExceptionHandler"/>.</summary>
public sealed class NotFoundException(string message) : Exception(message);

/// <summary>Requested AD object already exists. Mapped to 409 by <see cref="ProblemDetailsExceptionHandler"/>.</summary>
public sealed class ConflictException(string message) : Exception(message);

/// <summary>Request violates a business rule (e.g. a path outside the configured directory
/// scope). Mapped to 400 by <see cref="ProblemDetailsExceptionHandler"/>.</summary>
public sealed class InvalidRequestException(string message) : Exception(message);

/// <summary>A search would return more entries than DirectoryConnection:MaxSearchResults
/// allows. Mapped to 400 by <see cref="ProblemDetailsExceptionHandler"/> -- the caller should
/// narrow the query (e.g. a more specific ouPath), not retry as-is.</summary>
public sealed class SearchResultsLimitExceededException(string message) : Exception(message);
