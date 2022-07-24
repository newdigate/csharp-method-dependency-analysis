using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
namespace type_deinference;

public class MethodCallAnalizer 
{
    private readonly IEnumerable<MetadataReference> references;

    public MethodCallAnalizer(IEnumerable<MetadataReference> references)
    {
        this.references = references;
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
        foreach (string sourceIdentifier in sourceIdentifiers) {
            sourceCodeByIdentifier.Add( sourceIdentifier, getSourceFromIdentifier(sourceIdentifier));
        }

        CSharpCompilation compilation = 
            CompileCSharp(
                sourceCodeByIdentifier.Values, 
                out IDictionary<SyntaxTree, CompilationUnitSyntax> trees);

        IEnumerable<ClassDeclarationSyntax> classDeclarationSyntaxes = 
            trees
                .Keys
                .SelectMany(
                    cds => cds
                        .GetRoot()
                        .DescendantNodes()
                        .OfType<ClassDeclarationSyntax>()
                );

        IEnumerable<ClassDeclarationSyntax> partialClassDeclarationSyntaxes = 
            classDeclarationSyntaxes
                .Where( c => c
                                .Modifiers
                                .Any(
                                    m => m.IsKind(SyntaxKind.PartialKeyword)));

        IEnumerable<IGrouping<string, ClassDeclarationSyntax>> groupedPartialClasses =
            partialClassDeclarationSyntaxes.GroupBy( partial => partial.Identifier.ToFullString() );
        IEnumerable<MethodDeclarationSyntax> methods = partialClassDeclarationSyntaxes.SelectMany( cds => cds.DescendantNodes().OfType<MethodDeclarationSyntax>().Cast<MethodDeclarationSyntax>() ); 

        IEnumerable<SemanticModel> models = trees.Keys.Select( key => compilation.GetSemanticModel(key) );

        IDictionary<ISymbol, IList<ISymbol>> dependencyTree = new Dictionary<ISymbol, IList<ISymbol>>();

        foreach( MethodDeclarationSyntax methodDeclaration in methods) {
            SemanticModel model = compilation.GetSemanticModel(methodDeclaration.SyntaxTree);

            ISymbol? methodSymbol =  model.GetDeclaredSymbol(methodDeclaration);
            //Console.WriteLine($"Method: {methodSymbol?.ToDisplayString()};");
            if (methodSymbol == null) continue;

            IList<ISymbol> dependencies = null;
            if (dependencyTree.ContainsKey(methodSymbol)) {
                dependencies = dependencyTree[methodSymbol];
            } else 
            {
                dependencies = new List<ISymbol>();
                dependencyTree[methodSymbol] = dependencies;
            }

            IEnumerable<InvocationExpressionSyntax>? invocations = methodDeclaration?.Body?.DescendantNodes().OfType<InvocationExpressionSyntax>().Cast<InvocationExpressionSyntax>();
            if (invocations != null)
            {
                foreach (InvocationExpressionSyntax invocation in invocations) {
                    IEnumerable<ISymbol> symbols =  model.GetMemberGroup(invocation.Expression);
                    foreach (ISymbol symbol in symbols) {
                        if (!dependencies.Contains(symbol))
                            dependencies.Add(symbol);
                        //Console.WriteLine($"Depedency: {symbol.ToDisplayString()};");
                    }
                }
            }
        }

        return dependencyTree;
    }

    private CSharpCompilation CompileCSharp(
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

public class MethodDependencyNode {
    private readonly MethodInfo _method;

    public MethodDependencyNode(MethodInfo method)
    {
        _method = method;
    }

    private readonly List<MethodInfo> _dependencies = new List<MethodInfo>();

    public IEnumerable<MethodInfo> Dependencies { get { return _dependencies; } }

    public void AddDependency(MethodInfo method) {
        if (!_dependencies.Contains(method))
            _dependencies.Add(method);
    }
}
