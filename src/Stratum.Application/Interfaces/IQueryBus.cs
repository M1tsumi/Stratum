namespace Stratum.Application.Interfaces;

using MediatR;

/// <summary>
/// Provides a simplified abstraction over MediatR for query dispatching.
/// </summary>
public interface IQueryBus
{
    /// <summary>
    /// Sends a query for handling and returns the result.
    /// </summary>
    /// <typeparam name="TQuery">The type of query.</typeparam>
    /// <typeparam name="TResult">The type of result.</typeparam>
    /// <param name="query">The query to send.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The result of the query handling.</returns>
    Task<TResult> SendAsync<TQuery, TResult>(TQuery query, CancellationToken cancellationToken = default)
        where TQuery : IQuery<TResult>;
}
