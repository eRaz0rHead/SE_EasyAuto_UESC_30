/* Wico Craft ORIBAL DESCENT control sub-module 
 * 68.2 descent support horizontal ships. (sabrina) 
 * Moved DESCENT out of launch script. This is now DESCENT only script. 
 *  
 * Calculate roll toward target location. (need to change for horizontal craft) 
 *  
 * 68.3 support descent towards hover start (end in hover mode when 'close') 
 *  
 * 68.3.1 Try not using gravity align in last 100 meters.. 
 *  
 * 68.3.2 re-init after landing gear released as grids change (me was everything, not just 'me') 
 *  
 * 68.3.3 fix gpsCenter not using me.cubegrid 
 *  
 * 70 Update for SE V1.142 
 *   
 * NEED:  
 *  
 * NAV needs to ignore/handle gyros that aren't pointed 'right' way.. 
 *  
 * need to handle other remote controls on merged ship on launch 
 * Reactors on/off for docked. 
 *  
 * ROCKET: Main engines at the back of the craft. Starts in 'facing up' position (like a rocket). 
 * VTOL: has atmo for hovering/rising in horizonal postion. then transitions to vertical for orbital 
 *  
 *  
 * WANTED: 
 * VTOL Support 
 * 'normal' support (horizontal) 
 * calculate mass ship 
 * support non-rocket launch 
 * calculate inventory multiplier 
 * calculate thrust available and determine if able to lift-off 
 * set intial thrust values to estimated lift-off values 
 * predict acceleration/decel and adjust thrust on launch based on what's it's GOING to be.. 
 * choose ROCKET/VTOL/NORMAL automatically based on thruster arrangement determination 
 * Need reset to erase storage and return to defaults.. 
 *  
*/

string OurName = "Wico Craft";
string moduleName = "Orbital Descent";

//int maxPower=200; // % of ion+atmo for max. 
int retroStartAlt = 1300;
int startReverseAlt = 6000;

const string sGPSCenter = "Craft Remote Control";

Vector3I iForward = new Vector3I(0, 0, 0);
Vector3I iUp = new Vector3I(0, 0, 0);
Vector3I iLeft = new Vector3I(0, 0, 0);
//double velocityShip, velocityForward, velocityUp, velocityLeft; 
Vector3D currentPosition; //, lastPosition, currentVelocity, lastVelocity; 
//Vector3 vectorForward, vectorLeft, vectorUp; 
//Vector3 center,up,left,forward; 
//float fAssumeElapsed=1.339f; 
//DateTime dtStartTime; 
//bool bCalcAssumed=true;  
//bool bGotStart=false; 
// 
const string velocityFormat = "0.00";

IMyTerminalBlock anchorPosition;

class OurException: Exception {
	public OurException(string msg) : base("WicoOrbital" + ": " + msg) {}
}

Dictionary < string,
int > modeCommands = new Dictionary < string,
int > ();

#region tanks
List < IMyTerminalBlock > tankList = new List < IMyTerminalBlock > ();
const int iTankOxygen = 1;
const int iTankHydro = 2;
int iHydroTanks = 0;
int iOxygenTanks = 0;
string tanksInit() {
	{
		tankList = new List < IMyTerminalBlock > ();
		GridTerminalSystem.GetBlocksOfType < IMyOxygenTank > (tankList, (x = >x.CubeGrid == Me.CubeGrid));
	}
	iHydroTanks = 0;
	iOxygenTanks = 0;
	for (int i = 0; i < tankList.Count; ++i) {
		if (tankType(tankList[i]) == iTankOxygen) iOxygenTanks++;
		else if (tankType(tankList[i]) == iTankHydro) iHydroTanks++;
	}
	return "T" + tankList.Count.ToString("00");
}
double tanksFill(int iTypes = 0xff) {
	double totalPercent = 0;
	int iTanksCount = 0;
	for (int i = 0; i < tankList.Count; ++i) {
		int iTankType = tankType(tankList[i]);
		if ((iTankType & iTypes) > 0) {
			IMyOxygenTank tank = tankList[i] as IMyOxygenTank;
			float tankLevel = tank.GetOxygenLevel();
			totalPercent += tankLevel;
			iTanksCount++;
		}
	}
	if (iTanksCount > 0) {
		return totalPercent * 100 / iTanksCount;
	}
	else return 0;
}
int tankType(IMyTerminalBlock theBlock) {
	if (theBlock is IMyOxygenTank) {
		if (theBlock.BlockDefinition.SubtypeId.Contains("Hydro")) return iTankHydro;
		else return iTankOxygen;
	}
	return 0;
}

#endregion

#region lights

List < IMyTerminalBlock > lightsList = new List < IMyTerminalBlock > ();

string lightsInit() {
	//	if(wheelList.Count<1)  
	{
		lightsList = new List < IMyTerminalBlock > ();
		GridTerminalSystem.GetBlocksOfType < IMyLightingBlock > (lightsList, (x = >x.CubeGrid == Me.CubeGrid));
	}

	return "L" + lightsList.Count.ToString("00");
}

void setLightColor(List < IMyTerminalBlock > lightsList, Color c) {
	for (int i = 0; i < lightsList.Count; i++) {
		var light = lightsList[i] as IMyLightingBlock;
		if (light == null) continue;
		if (light.GetValue < Color > ("Color").Equals(c) && light.Enabled) {
			continue;
		}
		light.SetValue("Color", c);
		// make sure we switch the color of the texture as well 
		light.ApplyAction("OnOff_Off");
		light.ApplyAction("OnOff_On");
	}
}

#endregion

#region gears

List < IMyTerminalBlock > gearList = new List < IMyTerminalBlock > ();
string gearsInit() {
	{
		gearList = new List < IMyTerminalBlock > ();
		GridTerminalSystem.GetBlocksOfType < IMyLandingGear > (gearList, (x = >x.CubeGrid == Me.CubeGrid));
	}
	return "LG" + gearList.Count.ToString("00");
}
bool anyGearIsLocked() {
	for (int i = 0; i < gearList.Count; i++) {
		IMyLandingGear lGear;
		lGear = gearList[i] as IMyLandingGear;
		if (lGear != null && lGear.IsLocked) return true;
	}
	return false;
}#endregion#region thrusters
List < IMyTerminalBlock > thrustForwardList = new List < IMyTerminalBlock > ();
List < IMyTerminalBlock > thrustBackwardList = new List < IMyTerminalBlock > ();
List < IMyTerminalBlock > thrustDownList = new List < IMyTerminalBlock > ();
List < IMyTerminalBlock > thrustUpList = new List < IMyTerminalBlock > ();
List < IMyTerminalBlock > thrustLeftList = new List < IMyTerminalBlock > ();
List < IMyTerminalBlock > thrustRightList = new List < IMyTerminalBlock > ();

double thrustForward = 0;
double thrustBackward = 0;
double thrustDown = 0;
double thrustUp = 0;
double thrustLeft = 0;
double thrustRight = 0;

int ionThrustCount = 0;
int hydroThrustCount = 0;
int atmoThrustCount = 0;
const int thrustatmo = 1;
const int thrusthydro = 2;
const int thrustion = 4;
const int thrustAll = 0xff;
List < IMyTerminalBlock > thrustAllList = new List < IMyTerminalBlock > ();
readonly Matrix identityMatrix = new Matrix(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1);

string thrustersInit(IMyTerminalBlock orientationBlock) {
	thrustForwardList = new List < IMyTerminalBlock > ();
	thrustBackwardList = new List < IMyTerminalBlock > ();
	thrustDownList = new List < IMyTerminalBlock > ();
	thrustUpList = new List < IMyTerminalBlock > ();
	thrustLeftList = new List < IMyTerminalBlock > ();
	thrustRightList = new List < IMyTerminalBlock > ();
	thrustAllList = new List < IMyTerminalBlock > ();
	GridTerminalSystem.GetBlocksOfType < IMyThrust > (thrustAllList, (x = >x.CubeGrid == Me.CubeGrid));
	Matrix fromGridToReference;
	orientationBlock.Orientation.GetMatrix(out fromGridToReference);
	Matrix.Transpose(ref fromGridToReference, out fromGridToReference);

	thrustForward = 0;
	thrustBackward = 0;
	thrustDown = 0;
	thrustUp = 0;
	thrustLeft = 0;
	thrustRight = 0;

	for (int i = 0; i < thrustAllList.Count; ++i) {
		IMyThrust thruster = thrustAllList[i] as IMyThrust;
		Matrix fromThrusterToGrid;
		thruster.Orientation.GetMatrix(out fromThrusterToGrid);
		Vector3 accelerationDirection = Vector3.Transform(fromThrusterToGrid.Backward, fromGridToReference);
		int iThrustType = thrusterType(thrustAllList[i]);
		if (iThrustType == thrustatmo) atmoThrustCount++;
		else if (iThrustType == thrusthydro) hydroThrustCount++;
		else if (iThrustType == thrustion) ionThrustCount++;
		if (accelerationDirection == identityMatrix.Left) {
			thrustLeft += maxThrust((IMyThrust) thrustAllList[i]);
			thrustLeftList.Add(thrustAllList[i]);
		} else if (accelerationDirection == identityMatrix.Right) {
			thrustRight += maxThrust((IMyThrust) thrustAllList[i]);
			thrustRightList.Add(thrustAllList[i]);
		} else if (accelerationDirection == identityMatrix.Backward) {
			thrustBackward += maxThrust((IMyThrust) thrustAllList[i]);
			thrustBackwardList.Add(thrustAllList[i]);
		} else if (accelerationDirection == identityMatrix.Forward) {
			thrustForward += maxThrust((IMyThrust) thrustAllList[i]);
			thrustForwardList.Add(thrustAllList[i]);
		} else if (accelerationDirection == identityMatrix.Up) {
			thrustUp += maxThrust((IMyThrust) thrustAllList[i]);
			thrustUpList.Add(thrustAllList[i]);
		} else if (accelerationDirection == identityMatrix.Down) {
			thrustDown += maxThrust((IMyThrust) thrustAllList[i]);
			thrustDownList.Add(thrustAllList[i]);
		}
	}

	StatusLog("thrustUp=" + thrustUp.ToString("N0"), getTextBlock(longStatus), true);
	StatusLog("thrustDown=" + thrustDown.ToString("N0"), getTextBlock(longStatus), true);
	StatusLog("thrustLeft=" + thrustLeft.ToString("N0"), getTextBlock(longStatus), true);
	StatusLog("thrustRight=" + thrustRight.ToString("N0"), getTextBlock(longStatus), true);
	StatusLog("thrustBackward=" + thrustBackward.ToString("N0"), getTextBlock(longStatus), true);
	StatusLog("thrustForward=" + thrustForward.ToString("N0"), getTextBlock(longStatus), true);

	string s;
	s = ">";
	s += "F" + thrustForwardList.Count.ToString("00");
	s += "B" + thrustBackwardList.Count.ToString("00");
	s += "D" + thrustDownList.Count.ToString("00");
	s += "U" + thrustUpList.Count.ToString("00");
	s += "L" + thrustLeftList.Count.ToString("00");
	s += "R" + thrustRightList.Count.ToString("00");
	s += "<";
	return s;
}
int thrusterType(IMyTerminalBlock theBlock) {
	if (theBlock is IMyThrust) {
		if (theBlock.BlockDefinition.SubtypeId.Contains("Atmo")) return thrustatmo;
		else if (theBlock.BlockDefinition.SubtypeId.Contains("Hydro")) return thrusthydro;
		else return thrustion;
	}
	return 0;
}

double maxThrust(IMyThrust thruster) {
	//StatusLog(thruster.CustomName+":"+thruster.BlockDefinition.SubtypeId,getTextBlock(longStatus),true); 
	double max = 0;
	string s = thruster.BlockDefinition.SubtypeId;
	if (s.Contains("LargeBlock")) {
		if (s.Contains("LargeAtmo")) {
			max = 5400000;
		}
		else if (s.Contains("SmallAtmo")) {
			max = 420000;
		}
		else if (s.Contains("LargeHydro")) {
			max = 6000000;
		}
		else if (s.Contains("SmallHydro")) {
			max = 900000;
		}
		else if (s.Contains("LargeThrust")) {
			max = 3600000;
		}
		else if (s.Contains("SmallThrust")) {
			max = 288000;
		}
		else {
			StatusLog("Unknown Thrust Type", getTextBlock(longStatus), true);

			Echo("Unknown Thrust Type");
		}
	}
	else {
		//StatusLog("Small grid",getTextBlock(longStatus),true); 
		if (s.Contains("LargeAtmo")) {
			max = 408000;
		}
		else if (s.Contains("SmallAtmo")) {
			max = 80000;
		}
		else if (s.Contains("LargeHydro")) {
			max = 400000;
		}
		else if (s.Contains("SmallHydro")) {
			max = 82000;
		}
		else if (s.Contains("LargeThrust")) {
			max = 144000;
		}
		else if (s.Contains("SmallThrust")) {
			max = 12000;
		}
		else {
			StatusLog("Unknown Thrust Type", getTextBlock(longStatus), true);

			Echo("Unknown Thrust Type");
		}

	}
	return max;
}

double calculateMaxThrust(List < IMyTerminalBlock > thrusters, int iTypes = thrustAll) {
	double thrust = 0;
	for (int thrusterIndex = 0; thrusterIndex < thrusters.Count; thrusterIndex++) {
		int iThrusterType = thrusterType(thrusters[thrusterIndex]);
		if ((iThrusterType & iTypes) > 0) {
			IMyThrust thruster = thrusters[thrusterIndex] as IMyThrust;
			double dThrust = maxThrust(thruster);
			if (dGravity < 1.0) {
				if (iThrusterType == thrustatmo) dThrust = 0;
			}
			else {
				if (iThrusterType == thrustion) {
					double dAdjust = 1;
					dAdjust = (1 - dGravity);
					if (dAdjust < .3) dAdjust = .3;
					dThrust = dThrust * dAdjust;
				}
			}
			thrust += dThrust;
		}
	}

	return thrust;
}

