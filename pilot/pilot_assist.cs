
class PilotAssist {

  IMyGridTerminalSystem grid;

  public PilotAssist(IMyGridTerminalSystem grid) {
    this.grid = grid;
  }
  
  
  // MESSAGES: 
  // Automatic Drive Assist : 
  //    Press 1 = Assisted Orbital Descent.   
  //          9 = Manual Control
  //          
  // Warning : Entering Natural Gravity field. /n Automatic Drive Assist will switch to Hydrogen Thrusters at 0.5Gs
  //  ALERT : Inufficient Hydrogen to land on planet. /n Abort re-entry and return to space until tanks are refilled.
  // Automatic Drive Assist (Press 9 for Manual Control) : Engaging Hydrogen Thrusters in 3 .. 2 .. 1
  // Automatic Drive Assist (Press 9 for Manual Control) : 
  //          Hydrogen fuel conservation enabled / Disengaging Hydrogen Thrusters in 3 .. 2 .. 1
  
  // Automatic Drive Assist : Disabled 
  //    This vessel is now under manual control.
  //    May Heaven Help us all.
  //    Manual drive controls are located on your second hotbar.
  // Press  1 to reactivate Automatic Drive Assist  (please!)
  //        2 to turn on Gravity Drive. 
  //        3 to recalibrate Gravity Drive. 
  //        4 to turn off Gravity Drive. 
  //        5 to toggle on/off Hydrogen thrusters.
  
  
  // FUNCTIONS
  
  // Detect atmosphere by reading external Air Vent? Might not be useful for this script.
  
  // Detect Natural Gravity vector.
  // Detect Dampeners
  // 
  
  // Detect Altitude.

  
}
