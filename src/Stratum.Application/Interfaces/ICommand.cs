namespace Stratum.Application.Interfaces;

using MediatR;

/// <summary>
/// Marker interface for commands in the CQRS pattern.
/// Commands represent requests that change state and have no return value.
/// </summary>
public interface ICommand : IRequest
{
}

/// <summary>
/// Marker interface for commands that return a result.
/// </summary>
/// <typeparam name="TResult">The type of result returned by the command.</typeparam>
public interface ICommand<TResult> : IRequest<TResult>
{
}
