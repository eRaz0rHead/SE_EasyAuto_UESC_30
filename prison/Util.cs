
class Util {
  public static System.Text.RegularExpressions.Match NameRegex(IMyTerminalBlock block, string Pattern) {
    return
        new System.Text.RegularExpressions.Regex(Pattern).Match(block.GetCustomName());
  }
  
  public static Dictionary<string, string> DetailedInfo(IMyTerminalBlock block) {
    Dictionary<string, string> properties = new Dictionary<string, string>();
    var statements = block.DetailedInfo.Split('\n');
    for (int n = 0; n < statements.Length; n++) {
        var pair = statements[n].Split(':');
        properties.Add(pair[0], pair[1].Substring(1));
    }
    return properties;
  }
}

