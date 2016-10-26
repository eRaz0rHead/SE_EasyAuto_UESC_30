
string CellPattern = "Cell 00([1-5])";
string ChamberPattern = "Cryo ([1-5])([A-D])";

class PrisonManager: EasyAPI
{
  Dictionary<int, PrisonCell> prisons = new Dictonary<int,PrisonCell>();
  List<int> rotorLinks = new List<int> { 2, 1 };
  
  void Init() {
    EasyBlocks rotors;
    rotors = Blocks.NameRegex(CellPattern);
    for(int n = 0; n < rotors.Count; n++) {
      PrisonCell p = new PrisonCell(this, rotors[n]);
      prisons[p.id] = p;
    }
  }
  
  PrisonCell FindCell(string arg) {
    System.Text.RegularExpressions.Match m = (new System.Text.RegularExpressions.Regex("([1-5])([A-D]")).Match(arg);
    if (!m.Success) {
      Echo("No Prison called [" + arg + "] found");
      return null;
    }
    int rotor = int.Parse(m.Groups[1].Value);
    var chamber = m.Groups[2].Value; 
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
    if (cell != null) cell.lock();
  }
  
  void OpenAll() {
    for (var p in prisons) {
      p.openEmptyChamber();
    }
  }
  void LockAll() {
    for (var p in prisons) {
      p.lock();
    }
  }
  
  void OpenFirstUnoccupied() {
    bool found = false;
    for (PrisonCell prison in prisons) {
      if (prison.hasSpace() && !found) {
        prison.openEmptyChamber();
        found = true;
      } else {
        prison.lock();
      }
    }
  }
  
  void LockAllOccupied() {
    for (PrisonCell prison in prisons) {
      if (prison.currentCellIsOccupied()) {
        prison.lock();
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
  // IMyRotorStator
  EasyBlock rotor;
  PrisonManager manager;
  int id;
  string requestedChamber;
  Dictionary <string, IMyCryoChamber> chambers = new Dictonary<int, IMyCryoChamber>();

  PrisonCell(PrisonManager manager, EasyBlock rotor) {
    this.rotor = rotor;
    this.manager = manager;
    
    System.Text.RegularExpressions.Match m = (new System.Text.RegularExpressions.Regex(CellPattern).Match(rotor.Name());
    if (m.Success) {
     this.id = int.Parse(m.Groups[1].Value);
    }
   
   }

  float GetRotorAngle() {
    string angleText = rotor.DetailedInfo()[“Current angle”]);
    return float.Parse(angleText.Split(' ')[0]);
  }
  
  float GetCurrentAngle() {
    // sum across the rotor-links;
    int idx = rotorLinks.indexOf(id);
    if (idx == -1) return GetRotorAngle();
    float angle;
    for (idx; idx < rotorLinks.Count(); idx++) {
      angle += manager.prisons[idx].GetRotorAngle();
    }
    return angle;
  }

  void SetCurrentAngle(float angle) {
    float current = GetCurrentAngle();
    float speed = 3.0;
    rotor.SetValueFloat("UpperLimit", angle); 
    rotor.SetValueFloat("LowerLimit", angle);
    int diff = angle - current
    if (diff == 0) return;
    if (diff < 0 && diff > -180) {
      // TODO -- setting Upper == Lower is janky. We should detect direction and set upper > lower
      // in the direction that the thing is turning.
      speed = -3.0;
    }
    rotor.SetValueFloat("Velocity", speed);
  }
  
  string visibleChamber() {
   // What about IF moving ..
   int angle = GetCurrentAngle();
   for (var chamber in ["A", "B", "C", "D"]) {
    if (CHAMBER_ANGLES(chamber) == angle) return chamber;
   }
   return null;
  }

  void open() {
   if (requestedChamber != null) openChamber(requestedChamber);
  }

  void openChamber(string chamber) {
   int angle = CHAMBER_ANGLES(chamber);
   if (angle != -1) SetCurrentAngle(angle);
  }

  void lock() {
   int angle = GetCurrentAngle();
   SetCurrentAngle(angle + 45);
  }
  // TODO
  // hasSpace
  // openEmptyChamber
  // currentCellIsOccupied
}
