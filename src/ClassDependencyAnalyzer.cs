using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
namespace type_deinference;

public class ClassDependencyAnalyzer : IClassDependencyAnalyzer
{
    private readonly ICSharpCompilationProvider _csharpCompilationProvider;
    private readonly IMethodCallAnalyzer _methodCallAnalyser;
    private readonly ISymbolCache _symbolCache;

    public ClassDependencyAnalyzer(ICSharpCompilationProvider csharpCompilationProvider, IMethodCallAnalyzer methodCallAnalyser, ISymbolCache symbolCache)
    {
        _csharpCompilationProvider = csharpCompilationProvider;
        _methodCallAnalyser = methodCallAnalyser;
        _symbolCache = symbolCache;
    }

    public IDictionary<ISymbol, IDictionary<ISymbol, IList<ISymbol>>> AnalizeClassCalls(string source)
    {
        return AnalizeClassCalls(new [] { string.Empty }, s => source);
    }

    public IDictionary<ISymbol, IDictionary<ISymbol, IList<ISymbol>>> AnalizeClassCalls(
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

        return AnalizeClassCalls(compilation, trees);
    }

    public IDictionary<ISymbol, IDictionary<ISymbol, IList<ISymbol>>> AnalizeClassCalls(Compilation compilation, IDictionary<SyntaxTree, CompilationUnitSyntax> trees)
    {
        IEnumerable<ClassDeclarationSyntax> classDeclarationSyntaxes =
            trees
                .Keys
                .SelectMany(
                    cds => cds
                        .GetRoot()
                        .DescendantNodes()
                        .OfType<ClassDeclarationSyntax>());     

        IDictionary<ISymbol, IList<ISymbol>> dependencyTree = 
            _methodCallAnalyser
                .AnalizeMethodCalls(compilation, trees);

        IDictionary<ISymbol, IDictionary<ISymbol, IList<ISymbol>>> result = 
            new Dictionary<ISymbol, IDictionary<ISymbol, IList<ISymbol>>>();

        foreach (ClassDeclarationSyntax classDeclaration in classDeclarationSyntaxes)
        {
            SemanticModel model = compilation.GetSemanticModel(classDeclaration.SyntaxTree);

            ISymbol? classSymbol = model.GetDeclaredSymbol(classDeclaration);
            if (classSymbol == null) continue;

            _symbolCache.Add(classSymbol);

            IDictionary<ISymbol, IList<ISymbol>> methodSymbolReferencesPerClass = null;
            if (result.ContainsKey(classSymbol))
            {
                methodSymbolReferencesPerClass = result[classSymbol];
            } else {
                methodSymbolReferencesPerClass = new Dictionary<ISymbol, IList<ISymbol>>();
                result[classSymbol] = methodSymbolReferencesPerClass;
            }

            IEnumerable<MethodDeclarationSyntax> declaredMethodSyntaxs =
                classDeclaration
                    .DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
                    .Cast<MethodDeclarationSyntax>();
            
            foreach(MethodDeclarationSyntax methodSymbol in declaredMethodSyntaxs) {
                SemanticModel methodModel = compilation.GetSemanticModel(methodSymbol.SyntaxTree);

                ISymbol? declaredMethodSymbol = methodModel.GetDeclaredSymbol(methodSymbol);
                if (declaredMethodSymbol  != null && dependencyTree.ContainsKey(declaredMethodSymbol)) 
                {
                    IList<ISymbol> symbolReferencesForMethodSymbol = null;
                    if (methodSymbolReferencesPerClass.ContainsKey(declaredMethodSymbol)) {
                        symbolReferencesForMethodSymbol = methodSymbolReferencesPerClass[declaredMethodSymbol];
                    } else {
                        symbolReferencesForMethodSymbol = new List<ISymbol>();
                        methodSymbolReferencesPerClass[declaredMethodSymbol] = symbolReferencesForMethodSymbol;
                    }
                    IList<ISymbol> dependenciesForMethodSymbol = dependencyTree[declaredMethodSymbol];
                    ((List<ISymbol>)symbolReferencesForMethodSymbol).AddRange(dependenciesForMethodSymbol);
                }
            }
        }
        return result;
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

    private IEnumerable<T> GetOfType<T>(SyntaxTree tree) {
        return 
            tree
                .GetRoot()
                .DescendantNodes()
                .OfType<T>()
                .Cast<T>();
    }


}