bool calculateHoverThrust(List < IMyTerminalBlock > thrusters, out int atmoPercent, out int hydroPercent, out int ionPercent) {
	atmoPercent = 0;
	hydroPercent = 0;
	ionPercent = 0;
	double ionThrust = calculateMaxThrust(thrusters, thrustion);
	double atmoThrust = calculateMaxThrust(thrusters, thrustatmo);
	double hydroThrust = calculateMaxThrust(thrusters, thrusthydro);

	MyShipMass myMass;
	myMass = ((IMyShipController) anchorPosition).CalculateShipMass();
	double hoverthrust = 0;
	hoverthrust = myMass.TotalMass * dGravity * 9.810;

	if (atmoThrust > 0) {
		if (atmoThrust < hoverthrust) {
			atmoPercent = 100;
			hoverthrust -= atmoThrust;
		}
		else {
			atmoPercent = (int)(hoverthrust / atmoThrust * 100);
			if (atmoPercent > 0) hoverthrust -= (atmoThrust * atmoPercent / 100);
		}
	}
	Echo("ALeft over thrust=" + hoverthrust.ToString("N0"));

	if (ionThrust > 0 && hoverthrust > 0) {
		if (ionThrust < hoverthrust) {
			ionPercent = 100;
			hoverthrust -= ionThrust;
		}
		else {
			ionPercent = (int)(hoverthrust / ionThrust * 100);
			if (ionPercent > 0) hoverthrust -= ((ionThrust * ionPercent) / 100);
		}
	}
	Echo("ILeft over thrust=" + hoverthrust.ToString("N0"));

	if (hydroThrust > 0 && hoverthrust > 0) {
		if (hydroThrust < hoverthrust) {
			hydroPercent = 100;
			hoverthrust -= hydroThrust;
		}
		else {
			hydroPercent = (int)(hoverthrust / hydroThrust * 100);
			if (hydroPercent > 0) hoverthrust -= ((hydroThrust * hydroPercent) / 100);;
		}
	}
	Echo("Atmo=" + ((atmoThrust * atmoPercent) / 100).ToString("N0"));
	Echo("ion=" + ((ionThrust * ionPercent) / 100).ToString("N0"));
	Echo("hydro=" + ((hydroThrust * hydroPercent) / 100).ToString("N0"));
	Echo("Left over thrust=" + hoverthrust.ToString("N0"));
	if (hoverthrust > 0) return false;
	return true;
}

List < IMyTerminalBlock > findThrusters(string sGroup) {
	List < IMyTerminalBlock > lthrusters = new List < IMyTerminalBlock > ();
	List < IMyBlockGroup > groups = new List < IMyBlockGroup > ();
	GridTerminalSystem.GetBlockGroups(groups);
	for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++) {
		if (groups[groupIndex].Name == sGroup) {
			List < IMyTerminalBlock > thrusters = null;
			groups[groupIndex].GetBlocks(thrusters, (x = >x.CubeGrid == Me.CubeGrid));
			for (int thrusterIndex = 0; thrusterIndex < thrusters.Count; thrusterIndex++) {
				lthrusters.Add(thrusters[thrusterIndex]);
			}
			break;
		}
	}
	return lthrusters;
}
int powerUpThrusters(List < IMyTerminalBlock > thrusters, int iPower = 100, int iTypes = thrustAll) {
	int iCount = 0;
	if (iPower > 100) iPower = 100;
	for (int thrusterIndex = 0; thrusterIndex < thrusters.Count; thrusterIndex++) {
		int iThrusterType = thrusterType(thrusters[thrusterIndex]);
		if ((iThrusterType & iTypes) > 0) {
			IMyThrust thruster = thrusters[thrusterIndex] as IMyThrust;
			float maxThrust = thruster.GetMaximum < float > ("Override");
			if (!thruster.IsWorking) {
				thruster.ApplyAction("OnOff_On");
			}
			iCount += 1;
			thruster.SetValueFloat("Override", maxThrust * ((float) iPower / 100.0f));
		}
	}
	return iCount;
}
bool powerUpThrusters(string sFThrust, int iPower = 100, int iTypes = thrustAll) {
	if (iPower > 100) iPower = 100;
	List < IMyBlockGroup > groups = new List < IMyBlockGroup > ();
	GridTerminalSystem.GetBlockGroups(groups);
	for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++) {
		if (groups[groupIndex].Name == sFThrust) {
			List < IMyTerminalBlock > thrusters = null;
			groups[groupIndex].GetBlocks(thrusters, (x = >x.CubeGrid == Me.CubeGrid));
			return (powerUpThrusters(thrusters, iPower, iTypes) > 0);
		}
	}
	return false;
}
int powerDownThrusters(List < IMyTerminalBlock > thrusters, int iTypes = thrustAll, bool bForceOff = false) {
	int iCount = 0;
	for (int thrusterIndex = 0; thrusterIndex < thrusters.Count; thrusterIndex++) {
		int iThrusterType = thrusterType(thrusters[thrusterIndex]);
		if ((iThrusterType & iTypes) > 0) {
			iCount++;
			IMyThrust thruster = thrusters[thrusterIndex] as IMyThrust;
			thruster.SetValueFloat("Override", 0);
			if (thruster.IsWorking && bForceOff) thruster.ApplyAction("OnOff_Off");
			else if (!thruster.IsWorking && !bForceOff) thruster.ApplyAction("OnOff_On");
		}
	}
	return iCount;
}
bool powerDownThrusters(string sFThrust) {
	List < IMyBlockGroup > groups = new List < IMyBlockGroup > ();
	GridTerminalSystem.GetBlockGroups(groups);
	for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++) {
		if (groups[groupIndex].Name == sFThrust) {
			List < IMyTerminalBlock > thrusters = null;
			groups[groupIndex].GetBlocks(thrusters, (x = >x.CubeGrid == Me.CubeGrid));
			return (powerDownThrusters(thrusters) > 0);
		}
	}
	return false;
}
bool powerUpThrusters() {
	return (powerUpThrusters(thrustForwardList) > 0);
}
bool powerDownThrusters() {
	return (powerDownThrusters(thrustForwardList) > 0);
}
double currentOverrideThrusters(List < IMyTerminalBlock > theBlocks, int iTypes = thrustAll) {
	for (int i = 0; i < theBlocks.Count; i++) {
		int iThrusterType = thrusterType(theBlocks[i]);
		if ((iThrusterType & iTypes) > 0 && theBlocks[i].IsWorking) {
			IMyThrust thruster = theBlocks[i] as IMyThrust;
			float maxThrust = thruster.GetMaximum < float > ("Override");
			if (maxThrust > 0) return (double) thruster.ThrustOverride / maxThrust * 100;
		}
	}
	return 0;
}
bool areThrustersOn(List < IMyTerminalBlock > theBlocks, int iTypes = thrustAll) {
	for (int i = 0; i < theBlocks.Count; i++) {
		int iThrusterType = thrusterType(theBlocks[i]);
		if ((iThrusterType & iTypes) > 0 && theBlocks[i].IsWorking) {
			return true;
		}
	}
	return false;
}
string thrusterInfo() {
	string sResults = "";
	if ((craft_operation & CRAFT_MODE_ION) > 0) {
		sResults += "ION:";
		if (areThrustersOn(thrustAllList, thrustion)) {
			sResults += "ON";
		}
		else {
			sResults += "Off";
		}
		sResults += " Forward:";
		if (areThrustersOn(thrustForwardList, thrustion)) {
			sResults += "ON";
			sResults += "\n";
			sResults += progressBar(currentOverrideThrusters(thrustForwardList, thrustion));
		}
		else {
			sResults += "Off";
			sResults += "\n";
			sResults += progressBar(0);
		}
		sResults += "\n";
	}
	if ((craft_operation & CRAFT_MODE_ATMO) > 0) {
		sResults += "ATMO:";
		if (areThrustersOn(thrustAllList, thrustatmo)) {
			sResults += "ON";
		}
		else {
			sResults += "Off";
		}
		sResults += " Forward:";
		if (areThrustersOn(thrustForwardList, thrustatmo)) {
			sResults += "ON";
			sResults += "\n";
			sResults += progressBar(currentOverrideThrusters(thrustForwardList, thrustatmo));
		}
		else {
			sResults += "Off";
			sResults += "\n";
			sResults += progressBar(0);
		}
		sResults += "\n";
	}
	if ((craft_operation & CRAFT_MODE_HYDRO) > 0) {
		sResults += "HYRDO:";
		if (areThrustersOn(thrustAllList, thrusthydro)) {
			sResults += "ON";
		}
		else {
			sResults += "Off";
		}
		sResults += " Forward:";
		if (areThrustersOn(thrustForwardList, thrusthydro)) {
			sResults += "ON";
			sResults += "\n";
			sResults += progressBar(currentOverrideThrusters(thrustForwardList, thrusthydro));
		}
		else {
			sResults += "Off";
			sResults += "\n";
			sResults += progressBar(0);
		}
		sResults += "\n";
	}
	return sResults;
}

#endregion

#region logging

string longStatus = "Wico Craft Log";
string sTextPanelReport = "Craft Report";
IMyTextPanel statustextblock = null;
IMyTextPanel getTextBlock(string stheName) {
	IMyTextPanel textblock = null;
	List < IMyTerminalBlock > blocks = new List < IMyTerminalBlock > ();
	blocks = GetBlocksNamed < IMyTerminalBlock > (stheName);
	if (blocks.Count < 1) {
		blocks = GetBlocksContains < IMyTextPanel > (stheName);
		if (blocks.Count > 0) textblock = blocks[0] as IMyTextPanel;
	}
	else if (blocks.Count > 1) throw new OurException("Multiple status blocks found: \"" + stheName + "\"");
	else textblock = blocks[0] as IMyTextPanel;
	return textblock;
}
IMyTextPanel getTextStatusBlock(bool force_update = false) {
	if (statustextblock != null && !force_update) return statustextblock;
	statustextblock = getTextBlock(OurName + " Status");
	return statustextblock;
}
void StatusLog(string text, IMyTextPanel block, bool bReverse = false) {
	if (block == null) return;
	if (text.Equals("clear")) {
		block.WritePublicText("");
	} else {
		if (bReverse) {
			string oldtext = block.GetPublicText();
			block.WritePublicText(text + "\n" + oldtext);
		}
		else block.WritePublicText(text + "\n", true);
		//  block.WritePublicTitle(DateTime.Now.ToString()); 
	}
	block.ShowTextureOnScreen();
	block.ShowPublicTextOnScreen();

}

void Log(string text) {
	StatusLog(text, getTextStatusBlock());
}
string progressBar(double percent) {
	int barSize = 75;
	if (percent < 0) percent = 0;
	int filledBarSize = (int)(percent * barSize) / 100;
	if (filledBarSize > barSize) filledBarSize = barSize;
	string sResult = "[" + new String('|', filledBarSize) + new String('\'', barSize - filledBarSize) + "]";
	return sResult;
}

#endregion

#region MAIN

bool init = false;
bool bWasInit = false;
bool bWantFast = false;

bool bWorkingProjector = false;

List < IMyTerminalBlock > projectorList = new List < IMyTerminalBlock > ();

void Main(string sArgument) {
	bWantFast = false;
	bWorkingProjector = false;
	projectorList.Clear();
	//	var list = new List<IMyTerminalBlock>(); 
	GridTerminalSystem.GetBlocksOfType < IMyProjector > (projectorList, (x = >x.CubeGrid == Me.CubeGrid));
	for (int i = 0; i < projectorList.Count; i++) {
		if (projectorList[i].IsWorking) {

			Echo("Working local Projector found!");
			init = false;
			sInitResults = "";
			bWorkingProjector = true;
		}
	}

	string output = "";

	if (sArgument != "" && sArgument != "timer") Echo("Arg=" + sArgument);

	if (sArgument == "init") {
		sInitResults = "";
		init = false;
		currentInit = 0;

	}

	//	Log("clear"); 
	//	StatusLog("clear",getTextBlock(sTextPanelReport)); 

	if (!init) {
		if (bWorkingProjector) {
			//	Log("Construction in Progress\nTurn off projector to continue"); 
			StatusLog("clear", getTextBlock(sTextPanelReport));

			StatusLog(moduleName + ":Construction in Progress\nTurn off projector to continue", getTextBlock(sTextPanelReport));
		}
		else bWantFast = true;
		doInit();
		bWasInit = true;
	}
	else {
		if (bWasInit) StatusLog(DateTime.Now.ToString() + " " + OurName + ":" + sInitResults, getTextBlock(longStatus), true);

		if (SaveFile == null) { // No save file to share state! 
			StatusLog(moduleName + ":No Save file!\nCannot get saved information from main module. Check Blueprint/damage\n" + SAVE_FILE_NAME, getTextBlock(sTextPanelReport));
			Echo(SAVE_FILE_NAME + " (TextPanel) is missing or Named incorrectly. ");
			/* 
		sInitResults=""; 
		init=false; 
		currentInit=0; 
 
return; 
 */
		}

		Deserialize();

		//Log(craftOperation()); 
		Echo(craftOperation());
		Echo(sArgResults);
		IMyTerminalBlock anchorOrientation = gpsCenter;
		if (anchorOrientation != null) {
			Matrix mTmp;
			anchorOrientation.Orientation.GetMatrix(out mTmp);
			mTmp *= -1;
			iForward = new Vector3I(mTmp.Forward);
			iUp = new Vector3I(mTmp.Up);
			iLeft = new Vector3I(mTmp.Left);
		}
		Vector3D mLast = vCurrentPos;
		vCurrentPos = gpsCenter.GetPosition();

		/* 
		if(gpsCenter is IMyRemoteControl) 
		{ 
			Vector3D vNG=((IMyRemoteControl)gpsCenter).GetNaturalGravity(); 
			double dLength=vNG.Length(); 
			dGravity=dLength/9.81; 
		} 
		else dGravity=-1.0; 
 */

		if (processArguments(sArgument)) return;
		/* 
		if(AnyConnectorIsConnected())	output+="Connected"; 
		else output+="Not Connected"; 
	 
		if(AnyConnectorIsLocked())	output+="\nLocked"; 
		else output+="\nNot Locked"; 
*/
		Echo(output);
		//		Log(output); 
		if (bWantFast) Echo("FAST!");

		/* Done in main module 
		if(dGravity>=0) 
		{ 
			Echo("Grav="+dGravity.ToString(velocityFormat)); 
			Log("Planet Gravity "+dGravity.ToString(velocityFormat)+" g"); 
		Log(progressBar((int)(dGravity/1.1*100))); 
 
 
		} 
		else Log("ERROR: No Remote Control found!"); 
*/
		doCargoCheck();
		Echo("Cargo=" + cargopcent.ToString() + "%");
		//Log("C:"+progressBar(cargopcent)); 
		batteryCheck(0, false);
		//Log("B:"+progressBar(batteryPercentage)); 
		//if(iOxygenTanks>0) Log("O:" +progressBar(tanksFill(iTankOxygen))); 
		//if(iHydroTanks>0) Log("H:" +progressBar(tanksFill(iTankHydro))); 
		Echo("Batteries=" + batteryPercentage.ToString() + "%");

		logState();
		doModes();
	}
	if (bWantFast) doSubModuleTimerTriggers("[WCCT]");

	Serialize();
	//	doSubModuleTimerTriggers(); 
	bWasInit = false;

	Echo(sInitResults); // make it show last 

	//	verifyAntenna(); 
}#endregion

