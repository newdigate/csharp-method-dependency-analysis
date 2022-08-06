using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
namespace type_deinference;

public interface IMethodCallAnalyzer {
    IDictionary<ISymbol, IList<ISymbol>> AnalizeMethodCalls(string source);


    IDictionary<ISymbol, IList<ISymbol>> AnalizeMethodCalls(Compilation compilation, IDictionary<SyntaxTree, CompilationUnitSyntax> trees);
    

    IDictionary<ISymbol, IList<ISymbol>> AnalizeMethodCalls(
        IEnumerable<string> sourceIdentifiers, 
        Func<string, string> getSourceFromIdentifier);
}

public interface IClassDependencyAnalyzer {
    IDictionary<ISymbol, IDictionary<ISymbol, IList<ISymbol>>> AnalizeClassCalls(string source);


    IDictionary<ISymbol, IDictionary<ISymbol, IList<ISymbol>>> AnalizeClassCalls(Compilation compilation, IDictionary<SyntaxTree, CompilationUnitSyntax> trees);
    

    IDictionary<ISymbol, IDictionary<ISymbol, IList<ISymbol>>> AnalizeClassCalls(
        IEnumerable<string> sourceIdentifiers, 
        Func<string, string> getSourceFromIdentifier);
}

public interface ICSharpCompilationProvider {
    CSharpCompilation CompileCSharp(IEnumerable<string> sourceCodes, out IDictionary<SyntaxTree, CompilationUnitSyntax> roots);
}

public class CSharpCompilationProvider : ICSharpCompilationProvider {
    private readonly IEnumerable<MetadataReference> references;
    public CSharpCompilationProvider(IEnumerable<MetadataReference> references)
    {
        this.references = references;
    }
    public CSharpCompilation CompileCSharp(
        IEnumerable<string> sourceCodes, 
        out IDictionary<SyntaxTree, CompilationUnitSyntax> roots) 
    {
        Dictionary<SyntaxTree, CompilationUnitSyntax> syntaxTrees = new Dictionary<SyntaxTree, CompilationUnitSyntax>();
        roots = syntaxTrees;

        foreach (string sourceCode in sourceCodes) {
            SyntaxTree tree = CSharpSyntaxTree.ParseText(sourceCode);
            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
            syntaxTrees[tree] = root;
        }
        CSharpCompilationOptions cSharpCompilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);

        CSharpCompilation compilation = 
            CSharpCompilation
                .Create(
                    "assemblyName",
                    syntaxTrees.Keys,
                    references,
                    cSharpCompilationOptions
                   );
        foreach (var d in compilation.GetDiagnostics())
        {
            Console.WriteLine(CSharpDiagnosticFormatter.Instance.Format(d));
        }
        return compilation;
    }

}

public interface ISymbolFinder {
    ISymbol? Find(ExpressionSyntax syntax);
}

public class SymbolFinder : ISymbolFinder {
    private readonly Func<ExpressionSyntax, ISymbol?> _fnFindSymbol;

    public SymbolFinder(Func<ExpressionSyntax, ISymbol?> fnFindSymbol)
    {
        _fnFindSymbol = fnFindSymbol;
    }

    public ISymbol? Find(ExpressionSyntax syntax) {
        return _fnFindSymbol(syntax);
    }
}

public interface ISymbolCache {
    void Add(ISymbol symbol);
}
public class SymbolCache : ISymbolCache {
    private readonly IDictionary<string, ISymbol> _symbolCache;

    public SymbolCache(IDictionary<string, ISymbol> symbolCache)
    {
        _symbolCache = symbolCache;
    }

    public void Add(ISymbol symbol) {
        _symbolCache[symbol.Name] = symbol;
    }
}
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
