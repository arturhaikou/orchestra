using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.DependencyInjection;

namespace Orchestra.Infrastructure.Tools;

/// <summary>
/// DispatchProxy that creates a fresh DI scope for every method invocation.
/// Prevents AppDbContext concurrency errors when the agent framework invokes
/// multiple tools in parallel — each tool call gets its own scope and DbContext.
/// </summary>
internal class ScopedToolProxy : DispatchProxy
{
    private IServiceScopeFactory _scopeFactory = null!;
    private Type _serviceType = null!;

    private static readonly MethodInfo _createProxyMethod =
        typeof(ScopedToolProxy).GetMethod(
            nameof(CreateTypedProxy),
            BindingFlags.NonPublic | BindingFlags.Static)!;

    internal static object Create(IServiceScopeFactory scopeFactory, Type serviceType)
    {
        var proxy = (ScopedToolProxy)_createProxyMethod
            .MakeGenericMethod(serviceType)
            .Invoke(null, null)!;
        proxy._scopeFactory = scopeFactory;
        proxy._serviceType = serviceType;
        return proxy;
    }

    private static T CreateTypedProxy<T>() where T : class
        => DispatchProxy.Create<T, ScopedToolProxy>();

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod == null) return null;

        var scope = _scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService(_serviceType);

        try
        {
            var result = targetMethod.Invoke(service, args);
            if (result is Task task)
                return WrapTask(task, scope, targetMethod.ReturnType);

            scope.DisposeAsync().AsTask().GetAwaiter().GetResult();
            return result;
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            scope.DisposeAsync().AsTask().GetAwaiter().GetResult();
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
        catch
        {
            scope.DisposeAsync().AsTask().GetAwaiter().GetResult();
            throw;
        }
    }

    private static object WrapTask(Task task, AsyncServiceScope scope, Type returnType)
    {
        if (!returnType.IsGenericType)
            return WrapVoidAsync(task, scope);

        var innerType = returnType.GetGenericArguments()[0];
        return typeof(ScopedToolProxy)
            .GetMethod(nameof(WrapTypedAsync), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(innerType)
            .Invoke(null, [task, scope])!;
    }

    private static async Task WrapVoidAsync(Task task, AsyncServiceScope scope)
    {
        try { await task; }
        finally { await scope.DisposeAsync(); }
    }

    private static async Task<T> WrapTypedAsync<T>(Task<T> task, AsyncServiceScope scope)
    {
        try { return await task; }
        finally { await scope.DisposeAsync(); }
    }
}
