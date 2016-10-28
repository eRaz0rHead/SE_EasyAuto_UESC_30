/* 
 * Wico craft controller 
 *  
 * Control Script for Rovers and Drones and Oribtal craft 
 *  
 Version 1.6X (initial) (WIP) 
 * .2 Add serialize; needed to tell if init and NAV needs kick-start 
 *  *  
 * (Backwards) 1.64 to match WSAM 
 * WSM control code. 
 * .4 
 * Air vent control: On when cockpit occupied. Off when cockpit not occupied.  NEED: Override control 
 * globals for rotor angles 
 * .5 
 * get thrusters by orientation 
 * .6 godock (incomplete) 
 *  
 * .7 Orbital Launch Control 
 * automatically control all three thruster types to minimize usage of ower and hydrogen 
 *  
 * .8 Start Minify 
 * calculate altitude on orbital launch and use for thruster controls 
 * align ship to gravity during orbital launch 
 * only turn off air vents that are set to 'depressurize' when not in cockpit 
 *  
 * .9 WANT: non-rocket orbital (need to find one to test) (sabrina) No testing yet. 
 *  
 * .10 add setsimspeed command to set current simspeed. 
 *  
 * .11 add undock and dock commands.  
 * Added prepareForSolo that turns off stockpile, etc.  
 * added check for connector and gear releases to oribitallaunch 
 *  
 * .12 
 * add local only for remote and cockpit for directions.. 
 * got rid of 'config' 
 * save state (serialize) the orbital code 
 * "Working Projector" Message 
 * 01/18/16 
 *  
 * .13 
 *  load into Sled 
 * Force antenna ON like miner does. 
 *  PET mode 
 *   
 * .65 
 * (used above, so skip) 
 *  
 * .66  
 * sub-script prep: all StartXXXMode() does just setmode and current_state=0; Done 
 * add dict of commands and modes in prep for loading dynamically: done 
 * add simple trigger of sub-modules: done 
 * BP Change: master timer must contain [WCCM] (or FAST won't work) 
 * BP Change: sub-timers must contain [WCCS] 
 *  
 * 66.1 check dictionary for mode commands dictionary 
 * Set speed limit in pet mode. 
 *  
 * 66.2 antennaInit so that verifyAntenna has something to work on.. 
 * [WCCT] for fast timer 
 *  
 * .66.3 Support for Wico Atmo Miner (connectors!=docked) 
 *  
 * .66.4 Airvent control only in MODE_IDLE 
 *  
 * .66.5 IMyInventory broken with 1.123; use work-around 
 * and again with hotpatch. 
 *  
 * 66.5.1 don't echo progress bar 
 *  
 * 67 Vertical Support (Sabrina) 
 * "No Power" setting 
 * Optimizations 
 *  
 * 67.1 IMyInventory is back to normal (and workaround broken) 
 *  
 * 67.2 Remove Sled Miner and Orbital commands from (this) main module. 
 * Don't assume SLED just because has wheels. 
 * SABRINA 
 *  
 * 68 
 *  tighten gyro alignment operation so keep more perfectly level. Also made this a variable in gyro section for setting alignment limits 
 *  
 * 68.1 Don't autodock in domodes when MODE_LAUNCH or MODE_RELAUNCH 
 *  
 * 68.2 speed added to shipcontroller (SE V1.36) 
 *  
 * 68.3 handle MODE_ATTENTION 
 *  
 * 68.3.2 re-init after landing gear released as grids change (me was everything, not just 'me') 
 *  
 * 68.3.3 fix gpsCenter not using me.cubegrid 
 *  
 * 70 Updates for SE 1.142 
 *   
 * * Need:  
 * serialize/deserialize 
 * advanced trigger: only when module handles that mode... (so need mode->module dictionary) 
 * override base modes? 
 *   
 * NEED: 
 *  
 * save state in status private data. 
 *  
 * common function for 'handle this' combining 'me' grid and an exclusion name check 
 *  
 * multi-script handling for modes 
 *  
 * Airvent control override 
 *  
 * WAYPOINT controls 
 * ----------------- 
 * New list:Name 
 * Add 'Here' to list 
 *  
 * Start Nav through "list:name" 
 *  
 * set navlist mode: One-way, patrol, cycle? 
 *  
 * re-dock 
 *  
 *  
 * WANT: 
 * setvalueb 
 * Actions 
 * Trigger timers on 'events'. 
 * set antenna name to match mode? 
 * 
 * Arrange for second PB to handle modes? 
 *  
 *  
 * support multiple text panels of a 'type' (lists instead of single) 
 
 *   
 * Setvaules that take groups. 
 *  
 * Behavior to control: 
 * -Charge one battery or all when docked 
 *  
 * SLED Miner: 
 * rotor lock position 
 * change rotor offset 'down' 
 * change rotor offset 'up' 
 * auto-detect gravity vector and adjust rotor 'vector' 
 *  
 * Note: to fit into 100k Limit, some code has been 'minified' 
 * Beautify at http://codebeautify.org/csharpviewer 
 *  
*/

string OurName = "Wico Craft";
string moduleName = "WCCM";

const string sGPSCenter = "Craft Remote Control";

Vector3I iForward = new Vector3I(0, 0, 0);
Vector3I iUp = new Vector3I(0, 0, 0);
Vector3I iLeft = new Vector3I(0, 0, 0);
Vector3D currentPosition,
lastPosition,
currentVelocity,
lastVelocity;
Vector3 vectorForward,
vectorLeft,
vectorUp;
Vector3 center,
up,
left,
forward;

float fAssumeElapsed = 1.339f;

DateTime dtStartTime;
bool bCalcAssumed = true;
bool bGotStart = false;

// 
const string velocityFormat = "0.00";

IMyTerminalBlock anchorPosition;

class OurException: Exception {
	public OurException(string msg) : base("WicoCraft" + ": " + msg) {}
}

Dictionary < string,
int > modeCommands = new Dictionary < string,
int > ();

#region massblocks
List < IMyTerminalBlock > massList = new List < IMyTerminalBlock > ();
string massInit() {
	massList = new List < IMyTerminalBlock > ();
	GridTerminalSystem.GetBlocksOfType < IMyVirtualMass > (massList, (x = >x.CubeGrid == Me.CubeGrid));
	return "M" + massList.Count.ToString("00");
}#endregion

#region drills

List < IMyTerminalBlock > drillList = new List < IMyTerminalBlock > ();
string drillInit() {
	{
		drillList = new List < IMyTerminalBlock > ();
		GridTerminalSystem.GetBlocksOfType < IMyShipDrill > (drillList);
	}
	return "D" + drillList.Count.ToString("00");
}#endregion

