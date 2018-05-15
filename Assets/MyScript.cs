using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RFExplorerCommunicator;
using System;
using System.Threading;


using System.IO.Ports;
using System.Text;

using UnityEngine.UI;

using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

public class MyScript : MonoBehaviour {


    public class MyCell
    {
        public GameObject obj;
        public double fAmplitude;
        public double fFreq;


    };

    public GameObject m_panelIterations;
    public GameObject m_panelKeys;
    public GameObject m_panelSmallKeys;
    public GameObject m_panelCellInfo;
    public GameObject m_panelMarker1;
    public GameObject m_panelMarker2;
    public GameObject m_panelDelta;
    public GameObject m_panelRP;
    public GameObject m_panelDSP;
    bool m_bShowInstructions = true;

    public GameObject m_panelRecord;
    public Button m_btnRecordFile1;
    public Button m_btnRecordFile2;
    public Button m_btnRecordFile3;
    public Button m_btnRecordFile4;
    public Button m_btnRecordFile5;
    public Button m_btnRecordCancel;
    public Text m_txtRecord;
    bool m_bWantPlayback = false;
    double m_fPrevPlaybackElapsed = 0;
    double m_fDeltaPlaybackElapsed = 0;

    GameObject m_objAllSweeps;
    GameObject[] m_objSweep;

    const int MODE_AVG = 0;
    const int MODE_MAX = 1;
    const int MODE_MIN = 2;

    const int DSP_MODE_AUTO = 0;
    const int DSP_MODE_FILTER = 1;
    const int DSP_MODE_FAST = 2;

    public MyCell [] m_cells;
    bool m_bPause = false;
    int m_iNumSweeps = 125;
    int m_iNumDataPoints = 0;
    int m_iSweepCounter = 0;
    int m_iAdvanceCounter = 0;
    int m_iNumStepsToAdvance = 1;
    int m_iCalcMode = MODE_AVG;
    int m_iDSPMode = DSP_MODE_FAST;

    public Material m_matAlternate;

    public Material m_matAmp0;
    public Material m_matAmp1;
    public Material m_matAmp2;
    public Material m_matAmp3;
    public Material m_matAmp4;
    public Material m_matAmp5;
    public Material m_matAmp6;
    public Material m_matAmp7;
    public Material m_matAmp8;
    public Material m_matAmp9;
    public Material m_matAmp10;
    public Material m_matAmp11;
    public Material m_matAmp12;
    public Material m_matAmp13;

    public GameObject m_txtFreqStart;
    public GameObject m_txtFreqEnd;
    public GameObject m_txtM1;
    public GameObject m_txtM2;

    public Text m_txtCellInfo;
    public Text m_txtMarker1;
    public Text m_txtMarker2;
    public Text m_txtM1IsNext;
    public Text m_txtM2IsNext;
    public Text m_txtMarkerDelta;
    public Text m_txtIterations;

    public Text m_txtRP;
    public Text m_txtLblRP;
    public Text m_txtRPActive;

    public Text m_txtDSP;

    public GameObject m_theCube;
    bool m_bInitializedGraphics = false;
    TimeSpan m_tsLastRefresh = new TimeSpan(0);
    GameObject m_prevHitObject = null;
    GameObject m_objMarker1 = null;
    GameObject m_objMarker2 = null;
    int m_iCurMarker = 0;
    bool m_bColumnSelect = true;
    TimeSpan m_ts_from_panel_dismiss = new TimeSpan(0);
    FileStream m_fileRecord = null;
    BinaryWriter m_bwRecorder=null;
    FileStream m_filePlayback = null;
    BinaryReader m_brPlayback = null;
    bool m_bIsRecording = false;
    bool m_bIsPlayback = false;
    int m_iFileVersion = 31;
    double m_fSavedPlaybackRate = 1.0;
    double m_fPlaybackRate = 1.0;
    double m_fMinPlaybackRate = (1.0 / 32.0);
    double m_fMaxPlaybackRate = 1024.0;

    RFECommunicator m_objRFE=null;

    double[] m_fCurrentSweepAmp = null;
    double[] m_fCurrentSweepFreq = null;
    double[] m_fCurrentTimeStamp = null;

    float m_fMinAmplitude = -120.0f;
    int m_iConnectIndex = -1;

    Vector3 m_vecInitialCameraPosition;

    TimeSpan m_ts_last_adj_steps_advance = new TimeSpan(0);
    TimeSpan m_ts_held_key_J = new TimeSpan(0);
    TimeSpan m_ts_held_key_K = new TimeSpan(0);

    public GameObject m_panelConnect;
    public Button m_btnRefreshDevList;
    public Button m_btnConnect;
    public Button m_btnLoadRecording;
    public Button m_btnQuit;
    public Dropdown m_dropDownPort;
    public Text m_txtConnecting;
    public Text m_txtNoDevices;

    public void DoConnect()
    {
        m_txtConnecting.gameObject.SetActive(true);

        m_iConnectIndex = 0;
    }

    public void DoConnect2()
    {
        String strPort;
        int idx;

        idx = m_dropDownPort.value;

        strPort = m_dropDownPort.options[idx].text;

        m_objRFE = new RFECommunicator(true);
        m_objRFE.PortClosedEvent += new EventHandler(OnRFE_PortClosed);
        m_objRFE.ReportInfoAddedEvent += new EventHandler(OnRFE_ReportLog);
        m_objRFE.ReceivedConfigurationDataEvent += new EventHandler(OnRFE_ReceivedConfigData);
        m_objRFE.UpdateDataEvent += new EventHandler(OnRFE_UpdateData);

        m_objRFE.ConnectPort(strPort, 500000);

        if (!m_objRFE.PortConnected)
        {
            return;
        }

        m_panelConnect.SetActive(false);

        m_objRFE.SendCommand_RequestConfigData();

        Thread.Sleep(500);


        // Set calc mode to "average"
        m_objRFE.SendCommand("C+\x02");

        if (DSP_MODE_FAST == m_iDSPMode)
        {
            // Set DSP mode to "fast"
            m_objRFE.SendCommand("Cp\x02");
        }
        else
        {
            // Set DSP mode to "filter"
            m_objRFE.SendCommand("Cp\x01");
        }


        m_iAdvanceCounter = m_iNumStepsToAdvance;

        String sRFEReceivedString;
        m_objRFE.ProcessReceivedString(true, out sRFEReceivedString);
    }

