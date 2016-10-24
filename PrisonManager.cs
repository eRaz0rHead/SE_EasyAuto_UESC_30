
class PrisonManager: EasyAPI
{
  Dictionary<int, PrisonCell> prisons = new Dictonary<int,PrisonCell>();
  List<int> rotorLinks = new List<int> { 2, 1 };
  
  void Init() {
    for (var block in Blocks.byNameRegex(PrisonCell.CellPattern) {
      PrisonCell p = new PrisonCell(PrisonManager, block);
      Prisons[p.id] = p;
    }
  }
  
  PrisonCell FindCell(string arg) {
    System.Text.RegularExpressions.Match m = (new System.Text.RegularExpressions.Regex("([1-5])([A-D]")).Match(arg);
    if (!m.Success) {
      Echo("No Prison called " + arg + " found");
      return null;
    }
    var rotor = m.Groups[1].Value;
    var chamber = m.Groups[2].Value; 
    PrisonCell cell = prisons[rotor]; 
    cell.requestedChamber = chamber;
    return cell;
  }
  
  
}

class PrisonCell {
  static string CellPattern = “Cell 00([1-5])”;
  static string ChamberPattern = “Cryo ([A-D])”;

  public static int CHAMBER_ANGLES(string name) {
    switch (name) {
     case "A":
      return 45;
     case "B":
      return 135;
     case "C":
      return 225;
     case "D":
      return 315;
     default:
      return -1;
    }
  }

  // IMyRotorStator
  EasyBlock rotor;
  PrisonManager manager;
  int id;
  String requestedChamber;
  Dictionary < string, IMyCryoChamber > chambers;

  PrisonCell(EasyBlock rotor) {
    this.rotor = rotor;
    System.Text.RegularExpressions.Match m = (new System.Text.RegularExpressions.Regex(CellPattern).Match(rotor.Name());
    if (m.Success) {
     this.id = Int32.Parse(m.Groups[1].Value);
    }
    // // TODO -- initialize chamber list
    // // Not sure if RotorGrid is visible via this API.
    // var gridBlocks = rotor.RotorGrid.getBlocks etc...
    // for (var block in gridBlocks.byNameRegex(PrisonCell.ChamberPattern) {
    // 
    // }
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
   for (var chamber in [“A”, “B”, “C”, “D”]) {
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
}
