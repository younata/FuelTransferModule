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
        print("FuelTransfer Log Test");

        foreach (Vessel v in FlightGlobals.Vessels)
        {
            print("Vessel: " + v.vesselName);
        }
    }


}

public class FuelTransferCore
{
    #region Member Data
    const int WINDOW_ID = 93318;
    Rect m_window_pos = new Rect(125, 0, 10, 10);
    FuelTransferParameters m_parameters;

    // The Part
    Part m_part;

    Vector2 m_vessel_scroll = new Vector2();
    public RefuelTargets m_refuel_targets;


    Vector2 m_source_tanks_scroll = new Vector2();
    public List<Part> m_source_tanks = new List<Part>();

    public List<Part> m_dest_tanks = new List<Part>();

    int m_ticks_since_target_check = 0;

    // UI Toggles
    bool m_system_online = false;
    bool m_list_vessels = false;
    bool m_select_source_tank = false;
    bool m_select_dest_tank = false;

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
            String[] options = {"Select Source Tank", "List Vessels", "Select Dest Tank", "Transfer"};
            m_selected_action = GUILayout.SelectionGrid(m_selected_action, options, 4, GUI.skin.button);
        }
        GUILayout.EndHorizontal();
        #endregion
        #region List Vessels Scroll Window
        if (m_selected_action == 0 && m_system_online)
        {
            m_vessel_scroll = GUILayout.BeginScrollView(m_vessel_scroll);

            List<Vessel> refuel_targets = new List<Vessel>();
            if (m_refuel_targets != null)
            {
                foreach (RefuelTarget target in m_refuel_targets.targets)
                {
                    double distance = (Math.Round(Math.Sqrt(Math.Pow(Math.Abs(target.Position.x - m_part.vessel.transform.position.x), 2)
                                                                                 + Math.Pow(Math.Abs(target.Position.y - m_part.vessel.transform.position.y), 2)
                                                                                 + Math.Pow(Math.Abs(target.Position.z - m_part.vessel.transform.position.z), 2)), 2));
                    if ((target.Vessel != null) && is_refuel_target(target.Vessel) && distance < 100000) 
                         refuel_targets.Add(target.Vessel);
                }
            }

            savedColor = GUI.color;
            //list the targets
            foreach (Vessel v in refuel_targets)
            {
                double distance = (Math.Round(Math.Sqrt(Math.Pow(Math.Abs(v.transform.position.x - m_part.vessel.transform.position.x), 2)
                                                                                                 + Math.Pow(Math.Abs(v.transform.position.y - m_part.vessel.transform.position.y), 2)
                                                                                                 + Math.Pow(Math.Abs(v.transform.position.z - m_part.vessel.transform.position.z), 2)), 2));
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Select", new GUIStyle(GUI.skin.button)))
                {
                    m_selected_target = v;
                }
                GUI.color = Color.green;
                GUILayout.Label(String.Format("FT: " + v.vesselName + " - " + string.Format("{0:#,###0}", distance) + "km"));
                GUI.color = savedColor;
                GUILayout.EndHorizontal();
                //GUILayout.Label(String.Format("Target: " + v.vesselName + ": {0:0} km above " + v.mainBody.name, ((v.transform.position - v.mainBody.position).magnitude - v.mainBody.Radius) / 1000.0));
            }
            
            GUI.color = savedColor;

            //list the non targets
            foreach (Vessel v in FlightGlobals.Vessels)
            {
                if (!refuel_targets.Contains(v) && !v.vesselName.ToLower().Contains("debris"))
                {
                    //GUILayout.Label(String.Format(v.vesselName + ": {0:0} km above " + v.mainBody.name, ((v.transform.position - v.mainBody.position).magnitude - v.mainBody.Radius) / 1000.0));

                    double distance = (Math.Round(Math.Sqrt(Math.Pow(Math.Abs(v.transform.position.x - m_part.vessel.transform.position.x), 2)
                                                                                                     + Math.Pow(Math.Abs(v.transform.position.y - m_part.vessel.transform.position.y), 2)
                                                                                                     + Math.Pow(Math.Abs(v.transform.position.z - m_part.vessel.transform.position.z), 2)), 2));
                    GUILayout.Label(String.Format(v.vesselName + " - " + string.Format("{0:#,###0}", distance) + "km"));
                }
            }
        

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
            m_transfer_amount_str =GUILayout.TextField(m_transfer_amount_str, new GUIStyle(GUI.skin.textField));
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

        
        if (m_ticks_since_target_check++ > 100)
        {
            m_ticks_since_target_check = 0;

            List<Vessel> targets = new List<Vessel>();

            // Loop through vessels and find possible refueling targets
            foreach (Vessel v in FlightGlobals.Vessels)
                if (is_refuel_target(v))
                    targets.Add(v);
        }
    }

    public void onFlightStart()
    {
        print("--------ARRemoteCore m_parameters--------");
        print("ARRemoteCore: basePlanet = " + m_parameters.basePlanet);
        print("ARRemoteCore: baseLatitude = " + m_parameters.baseLatitude);
        print("ARRemoteCore: baseLongitude = " + m_parameters.baseLongitude);
        print("ARRemoteCore: windowed = " + m_parameters.windowed);
        print("ARRemoteCore: displayX = " + m_parameters.displayX);
        print("ARRemoteCore: displayY = " + m_parameters.displayY);

        m_window_pos = new Rect(m_parameters.displayX, m_parameters.displayY, 10, 10);

        FlightInputHandler.OnFlyByWire += new FlightInputHandler.FlightInputCallback(this.drive);
        
        RenderingManager.AddToPostDrawQueue(3, new Callback(drawGUI));

    }

    public void onPartDestroy()
    {

        FlightInputHandler.OnFlyByWire -= new FlightInputHandler.FlightInputCallback(this.drive);
        RenderingManager.RemoveFromPostDrawQueue(3, new Callback(drawGUI)); //close the GUI
    }

    public void onDisconnect()
    {
        FlightInputHandler.OnFlyByWire -= new FlightInputHandler.FlightInputCallback(this.drive);
        RenderingManager.RemoveFromPostDrawQueue(3, new Callback(drawGUI)); //close the GUI
    }

    void print(String s)
    {
        MonoBehaviour.print(s);
    }
}


