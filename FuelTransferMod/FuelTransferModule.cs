using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public struct FuelTransferParameters
{
    public String basePlanet;
    public double baseLatitude;
    public double baseLongitude;
    public bool windowed;
    public float displayX;
    public float displayY;
}

interface IFuelSource
{
    float Fuel { get; set; }
    String FuelType { get; } // Name your fuel source!
    // We may add more in the future if we can dynamically figure out what other mods are available.
    // I think it'd be awesome to support more than just these two fuel types.
    bool RequestFuel(float amount, String fuelType);
    // First checks that we can grab the fuel we want, and if so, removes the fuel from the tank.
    // Despite the fact that we do check if your part is not dead, and that it has sufficient fuel,
    // you should also check that we didn't mess up.
    // The simplest implementation for this would do:
    /*
    if ((this.States == PartStates.DEAD) || (this.Fuel.get() - amount) <= 0 || (this.FuelType.get().Equals(fuelType)))
        return false;
    this.fuel -= amount;
    return true;
    */
    // If your part does anything special, you may want to implement this differently.
}

public class FuelTransferPod : CommandPod
{
    //see cfg file for explanation of m_parameters
    public String basePlanet = "Kerbin";
    public double baseLatitude = -0.103;
    public double baseLongitude = -74.570;
    public bool windowed = true;
    public float displayX = 125;
    public float displayY = 0;

    FuelTransferParameters parameters;

    FuelTransferCore core;

    public FuelTransferPod()
    {
        core = new FuelTransferCore(this);
    }

    protected override void onPartAwake()
    {
        base.onPartAwake();
    }

    protected override void onPartFixedUpdate()
    {
        core.onPartFixedUpdate();
        base.onPartFixedUpdate();
    }


    protected override void onFlightStart()
    {
        parameters.basePlanet = basePlanet;
        parameters.baseLatitude = baseLatitude;
        parameters.baseLongitude = baseLongitude;
        parameters.windowed = windowed;
        parameters.displayX = displayX;
        parameters.displayY = displayY;

        core.applyParameters(parameters);
        core.onFlightStart();
        base.onFlightStart();
    }

    protected override void onPartDestroy()
    {
        core.onPartDestroy();
        base.onPartDestroy();
    }

    protected override void onDisconnect()
    {
        core.onDisconnect();
        base.onDisconnect();
    }

    /// <summary>
    /// Called when the m_part is started by Unity
    /// </summary>
    protected override void onPartStart()
    {
    }


}
public class FuelTransferModule : Part
{
    //see cfg file for explanation of m_parameters
    public String basePlanet = "Kerbin";
    public double baseLatitude = -0.103;
    public double baseLongitude = -74.570;
    public bool windowed = true;
    public float displayX = 125;
    public float displayY = 0;

    FuelTransferParameters parameters;

    FuelTransferCore core;

    public FuelTransferModule()
    {
        core = new FuelTransferCore(this);
    }

    protected override void onPartAwake()
    {
        base.onPartAwake();
    }
    
    protected override void onPartFixedUpdate()
    {
        core.onPartFixedUpdate();
        base.onPartFixedUpdate();
    }


    protected override void onFlightStart()
    {
        parameters.basePlanet = basePlanet;
        parameters.baseLatitude = baseLatitude;
        parameters.baseLongitude = baseLongitude;
        parameters.windowed = windowed;
        parameters.displayX = displayX;
        parameters.displayY = displayY;

        core.applyParameters(parameters);
        core.onFlightStart();
        base.onFlightStart();
    }

    protected override void onPartDestroy()
    {
        core.onPartDestroy();
        base.onPartDestroy();
    }

    protected override void onDisconnect()
    {
        core.onDisconnect();
        base.onDisconnect();
    }

