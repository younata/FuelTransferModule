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

public class FuelTransferCore
{
    #region Member Data
    const int WINDOW_ID = 93323;
    const double PROXIMITY_DISTANCE = 50000d; // 50KM
    Rect m_window_pos = new Rect(200, 50, 10, 10);
    FuelTransferParameters m_parameters;

    // The Part
    Part m_part;

    Vector2 m_vessel_scroll = new Vector2();
    //public RefuelTargets m_refuel_targets;


    Vector2 m_source_tanks_scroll = new Vector2();
    public List<Part> m_source_tanks = new List<Part>();

    public List<Part> m_dest_tanks = new List<Part>();

    // UI Toggles
    bool m_system_online = false;

    // making the above 4 bools obsolete
    int m_selected_action = -1;

    string m_transfer_amount_str = "0";
    float m_transfer_amount = 0;

    Part m_selected_source_tank = null;
    Vessel m_selected_target = null;
    Part m_selected_dest_tank = null;
    #endregion

    public FuelTransferCore(Part part)
    {
        this.m_part = part;
    }

    public void applyParameters(FuelTransferParameters parameters)
    {
        this.m_parameters = parameters;
    }

    //decide whether a vessel has fueltanks!
    bool is_refuel_target (Vessel v)
	{
		foreach (Part p in v.parts) {
			if (p.GetType() == typeof(FuelTank))
				return true;
		}
		return false;
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
        #region Tank Selections
        if (m_system_online)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Source Tank: ");

            if (m_selected_source_tank != null)
            {
                GUI.color = Color.green;
                GUILayout.Label(m_selected_source_tank.name + " - " + ((FuelTank)m_selected_source_tank).fuel.ToString() + "L");
            }
            else
            {
                GUILayout.Label("None Selected");
            }
            GUILayout.EndHorizontal();
            GUI.color = savedColor;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Target Vessel: ");

            if (m_selected_target != null)
            {
                GUI.color = Color.green;
                GUILayout.Label(m_selected_target.vesselName);
            }
            else
            {
                GUILayout.Label("None Selected");
            }
            GUILayout.EndHorizontal();
            GUI.color = savedColor;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Destination Tank: ");

            if (m_selected_dest_tank != null)
            {
                GUI.color = Color.green;
                GUILayout.Label(m_selected_dest_tank.name + " - " + ((FuelTank)m_selected_dest_tank).fuel.ToString() + "L");
            }
            else
            {
                GUILayout.Label("None Selected");
            }
            GUILayout.EndHorizontal();
            GUI.color = savedColor;
        }
        #endregion
        #region Buttons
        GUILayout.BeginHorizontal();