#region logstate
void logState() {
	string s;
	string s2;
	double dist;

	string sShipName = "";

	List < IMyTerminalBlock > Antenna = new List < IMyTerminalBlock > ();
	GridTerminalSystem.GetBlocksOfType < IMyRadioAntenna > (Antenna, (x = >x.CubeGrid == Me.CubeGrid));
	if (Antenna.Count > 0) sShipName = Antenna[0].CustomName.Split('!')[0].Trim();

	StatusLog("clear", gpsPanel);

	s = "Home";

	if (bValidLaunch1) {
		s2 = "GPS:" + sShipName + " Docking Entry:" + Vector3DToString(vLaunch1) + ":";
		StatusLog(s2, gpsPanel);
	}

	if (bValidDock) {
		s2 = "GPS:" + sShipName + " Dock:" + Vector3DToString(vDock) + ":";
		StatusLog(s2, gpsPanel);
	}

	if (bValidHome) {
		dist = (vCurrentPos - vHome).Length();
		s += ": " + dist.ToString("0") + "m";
		s2 = "GPS:" + sShipName + " Home Entry:" + Vector3DToString(vHome) + ":";
		StatusLog(s2, gpsPanel);
	}
	else s += ": NOT SET";
	s2 = "GPS:" + sShipName + " Current Position:" + Vector3DToString(remoteControl.GetPosition()) + ":";
	StatusLog(s2, gpsPanel);

}#endregion

#region domodes
void doModes() {
	Echo("mode=" + iMode.ToString());

	if ((craft_operation & CRAFT_MODE_PET) > 0 && iMode != MODE_PET) setLightColor(lightsList, Color.Chocolate);

	if (AnyConnectorIsConnected() && !((craft_operation & CRAFT_MODE_ORBITAL) > 0)) {
		Echo("DM:docked");
		setMode(MODE_DOCKED);
	}
	if (iMode == MODE_IDLE) doModeIdle();
	//	else if (iMode == MODE_HOVER) doModeHover(); 
	//	else if (iMode == MODE_LAUNCHPREP) doModeLaunchprep(); 
	//	else if (iMode == MODE_INSPACE) doModeInSpace(); 
	//	else if (iMode == MODE_LANDED) doModeLanded(); 
	//	else if (iMode == MODE_ORBITALLAUNCH) doModeOrbitalLaunch(); 
	else if (iMode == MODE_DESCENT) doModeDescent();
}#endregion

#region maininit

string sInitResults = "";
string sArgResults = "";

int currentInit = 0;

string doInit() {

	// initialization of each module goes here: 
	// when all initialization is done, set init to true. 
	Log("Init:" + currentInit.ToString());
	double progress = currentInit * 100 / 3;
	string sProgress = progressBar(progress);
	StatusLog(moduleName + sProgress, getTextBlock(sTextPanelReport));

	Echo("Init");
	if (currentInit == 0) {
		//StatusLog("clear",getTextBlock(longStatus),true); 
		StatusLog(DateTime.Now.ToString() + " " + OurName + ":" + moduleName + ":INIT", getTextBlock(longStatus), true);

		//	if(!modeCommands.ContainsKey("launchprep")) modeCommands.Add("launchprep", MODE_LAUNCHPREP); 
		//	if(!modeCommands.ContainsKey("orbitallaunch")) modeCommands.Add("orbitallaunch", MODE_ORBITALLAUNCH); 
		if (!modeCommands.ContainsKey("orbitaldescent")) modeCommands.Add("orbitaldescent", MODE_DESCENT);

		sInitResults += initSerializeCommon();
		Deserialize();
	}
	else if (currentInit == 1) {
		sInitResults += BlockInit();
		anchorPosition = gpsCenter;
		currentPosition = anchorPosition.GetPosition();

		sInitResults += thrustersInit(gpsCenter);
		/* 
	} 
	else if(currentInit==2) 
	{ 
 */
		sInitResults += gearsInit();
		sInitResults += tanksInit();

		sInitResults += NAVInit();
		sInitResults += gyrosetup();
		sInitResults += doorsInit();

		Deserialize();

		//		autoConfig(); 
		bWantFast = false;
		sInitResults += modeOnInit(); // handle mode initializting from load/recompile.. 
		init = true;
	}

	currentInit++;
	if (init) currentInit = 0;

	Log(sInitResults);

	return sInitResults;

}

IMyTextPanel gpsPanel = null;

string BlockInit() {
	string sInitResults = "";

	List < IMyTerminalBlock > centerSearch = new List < IMyTerminalBlock > ();
	GridTerminalSystem.SearchBlocksOfName(sGPSCenter, centerSearch, (x = >x.CubeGrid == Me.CubeGrid));
	//	GridTerminalSystem.SearchBlocksOfName(sGPSCenter, centerSearch); 
	if (centerSearch.Count == 0) {
		centerSearch = GetBlocksContains < IMyRemoteControl > ("[NAV]");
		if (centerSearch.Count == 0) {
			GridTerminalSystem.GetBlocksOfType < IMyRemoteControl > (centerSearch, (x = >x.CubeGrid == Me.CubeGrid));
			if (centerSearch.Count == 0) {
				GridTerminalSystem.GetBlocksOfType < IMyShipController > (centerSearch, (x = >x.CubeGrid == Me.CubeGrid));
				if (centerSearch.Count == 0) {
					sInitResults += "!!NO Controller";
					Echo("No Controller found");
					return sInitResults;
				}
				else {
					sInitResults += "S";
					Echo("Using first ship Controller found: " + centerSearch[0].CustomName);
				}
			}
			else {
				sInitResults += "R";
				Echo("Using First Remote control found: " + centerSearch[0].CustomName);
			}
		}
	}
	else {
		sInitResults += "N";
		Echo("Using Named: " + centerSearch[0].CustomName);
	}
	gpsCenter = centerSearch[0];

	List < IMyTerminalBlock > blocks = new List < IMyTerminalBlock > ();
	blocks = GetBlocksContains < IMyTextPanel > ("[GPS]");
	if (blocks.Count > 0) gpsPanel = blocks[0] as IMyTextPanel;

	return sInitResults;
}

#endregion

#region arguments

bool processArguments(string sArgument) {
	//	string output=""; 
	string[] args = sArgument.Trim().Split(' ');

	if (args[0] == "timer") {
		// do nothing for sub-module 
	}
	else if (args[0] == "setmaxspeed") {
		if (args.Length < 2) {
			Echo("Invalid arg");
			return false;
		}
		float fValue = 0;
		bool fOK = float.TryParse(args[1].Trim(), out fValue);
		if (!fOK) {
			Echo("invalid float value:" + args[1]);
			return false;
		}
		fMaxMps = fValue;
		sArgResults = "max speed set to " + fMaxMps.ToString() + "mps";

	}
	else if (args[0] == "wccs" || args[0] == "") {

}
	else {
		int iDMode;
		if (modeCommands.TryGetValue(args[0].ToLower(), out iDMode)) {
			sArgResults = "mode set to " + iDMode;
			setMode(iDMode);
			//			return true; 
		}
		else {
			sArgResults = "Unknown argument:" + args[0];
		}
	}
	return false; // keep processing in main 
}#endregion

#region ORIBITALMODES

float fMaxMps = 100;
//double dStartingGravity=0; 
//double dAtmoCrossOver=7000; 

// we have entered gravity well 
// 0=initialize 
// 10=dampeners on. aim towards target 
// 11=aligned check 
// 20=dampeners on. minor thrust fowards to align motion to target 
// 21 hold alignment 
// 22 hold alignment 
// 23 hold alignment 
// 30=dampeners off 
// 40=free-falll. continue alignment. when in range for 180. start 180 "CMD: !;V" 
// 60= check for 180 completed 
// 70=check for in retro-burn range of target in range; Dampeners on 
// 90=wait for zero velocity 
// 100 ... user control.. 
// 200 orient top toward location 
// 201 thrust toward location 
// 202 'over' location 
// 203 >1k 'over' location 
// 204 >500 'over' location 
// 205 >100 'over' location 
// 206 >25 'over' location 
// 207 final descent 

