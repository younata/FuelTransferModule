using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;


public struct FuelTransferParameters
{
    public double speedOfLight; //default speed of light 30,000 km/s; (one tenth real speed)
    public String comsatString;
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
    public double speedOfLight = 30000000.0; //default speed of light 30,000 km/s; (one tenth real speed)
    public String comsatString = "comsat";
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

    //make the vessel crewless
    protected override void onPartAwake()
    {
        //make the vessel crewless
        /*
        GameObject go = GameObject.Find("internalSpace");
        if (go != null)
        {
            Transform t = go.transform;
            if (t != null)
            {
                Transform child = t.FindChild("mk1pod_internal");
                if (child != null)
                {
                    InternalModel im = child.GetComponent<InternalModel>();
                    if (im != null)
                    {
                        im.seats = new InternalSeat
                    }
                }
            }
        }
        */
        base.onPartAwake();
    }

    protected override void onPartFixedUpdate()
    {
        core.onPartFixedUpdate();
        base.onPartFixedUpdate();
    }


    protected override void onFlightStart()
    {
        parameters.speedOfLight = speedOfLight; //default speed of light 30,000 km/s; (one tenth real speed)
        parameters.comsatString = comsatString;
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

    private LineRenderer line = null;

    PlanetariumCamera planetariumCamera = null;
    GameObject obj = new GameObject("Line");
    // The Part
    Part m_part;

    Vector2 m_vessel_scroll = new Vector2();
    public RefuelTargets m_refuel_targets;


    Vector2 m_source_tanks_scroll = new Vector2();
    public List<Part> m_source_tanks = new List<Part>();

    Vector2 m_dest_tanks_scroll = new Vector2();
    public List<Part> m_dest_tanks = new List<Part>();

    int m_ticks_since_target_check = 0;

    // UI Toggles
    bool m_system_online = false;
    bool m_list_vessels = false;
    bool m_select_source_tank = false;
    bool m_select_dest_tank = false;
    #endregion

    public FuelTransferCore(Part part)
    {
        this.m_part = part;
    }

    public void applyParameters(FuelTransferParameters parameters)
    {
        this.m_parameters = parameters;
    }

    //decide whether a vessel is a comsat
    bool is_refuel_target(Vessel v)
    {
        /*
        foreach (ProtoPartSnapshot pps in v.protoVessel.protoPartSnapshots)
        {
            if (pps.partStateValues.ContainsKey("SatState"))
            {
                if (pps.partStateValues["SatState"].value_int == 3) return true;
            }
        }
        */

        //return (v.vesselName.ToLower().Contains(parameters.comsatString) && (v.isCommandable || !v.vesselName.ToLower().Contains("debris")));
        return (v.isCommandable && !v.vesselName.ToLower().Contains("debris"));
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
        #region Buttons
        GUILayout.BeginHorizontal();

        m_system_online = GUILayout.Toggle(m_system_online, "System Power", new GUIStyle(GUI.skin.button));
        //if (!newLocalControl && localControl) delayedBuffer = new FlightCtrlStateBuffer();
        //localControl = newLocalControl;
        if (m_system_online)
        {
            m_select_source_tank = GUILayout.Toggle(m_select_source_tank, "Select Source Tank", new GUIStyle(GUI.skin.button));
            m_list_vessels = GUILayout.Toggle(m_list_vessels, "List Vessels", new GUIStyle(GUI.skin.button));
            m_select_dest_tank = GUILayout.Toggle(m_select_dest_tank, "Select Dest Tank", new GUIStyle(GUI.skin.button));
        }
        GUILayout.EndHorizontal();
        #endregion
        #region List Vessels Scroll Window
        if (m_list_vessels)
        {
            m_vessel_scroll = GUILayout.BeginScrollView(m_vessel_scroll);

            //compile a list of comsat vessels that are in the current relay path
            List<Vessel> refuel_targets = new List<Vessel>();
            if (m_refuel_targets != null)
            {
                foreach (RefuelTarget target in m_refuel_targets.targets)
                {
                    if ((target.Vessel != null) && is_refuel_target(target.Vessel)) 
                         refuel_targets.Add(target.Vessel);
                }
            }

            savedColor = GUI.color;
            GUI.color = Color.green;
            
            //list the targets
            foreach (Vessel v in refuel_targets)
            {
                GUILayout.Label(String.Format("Target: " + v.vesselName + ": {0:0} km above " + v.mainBody.name, ((v.transform.position - v.mainBody.position).magnitude - v.mainBody.Radius) / 1000.0));
            }
            
            GUI.color = savedColor;

            //list the non targets
            foreach (Vessel v in FlightGlobals.Vessels)
            {
                if (!refuel_targets.Contains(v) && !v.vesselName.ToLower().Contains("debris"))//isComsat(v) && ))
                {
                    GUILayout.Label(String.Format(v.vesselName + ": {0:0} km above " + v.mainBody.name, ((v.transform.position - v.mainBody.position).magnitude - v.mainBody.Radius) / 1000.0));
                }
            }
        

            GUILayout.EndScrollView();
        }
        #endregion
        #region Select Source Tank
        else if (m_select_source_tank)
        {
            m_source_tanks_scroll = GUILayout.BeginScrollView(m_source_tanks_scroll);

            m_source_tanks = new List<Part>();
            foreach (Part p in m_part.vessel.parts)
            {
                GUILayout.Label("Part: " + p.name);
                if (RequestFuel(p, 0.1f, p.lastFuelRequestId))
                    m_source_tanks.Add(p);
            }
            GUI.color = Color.green;
            foreach (Part p in m_source_tanks)
            {
                GUILayout.Label("Tank: " + p.name);
            }

            GUI.color = savedColor;
            GUILayout.EndScrollView();
        }
        #endregion
        #region Select Dest Tank
        else if (m_select_dest_tank)
        {
            m_vessel_scroll = GUILayout.BeginScrollView(m_vessel_scroll);
            GUILayout.EndScrollView();
        }
        #endregion



        GUI.DragWindow();
    }

    void drawGUI()
    {
        //if (FlightGlobals.ActiveVessel != m_part.vessel || !weAreMainCore()) return;

        GUI.skin = HighLogic.Skin;

        if (m_parameters.windowed)
        {
            m_window_pos = GUILayout.Window(WINDOW_ID, m_window_pos, WindowGUI, "Fuel Transfer System", GUILayout.Width(350), GUILayout.Height(((m_select_source_tank || m_list_vessels || m_select_dest_tank) ? 300 : 50)));
        }
        else
        {
            Color savedColor = GUI.color;
            /*if (m_radio_contact && controlPath != null)
            {
                GUI.color = Color.yellow;
                GUI.Label(new Rect(m_parameters.displayX, m_parameters.displayY, 400, 300), "Relay path: " + controlPath.ToString() + "\n"
                    + String.Format("Path length: {0:0} km, round trip delay: {1:0.00} s", controlPath.Length / 1000.0, controlDelay));
            }
            else
            {*/
                GUI.color = Color.red;
                GUI.Label(new Rect(m_parameters.displayX, m_parameters.displayY, 300, 40), "Out of radio contact!");
            //}
            GUI.color = savedColor;
        }
    }

    public void drive(FlightCtrlState s)
    {
        //if (FlightGlobals.ActiveVessel != m_part.vessel || !weAreMainCore()) return;
        /*
        if (!localControl)
        {
            if (!m_radio_contact)
            {
                //lock out the player if we are out of radio contact
                FlightInputHandler.SetNeutralControls();
            }
            else
            {
                //usually the game feeds back whatever value of killrot we gave it last frame. If this is not true, then the 
                //user toggled SAS.
                if (s.killRot != lastKillRot)
                {
                    myKillRot = !myKillRot;
                }
                s.killRot = myKillRot;
                delayedBuffer.push(s, Planetarium.GetUniversalTime());
                delayedBuffer.pop(s, Planetarium.GetUniversalTime() - controlDelay);
                lastKillRot = s.killRot;
            }
        }
         */

    }

    public void onPartFixedUpdate()
    {
        if (FlightGlobals.ActiveVessel != m_part.vessel) return;// || !weAreMainCore()) return;

        
        if (m_ticks_since_target_check++ > 100)
        {
            m_ticks_since_target_check = 0;

            List<Vessel> targets = new List<Vessel>();

            // Loop through vessels and find possible refueling targets
            foreach (Vessel v in FlightGlobals.Vessels)
                if (is_refuel_target(v))
                    targets.Add(v);

            //m_refuel_targets =;
            if (m_refuel_targets == null)
            {
                //print("ARRemoteCore: no radio contact!!");
            }
            else
            {
                //controlDelay = 2 * controlPath.Length / m_parameters.speedOfLight;
                //print("ARRemoteCore: radio contact: " + controlPath.ToString());
                //print("ARRemoteCore: signal path length (km) = " + controlPath.Length / 1000.0 + "; round trip time (s) = " + controlDelay);
            }
        }
         
        /*
        if (m_radio_contact && showPathInMapView && MapView.MapIsEnabled)
        {
            line.enabled = true;
            if (controlPath != null)
            {
                line.SetVertexCount(controlPath.targets.Count);
                for (int i = 0; i < controlPath.targets.Count; i++)
                {
                    if (controlPath.targets[i].IsBase)
                    {
                        line.SetPosition(i, computeBaseRelayPosition() * Planetarium.InverseScaleFactor);
                    }
                    else
                    {
                        line.SetPosition(i, controlPath.targets[i].Position * Planetarium.InverseScaleFactor);
                    }
                }

                line.SetWidth((float)(0.01 * planetariumCamera.Distance), (float)(0.01 * planetariumCamera.Distance));
            }
        }
        else
        {
            line.enabled = false;
        }
         */
    }

    public void onFlightStart()
    {
        print("--------ARRemoteCore m_parameters--------");
        print("ARRemoteCore: speedOfLight = " + m_parameters.speedOfLight);
        print("ARRemoteCore: comsatString = " + m_parameters.comsatString);
        print("ARRemoteCore: basePlanet = " + m_parameters.basePlanet);
        print("ARRemoteCore: baseLatitude = " + m_parameters.baseLatitude);
        print("ARRemoteCore: baseLongitude = " + m_parameters.baseLongitude);
        print("ARRemoteCore: windowed = " + m_parameters.windowed);
        print("ARRemoteCore: displayX = " + m_parameters.displayX);
        print("ARRemoteCore: displayY = " + m_parameters.displayY);

        m_window_pos = new Rect(m_parameters.displayX, m_parameters.displayY, 10, 10);

        FlightInputHandler.OnFlyByWire += new FlightInputHandler.FlightInputCallback(this.drive);
        
        RenderingManager.AddToPostDrawQueue(3, new Callback(drawGUI));

        /*
        planetariumCamera = (PlanetariumCamera)GameObject.FindObjectOfType(typeof(PlanetariumCamera));

        obj.layer = 9;

        // Then create renderer itself...
        line = obj.AddComponent<LineRenderer>();
        line.transform.parent = null;
        line.useWorldSpace = true;

        // Make it render a red to yellow triangle, 1 meter wide and 2 meters long
        line.material = new Material(Shader.Find("Particles/Additive"));
        line.SetColors(Color.blue, Color.blue);
        line.SetWidth(1, 1);
        */
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
    //targets through which the signal is relayed
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
