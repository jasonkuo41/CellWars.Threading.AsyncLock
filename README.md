
# CellWars.Async
A re-entrant async lock for C#.

This library originally belongs to a private repository "CellWars" and is now made public, currently only provides `AsyncLock`, which is an async-compatible `lock` in C#.

Supports `netstandard 1.3` (.Net Framework 4.6, .Net Core 1.0 and above)

## AsyncLock
This is an async-compatible counterpart of `lock` in C#.

It not only provides the ability to lock an async-await block but also provides features like re-entrant; meaning you can re-acquire and lock the same lock again without suffering dead-lock or any other consequences, not even performance degradation.

You can read the `#Theory` for further understanding of how this is achieved.

### Quick Start
The using of AsyncLock is extremely straight forward, it takes advantage of `using` and unlocks the mutex when `Dispose()` is called.

```c#
public class ExampleClass {
    private readonly AsyncLock _mutex = new AsyncLock();
    public async Task LockContentAsync() {
        // This is equal to lock(_mutex) {}
        using(await _mutex.LockAsync()) {
            // It is safe to await when AsyncLock is locked
            // This garuntees only one thread is allow to access this block one at a time
            await Task.Delay(1000);
        }
    }
}
```
We suggest that for classes that utilizes `AsyncLock` should stop using `lock` all together, instead, use the synchronous API provided by `AsyncLock`.

```c#
public void LockContent() {
    using (_mutex.Lock()) {
        // Your work
    }
}
```
This garuantees both `LockContentAsnyc` and `LockContent` will only have one thread accessing it simultaneously.

Since the biggest feature of `AsyncLock` is that it behaves like `lock` which is re-entrant, meaning that the code below would not end up in a dead-lock (like `SempahoreSlim.WaitAsync` and other commonly used async lock provided by others).
```c#
public async Task ReentrantLockAsync() {
    using (await _mutex.Lock()) {
        using (await _mutex.Lock()) {
            // Does not dead lock
        }
    }
}
```


> **Notes** : Remember to Dispose `AsyncLock`  when it's belonging class no longer uses it; although it is pretty safe not disposing it in the current implementation, we still recommend doing a check.


### Timeout and Cancellation
`AsyncLock` also provides a timeout and `CancellationToken` parameter for its interface. When timeout is provided, the lock will only wait for that timespan before throwing `TimoutException`; If `CancellationToken` is canceled, this would cause `OperationCancelledException` to be thrown.

 If the mutex is already acquired, then cancellation from `CancellationToken` would not be effective.
If timeout is not specified, it would fallback to wait for the default timeout specified in the constructor of `AsyncLock`, if that is also not specified, then it would wait forever.

### Theory
This section will explain how `AsyncLock` manages to allow re-entrant and it's high performance.

`AsyncLock` utilizes two main classes `SemphoreSlim` and `AsyncLocal`; the actual lock is maintained by `SemphoreSlim` and the re-entrant check is done by using `AsyncLocal`.

When locking with `AsyncLock`, it returns an object implementing `ILockHandle`, which can either contain actual content that does the unlocking when `Dispose()` is called, or does absolutely nothing. The former one is acquired when the current Task has not acquired `AsyncLock`, the latter one is returned when `AsyncLock` detects when it already acquired the lock.

The detection of the current lock acquired by the current task is achieved through `AsyncLocal`, the underlying value of `AsyncLocal` is *(almost)* different when it's in different Task. When it's underlying value is null we know that the current Task hasn't acquired the lock and is put into the semaphore; if it's not null then we know the current Task has already acquired a lock given by this current `AsyncLock` instance, and would not await the semaphore and returns an empty `ILockHandle`.

If you like to know how `AsyncLocal` can distinguish between different Task's, you are welcome to read up the documents provided by [MSDN](https://docs.microsoft.com/en-us/dotnet/api/system.threading.asynclocal-1?redirectedfrom=MSDN&view=netframework-4.8) and [this SO post](https://stackoverflow.com/questions/31707362/how-do-the-semantics-of-asynclocal-differ-from-the-logical-call-context).

> **Warning** : Although it's unlikely to happen, you shall not use `AsyncLock` inside any blocks of where `ExecutionContext.SuppressFlow()` is present. This would effectively break the re-entrant check with `AsyncLocal`