    void InitDeviceList()
    {
        int i;

        m_btnConnect.interactable = false;

        m_dropDownPort.ClearOptions();

        if (!m_objRFE.GetConnectedPorts())
        {
            return;
        }

        for (i = 0; i < m_objRFE.ValidCP2101Ports.Length; i++)
        {
            m_dropDownPort.options.Add(new Dropdown.OptionData() { text = m_objRFE.ValidCP2101Ports[i] });
        }

        if (m_objRFE.ValidCP2101Ports.Length > 0)
        {
            m_txtNoDevices.text = "Select Device Port";
        }

        m_dropDownPort.value = 0;
        m_dropDownPort.RefreshShownValue();

        m_btnConnect.interactable = true;
    }

    void DoRefreshDeviceList()
    {
        InitDeviceList();
    }

    void DoQuit()
    {
        Application.Quit();
    }

    void InitRFE()
    {
        m_objRFE = new RFECommunicator(true);

        m_objRFE.PortClosedEvent += new EventHandler(OnRFE_PortClosed);
        m_objRFE.ReportInfoAddedEvent += new EventHandler(OnRFE_ReportLog);
        m_objRFE.ReceivedConfigurationDataEvent += new EventHandler(OnRFE_ReceivedConfigData);
        m_objRFE.UpdateDataEvent += new EventHandler(OnRFE_UpdateData);

        InitDeviceList();

        m_btnRefreshDevList.onClick.AddListener(DoRefreshDeviceList);
        m_btnConnect.onClick.AddListener(DoConnect);
        m_btnLoadRecording.onClick.AddListener(DoLoadRecording);
        m_btnQuit.onClick.AddListener(DoQuit);

        m_btnRecordFile1.onClick.AddListener(OnRecordFile1);
        m_btnRecordFile2.onClick.AddListener(OnRecordFile2);
        m_btnRecordFile3.onClick.AddListener(OnRecordFile3);
        m_btnRecordFile4.onClick.AddListener(OnRecordFile4);
        m_btnRecordFile5.onClick.AddListener(OnRecordFile5);
        m_btnRecordCancel.onClick.AddListener(OnRecordCancel);

        m_txtConnecting.gameObject.SetActive(false);
    }

    public void UpdateRecordPanel()
    {
        if (!m_bWantPlayback)
        {
            m_txtRecord.text = "Record to File";
            m_btnRecordFile1.interactable = true;
            m_btnRecordFile2.interactable = true;
            m_btnRecordFile3.interactable = true;
            m_btnRecordFile4.interactable = true;
            m_btnRecordFile5.interactable = true;
        }
        else
        {
            m_txtRecord.text = "Playback from File";
            m_btnRecordFile1.interactable = HasSlot(1);
            m_btnRecordFile2.interactable = HasSlot(2);
            m_btnRecordFile3.interactable = HasSlot(3);
            m_btnRecordFile4.interactable = HasSlot(4);
            m_btnRecordFile5.interactable = HasSlot(5);
        }

        m_btnRecordFile1.GetComponentInChildren<Text>().text = GetSlotText(1);
        m_btnRecordFile2.GetComponentInChildren<Text>().text = GetSlotText(2);
        m_btnRecordFile3.GetComponentInChildren<Text>().text = GetSlotText(3);
        m_btnRecordFile4.GetComponentInChildren<Text>().text = GetSlotText(4);
        m_btnRecordFile5.GetComponentInChildren<Text>().text = GetSlotText(5);
    }

    public void DoLoadRecording()
    {
        m_panelRecord.SetActive(true);
        m_panelConnect.SetActive(false);
        
        m_bWantPlayback = true;

        UpdateRecordPanel();
    }

    public bool HasSlot(int nSlot)
    {
        string destination = Application.dataPath + "/slot" + nSlot + ".dat";

        if (File.Exists(destination))
        {
            return true;
        }

        return false;
    }

    public string GetSlotText(int nSlot)
    {
        DateTime date_created;
        string destination = Application.dataPath + "/slot" + nSlot + ".dat";

        if (!File.Exists(destination))
        {
            return "Slot " + nSlot + " [Empty]";
        }

        date_created = File.GetLastWriteTime(destination);

        return "Slot " + nSlot + " [" + date_created.ToString() + "]";
    }

    public void OnRecordFile1()
    {
        StartRecording(1);
    }

    public void OnRecordFile2()
    {
        StartRecording(2);
    }

    public void OnRecordFile3()
    {
        StartRecording(3);
    }

    public void OnRecordFile4()
    {
        StartRecording(4);
    }

    public void OnRecordFile5()
    {
        StartRecording(5);
    }

    public void OnRecordCancel()
    {
        m_panelRecord.SetActive(false);
        m_ts_from_panel_dismiss = new TimeSpan(0);

        if (m_bWantPlayback)
        {
            m_bWantPlayback = false;
            m_panelConnect.SetActive(true);
        }
    }

    public void StopRecording()
    {
        if (null != m_bwRecorder)
        {
            m_bwRecorder.Close();
            m_bwRecorder = null;
        }

        if (null != m_fileRecord)
        {
            m_fileRecord.Close();
            m_fileRecord = null;
        }

        m_bIsRecording = false;
    }

    public void StopPlayback()
    {
        if (null != m_brPlayback)
        {
            m_brPlayback.Close();
            m_brPlayback = null;
        }

        if (null != m_filePlayback)
        {
            m_filePlayback.Close();
            m_filePlayback = null;
        }

        m_bIsPlayback = false;
    }

    public void StartRecording2(string destination)
    {
        if (File.Exists(destination))
        {
            //m_fileRecord = File.OpenWrite(destination);
            m_fileRecord = File.Create(destination);
        }
        else
        {
            m_fileRecord = File.Create(destination);
        }

        if (null == m_fileRecord)
        {
            return;
        }

        m_bwRecorder = new BinaryWriter(m_fileRecord);
        m_bwRecorder.Write(m_iFileVersion);

        m_bIsRecording = true;
    }

