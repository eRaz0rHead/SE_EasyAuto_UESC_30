
class PilotAssist {

  IMyGridTerminalSystem grid;

  public PilotAssist(IMyGridTerminalSystem grid) {
    this.grid = grid;
  }
  
  
  
  // Automatic Drive Assist :  < Enabled >
  //    Helios Grav Drive   :  < ON / Engaged >
  //    Hydrogen Thrusters  :  < OFF / Conserving Fuel >
  //    Inertial Dampeners  :  < ON >
  //    
  //    Press 1 = Assisted Orbital Descent.   
  //          9 = Manual Control
  //          
     
  
  // < Entering Natural Gravity field >
  //      Automatic switch to Hydrogen Thrusters at 0.5Gs
  
  //  ALERT : Inufficient Hydrogen to land on planet.
  //          Abort re-entry and return to space until tanks are refilled.
  
  
  //  < Engaging Hydrogen Thrusters >
  //        in 3 .. 2 .. 1
  //  < Fuel Conservation : Disengaging Hydrogen Thrusters >
  //        in 3 .. 2 .. 1
  
  // Automatic Drive Assist : < Disabled >
  //    This vessel is now under manual control.
  //    May Heaven Help us all.
  //
  //    Manual drive controls are located on your second hotbar.
  // Press  1 to reactivate Automatic Drive Assist  (Please!)
  //        2 to enable Gravity Drive. 
  //        3 to reset Gravity Drive. 
  //        4 to disable Gravity Drive. 
  //        5 to toggle on/off Hydrogen thrusters.
  
  
  // FUNCTIONS
  
  // Detect atmosphere by reading external Air Vent? Might not be useful for this script.
  
  // Detect Natural Gravity vector.
  // Detect Dampeners
  // 
  
  // Detect Altitude.

  
}
