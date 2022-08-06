using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
namespace Newdigate.MethodCallAnalysis.Core;

public interface IClassDependencyAnalyzer {
    IDictionary<ISymbol, IDictionary<ISymbol, IList<ISymbol>>> AnalizeClassCalls(string source);


    IDictionary<ISymbol, IDictionary<ISymbol, IList<ISymbol>>> AnalizeClassCalls(Compilation compilation, IDictionary<SyntaxTree, CompilationUnitSyntax> trees);
    

    IDictionary<ISymbol, IDictionary<ISymbol, IList<ISymbol>>> AnalizeClassCalls(
        IEnumerable<string> sourceIdentifiers, 
        Func<string, string> getSourceFromIdentifier);
}
