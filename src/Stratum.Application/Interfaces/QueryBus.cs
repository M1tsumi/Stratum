namespace Stratum.Application.Interfaces;

using MediatR;

/// <summary>
/// Implementation of IQueryBus using MediatR.
/// Provides a simplified interface for query dispatching.
/// </summary>
public sealed class QueryBus : IQueryBus
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the QueryBus class.
    /// </summary>
    /// <param name="mediator">The MediatR instance.</param>
    public QueryBus(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    /// <summary>
    /// Sends a query for handling and returns the result.
    /// </summary>
    /// <typeparam name="TQuery">The type of query.</typeparam>
    /// <typeparam name="TResult">The type of result.</typeparam>
    /// <param name="query">The query to send.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The result of the query handling.</returns>
    public async Task<TResult> SendAsync<TQuery, TResult>(TQuery query, CancellationToken cancellationToken = default)
        where TQuery : IQuery<TResult>
    {
        return await _mediator.Send(query, cancellationToken);
    }
}
