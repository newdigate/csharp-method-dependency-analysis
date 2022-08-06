using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
namespace Newdigate.MethodCallAnalysis.Core;

public interface IMethodCallAnalyzer {
    IDictionary<ISymbol, IList<ISymbol>> AnalizeMethodCalls(string source);


    IDictionary<ISymbol, IList<ISymbol>> AnalizeMethodCalls(Compilation compilation, IDictionary<SyntaxTree, CompilationUnitSyntax> trees);
    

    IDictionary<ISymbol, IList<ISymbol>> AnalizeMethodCalls(
        IEnumerable<string> sourceIdentifiers, 
        Func<string, string> getSourceFromIdentifier);
}