    public void StartPlayback(string destination)
    {
        int nFileVersion;

        if (!File.Exists(destination))
        {
            return;
        }

        m_filePlayback = File.OpenRead(destination);

        if (null == m_filePlayback)
        {
            return;
        }

        m_brPlayback = new BinaryReader(m_filePlayback);
        nFileVersion = m_brPlayback.ReadInt32();

        m_bIsPlayback = true;

        m_fPlaybackRate = 1.0;
    }

    public void StartRecording(int nSlot)
    {
        //string destination = Application.persistentDataPath + "/slot" + nSlot + ".dat";
        string destination = Application.dataPath + "/slot" + nSlot + ".dat";

        m_panelConnect.SetActive(false);
        m_panelRecord.SetActive(false);
        m_ts_from_panel_dismiss = new TimeSpan(0);

        StopRecording();
        StopPlayback();

        if (!m_bWantPlayback)
        {
            StartRecording2(destination);
        }
        else
        {
            StartPlayback(destination);
        }
    }

    public void ShowBaseObjects(bool bShow)
    {
        m_theCube.SetActive(bShow);
        m_txtFreqStart.SetActive(bShow);
        m_txtFreqEnd.SetActive(bShow);
        m_txtM1.SetActive(bShow);
        m_txtM2.SetActive(bShow);

        m_panelIterations.SetActive(bShow);
        m_panelKeys.SetActive(bShow & m_bShowInstructions);
        m_panelSmallKeys.SetActive(bShow & !m_bShowInstructions);
        m_panelCellInfo.SetActive(bShow);
        m_panelMarker1.SetActive(bShow);
        m_panelMarker2.SetActive(bShow);
        m_panelDelta.SetActive(bShow);
        m_panelRP.SetActive(bShow);
        m_panelDSP.SetActive(bShow);
    }

    // Use this for initialization
    void Start () {

        LoadSettings();

        m_vecInitialCameraPosition = Camera.main.transform.position;

        ShowBaseObjects(false);

        InitRFE();

    }


    private void OnRFE_ReceivedConfigData(object sender, EventArgs e)
    {
        //ReportDebug(m_sRFEReceivedString);
        m_objRFE.SweepData.CleanAll(); //we do not want mixed data sweep values
    }

    public void InitGraphics(int nNumDataPoints)
    {
        int idx;
        int i;
        int j;

        if (nNumDataPoints < 20)
        {
            return;
        }

        m_iNumDataPoints = nNumDataPoints;

        m_fCurrentSweepAmp = new double[m_iNumDataPoints];
        m_fCurrentSweepFreq = new double[m_iNumDataPoints];
        m_fCurrentTimeStamp = new double[m_iNumDataPoints];

        m_bInitializedGraphics = true;

        m_iCurMarker = 0;

        ShowBaseObjects(true);

        m_objAllSweeps = new GameObject();
        m_objAllSweeps.name = "Waterfall";

        m_objSweep = new GameObject[m_iNumSweeps];

        for (j = 0; j < m_iNumSweeps; j++)
        {
            m_objSweep[j] = new GameObject();
            m_objSweep[j].transform.SetParent(m_objAllSweeps.transform);
            m_objSweep[j].name = "Sweep " + j;

            m_objSweep[j].transform.localPosition = new Vector3(0, 0, j);
        }

        m_cells = new MyCell[m_iNumSweeps * m_iNumDataPoints];

        for (i = 0; i < m_iNumDataPoints; i++)
        {
            for (j = 0; j < m_iNumSweeps; j++)
            {
                idx = (j * m_iNumDataPoints) + i;

                m_cells[idx] = new MyCell();

                m_cells[idx].obj = Instantiate(m_theCube);

                m_cells[idx].obj.transform.SetParent(m_objSweep[j].transform);
                m_cells[idx].obj.transform.localPosition = new Vector3(i, 0, 0);
                m_cells[idx].obj.transform.localScale = new Vector3(1.00f, 1.00f, 1.00f);

                m_cells[idx].obj.name = "<uninitialized>";

                m_cells[idx].obj.GetComponent<CubeScript>().obj_highlight = m_cells[idx].obj.transform.Find("HighlightCube").gameObject;
                m_cells[idx].obj.GetComponent<CubeScript>().obj_marker = m_cells[idx].obj.transform.Find("MarkerCube").gameObject;
                m_cells[idx].obj.GetComponent<CubeScript>().dt_set = DateTime.UtcNow;
                m_cells[idx].obj.GetComponent<CubeScript>().row = j;
                m_cells[idx].obj.GetComponent<CubeScript>().col = i;
                

                m_cells[idx].fAmplitude = m_fMinAmplitude;

                //if (i % 2 == 0 && 0 == j)
                //if (i % 2 == 0)
                //{
                //    m_cells[idx].obj.GetComponent<Renderer>().material = m_matAlternate;
                //}
            }

        }



        m_txtFreqStart.gameObject.transform.position = m_cells[0].obj.transform.position;
        m_txtFreqEnd.gameObject.transform.position = m_cells[m_iNumDataPoints - 1].obj.transform.position;

        m_txtFreqStart.gameObject.transform.position += new Vector3(-33, 8, 0);
        m_txtFreqEnd.gameObject.transform.position += new Vector3(0, 8, 0);

        m_txtM1.SetActive(false);
        m_txtM2.SetActive(false);

        m_theCube.SetActive(false);
    }

