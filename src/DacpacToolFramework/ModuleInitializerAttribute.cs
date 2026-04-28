/// <summary>
/// ModuleInitializerAttribute is not available in .NET Framework, so we define it ourselves for compatibility.
/// Fix found at: https://github.com/thomhurst/TUnit/issues/3731#issuecomment-3654853871
/// </summary>
namespace System.Runtime.CompilerServices
{
    #if !NET
    [Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1018:Specify AttributeUsage on ModuleInitializerAttribute")]    
    internal sealed class ModuleInitializerAttribute : Attribute { }
    #endif    
}
