class Util {       
  public static System.Text.RegularExpressions.Match NameRegex(IMyTerminalBlock block, string Pattern) {       
    return       
        new System.Text.RegularExpressions.Regex(Pattern).Match(block.CustomName);       
  }  
  public static float toFloat(string s) {       
    var m = new System.Text.RegularExpressions.Regex("(-?[0-9]*).*").Match(s); 
    if (m.Success) return float.Parse(m.Groups[1].Value); 
    throw new ArgumentException(); 
  }   
         
  public static Dictionary<string, string> DetailedInfo(IMyTerminalBlock block) {  
    Dictionary<string, string> properties = new Dictionary<string, string>();       
    var statements = block.DetailedInfo.Split('\n');       
    for (int n = 0; n < statements.Length; n++) { 
        if (statements[n].IndexOf(':') > 0) { 
            var pair = statements[n].Split(':');       
            properties.Add(pair[0], pair[1].Substring(1));       
        } else {
            properties.Add(statements[n], "True");
        }
    }       
    return properties;       
  }       
}       
