namespace PoC.Improved.Application.Cqrs;

/// <summary>
/// Full mediator surface. Extends ISender (Send) and is reserved for future additions
/// (e.g. IPublisher.Publish if notifications get added). Inject ISender if you only
/// need to send a request; reach for IMediator only when you need more.
/// </summary>
public interface IMediator : ISender
{
}
