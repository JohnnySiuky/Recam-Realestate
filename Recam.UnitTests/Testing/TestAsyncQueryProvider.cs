// Recam.UnitTests/Testing/TestAsyncQueryProvider.cs
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Query; // IAsyncQueryProvider

namespace Recam.UnitTests.Testing
{
    public static class QueryableAsyncExtensions
    {
        public static IQueryable<T> ToAsyncQueryable<T>(this IEnumerable<T> source)
            => new TestAsyncEnumerable<T>(source);
    }

    internal sealed class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
    {
        public TestAsyncEnumerable(IEnumerable<T> enumerable) : base(enumerable) { }
        public TestAsyncEnumerable(Expression expression) : base(expression) { }

        // ✅ 关键修正：通过 (IQueryable)this 访问显式接口实现的 Expression，
        // 再用新的 EnumerableQuery<T>(expr) 包一层，最后取 Provider。
        IQueryProvider IQueryable.Provider =>
            new TestAsyncQueryProvider<T>(
                ((IQueryable)new EnumerableQuery<T>(((IQueryable)this).Expression)).Provider
            );

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            => new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());
    }

    internal sealed class TestAsyncEnumerator<T> : IAsyncEnumerator<T>
    {
        private readonly IEnumerator<T> _inner;
        public TestAsyncEnumerator(IEnumerator<T> inner) => _inner = inner;

        public T Current => _inner.Current;

        public ValueTask DisposeAsync()
        {
            _inner.Dispose();
            return default;
        }

        public ValueTask<bool> MoveNextAsync() => new(_inner.MoveNext());
    }

    internal sealed class TestAsyncQueryProvider<T> : IAsyncQueryProvider
    {
        private readonly IQueryProvider _inner;
        public TestAsyncQueryProvider(IQueryProvider inner) => _inner = inner;

        public IQueryable CreateQuery(Expression expression)
        {
            var sanitized = EfNoOpExtensionStripper.Strip(expression);
            var elementType = sanitized.Type.GetInterfaces()
                                   .Concat(new[] { sanitized.Type })
                                   .Where(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IQueryable<>))
                                   .Select(t => t.GetGenericArguments()[0])
                                   .FirstOrDefault() ?? typeof(object);

            var testEnumType = typeof(TestAsyncEnumerable<>).MakeGenericType(elementType);
            return (IQueryable)Activator.CreateInstance(testEnumType, sanitized)!;
        }

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
            => new TestAsyncEnumerable<TElement>(EfNoOpExtensionStripper.Strip(expression));
        
        [return: MaybeNull]
        public object Execute(Expression expression)
            => _inner.Execute(EfNoOpExtensionStripper.Strip(expression));

        public TResult Execute<TResult>(Expression expression)
            => _inner.Execute<TResult>(EfNoOpExtensionStripper.Strip(expression));

        // 支持 IAsyncEnumerable<TResult> 路径（用于 ToListAsync/AnyAsync 的内部）
        public IAsyncEnumerable<TResult> ExecuteAsync<TResult>(Expression expression)
            => new TestAsyncEnumerable<TResult>(EfNoOpExtensionStripper.Strip(expression));

        // 支持 Task<TResult>/Task 路径（SingleOrDefaultAsync/AnyAsync 等）
        public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken)
        {
            var sanitized = EfNoOpExtensionStripper.Strip(expression);
            var resultType = typeof(TResult);

            // Task<T>
            if (resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var innerType = resultType.GetGenericArguments()[0];

                var execGeneric = typeof(IQueryProvider).GetMethods()
                    .First(m => m.Name == nameof(IQueryProvider.Execute) && m.IsGenericMethod)
                    .MakeGenericMethod(innerType);

                var execResult = execGeneric.Invoke(_inner, new object[] { sanitized });

                var fromResult = typeof(Task).GetMethods()
                    .First(m => m.Name == nameof(Task.FromResult) && m.IsGenericMethod)
                    .MakeGenericMethod(innerType);

                var taskObj = fromResult.Invoke(null, new[] { execResult }); // 允许 null
                return (TResult)taskObj!;
            }

            // Task（非泛型，几乎不会用到，但保留）
            if (resultType == typeof(Task))
            {
                _inner.Execute(sanitized);
                return (TResult)(object)Task.CompletedTask;
            }

            // 直接 TResult
            return _inner.Execute<TResult>(sanitized);
        }
    }

    /// <summary>
    /// 把 EF 的扩展（AsNoTracking/Include/ThenInclude/...）在表达式里直接“剥掉”，
    /// 避免在内存 LINQ 提供器上抛 ArgumentException 或导致递归。
    /// </summary>
    internal sealed class EfNoOpExtensionStripper : ExpressionVisitor
    {
        private static readonly HashSet<string> Methods = new(StringComparer.Ordinal)
        {
            "AsNoTracking",
            "AsNoTrackingWithIdentityResolution",
            "Include",
            "ThenInclude",
            "AsSplitQuery",
            "AsSingleQuery",
            "IgnoreQueryFilters",
            "AsTracking",
            "TagWith",
            "TagWithCallSite"
        };

        public static Expression Strip(Expression expression) => new EfNoOpExtensionStripper().Visit(expression);

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            var decl = node.Method.DeclaringType;
            if (decl != null &&
                decl.FullName == "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions" &&
                Methods.Contains(node.Method.Name))
            {
                // 直接返回源 IQueryable
                return Visit(node.Arguments[0]);
            }

            return base.VisitMethodCall(node);
        }
    }
}