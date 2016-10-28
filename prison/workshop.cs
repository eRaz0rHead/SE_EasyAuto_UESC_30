public Program() {       
    // The constructor, called only once every session and      
    // always before any other method is called. Use it to      
    // initialize your script.       
    //           
    // The constructor is optional and can be removed if not      
    // needed.       
}       
       
public void Save() {       
    // Called when the program needs to save its state. Use      
    // this method to save your state to the Storage field      
    // or some other means.       
    //       
    // This method is optional and can be removed if not      
    // needed.       
}       
       
public void Main(string argument) {       
   Echo("argument " + argument);      
    PrisonManager manager = new PrisonManager(this);  
    manager.handleCommand(argument);  
}      
    
public class PrisonCell {       
    public static string CellIndicatorPattern = "Cell Indicator Light 00([1-5])";       
    public static int CHAMBER_ANGLES(string name) {       
        switch (name) {       
            case "A": return 45;       
            case "B": return 135;       
            case "C": return 225;       
            case "D": return 315;       
            default:  return -1;       
        }       
    }       
           
    public static string CHAMBER_ANGLES(int angle) {       
        switch (angle) {       
            case 45:       
            case -315:       
            return "A";       
            case 135:       
            case -225:       
            return "B";       
            case 225:       
            case -135:       
            return "C";       
            case 315:       
            case -45: return "D";       
            default:  return null;       
        }       
    }       
           
    public IMyMotorStator rotor;       
    public int id;       
    public string requestedChamber;       
    IMyInteriorLight indicator;       
    int numOccupied = 0;       
    PrisonManager manager;       
    Dictionary <string, IMyCryoChamber> chambers = new Dictionary<string, IMyCryoChamber>();       
            
    public PrisonCell(PrisonManager manager, IMyMotorStator rotor) {       
        this.rotor = rotor;       
        this.manager = manager;       
               
        var m = Util.NameRegex(rotor, PrisonManager.CellPattern);       
        if (m.Success) {       
            this.id = int.Parse(m.Groups[1].Value);       
            InitChambers();       
        }       
    }       
           
    void InitChambers() {       
        string pattern = "Cryo "+id+"([A-D])";       
        List<IMyTerminalBlock> cryos = new List<IMyTerminalBlock>();       
        manager.grid.SearchBlocksOfName("Cryo ", cryos,       
            delegate(IMyTerminalBlock block) {       
                return block is IMyCryoChamber && Util.NameRegex(block, pattern).Success;       
            }       
        );      
 
        numOccupied = 0;       
 
        for (int i=0; i < cryos.Count; i++) {       
            IMyCryoChamber cryo = cryos[i] as IMyCryoChamber;       
            var m = Util.NameRegex(cryo, pattern);       
            if (m.Success) { 
                chambers[m.Groups[1].Value] = cryo;       
                if (cryo.IsUnderControl) numOccupied++;   
            }             
        }       
    }       
           
           
    // NOTE : "Force weld" is the action for Safety lock on/off.       
           
    public float GetRotorAngle() {  
        manager.program.Echo("getRotorAngle "); 
        var info = Util.DetailedInfo(rotor); 
        if (!info.ContainsKey("Current angle")) return -361.0F; 
        string angleText = Util.DetailedInfo(rotor)["Current angle"];  
        manager.program.Echo("getRotorAngle from '"+ angleText+ "' = " + Util.toFloat(angleText)); 
         
        if (angleText == null) return -361.0F; 
        return  Util.toFloat(angleText);    
    }      
/*    
    float PreceedingAngle() {       
        // sum across the rotor-links;       
        int idx = manager.rotorLinks.indexOf(id);       
        if (idx == -1) return 0;       
        float angle;       
        for (idx; idx > 0; idx--) {       
            angle += manager.prisons[idx].GetRotorAngle();       
        }       
        return angle;       
    }      
*/    
           
    public float GetCurrentAngle() {       
        // return PreceedingAngle() +    
        return GetRotorAngle();       
    }       
           
    public float SetCurrentAngle(float angle) {  
        manager.program.Echo("setCurrentAngle " + angle + " on rotor "+ this.rotor); 
        if (isRotorLocked()) { 
            toggleSafetyLock();     
        }     
       
        float current = GetCurrentAngle();       
        float speed = 3.0F;   
        rotor.SetValueFloat("UpperLimit", angle);   
        rotor.SetValueFloat("LowerLimit", angle);   
    
        var diff = angle - current;      
        if (diff == 0)    
            return diff;       
   
        if (diff < 0.0F && diff > -180.0F) {       
            speed = -3.0F;       
        }       
        rotor.SetValueFloat("Velocity", speed);   
        return diff;       
    }   
       
    public string visibleChamber() {       
        float angle = GetCurrentAngle();       
        return CHAMBER_ANGLES(Convert.ToInt32(angle));       
    }       
           
