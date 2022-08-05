using Microsoft.CodeAnalysis;
namespace type_deinference;

public class MethodSymbolAnnotater : IMethodSymbolAnnotater{
    public string Annotate(ISymbol symbol) {
        if (symbol is IMethodSymbol methodSymbol) {
            string parameters = string.Join(",", methodSymbol.Parameters.Select( p => p.Type.Name ));
            string typeparameters = methodSymbol.TypeParameters.Any()? $"<{string.Join(", ", methodSymbol.TypeParameters.Select( p => p.Name))}>" : string.Empty;
            string result = $"{methodSymbol.ContainingType.Name}.{methodSymbol.Name}{typeparameters}({parameters})";
            return result;
        }
        return "Not a method symbol...";
    }
}