//bool bOverTarget=false; 
void doModeDescent() {
	StatusLog("clear", getTextBlock(sTextPanelReport));
	StatusLog(OurName + ":" + moduleName + ":Descent", getTextBlock(sTextPanelReport));
	StatusLog("Gravity=" + dGravity.ToString(velocityFormat), getTextBlock(sTextPanelReport));
	double alt = 0;
	double halt = 0;

	Vector3D vTarget;
	bool bValidTarget = false;
	if (bValidLaunch1) {
		bValidTarget = true;
		vTarget = vLaunch1;
	}
	else if (bValidHome) {
		bValidTarget = true;
		vTarget = vHome;
	}
	else {
		StatusLog(OurName + ":" + moduleName + ":Cannot Descend: No Waypoint present.", getTextBlock(sTextPanelReport));
		setMode(MODE_IDLE);
		return;
	}

	if (bValidTarget) {

		alt = (vCurrentPos - vTarget).Length();

		StatusLog("Altitude: " + alt.ToString("0") + " Meters", getTextBlock(sTextPanelReport));

		if (dGravity > 0) {
			if (gpsCenter is IMyRemoteControl) {
				Vector3D vNG = ((IMyRemoteControl) gpsCenter).GetNaturalGravity();

				//double Pitch,Yaw; 
				Vector3D groundPosition;
				groundPosition = gpsCenter.GetPosition();
				vNG.Normalize();
				groundPosition += vNG * alt;

				halt = (groundPosition - vTarget).Length();
				StatusLog("Hor distance: " + halt.ToString("0.00") + " Meters", getTextBlock(sTextPanelReport));

			}
		}
	}
	Echo("Descent Mode:" + current_state.ToString());

	double progress = 0;
	if (velocityShip <= 0) progress = 0;
	else if (velocityShip > fMaxMps) progress = 100;
	else progress = ((velocityShip - 0) / (fMaxMps - 0) * 100.0f);

	string sProgress = progressBar(progress);
	StatusLog("V:" + sProgress, getTextBlock(sTextPanelReport));
	batteryCheck(0, false);
	StatusLog("B:" + progressBar(batteryPercentage), getTextBlock(sTextPanelReport));
	if (iOxygenTanks > 0) StatusLog("O:" + progressBar(tanksFill(iTankOxygen)), getTextBlock(sTextPanelReport));
	if (iHydroTanks > 0) StatusLog("H:" + progressBar(tanksFill(iTankHydro)), getTextBlock(sTextPanelReport));

	string sOrientation = "";
	if ((craft_operation & CRAFT_MODE_ROCKET) > 0) sOrientation = "rocket";

	IMyShipController imsc = gpsCenter as IMyShipController;
	if (imsc != null && imsc.DampenersOverride) {
		StatusLog("DampenersOverride ON", getTextBlock(sTextPanelReport));

		Echo("DampenersOverride ON");
	}
	else {
		StatusLog("DampenersOverride OFF", getTextBlock(sTextPanelReport));
		Echo("DampenersOverride OFF");
	}

	if (AnyConnectorIsConnected()) {
		setMode(MODE_LAUNCHPREP);
		return;
	}
	if (AnyConnectorIsLocked()) {
		ConnectAnyConnectors(true);
		blockApplyAction(gearList, "Lock");
	}

	if (thrustStage1UpList.Count < 1) {
		if ((craft_operation & CRAFT_MODE_ROCKET) > 0) {
			thrustStage1UpList = thrustForwardList;
			thrustStage1DownList = thrustBackwardList;
		}
		else {
			//Echo("Setting thrustStage1UpList"); 
			thrustStage1UpList = thrustUpList;
			thrustStage1DownList = thrustDownList;
		}
	}

	if (current_state == 0) {
		if (imsc != null && imsc.DampenersOverride) blockApplyAction(gpsCenter, "DampenersOverride"); //DampenersOverride 
		ConnectAnyConnectors(false, "OnOff_On");
		if (!bValidTarget) {
			StatusLog("No target landing waypoint set.", getTextBlock(sTextPanelReport));
			setMode(MODE_IDLE);
		}
		else current_state = 10;
	}
	if (current_state == 10) {
		//		bOverTarget=false; 
		powerDownThrusters(thrustStage1DownList, thrustAll, true);
		startNavWaypoint(vTarget, true);
		current_state = 11;
	}
	if (current_state == 11) {
		string sStatus = navStatus.CustomName;
		StatusLog("Waiting for alignment with launch location" + dGravity.ToString(velocityFormat), getTextBlock(sTextPanelReport));

		if (sStatus.Contains("Shutdown")) { // somebody hit nav override 
			current_state = 0;
		}
		if (sStatus.Contains("Done")) {
			current_state = 20;
		}
	}
	if (current_state == 20) {

		StatusLog("Move towards launch location", getTextBlock(sTextPanelReport));
		if (imsc != null && !imsc.DampenersOverride) blockApplyAction(gpsCenter, "DampenersOverride");
		//		current_state=30; 
		if (dGravity <= 0 || velocityShip < (fMaxMps * .8)) powerUpThrusters(thrustForwardList, 5);
		else powerDownThrusters(thrustForwardList);
		powerDownThrusters(thrustBackwardList, thrustAll, true);
		if (velocityShip > 50 && dGravity > 0) current_state = 30;
		return;
	}
	if (current_state == 21) {
		StatusLog("Alignment", getTextBlock(sTextPanelReport));
		current_state = 22;
		return; // give at least one tick of dampeners 
	}
	if (current_state == 22) {
		StatusLog("Alignment", getTextBlock(sTextPanelReport));
		current_state = 23;
		return; // give at least one tick of dampeners 
	}
	if (current_state == 23) {
		StatusLog("Alignment", getTextBlock(sTextPanelReport));
		current_state = 30;
		return; // give at least one tick of dampeners 
	}
	if (current_state == 30) {
		powerDownThrusters(thrustStage1DownList, thrustAll, true);

		if (imsc != null && imsc.DampenersOverride) blockApplyAction(gpsCenter, "DampenersOverride");
		current_state = 40;
	}
	if (current_state == 40) {
		StatusLog("Free Fall", getTextBlock(sTextPanelReport));
		if (imsc != null && imsc.DampenersOverride) blockApplyAction(gpsCenter, "DampenersOverride");

		powerDownThrusters(thrustStage1UpList);
		if (alt < startReverseAlt) {
			//			startNavCommand("!;V"); 
			current_state = 60;
		}
		else if (alt > 44000 && alt < 45000) current_state = 10; // re-align 
		else if (alt > 34000 && alt < 35000) current_state = 10; // re-align 
		else if (alt > 24000 && alt < 25000) current_state = 10; // re-align 
		else if (alt > 14000 && alt < 15000) current_state = 10; // re-align 
	}
	if (current_state == 60) {
		string sStatus = navStatus.CustomName;
		StatusLog("Waiting for reverse alignment with travel vector", getTextBlock(sTextPanelReport));

		if (imsc != null && imsc.DampenersOverride) blockApplyAction(gpsCenter, "DampenersOverride");

		GyroMain(sOrientation);

		for (int i = 0; i < gyros.Count; ++i) {
			IMyGyro g = gyros[i] as IMyGyro;

			float maxPitch = g.GetMaximum < float > ("Pitch");
			g.SetValueFloat("Pitch", maxPitch);
			g.SetValueFloat("Yaw", 0);
			g.SetValueFloat("Roll", 0);
			g.SetValueBool("Override", true);
		}

		current_state = 61;
		return;
	}

	if (current_state == 61) {
		if (GyroMain(sOrientation)) {
			current_state = 70;
		}

	}
	if (current_state == 70) {
		StatusLog("Waiting for range for retro-thrust", getTextBlock(sTextPanelReport));
		GyroMain(sOrientation);
		if (dGravity > .9) {
			powerDownThrusters(thrustAllList, thrustatmo, false);
			powerDownThrusters(thrustAllList, thrusthydro, false);
			powerDownThrusters(thrustAllList, thrustion, true);
		}
		else if (dGravity < .5) {
			powerDownThrusters(thrustAllList, thrustatmo, true);
			powerDownThrusters(thrustAllList, thrusthydro, false);
			powerDownThrusters(thrustAllList, thrustion, false);
		}

		if (alt < retroStartAlt) {
			if (imsc != null && !imsc.DampenersOverride) blockApplyAction(gpsCenter, "DampenersOverride");
			current_state = 90;
		}
	}
	double roll = CalculateRoll(vTarget, gpsCenter);
	string s;
	s = "Roll=" + roll.ToString("0.00");
	Echo(s);
	StatusLog(s, getTextBlock(sTextPanelReport));

	if (current_state == 90) {
		StatusLog("RETRO! Waiting for ship to slow", getTextBlock(sTextPanelReport));
		if (velocityShip < 1) current_state = 200;
		if (velocityShip < 50) if ((craft_operation & CRAFT_MODE_NOAUTOGYRO) > 0) StatusLog("Wico Gyro Alignment OFF", getTextBlock(sTextPanelReport));
		else {
			GyroMain(sOrientation);
			DoRoll(roll);
			bWantFast = true;
		}
	}

	if (current_state == 100) {
		StatusLog("Player control for final docking", getTextBlock(sTextPanelReport));
		if ((craft_operation & CRAFT_MODE_NOAUTOGYRO) > 0) StatusLog("Wico Gyro Alignment OFF", getTextBlock(sTextPanelReport));
		else {
			GyroMain(sOrientation);
		}
	}
	else if (current_state == 200) {
		StatusLog("Orient toward landing location", getTextBlock(sTextPanelReport));

		if ((craft_operation & CRAFT_MODE_NOAUTOGYRO) > 0) StatusLog("Wico Gyro Alignment OFF", getTextBlock(sTextPanelReport));
		else {
			GyroMain(sOrientation);
		}
		DoRoll(roll);
		if (roll < .01 && roll >= -.01) {
			current_state = 201;
		}
		else bWantFast = true;

	}
	else if (current_state == 201) {
		StatusLog("Moving toward landing location", getTextBlock(sTextPanelReport));
		if ((craft_operation & CRAFT_MODE_NOAUTOGYRO) > 0) StatusLog("Wico Gyro Alignment OFF", getTextBlock(sTextPanelReport));
		else {

			GyroMain(sOrientation);

		}
		s = "velocity=" + velocityShip.ToString("0.00");
		Echo(s);
		StatusLog(s, getTextBlock(sTextPanelReport));

		if (roll < .01 && roll >= -0.01) {
			DoRoll(roll);
			if (halt > 50) if (velocityShip > 75) powerUpThrusters(thrustUpList, 1);
			else powerUpThrusters(thrustUpList, 100);
			else if (halt > 25) if (velocityShip > 10) powerUpThrusters(thrustUpList, 1);
			else powerUpThrusters(thrustUpList, 75);
			else if (halt > 9) if (velocityShip > 5) powerUpThrusters(thrustUpList, 1);
			else powerUpThrusters(thrustUpList, 35);
			else {
				s = "Stop for roll only";
				Echo(s);
				StatusLog(s, getTextBlock(sTextPanelReport));

				powerDownThrusters(thrustUpList);
				gyrosOff();
				if (velocityShip < .01) current_state = 202;
			}
		}
		else {
			if (Math.Abs(roll) >= 1.0) {
				if (velocityShip < 0.01) current_state = 202;
			}
			powerDownThrusters(thrustUpList);
			DoRoll(roll);
			bWantFast = true;
		}

	}
	else if (current_state == 202) {
		Echo("down#=" + thrustStage1DownList.Count.ToString());
		Echo("alt=" + alt.ToString());
		StatusLog("Descending toward landing location", getTextBlock(sTextPanelReport));

		if ((craft_operation & CRAFT_MODE_NOAUTOGYRO) > 0) StatusLog("Wico Gyro Alignment OFF", getTextBlock(sTextPanelReport));
		else {
			//			if(alt>100) 
			GyroMain(sOrientation);
		}
		s = "velocity=" + velocityShip.ToString("0.00");
		Echo(s);
		StatusLog(s, getTextBlock(sTextPanelReport));

		if (roll < .01 && roll >= 0) {
			Echo("aiming target");
			bWantFast = true;

			DoRoll(roll);
			if (halt > 50) if (velocityShip > 25) powerUpThrusters(thrustUpList, 1);
			else powerUpThrusters(thrustUpList, 100);
			else if (halt > 25) if (velocityShip > 10) powerUpThrusters(thrustUpList, 1);
			else powerUpThrusters(thrustUpList, 75);
			else if (halt > 1) if (velocityShip > 2) powerUpThrusters(thrustUpList, 1);
			else powerUpThrusters(thrustUpList, 25);
			else {
				powerDownThrusters(thrustUpList);
				gyrosOff();
				if (velocityShip < .01) current_state = 202;
			}
		}
		else {
			powerDownThrusters(thrustUpList);
			DoRoll(roll);
			bWantFast = true;
		}

		if (halt < 5) {
			powerDownThrusters(thrustAllList);
			if (alt > 500) {
				if (velocityShip > 55) {
					powerDownThrusters(thrustAllList);
					//					powerDownThrusters(thrustStage1DownList); 
				}
				else // need to check for NO down thrusters.. 
				{
					powerUpThrusters(thrustStage1DownList, 100);
					powerDownThrusters(thrustStage1UpList, thrustAll, true);
				}
			}
			else if (alt > 100) {
				gyrosOff();
				GyroMain(sOrientation, vTarget - vCurrentPos);
				powerDownThrusters(thrustAllList);

				if (velocityShip > 20) powerDownThrusters(thrustStage1DownList);
				else powerUpThrusters(thrustStage1DownList, 65);
			}
			else if (alt > 20) {
				gyrosOff();
				GyroMain(sOrientation, vTarget - vCurrentPos);

				if (velocityShip > 5) powerDownThrusters(thrustStage1DownList);
				else powerUpThrusters(thrustStage1DownList, 15);
			}
			else if (alt > 1) {
				gyrosOff();
				GyroMain(sOrientation, vTarget - vCurrentPos);

				if (bValidLaunch1) {
					/* 
					// we are close.. move back/forth until we find connector lock (checked elsewhere). 
					powerDownThrusters(thrustAllList); 
					if(Math.Abs(roll)>=1.0) 
					{ 
						powerUpThrusters(thrustUpList,5); 
					} 
					else if(Math.Abs(roll)<=0.01) 
					{ 
						powerUpThrusters(thrustDownList,5); 
					} 
 */
					if (velocityShip > 1) powerDownThrusters(thrustStage1DownList);
					else powerUpThrusters(thrustStage1DownList, 10);
				}
				else {
					// we had started from hover.. 
					setMode(MODE_HOVER);
					powerDownThrusters(thrustStage1DownList);
					gyrosOff();
				}
			}
			else {
				powerDownThrusters(thrustStage1DownList);
				gyrosOff();
			}
		}
		else {
			powerDownThrusters(thrustStage1DownList);
		}
	}
	else if (current_state == 203) {} else if (current_state == 204) {} else if (current_state == 205) {} else if (current_state == 206) {} else if (current_state == 207) {}

}

List < IMyTerminalBlock > thrustStage1UpList = new List < IMyTerminalBlock > ();
List < IMyTerminalBlock > thrustStage1DownList = new List < IMyTerminalBlock > ();
//List<IMyTerminalBlock>thrustStage2UpList=new List<IMyTerminalBlock>(); 

#endregion

#region modes

int iMode = 0;

const int MODE_IDLE = 0;
const int MODE_SEARCH = 1; // old search method.. 
const int MODE_MINE = 2;
const int MODE_ATTENTION = 3;
const int MODE_WAITINGCARGO = 4; // waiting for cargo to clear before mining. 
const int MODE_LAUNCH = 5;
//const int MODE_TARGETTING = 6; // targetting mode to allow remote setting of asteroid target 
const int MODE_GOINGTARGET = 7; // going to target asteroid 
const int MODE_GOINGHOME = 8;
const int MODE_DOCKING = 9;
const int MODE_DOCKED = 13;

const int MODE_SEARCHORIENT = 10; // orient to entrance location 
const int MODE_SEARCHSHIFT = 11; // shift to new lcoation 
const int MODE_SEARCHVERIFY = 12; // verify asteroid in front (then mine)' 
const int MODE_RELAUNCH = 14;
const int MODE_SEARCHCORE = 15; // go to the center of asteroid and search from the core. 

const int MODE_HOVER = 16;
const int MODE_LAND = 17;
const int MODE_MOVE = 18;
const int MODE_LANDED = 19;

const int MODE_DUMBNAV = 20;

const int MODE_SLEDMMOVE = 21;
const int MODE_SLEDMRAMPD = 22;
const int MODE_SLEDMLEVEL = 23;
const int MODE_SLEDMDRILL = 24;
const int MODE_SLEDMBDRILL = 25;

const int MODE_LAUNCHPREP = 26; // oribital launch prep 
const int MODE_INSPACE = 27; // now in space (no gravity) 
const int MODE_ORBITALLAUNCH = 28; // start orbital launch 
const int MODE_DESCENT = 29;

const int MODE_PET = 111; // pet that follows the player 
const string sgRL = "Running Lights";
const string sgML = "Mining Lights";