    /// <summary>
    /// Called when the m_part is started by Unity
    /// </summary>
    protected override void onPartStart()
    {
    }


}
public class FuelTransferCore
{
    #region Member Data
    const int WINDOW_ID = 93323;
    const double PROXIMITY_DISTANCE = 50000d; // 50KM
    Rect m_window_pos = new Rect(200, 50, 10, 10);
    FuelTransferParameters m_parameters;

    // The Part
    Part m_part;

    Vector2 m_source_vessel_scroll = new Vector2();
    Vector2 m_dest_vessel_scroll = new Vector2();
    //public RefuelTargets m_refuel_targets;


    Vector2 m_source_tanks_scroll = new Vector2();
    Vector2 m_dest_tanks_scroll = new Vector2();
    public List<Part> m_source_tanks = new List<Part>();

    public List<Part> m_dest_tanks = new List<Part>();

    // UI Toggles
    bool m_system_online = false;

    float m_transfer_amount = 0;
    float m_transfer_amount_percent = 0;

	List<String> fts; // FuelTypeStrings

    int m_fuel_type = 0;
    const int RegularFuel = 0;
    const int RCSFuel = 1;

    Vessel m_source_vessel = null;
    Part m_source_tank = null;
    Vessel m_dest_vessel = null;
    Part m_dest_tank = null;
    Part m_dest_tank_editor = null; //Stores the editor version of the destination fuel tank, so we can retrieve the max fuel
    #endregion

    public FuelTransferCore(Part part)
    {
        this.m_part = part;
    }

    public void applyParameters(FuelTransferParameters parameters)
    {
        this.m_parameters = parameters;
    }

	List<String> otherFuelParts ()
	{
		List<String> ret = new List<String> ();
		foreach (Vessel v in FlightGlobals.Vessels) {
			foreach (Part p in v.parts) {
				if (p is IFuelSource) {
					IFuelSource ifs = (IFuelSource)p;
					if (!ret.Contains(ifs.GetType())) {
						ret.Add (ifs.GetType());
					}
				}
			}
		}
	}

    //decide whether a vessel has fueltanks with fuel left in them!
    bool is_refuel_target (Vessel v, int fuelType)
    {
        foreach (Part p in v.parts) {
            if (p.State == PartStates.DEAD)
                continue;
            if (p is IFuelSource) {
                if (((IFuelSource)p).fuelType == fuelType) {
                    return true;
                } else {
                    continue;
                }
            }
            if (fuelType == RegularFuel)
            {
                if (p.GetType() == typeof(FuelTank)) { return true; }
            }
            else if (fuelType == RCSFuel)
            {
                if (p.GetType() == typeof(RCSFuelTank)) { return true; }
            }
        }
        return false;
    }

    float getFuelForPartAndFueltype (Part p, int fuelType)
    {
        if (p is IFuelSource) {
            IFuelSource ifs = (IFuelSource)p;
            if (ifs.fuelType != fuelType)
                return -1;
            return ifs.fuel;
        }
        if (fuelType == RegularFuel) {
            if (p.GetType () == typeof(FuelTank))
                return ((FuelTank)p).fuel;
        } else if (fuelType == RCSFuel) {
            if (p.GetType () == typeof(RCSFuelTank))
                return ((RCSFuelTank)p).fuel;
        }
        return -1;
    }

    private void addFuel(Part dest, float amount, int fuelType)
    {/*
        if (dest == null)
            return;
*/
        if (dest is IFuelSource) {
            ((IFuelSource)dest).fuel += amount;
        }
        if (fuelType == RegularFuel) {
            if (dest.GetType() == typeof(FuelTank)) {
                ((FuelTank)dest).fuel += amount;
            }
        } else if (fuelType == RCSFuel) {
            if (dest.GetType() == typeof(RCSFuelTank)) {
                ((RCSFuelTank)dest).fuel += amount;
            }
        }
    }

