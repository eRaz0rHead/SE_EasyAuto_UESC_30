
      
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