void setMode(int newMode) {
	if (iMode == newMode) return;
	// process delta mode 
	if (newMode == MODE_IDLE) {

}
	iMode = newMode;
	current_state = 0;
}

string modeOnInit() {

	return ">";
}#endregion

#region getblocks
IMyTerminalBlock get_block(string name) {
	IMyTerminalBlock block;
	block = (IMyTerminalBlock) GridTerminalSystem.GetBlockWithName(name);
	if (block == null) throw new Exception(name + " Not Found");
	return block;
}

public List < IMyTerminalBlock > GetTargetBlocks < T > (string Keyword = null) where T: class {
	List < IMyTerminalBlock > Output = new List < IMyTerminalBlock > ();
	List < IMyTerminalBlock > Blocks = new List < IMyTerminalBlock > ();
	GridTerminalSystem.GetBlocksOfType < T > (Blocks, (x = >x.CubeGrid == Me.CubeGrid));
	for (int e = 0; e < Blocks.Count; e++) {
		if ((Keyword == null) || (Keyword != null && Blocks[e].CustomName.StartsWith(Keyword))) {
			Output.Add(Blocks[e]);
		}
	}
	return Output;
}
public List < IMyTerminalBlock > GetBlocksContains < T > (string Keyword = null) where T: class {
	List < IMyTerminalBlock > Output = new List < IMyTerminalBlock > ();
	List < IMyTerminalBlock > Blocks = new List < IMyTerminalBlock > ();
	GridTerminalSystem.GetBlocksOfType < T > (Blocks, (x = >x.CubeGrid == Me.CubeGrid));
	for (int e = 0; e < Blocks.Count; e++) {
		if (Keyword != null && Blocks[e].CustomName.Contains(Keyword)) {
			Output.Add(Blocks[e]);
		}
	}
	return Output;
}
public List < IMyTerminalBlock > GetBlocksNamed < T > (string Keyword = null) where T: class {
	List < IMyTerminalBlock > Output = new List < IMyTerminalBlock > ();
	List < IMyTerminalBlock > Blocks = new List < IMyTerminalBlock > ();
	GridTerminalSystem.GetBlocksOfType < T > (Blocks, (x = >x.CubeGrid == Me.CubeGrid));
	for (int e = 0; e < Blocks.Count; e++) {
		if (Keyword != null && Blocks[e].CustomName == Keyword) {
			Output.Add(Blocks[e]);
		}
	}
	return Output;
}

#endregion

#region connectors
List < IMyTerminalBlock > localConnectors = new List < IMyTerminalBlock > ();

bool AnyConnectorIsLocked() {
	if (localConnectors.Count < 1) localConnectors = GetTargetBlocks < IMyShipConnector > (); //List<IMyTerminalBlock> blocks = GetTargetBlocks<IMyShipConnector>(); 
	for (int i = 0; i < localConnectors.Count; i++) {
		IMyShipConnector sc = localConnectors[i] as IMyShipConnector;
		if (sc.IsLocked) return true;
	}
	return false;
}

bool AnyConnectorIsConnected() {
	if (localConnectors.Count < 1) localConnectors = GetTargetBlocks < IMyShipConnector > (); //List<IMyTerminalBlock> blocks = GetTargetBlocks<IMyShipConnector>(); 
	for (int i = 0; i < localConnectors.Count; i++) {
		IMyShipConnector sc = localConnectors[i] as IMyShipConnector;
		if (sc.IsConnected) {
			IMyShipConnector sco = sc.OtherConnector;
			if (sco.CubeGrid == sc.CubeGrid) {
				//Echo("Locked-but connected to 'us'"); 
				continue;
			}
			else return true;
		}
	}
	return false;
}

void ConnectAnyConnectors(bool bConnect = true, string sAction = "") {
	if (localConnectors.Count < 1) localConnectors = GetTargetBlocks < IMyShipConnector > (); //List<IMyTerminalBlock> blocks = GetTargetBlocks<IMyShipConnector>(); 
	for (int i = 0; i < localConnectors.Count; i++) {
		IMyShipConnector sc = localConnectors[i] as IMyShipConnector;
		if (sc.IsConnected) {
			IMyShipConnector sco = sc.OtherConnector;
			if (sco.CubeGrid == sc.CubeGrid) {
				//Echo("Locked-but connected to 'us'"); 
				continue; // skip it. 
			}
		}
		if (bConnect) {
			if (sc.IsLocked && !sc.IsConnected) sc.ApplyAction("SwitchLock");
		}
		else {
			if (sc.IsConnected) sc.ApplyAction("SwitchLock");
		}

		if (sAction != "") {
			ITerminalAction ita;
			ita = sc.GetActionWithName(sAction);
			if (ita != null) ita.Apply(sc);
		}

	}
	return;
}

#endregion

#region modeidle
void ResetToIdle() {
	StatusLog(DateTime.Now.ToString() + " ACTION: Reset To Idle", getTextBlock(longStatus), true);
	ResetMotion();
	if (navCommand != null) if (! (navCommand is IMyTextPanel)) navCommand.SetCustomName("NAV: C Wico Craft");
	if (navStatus != null) navStatus.SetCustomName(sNavStatus + " Control Reset");
	// bValidPlayerPosition=false; 
	setMode(MODE_IDLE);
	if (AnyConnectorIsConnected()) setMode(MODE_DOCKED);
}
void doModeIdle() {
	//StatusLog("clear",getTextBlock(sTextPanelReport)); 
	StatusLog(moduleName + " Manual Control", getTextBlock(sTextPanelReport));
	if ((craft_operation & CRAFT_MODE_ORBITAL) > 0) {
		if (dGravity <= 0) {
			if (AnyConnectorIsConnected()) setMode(MODE_DOCKED);
			else setMode(MODE_INSPACE);
		}
		else setMode(MODE_HOVER);
		//		else setMode(MODE_LAUNCHPREP); 
	}
	/* 
 * else  
*/
	/* 
{ 
 
  if (bWantAutoGyro) 
   GyroMain(""); 
 } 
 */
}#endregion

void ResetMotion(bool bNoDrills = false) {
	if (navEnable != null) blockApplyAction(navEnable, "OnOff_Off"); //navEnable.ApplyAction("OnOff_Off"); 
	powerDownThrusters(thrustAllList);
	blockApplyAction(sRemoteControl, "AutoPilot_Off");
}

#region blockactions
void groupApplyAction(string sGroup, string sAction) {
	List < IMyBlockGroup > groups = new List < IMyBlockGroup > ();
	GridTerminalSystem.GetBlockGroups(groups);
	for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++) {
		if (groups[groupIndex].Name == sGroup) {
			List < IMyTerminalBlock > theBlocks = null;
			groups[groupIndex].GetBlocks(theBlocks, (x = >x.CubeGrid == Me.CubeGrid));;
			for (int iIndex = 0; iIndex < theBlocks.Count; iIndex++) {
				theBlocks[iIndex].ApplyAction(sAction);
			}
			return;
		}
	}
	return;
}
void listSetValueFloat(List < IMyTerminalBlock > theBlocks, string sProperty, float fValue) {
	for (int iIndex = 0; iIndex < theBlocks.Count; iIndex++) {
		if (theBlocks[iIndex].CubeGrid == Me.CubeGrid) theBlocks[iIndex].SetValueFloat(sProperty, fValue);
	}
	return;
}
void blockApplyAction(string sBlock, string sAction) {
	List < IMyTerminalBlock > blocks = new List < IMyTerminalBlock > ();
	blocks = GetBlocksNamed < IMyTerminalBlock > (sBlock);
	blockApplyAction(blocks, sAction);
}
void blockApplyAction(IMyTerminalBlock sBlock, string sAction) {
	ITerminalAction ita;
	ita = sBlock.GetActionWithName(sAction);
	if (ita != null) ita.Apply(sBlock);
	else Echo("Unsupported action:" + sAction);
}
void blockApplyAction(List < IMyTerminalBlock > lBlock, string sAction) {
	if (lBlock.Count > 0) {
		for (int i = 0; i < lBlock.Count; i++) {
			ITerminalAction ita;
			ita = lBlock[i].GetActionWithName(sAction);
			if (ita != null) ita.Apply(lBlock[i]);
			else Echo("Unsupported action:" + sAction);
		}
	}
}#endregion

#region triggers
//string[] aTriggerNames = {"Timer Block Wico Miner Status"}; 
//string[] aITriggerNames = {"Timer Block LCD","Timer Block BARABAS", "Timer Block Miner Utility"}; 
//string[] aFTriggerNames = {"Timer Block Wico Craft Control"}; 
void doTimerTriggers(string[] aNames) {
	foreach(string s in aNames) {
		IMyTimerBlock theTriggerTimer = null;

		List < IMyTerminalBlock > blocks = new List < IMyTerminalBlock > ();
		GridTerminalSystem.SearchBlocksOfName(s, blocks);

		if (blocks.Count > 1) Echo("Multiple blocks found: \"" + s + "\"");
		else if (blocks.Count == 0) Echo("Missing: " + s);

		for (int i = 0; i < blocks.Count; i++) {
			theTriggerTimer = blocks[i] as IMyTimerBlock;
			if (theTriggerTimer != null) theTriggerTimer.GetActionWithName("TriggerNow").Apply(theTriggerTimer);
		}
	}
}

void doSubModuleTimerTriggers(string sKeyword = "[WCCS]") {
	List < IMyTerminalBlock > blocks = GetBlocksContains < IMyTimerBlock > (sKeyword);

	IMyTimerBlock theTriggerTimer = null;
	blocks = GetBlocksContains < IMyTerminalBlock > (sKeyword);
	for (int i = 0; i < blocks.Count; i++) {
		theTriggerTimer = blocks[i] as IMyTimerBlock;
		if (theTriggerTimer != null) {
			Echo("dSMT:" + blocks[i].CustomName);
			theTriggerTimer.ApplyAction("TriggerNow");
		}
	}
}

#endregion

#region Autogyro
// http://forums.keenswh.com/threads/aligning-ship-to-planet-gravity.7373513/#post-1286885461 
double CTRL_COEFF = 0.5;
int LIMIT_GYROS = 3; // max number of gyros to use to align craft. Leaving some available allows for player control to continue during auto-align 
IMyRemoteControl rc;
List < IMyGyro > gyros;

float minAngleRad = 0.01f; // how tight to maintain horizontal Lower is tighter. 
bool GyroMain(string argument) {
	bool bAligned = true;
	if (rc == null) gyrosetup();

	Matrix or;
	rc.Orientation.GetMatrix(out or);
	Vector3D down;
	if (argument.ToLower().Contains("rocket")) down = or.Backward;
	else down = or.Down;

	Vector3D grav = rc.GetNaturalGravity();
	grav.Normalize();
	for (int i = 0; i < gyros.Count; ++i) {
		var g = gyros[i];
		g.Orientation.GetMatrix(out or);
		var localDown = Vector3D.Transform(down, MatrixD.Transpose(or));
		var localGrav = Vector3D.Transform(grav, MatrixD.Transpose(g.WorldMatrix.GetOrientation()));

		//Since the gyro ui lies, we are not trying to control yaw,pitch,roll but rather we 
		//need a rotation vector (axis around which to rotate) 
		var rot = Vector3D.Cross(localDown, localGrav);

		double ang = rot.Length();
		ang = Math.Atan2(ang, Math.Sqrt(Math.Max(0.0, 1.0 - ang * ang)));
		if (ang < minAngleRad) { // close enough 
			g.SetValueBool("Override", false);
			continue;
		}
		//		Echo("Auto-Level:Off level: "+(ang*180.0/3.14).ToString()+"deg"); 
		double ctrl_vel = g.GetMaximum < float > ("Yaw") * (ang / Math.PI) * CTRL_COEFF;
		ctrl_vel = Math.Min(g.GetMaximum < float > ("Yaw"), ctrl_vel);
		ctrl_vel = Math.Max(0.01, ctrl_vel);
		rot.Normalize();
		rot *= ctrl_vel;
		g.SetValueFloat("Pitch", (float) rot.GetDim(0));
		g.SetValueFloat("Yaw", -(float) rot.GetDim(1));
		g.SetValueFloat("Roll", -(float) rot.GetDim(2));
		g.SetValueFloat("Power", 1.0f);
		g.SetValueBool("Override", true);
		bAligned = false;
	}
	return bAligned;
}
bool GyroMain(string argument, Vector3D vDirection) {
	bool bAligned = true;
	if (rc == null) gyrosetup();

	Matrix or;
	rc.Orientation.GetMatrix(out or);
	Vector3D down;
	if (argument.ToLower().Contains("rocket")) down = or.Backward;
	else down = or.Down;

	//	Vector3D grav = rc.GetNaturalGravity(); 
	vDirection.Normalize();
	for (int i = 0; i < gyros.Count; ++i) {
		var g = gyros[i];
		g.Orientation.GetMatrix(out or);
		var localDown = Vector3D.Transform(down, MatrixD.Transpose(or));
		var localGrav = Vector3D.Transform(vDirection, MatrixD.Transpose(g.WorldMatrix.GetOrientation()));

		//Since the gyro ui lies, we are not trying to control yaw,pitch,roll but rather we 
		//need a rotation vector (axis around which to rotate) 
		var rot = Vector3D.Cross(localDown, localGrav);

		double ang = rot.Length();
		ang = Math.Atan2(ang, Math.Sqrt(Math.Max(0.0, 1.0 - ang * ang)));
		if (ang < minAngleRad) { // close enough 
			g.SetValueBool("Override", false);
			continue;
		}
		//		Echo("Auto-Level:Off level: "+(ang*180.0/3.14).ToString()+"deg"); 
		double ctrl_vel = g.GetMaximum < float > ("Yaw") * (ang / Math.PI) * CTRL_COEFF;
		ctrl_vel = Math.Min(g.GetMaximum < float > ("Yaw"), ctrl_vel);
		ctrl_vel = Math.Max(0.01, ctrl_vel);
		rot.Normalize();
		rot *= ctrl_vel;
		g.SetValueFloat("Pitch", (float) rot.GetDim(0));
		g.SetValueFloat("Yaw", -(float) rot.GetDim(1));
		g.SetValueFloat("Roll", -(float) rot.GetDim(2));
		g.SetValueFloat("Power", 1.0f);
		g.SetValueBool("Override", true);
		bAligned = false;
	}
	return bAligned;
}

