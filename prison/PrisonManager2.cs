
string CellIndicatorPattern = "Cell Indicator Light 00([1-5])";
/*
Color standard = new Color(100,128,230); // 100, 128, 230
Color alert = new Color(255,82,41);  // 255, 82, 41
*/
// base Green - 84, 233, 144
/** 
 * PrisonManager
 */
public class PrisonManager
{
  static string CellPattern = "Cell 00([1-5])";
  static string IDPattern = "([1-5])([A-D])";
  IMyGridTerminalSystem grid;
  
  IMyProgrammableBlock me;
  Dictionary<int, PrisonCell> prisons = new Dictionary<int,PrisonCell>();
  // Used to control rotation of linked rotors.
  List<int> rotorLinks = new List<int> { 2, 1 };
  List<IMyInteriorLight> prisonLights = new List<IMyInteriorLight>();
  
  bool isSecured = false;
  bool onAlert = false;
  
  int numPlayersInPrison = 0;
  
  public PrisonManager(IMyGridTerminalSystem grid, IMyProgrammableBlock me) {
    this.grid = grid;
    this.me = me;
    Init();
  }
  
  private void Init() {
    List<IMyTerminalBlock> rotors = new List<IMyTerminalBlock>();
    grid.SearchBlocksOfName("Cell", rotors, 
      delegate(IMyTerminalBlock block) {
        return block is IMyMotorStator && Util.NameRegex(block, CellPattern).Success;
      });
    for(int n = 0; n < rotors.Count; n++) {
      PrisonCell p = new PrisonCell(this, rotors[n] as IMyMotorStator);
      prisons[p.id] = p;
    }
    
    // Init Lights.
    // Init Turrets.
    // Init Vents (in prison).
    
  }
  
  // TODO
  private void standardLighting() {
      
  }
  
  // TODO
  private void securityLighting() {
      
  }
  
  private void alertLighting() {
      
  }
  
  public PrisonCell FindCell(string arg) {
    System.Text.RegularExpressions.Match m = (new System.Text.RegularExpressions.Regex(IDPattern)).Match(arg);
    if (!m.Success) {
      Echo("No Prison called [" + arg + "] found");
      return null;
    }
    int rotor = int.Parse(m.Groups[1].Value);
    string chamber = m.Groups[2].Value; 
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
 
  public void OpenAll() {
    for (var e = prisons.Values.GetEnumerator(); e.MoveNext();) {
      PrisonCell p = e.Current;  
      p.openEmptyChamber();
    }
  }

  public void LockAll() {
    for (var e = prisons.Values.GetEnumerator(); e.MoveNext();) {
      PrisonCell p = e.Current;  
      p.close();
    }
  }

  public void OpenFirstUnoccupied() {
    bool found = false;
    for (var e = prisons.Values.GetEnumerator(); e.MoveNext();) {
      PrisonCell p = e.Current;
      if (!found && p.hasSpace()) {
        p.openEmptyChamber();
        found = true;
      } else {
        p.close();
      }
    }  
  }
  
  public void LockAllOccupied() {
    for (var e = prisons.Values.GetEnumerator(); e.MoveNext();) {
      PrisonCell p = e.Current;
      if (p.currentCellIsOccupied()) {
        p.close();
      }
    }
  }
  
  // TODO 
  public void SecurePrison(bool) {
    for (var e = prisons.Values.GetEnumerator(); e.MoveNext();) {
      PrisonCell p = e.Current;
        p.toggleSafetyLock();
      }
      isSecure = true;
      securityLighting();
      // TODO - lock all doors in prison area (by group name);
      // TODO -- Vents to depressurize.
  }

  public void SecurityAlert(bool on) {
      alertLighting();
      // Lock All Doors
      // Sound blocks?
      // Turrets ON;
      // Vents should stay the same?
  }
  
  // TODO
  public void VerifyPrisonIsEmptyThenLockAll() {
      // TODO -- schedule a sensor on/off to verify no players still in area.
      LockAll();
  }
  
  public void SensorEnter() {
      Echo("Player detected in Prison Area");
      numPlayersInPrison++;
      if (isSecure) SecurityAlert();
      else OpenFirstUnoccupiedCell();
  }
  public void SenseExit() {
      Echo("A Player left the Prison Area");
      numPlayersInPrison--;
      if (numPlayersInPrison == 0) VerifyPrisonIsEmptyThenLockAll();
      else LockAllOccupied();
  }
  
  // TODO
  public void WriteStatus() {
  }
}