    bool transferFuel (Part source, Part dest, float amount, int FuelType)
    {
        bool wasDeactive = false;
        if (dest.State == PartStates.DEACTIVATED) {
            dest.force_activate();
            wasDeactive = true;
        }
        float fuelBefore = getFuelForPartAndFueltype(source, FuelType);

        bool deactivated = false;

        if (source is IFuelSource) {
            IFuelSource ifs = (IFuelSource)source;
            if (ifs.fuelType == FuelType && ifs.RequestFuel(amount, FuelType)) {
                addFuel(dest, amount, FuelType);
                /*
                if (fuelBefore - amount != getFuelForPartAndFueltype(source, FuelType)) {
                    addFuel(source, fuelBefore - amount, FuelType);
                }
                */
            }
            if (ifs.fuel <= 0.0) {
                source.deactivate();
                deactivated = true;
            }
        } else {
            if (FuelType == RegularFuel && source.GetType() == typeof(FuelTank)) {
                FuelTank ft = (FuelTank)source;
                if (ft.RequestFuel (dest, amount, dest.uid)) {
                    addFuel(dest, amount, FuelType);
                    /*
                    if (fuelBefore - amount != getFuelForPartAndFueltype(source, FuelType)) {
                        addFuel(source, fuelBefore - amount, FuelType);
                    }
                    */
                } else {
                    ft.fuel = fuelBefore;
                }
                if (ft.fuel <= 0.0) {
                    ft.deactivate();
                    deactivated = true;
                }
            } else if (FuelType == RCSFuel && source.GetType() == typeof(RCSFuelTank)) {
                RCSFuelTank ft = (RCSFuelTank)source;
                ft.fuel -= amount;
                addFuel(dest, amount, FuelType);
                /*
                if (fuelBefore - amount != getFuelForPartAndFueltype(source, FuelType)) {
                    addFuel(source, fuelBefore - amount, FuelType);
                }
                */
                if (ft.fuel <= 0.0) {
                    ft.deactivate();
                    deactivated = true;
                }
            }
        }

        if (wasDeactive)
            source.deactivate();
        return deactivated;
    }

    double distanceBetweenVessels (Vessel a, Vessel b)
    {
        double distance = (Math.Round(Math.Sqrt(Math.Pow(Math.Abs(a.transform.position.x - b.transform.position.x), 2)
                                                             + Math.Pow(Math.Abs(a.transform.position.y - b.transform.position.y), 2)
                                                             + Math.Pow(Math.Abs(a.transform.position.z - b.transform.position.z), 2)), 2));
        return distance;
    }