string gyrosetup() {
	var l = new List < IMyTerminalBlock > ();
	rc = (IMyRemoteControl) GridTerminalSystem.GetBlockWithName(sGPSCenter);
	if (rc == null) {
		GridTerminalSystem.GetBlocksOfType < IMyRemoteControl > (l, x = >x.CubeGrid == Me.CubeGrid);
		if (l.Count < 1) return "No RC!";
		rc = (IMyRemoteControl) l[0];
	}
	GridTerminalSystem.GetBlocksOfType < IMyGyro > (l, x = >x.CubeGrid == Me.CubeGrid);
	gyros = l.ConvertAll(x = >(IMyGyro) x);
	if (gyros.Count > LIMIT_GYROS) gyros.RemoveRange(LIMIT_GYROS, gyros.Count - LIMIT_GYROS);
	return "G" + gyros.Count.ToString("00");
}
void gyrosOff() {
	for (int i = 0; i < gyros.Count; ++i) {
		gyros[i].SetValueBool("Override", false);
	}
}#endregion

#region serializecommon
const string SAVE_FILE_NAME = "Wico Craft Save";
float savefileversion = 1.66f;
IMyTextPanel SaveFile = null;

int current_state = 0;

Vector3D vCurrentPos;
//Vector3D vLastPos; 
Vector3D vDock;
Vector3D vLaunch1;
Vector3D vHome;
bool bValidDock = false;
bool bValidLaunch1 = false;
bool bValidHome = false;

bool bAutoRelaunch = false;

//DateTime dtStartShip; 
int iAtmoPower = 0;
int iHydroPower = 0;
int iIonPower = 0;

double dGravity = -2;

float fAssumeSimSpeed = 1.0f;

bool canthrowstone = true;

int batterypcthigh = 80;
int batterypctlow = 20;

int cargopctmin = 5;

int batteryPercentage = 100;

int cargopcent = 0;

int craft_operation = CRAFT_MODE_AUTO;

float fSimSpeed = 1.0f;
double velocityShip,
velocityForward,
velocityUp,
velocityLeft;

int currentRun = 0;

string initSerializeCommon() {

	string sInitResults = "S";
	//	if (SaveFile == null)  
	{
		List < IMyTerminalBlock > blocks = new List < IMyTerminalBlock > ();
		blocks = GetBlocksNamed < IMyTerminalBlock > (SAVE_FILE_NAME);

		if (blocks.Count > 1) throw new OurException("Multiple blocks found: \"" + SAVE_FILE_NAME + "\"");
		else if (blocks.Count == 0) Echo("Missing: " + SAVE_FILE_NAME);
		else SaveFile = blocks[0] as IMyTextPanel;
		if (SaveFile == null) {
			sInitResults = "-";
			Echo(SAVE_FILE_NAME + " (TextPanel) is missing or Named incorrectly. ");
		}
	}
	return sInitResults;
}
string Vector3DToString(Vector3D v) {
	string s;
	s = v.GetDim(0) + ":" + v.GetDim(1) + ":" + v.GetDim(2);
	return s;
}
bool ParseVector3d(string sVector, out double x, out double y, out double z) {
	string[] coordinates = sVector.Trim().Split(',');
	if (coordinates.Length < 3) {
		coordinates = sVector.Trim().Split(':');
	}
	bool xOk = double.TryParse(coordinates[0].Trim(), out x);
	bool yOk = double.TryParse(coordinates[1].Trim(), out y);
	bool zOk = double.TryParse(coordinates[2].Trim(), out z);
	if (!xOk || !yOk || !zOk) {
		return false;
	}
	return true;
}

#endregion

#region mainserialize

// state variables 
void Serialize() {
	string sb = "";
	sb += "Wico Craft Controller Saved State Do Not Edit" + "\n";
	sb += savefileversion.ToString("0.00") + "\n";

	sb += iMode.ToString() + "\n";
	sb += current_state.ToString() + "\n";

	sb += Vector3DToString(vDock) + "\n";
	sb += Vector3DToString(vLaunch1) + "\n";
	sb += Vector3DToString(vHome) + "\n";

	sb += bValidDock.ToString() + "\n";
	sb += bValidLaunch1.ToString() + "\n";
	sb += bValidHome.ToString() + "\n";

	sb += bAutoRelaunch.ToString() + "\n";

	sb += iAtmoPower.ToString() + "\n";
	sb += iHydroPower.ToString() + "\n";
	sb += iIonPower.ToString() + "\n";

	sb += dGravity.ToString() + "\n";

	sb += fAssumeSimSpeed.ToString() + "\n";

	sb += canthrowstone.ToString() + "\n";
	sb += batterypcthigh.ToString() + "\n";
	sb += batterypctlow.ToString() + "\n";
	sb += cargopctmin.ToString() + "\n";

	sb += batteryPercentage.ToString() + "\n";
	sb += cargopcent.ToString() + "\n";
	sb += craft_operation.ToString() + "\n";
	sb += fSimSpeed.ToString() + "\n";
	sb += velocityShip.ToString() + "\n";
	sb += velocityForward.ToString() + "\n";
	sb += velocityUp.ToString() + "\n";
	sb += velocityLeft.ToString() + "\n";
	sb += currentRun.ToString() + "\n";

	if (SaveFile == null) {
		Storage = sb.ToString();
		return;
	}
	SaveFile.WritePublicText(sb.ToString(), false);
}

void Deserialize() {
	double x,
	y,
	z;

	string sSave;
	if (SaveFile == null) sSave = Storage;
	else sSave = SaveFile.GetPublicText();

	//	Echo("DS"); 
	//	Echo(sSave); 
	if (sSave.Length < 1) {
		Echo("Saved information not available");
		return;
	}

	int i = 1;
	float fVersion = 0;

	string[] atheStorage = sSave.Split('\n');

	// Trick using a "local method", to get the next line from the array `atheStorage`. 
	Func < string > getLine = () = >{
		return (i >= 0 && atheStorage.Length > i ? atheStorage[i++] : null);
	};

	if (atheStorage.Length < 3) {
		// invalid storage 
		Storage = "";
		Echo("Invalid Storage");
		return;
	}

	// Simple "local method" which returns false/true, depending on if the 
	// given `txt` argument contains the text "True" or "true". 
	Func < string,
	bool > asBool = (txt) = >{
		txt = txt.Trim().ToLower();
		return (txt == "True" || txt == "true");
	};

	fVersion = (float) Convert.ToDouble(getLine());

	if (fVersion > savefileversion) {
		Echo("Save file version mismatch; it is newer. Check programming blocks.");
		return; // it is something NEWER than us.. 
	}
	iMode = Convert.ToInt32(getLine());
	current_state = Convert.ToInt32(getLine());

	ParseVector3d(getLine(), out x, out y, out z);
	vDock = new Vector3D(x, y, z);

	ParseVector3d(getLine(), out x, out y, out z);
	vLaunch1 = new Vector3D(x, y, z);

	ParseVector3d(getLine(), out x, out y, out z);
	vHome = new Vector3D(x, y, z);

	bValidDock = asBool(getLine());
	bValidLaunch1 = asBool(getLine());
	bValidHome = asBool(getLine());

	bAutoRelaunch = asBool(getLine());

	iAtmoPower = Convert.ToInt32(getLine());
	iHydroPower = Convert.ToInt32(getLine());
	iIonPower = Convert.ToInt32(getLine());

	bool pOK;
	pOK = double.TryParse(getLine(), out dGravity);
	pOK = float.TryParse(getLine(), out fAssumeSimSpeed);

	canthrowstone = asBool(getLine());
	batterypcthigh = Convert.ToInt32(getLine());
	batterypctlow = Convert.ToInt32(getLine());
	cargopctmin = Convert.ToInt32(getLine());
	// .66 
	if (fVersion >= 1.66f) {
		batteryPercentage = Convert.ToInt32(getLine());
		cargopcent = Convert.ToInt32(getLine());
		craft_operation = Convert.ToInt32(getLine());

		pOK = float.TryParse(getLine(), out fSimSpeed);
		pOK = double.TryParse(getLine(), out velocityShip);
		pOK = double.TryParse(getLine(), out velocityForward);
		pOK = double.TryParse(getLine(), out velocityUp);
		pOK = double.TryParse(getLine(), out velocityLeft);
		currentRun = Convert.ToInt32(getLine());

	}
}

#endregion

#region Grid2World
// from http://forums.keenswh.com/threads/library-grid-to-world-coordinates.7284828/ 
MatrixD GetGrid2WorldTransform(IMyCubeGrid grid) {
	Vector3D origin = grid.GridIntegerToWorld(new Vector3I(0, 0, 0));
	Vector3D plusY = grid.GridIntegerToWorld(new Vector3I(0, 1, 0)) - origin;
	Vector3D plusZ = grid.GridIntegerToWorld(new Vector3I(0, 0, 1)) - origin;
	return MatrixD.CreateScale(grid.GridSize) * MatrixD.CreateWorld(origin, -plusZ, plusY);
}
MatrixD GetBlock2WorldTransform(IMyCubeBlock blk) {
	Matrix blk2grid;
	blk.Orientation.GetMatrix(out blk2grid);
	return blk2grid * MatrixD.CreateTranslation(((Vector3D) new Vector3D(blk.Min + blk.Max)) / 2.0) * GetGrid2WorldTransform(blk.CubeGrid);
}#endregion

#region batterycheck

bool isRechargeSet(IMyTerminalBlock block) {
	if (block is IMyBatteryBlock) {
		IMyBatteryBlock myb = block as IMyBatteryBlock;
		return myb.GetValueBool("Recharge");
	} else return false;
}
bool isDischargeSet(IMyTerminalBlock block) {
	if (block is IMyBatteryBlock) {
		IMyBatteryBlock myb = block as IMyBatteryBlock;
		return myb.GetValueBool("Discharge");
	} else return false;
}
bool isRecharging(IMyTerminalBlock block) {
	if (block is IMyBatteryBlock) {
		IMyBatteryBlock myb = block as IMyBatteryBlock;
		return PowerProducer.IsRecharging(myb);
	} else return false;
}
List < IMyTerminalBlock > batteries = new List < IMyTerminalBlock > ();

bool batteryCheck(int targetMax, bool bEcho = true, IMyTextPanel textBlock = null, bool bProgress = false) {
	if (batteries.Count < 1) GridTerminalSystem.GetBlocksOfType < IMyBatteryBlock > (batteries, (x = >x.CubeGrid == Me.CubeGrid));
	float totalCapacity = 0;
	float totalCharge = 0;
	bool bFoundRecharging = false;
	float f;
	batteryPercentage = 0;
	for (int ib = 0; ib < batteries.Count; ib++) {
		float charge = 0;
		float capacity = 0;
		//bool thecharge = true; 
		int percentthisbattery = 100;
		IMyBatteryBlock b;
		b = batteries[ib] as IMyBatteryBlock;
		PowerProducer.GetMaxStored(b, out f);
		capacity += f;
		totalCapacity += f;
		PowerProducer.GetCurrentStored(b, out f);
		charge += f;
		totalCharge += f;
		if (capacity > 0) {
			f = ((charge * 100) / capacity);
			f = (float) Math.Round(f, 0);
			percentthisbattery = (int) f;
		}
		string s;
		s = "";
		if (isRechargeSet(batteries[ib])) s += "R";
		else if (isDischargeSet(batteries[ib])) s += "D";
		else s += "a";
		float fPower;
		PowerProducer.GetCurrentInput(batteries[ib], out fPower);
		if (fPower > 0) s += "+";
		else s += " ";
		PowerProducer.GetCurrentOutput(batteries[ib], out fPower);
		if (fPower > 0) s += "-";
		else s += " ";
		s += percentthisbattery + "%";
		s += ":" + batteries[ib].CustomName;
		if (bEcho) Echo(s);
		if (textBlock != null) StatusLog(s, textBlock);
		if (bProgress) {
			s = progressBar(percentthisbattery);
			if (textBlock != null) StatusLog(s, textBlock);
		}
		if (isRechargeSet(batteries[ib])) {
			if (percentthisbattery < targetMax) bFoundRecharging = true;
			else if (percentthisbattery > 99) batteries[ib].ApplyAction("Recharge");
		}
		if (!isRechargeSet(batteries[ib]) && percentthisbattery < targetMax && !bFoundRecharging) {
			Echo("Turning on Recharge for " + batteries[ib].CustomName);
			batteries[ib].ApplyAction("Recharge");
			bFoundRecharging = true;
		}
	}
	if (totalCapacity > 0) {
		f = ((totalCharge * 100) / totalCapacity);
		f = (float) Math.Round(f, 0);
		batteryPercentage = (int) f;
	} else batteryPercentage = 0;
	return bFoundRecharging;
}
void batteryDischargeSet(bool bEcho = false) {
	if (batteries.Count < 1) GridTerminalSystem.GetBlocksOfType < IMyBatteryBlock > (batteries, (x = >x.CubeGrid == Me.CubeGrid));
	Echo(batteries.Count + " Batteries");
	for (int i = 0; i < batteries.Count; i++) {
		string s = batteries[i].CustomName + ": ";
		if (isRechargeSet(batteries[i])) {
			s += "RECHARGE/";
			batteries[i].ApplyAction("Recharge");
		} else s += "NOTRECHARGE/";
		if (isDischargeSet(batteries[i])) {
			s += "DISCHARGE";
		} else {
			s += "NOTDISCHARGE";
			batteries[i].ApplyAction("Discharge");
		}
		if (bEcho) Echo(s);
	}
}#endregion