#region ejectors
List < IMyTerminalBlock > ejectorsList = new List < IMyTerminalBlock > ();
string ejectorsInit() {
	{
		ejectorsList = new List < IMyTerminalBlock > ();
		List < IMyTerminalBlock > Blocks = new List < IMyTerminalBlock > ();
		GridTerminalSystem.GetBlocksOfType < IMyShipConnector > (Blocks, (x = >x.CubeGrid == Me.CubeGrid));
		for (int e = 0; e < Blocks.Count; e++) {
			if (Blocks[e].BlockDefinition.SubtypeId.Contains("Small")) {
				ejectorsList.Add(Blocks[e]);
			}
		}
	}
	return "E" + ejectorsList.Count.ToString("00");
}
void doStoneRemoval() {
	if (ejectorsList.Count < 1) return;
	if (!canthrowstone) return;
	bool bTurnOffEjectors = false;
	if (iMode == MODE_DOCKING || iMode == MODE_DOCKED || iMode == MODE_LAUNCH) bTurnOffEjectors = true;
	if (bTurnOffEjectors) {
		blockApplyAction(ejectorsList, "OnOff_Off");
	}
	else {
		blockApplyAction(ejectorsList, "OnOff_On");
	}
}

#endregion

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

#region wheels

List < IMyTerminalBlock > wheelList = new List < IMyTerminalBlock > ();
string wheelsInit() {
	{
		wheelList = new List < IMyTerminalBlock > ();
		GridTerminalSystem.GetBlocksOfType < IMyMotorSuspension > (wheelList, (x = >x.CubeGrid == Me.CubeGrid));
	}
	return "W" + wheelList.Count.ToString("00");
}#endregion

#region gasgens

List < IMyTerminalBlock > gasgenList = new List < IMyTerminalBlock > ();
string gasgenInit() {
	{
		gasgenList = new List < IMyTerminalBlock > ();
		GridTerminalSystem.GetBlocksOfType < IMyOxygenGenerator > (gasgenList, (x = >x.CubeGrid == Me.CubeGrid));
	}
	return "GG" + gasgenList.Count.ToString("00");
}#endregion

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
}#endregion

#region sensors

List < IMyTerminalBlock > sensorsList = new List < IMyTerminalBlock > ();

string sensorInit() {
	sensorsList = new List < IMyTerminalBlock > ();
	GridTerminalSystem.GetBlocksOfType < IMySensorBlock > (sensorsList, (x = >x.CubeGrid == Me.CubeGrid));

	return "S" + sensorsList.Count.ToString("00");
}

List < IMyTerminalBlock > activeSensors(string sKey = null) {
	List < IMyTerminalBlock > activeSensors = new List < IMyTerminalBlock > ();;
	for (int i = 0; i < sensorsList.Count; i++) {
		IMySensorBlock s = sensorsList[i] as IMySensorBlock;
		if (s == null) continue;
		if (s.IsActive) activeSensors.Add(sensorsList[i]);
	}
	return activeSensors;
}

//SFrontNAsteroid.LastDetectedEntity.GetPosition() 
//typeEntityDetected(SFront.LastDetectedEntity.ToString())==ENTITY_TYPE_ASTEROID 
const int ENTITY_TYPE_SHIPSMALL = 1;
const int ENTITY_TYPE_SHIPLARGE = 3;
const int ENTITY_TYPE_ASTEROID = 4;
const int ENTITY_TYPE_PLAYER = 8;
int typeEntityDetected(string s) {
	if (s.Contains("Grid_D_Small")) return ENTITY_TYPE_SHIPSMALL;
	if (s.Contains("Grid_D_Large")) return ENTITY_TYPE_SHIPLARGE;
	if (s.Contains("MyVoxelMap")) return ENTITY_TYPE_ASTEROID;
	if (s.Contains("Engineer_suit")) return ENTITY_TYPE_PLAYER;
	if (s.Contains("Astronaut")) return ENTITY_TYPE_PLAYER;
	return 0;
}

#endregion

#region airvents

List < IMyTerminalBlock > airventList = new List < IMyTerminalBlock > ();

List < IMyTerminalBlock > hangarairventList = new List < IMyTerminalBlock > (); // user controled area 
List < IMyTerminalBlock > airlockairventList = new List < IMyTerminalBlock > (); // system vent in airlock area; used to pressurize if isolated is empty 
List < IMyTerminalBlock > isolatedairlockairventList = new List < IMyTerminalBlock > (); // connected isolated air tank used to cycle airlock 
List < IMyTerminalBlock > bridgeairventList = new List < IMyTerminalBlock > (); // crew/bridge area. should always be pressurized 
List < IMyTerminalBlock > outsideairventList = new List < IMyTerminalBlock > (); // outside air. detect if planet and depressurize to make Free O2. open doors early/always. detect if space and keep doors closed 
List < IMyTerminalBlock > cockpitairventList = new List < IMyTerminalBlock > (); // outside air. directly supplying a cockpit. turn off if cockpit not occupied. 
string airventInit() {
	airventList = new List < IMyTerminalBlock > ();
	GridTerminalSystem.GetBlocksOfType < IMyAirVent > (airventList, (x = >x.CubeGrid == Me.CubeGrid));

	for (int i = 0; i < airventList.Count; i++) {
		if (airventList[i].CustomName.ToLower().Contains("hangar")) hangarairventList.Add(airventList[i]);
		if (airventList[i].CustomName.ToLower().Contains("outside")) outsideairventList.Add(airventList[i]);
		if (airventList[i].CustomName.ToLower().Contains("bridge")) bridgeairventList.Add(airventList[i]);
		if (airventList[i].CustomName.ToLower().Contains("crew")) bridgeairventList.Add(airventList[i]);

		if (airventList[i].CustomName.ToLower().Contains("isolated")) isolatedairlockairventList.Add(airventList[i]);
		else if (airventList[i].CustomName.ToLower().Contains("airlock")) airlockairventList.Add(airventList[i]);

		if (airventList[i].CustomName.ToLower().Contains("cockpit")) cockpitairventList.Add(airventList[i]);

	}
	return "A" + airventList.Count.ToString("0");
}

void airventOccupied() {
	for (int i = 0; i < cockpitairventList.Count; i++) {
		IMyAirVent av;
		av = airventList[i] as IMyAirVent;
		if (av != null) {
			if (av.IsDepressurizing && dGravity > .75) av.ApplyAction("OnOff_On");
		}
	}
}
void airventUnoccupied() {
	for (int i = 0; i < cockpitairventList.Count; i++) {
		IMyAirVent av;
		av = airventList[i] as IMyAirVent;
		if (av != null) {
			if (av.IsDepressurizing) av.ApplyAction("OnOff_Off");
		}
	}
}#endregion