    void WindowGUI(int windowID)
    {
        Color savedColor = GUI.color;
        #region Print Header & System Status
        GUILayout.Label("Welcome to the Kerlox Fueling System.");
        GUILayout.BeginHorizontal();
        GUILayout.Label("System Status: ");
        if (m_system_online)
        {
            GUI.color = Color.green;
            GUILayout.Label("Online");
        }
        else
        {
            GUI.color = Color.red;
            GUILayout.Label("Offline");
        }
        GUI.color = savedColor;
        GUILayout.EndHorizontal();
        #endregion
        if (m_system_online)
        {
            GUILayout.BeginHorizontal();
            #region Fuel Type Selection
            GUILayout.Label ("Select Fuel Type:");
            String[] fuelTypeStrings = fts.ToArray();
            int foo;
            if ((foo = GUILayout.SelectionGrid(m_fuel_type, fuelTypeStrings, fuelTypeStrings.GetLength(0), GUI.skin.button)) != m_fuel_type)
            {
                m_dest_tank = null;
                m_dest_vessel = null;
                m_source_tank = null;
                m_source_vessel = null;
            }
            m_fuel_type = foo;
            GUILayout.EndHorizontal();
            #endregion
            GUILayout.BeginHorizontal();
                #region Source Column
                GUILayout.BeginVertical("box");
                    GUILayout.Label("Source");
                    #region Source vessel
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Vessel: ");

                    if (m_source_vessel != null)
                    {
                        GUI.color = (FlightGlobals.ActiveVessel == m_source_vessel) ? Color.magenta : Color.green;
                        GUILayout.Label(m_source_vessel.vesselName);
                    }
                    else
                    {
                        GUILayout.Label("None Selected");
                    }
                    GUILayout.EndHorizontal();
                    GUI.color = savedColor;
                    #endregion
                    #region Source Tank
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Tank: ");
                    if (m_source_tank != null)
                    {
                        GUI.color = Color.green;
                        float f = getFuelForPartAndFueltype(m_source_tank, m_fuel_type); // returns -1 on error
                        if (f != -1)
                            GUILayout.Label(f.ToString() + "L");
                        else
                            GUILayout.Label("Invalid Tank Selected");
                    }
                    else
                    {
                        GUILayout.Label("None Selected");
                    }
                    GUILayout.EndHorizontal();
                    GUI.color = savedColor;
                    
                    #endregion
                    #region Source Vessel List
                m_source_vessel_scroll = GUILayout.BeginScrollView(m_source_vessel_scroll);

                // Draw all the vessels in the list vessels scroll view
                foreach (Vessel v in FlightGlobals.Vessels)
                {
                    if (!v.vesselName.ToLower().Contains("debris") && v.isCommandable && v != null)
                    {   // We want to make sure that this vessel is not debris and is a legitimate command pod

                        // This calculate the distance from the current vessel (v) to ourselves
                        //      Rounded to 2 decimal places
                        double distance = distanceBetweenVessels (v, m_part.vessel);

                        // If the distance is less than 2,000m we can now scan for fuel tanks
                        //      TODO: We will want a stage closer than this that will be the ACTUAL refueling range
                        if (distance < 2000d && is_refuel_target(v, m_fuel_type))
                        {
                            GUILayout.BeginHorizontal();
                            // If user clicks the select button make this vessel the active target
                            if (GUILayout.Button("+", new GUIStyle(GUI.skin.button)))
                            {
                                m_source_vessel = v;
                                m_source_tank = null;
                            }

                            // If this vessel is the Active Vessel, lets change the color and not include the distance
                            if (FlightGlobals.ActiveVessel == v)
                            {
                                GUI.color = Color.magenta;
                                GUILayout.Label(v.vesselName);
                            }
                            else
                            {   // Standard green output for scannable vessels
                                GUI.color = Color.green;
                                GUILayout.Label(String.Format(v.vesselName + " - " + string.Format("{0:#,###0}", distance) + "m"));
                            }
                            GUI.color = savedColor;
                            GUILayout.FlexibleSpace();
                            GUILayout.EndHorizontal();
                        }
                        else if (distance <= PROXIMITY_DISTANCE)
                        {   // This is for a "medium" contact range. Nearby Vessels. Set large for testing
                            GUI.color = Color.yellow;
                            GUILayout.Label(String.Format(v.vesselName + " - " + string.Format("{0:#,###0}", distance) + "m"));
                            GUI.color = savedColor;
                        }
                    /*
                        else
                        {   // All other vessels. Greater than PROXIMITY_DISTANCE
                            GUILayout.Label(String.Format(v.vesselName + " - " + string.Format("{0:#,###0}", distance) + "m"));
                        }
                    */
                    }
                }

                GUI.color = savedColor;

                GUILayout.EndScrollView();
                #endregion
                    #region Source Tank List
                    GUILayout.Label("Source Tank");
                    m_source_tanks_scroll = GUILayout.BeginScrollView(m_source_tanks_scroll);

                    m_source_tanks = new List<Part>();
                    if (m_source_vessel != null)
                    {
                        foreach (Part p in m_source_vessel.parts)
                        {
                            if (p.State == PartStates.ACTIVE || p.State == PartStates.IDLE) {
                                float f = getFuelForPartAndFueltype(p, m_fuel_type);
                                if (f > 0.0)
                                    m_source_tanks.Add(p);
                            }
                        }
                    }
                    foreach (Part p in m_source_tanks)
                    {
                        if (m_dest_tank == null || p.UID != m_dest_tank.UID) //See the equivalent dest_tanks code for comments
                        {
                            GUILayout.BeginHorizontal();
                            if (GUILayout.Button(new GUIContent("+", p.UID), new GUIStyle(GUI.skin.button)))
                            {
                                m_source_tank = p;
                            }
                            if (GUI.tooltip == p.UID)
                            {
                                p.highlight(Color.green);
                            }
                            else
                            {
                                p.highlight(Color.black);
                            }
                            float f = getFuelForPartAndFueltype(p, m_fuel_type);
                            GUILayout.Label(Math.Round(f, 1).ToString() + "L");
                            GUILayout.FlexibleSpace();
                            GUILayout.EndHorizontal();
                        }
                    }

                    GUI.color = savedColor;
                    GUILayout.EndScrollView();
                    #endregion
                    GUILayout.FlexibleSpace();
                GUILayout.EndVertical();
                #endregion
                #region Destination Column
                GUILayout.BeginVertical("box");
                    GUILayout.Label("Destination");
                    #region Dest Vessel
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Vessel: ");

                    if (m_dest_vessel != null)
                    {
                        GUI.color = (FlightGlobals.ActiveVessel == m_dest_vessel) ? Color.magenta : Color.green;
                        GUILayout.Label(m_dest_vessel.vesselName);
                    }
                    else
                    {
                        GUILayout.Label("None Selected");
                    }
                    GUILayout.EndHorizontal();
                    GUI.color = savedColor;
                    #endregion
                    #region Dest Tank
                GUILayout.BeginHorizontal();
                GUILayout.Label("Tank: ");

                if (m_dest_tank != null)
                {
                    GUI.color = Color.green;
                    float f = getFuelForPartAndFueltype(m_dest_tank, m_fuel_type);
                    if (f != -1)
                        GUILayout.Label(f.ToString() + "L");
                    else
                        GUILayout.Label("Invalid Tank Selected");
                }
                else
                {
                    GUILayout.Label("None Selected");
                }
                GUILayout.EndHorizontal();
                GUI.color = savedColor;
                #endregion
                    #region Dest Vessel List
                    m_dest_vessel_scroll = GUILayout.BeginScrollView(m_dest_vessel_scroll);

                    // Draw all the vessels in the list vessels scroll view
                    foreach (Vessel v in FlightGlobals.Vessels)
                    {
                        if (!v.vesselName.ToLower().Contains("debris") && v.isCommandable && v != null)
                        { /* Don't care about whether or not it's debris, or even if it has a command pod.
                             Only care if it has a fuel tank on it.
                            */
                            // This calculate the distance from the current vessel (v) to ourselves
                            //      Rounded to 2 decimal places
                            double distance = distanceBetweenVessels(v, m_part.vessel);

                            // If the distance is less than 2,000m we can now scan for fuel tanks
                            // TODO: We will want a stage closer than this that will be the ACTUAL refueling range
                            if (distance < 2000d && is_refuel_target(v, m_fuel_type))
                            {
                                GUILayout.BeginHorizontal();
                                // If user clicks the select button make this vessel the active target
                                if (GUILayout.Button("+", new GUIStyle(GUI.skin.button)))
                                {
                                    m_dest_vessel = v;
                                    m_dest_tank = null;
                                }

                                // If this vessel is the Active Vessel, lets change the color and not include the distance
                                if (FlightGlobals.ActiveVessel == v)
                                {
                                    GUI.color = Color.magenta;
                                    GUILayout.Label(v.vesselName);
                                }
                                else
                                {   // Standard green output for scannable vessels
                                    GUI.color = Color.green;
                                    GUILayout.Label(String.Format(v.vesselName + " - " + string.Format("{0:#,###0}", distance) + "m"));
                                }
                                GUI.color = savedColor;
                                GUILayout.FlexibleSpace();
                                GUILayout.EndHorizontal();
                            }
                            else if (distance <= PROXIMITY_DISTANCE)
                            {   // This is for a "medium" contact range. Nearby Vessels. Set large for testing
                                GUI.color = Color.yellow;
                                GUILayout.Label(String.Format(v.vesselName + " - " + string.Format("{0:#,###0}", distance) + "m"));
                                GUI.color = savedColor;
                            }
                    /*
                            else
                            {   // All other vessels. Greater than PROXIMITY_DISTANCE
                                GUILayout.Label(String.Format(v.vesselName + " - " + string.Format("{0:#,###0}", distance) + "m"));
                            }
                    */
                        }
                    }

                    GUI.color = savedColor;

                    GUILayout.EndScrollView();
                    #endregion
                    #region Dest Tank List
                    GUILayout.Label("Dest Tank");
                    m_dest_tanks_scroll = GUILayout.BeginScrollView(m_dest_tanks_scroll);

                    m_dest_tanks = new List<Part>();
                    if (m_dest_vessel != null)
                    {
                        foreach (Part p in m_dest_vessel.parts)
                        {
                            if (p.State == PartStates.DEAD)
                                continue;
                            float f = getFuelForPartAndFueltype(p, m_fuel_type);
                            if (f != -1)
                                m_dest_tanks.Add(p);
                        }
                    }
                    foreach (Part p in m_dest_tanks)
                    {
                        //This prevents the currently selected source tank from showing up in the dest list
                        if (m_source_tank == null || m_source_tank.UID != p.UID) //If the source tank is null or the source tank's Unique ID isn't equal to the current potential dest tank, draw the button and info
                        {
                            GUILayout.BeginHorizontal();
                            if (GUILayout.Button(new GUIContent("+", p.UID), new GUIStyle(GUI.skin.button))) //Add a tooltip to the button, the tooltip won't show up but it allows us to identify what button the mouse is over
                            {
                                m_dest_tank = p;
                                
                                foreach (AvailablePart editorPart in PartLoader.fetch.loadedPartsList) //Linear search through the loaded parts for the current part's name
                                {
                                    if (editorPart.name == p.partInfo.name)
                                    {
                                        m_dest_tank_editor = editorPart.partPrefab;
                                        break;
                                    }
                                }
                            }

                            if (GUI.tooltip == p.UID) //Check the current tooltip to see if it matches the current part's unique ID
                            {
                                p.highlight(Color.green); //Hightlight the part if so
                            }
                            else
                            {
                                p.highlight(Color.black); //Remove highlighting if not
                            }

                            float f = getFuelForPartAndFueltype(p, m_fuel_type);
                            if (f != -1)
                                GUILayout.Label(Math.Round(f, 1).ToString() + "L");
                            else
                                GUILayout.Label("Invalid Tank Selected");
                            GUILayout.FlexibleSpace();
                            GUILayout.EndHorizontal();
                        }
                    }
                    GUI.color = savedColor;
                    GUILayout.EndScrollView();
                    #endregion
                    GUILayout.FlexibleSpace();
                GUILayout.EndVertical();
                #endregion
            GUILayout.EndHorizontal();
            #region Transfer Parameters
            GUILayout.Label("Transfer Parameters");
            #region Transfer Values
            GUILayout.Label("Amount: " + Math.Round(m_transfer_amount, 2).ToString() + " (" + Math.Round(m_transfer_amount_percent, 2) + "%)");

            float maxTransfer = 0; //Default max amount of fuel to transfer to 0 (in case of null dest_tank)
            if (m_dest_tank != null)
            {
                float f1 = getFuelForPartAndFueltype(m_dest_tank_editor, m_fuel_type);
                float f2 = getFuelForPartAndFueltype(m_dest_tank, m_fuel_type);
                if (f1 != -1 && f2 != -2)
                    maxTransfer = f1 - f2; //Calculate the maximum amount the destination can accept (full-current)
            }

            if (m_transfer_amount > maxTransfer) m_transfer_amount = maxTransfer; //Limit the transfer amount

            float maxTransferPercent = 100; //Default max transfer percentage to 100% in case of null source_tank

            if (m_source_tank != null && maxTransfer > 0)
            {
                float f = getFuelForPartAndFueltype(m_source_tank, m_fuel_type);
                if (f != -1)
                    maxTransferPercent = (maxTransfer / f) * 100f;

                if (maxTransferPercent > 100f) maxTransferPercent = 100f; //In the case where the source has less fuel than the maximum transfer amount, limit the percentage to 100%
            }
            else if (maxTransfer == 0) maxTransferPercent = 0; //In the case of a null dest_tank limit change the max transfer percent to 0

            if (m_transfer_amount_percent > maxTransferPercent) m_transfer_amount_percent = maxTransferPercent; //limit the transfer percent to the max transfer percent
            

            m_transfer_amount_percent = GUILayout.HorizontalSlider(m_transfer_amount_percent, 0, 100);
            if (m_source_tank != null)
            {
                float f = getFuelForPartAndFueltype(m_source_tank, m_fuel_type);
                if (f != -1)
                    m_transfer_amount = f * (m_transfer_amount_percent / 100);
                else
                    m_transfer_amount = 0;
            }
            else
                m_transfer_amount = 0;

            GUILayout.Label("Flow Rate: ");
            #endregion
            if (GUILayout.Button("Transfer Now", new GUIStyle(GUI.skin.button)))
            {
                if (transferFuel(m_source_tank, m_dest_tank, m_transfer_amount, m_fuel_type)) // returns whether or not the source tank was deactivated.
                    m_source_tank = null;
            }
            #endregion
        }

        m_system_online = GUILayout.Toggle(m_system_online, "System Power", new GUIStyle(GUI.skin.button));

        GUI.DragWindow();
    }