#region powerproducer

public static class PowerProducer {

	/// <summary> 
	/// Getting power level from its position in DetailedInfo. 
	/// The order of power levels changes from block to block, so each block type needs functions. 
	/// </summary> 
	#region Positional

	private const byte
	Enum_BatteryLine_MaxOutput = 1,
	Enum_BatteryLine_MaxRequiredInput = 2,
	Enum_BatteryLine_MaxStored = 3,
	Enum_BatteryLine_CurrentInput = 4,
	Enum_BatteryLine_CurrentOutput = 5,
	Enum_BatteryLine_CurrentStored = 6;

	private const byte
	Enum_ReactorLine_MaxOutput = 1,
	Enum_ReactorLine_CurrentOutput = 2;

	private const byte
	Enum_SolarPanelLine_MaxOutput = 1,
	Enum_SolarPanelLine_CurrentOutput = 2;

	private const byte
	Enum_GravityLine_MaxRequiredInput = 1,
	Enum_GravityLine_CurrentInput = 2;

	private static readonly char[] wordBreak = {
		' '
	};

	public static bool GetMaxOutput(IMyBatteryBlock battery, out float value) {
		return GetPowerFromInfo(battery, Enum_BatteryLine_MaxOutput, out value);
	}

	public static bool GetMaxRequiredInput(IMyBatteryBlock battery, out float value) {
		return GetPowerFromInfo(battery, Enum_BatteryLine_MaxRequiredInput, out value);
	}

	public static bool GetCurrentInput(IMyBatteryBlock battery, out float value) {
		return GetPowerFromInfo(battery, Enum_BatteryLine_CurrentInput, out value);
	}

	public static bool GetCurrentOutput(IMyBatteryBlock battery, out float value) {
		return GetPowerFromInfo(battery, Enum_BatteryLine_CurrentOutput, out value);
	}

	public static bool GetMaxStored(IMyBatteryBlock battery, out float value) {
		return GetPowerFromInfo(battery, Enum_BatteryLine_MaxStored, out value);
	}

	public static bool GetCurrentStored(IMyBatteryBlock battery, out float value) {
		return GetPowerFromInfo(battery, Enum_BatteryLine_CurrentStored, out value);
	}

	public static bool GetMaxOutput(IMyReactor reactor, out float value) {
		return GetPowerFromInfo(reactor, Enum_ReactorLine_MaxOutput, out value);
	}

	public static bool GetCurrentOutput(IMyReactor reactor, out float value) {
		return GetPowerFromInfo(reactor, Enum_ReactorLine_CurrentOutput, out value);
	}

	public static bool GetMaxOutput(IMySolarPanel panel, out float value) {
		return GetPowerFromInfo(panel, Enum_SolarPanelLine_MaxOutput, out value);
	}

	public static bool GetCurrentOutput(IMySolarPanel panel, out float value) {
		return GetPowerFromInfo(panel, Enum_SolarPanelLine_CurrentOutput, out value);
	}

	public static bool GetMaxRequiredInput(IMyGravityGeneratorBase gravity, out float value) {
		return GetPowerFromInfo(gravity, Enum_GravityLine_MaxRequiredInput, out value);
	}

	public static bool GetCurrentInput(IMyGravityGeneratorBase gravity, out float value) {
		return GetPowerFromInfo(gravity, Enum_GravityLine_CurrentInput, out value);
	}

	private static bool GetPowerFromInfo(IMyTerminalBlock block, byte lineNumber, out float value) {
		value = -1;
		float multiplier;

		string[] lines = block.DetailedInfo.Split('\n');
		string[] words = lines[lineNumber].Split(wordBreak, StringSplitOptions.RemoveEmptyEntries);

		if (words.Length < 2 || !float.TryParse(words[words.Length - 2], out value) || !getMultiplier('W', words[words.Length - 1], out multiplier)) return false;

		value *= multiplier;
		value /= 1000 * 1000f;

		return true;
	}

	#endregion