        m_system_online = GUILayout.Toggle(m_system_online, "System Power", new GUIStyle(GUI.skin.button));
        if (m_system_online)
        {
            String[] options = {"List Vessels", "Select Source Tank", "Select Dest Tank", "Transfer"};
            m_selected_action = GUILayout.SelectionGrid(m_selected_action, options, 4, GUI.skin.button);
        }
        GUILayout.EndHorizontal();
        #endregion
        #region List Vessels Scroll Window
        if (m_selected_action == 0 && m_system_online)
        {
            m_vessel_scroll = GUILayout.BeginScrollView(m_vessel_scroll);

            // Draw all the vessels in the list vessels scroll view
            foreach (Vessel v in FlightGlobals.Vessels)
            {
                if (!v.vesselName.ToLower().Contains("debris") && v.isCommandable && v != null)
                {   // We want to make sure that this vessel is not debris and is a legitimate command pod

                    // This calculate the distance from the current vessel (v) to ourselves
                    //      Rounded to 2 decimal places
                    double distance = (Math.Round(Math.Sqrt(Math.Pow(Math.Abs(v.transform.position.x - m_part.vessel.transform.position.x), 2)
                                                         + Math.Pow(Math.Abs(v.transform.position.y - m_part.vessel.transform.position.y), 2)
                                                         + Math.Pow(Math.Abs(v.transform.position.z - m_part.vessel.transform.position.z), 2)), 2));

                    // If the distance is less than 2,000m we can now scan for fuel tanks
                    //      TODO: We will want a stage closer than this that will be the ACTUAL refueling range
                    if (distance < 2000d && is_refuel_target(v))
                    {
                        //GUILayout.Label(v.vesselName + " - is_refuel_target: " + is_refuel_target(v));
                        GUILayout.BeginHorizontal();
                        // If user clicks the select button make this vessel the active target
                        if (GUILayout.Button("Select", new GUIStyle(GUI.skin.button)))
                        {
                            m_selected_target = v;
                        }

                        // If this vessel is the Active Vessel, lets change the color and not include the distance
                        if (FlightGlobals.ActiveVessel == v)
                        {
                            GUI.color = Color.magenta;
                            GUILayout.Label(v.vesselName + " (Self)");
                        }
                        else
                        {   // Standard green output for scannable vessels
                            GUI.color = Color.green;
                            GUILayout.Label(String.Format(v.vesselName + " - " + string.Format("{0:#,###0}", distance) + "m"));
                        }
                        GUI.color = savedColor;
                        GUILayout.EndHorizontal();
                    }
                    else if (distance <= PROXIMITY_DISTANCE)
                    {   // This is for a "medium" contact range. Nearby Vessels. Set large for testing
                        GUI.color = Color.yellow;
                        GUILayout.Label(String.Format(v.vesselName + " - " + string.Format("{0:#,###0}", distance) + "m"));
                        GUI.color = savedColor;
                    }
                    else
                    {   // All other vessels. Greater than PROXIMITY_DISTANCE
                        GUILayout.Label(String.Format(v.vesselName + " - " + string.Format("{0:#,###0}", distance) + "m"));
                    }
                }
            }
               
            GUI.color = savedColor;

            GUILayout.EndScrollView();
        }
        #endregion
        #region Select Source Tank
        else if (m_selected_action == 1 && m_system_online)
        {
            m_source_tanks_scroll = GUILayout.BeginScrollView(m_source_tanks_scroll);

            m_source_tanks = new List<Part>();
            foreach (Part p in m_part.vessel.parts)
            {
                if (p.GetType() == typeof(FuelTank) && ((FuelTank)p).fuel > 0.0)
                    m_source_tanks.Add(p);
            }
            
            foreach (Part p in m_source_tanks)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Select", new GUIStyle(GUI.skin.button)))
                {
                    m_selected_source_tank = p;
                }
                GUILayout.Label("Tank: " + p.name + " - " + Math.Round(((FuelTank)p).fuel, 1).ToString() +"L");
                GUILayout.EndHorizontal();
            }

            GUI.color = savedColor;
            GUILayout.EndScrollView();
        }
        #endregion
        #region Select Dest Tank
        else if (m_selected_action == 2 && m_system_online)
        {
            m_vessel_scroll = GUILayout.BeginScrollView(m_vessel_scroll);

            m_dest_tanks = new List<Part>();
            foreach (Part p in m_part.vessel.parts)
            {
                if (p.GetType() == typeof(FuelTank))
                    m_dest_tanks.Add(p);
            }

            foreach (Part p in m_dest_tanks)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Select", new GUIStyle(GUI.skin.button)))
                {
                    m_selected_dest_tank = p;
                }
                GUILayout.Label("Tank: " + p.name + " - " + Math.Round(((FuelTank)p).fuel, 1).ToString() + "L");
                GUILayout.EndHorizontal();
            }

            GUI.color = savedColor;

            GUILayout.EndScrollView();
        }
        #endregion
        #region Transfer Window
        else if (m_selected_action == 3 && m_system_online)
        {
            m_transfer_amount_str = GUILayout.TextField(m_transfer_amount_str, new GUIStyle(GUI.skin.textField));
            try
            {
                m_transfer_amount = (float)Convert.ToDouble(m_transfer_amount_str);
				if (m_transfer_amount > ((FuelTank)m_selected_source_tank).fuel)
				    m_transfer_amount = ((FuelTank)m_selected_source_tank).fuel;
            }
            catch
            {
                m_transfer_amount = 0;
            }
            if (GUILayout.Button("Transfer Now", new GUIStyle(GUI.skin.button)))
            {
                ((FuelTank)m_selected_source_tank).fuel -= m_transfer_amount;
                ((FuelTank)m_selected_dest_tank).fuel += m_transfer_amount;
            }
            

        }
        #endregion

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
            m_window_pos = GUILayout.Window(WINDOW_ID, m_window_pos, WindowGUI, "Fuel Transfer System", GUILayout.Width(350), GUILayout.Height(((m_selected_action >= 0) ? 400 : 100)));
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