    public void UpdateSweepRow(int idxRow, bool reset_data)
    {
        Material mat;
        Vector3 pos;
        double fCoeffNew;
        double fCoeffOld;
        double fFreq;
        double fAmpNew;
        double fAmpOld;
        float scale_x;
        float scale_y;
        float scale_z;
        float fAmpHigh;
        float fAmpLow;
        float fAmpSpan;
        float fAmpStep;
        float fAmp;
        int idx;
        int i;
        int j;

        for (i = 0; i < m_iNumDataPoints; i++)
        {
            j = idxRow;
            idx = (j * m_iNumDataPoints) + i;

            fAmpNew = m_fCurrentSweepAmp[i];
            fAmpOld = m_cells[idx].fAmplitude;
            fCoeffNew = 1.0 / (m_iNumStepsToAdvance);
            fCoeffOld = ((m_iNumStepsToAdvance - 1.0) / m_iNumStepsToAdvance);

            if (reset_data)
            {
                m_cells[idx].fAmplitude = fAmpNew;

                m_cells[idx].obj.GetComponent<CubeScript>().dt_set = (new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).AddMilliseconds(m_fCurrentTimeStamp[i]);
            }
            else
            {
                if (MODE_AVG == m_iCalcMode)
                {
                    m_cells[idx].fAmplitude = fCoeffOld * fAmpOld + fCoeffNew * fAmpNew;
                }
                else if (MODE_MAX == m_iCalcMode)
                {
                    m_cells[idx].fAmplitude = Math.Max(fAmpOld, fAmpNew);
                }
                else
                {
                    m_cells[idx].fAmplitude = Math.Min(fAmpOld, fAmpNew);
                }
            }

            UpdateMarkerAmp(m_objMarker1, i, fAmpNew);
            UpdateMarkerAmp(m_objMarker2, i, fAmpNew);

            m_cells[idx].fFreq = m_fCurrentSweepFreq[i];

            scale_x = 1.00f;
            scale_y = (-m_fMinAmplitude) + (float)m_cells[idx].fAmplitude;
            scale_z = 1.00f;

            if (scale_y < 0.01)
            {
                scale_y = 0.01f;
            }

            m_cells[idx].obj.transform.localScale = new Vector3(scale_x, scale_y, scale_z);
            pos = m_cells[idx].obj.transform.localPosition;
            m_cells[idx].obj.transform.localPosition = new Vector3(pos.x, scale_y / 2, 0);

            fAmp = (float)m_cells[idx].fAmplitude;
            fFreq = m_cells[idx].fFreq;

            mat = m_matAmp0;

            fAmpHigh = -10;
            fAmpLow = -90;
            fAmpSpan = Math.Abs(fAmpHigh - fAmpLow);
            fAmpStep = fAmpSpan / 14.0f;

            if (fAmp < fAmpLow + 0 * fAmpStep) { mat = m_matAmp0; }
            else if (fAmp < fAmpLow + 1 * fAmpStep) { mat = m_matAmp1; }
            else if (fAmp < fAmpLow + 2 * fAmpStep) { mat = m_matAmp2; }
            else if (fAmp < fAmpLow + 3 * fAmpStep) { mat = m_matAmp3; }
            else if (fAmp < fAmpLow + 4 * fAmpStep) { mat = m_matAmp4; }
            else if (fAmp < fAmpLow + 5 * fAmpStep) { mat = m_matAmp5; }
            else if (fAmp < fAmpLow + 6 * fAmpStep) { mat = m_matAmp6; }
            else if (fAmp < fAmpLow + 7 * fAmpStep) { mat = m_matAmp7; }
            else if (fAmp < fAmpLow + 8 * fAmpStep) { mat = m_matAmp8; }
            else if (fAmp < fAmpLow + 9 * fAmpStep) { mat = m_matAmp9; }
            else if (fAmp < fAmpLow + 10 * fAmpStep) { mat = m_matAmp10; }
            else if (fAmp < fAmpLow + 11 * fAmpStep) { mat = m_matAmp11; }
            else if (fAmp < fAmpLow + 12 * fAmpStep) { mat = m_matAmp12; }
            else { mat = m_matAmp13; }

            m_cells[idx].obj.GetComponent<Renderer>().material = mat;
            m_cells[idx].obj.GetComponent<CubeScript>().freq = fFreq;
            m_cells[idx].obj.GetComponent<CubeScript>().amp = fAmp;
        }
    }

    public int MoveLastSweepToFront()
    {
        Vector3 vecDiff;
        int idxSweepLast;
        int idxSweepFirst;

        idxSweepFirst = (m_iNumSweeps - m_iSweepCounter) % m_iNumSweeps;
        idxSweepLast = (m_iNumSweeps - m_iSweepCounter - 1) % m_iNumSweeps;

        m_objSweep[idxSweepLast].transform.localPosition = m_objSweep[idxSweepFirst].transform.localPosition;
        m_objSweep[idxSweepLast].transform.localPosition -= new Vector3(0, 0, 1.00f);

        vecDiff = m_objSweep[idxSweepLast].transform.localPosition - m_objSweep[idxSweepFirst].transform.localPosition;
        Camera.main.transform.position += vecDiff;


        // Could child these under one parent game object and move that one object.
        m_txtFreqStart.gameObject.transform.position += vecDiff;
        m_txtFreqEnd.gameObject.transform.position += vecDiff;
        m_txtM1.gameObject.transform.position += vecDiff;
        m_txtM2.gameObject.transform.position += vecDiff;

        return idxSweepLast;
    }