public class RefuelTarget : IEquatable<RefuelTarget>
{
    Vessel vessel;
    Vector3d position;

    public RefuelTarget(Vessel v)
    {
        this.vessel = v;
        this.position = v.transform.position;
    }

    public RefuelTarget(Vector3d pos)
    {
        this.vessel = null;
        this.position = pos;
    }

    public Vessel Vessel
    {
        get
        {
            return this.vessel;
        }
    }

    public Vector3d Position
    {
        get
        {
            if (this.vessel != null) return this.vessel.transform.position;
            else return this.position;
        }
    }

    public bool IsBase
    {
        get
        {
            return (this.vessel == null);
        }
    }

    public bool Equals(RefuelTarget other)
    {
        return (this.Vessel == other.Vessel && this.Position == other.Position);
    }

    public override String ToString()
    {
        return ((this.vessel == null) ? "Mission Control" : this.vessel.vesselName);
    }
}

public class RefuelTargets
{
    public List<RefuelTarget> targets = new List<RefuelTarget>();

    public RefuelTargets(List<RefuelTarget> nodes)
    {
        this.targets = nodes;
    }

    public double Length
    {
        get
        {
            double length = 0;
            for (int i = 1; i < targets.Count; i++)
            {
                length += (targets[i].Position - targets[i - 1].Position).magnitude;
            }
            return length;
        }
    }

    public override String ToString()
    {
        String ret;
        if (targets.Count > 0) ret = targets[targets.Count - 1].ToString();
        else ret = "empty path???????";

        for (int i = targets.Count - 2; i >= 0; i--)
        {
            ret += " → " + targets[i].ToString();
        }
        return ret;
    }
}
