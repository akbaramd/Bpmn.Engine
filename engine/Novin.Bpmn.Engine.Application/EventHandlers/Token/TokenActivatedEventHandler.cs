// Application/EventHandlers/TokenActivatedEventHandler.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Commands.NodeDispatch;
using Novin.Bpmn.Engine.Application.Commands.NodeInstances;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Services; // TokenProcessResult
using Novin.Bpmn.Engine.Domain.Entities;      // TokenState
using Novin.Bpmn.Engine.Domain.Events;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.EventHandlers;

public sealed class TokenActivatedEventHandler : INotificationHandler<TokenActivatedEvent>
{
    private readonly IUnitOfWork _uow;
    private readonly IMediator _mediator;
    private readonly ILogger<TokenActivatedEventHandler> _logger;

    public TokenActivatedEventHandler(
        IUnitOfWork uow,
        IMediator mediator,
        ILogger<TokenActivatedEventHandler> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(TokenActivatedEvent notification, CancellationToken ct)
    {
       
    }
}