    public void open() {       
        if (requestedChamber != null) openChamber(requestedChamber);       
    }       
           
    public void openChamber(string chamber) {       
        int angle = CHAMBER_ANGLES(chamber);   
        manager.program.Echo("opening chamber "+chamber +" by rotating to " + angle); 
        if (angle != -1) SetCurrentAngle(angle);       
    }       
           
    public void close() {       
        float angle = GetCurrentAngle();       
        SetCurrentAngle(angle + 45);       
    }       
           
    public void resetOccupancy() {       
        numOccupied = 0;       
        for (var e = chambers.Values.GetEnumerator(); e.MoveNext();) {  
            IMyCryoChamber c = e.Current;  
            if (c.IsUnderControl) numOccupied ++;       
        }       
        // SET LIGHTS       
    }       
           
    // TODO       
    public bool hasSpace() {       
        for (var e = chambers.Values.GetEnumerator(); e.MoveNext();) {  
             IMyCryoChamber c = e.Current;  
            if (!c.IsUnderControl) return true;       
        }       
        return false;       
    }       
        
    public void openEmptyChamber() {     
        for (var e = chambers.Keys.GetEnumerator(); e.MoveNext();) {  
            string k = e.Current;  
            IMyCryoChamber c = chambers[k];       
            if (!c.IsUnderControl) {       
                openChamber(k);       
                return;       
            }       
        }       
    }       
    // TODO       
    public bool currentCellIsOccupied() {       
        string current = visibleChamber();       
        if (current == null) return false;       
        return chambers[current].IsUnderControl;       
    }       
           
    public bool isRotorLocked() {   
         return this.rotor.IsLocked;  
    }       
           
    public void toggleSafetyLock() {  
       rotorAction("Force weld");       
    }       
       
    public void rotorAction(String Name)       
    {       
        ITerminalAction Action = this.rotor.GetActionWithName(Name);       
               
        if(Action != null)       
        {       
            Action.Apply(this.rotor);       
        }       
    }       
       
       
}      
       
       
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
        } 
    }       
    return properties;       
  }       
}       
      
public class PrisonManager      
{      
  public static string CellPattern = "Cell 00([1-5])";      
  public static string IDPattern = "([1-5])([A-D])";     
 
   
  public IMyGridTerminalSystem grid;    
   
  public MyGridProgram  program; 
  IMyProgrammableBlock me;    
   
  public Dictionary<int, PrisonCell> prisons = new Dictionary<int,PrisonCell>();      
  // Used to control rotation of linked rotors.      
  public List<int> rotorLinks = new List<int> { 2, 1 };      
  List<IMyInteriorLight> prisonLights = new List<IMyInteriorLight>();      
        
  public bool isSecured = false;      
  public bool onAlert = false;      
        
  public int numPlayersInPrison = 0;      
        
  public PrisonManager(MyGridProgram  program) { 
    this.program = program;   
    this.grid = program.GridTerminalSystem;      
    this.me = program.Me ;      
    Init();      
  }      
        
  private void Init() {      
    List<IMyTerminalBlock> rotors = new List<IMyTerminalBlock>();      
    grid.SearchBlocksOfName("Cell", rotors,       
      delegate(IMyTerminalBlock block) {      
        return block is IMyMotorStator && Util.NameRegex(block, CellPattern).Success;      
      });      
       
    program.Echo("initialized with " + rotors.Count + " rotors"); 
    for(int n = 0; n < rotors.Count; n++) {      
      PrisonCell p = new PrisonCell(this, rotors[n] as IMyMotorStator);      
      prisons[p.id] = p;      
    }     
      
  }    
   
  private void standardLighting() {      
            
  }      
   
  private void securityLighting() {      
    
  }      
        
  private void alertLighting() {      
            
  }      
        
  public PrisonCell FindCell(string arg) {      
    System.Text.RegularExpressions.Match m = (new System.Text.RegularExpressions.Regex(IDPattern)).Match(arg);      
    if (!m.Success) {      
      program.Echo("No Prison called [" + arg + "] found");      
      return null;      
    } 
 
    int rotor = int.Parse(m.Groups[1].Value);      
    string chamber = m.Groups[2].Value;  
    program.Echo("should have a cell "+ rotor + " -> " + chamber);     
    PrisonCell cell = prisons[rotor];       
    cell.requestedChamber = chamber; 
   
    return cell;      
  }      
        
  public void OpenCell(string arg) {      
    PrisonCell cell = FindCell(arg);      
    if (cell != null) cell.open();      
  }      
        
  public void LockCell(string arg) {      
    PrisonCell cell = FindCell(arg);      
    if (cell != null) cell.close();      
  }      
       
       
  // TODO      
  public void WriteStatus() {      
  }      
   
  public void handleCommand(string command) { 
    // TODO 
 
    OpenCell(command);  
  
  } 
}