#region cockpits

List < IMyTerminalBlock > cockpitList = new List < IMyTerminalBlock > ();
string cockpitInit() {
	{
		cockpitList = new List < IMyTerminalBlock > ();
		GridTerminalSystem.GetBlocksOfType < IMyCockpit > (cockpitList, (x = >x.CubeGrid == Me.CubeGrid));
	}
	return "C" + cockpitList.Count.ToString("0");
}
bool AnyCockpitOccupied() {
	for (int i = 0; i < cockpitList.Count; i++) {
		IMyCockpit sc = cockpitList[i] as IMyCockpit;
		if (sc.IsUnderControl) return true;
	}
	return false;
}#endregion

#region thrusters
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
			thrustLeft += thrustAllList[i].GetMaximum < float > ("Override");
			thrustLeftList.Add(thrustAllList[i]);
		}
		else if (accelerationDirection == identityMatrix.Right) {
			thrustRight += thrustAllList[i].GetMaximum < float > ("Override");
			thrustRightList.Add(thrustAllList[i]);
		}
		else if (accelerationDirection == identityMatrix.Backward) {
			thrustBackward += thrustAllList[i].GetMaximum < float > ("Override");
			thrustBackwardList.Add(thrustAllList[i]);
		}
		else if (accelerationDirection == identityMatrix.Forward) {
			thrustForward += thrustAllList[i].GetMaximum < float > ("Override");
			thrustForwardList.Add(thrustAllList[i]);
		}
		else if (accelerationDirection == identityMatrix.Up) {
			thrustUp += thrustAllList[i].GetMaximum < float > ("Override");
			thrustUpList.Add(thrustAllList[i]);
		}
		else if (accelerationDirection == identityMatrix.Down) {
			thrustDown += thrustAllList[i].GetMaximum < float > ("Override");
			thrustDownList.Add(thrustAllList[i]);
		}
	}
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
bool powerUpThrusters(List < IMyTerminalBlock > thrusters, int iPower = 100, int iTypes = thrustAll) {
	if (iPower > 100) iPower = 100;
	for (int thrusterIndex = 0; thrusterIndex < thrusters.Count; thrusterIndex++) {
		int iThrusterType = thrusterType(thrusters[thrusterIndex]);
		if ((iThrusterType & iTypes) > 0) {
			IMyThrust thruster = thrusters[thrusterIndex] as IMyThrust;
			float maxThrust = thruster.GetMaximum < float > ("Override");
			if (!thruster.IsWorking) {
				thruster.ApplyAction("OnOff_On");
			}
			thruster.SetValueFloat("Override", maxThrust * ((float) iPower / 100.0f));
		}
	}
	return true;
}
bool powerUpThrusters(string sFThrust, int iPower = 100, int iTypes = thrustAll) {
	if (iPower > 100) iPower = 100;
	List < IMyBlockGroup > groups = new List < IMyBlockGroup > ();
	GridTerminalSystem.GetBlockGroups(groups);
	for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++) {
		if (groups[groupIndex].Name == sFThrust) {
			List < IMyTerminalBlock > thrusters = null;
			groups[groupIndex].GetBlocks(thrusters, (x = >x.CubeGrid == Me.CubeGrid));
			return (powerUpThrusters(thrusters, iPower, iTypes));
		}
	}
	return false;
}
bool powerDownThrusters(List < IMyTerminalBlock > thrusters, int iTypes = thrustAll, bool bForceOff = false) {
	for (int thrusterIndex = 0; thrusterIndex < thrusters.Count; thrusterIndex++) {
		int iThrusterType = thrusterType(thrusters[thrusterIndex]);
		if ((iThrusterType & iTypes) > 0) {
			IMyThrust thruster = thrusters[thrusterIndex] as IMyThrust;
			thruster.SetValueFloat("Override", 0);
			if (thruster.IsWorking && bForceOff) thruster.ApplyAction("OnOff_Off");
			else if (!thruster.IsWorking && !bForceOff) thruster.ApplyAction("OnOff_On");
		}
	}
	return true;
}
bool powerDownThrusters(string sFThrust) {
	List < IMyBlockGroup > groups = new List < IMyBlockGroup > ();
	GridTerminalSystem.GetBlockGroups(groups);
	for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++) {
		if (groups[groupIndex].Name == sFThrust) {
			List < IMyTerminalBlock > thrusters = null;
			groups[groupIndex].GetBlocks(thrusters, (x = >x.CubeGrid == Me.CubeGrid));
			return (powerDownThrusters(thrusters));
		}
	}
	return false;
}
bool powerUpThrusters() {
	return (powerUpThrusters(thrustForwardList));
}
bool powerDownThrusters() {
	return (powerDownThrusters(thrustForwardList));
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

void Main(string sArgument) {
	bWorkingProjector = false;
	var list = new List < IMyTerminalBlock > ();
	GridTerminalSystem.GetBlocksOfType < IMyProjector > (list, (x = >x.CubeGrid == Me.CubeGrid));
	for (int i = 0; i < list.Count; i++) {
		if (list[i].IsWorking) {
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

	Log("clear");
	//	StatusLog("clear",getTextBlock(sTextPanelReport)); 
	if (!init) {
		if (bWorkingProjector) {
			Log("Construction in Progress\nTurn off projector to continue");
			StatusLog("Construction in Progress\nTurn off projector to continue", getTextBlock(sTextPanelReport));
		}
		bWantFast = true;
		doInit();
		bWasInit = true;
	}
	else {
		if (bWasInit) StatusLog(DateTime.Now.ToString() + " " + sInitResults, getTextBlock(longStatus), true);

		Echo(sInitResults);
		Deserialize();

		Log(craftOperation());
		Echo(craftOperation());
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

		if (gpsCenter is IMyRemoteControl) {
			Vector3D vNG = ((IMyRemoteControl) gpsCenter).GetNaturalGravity();
			double dLength = vNG.Length();
			dGravity = dLength / 9.81;
		}
		else dGravity = -1.0;

		if (processArguments(sArgument)) return;
		/* 
		if(AnyConnectorIsConnected())	output+="Connected"; 
		else output+="Not Connected"; 
	 
		if(AnyConnectorIsLocked())	output+="\nLocked"; 
		else output+="\nNot Locked"; 
*/
		Echo(output);
		Log(output);

		if (bWantFast) Echo("FAST!");

		if (dGravity >= 0) {
			Echo("Grav=" + dGravity.ToString(velocityFormat));
			if (dGravity > 0) Log("Planet Gravity " + dGravity.ToString(velocityFormat) + " g");
			else Log("No Planetary Gravity");
			Log(progressBar((int)(dGravity / 1.1 * 100)));

		}
		else Log("ERROR: No Remote Control found!");

		/* 
		doCargoCheck(); 
		Echo("Cargo="+cargopcent.ToString()+"%"); 
		Log("C:"+progressBar(cargopcent)); 
 
	batteryCheck(0,false); 
	Log("B:"+progressBar(batteryPercentage)); 
	if(iOxygenTanks>0) Log("O:" +progressBar(tanksFill(iTankOxygen))); 
	if(iHydroTanks>0) Log("H:" +progressBar(tanksFill(iTankHydro))); 
 
		Echo("Batteries="+batteryPercentage.ToString()+"%"); 
*/

		//		logState(); 
		doModes();
	}

	if (bWantFast)
	//		doTimerTriggers(aFTriggerNames); 
	doSubModuleTimerTriggers("[WCCT]");

	Serialize();
	doSubModuleTimerTriggers();

	bWasInit = false;

	verifyAntenna();
	float fper = 0;
	fper = Runtime.CurrentInstructionCount / (float) Runtime.MaxInstructionCount;
	Echo("Instructions=" + (fper * 100).ToString() + "%");

}#endregion

#region domodes
void doModes() {
	Echo("mode=" + iMode.ToString());

	if ((craft_operation & CRAFT_MODE_PET) > 0 && iMode != MODE_PET) setLightColor(lightsList, Color.Chocolate);

	if (AnyConnectorIsConnected() && iMode != MODE_LAUNCH && iMode != MODE_RELAUNCH && !((craft_operation & CRAFT_MODE_ORBITAL) > 0) && !((craft_operation & CRAFT_MODE_NAD) > 0)) {
		setMode(MODE_DOCKED);
	}
	if (iMode == MODE_IDLE) doModeIdle();
	else if (iMode == MODE_DUMBNAV) {
		doModeNav();
	}
	/* 
	else if (iMode == MODE_SLEDMMOVE)  
	{ 
	doModeSledmMove(); 
	} 
	else if (iMode == MODE_SLEDMRAMPD)  
	{ 
	doModeSledmRampD(); 
	} 
	else if (iMode == MODE_SLEDMLEVEL)  
	{ 
	doModeSledmLevel(); 
	} 
	else if (iMode == MODE_SLEDMDRILL)  
	{ 
	doModeSledmDrill(); 
	} 
	else if (iMode == MODE_SLEDMBDRILL)  
	{ 
	doModeSledmBDrill(); 
	} 
 */
	else if (iMode == MODE_DOCKING) {
		//	doModeDocking(); 
	}
	else if (iMode == MODE_DOCKED) {
		doModeDocked();
	}
	else if (iMode == MODE_LAUNCH) {
		//	doModeLaunch(); 
	}
	else if (iMode == MODE_PET) {
		doModePet();
	}
	else if (iMode == MODE_ATTENTION) {
		StatusLog("clear", getTextBlock(sTextPanelReport));
		StatusLog(moduleName + ":ATTENTION!", getTextBlock(sTextPanelReport));
		StatusLog(moduleName + ": current_state=" + current_state.ToString(), getTextBlock(sTextPanelReport));
		StatusLog("\nCraft Needs attention", getTextBlock(sTextPanelReport));

	}
}#endregion

#region manageautogyro
bool bWantAutoGyro = true;

#endregion

#region maininit

string sInitResults = "";
int currentInit = 0;

string doInit() {

	// initialization of each module goes here: 
	// when all initialization is done, set init to true. 
	Log("Init:" + currentRun.ToString());
	double progress = currentInit * 100 / 3;
	string sProgress = progressBar(progress);
	StatusLog(moduleName + sProgress, getTextBlock(sTextPanelReport));

	Echo("Init");
	if (currentInit == 0) {
		StatusLog("clear", getTextBlock(longStatus), true);
		StatusLog(DateTime.Now.ToString() + " " + OurName + ":" + moduleName + ":INIT", getTextBlock(longStatus), true);

		if (!modeCommands.ContainsKey("godock")) modeCommands.Add("godock", MODE_DOCKING);
		/* 
	if(!modeCommands.ContainsKey("launchprep")) modeCommands.Add("launchprep", MODE_LAUNCHPREP); 
	if(!modeCommands.ContainsKey("orbitallaunch")) modeCommands.Add("orbitallaunch", MODE_ORBITALLAUNCH); 
	if(!modeCommands.ContainsKey("sledmmove")) modeCommands.Add("sledmmove", MODE_SLEDMMOVE); 
	if(!modeCommands.ContainsKey("sledmrampd")) modeCommands.Add("sledmrampd", MODE_SLEDMRAMPD); 
	if(!modeCommands.ContainsKey("sledmdrill")) modeCommands.Add("sledmdrill", MODE_SLEDMDRILL); 
	if(!modeCommands.ContainsKey("sledmbdrill")) modeCommands.Add("sledmbdrill", MODE_SLEDMBDRILL); 
	if(!modeCommands.ContainsKey("sledmlevel")) modeCommands.Add("sledmlevel", MODE_SLEDMLEVEL); 
 */
		if (!modeCommands.ContainsKey("pet")) modeCommands.Add("pet", MODE_PET);
		//		parseConfiguration(); 
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
	else if(currentRun==2) 
	{ 
 */
		sInitResults += wheelsInit();
		sInitResults += lightsInit();
		sInitResults += airventInit();
		sInitResults += cockpitInit();
		sInitResults += ejectorsInit();
		sInitResults += gearsInit();
		sInitResults += tanksInit();

		sInitResults += massInit();
		sInitResults += NAVInit();
		sInitResults += gyrosetup();
		sInitResults += drillInit();
		sInitResults += sensorInit();
		sInitResults += antennaInit();

		Deserialize();

		autoConfig();
		bWantFast = false;
		sInitResults += modeOnInit(); // handle mode initializing from load/recompile.. 
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

//bool bControlVents=true; 
bool processArguments(string sArgument) {
	string output = "";

	string[] args = sArgument.Trim().Split(' ');

	if (args[0] == "timer") {
		if (bWantFast) {
			bWantFast = false;
			return false;
		}
		if (bCalcAssumed) {
			if (bGotStart) {
				bCalcAssumed = false;
				bGotStart = false;
				fAssumeElapsed = (float)(DateTime.Now.Subtract(dtStartTime).TotalSeconds * fAssumeSimSpeed);
			}
			else {
				dtStartTime = DateTime.Now;
				bGotStart = true;
			}
		}

		lastPosition = currentPosition;
		currentPosition = anchorPosition.GetPosition();
		lastVelocity = currentVelocity;

		currentVelocity = (currentPosition - lastPosition) / Runtime.TimeSinceLastRun.TotalSeconds; // fAssumeElapsed; 
		//		currentVelocity = (currentPosition - lastPosition) / ElapsedTime.TotalSeconds;// fAssumeElapsed; 
		center = Me.CubeGrid.GridIntegerToWorld(anchorPosition.Position);
		forward = Me.CubeGrid.GridIntegerToWorld(anchorPosition.Position + iForward) - center;
		up = Me.CubeGrid.GridIntegerToWorld(anchorPosition.Position + iUp) - center;
		left = Me.CubeGrid.GridIntegerToWorld(anchorPosition.Position + iLeft) - center;
		forward.Normalize();
		up.Normalize();
		left.Normalize();
		Echo("assume=" + fAssumeElapsed.ToString("0.000") + " elapsed=" + Runtime.TimeSinceLastRun.TotalSeconds.ToString("0.000"));
		//Echo("assume="+fAssumeElapsed.ToString("0.000")+ " elapsed="+ElapsedTime.TotalSeconds.ToString("0.000")); 
		fSimSpeed = fAssumeElapsed / (float) Runtime.TimeSinceLastRun.TotalSeconds;
		//		fSimSpeed=fAssumeElapsed/(float)ElapsedTime.TotalSeconds; 
		vectorForward = forward.Project(currentVelocity) * 1 / fSimSpeed;
		vectorUp = up.Project(currentVelocity) * 1 / fSimSpeed;
		vectorLeft = left.Project(currentVelocity) * 1 / fSimSpeed;
		velocityShip = currentVelocity.Length() * 1 / fSimSpeed;
		velocityForward = vectorForward.Length() * 1 / fSimSpeed;
		velocityUp = vectorUp.Length() * 1 / fSimSpeed;
		velocityLeft = vectorLeft.Length() * 1 / fSimSpeed;

		output += velocityShip.ToString(velocityFormat) + " m/s";
		output += " (" + (velocityShip * 3.6).ToString(velocityFormat) + "km/h)";
		if (velocityForward > 0.1f) output += "\nF/B:" + velocityForward.ToString(velocityFormat) + " m/s";
		if (velocityUp > 0.1f) output += "\nU/D:" + velocityUp.ToString(velocityFormat) + " m/s";
		if (velocityLeft > 0.1f) output += "\nL/R:" + velocityLeft.ToString(velocityFormat) + " m/s";

		//		output+="\nElapsed:"+ElapsedTime.TotalSeconds.ToString(); 
		output += "\nSimspeed=" + fSimSpeed.ToString(velocityFormat);
		Echo(output);
		output += "\n" + progressBar((int)(fSimSpeed * 100));
		Log(output);

		double velocityShip2 = 0;
		output = "";
		velocityShip2 = ((IMyShipController) anchorPosition).GetShipSpeed();
		output += "V2:" + velocityShip2.ToString(velocityFormat) + " m/s";
		Echo(output);

		MyShipMass myMass;
		output = "";
		myMass = ((IMyShipController) anchorPosition).CalculateShipMass();
		output += "Base Mass=" + myMass.BaseMass.ToString() + "\n";
		output += "Total Mass=" + myMass.TotalMass.ToString() + "\n";
		Echo(output);

	}
	else if (args[0] == "idle") ResetToIdle();
	else if (args[0] == "godock") {
		setMode(MODE_DOCKING); //StartModeDocking(); 
	}
	else if (args[0] == "startnav") {
		StartNav();
	}
	else if (args[0].ToLower() == "undock") {
		prepareForSolo();
	}
	else if (args[0].ToLower() == "dock") {
		prepareForSupported();
	}
	else if (args[0].ToLower() == "coast") {
		if (thrustBackwardList.Count > 1) {
			blockApplyAction(thrustBackwardList, "OnOff");
		}
	}
	else if (args[0] == "setvaluef") {
		Echo("SetValueFloat");
		//Miner Advanced Rotor:UpperLimit:-24 
		string sArg = "";
		for (int i = 1; i < args.Length; i++) {
			sArg += args[i];
			if (i < args.Length - 1) {
				sArg += " ";
			}
		}
		string[] cargs = sArg.Trim().Split(':');

		if (cargs.Length < 3) {
			Echo("Invalid Args");
			return true;
		}
		IMyTerminalBlock block;
		block = (IMyTerminalBlock) GridTerminalSystem.GetBlockWithName(cargs[0]);
		if (block == null) {
			Echo("Block not found:" + cargs[0]);
			return false;
		}
		float fValue = 0;
		bool fOK = float.TryParse(cargs[2].Trim(), out fValue);
		if (!fOK) {
			Echo("invalid float value:" + cargs[2]);
			return false;
		}
		Echo("SetValueFloat:" + cargs[0] + " " + cargs[1] + " to:" + fValue.ToString());
		block.SetValueFloat(cargs[1], fValue);
	}
	else if (args[0] == "setsimspeed") {
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
		fAssumeSimSpeed = fValue;
		bCalcAssumed = true;

	}
	else {
		int iDMode;
		if (modeCommands.TryGetValue(args[0].ToLower(), out iDMode)) {
			setMode(iDMode);
		}
	}
	return false; // keep processing in main 
}#endregion

#region NAVMODE
void StartNav() {
	StatusLog(DateTime.Now.ToString() + " ACTION: Start Dumb Nav", getTextBlock(longStatus), true);
	batteryDischargeSet();
	if (navTriggerTimer != null) {
		navStatus.SetCustomName(sNavStatus + " Dumb NAV Started");
		if (navEnable != null) {
			blockApplyAction(navEnable, "OnOff_On");
		}
		else Echo("No NAVENABLE!");
		navTriggerTimer.ApplyAction("Start");
		setMode(MODE_DUMBNAV);
	}
}
void doModeNav() {
	string sStatus = navStatus.CustomName;
	if (sStatus.Contains("Done")) {
		ResetToIdle();
	}
}#endregion

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
const int MODE_ORBITALLAUNCH = 28;

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

	if (iMode == MODE_DUMBNAV) {
		// we were navigating.  We can't continue because it's A DUMB nav and we don't know where we are in the path. 
		ResetToIdle();
	}
	else if (iMode == MODE_PET) {
		bValidPlayerPosition = false;
		ResetToIdle();
	}

	return ">";
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
void startNavWaypoint(Vector3D vWaypoint, bool bOrient = false, int iRange = 10, int iMaxSpeed = -1) {
	string sNav;
	sNav = "";
	if (iMaxSpeed > 0) {
		sNav += "S " + iMaxSpeed;
		if (bNavCmdIsTextPanel) sNav += "\n";
		else sNav += ";";
	}
	sNav += "D " + iRange;
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
	if (navTriggerTimer != null) navTriggerTimer.ApplyAction("Start");
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
	bValidPlayerPosition = false;
	setMode(MODE_IDLE);
	if (AnyConnectorIsConnected() && iMode != MODE_LAUNCH && iMode != MODE_RELAUNCH && !((craft_operation & CRAFT_MODE_ORBITAL) > 0) && !((craft_operation & CRAFT_MODE_NAD) > 0)) setMode(MODE_DOCKED);
}
void doModeIdle() {

	StatusLog("clear", getTextBlock(sTextPanelReport));

	StatusLog("Manual Control", getTextBlock(sTextPanelReport));
	/* 
 if ((craft_operation & CRAFT_MODE_ORBITAL) > 0) { 
  StatusLog(thrusterInfo(), getTextBlock(sTextPanelReport)); 
 }  
*/
	if ((craft_operation & CRAFT_MODE_PET) > 0) {
		StartModePet();
	} else {
		if (bWantAutoGyro && dGravity > 0 && (craft_operation & CRAFT_MODE_NOAUTOGYRO) > 0) {
			string sOrientation = "";
			if ((craft_operation & CRAFT_MODE_ROCKET) > 0) sOrientation = "rocket";

			GyroMain(sOrientation);
		}
	}
	if (AnyCockpitOccupied()) {
		Echo("Occupied");
		airventOccupied();
	}
	else {
		Echo("NOT Occupied");
		airventUnoccupied();
	}

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
		if (groups[groupIndex].Name == sGroup) { //var blocks=null; 
			List < IMyTerminalBlock > blocks = null;
			groups[groupIndex].GetBlocks(blocks, (x = >x.CubeGrid == Me.CubeGrid));

			//blocks=groups[groupIndex].Blocks; 
			List < IMyTerminalBlock > theBlocks = blocks;
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
void GyroMain(string argument) {
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
	}
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

DateTime dtStartShip;

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
			//			Echo(SAVE_FILE_NAME + " (TextPanel) is missing or Named incorrectly. "); 
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

	string sLine;

	string sSave;
	if (SaveFile == null) sSave = Storage;
	else sSave = SaveFile.GetPublicText();

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

	sLine = getLine();
	ParseVector3d(sLine, out x, out y, out z);
	vHome = new Vector3D(x, y, z);

	sLine = getLine();
	bValidDock = asBool(sLine);

	sLine = getLine();
	bValidLaunch1 = asBool(sLine.ToLower());
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

#region pet

Vector3D vPlayerPosition;
bool bValidPlayerPosition = false;

void StartModePet() {
	current_state = 0;
	setMode(MODE_PET);
}

void doModePet() {
	Log("Pet Mode");
	Log("state=" + current_state.ToString());

	IMyTextPanel txtPanel = getTextBlock("Text panel Pet");

	StatusLog("clear", txtPanel);
	StatusLog("state=" + current_state.ToString(), txtPanel);

	List < IMyTerminalBlock > aSensors = activeSensors();
	//StatusLog(aSensors.Count.ToString() + " active sensors",txtPanel); 
	//Echo(aSensors.Count.ToString() + " active sensors"); 
	for (int i = 0; i < aSensors.Count; i++) {
		IMySensorBlock s = aSensors[i] as IMySensorBlock;
		//StatusLog(aSensors[i].CustomName + " ACTIVE!",txtPanel); 
		//Echo(aSensors[i].CustomName + " ACTIVE!"); 
		StatusLog(s.LastDetectedEntity.ToString(), txtPanel);
		//Echo(s.LastDetectedEntity.ToString()); 
		if (typeEntityDetected(s.LastDetectedEntity.ToString()) == ENTITY_TYPE_PLAYER) {
			vPlayerPosition = s.LastDetectedEntity.GetPosition();
			bValidPlayerPosition = true;
		}

	}
	double dist = 0;
	if (bValidPlayerPosition) {
		Vector3D vPos = gpsCenter.GetPosition();
		dist = (vPos - vPlayerPosition).Length();
		//		listSetValueFloat(lightsList,"BlinkLenght",0); 
	}
	//else 
	//		listSetValueFloat(lightsList,"BlinkLenght",1); 

	if (current_state == 0) { // searching 
		if (bValidPlayerPosition) {
			//			listSetValueFloat(lightsList,"BlinkLenght",0); 
			setLightColor(lightsList, Color.White);
			current_state = 1;
		}
		else {
			//			listSetValueFloat(lightsList,"BlinkLenght",1); 
			setLightColor(lightsList, Color.Red);
		}

	}
	else if (current_state == 1) {
		if (dist < 10) {
			StatusLog("Player Close: Standing Still", txtPanel);

			// stand still 
			current_state = 0;
		}
		else // if(dist<20) 
		{
			StatusLog("Aiming toward player", txtPanel);
			// start orient to player 
			startNavWaypoint(vPlayerPosition, true, 5);
			setLightColor(lightsList, Color.Blue);
			current_state = 2;
		}
		/* 
		else 
		{ 
			startNavWaypoint(vPlayerPosition,false,5); 
			current_state=4; 
		} 
 */
	}
	else if (current_state == 2) {
		// orientating to player 
		if (dist < 10) {
			ResetMotion();
			current_state = 0;
		}
		string sStatus = navStatus.CustomName;
		StatusLog(sStatus, txtPanel);
		StatusLog("Waiting for alignment with player", txtPanel);

		if (sStatus.Contains("Shutdown")) { // somebody hit nav override 
			current_state = 0;
		}
		if (sStatus.Contains("Done")) {

			//			if(dist<5) 
			if (dist < 7) {
				StatusLog("Close enought to player", txtPanel);
				Echo("Close Enough to player");
				current_state = 0; // check again.	 
			}
			else {
				StatusLog("Initiate Move to player", txtPanel);
				StatusLog(DateTime.Now.ToString() + "Initiate Move to player", getTextBlock(longStatus), true);

				Echo("Moving to player");
				Echo("Dist=" + dist.ToString());
				//StatusLog(DateTime.Now.ToString()+" Dist="+dist.ToString(),getTextBlock(longStatus),true); 
				gpsCenter = rc;
				MatrixD g2w = GetGrid2WorldTransform(gpsCenter.CubeGrid);
				Vector3D gridPos = (new Vector3D(gpsCenter.Min + gpsCenter.Max)) / 2.0;
				Vector3D calcPos = Vector3D.Transform(gridPos, ref g2w);
				MatrixD b2w = GetBlock2WorldTransform(gpsCenter);
				Vector3D vVec = b2w.Forward;
				vVec.Normalize();
				//				vLaunch1= gpsCenter.GetPosition() + vVec * 10;//(dist-10); 
				double dSpace = 5;
				if ((craft_operation & CRAFT_MODE_SLED) > 0) dSpace += 7;
				double aDist = dist - dSpace;
				if (aDist < 0) aDist = 0;
				//StatusLog(DateTime.Now.ToString()+" aDist="+aDist.ToString(),getTextBlock(longStatus),true); 
				vLaunch1 = gpsCenter.GetPosition() + vVec * aDist;
				bValidLaunch1 = true;

				setLightColor(lightsList, Color.Green);
				startNavWaypoint(vLaunch1, false, 5, 15);
				current_state = 4;

			}
		}
	}
	else if (current_state == 3) { // initiate move to player 
		startNavWaypoint(vPlayerPosition, true, 5);
		current_state = 4;
	}
	else if (current_state == 4) {
		StatusLog("Moving to player", txtPanel);
		Echo("Moving to player");
		// moving to player 
		string sStatus = navStatus.CustomName;
		if (dist < 10) {
			ResetMotion();
			current_state = 0;
		}

		StatusLog(sStatus, txtPanel);

		if (sStatus.Contains("Shutdown")) { // somebody hit nav override 
			current_state = 0;
		}

		if (sStatus.Contains("Done")) {
			current_state = 0; // try again. 
		}
	}

}#endregion

#region docking
void StartModeDocking() {
	current_state = 0;
	setMode(MODE_DOCKING);
}

/* 
 * 
 * Going home states: Includes docking 
 *0 init: pick nearest point to nav to (target or Home) 
 * 
 * all navigating with collision avoid 
 * 
 *rest of states in order: 
 *1 Orient to target (in case on 'near' side of asteroid. avoid collision detect of state 4) 
 * but only if home is >55 meters 
 *2 
 *3 
 * 
 *4 proceed to Home 
 *collision detected: 
 *13 delay for dampeners 
 *5 circumnavigate obsticle 
 *12 clear obst 
 * 
 *6 Arrive at Home: 
 * actiongodock 
 * 18 iff keen nav on. docking 
 * 
 *7 Proceed to Launch1 
 *8 Arrive at Launch1: 
 *9 Orient to Home 
 *10 start Reverse to vDock 
 *14 check distance to dock/collision 
 *15 orient 
 *16 roll 
 *17 reverse minor 
 * 
 *11 Dock 
 * */

//bool bAutopilotSet=false; 
//Vector3D vCurrentNavTarget; 
void doModeDocking() {
	Log("Sled Docking Mode");
	Log("state=" + current_state.ToString());
	StatusLog("Sled Docking Mode", getTextBlock(sTextPanelReport));
	StatusLog("state=" + current_state.ToString(), getTextBlock(sTextPanelReport));

}#endregion

#region relaunch
void StartRelaunch() {
	StatusLog(DateTime.Now.ToString() + " ACTION: ReLaunch", getTextBlock(longStatus), true);
	if (!AnyConnectorIsConnected()) {
		StatusLog("Can't perform action unless docked", getTextBlock(longStatus), true);
		ResetToIdle();
		return;
	}
	/* 
	if (!bValidTarget && !bValidInitialContact && !bValidAsteroid)  
	{ 
//		setAlertState(ALERT_CANNOTREACH); 
		return; 
	} 
 */
	//	setAlertState(ALERT_LAUNCHING); 
	setMode(MODE_RELAUNCH);
	//	dtStartShip = DateTime.Now; 
	current_state = 0;
	//	Serialize(); 
}

void doModeRelaunch() {
	if (current_state == 0) {
		dtStartShip = DateTime.Now;
		current_state = 1;
	}
	// setAlertState(ALERT_LAUNCHING); 
	DateTime dtMaxWait = dtStartShip.AddSeconds(5.0f);
	DateTime dtNow = DateTime.Now;
	if (DateTime.Compare(dtNow, dtMaxWait) > 0) {
		StartLaunch();
	}
}#endregion

#region launch
void StartLaunch() {
	current_state = 0;
	StatusLog(DateTime.Now.ToString() + " ACTION: StartLaunch", getTextBlock(longStatus), true);
	if (!AnyConnectorIsConnected()) {
		StatusLog("Can't perform action unless docked", getTextBlock(longStatus), true);
		ResetToIdle();
		return;
	}
	blockApplyAction(thrustAllList, "OnOff_On");
	setMode(MODE_LAUNCH);
	current_state = 0;
}
void doModeLaunch() {
	if (AnyConnectorIsLocked()) {
		ConnectAnyConnectors(false, "OnOff_Off");
		return;
	}
	if (current_state == 0) {
		powerUpThrusters(thrustForwardList);
		current_state = 1;
	}
	Vector3D vPos = gpsCenter.GetPosition();
	double dist = (vPos - vDock).Length();
	powerUpThrusters(thrustForwardList);
	if (dist > 10) {
		ConnectAnyConnectors(true, "OnOff_On");
	} {
		if (velocityShip > 2) powerUpThrusters(thrustForwardList, 25);
		else powerUpThrusters(thrustForwardList);
	}
	if (dist > 45) {}
}#endregion

void prepareForSupported() {

	if ((craft_operation & CRAFT_MODE_NOPOWERMGMT) == 0) if (!batteryCheck(30, true, getTextBlock(sTextPanelReport))) if (!batteryCheck(80, false)) batteryCheck(100, false);

	blockApplyAction(thrustAllList, "OnOff_Off");

	blockApplyAction(tankList, "Stockpile_On");
	//	blockApplyAction(gasgenList,"OnOff_Off"); 
	//	blockApplyAction(airventList,"OnOff_Off"); 
}

void prepareForSolo() {
	powerDownThrusters(thrustAllList);

	if ((craft_operation & CRAFT_MODE_NOPOWERMGMT) == 0) batteryDischargeSet();

	//	if(AnyConnectorIsConnected() || AnyConnectorIsLocked()) 
	//	{ 
	ConnectAnyConnectors(false, "OnOff_Off");
	//	} 
	blockApplyAction(tankList, "Stockpile_Off");
	//	blockApplyAction(gasgenList,"OnOff_On"); 
	//	blockApplyAction(airventList,"OnOff_On"); 
	blockApplyAction(gearList, "Unlock");
}

#region docked

void doModeDocked() {
	return;
	/* 
StatusLog("clear",getTextBlock(sTextPanelReport)); 
StatusLog("WCCM:DOCKED!",getTextBlock(sTextPanelReport)); 
 
	prepareForSupported(); 
 
	if (!AnyConnectorIsConnected())  
	{ // we came unconnected. NOTE: locked!=connected 
Echo("NOT CONNECTED!"); 
		setMode(MODE_IDLE); 
		batteryDischargeSet(); 
		init=false; // reinit our blocks 
		sInitResults=""; 
	}  
	else  
	{ 
 
		if (bAutoRelaunch && bValidDock)  
		{ 
			Echo("Docked. Checking Relaunch"); 
			if (batteryPercentage > batterypcthigh && cargopcent < cargopctmin)  
			{ 
				StartRelaunch(); 
				return; 
			}  
			else 
				Echo(" Awaiting Relaunch Criteria"); 
		} 
  
		listSetValueFloat(wheelList,"Friction",100); 
 
//		groupApplyAction(sSMNT, "OnOff_Off"); 
//		if (bControlLights) groupApplyAction(sgRL, "OnOff_Off"); 
 
		if((craft_operation&CRAFT_MODE_ORBITAL)>0) 
		{ 
			Echo("Orbital; no autoGPS"); 
		} 
		else 
		{ 
			IMyTerminalBlock gpsCenter; 
			gpsCenter = rc; 
			MatrixD g2w = GetGrid2WorldTransform(gpsCenter.CubeGrid); 
			Vector3D gridPos = (new Vector3D(gpsCenter.Min + gpsCenter.Max)) / 2.0; 
			Vector3D calcPos = Vector3D.Transform(gridPos, ref g2w); 
			MatrixD b2w = GetBlock2WorldTransform(gpsCenter); 
			Vector3D vVec = b2w.Forward; 
			vVec.Normalize(); 
			vDock = gpsCenter.GetPosition(); 
			bValidDock = true; 
			vLaunch1 = gpsCenter.GetPosition() + vVec * 20; 
			bValidLaunch1 = true; 
			vHome = gpsCenter.GetPosition() + vVec * 50; 
			bValidHome = true; 
		} 
	} 
*/
}#endregion

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
bool batteryCheck(int targetMax, bool bEcho = true, IMyTextPanel textBlock = null, bool bProgress = false) {
	List < IMyTerminalBlock > batteries = new List < IMyTerminalBlock > ();
	GridTerminalSystem.GetBlocksOfType < IMyBatteryBlock > (batteries, (x = >x.CubeGrid == Me.CubeGrid));
	float totalCapacity = 0;
	float totalCharge = 0;
	bool bFoundRecharging = false;
	float f;
	batteryPercentage = 0;
	for (int ib = 0; ib < batteries.Count; ib++) {
		float charge = 0;
		float capacity = 0;
		//  bool thecharge = true; 
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
	List < IMyTerminalBlock > batteries = new List < IMyTerminalBlock > ();
	GridTerminalSystem.GetBlocksOfType < IMyBatteryBlock > (batteries, (x = >x.CubeGrid == Me.CubeGrid));
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
	//	int pcent = 0; 
	//	if (initialCargo == 0) initialCargo = lContainers.Count; 
	//	else if (initialCargo != lContainers.Count) integrityFail(); 
	for (int i = 0; i < lContainers.Count; i++) {
		//if(i==0)  
		//Echo("lContainers="+lContainers[i].DefinitionDisplayNameText+"'"); 
		var count = lContainers[i].GetInventoryCount(); // Multiple inventories in Refineriers, Assemblers, Arc Furnances. 
		for (var invcount = 0; invcount < count; invcount++) {
			//VRage.ModAPI.Ingame.IMyInventory // 1.123.007 (hotpatch) 
			//VRage.ModAPI.Ingame.IMyInventory // 1.123.007 (hotpatch) 
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

#region autoconfig
void autoConfig() {
	craft_operation = CRAFT_MODE_AUTO;
	if ((craft_operation & CRAFT_MODE_MASK) == CRAFT_MODE_AUTO) {
		int iThrustModes = 0;

		if (Me.CustomName.ToLower().Contains("nad")) craft_operation |= CRAFT_MODE_NAD;
		if (Me.CustomName.ToLower().Contains("rotor")) craft_operation |= CRAFT_MODE_ROTOR;
		else if (
		/*wheelList.Count>0 || */
		Me.CustomName.ToLower().Contains("sled"))
		craft_operation |= CRAFT_MODE_SLED;
		if (ionThrustCount > 0) {
			iThrustModes++;
			craft_operation |= CRAFT_MODE_ION;
		}
		if (hydroThrustCount > 0) {
			iThrustModes++;
			craft_operation |= CRAFT_MODE_HYDRO;
		}
		if (atmoThrustCount > 0) {
			iThrustModes++;
			craft_operation |= CRAFT_MODE_ATMO;
		}
		if (iThrustModes > 1 || Me.CustomName.ToLower().Contains("orbital")) craft_operation |= CRAFT_MODE_ORBITAL;
		if (Me.CustomName.ToLower().Contains("rocket")) craft_operation |= CRAFT_MODE_ROCKET;
		if (Me.CustomName.ToLower().Contains("pet")) craft_operation |= CRAFT_MODE_PET;
		if (Me.CustomName.ToLower().Contains("noautogyro")) craft_operation |= CRAFT_MODE_NOAUTOGYRO;
		if (Me.CustomName.ToLower().Contains("nopower")) craft_operation |= CRAFT_MODE_NOPOWERMGMT;
	}

	if (drillList.Count > 0) craft_operation |= CRAFT_TOOLS_DRILLS;
	if (ejectorsList.Count > 0) craft_operation |= CRAFT_TOOLS_EJECTORS;

}

#endregion

#region Antenna

List < IMyTerminalBlock > antennaList = new List < IMyTerminalBlock > ();

string antennaInit() {
	{
		antennaList = new List < IMyTerminalBlock > ();
		GridTerminalSystem.GetBlocksOfType < IMyRadioAntenna > (antennaList, (x = >x.CubeGrid == Me.CubeGrid));
	}

	return "A" + antennaList.Count.ToString("0");
}

//// Verify antenna stays on to fix keen bug where antenna will turn itself off when you try to remote control 
void verifyAntenna() {
	blockApplyAction(antennaList, "OnOff_On");
}
#endregion
