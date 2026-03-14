namespace Ruvarr.Abstractions;

public interface IRequestHandler<TRequest>
{
    Task<RuvarrResult> Handle(TRequest request, CancellationToken cancellationToken);
}

public interface IRequestHandler<TRequest, TResponse>
{
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}

public interface IStreamingRequestHandler<TRequest, out TResponse>
{
    IAsyncEnumerable<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}