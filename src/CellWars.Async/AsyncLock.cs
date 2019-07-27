using System;
using System.Threading;
using System.Threading.Tasks;

namespace CellWars.Threading {
    /// <summary>
    /// A class that allows locking in an async-await block and also providing re-entrant ability
    /// </summary>
    public class AsyncLock : IDisposable {
        /// <summary>
        /// Provides a handle for the lock, unlocks the lock when it is disposed
        /// </summary>
        public interface ILockHandle : IDisposable { }

        private class EmptyHandle : ILockHandle {
            public void Dispose() { }
        }

        private class ThreadSafeHandle : ILockHandle {

            private readonly AsyncLock Source;
            private int isDiposed = 0;

            public ThreadSafeHandle(AsyncLock source) {
                Source = source;
                Source._handle.Value = this;
            }

            public void Dispose() {
                if (Interlocked.CompareExchange(ref isDiposed, 1, 0) != 0)
                    throw new ObjectDisposedException("Handle is already disposed");
                if (Source._handle.Value != this)
                    throw new InvalidOperationException("You cannot dispose this handle when executing other Task.");

                Source._handle.Value = null;
                Source._semaphore.Release();
            }
        }

        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private readonly AsyncLocal<ILockHandle> _handle = new AsyncLocal<ILockHandle>();
        private static readonly ILockHandle EmptyHandler = new EmptyHandle();

        /// <summary>
        /// The Default TimeOut value specified in the constructor (Infinite if not specified)
        /// </summary>
        public TimeSpan DefaultTimeOut { get; } = Timeout.InfiniteTimeSpan;

        /// <summary>
        /// Create a async-lock and set it's default timeout to infinite
        /// </summary>
        public AsyncLock() { }

        /// <summary>
        /// Create a async-lock specifying it's default timeout
        /// </summary>
        /// <param name="defaultTimeout"> The default timeout value </param>
        public AsyncLock(TimeSpan defaultTimeout) {
            DefaultTimeOut = defaultTimeout;
        }

        /// <summary>
        /// Acquires the lock asynchronously, uses the default timeout specified in consturctor if not specified here
        /// </summary>
        /// <param name="timeout">The timeout to this lock, default would fallback using DefaultTimeOut</param>
        /// <param name="ct"> The CancellationToken to cancel waiting </param>
        /// <returns>A one use handle for this lock, dispose it to unlock the lock</returns>
        /// <exception cref="OperationCanceledException"></exception>
        /// <exception cref="TimeoutException">If the specified time hits and is unable to acquire the lock</exception>
        public Task<ILockHandle> LockAsync(TimeSpan? timeout = null, CancellationToken ct = default) {
            // Checks if current handle is null
            if (_handle.Value == null) {
                return InternalEnterAsync(timeout, new ThreadSafeHandle(this), ct);
            }
            // If the CurrentHandle is not null, return a empty hanlder enabling re-entrant by spec
            return Task.FromResult(EmptyHandler);
        }

        // We need this function because if AsyncLocal Enters a async function, it's altered value cannot be 
        // visible from it's parent caller.
        private async Task<ILockHandle> InternalEnterAsync(TimeSpan? timeout, ILockHandle handler, CancellationToken ct) {
            if (!await _semaphore.WaitAsync(timeout ?? DefaultTimeOut, ct))
                throw new OperationCanceledException("Semaphore Timeout");
            return handler;
        }


        /// <summary>
        /// Acquires the lock synchronously, uses the default timeout specified in consturctor if not specified here
        /// </summary>
        /// <param name="timeout">The timeout to this lock, default value null would fallback using DefaultTimeOut</param>
        /// <param name="ct"> The CancellationToken to cancel waiting </param>
        /// <returns>A one use handle for this lock, dispose it to unlock the lock</returns>
        /// <exception cref="OperationCanceledException"></exception>
        /// <exception cref="TimeoutException">If the specified time hits and is unable to acquire the lock</exception>
        public ILockHandle Lock(TimeSpan? timeout = null, CancellationToken ct = default) {
            if (_handle.Value == null) {
                _semaphore.Wait(timeout ?? DefaultTimeOut, ct);
                return new ThreadSafeHandle(this);
            }
            return EmptyHandler;
        }

        public void Dispose() {
            _semaphore.Dispose();
        }

    }


    /// <summary>
    /// A class that allows locking in an async-await block and also providing re-entrant ability, with some helper functions
    /// </summary>
    public class AsyncLock<T> : AsyncLock {

        private readonly T _holder;

        /// <summary>
        /// Create a async-lock and set it's default timeout to infinite
        /// </summary>
        public AsyncLock(T impl) {
            _holder = impl;
        }


        /// <summary>
        /// Create a async-lock specifying it's default timeout
        /// </summary>
        /// <param name="defaultTimeout"> The default timeout value </param>
        public AsyncLock(T impl, TimeSpan defaultTimeout) : base(defaultTimeout) {
            _holder = impl;
        }

        /// <summary>
        /// A helper function to acquire a field within the class with thread-safety concerned
        /// </summary>
        /// <example>
        /// This is commonly used for tidier get property
        /// <code>
        /// public string Foo => AcquireField(x => x.foo);
        /// </code>
        /// </example>
        /// <typeparam name="TValue">The fields type</typeparam>
        /// <param name="field">The acquiring field</param>
        /// <param name="span">The timeout to this lock, default value null would fallback using DefaultTimeOut</param>
        /// <param name="ct">The CancellationToken to cancel waiting</param>
        /// <returns>The field's value</returns>
        public TValue AcquireField<TValue>(Func<T, TValue> field, TimeSpan? span = null, CancellationToken ct = default) {
            using (Lock(span, ct))
                return field(_holder);
        }


        /// <summary>
        /// A helper function to write or set a field within a class with thread-safety concerned
        /// </summary>
        /// <example>
        /// This is commonly used for tidier set property
        /// <code>
        /// public string Foo {
        ///     set => SetField(x => x.foo, foo => foo = value);
        /// }
        /// </code>
        /// </example>
        /// <typeparam name="TValue">The fields type</typeparam>
        /// <param name="field">The acquiring field</param>
        /// <param name="value">The operation on to the field</param>
        /// <param name="span">The timeout to this lock, default value null would fallback using DefaultTimeOut</param>
        /// <param name="ct">The CancellationToken to cancel waiting</param>
        /// <returns>The field's value</returns>
        public void SetField<TValue>(Func<T, TValue> field, Action<TValue> value, TimeSpan? span = null, CancellationToken ct = default) {
            using (Lock(span, ct))
                value(field(_holder));
        }
    }
}
