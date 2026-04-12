namespace Stratum.Application.Interfaces;

using MediatR;

/// <summary>
/// Implementation of ICommandBus using MediatR.
/// Provides a simplified interface for command dispatching.
/// </summary>
public sealed class CommandBus : ICommandBus
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the CommandBus class.
    /// </summary>
    /// <param name="mediator">The MediatR instance.</param>
    public CommandBus(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    /// <summary>
    /// Sends a command for handling.
    /// </summary>
    /// <typeparam name="TCommand">The type of command.</typeparam>
    /// <param name="command">The command to send.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SendAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default)
        where TCommand : ICommand
    {
        await _mediator.Send(command, cancellationToken);
    }

    /// <summary>
    /// Sends a command for handling and returns a result.
    /// </summary>
    /// <typeparam name="TCommand">The type of command.</typeparam>
    /// <typeparam name="TResult">The type of result.</typeparam>
    /// <param name="command">The command to send.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The result of the command handling.</returns>
    public async Task<TResult> SendAsync<TCommand, TResult>(TCommand command, CancellationToken cancellationToken = default)
        where TCommand : ICommand<TResult>
    {
        return await _mediator.Send(command, cancellationToken);
    }
}
