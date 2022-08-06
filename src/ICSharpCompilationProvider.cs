using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
namespace Newdigate.MethodCallAnalysis.Core;

public interface ICSharpCompilationProvider {
    CSharpCompilation CompileCSharp(IEnumerable<string> sourceCodes, out IDictionary<SyntaxTree, CompilationUnitSyntax> roots);
}
