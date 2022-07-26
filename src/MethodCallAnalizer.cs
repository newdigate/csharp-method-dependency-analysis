using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
namespace Newdigate.MethodCallAnalysis.Core;
public class MethodCallAnalizer : IMethodCallAnalyzer
{
    private readonly ICSharpCompilationProvider _csharpCompilationProvider;
    private readonly ISymbolFinder _symbolFinder;

    public MethodCallAnalizer(ICSharpCompilationProvider csharpCompilationProvider, ISymbolFinder symbolFinder)
    {
        _csharpCompilationProvider = csharpCompilationProvider;
        _symbolFinder = symbolFinder;
    }

    public IDictionary<ISymbol, IList<ISymbol>> AnalizeMethodCalls(string source)
    {
        return AnalizeMethodCalls(new [] { string.Empty }, s => source);
    }

    public IDictionary<ISymbol, IList<ISymbol>> AnalizeMethodCalls(
        IEnumerable<string> sourceIdentifiers, 
        Func<string, string> getSourceFromIdentifier)
    {
        Dictionary<string, string> sourceCodeByIdentifier = new Dictionary<string, string>();
        foreach (string sourceIdentifier in sourceIdentifiers)
        {
            sourceCodeByIdentifier.Add(sourceIdentifier, getSourceFromIdentifier(sourceIdentifier));
        }

        CSharpCompilation compilation =
            _csharpCompilationProvider
                .CompileCSharp(
                    sourceCodeByIdentifier.Values,
                    out IDictionary<SyntaxTree, CompilationUnitSyntax> trees);

        return AnalizeMethodCalls(compilation, trees);
    }

    public IDictionary<ISymbol, IList<ISymbol>> AnalizeMethodCalls(Compilation compilation, IDictionary<SyntaxTree, CompilationUnitSyntax> trees)
    {
        IEnumerable<ClassDeclarationSyntax> classDeclarationSyntaxes =
            trees
                .Keys
                .SelectMany(
                    cds => cds
                        .GetRoot()
                        .DescendantNodes()
                        .OfType<ClassDeclarationSyntax>()
                );

        IEnumerable<MethodDeclarationSyntax> methods = classDeclarationSyntaxes.SelectMany(cds => cds.DescendantNodes().OfType<MethodDeclarationSyntax>().Cast<MethodDeclarationSyntax>());
        IDictionary<ISymbol, IList<ISymbol>> dependencyTree = new Dictionary<ISymbol, IList<ISymbol>>();
        foreach (MethodDeclarationSyntax methodDeclaration in methods)
        {
            SemanticModel model = compilation.GetSemanticModel(methodDeclaration.SyntaxTree);

            ISymbol? methodSymbol = model.GetDeclaredSymbol(methodDeclaration);
            Console.WriteLine($"Method: {methodSymbol?.ToDisplayString()};");
            if (methodSymbol == null) continue;

            IList<ISymbol>? dependencies = null;
            if (dependencyTree.ContainsKey(methodSymbol))
            {
                dependencies = dependencyTree[methodSymbol];
            }
            else
            {
                dependencies = new List<ISymbol>();
                dependencyTree[methodSymbol] = dependencies;
            }

            IEnumerable<InvocationExpressionSyntax>? invocations = methodDeclaration?.Body?.DescendantNodes().OfType<InvocationExpressionSyntax>().Cast<InvocationExpressionSyntax>();
            if (invocations != null)
            {
                foreach (InvocationExpressionSyntax invocation in invocations)
                {
                    //Console.WriteLine($"\tInvocation: {invocation.ToString()};");
                    SymbolInfo? symbol = 
                         model.GetSymbolInfo(invocation.Expression); 

                    if (symbol.HasValue && symbol.Value.Symbol != null) {
                        if (!dependencies.Contains(symbol.Value.Symbol)) {
                            dependencies.Add(symbol.Value.Symbol);
                            //Console.WriteLine($"model.GetSymbolInfo({invocation.Expression}) returns {symbol.Value.Symbol}");
                        }
                    } else 
                    {
                        if (invocation.Expression is MemberAccessExpressionSyntax memberAccessExpressionSyntax) {
                            if (memberAccessExpressionSyntax.Expression is IdentifierNameSyntax memberIdentifierNameSyntax){
                                string mem = memberIdentifierNameSyntax.ToFullString();
                                SymbolInfo? identifierSymbol =  model.GetSymbolInfo(memberIdentifierNameSyntax);
                            }
                        } else {
                            ISymbol? symbol2 = _symbolFinder.Find(invocation.Expression); 
                            if (symbol2 != null)
                            {
                                if (!dependencies.Contains(symbol2)) {
                                    dependencies.Add(symbol2);
                                    //Console.WriteLine($"model.GetDeclaredSymbol({invocation.Expression}) returns {symbol2}");
                                }                  
                            } else 
                                Console.WriteLine($"could not find symbol for {invocation.Expression}...");
                        }
                        /*
                        INamespaceSymbol? currentNamespace = compilation.GlobalNamespace;
                        string[] tokens = invocation.Expression.ToFullString().Split(".");
                        bool found = false;
                        int i = 0;
                        while (currentNamespace != null) {
                            currentNamespace = currentNamespace.GetNamespaceMembers().FirstOrDefault( n => n.Name == tokens[i]);
                            i ++;
                            
                        }
                        */
                    }                
                }
            }
        }

        return dependencyTree;
    }


    private ITypeSymbol? GetEnumerationType(ITypeSymbol typeSymbol) {
        if (typeSymbol is INamedTypeSymbol returnTypeNamedTypeSymbol){
            if (returnTypeNamedTypeSymbol.ConstructedFrom.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>")
            {
                return returnTypeNamedTypeSymbol.TypeArguments[0];
            }
        }
        INamedTypeSymbol? ienumerable = typeSymbol.AllInterfaces.FirstOrDefault( i => i.ConstructedFrom?.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>");
        if (ienumerable != null) {
            return ienumerable.TypeArguments[0];
        }
       
        return null;
    }


}