	/// <summary> 
	/// Getting power level from DetailedInfo using regular expressions. 
	/// No localization. 
	/// </summary> 
	#region Regular Expressions
	private static readonly System.Text.RegularExpressions.Regex CurrentInput = new System.Text.RegularExpressions.Regex(@"(\nCurrent Input:)\s+(-?\d+\.?\d*)\s+(\w+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
	private static readonly System.Text.RegularExpressions.Regex CurrentOutput = new System.Text.RegularExpressions.Regex(@"(\nCurrent Output:)\s+(-?\d+\.?\d*)\s+(\w+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
	private static readonly System.Text.RegularExpressions.Regex MaxPowerOutput = new System.Text.RegularExpressions.Regex(@"(\nMax Output:)\s+(-?\d+\.?\d*)\s+(\w+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
	private static readonly System.Text.RegularExpressions.Regex MaxRequiredInput = new System.Text.RegularExpressions.Regex(@"(\nMax Required Input:)\s+(-?\d+\.?\d*)\s+(\w+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
	private static readonly System.Text.RegularExpressions.Regex RequiredInput = new System.Text.RegularExpressions.Regex(@"(\nRequired Input:)\s+(-?\d+\.?\d*)\s+(\w+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
	public static bool GetCurrentInput(IMyTerminalBlock block, out float value) {
		return GetPowerFromInfo(block, CurrentInput, out value);
	}
	public static bool GetCurrentOutput(IMyTerminalBlock block, out float value) {
		return GetPowerFromInfo(block, CurrentOutput, out value);
	}
	public static bool GetMaxPowerOutput(IMyTerminalBlock block, out float value) {
		return GetPowerFromInfo(block, MaxPowerOutput, out value);
	}
	public static bool GetMaxRequiredInput(IMyTerminalBlock block, out float value) {
		return GetPowerFromInfo(block, MaxRequiredInput, out value);
	}
	public static bool GetRequiredInput(IMyTerminalBlock block, out float value) {
		return GetPowerFromInfo(block, RequiredInput, out value);
	}
	private static bool GetPowerFromInfo(IMyTerminalBlock block, System.Text.RegularExpressions.Regex regex, out float value) {
		value = -1;
		float multiplier;
		System.Text.RegularExpressions.Match match = regex.Match(block.DetailedInfo);
		if (!match.Success || !float.TryParse(match.Groups[2].ToString(), out value) || !getMultiplier('W', match.Groups[3].ToString(), out multiplier)) return false;
		value *= multiplier;
		return true;
	}#endregion
	public const string depleted_in = "Fully depleted in:";
	public const string recharged_in = "Fully recharged in:";
	private const float
	k = 1000f,
	M = k * k,
	G = k * M,
	T = k * G,
	m = 0.001f;
	public static bool IsRecharging(IMyBatteryBlock battery) {
		return battery.DetailedInfo.Contains(recharged_in);
	}
	public static bool IsDepleting(IMyBatteryBlock battery) {
		return battery.DetailedInfo.Contains(depleted_in);
	}
	private static bool getMultiplier(char unit, string expr, out float result) {
		result = 0;
		char firstChar = expr[0];
		if (firstChar == unit) {
			result = 1;
			return true;
		}
		if (expr[1] != unit) return false;
		float k = 1000;
		if (firstChar == 'k') result = k;
		else if (firstChar == 'M') result = M;
		else if (firstChar == 'G') result = G;
		else if (firstChar == 'T') result = T;
		else if (firstChar == 'm') result = m;
		return result != 0;
	}
}

#endregion

#region cargocheck
//int cargopcent = 0; 
List < IMyTerminalBlock > lContainers = null;
//List < IMyTerminalBlock > lDrills = null; 
bool bCreative = false;

double totalCurrent = 0.0; // volume 

void initCargoCheck() {
	List < IMyTerminalBlock > grid = new List < IMyTerminalBlock > ();

	lContainers = new List < IMyTerminalBlock > ();

	GridTerminalSystem.GetBlocksOfType < IMyCargoContainer > (grid, (x = >x.CubeGrid == Me.CubeGrid));

	lContainers.AddRange(grid);
	grid.Clear();

	//	GridTerminalSystem.GetBlocksOfType < IMyShipDrill > (lContainers, (x => x.CubeGrid == Me.CubeGrid)); 
	GridTerminalSystem.GetBlocksOfType < IMyShipDrill > (grid);

	lContainers.AddRange(grid);
	//	grid.clear; 
}

void doCargoCheck() {
	//	List < IMyTerminalBlock > grid = new List < IMyTerminalBlock > (); 
	//	List < IMyTerminalBlock > drills = new List < IMyTerminalBlock > (); 
	if (lContainers == null) initCargoCheck();

	totalCurrent = 0.0;
	double totalMax = 0.0;
	double ratio = 0;
	//int pcent = 0; 
	//	if (initialCargo == 0) initialCargo = lContainers.Count; 
	//	else if (initialCargo != lContainers.Count) integrityFail(); 
	for (int i = 0; i < lContainers.Count; i++) {
		//if(i==0)  
		//Echo("lContainers="+lContainers[i].DefinitionDisplayNameText+"'"); 
		var count = lContainers[i].GetInventoryCount(); // Multiple inventories in Refineriers, Assemblers, Arc Furnances. 
		for (var invcount = 0; invcount < count; invcount++) {
			//VRage.ModAPI.Ingame.IMyInventory // 1.123.007 (hotpatch) 1.126 back to just IMyInventory 
			IMyInventory
			inv = lContainers[i].GetInventory(invcount);

			if (inv != null) // null means, no items in inventory. 
			{

				totalCurrent += (double) inv.CurrentVolume;

				if (lContainers[i] is IMyCargoContainer) {
					if ((double) inv.MaxVolume > 9223372036854) bCreative = true;
					else bCreative = false;

					if (!bCreative) {
						//Echo("NCreateive"); 
						totalMax += (double) inv.MaxVolume;
					}
					else {
						//Echo("lContainers="+lContainers[i].DefinitionDisplayNameText+"'"); 
						if (lContainers[i].BlockDefinition.SubtypeId.Contains("LargeBlock")) {
							if (lContainers[i].DefinitionDisplayNameText.Contains("LargeContainer")) totalMax += 421.000; //;15.625; 
							//							else if(lContainers[i].DefinitionDisplayNameText.Contains("MediumContainer")) totalMax+=3.375; 
							else if (lContainers[i].DefinitionDisplayNameText.Contains("SmallContainer")) totalMax += 15.625;
							else totalMax += 1; // unknown cargo size 
						}
						else {
							if (lContainers[i].DefinitionDisplayNameText.Contains("LargeContainer")) totalMax += 15.625;
							else if (lContainers[i].DefinitionDisplayNameText.Contains("MediumContainer")) totalMax += 3.375;
							else if (lContainers[i].DefinitionDisplayNameText.Contains("SmallContainer")) totalMax += 0.125;
							else totalMax += 1; // unknown cargo size 
						}
					}

				} // else it's a drill 
			}
		}
	}

	if (totalMax > 0) {
		ratio = (totalCurrent / totalMax) * 100;
	}
	else {
		ratio = 100;
	}
	//Echo("ratio="+ratio.ToString()); 
	cargopcent = (int) ratio;

}#endregion

#region config

const int CRAFT_MODE_AUTO = 0;
const int CRAFT_MODE_ION = 1;
const int CRAFT_MODE_SLED = 2;
const int CRAFT_MODE_ROTOR = 4;
const int CRAFT_MODE_ATMO = 8;
const int CRAFT_MODE_HYDRO = 16;
const int CRAFT_MODE_ORBITAL = 32;
const int CRAFT_MODE_ROCKET = 64;

const int CRAFT_MODE_PET = 128;

const int CRAFT_MODE_NAD = 256; // no auto dock 
const int CRAFT_MODE_NOAUTOGYRO = 512;
const int CRAFT_MODE_NOPOWERMGMT = 1024;

const int CRAFT_MODE_MASK = 0xfff;

const int CRAFT_TOOLS_WELDER = 0x1000;
const int CRAFT_TOOLS_GRINDER = 0x2000;
const int CRAFT_TOOLS_DRILLS = 0x4000;
const int CRAFT_TOOLS_EJECTORS = 0x8000;

const int CRAFT_TOOLS_MASK = 0xf000;

//int craft_operation = CRAFT_MODE_AUTO; 
string craftOperation() {

	string sResult = "";
	//sResult+=craft_operation.ToString(); 
	if ((craft_operation & CRAFT_MODE_SLED) > 0) sResult += "SLED ";
	if ((craft_operation & CRAFT_MODE_ORBITAL) > 0) sResult += "ORBITAL ";
	if ((craft_operation & CRAFT_MODE_ROCKET) > 0) sResult += "ROCKET ";
	if ((craft_operation & CRAFT_MODE_ION) > 0) sResult += "ION ";
	if ((craft_operation & CRAFT_MODE_ATMO) > 0) sResult += "ATMO ";
	if ((craft_operation & CRAFT_MODE_HYDRO) > 0) sResult += "HYDRO ";
	if ((craft_operation & CRAFT_MODE_ROTOR) > 0) sResult += "ROTOR ";
	if ((craft_operation & CRAFT_MODE_PET) > 0) sResult += "PET ";
	if ((craft_operation & CRAFT_MODE_NAD) > 0) sResult += "NAD ";
	if ((craft_operation & CRAFT_MODE_NOAUTOGYRO) > 0) sResult += "NO Gyro ";
	if ((craft_operation & CRAFT_MODE_NOPOWERMGMT) > 0) sResult += "No Power ";
	return sResult;
}#endregion

#region NAV
const string sNavTimer = "NAV Timer";
const string sNavEnable = "NAV Enable";
const string sNavStatus = "NAV Status:";
const string sNavCmd = "NAV:";
const string sNavInstructions = "NAV Instructions";
const string sRemoteControl = "NAV Remote Control";
IMyTimerBlock navTriggerTimer = null;
IMyTerminalBlock navCommand = null;
IMyTerminalBlock navStatus = null;
bool bNavCmdIsTextPanel = false;
IMyTerminalBlock gpsCenter = null;
IMyTerminalBlock navEnable = null;
IMyRemoteControl remoteControl = null;
string NAVInit() {
	Echo("Navinit()");
	string sInitResults = "";
	if (! (gpsCenter is IMyRemoteControl)) {
		Echo("NO RC!");
		return "No Remote Control for NAV";
	}
	remoteControl = (IMyRemoteControl) gpsCenter;

	List < IMyTerminalBlock > blocks = new List < IMyTerminalBlock > ();
	blocks = GetTargetBlocks < IMyTerminalBlock > (sNavStatus);
	if (blocks.Count > 0) {
		for (int blockIndex = 0; blockIndex < blocks.Count; blockIndex++) {
			string name = blocks[blockIndex].CustomName;
			if (name.StartsWith(sNavStatus)) {
				sInitResults += "S";
				navStatus = blocks[blockIndex];
				break;
			}
		}
	} else sInitResults += "-";
	blocks = GetTargetBlocks < IMyTerminalBlock > (sNavCmd);
	if (blocks.Count > 0) {
		for (int blockIndex = 0; blockIndex < blocks.Count; blockIndex++) {
			string name = blocks[blockIndex].CustomName;
			if (name.StartsWith(sNavCmd)) {
				sInitResults += "C";
				navCommand = blocks[blockIndex];
				bNavCmdIsTextPanel = false;
				break;
			}
		}
	} else {
		blocks = GetBlocksNamed < IMyTextPanel > (sNavInstructions);
		if (blocks.Count > 0) {
			sInitResults += "T";
			navCommand = blocks[0];
		}
		bNavCmdIsTextPanel = true;
	}
	blocks = GetBlocksNamed < IMyTerminalBlock > (sNavTimer);
	if (blocks.Count > 1) throw new OurException("Multiple blocks found: \"" + sNavTimer + "\"");
	else if (blocks.Count == 0) Echo("Missing: " + sNavTimer);
	else navTriggerTimer = blocks[0] as IMyTimerBlock;
	blocks = GetBlocksNamed < IMyTerminalBlock > (sNavEnable);
	if (blocks.Count > 1) throw new OurException("Multiple blocks found: \"" + sNavEnable + "\"");
	else if (blocks.Count == 0) Echo("Missing: " + sNavEnable);
	else navEnable = blocks[0] as IMyTerminalBlock;
	Echo("EO:Navinit()");

	return sInitResults;
}

void startNavCommand(string sCmd) {
	string sNav = sCmd;
	if (navCommand == null || navStatus == null) {
		//  throw new OurException("No nav Command/Status blocks found"); 
		Echo("No nav Command/Status blocks found");
		return;
	}
	if (navCommand is IMyTextPanel) { ((IMyTextPanel) navCommand).WritePublicText(sNav);
	} else navCommand.SetCustomName(sNavCmd + " " + sNav);
	navStatus.SetCustomName(sNavStatus + " Command Set");
	if (navEnable != null) blockApplyAction(navEnable, "OnOff_On");
	if (navTriggerTimer != null) navTriggerTimer.ApplyAction("Start");
}

void startNavWaypoint(Vector3D vWaypoint, bool bOrient = false, int iRange = 10) {
	string sNav;
	sNav = "";
	sNav = "D " + iRange;
	if (bNavCmdIsTextPanel) sNav += "\n";
	else sNav += "; ";
	if (bOrient) sNav += "O ";
	else sNav += "W ";

	sNav += Vector3DToString(vWaypoint);
	if (navCommand == null || navStatus == null) {
		throw new OurException("No nav Command/Status blocks found");
	}
	if (navCommand is IMyTextPanel) { ((IMyTextPanel) navCommand).WritePublicText(sNav);
	} else navCommand.SetCustomName(sNavCmd + " " + sNav);
	navStatus.SetCustomName(sNavStatus + " Command Set");
	if (navEnable != null) blockApplyAction(navEnable, "OnOff_On");
	if (navTriggerTimer != null) {
		navTriggerTimer.SetValueFloat("TriggerDelay", 1.0f);
		navTriggerTimer.ApplyAction("Start");
	}
}
void startNavRotate(Vector3D vWaypoint) {
	string sNav;
	sNav = "";
	sNav += "r ";
	sNav += Vector3DToString(vWaypoint);
	if (navCommand is IMyTextPanel) { ((IMyTextPanel) navCommand).WritePublicText(sNav);
	} else navCommand.SetCustomName(sNavCmd + " " + sNav);
	navStatus.SetCustomName(sNavStatus + " Command Set");
	blockApplyAction(navEnable, "OnOff_On");
	navTriggerTimer.ApplyAction("Start");
}#endregion

void prepareForSupported(IMyTextPanel textBlock = null) {
	//Echo("pfSupport"); 
	if ((craft_operation & CRAFT_MODE_NOPOWERMGMT) == 0) if (!batteryCheck(30, true, textBlock)) if (!batteryCheck(80, false)) batteryCheck(100, false);

	//	blockApplyAction(thrustAllList,"OnOff_Off"); 
	//	blockApplyAction(tankList,"Stockpile_On"); 
	//	blockApplyAction(gasgenList,"OnOff_Off"); 
	//	blockApplyAction(airventList,"OnOff_Off"); 
}

void prepareForSolo() {
	//Echo("pfSolo"); 
	//	powerDownThrusters(thrustAllList); 
	if ((craft_operation & CRAFT_MODE_NOPOWERMGMT) == 0) batteryDischargeSet();

	//	if(AnyConnectorIsConnected() || AnyConnectorIsLocked()) 
	//	{ 
	ConnectAnyConnectors(false, "OnOff_Off");
	//	} 
	//	blockApplyAction(tankList,"Stockpile_Off"); 
	//	blockApplyAction(gasgenList,"OnOff_On"); 
	//	blockApplyAction(airventList,"OnOff_On"); 
	//	blockApplyAction(gearList,"Unlock"); 
	gyrosOff();
}

#region gasgens

List < IMyTerminalBlock > gasgenList = new List < IMyTerminalBlock > ();
string gasgenInit() {
	{
		gasgenList = new List < IMyTerminalBlock > ();
		GridTerminalSystem.GetBlocksOfType < IMyOxygenGenerator > (gasgenList, (x = >x.CubeGrid == Me.CubeGrid));
	}
	return "GG" + gasgenList.Count.ToString("00");
}#endregion

#region airvents

List < IMyTerminalBlock > airventList = new List < IMyTerminalBlock > ();
string airventInit() {
	{
		airventList = new List < IMyTerminalBlock > ();
		GridTerminalSystem.GetBlocksOfType < IMyAirVent > (airventList, (x = >x.CubeGrid == Me.CubeGrid));
	}
	return "A" + airventList.Count.ToString("0");
}
void airventOccupied() {
	for (int i = 0; i < airventList.Count; i++) {
		IMyAirVent av;
		av = airventList[i] as IMyAirVent;
		if (av != null) {
			if (av.IsDepressurizing) av.ApplyAction("OnOff_On");
		}
	}
}
void airventUnoccupied() {
	for (int i = 0; i < airventList.Count; i++) {
		IMyAirVent av;
		av = airventList[i] as IMyAirVent;
		if (av != null) {
			if (av.IsDepressurizing) av.ApplyAction("OnOff_Off");
		}
	}
}#endregion

#region doors

List < IMyTerminalBlock > hangarDoorList = new List < IMyTerminalBlock > ();
List < IMyTerminalBlock > outterairlockDoorList = new List < IMyTerminalBlock > ();
List < IMyTerminalBlock > innerairlockDoorList = new List < IMyTerminalBlock > ();
List < IMyTerminalBlock > allDoorList = new List < IMyTerminalBlock > ();

string doorsInit() {
	{
		allDoorList = new List < IMyTerminalBlock > ();
		GridTerminalSystem.GetBlocksOfType < IMyDoor > (allDoorList, (x = >x.CubeGrid == Me.CubeGrid));
	}

	for (int i = 0; i < allDoorList.Count; i++) {
		if (allDoorList[i].CustomName.Contains("Hangar Door")) hangarDoorList.Add(allDoorList[i]);
		if (allDoorList[i].CustomName.ToLower().Contains("bay")) outterairlockDoorList.Add(allDoorList[i]);

		if (allDoorList[i].CustomName.ToLower().Contains("airlock")) if (allDoorList[i].CustomName.ToLower().Contains("outside")) outterairlockDoorList.Add(allDoorList[i]);
		else if (allDoorList[i].CustomName.ToLower().Contains("inside")) innerairlockDoorList.Add(allDoorList[i]);

		if (allDoorList[i].CustomName.ToLower().Contains("bridge")) innerairlockDoorList.Add(allDoorList[i]);

	}
	string s = "";
	s += "D" + allDoorList.Count.ToString("00");
	s += "h" + hangarDoorList.Count.ToString("00");
	s += "o" + outterairlockDoorList.Count.ToString("00");
	s += "i" + innerairlockDoorList.Count.ToString("00");
	return s;
}

void closeDoors(List < IMyTerminalBlock > DoorList) {
	//Echo("Close Doors:" + DoorList.Count.ToString()); 
	for (int i = 0; i < DoorList.Count; i++) {
		IMyDoor d = DoorList[i] as IMyDoor;
		//Echo(d.CustomName); 
		if (d == null) continue;
		if (d.Open) d.ApplyAction("Open");
	}
}
void openDoors(List < IMyTerminalBlock > DoorList) {
	//Echo("Open Doors:" + DoorList.Count.ToString()); 
	for (int i = 0; i < DoorList.Count; i++) {
		IMyDoor d = DoorList[i] as IMyDoor;
		//Echo(d.CustomName); 
		if (d == null) continue;
		if (!d.Open) d.ApplyAction("Open");
	}
}

#endregion

double CalculateRoll(Vector3D destination, IMyTerminalBlock Origin) {
	double rollAngle = 0;
	bool facingTarget = false;

	Vector3D vCenter;
	Vector3D vBack;
	Vector3D vUp;
	Vector3D vRight;

	MatrixD refOrientation = GetBlock2WorldTransform(Origin);

	vCenter = Origin.GetPosition();
	vBack = vCenter + 1.0 * Vector3D.Normalize(refOrientation.Backward);
	vUp = vCenter + 1.0 * Vector3D.Normalize(refOrientation.Up);
	vRight = vCenter + 1.0 * Vector3D.Normalize(refOrientation.Right);

	double centerTargetDistance = calculateDistance(vCenter, destination);
	double upTargetDistance = calculateDistance(vUp, destination);
	double rightLocalDistance = calculateDistance(vRight, vCenter);
	double rightTargetDistance = calculateDistance(vRight, destination);

	facingTarget = centerTargetDistance > upTargetDistance;

	rollAngle = (centerTargetDistance - rightTargetDistance) / rightLocalDistance;
	//Echo("calc Angle=" + Math.Round(rollAngle,5)); 
	if (!facingTarget) {
		Echo("ROLL:NOT FACING!");
		rollAngle += (rollAngle < 0) ? -1 : 1;
	}
	return rollAngle;
}

bool DoRoll(double rollAngle) {
	//Echo("rollAngle=" + Math.Round(rollAngle,5)); 
	float targetRoll = 0;
	IMyGyro gyro = gyros[0] as IMyGyro;
	float maxRoll = gyro.GetMaximum < float > ("Roll");
	float minRoll = gyro.GetMinimum < float > ("Roll");

	if (Math.Abs(rollAngle) > 1.0) {
		targetRoll = (float) maxRoll * (float)(rollAngle);
	}
	else if (Math.Abs(rollAngle) > .7) {
		// need to dampen 
		targetRoll = (float) maxRoll * (float)(rollAngle) / 4;
	}
	else if (Math.Abs(rollAngle) > 0.5) {
		targetRoll = 0.11f * Math.Sign(rollAngle);
	} else if (Math.Abs(rollAngle) > 0.1) {
		targetRoll = 0.07f * Math.Sign(rollAngle);
	} else if (Math.Abs(rollAngle) > 0.01) {
		targetRoll = 0.05f * Math.Sign(rollAngle);
	} else if (Math.Abs(rollAngle) > 0.001) {
		targetRoll = 0.035f * Math.Sign(rollAngle);
	} else targetRoll = 0;

	//				Echo("targetRoll=" + targetRoll); 
	//	rollLevel = (int)(targetRoll * 1000); 
	for (int i = 0; i < gyros.Count; i++) {
		gyro = gyros[i] as IMyGyro;
		gyro.SetValueFloat("Roll", targetRoll);
		gyro.SetValueBool("Override", true);
	}
	return true;
}

double calculateDistance(Vector3D a, Vector3D b) {
	double x = a.GetDim(0) - b.GetDim(0);
	double y = a.GetDim(1) - b.GetDim(1);
	double z = a.GetDim(2) - b.GetDim(2);
	return Math.Sqrt((x * x) + (y * y) + (z * z));
}

// ORBITAL DESCENT 
