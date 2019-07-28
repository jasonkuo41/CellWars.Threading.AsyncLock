﻿using System;
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
            public void Dispose() {
                // Doing nothing since there's no lock to be release
            }
        }

        private class ThreadSafeHandle : ILockHandle {

            private readonly AsyncLock Source;
            private int isDiposed;

            public ThreadSafeHandle(AsyncLock source) {
                Source = source;
                Source._handle.Value = this;
            }

            public void Dispose() {
                if (Source._handle.Value == this && Interlocked.CompareExchange(ref isDiposed, 1, 0) == 0) {
                    Source._handle.Value = null;
                    Source._semaphore.Release();
                }
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
        /// Acquires the lock asynchronously, uses the default timeout specified in consturctor
        /// </summary>
        /// <returns>A one use handle for this lock, dispose it to unlock the lock</returns>
        /// <exception cref="OperationCanceledException"></exception>
        /// <exception cref="TimeoutException">If the specified time hits and is unable to acquire the lock</exception>
        public Task<ILockHandle> LockAsync() => LockAsync(null, default);

        /// <summary>
        /// Acquires the lock asynchronously, uses the default timeout specified in consturctor
        /// </summary>
        /// <param name="timeout">The timeout to this lock, default would fallback using DefaultTimeOut</param>
        /// <param name="ct"> The CancellationToken to cancel waiting </param>
        /// <returns>A one use handle for this lock, dispose it to unlock the lock</returns>
        /// <exception cref="OperationCanceledException"></exception>
        /// <exception cref="TimeoutException">If the specified time hits and is unable to acquire the lock</exception>
        public Task<ILockHandle> LockAsync(CancellationToken ct) => LockAsync(null, ct);

        /// <summary>
        /// Acquires the lock asynchronously, uses the default timeout specified in consturctor if not specified here
        /// </summary>
        /// <param name="timeout">The timeout to this lock, default would fallback using DefaultTimeOut</param>
        /// <param name="ct"> The CancellationToken to cancel waiting </param>
        /// <returns>A one use handle for this lock, dispose it to unlock the lock</returns>
        /// <exception cref="OperationCanceledException"></exception>
        /// <exception cref="TimeoutException">If the specified time hits and is unable to acquire the lock</exception>
        public Task<ILockHandle> LockAsync(TimeSpan? timeout, CancellationToken ct) {
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
            if (!await _semaphore.WaitAsync(timeout ?? DefaultTimeOut, ct).ConfigureAwait(false)) {
                throw new OperationCanceledException("Semaphore Timeout");
            }
            return handler;
        }

        /// <summary>
        /// Acquires the lock synchronously, uses the default timeout specified in consturctor
        /// </summary>
        /// <returns>A one use handle for this lock, dispose it to unlock the lock</returns>
        public ILockHandle Lock() => Lock(null, default);

        /// <summary>
        /// Acquires the lock synchronously, uses the default timeout specified in consturctor
        /// </summary>
        /// <param name="ct"> The CancellationToken to cancel waiting </param>
        /// <returns>A one use handle for this lock, dispose it to unlock the lock</returns>
        public ILockHandle Lock(CancellationToken ct) => Lock(null, ct);

        /// <summary>
        /// Acquires the lock synchronously, uses the default timeout specified in consturctor if not specified here
        /// </summary>
        /// <param name="timeout">The timeout to this lock, default value null would fallback using DefaultTimeOut</param>
        /// <param name="ct"> The CancellationToken to cancel waiting </param>
        /// <returns>A one use handle for this lock, dispose it to unlock the lock</returns>
        /// <exception cref="OperationCanceledException"></exception>
        /// <exception cref="TimeoutException">If the specified time hits and is unable to acquire the lock</exception>
        public ILockHandle Lock(TimeSpan? timeout, CancellationToken ct) {
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


}