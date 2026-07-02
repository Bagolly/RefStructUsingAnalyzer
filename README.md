# RefStructUsing
A simple analyzer that flags disposable ref structs declared without a 'using' statement or block. 

### Why it exists

The CLR allows deterministic cleanup, which C# exposes.
For reference types, implementing `IDisposable` is a requirement to use the type in a `using` statement.
For value types, while a `struct` still can't be disposable, a `ref struct` can. 

Before C# 14 added interface support for `ref struct` types, this was made possible through duck-typing; any `ref struct` that has a method with an appropriate signature,
can be used in `using` statements.

Some disposable types _may_ have a finalizer (depending on what they do), which _may_ save you by running _eventually_, but it's not a requirement.
Even still, finalizers can only be added to `class` and `record` types (although the [documentation](https://learn.microsoft.com/en-us/dotnet/csharp/misc/cs0575) seems to omit the latter), 
so forgetting to call `Dispose()` on a `ref struct` is a guaranteed resource leak. 

### What it's used for

I made this analyzer with a very specific use case in mind.
The restrictions on ref structs make them desirable to block-scoped temporary allocations, similar in concept to RAII in C++. On scope exit, the `Dispose()` method can run cleanup.
One usecase for this is `SpanOwner<T>` from the [CommunityToolkit.HighPerformance](https://www.nuget.org/packages/CommunityToolkit.HighPerformance) NuGet package released by the .NET Foundation. 

Since `SpanOwner` holds a reference to an array rented from an `ArrayPool`, not disposing of a `SpanOwner` means the rented buffer is never returned to the pool. At the same time, it is very easy to do `var a = SpanOwner<int>.Allocate(...);` (ask me how I know) and no warnings will be issued. This is where the analyzer comes in; it will issue a warning if you forget to add `using`.

### Limitations (or not)

The analyzer is somewhat basic. It detects `using` in either block or statement form (including delayed instantiation blocks), and will only warn if the `Dispose()` method has the exact
required signature.

However, it won't detect manual cleanups with an explicit call to `Dispose()`, or other methods that eventually calls `Dispose()`. It's not infeasible, but it's also a sign of potential misuse.

Having to call `Dispose()` manually implies that the variable leaves its current scope, but passing around types like `SpanOwner` is almost always a bad idea. This is the main reason why `ValueStringBuilder` (an internal type in the BCL) hasn't been exposed publicly, despite requests to do so. If you forget to pass it by reference, whatever handles you have get copied over,
and `Dispose()` will run as many times as you have copies. 

While `SpanOwner` isn't dangerous, it will still leak memory if misused. Another reason is that you lose control over the value's lifetime when you pass it around:
```csharp
var a = SpanOwner<int>.Allocate(8);
SetFirst(a);
a.Span[0] = 999; // Memory safe version of a use-after-free.
var b = SpanOwner<int>.Allocate(8);
Console.WriteLine(b.Span[0]); // Prints 999

static void SetFirst(SpanOwner<int> a)
{   
    using(a) { _ = 0; }
}
```

The analyzer also doesn't flag potentially dangerous default initializations (see below), as it depends entirely on what `Dispose()` does.
```csharp
static void DoNotDoThis()
{
   using SpanOwner<int> a = default; // Throws a NullReferenceException on scope exit.
}
```
