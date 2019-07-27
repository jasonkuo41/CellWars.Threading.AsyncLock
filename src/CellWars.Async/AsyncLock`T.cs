using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace CellWars.Async {

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
        /// <typeparam name="TValue">The fields type</typeparam>
        /// <param name="field">The acquiring field</param>
        /// <returns>The field's value</returns>
        public TValue AcquireField<TValue>(Func<T, TValue> field) => AcquireField(field, null, default);

        /// <summary>
        /// A helper function to acquire a field within the class with thread-safety concerned
        /// </summary>
        /// <typeparam name="TValue">The fields type</typeparam>
        /// <param name="field">The acquiring field</param>
        /// <param name="ct">The CancellationToken to cancel waiting</param>
        /// <returns>The field's value</returns>
        public TValue AcquireField<TValue>(Func<T, TValue> field, CancellationToken ct) => AcquireField(field, null, ct);

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
        public TValue AcquireField<TValue>(Func<T, TValue> field, TimeSpan? span, CancellationToken ct) {
            using (Lock(span, ct))
                return field(_holder);
        }

        /// <summary>
        /// A helper function to write or set a field within a class with thread-safety concerned
        /// </summary>
        /// <typeparam name="TValue">The fields type</typeparam>
        /// <param name="field">The acquiring field</param>
        /// <param name="value">The operation on to the field</param>
        public void SetField<TValue>(Func<T, TValue> field, Action<TValue> value) => SetField(field, value, null);

        /// <summary>
        /// A helper function to write or set a field within a class with thread-safety concerned
        /// </summary>
        /// <typeparam name="TValue">The fields type</typeparam>
        /// <param name="field">The acquiring field</param>
        /// <param name="value">The operation on to the field</param>
        /// <param name="span">The timeout to this lock, default value null would fallback using DefaultTimeOut</param>
        public void SetField<TValue>(Func<T, TValue> field, Action<TValue> value, TimeSpan? span) => SetField(field, value, span, default);

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
        public void SetField<TValue>(Func<T, TValue> field, Action<TValue> value, TimeSpan? span, CancellationToken ct) {
            using (Lock(span, ct))
                value(field(_holder));
        }
    }
}