    void drawGUI()
    {
        // Without this, when two FuelTransferPods were within 2km of each other,
        //      the plugin will crash as it will not know for which vessel to draw the menu
        if (FlightGlobals.ActiveVessel != m_part.vessel)
            return;

        GUI.skin = HighLogic.Skin;

        if (m_parameters.windowed)
        {
			List<String> fts = otherFuelParts();
			fts.Insert(0, "RCS Fuel");
			fts.Insert(0, "Regular Fuel");
            
            float activeHeight = 550;
            float heightMultiplier = 25;
            float tanksCount;
            if (m_source_tanks.Count > m_dest_tanks.Count)
                tanksCount = m_source_tanks.Count;
            else
                tanksCount = m_dest_tanks.Count;
            activeHeight += tanksCount * heightMultiplier;
            if (tanksCount > 6)
                activeHeight = 700;
            m_window_pos = GUILayout.Window(WINDOW_ID, m_window_pos, WindowGUI, "Fuel Transfer System", GUILayout.Width(((m_system_online) ? 650 : 250)), GUILayout.Height(((m_system_online) ? activeHeight : 100)));
        }
    }

    public void drive(FlightCtrlState s)
    {
    }

    public void onPartFixedUpdate()
    {
        if (FlightGlobals.ActiveVessel != m_part.vessel)
            return;
    }

    public void onFlightStart()
    {
        m_window_pos = new Rect(m_parameters.displayX, m_parameters.displayY, 10, 10);
        RenderingManager.AddToPostDrawQueue(3, new Callback(drawGUI));
    }

    public void onPartDestroy()
    {
        RenderingManager.RemoveFromPostDrawQueue(3, new Callback(drawGUI)); //close the GUI
    }

    public void onDisconnect()
    {
        RenderingManager.RemoveFromPostDrawQueue(3, new Callback(drawGUI)); //close the GUI
    }

    void print(String s)
    {
        MonoBehaviour.print(s);
    }
}
