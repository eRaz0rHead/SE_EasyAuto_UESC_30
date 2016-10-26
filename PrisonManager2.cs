string CellPattern = "Cell 00([1-5])";
string IDPattern = "([1-5])([A-D])";
// string ChamberPattern = "Cryo "+IDPattern;
float RotationSpeed = 3.0;

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

class PrisonManager 
{
  IMyGridTerminalSystem grid;
  IMyProgrammableBlock me;
  
  Dictionary<int, PrisonCell> prisons = new Dictonary<int,PrisonCell>();
  List<int> rotorLinks = new List<int> { 2, 1 };
  
  public PrisonManager(IMyGridTerminalSystem grid, IMyProgrammableBlock me) {
    this.grid = grid;
    this.me = me;
    Init();
  }
  
  void Init() {
    List<IMyTerminalBlock> rotors;
    grid.SearchBlocksOfName("Cell", rotors, 
      delegate(IMyTerminalBlock block) {
        return block is IMyMotorStator && Util.NameRegex(block, CellPattern).Success;
      });
    for(int n = 0; n < rotors.Count; n++) {
      PrisonCell p = new PrisonCell(this, rotors[n] as IMyMotorStator);
      prisons[p.id] = p;
    }
  }
  
  PrisonCell FindCell(string arg) {
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
  
  void OpenCell(string arg) {
    PrisonCell cell = FindCell(arg);
    if (cell != null) cell.open();
  }
  void LockCell(string arg) {
    PrisonCell cell = FindCell(arg);
    if (cell != null) cell.close();
  }
 
  void OpenAll() {
    for (int i = 0; i < prisons.Keys.Count; i++) {
      int k = prisons.Keys.ElementAt(i);
      PrisonCell p = prisons[k];
      p.openEmptyChamber();
    }
  }
  void LockAll() {
    for (int i = 0; i < prisons.Keys.Count; i++) {
      int k = prisons.Keys.ElementAt(i);
      PrisonCell p = prisons[k];
      p.close();
    }
  }

  void OpenFirstUnoccupied() {
    bool found = false;
    for (int i = 0; i < prisons.Keys.Count; i++) {
      int k = prisons.Keys.ElementAt(i);
      PrisonCell prison = prisons[k];
      if (!found && prison.hasSpace()) {
        prison.openEmptyChamber();
        found = true;
      } else {
        prison.close();
      }
    }  
  }
  
  void LockAllOccupied() {
     for (int i = 0; i < prisons.Keys.Count; i++) {
      int k = prisons.Keys.ElementAt(i);
      PrisonCell p = prisons[k];
      if (p.currentCellIsOccupied()) {
        p.close();
      }
    }
  }

  // TODO 
  // SecurePrison(bool)
  // SecurityAlert(bool) {
  // WriteStatus()
  // SenseEnter()
  // SenseExit()
}

/**
  PRISON CELL CLASS
 */
class PrisonCell {
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
  
  IMyMotorStator rotor;
  PrisonManager manager;
  int id;
  string requestedChamber;
  Dictionary <string, IMyCryoChamber> chambers = new Dictonary<string, IMyCryoChamber>();

  PrisonCell(PrisonManager manager, IMyMotorStator rotor) {
    this.rotor = rotor;
    this.manager = manager;
    
    var m = Util.NameRegex(rotor, CellPattern);
    if (m.Success) {
      this.id = int.Parse(m.Groups[1].Value);
      InitChambers();
    }
  }

  void InitChambers() {
    string pattern = "Cryo "+id+"([A-D])";
    List<IMyTerminalBlock> cryos;
    grid.SearchBlocksOfName("Cryo ", cryos), 
      delegate(IMyTerminalBlock block) {
        return block is IMyCryoChamber && Util.NameRegex(block, pattern).Success;
      }
    );
    
    for (int i=0; i < cryos.Count; i++) {
      var m = Util.NameRegex(block, pattern);
      chambers[m.Groups[1].Value] = cryos.ElementAt(i) as IMyCryoChamber;
    }
  }
  
  float GetRotorAngle() {
    string angleText = Util.DetailedInfo(rotor)[“Current angle”]);
    return float.Parse(angleText.Split(' ')[0]);
  }
  
  float PreceedingAngle() {
    // sum across the rotor-links;
    int idx = rotorLinks.indexOf(id);
    if (idx == -1) return 0;
    float angle;
    for (idx - 1; idx > 0; idx--) {
      angle += manager.prisons[idx].GetRotorAngle();
    }
    return angle;
  }
  
  float GetCurrentAngle() {
    return PreceedingAngle() + GetRotorAngle();
  }

  void SetCurrentAngle(float angle) {
    float current = GetCurrentAngle();
    float speed = 3.0;
    // TODO -- setting Upper == Lower is janky. We should detect direction and set upper > lower
    // in the direction that the thing is turning.
    rotor.SetValueFloat("UpperLimit", angle); 
    rotor.SetValueFloat("LowerLimit", angle);
    
    int diff = angle - current
    if (diff == 0) return;
    if (diff < 0 && diff > -180) {
      speed = -3.0;
    }
    rotor.SetValueFloat("Velocity", speed);
    
    // CounterRotate rotors on same chain.
    int idx = rotorLinks.indexOf(id);
    if (idx == -1) return;
    if (rotorLinks.Count > idx) {
       Echo("Counter rotating by " + diff);
    }
  }
  
  string visibleChamber() {
   // What about IF moving ..
    int angle = GetCurrentAngle();
    return CHAMBER_ANGLES(angle);
  }

  void open() {
    if (requestedChamber != null) openChamber(requestedChamber);
  }

  void openChamber(string chamber) {
    int angle = CHAMBER_ANGLES(chamber);
    if (angle != -1) SetCurrentAngle(angle);
  }

  void close() {
    int angle = GetCurrentAngle();
    SetCurrentAngle(angle + 45);
  }
/*
 for (int i = 0; i < prisons.Keys.Count; i++) {
      int k = prisons.Keys.ElementAt(i);
      PrisonCell p = prisons[k];
      p.openEmptyChamber();
    }
    */
  // TODO
  bool hasSpace() {
    for (int i = 0; i < chambers.Keys.Count; i++) {
      string k = chambers.Keys.ElementAt(i);
      IMyCryoChamber c = chambers[k];
      if (!c.isUnderControl) return true;
    }
    return false;
  }
  // TODO
  void openEmptyChamber() {
    for (int i = 0; i < chambers.Keys.Count; i++) {
      string k = chambers.Keys.ElementAt(i);
      IMyCryoChamber c = chambers[k];
      if (!c.isUnderControl) {
        openChamber(k);
      }
    }
  }
  // TODO
  bool currentCellIsOccupied() {
    string current = visibleChamber();
    if (current == null) return false;
    return chambers[current].isUnderControl;
  }
}
