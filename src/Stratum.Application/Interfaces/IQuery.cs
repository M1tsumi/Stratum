namespace Stratum.Application.Interfaces;

using MediatR;

/// <summary>
/// Marker interface for queries in the CQRS pattern.
/// Queries represent requests that retrieve data and do not change state.
/// </summary>
/// <typeparam name="TResult">The type of result returned by the query.</typeparam>
public interface IQuery<TResult> : IRequest<TResult>
{
}
