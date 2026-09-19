namespace System.Runtime.CompilerServices;

/// <summary>
/// Compiler shim enabling <c>init</c> accessors. The type ships in .NET 5+ but not in
/// netstandard2.1, which analyzers must target.
/// </summary>
internal static class IsExternalInit
{
}
