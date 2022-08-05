namespace type_deinference;

public class RandomColorProvider : IRandomColorProvider {
    static string[] colors = {"green", "red", "blue", "grey", "yellow", "purple", "salmon2", 
                            "deepskyblue", "goldenrod2", "burlywood2", "gold1", "greenyellow", 
                            "darkseagreen", "dodgerblue1", "thistle2","darkolivegreen3", "chocolate", 
                            "turquoise3", "steelblue3","navy","darkseagreen4","blanchedalmond","lightskyblue1","aquamarine2","lemonchiffon"  };
    
    public string RandomColor(Object result) {
        return colors[result.GetHashCode() % colors.Length];
    }
}