    public void DetectHitCell()
    {
        Transform objHit;
        RaycastHit hit;
        Ray ray;

        ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out hit))
        {
            if (null == m_prevHitObject)
            {
                // Previous is already null; nothing new to do.
                return;
            }

            // Previous is not currently null
            // Deselect it and set it to null before returning
            UpdateVisibleCubes(m_prevHitObject, true, true, false, false);
            m_prevHitObject = null;
            return;
        }

        objHit = hit.transform;

        if (objHit == m_prevHitObject)
        {
            // Same object already selected; nothing new to do.
            return;
        }

        if (null != m_prevHitObject)
        {
            // Deselect previous object before selecting new object
            UpdateVisibleCubes(m_prevHitObject, true, true, false, false);
        }

        // Select the new object
        UpdateVisibleCubes(objHit.gameObject, m_bColumnSelect, false, true, false);

        // Set previous object to new object
        m_prevHitObject = objHit.gameObject;
    }

    public void UpdateGraphics(double fStartFreq, double fEndFreq)
    {
        int idxSweepFront;

        if (!m_bInitializedGraphics)
        {
            return;
        }

        m_txtFreqStart.GetComponent<TextMesh>().text = fStartFreq.ToString("f3");
        m_txtFreqEnd.GetComponent<TextMesh>().text = fEndFreq.ToString("f3");

        if (m_iAdvanceCounter >= m_iNumStepsToAdvance)
        {
            // Reset advance counter
            m_iAdvanceCounter = 0;

            // Move last sweep to the front
            idxSweepFront = MoveLastSweepToFront();

            // Store current sweep at front
            UpdateSweepRow(idxSweepFront, true);

            // Update sweep counter
            m_iSweepCounter++;

            if (m_iSweepCounter >= m_iNumSweeps)
            {
                m_iSweepCounter = 0;
            }
        }
        else
        {
            // Determine front index
            idxSweepFront = (m_iNumSweeps - m_iSweepCounter) % m_iNumSweeps;

            // Store current sweep at front
            UpdateSweepRow(idxSweepFront, false);
        }

        m_iAdvanceCounter++;

    }

    public void RecordData(RFESweepData objData)
    {
        double fCurrentTimeStamp;
        double fElapsed;
        double fFreq;
        double fAmp;
        int i;

        if (!m_bIsRecording)
        {
            return;
        }

        fElapsed = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;

        m_bwRecorder.Write(fElapsed);
        m_bwRecorder.Write(objData.StartFrequencyMHZ);
        m_bwRecorder.Write(objData.EndFrequencyMHZ);
        m_bwRecorder.Write(m_iDSPMode);

        m_bwRecorder.Write(m_iNumDataPoints);

        for (i=0;i<m_iNumDataPoints;i++)
        {
            fAmp = objData.GetAmplitudeDBM((ushort)i);
            m_bwRecorder.Write(fAmp);

            fFreq = objData.GetFrequencyMHZ((ushort)i);
            m_bwRecorder.Write(fFreq);

            fCurrentTimeStamp = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
            m_bwRecorder.Write(fCurrentTimeStamp);
        }
    }

    public void PlaybackData()
    {
        double fElapsed;
        double fStartFreq;
        double fEndFreq;
        int i;

        if (!m_bIsPlayback)
        {
            return;
        }

        if (m_brPlayback.BaseStream.Position >= m_brPlayback.BaseStream.Length)
        {
            m_bIsPlayback = false;
            return;
        }

        m_fDeltaPlaybackElapsed -= (m_fPlaybackRate * TimeSpan.FromSeconds(Time.deltaTime).TotalMilliseconds);

        if (m_fDeltaPlaybackElapsed > 0 && m_fPlaybackRate < m_fMaxPlaybackRate)
        {
            return;
        }

        if (m_fPlaybackRate <= m_fMinPlaybackRate)
        {
            return;
        }

        fElapsed = m_brPlayback.ReadDouble();
        fStartFreq = m_brPlayback.ReadDouble();
        fEndFreq = m_brPlayback.ReadDouble();
        m_iDSPMode = m_brPlayback.ReadInt32();

        m_iNumDataPoints = m_brPlayback.ReadInt32();

        if (!m_bInitializedGraphics)
        {
            InitGraphics(m_iNumDataPoints);
        }

        for (i=0;i<m_iNumDataPoints;i++)
        {
            m_fCurrentSweepAmp[i] = m_brPlayback.ReadDouble();
            m_fCurrentSweepFreq[i] = m_brPlayback.ReadDouble();
            m_fCurrentTimeStamp[i] = m_brPlayback.ReadDouble();
        }

        UpdateGraphics(fStartFreq, fEndFreq);

        if (0 != m_fPrevPlaybackElapsed)
        {
            m_fDeltaPlaybackElapsed = fElapsed - m_fPrevPlaybackElapsed;
        }
        else
        {
            m_fDeltaPlaybackElapsed = 0;
        }

        m_fPrevPlaybackElapsed = fElapsed;
    }

    public void PlaybackData2()
    {
        int nNumLoops;
        int i;

        if (!m_bIsPlayback)
        {
            return;
        }

        if (m_fPlaybackRate < m_fMaxPlaybackRate)
        {
            nNumLoops = 1;
        }
        else
        {
            nNumLoops = 10;
        }

        for (i=0;i<nNumLoops;i++)
        {
            PlaybackData();
        }
    }

    private void OnRFE_UpdateData(object sender, EventArgs e)
    {
        RFESweepData objData = m_objRFE.SweepData.GetData(m_objRFE.SweepData.Count - 1);
        int i;

        if (null == objData)
        {
            return;
        }

        if (!m_bInitializedGraphics)
        {
            InitGraphics(objData.TotalDataPoints);
        }

        for (i=0;i<m_iNumDataPoints;i++)
        {
            m_fCurrentSweepAmp[i] = objData.GetAmplitudeDBM((ushort)i);
            m_fCurrentSweepFreq[i] = objData.GetFrequencyMHZ((ushort)i);
            m_fCurrentTimeStamp[i] = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
        }

        UpdateGraphics(objData.StartFrequencyMHZ, objData.EndFrequencyMHZ);

        RecordData(objData);
    }

    private void OnRFE_ReportLog(object sender, EventArgs e)
    {
        EventReportInfo objArg = (EventReportInfo)e;
        ReportDebug(objArg.Data);
    }

    private void ReportDebug(string sLine)
    {
        //if (!m_edRFEReportLog.IsDisposed && !m_chkDebug.IsDisposed && m_chkDebug.Checked)
        //{
        //    if (sLine.Length > 0)
        //        m_edRFEReportLog.AppendText(sLine);
        //    m_edRFEReportLog.AppendText(Environment.NewLine);
        //}
    }

    private void OnRFE_PortClosed(object sender, EventArgs e)
    {
        ReportDebug("RF Explorer PortClosed");
    }

    public void UpdateCellInfo(GameObject s, bool is_marker, Text t, String strUndefined)
    {
        DateTime dt_file_timestamp;
        DateTime dt_cell_timestamp;
        double fFreq;
        double fAmp;
        int idx;
        int row;
        int col;

        if (null == s)
        {
            t.text = strUndefined;
            return;
        }

        fFreq = s.GetComponent<CubeScript>().freq;

        col = s.GetComponent<CubeScript>().col;

        if (m_bColumnSelect)
        {
            row = (m_iNumSweeps - m_iSweepCounter + 1) % m_iNumSweeps;
        }
        else
        {
            row = s.GetComponent<CubeScript>().row;
        }

        idx = (row * m_iNumDataPoints) + col;

        fAmp = m_cells[idx].fAmplitude;

        t.text = "Freq=" + fFreq.ToString("f3") + "\r\n" + "Amp=" + fAmp.ToString("f2");


        if (m_bColumnSelect)
        {
            return;
        }

        dt_cell_timestamp = s.GetComponent<CubeScript>().dt_set;

        t.text += "\r\n" + dt_cell_timestamp.ToLocalTime().ToString();
    }

    public void UpdateDeltaInfo()
    {
        TimeSpan ts_delta;
        double fDeltaFreq;
        double fDeltaAmp;
        double fAmp1;
        double fAmp2;
        double fSeconds;
        double fMinutes;
        double fHours;
        int idx1;
        int idx2;
        int col1;
        int row1;
        int col2;
        int row2;

        if (null == m_objMarker1 ||
            null == m_objMarker2)
        {
            m_txtMarkerDelta.text = "[Markers not set]";
            return;
        }

        fDeltaFreq = Math.Abs(m_objMarker1.GetComponent<CubeScript>().freq - m_objMarker2.GetComponent<CubeScript>().freq);

        col1 = m_objMarker1.GetComponent<CubeScript>().col;
        col2 = m_objMarker2.GetComponent<CubeScript>().col;

        if (m_bColumnSelect)
        {
            row1 = (m_iNumSweeps - m_iSweepCounter + 1) % m_iNumSweeps;
            row2 = (m_iNumSweeps - m_iSweepCounter + 1) % m_iNumSweeps;
        }
        else
        {
            row1 = m_objMarker1.GetComponent<CubeScript>().row;
            row2 = m_objMarker2.GetComponent<CubeScript>().row;
        }

        idx1 = (row1 * m_iNumDataPoints) + col1;
        idx2 = (row2 * m_iNumDataPoints) + col2;

        fAmp1 = m_cells[idx1].fAmplitude;
        fAmp2 = m_cells[idx2].fAmplitude;

        fDeltaAmp = fAmp1 - fAmp2;

        m_txtMarkerDelta.text = "Delta Freq=" + fDeltaFreq.ToString("f3") + "\r\n" + "Delta Amp=" + fDeltaAmp.ToString("f2");

        if (m_bColumnSelect)
        {
            return;
        }

        ts_delta = (m_cells[idx1].obj.GetComponent<CubeScript>().dt_set - m_cells[idx2].obj.GetComponent<CubeScript>().dt_set);

        fSeconds = ts_delta.TotalSeconds;

        if (fSeconds < 60)
        {
            m_txtMarkerDelta.text += "\r\n" + fSeconds.ToString("f1") + "(s)";
        }
        else if (fSeconds < 3600)
        {
            fSeconds = (int)fSeconds;
            fMinutes = fSeconds / 60;
            fMinutes = (int)fMinutes;
            fSeconds -= 60 * (fMinutes);
            fSeconds = (int)fSeconds;

            m_txtMarkerDelta.text += "\r\n" + (int)fMinutes + "(m) " + (int)fSeconds + "(s)";
        }
        else
        {
            fSeconds = (int)fSeconds;

            fHours = fSeconds / 3600;
            fHours = (int)fHours;
            fSeconds -= 3600 * fHours;

            fMinutes = fSeconds / 60;
            fMinutes = (int)fMinutes;

            fSeconds -= 60 * fMinutes;
            fSeconds = (int)fSeconds;

            m_txtMarkerDelta.text += "\r\n" + (int)fHours + "(h)" + (int)fMinutes + "(m) " + (int)fSeconds + "(s)";
        }
    }

    public void ShowCellInfo()
    {
        UpdateVisibleCubes(m_prevHitObject, m_bColumnSelect, false, true, false);

        UpdateCellInfo(m_prevHitObject, false, m_txtCellInfo, "[Cell Info]");
        UpdateCellInfo(m_objMarker1, true, m_txtMarker1, "[Marker 1 Not Set]");
        UpdateCellInfo(m_objMarker2, true, m_txtMarker2, "[Marker 2 Not Set]");

        UpdateDeltaInfo();
    }

    public void SetMarker()
    {
        Vector3 vecLblMarker;

        if (null == m_prevHitObject)
        {
            return;
        }

        if (m_panelRecord.activeSelf)
        {
            return;
        }

        if (m_ts_from_panel_dismiss.TotalSeconds < 1)
        { 
            return;
        }

        if (0 == m_iCurMarker)
        {
            UpdateVisibleCubes(m_objMarker1, m_bColumnSelect, true, false, false);
            m_objMarker1 = m_prevHitObject;
            m_objMarker1.GetComponent<CubeScript>().marker_amp = m_objMarker1.GetComponent<CubeScript>().amp;

            m_txtM1.SetActive(true);
            vecLblMarker = new Vector3(m_objMarker1.transform.position.x - 6, m_objMarker1.transform.position.y, m_objMarker1.transform.position.z);
            vecLblMarker.y = m_txtFreqStart.transform.position.y - 8;
            vecLblMarker.z = m_txtFreqStart.transform.position.z;
            m_txtM1.transform.position = vecLblMarker;
        }
        else
        {
            UpdateVisibleCubes(m_objMarker2, m_bColumnSelect, true, false, false);
            m_objMarker2 = m_prevHitObject;
            m_objMarker2.GetComponent<CubeScript>().marker_amp = m_objMarker2.GetComponent<CubeScript>().amp;

            m_txtM2.SetActive(true);
            vecLblMarker = new Vector3(m_objMarker2.transform.position.x - 6, m_objMarker2.transform.position.y, m_objMarker2.transform.position.z);
            vecLblMarker.y = m_txtFreqStart.transform.position.y - 8;
            vecLblMarker.z = m_txtFreqStart.transform.position.z;
            m_txtM2.transform.position = vecLblMarker;
        }

        m_iCurMarker = (m_iCurMarker + 1) % 2;
    }

    public void UpdateVisibleCubes(GameObject s, bool do_column, bool show_main, bool show_highlight, bool show_marker)
    {
        int idx;
        int row;
        int col;
        int i;

        if (null == s)
        {
            return;
        }

        row = s.GetComponent<CubeScript>().row;
        col = s.GetComponent<CubeScript>().col;

        if (!do_column)
        {
            s.GetComponent<Renderer>().enabled = show_main;
            s.GetComponent<CubeScript>().obj_highlight.GetComponent<Renderer>().enabled = show_highlight;
            s.GetComponent<CubeScript>().obj_marker.GetComponent<Renderer>().enabled = show_marker;
            return;
        }

        for (i=0;i<m_iNumSweeps;i++)
        {
            idx = (i * m_iNumDataPoints) + col;
            m_cells[idx].obj.GetComponent<Renderer>().enabled = show_main;
            m_cells[idx].obj.GetComponent<CubeScript>().obj_highlight.GetComponent<Renderer>().enabled = show_highlight;
            m_cells[idx].obj.GetComponent<CubeScript>().obj_marker.GetComponent<Renderer>().enabled = show_marker;
        }
    }

    public void UpdateMarkerAmp(GameObject s, int col, double fAmpNew)
    {
        double fNumSamples;
        double fCoeffNew;
        double fCoeffOld;
        double fAmpOld;
        double fAmp;

        if (null == s)
        {
            return;
        }

        if (col != s.GetComponent<CubeScript>().col)
        {
            return;
        }

        fAmpOld = s.GetComponent<CubeScript>().marker_amp;

        fNumSamples = 12.0;

        fCoeffOld = (fNumSamples - 1.0) / fNumSamples;
        fCoeffNew = 1.0 / fNumSamples;

        fAmp = fCoeffNew * fAmpNew + fCoeffOld * fAmpOld;

        s.GetComponent<CubeScript>().marker_amp = fAmp;

    }

    public void ShowMarkers()
    {
        UpdateVisibleCubes(m_objMarker1, m_bColumnSelect, false, false, true);
        UpdateVisibleCubes(m_objMarker2, m_bColumnSelect, false, false, true);

        if (0==m_iCurMarker)
        {
            m_txtM1IsNext.gameObject.SetActive(true);
            m_txtM2IsNext.gameObject.SetActive(false);
        }
        else
        {
            m_txtM1IsNext.gameObject.SetActive(false);
            m_txtM2IsNext.gameObject.SetActive(true);
        }
    }

    public void ToggleColumnSelect()
    {
        m_bColumnSelect = !m_bColumnSelect;
        UpdateVisibleCubes(m_objMarker1, true, true, false, false);
        UpdateVisibleCubes(m_objMarker2, true, true, false, false);

        UpdateVisibleCubes(m_objMarker1, m_bColumnSelect, false, false, true);
        UpdateVisibleCubes(m_objMarker2, m_bColumnSelect, false, false, true);
    }

    public void ToggleRecording()
    {
        m_bWantPlayback = false;

        if (!m_bIsRecording)
        {
            m_panelRecord.SetActive(!m_panelRecord.activeSelf);
        }
        else
        {
            StopRecording();
        }

        if (m_panelRecord.activeSelf)
        {
            UpdateRecordPanel();
        }
    }

    public void SaveSettings()
    {
        //string destination = Application.persistentDataPath + "/settings.dat";
        string destination = Application.dataPath + "/settings.dat";
        FileStream file;

        if (File.Exists(destination))
        {
            //file = File.OpenWrite(destination);
            file = File.Create(destination);
        }
        else
        {
            file = File.Create(destination);
        }

        BinaryWriter bw = new BinaryWriter(file);
        bw.Write(m_iFileVersion);
        bw.Write(m_iNumStepsToAdvance);
        bw.Write(m_iCalcMode);
        bw.Write(m_iDSPMode);
        bw.Write(m_bShowInstructions);

        file.Close();
    }

    public void LoadSettings()
    {
        //string destination = Application.persistentDataPath + "/settings.dat";
        string destination = Application.dataPath + "/settings.dat";
        FileStream file;
        int nFileVersion;

        if (!File.Exists(destination))
        {
            return;
        }

        file = File.OpenRead(destination);

        BinaryReader br = new BinaryReader(file);
        nFileVersion = br.ReadInt32();
        m_iNumStepsToAdvance = br.ReadInt32();
        m_iCalcMode = br.ReadInt32();
        m_iDSPMode = br.ReadInt32();
        m_bShowInstructions = br.ReadBoolean();

        file.Close();
    }

    public void OnApplicationQuit()
    {
        SaveSettings();
        StopRecording();
    }

    public void UpdatePanelRP()
    {
        m_txtRPActive.gameObject.SetActive(false);
        m_txtRP.text = "[Record/Playback]";

        if (m_bIsRecording)
        {
            m_txtLblRP.text = "R";
            m_txtRP.text = "Recording...";
            m_txtRP.text += "\r\n" + (m_fileRecord.Position / 1024) + " KB";
            m_txtRPActive.gameObject.SetActive(true);
        }

        if (m_bIsPlayback)
        {
            m_txtLblRP.text = "P";
            m_txtRP.text = "Playback @ " + m_fPlaybackRate.ToString("f2");

            if (m_fPlaybackRate >= m_fMaxPlaybackRate)
            {
                m_txtRP.text = "Playback @ MAX rate";
            }
            else if (m_fPlaybackRate <= m_fMinPlaybackRate)
            {
                m_txtRP.text = "Playback @ MIN rate";
            }

            m_txtRP.text += "\r\n" + (m_filePlayback.Position / 1024) + " / " + (m_filePlayback.Length / 1024) + " KB";
            m_txtRPActive.gameObject.SetActive(true);
        }
    }

    public void UpdatePanelDSP()
    {
        if (DSP_MODE_AUTO == m_iDSPMode)
        {
            m_txtDSP.text = "DSP Mode=Auto";
        }
        else if (DSP_MODE_FILTER == m_iDSPMode)
        {
            m_txtDSP.text = "DSP Mode=Filter";
        }
        else if (DSP_MODE_FAST == m_iDSPMode)
        {
            m_txtDSP.text = "DSP Mode=Fast";
        }
        else
        {
            m_txtDSP.text = "DSP Mode=Unknown";
        }
    }

    public void DecreasePlaybackRate()
    {
        m_fPlaybackRate /= 2.0;

        if (m_fPlaybackRate < m_fMinPlaybackRate)
        {
            m_fPlaybackRate = m_fMinPlaybackRate;
        }
    }

    public void IncreasePlaybackRate()
    {
        m_fPlaybackRate *= 2.0;

        if (m_fPlaybackRate > m_fMaxPlaybackRate)
        {
            m_fPlaybackRate = m_fMaxPlaybackRate;
        }
    }

    public void ToggleInstructions()
    {
        m_bShowInstructions = !m_bShowInstructions;

        m_panelSmallKeys.SetActive(!m_bShowInstructions);
        m_panelKeys.SetActive(m_bShowInstructions);
    }

    public void DoRestart()
    {
        StopPlayback();
        StopRecording();

        ShowBaseObjects(false);

        m_objRFE.Close();

        Destroy(m_objAllSweeps);

        m_bInitializedGraphics = false;
        m_fDeltaPlaybackElapsed = 0;
        m_fPrevPlaybackElapsed = 0;
        m_objMarker1 = null;
        m_objMarker2 = null;
        m_objSweep = null;
        m_iConnectIndex = -1;
        m_cells = null;
        m_bColumnSelect = true;
        m_bPause = false;
        m_iCurMarker = 0;
        m_iAdvanceCounter = m_iNumStepsToAdvance; //0;
        m_iSweepCounter = 0;

        Camera.main.transform.position = m_vecInitialCameraPosition;

        m_panelRecord.SetActive(false);
        m_panelConnect.SetActive(true);
        m_txtConnecting.gameObject.SetActive(false);
    }

    public void HandleStepsAdvance()
    {
        int nAdjustment;

        m_ts_last_adj_steps_advance += TimeSpan.FromSeconds(Time.deltaTime);

        if (Input.GetKeyUp(KeyCode.J))
        {
            m_ts_held_key_J = new TimeSpan(0);
        }

        if (Input.GetKeyUp(KeyCode.K))
        {
            m_ts_held_key_K = new TimeSpan(0);
        }

        nAdjustment = 0;

        if (Input.GetKey(KeyCode.J))
        {
            m_ts_held_key_J += TimeSpan.FromSeconds(Time.deltaTime);

            if (m_ts_held_key_J.TotalMilliseconds >= 1500 ||
                m_ts_last_adj_steps_advance.TotalMilliseconds >= 250)
            {
                nAdjustment = -1;
            }

            if (m_ts_held_key_J.TotalMilliseconds >= 5000)
            {
                nAdjustment = -5;
            }

            if (m_ts_held_key_J.TotalMilliseconds >= 10000)
            {
                nAdjustment = -10;
            }
        }

        if (Input.GetKey(KeyCode.K))
        {
            m_ts_held_key_K += TimeSpan.FromSeconds(Time.deltaTime);

            if (m_ts_held_key_K.TotalMilliseconds >= 1500 ||
                m_ts_last_adj_steps_advance.TotalMilliseconds >= 250)
            {
                nAdjustment = 1;
            }

            if (m_ts_held_key_K.TotalMilliseconds >= 5000)
            {
                nAdjustment = 5;
            }

            if (m_ts_held_key_K.TotalMilliseconds >= 10000)
            {
                nAdjustment = 10;
            }
        }

        m_iNumStepsToAdvance += nAdjustment;

        if (0 != nAdjustment)
        {      
            m_ts_last_adj_steps_advance = new TimeSpan(0);
        }

        if (m_iNumStepsToAdvance < 1)
        {
            m_iNumStepsToAdvance = 1;
        }
    }

    public void ToggleDSPMode()
    {
        if (m_bIsPlayback)
        {
            // Do not send DSP commands while in playback mode.
            return;
        }

        if (DSP_MODE_FAST == m_iDSPMode)
        {
            m_iDSPMode = DSP_MODE_FILTER;
            m_objRFE.SendCommand("Cp\x01");
        }
        else
        {
            m_iDSPMode = DSP_MODE_FAST;
            m_objRFE.SendCommand("Cp\x02");
        }
    }

    // Update is called once per frame
    void Update () {

        m_ts_from_panel_dismiss += TimeSpan.FromSeconds(Time.deltaTime);

        m_txtIterations.text = "Iterations=" + m_iNumStepsToAdvance + "\r\nMode=";

        if (MODE_AVG == m_iCalcMode)
        {
            m_txtIterations.text += "AVG";
        }
        else if (MODE_MAX == m_iCalcMode)
        {
            m_txtIterations.text += "MAX";
        }
        else
        {
            m_txtIterations.text += "MIN";
        }

        HandleStepsAdvance();

        if (Input.GetKeyUp(KeyCode.M))
        {
            m_iCalcMode = (m_iCalcMode + 1) % 3;
        }

        if (Input.GetKeyUp(KeyCode.T))
        {
            ToggleColumnSelect();
        }

        if (Input.GetKeyUp(KeyCode.R))
        {
            ToggleRecording();
        }

        if (Input.GetMouseButtonDown(0))
        {
            SetMarker();
        }

        if (Input.GetKeyUp(KeyCode.Comma))
        {
            DecreasePlaybackRate();
        }

        if (Input.GetKeyUp(KeyCode.Period))
        {
            IncreasePlaybackRate();
        }

        if (Input.GetKeyUp(KeyCode.P))
        {
            m_bPause = !m_bPause;

            if (m_bPause && m_bIsPlayback)
            {
                m_fSavedPlaybackRate = m_fPlaybackRate;
                m_fPlaybackRate = m_fMinPlaybackRate;
            }
            else if (!m_bPause && m_bIsPlayback)
            {
                m_fPlaybackRate = m_fSavedPlaybackRate;
            }
        }

        if (Input.GetKeyUp(KeyCode.Y))
        {
            ToggleDSPMode();
        }

        if (Input.GetKeyUp(KeyCode.I))
        {
            ToggleInstructions();
        }

        if (Input.GetKeyUp(KeyCode.Escape))
        {
            DoRestart();
            return;
        }

        // Cell info detection
        DetectHitCell();

        // Show cell info
        ShowCellInfo();

        ProcessConnection();
        ProcessData();

        PlaybackData2();

        // Show markers
        ShowMarkers();

        // Update RP panel
        UpdatePanelRP();

        // Update DSP panel
        UpdatePanelDSP();
    }

    public void ProcessConnection()
    {
        if (-1 == m_iConnectIndex)
        {
            return;
        }

        m_iConnectIndex++;

        if (m_iConnectIndex < 5)
        {
            return;
        }

        DoConnect2();

        m_iConnectIndex = -1;
    }

    public void ProcessData()
    {
        if (null == m_objRFE)
        {
            return;
        }

        if (!m_objRFE.PortConnected)
        {
            return;
        }

        if (m_bPause)
        {
            return;
        }

        String sRFEReceivedString;
        m_objRFE.ProcessReceivedString(true, out sRFEReceivedString);
    }

}
