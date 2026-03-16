namespace Ruvarr.Abstractions;

#pragma warning disable S2326 // Type parameter is used as a marker for type discrimination
internal sealed record QueueChangedEvent<TItem> where TItem : class;
#pragma warning restore S2326
