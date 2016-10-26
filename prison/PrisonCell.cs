
/* indiciator colors
 75, 170, 255
 255, 170, 75
 255, 75, 75
 75, 170 (or 255), 75
 
 */

/**
    Prison Cell
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
    
    public IMyMotorStator rotor;
    public int id;
    public string requestedChamber;
    IMyInteriorLight indicator;
    
    int numOccupied = 0;
    
    PrisonManager manager;
    Dictionary <string, IMyCryoChamber> chambers = new Dictonary<string, IMyCryoChamber>();
    
    
    public PrisonCell(PrisonManager manager, IMyMotorStator rotor) {
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
        
        numOccupied = 0;
        
        for (int i=0; i < cryos.Count; i++) {
            var m = Util.NameRegex(block, pattern);
            IMyCryoChamber cryo = cryos.ElementAt(i) as IMyCryoChamber;
            chambers[m.Groups[1].Value] = cryo;
            if (cryo.isUnderControl) numOccupied ++;
        }
    }
    
    
    // NOTE : "Force weld" is the action for Safety lock on/off.
    
    public float GetRotorAngle() {
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
    
    public float GetCurrentAngle() {
        return PreceedingAngle() + GetRotorAngle();
    }
    
    public int SetCurrentAngle(float angle) {
        if (isLocked) toggleSafetyLock();
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
        
        /*
        int idx = manager.rotorLinks.indexOf(id);
        if (idx == -1) return;
        if (manager.rotorLinks.Count > idx) {
            Echo("Counter rotating by " + diff);
        }
        */
        
        // Schedule this in a delay .. toggleSafetyLock();
        return diff;
    }
    
    
    public string visibleChamber() {
        // What about IF moving ..
        int angle = GetCurrentAngle();
        return CHAMBER_ANGLES(angle);
    }
    
    public void open() {
        if (requestedChamber != null) openChamber(requestedChamber);
    }
    
    public void openChamber(string chamber) {
        int angle = CHAMBER_ANGLES(chamber);
        if (angle != -1) SetCurrentAngle(angle);
    }
    
    public void close() {
        int angle = GetCurrentAngle();
        SetCurrentAngle(angle + 45);
    }
    
    public void resetOccupancy() {
        numOccupants = 0;
        for (int i = 0; i < chambers.Keys.Count; i++) {
            string k = chambers.Keys.ElementAt(i);
            IMyCryoChamber c = chambers[k];
            if (c.isUnderControl) numOccupants ++;
        }
        // SET LIGHTS
    }
    
    // TODO
    public bool hasSpace() {
        for (int i = 0; i < chambers.Keys.Count; i++) {
            string k = chambers.Keys.ElementAt(i);
            IMyCryoChamber c = chambers[k];
            if (!c.isUnderControl) return true;
        }
        return false;
    }
 
    public void openEmptyChamber() {
        for (int i = 0; i < chambers.Keys.Count; i++) {
            string k = chambers.Keys.ElementAt(i);
            IMyCryoChamber c = chambers[k];
            if (!c.isUnderControl) {
                openChamber(k);
                return;
            }
        }
    }
    // TODO
    public bool currentCellIsOccupied() {
        string current = visibleChamber();
        if (current == null) return false;
        return chambers[current].isUnderControl;
    }
    
    public bool isLocked() {
        return rotor.isLocked();
    }
    
    public void toggleSafetyLock() {
        rotorAction("Force weld");
    }

    void rotorAction(String Name)
    {
        ITerminalAction Action = this.rotor.GetActionWithName(Name);
        
        if(Action != null)
        {
            Action.Apply(this.rotor);
        }
    }


}