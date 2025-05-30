
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace SaccFlightAndVehicles
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    [DefaultExecutionOrder(-10)]
    public class SaccEntity : UdonSharpBehaviour
    {
        [Tooltip("Put all scripts used by this vehicle that use the event system into this list (excluding DFUNCs)")]
        public UdonSharpBehaviour[] ExtensionUdonBehaviours;
        [Tooltip("Function dial scripts that you wish to be on the left dial")]
        public UdonSharpBehaviour[] Dial_Functions_L;
        [Tooltip("Function dial scripts that you wish to be on the right dial")]
        public UdonSharpBehaviour[] Dial_Functions_R;
        [Tooltip("Pointer on the dial")]
        public Transform LStickDisplayHighlighter;
        [Tooltip("Pointer on the dial")]
        public Transform RStickDisplayHighlighter;
        [Tooltip("How far the stick has to be pushed to select a function")]
        public float DialSensitivity = 0.7f;
        [Tooltip("Should there be a function at the top middle of the function dial[ ]? Or a divider[x]? Useful for adjusting function positions with an odd number of functions")]
        public bool LeftDialDivideStraightUp = false;
        [Tooltip("See above")]
        public bool RightDialDivideStraightUp = false;
        [Tooltip("Layer to find all objects on to use as AAM targets")]
        public LayerMask AAMTargetsLayer = 1 << 25;//layer 25
        [Tooltip("Object that is enabled when entering vehicle in any seat. Will be removed"), Header("Removing InVehicleOnly next version.")]
        public GameObject InVehicleOnly;
        [Tooltip("Object that is enabled when entering vehicle in any seat")]
        public GameObject[] EnableInVehicle;
        [Tooltip("Object that is enabled when holding this object")]
        public GameObject[] EnableWhenHolding;
        [Tooltip("To tell child scripts/rigidbodys where the center of the vehicle is")]
        public Transform CenterOfMass;
        [Tooltip("Change voice volumes for players who are in the vehicle together? (checked by SaccVehicleSeat)")]
        public bool DoVoiceVolumeChange = true;
        [Tooltip("Double tap the exit vehicle button to exit the vehicle?")]
        public bool DoubleTapToExit = false;
        [Tooltip("Double tap the exit vehicle button to exit the vehicle?")]
        public bool DisableBulletHitEvent = false;
        [Header("Selection Sound")]

        [Tooltip("Oneshot sound played each time function selection changes")]
        public AudioSource SwitchFunctionSound;
        public bool PlaySelectSoundLeft = true;
        public bool PlaySelectSoundRight = true;
        [Tooltip("You can add seats that are NOT a child of this object, if you want to control the vehicle from outside")]
        public VRCStation[] ExternalSeats;

        //HelicopterCurling
        public float BulletForceMulti = 50f;
        //HelicopterCurling

        [Header("For debugging, auto filled on build")]
        public GameObject[] AAMTargets;
        [System.NonSerializedAttribute] public bool InEditor = true;//false if in clientsim
        private VRCPlayerApi localPlayer;
        [System.NonSerializedAttribute] public VRC_Pickup EntityPickup;
        [System.NonSerializedAttribute] public bool Piloting;
        [System.NonSerializedAttribute] public int UsersID;
        [System.NonSerializedAttribute] public string UsersName;
        private Vector2 RStickCheckAngle;
        private Vector2 LStickCheckAngle;
        [System.NonSerializedAttribute] public bool MySeatIsExternal;
        [System.NonSerializedAttribute] public GameObject LastHitParticle;
        [System.NonSerializedAttribute] public float LStickFuncDegrees;
        [System.NonSerializedAttribute] public float RStickFuncDegrees;
        [System.NonSerializedAttribute] public float LStickFuncDegreesDivider;
        [System.NonSerializedAttribute] public float RStickFuncDegreesDivider;
        [System.NonSerializedAttribute] public int LStickNumFuncs;
        [System.NonSerializedAttribute] public int RStickNumFuncs;
        [System.NonSerializedAttribute] public bool DoDialLeft;
        [System.NonSerializedAttribute] public bool DoDialRight;
        //specially used by limits function
        //this stuff can be used by DFUNCs
        //if these == 0 then they are not disabled. Being an int allows more than one extension to disable it at a time
        //the bools exists to save externs every frame
        [System.NonSerializedAttribute] public bool _DisableLeftDial;
        [System.NonSerializedAttribute, FieldChangeCallback(nameof(DisableLeftDial_))] public int DisableLeftDial = 0;
        public int DisableLeftDial_
        {
            set
            {
                _DisableLeftDial = value > 0;
                DisableLeftDial = value;
            }
            get => DisableLeftDial;
        }
        [System.NonSerializedAttribute] public bool _DisableRightDial;
        [System.NonSerializedAttribute, FieldChangeCallback(nameof(DisableRightDial_))] public int DisableRightDial = 0;
        public int DisableRightDial_
        {
            set
            {
                _DisableRightDial = value > 0;
                DisableRightDial = value;
            }
            get => DisableRightDial;
        }
        [System.NonSerialized] public bool _DisallowOwnerShipTransfer;
        [System.NonSerializedAttribute, FieldChangeCallback(nameof(DisallowOwnerShipTransfer_))] public int DisallowOwnerShipTransfer = 0;
        public int DisallowOwnerShipTransfer_
        {
            set
            {
                _DisallowOwnerShipTransfer = value > 0;
                DisallowOwnerShipTransfer = value;
            }
            get => DisallowOwnerShipTransfer;
        }
        [System.NonSerializedAttribute] public bool KeepAwake = false;
        [System.NonSerializedAttribute, FieldChangeCallback(nameof(KeepAwake_))] public int _KeepAwake = 0;
        public int KeepAwake_
        {
            set
            {
                if (value > 0 && _KeepAwake == 0)
                {
                    SendEventToExtensions("SFEXT_L_KeepAwake");
                }
                else if (value == 0 && _KeepAwake > 0)
                {
                    SendEventToExtensions("SFEXT_L_KeepAwakeFalse");
                }
                KeepAwake = value > 0;
                _KeepAwake = value;
            }
            get => _KeepAwake;
        }
        [System.NonSerializedAttribute] public bool[] LStickNULL;
        [System.NonSerializedAttribute] public bool[] RStickNULL;
        [System.NonSerializedAttribute] public int RStickSelection = -1;
        [System.NonSerializedAttribute] public int LStickSelection = -1;
        [System.NonSerializedAttribute] public int RStickSelectionLastFrame = -1;
        [System.NonSerializedAttribute] public int LStickSelectionLastFrame = -1;
        [System.NonSerializedAttribute] public bool _dead = false;
        public bool dead
        {
            set
            {
                if (value)
                { SendEventToExtensions("SFEXT_G_Dead"); }
                else
                { SendEventToExtensions("SFEXT_G_NotDead"); }
                _dead = value;
            }
            get => _dead;
        }
        [System.NonSerializedAttribute] public bool Using = false;
        [System.NonSerializedAttribute] public bool Occupied = false;
        [System.NonSerializedAttribute] public bool Passenger = false;
        [System.NonSerializedAttribute] public bool InVehicle = false;
        [System.NonSerializedAttribute] public bool InVR = false;
        [System.NonSerializedAttribute] public bool IsOwner;
        [System.NonSerializedAttribute] public bool Initialized;

        //old Leavebutton Stuff
        [System.NonSerializedAttribute] public int PilotSeat = -1;
        [System.NonSerializedAttribute] public int MySeat = -1;
        [System.NonSerializedAttribute] public int[] SeatedPlayers;
        [System.NonSerializedAttribute] public VRCStation[] VehicleStations;
        [System.NonSerializedAttribute] public SaccVehicleSeat[] VehicleSeats;
        [System.NonSerializedAttribute] public SaccEntity LastAttacker;
        [System.NonSerializedAttribute] public float PilotExitTime;
        [System.NonSerializedAttribute] public float PilotEnterTime;
        [System.NonSerializedAttribute] public bool Holding;
        //end of old Leavebutton stuff
        public void Init() { Start(); }
        private void Start()
        {
            if (Initialized) { return; }
            Initialized = true;
            localPlayer = Networking.LocalPlayer;
            if (localPlayer != null)
            {
                InEditor = false;
                IsOwner = localPlayer.isMaster;
                InVR = localPlayer.IsUserInVR();
            }
            else
            {
                IsOwner = true;
                Using = true;
                InVehicle = true;
                Occupied = true;
            }
            Spawnposition = transform.localPosition;
            Spawnrotation = transform.localRotation;
            if (CenterOfMass)
            {
                SetCoM();
            }
            else
            {
                CenterOfMass = gameObject.transform;
                Debug.Log(gameObject.name + ": " + "No Center Of Mass Set");
            }
            VehicleStations = GetComponentsInChildren<VRCStation>(true);
            //add EXTRASEATS to VehicleStations list
            if (ExternalSeats.Length > 0)
            {
                var temp = VehicleStations;
                VehicleStations = new VRCStation[temp.Length + ExternalSeats.Length];
                for (int i = 0; i < temp.Length; i++)
                { VehicleStations[i] = temp[i]; }
                for (int i = temp.Length; i < temp.Length + ExternalSeats.Length; i++)
                { VehicleStations[i] = ExternalSeats[i - temp.Length]; }
            }
            SeatedPlayers = new int[VehicleStations.Length];
            for (int i = 0; i != SeatedPlayers.Length; i++)
            { SeatedPlayers[i] = -1; }
            VehicleSeats = new SaccVehicleSeat[VehicleStations.Length];
            for (int i = 0; i < VehicleSeats.Length; i++)
            {
                VehicleSeats[i] = (SaccVehicleSeat)VehicleStations[i].GetComponent<SaccVehicleSeat>();
                if (VehicleSeats[i]) { VehicleSeats[i].InitializeSeat(); }
            }
            EntityPickup = (VRC_Pickup)gameObject.GetComponent<VRC_Pickup>();

            //Dial Stuff
            LStickNumFuncs = Dial_Functions_L.Length;
            RStickNumFuncs = Dial_Functions_R.Length;
            DoDialLeft = LStickNumFuncs > 1;
            DoDialRight = RStickNumFuncs > 1;
            DisableLeftDial_ = 0;
            DisableRightDial_ = 0;
            LStickFuncDegrees = 360 / Mathf.Max((float)LStickNumFuncs, 1);
            RStickFuncDegrees = 360 / Mathf.Max((float)RStickNumFuncs, 1);
            LStickFuncDegreesDivider = 1 / LStickFuncDegrees;
            RStickFuncDegreesDivider = 1 / RStickFuncDegrees;
            LStickNULL = new bool[LStickNumFuncs];
            RStickNULL = new bool[RStickNumFuncs];
            int u = 0;
            foreach (UdonSharpBehaviour usb in Dial_Functions_L)
            {
                if (usb == null) { LStickNULL[u] = true; }
                u++;
            }
            u = 0;
            foreach (UdonSharpBehaviour usb in Dial_Functions_R)
            {
                if (usb == null) { RStickNULL[u] = true; }
                u++;
            }
            //work out angle to check against for function selection because straight up is the middle of a function
            Vector3 angle = new Vector3(0, 0, -1);
            if (LStickNumFuncs > 1)
            {
                if (LeftDialDivideStraightUp)
                {
                    LStickCheckAngle.x = 0;
                    LStickCheckAngle.y = -1;
                }
                else
                {
                    angle = Quaternion.Euler(0, -((360 / LStickNumFuncs) / 2), 0) * angle;
                    LStickCheckAngle.x = angle.x;
                    LStickCheckAngle.y = angle.z;
                }
            }

            angle = new Vector3(0, 0, -1);
            if (RStickNumFuncs > 1)
            {
                if (RightDialDivideStraightUp)
                {
                    RStickCheckAngle = Vector2.down;
                }
                else
                {
                    angle = Quaternion.Euler(0, -((360 / RStickNumFuncs) / 2), 0) * angle;
                    RStickCheckAngle.x = angle.x;
                    RStickCheckAngle.y = angle.z;
                }
            }

            TellDFUNCsLR();

            foreach (UdonSharpBehaviour EXT in ExtensionUdonBehaviours)
            {
                if (EXT) EXT.SetProgramVariable("EntityControl", this);
            }
            foreach (UdonSharpBehaviour EXT in Dial_Functions_L)
            {
                if (EXT) EXT.SetProgramVariable("EntityControl", this);
            }
            foreach (UdonSharpBehaviour EXT in Dial_Functions_R)
            {
                if (EXT) EXT.SetProgramVariable("EntityControl", this);
            }

            SendEventToExtensions("SFEXT_L_EntityStart");

            //if in editor play mode without clientsim
            if (InEditor)
            {
                PilotEnterVehicleLocal();
                PilotEnterVehicleGlobal(null);
            }
        }
        void OnParticleCollision(GameObject other)
        {
            if (!other || dead || DisableBulletHitEvent) { return; }//avatars can't hurt you, and you can't get hurt when you're dead
            LastHitParticle = other;

            int index = -1;
            string pname = string.Empty;
            if (other.transform.childCount > 0)
            {
                pname = other.transform.GetChild(0).name;
                index = pname.LastIndexOf(':');
            }
            int dmg = 1;
            bool More = true;
            if (index > -1)
            {
                pname = pname.Substring(index);
                if (pname.Length == 3)
                {
                    if (pname[1] == 'x')
                    {
                        if (pname[2] >= '0' && pname[2] <= '9')
                        {
                            //damage reduction using case:
                            dmg = pname[2] - 48;
                            LastHitBulletDamageMulti = 1 / (float)(dmg);
                            More = false;
                        }
                    }
                    else if (pname[1] >= '0' && pname[1] <= '9')
                    {
                        if (pname[2] >= '0' && pname[2] <= '9')
                        {
                            //damage increase using case:
                            dmg = 10 * (pname[1] - 48);
                            dmg += pname[2] - 48;
                            LastHitBulletDamageMulti = dmg == 1 ? 1 : Mathf.Pow(2, dmg - 1);
                            More = true;
                        }
                    }
                }
            }
            //Try to find the saccentity that shot at us
            GameObject EnemyObjs = other;
            SaccEntity EnemyEntityControl = null;
            //search up the hierarchy to find the saccentity directly
            while (!EnemyEntityControl && EnemyObjs.transform.parent)
            {
                EnemyObjs = EnemyObjs.transform.parent.gameObject;
                EnemyEntityControl = EnemyObjs.GetComponent<SaccEntity>();
            }
            LastAttacker = EnemyEntityControl;
            //if failed to find it, search up the hierarchy for an udonsharpbehaviour with a reference to the saccentity (for instantiated missiles etc)
            if (!EnemyEntityControl)
            {
                EnemyObjs = other;
                UdonBehaviour EnemyUdonBehaviour = null;
                while (!EnemyUdonBehaviour && EnemyObjs.transform.parent)
                {
                    EnemyObjs = EnemyObjs.transform.parent.gameObject;
                    EnemyUdonBehaviour = (UdonBehaviour)EnemyObjs.GetComponent(typeof(UdonBehaviour));
                }
                if (EnemyUdonBehaviour)
                { LastAttacker = (SaccEntity)EnemyUdonBehaviour.GetProgramVariable("EntityControl"); }
            }
            SendEventToExtensions("SFEXT_L_BulletHit");
            SendDamageEvent(dmg, More);
            if (LastAttacker && LastAttacker != this) { LastAttacker.SendEventToExtensions("SFEXT_L_DamageFeedback"); }

            //HelicopterCurling
            Vector3 hitVector3 = other.transform.position - transform.position; //機体から弾へのベクトル
            Vector2 hitVector2 = new Vector2(hitVector3.x, hitVector3.z);
            float hitAngle = Vector2.SignedAngle(Vector2.up, hitVector2); //反時計回り（z軸の進行方向に時計回り）で0から180、-180から0
            if (hitAngle < 0)
            {
                hitAngle += 360;
            }
            if (hitAngle < 22.5)
            {
                SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(AddBulletForce000));
            }
            else if (hitAngle < 67.5)
            {
                SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(AddBulletForce045));
            }
            else if (hitAngle < 112.5)
            {
                SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(AddBulletForce090));
            }
            else if (hitAngle < 157.5)
            {
                SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(AddBulletForce135));
            }
            else if (hitAngle < 202.5)
            {
                SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(AddBulletForce180));
            }
            else if (hitAngle < 247.5)
            {
                SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(AddBulletForce225));
            }
            else if (hitAngle < 292.5)
            {
                SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(AddBulletForce270));
            }
            else if (hitAngle < 337.5)
            {
                SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(AddBulletForce315));
            }
            else
            {
                SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(AddBulletForce000));
            }
            //HelicopterCurling
        }
        //HelicopterCurling
        public void AddBulletForce000()
        {
            Vector3 bulletForce = new Vector3(0, 0, -1).normalized;
            AddBulletForce(bulletForce);
        }
        public void AddBulletForce045()
        {
            Vector3 bulletForce = new Vector3(1, 0, -1).normalized;
            AddBulletForce(bulletForce);
        }
        public void AddBulletForce090()
        {
            Vector3 bulletForce = new Vector3(1, 0, 0).normalized;
            AddBulletForce(bulletForce);
        }
        public void AddBulletForce135()
        {
            Vector3 bulletForce = new Vector3(1, 0, 1).normalized;
            AddBulletForce(bulletForce);
        }
        public void AddBulletForce180()
        {
            Vector3 bulletForce = new Vector3(0, 0, 1).normalized;
            AddBulletForce(bulletForce);
        }
        public void AddBulletForce225()
        {
            Vector3 bulletForce = new Vector3(-1, 0, 1).normalized;
            AddBulletForce(bulletForce);
        }
        public void AddBulletForce270()
        {
            Vector3 bulletForce = new Vector3(-1, 0, 0).normalized;
            AddBulletForce(bulletForce);
        }
        public void AddBulletForce315()
        {
            Vector3 bulletForce = new Vector3(-1, 0, -1).normalized;
            AddBulletForce(bulletForce);
        }
        public void AddBulletForce(Vector3 bulletForce)
        {
            if (IsOwner)
            {
                GetComponent<Rigidbody>().AddForce(bulletForce * BulletForceMulti, ForceMode.Impulse);
            }
        }
        //HelicopterCurling
        private void SendDefaultDamage()
        {
            LastHitBulletDamageMulti = 1;
            SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(BulletDamageDefault));
        }
        public void InVehicleControls()
        {
            if (!InVehicle) { return; }
            SendCustomEventDelayedFrames(nameof(InVehicleControls), 1);
            if (Input.GetKeyDown(KeyCode.Return))
            {
                if (!InEditor)
                {
                    ExitVehicleCheck();
                }
            }
            if (!Piloting) { return; }
            Vector2 LStickPos = Vector2.zero;
            Vector2 RStickPos = Vector2.zero;
            float LTrigger = 0;
            float RTrigger = 0;
            if (!InEditor)
            {
                LStickPos.x = Input.GetAxisRaw("Oculus_CrossPlatform_PrimaryThumbstickHorizontal");
                LStickPos.y = Input.GetAxisRaw("Oculus_CrossPlatform_PrimaryThumbstickVertical");
                RStickPos.x = Input.GetAxisRaw("Oculus_CrossPlatform_SecondaryThumbstickHorizontal");
                RStickPos.y = Input.GetAxisRaw("Oculus_CrossPlatform_SecondaryThumbstickVertical");
                LTrigger = Input.GetAxisRaw("Oculus_CrossPlatform_PrimaryIndexTrigger");
                RTrigger = Input.GetAxisRaw("Oculus_CrossPlatform_SecondaryIndexTrigger");
            }

            //LStick Selection wheel
            if (DoDialLeft && !_DisableLeftDial)
            {
                if (InVR && LStickPos.magnitude > DialSensitivity)
                {
                    float stickdir = Vector2.SignedAngle(LStickCheckAngle, LStickPos);

                    stickdir = -(stickdir - 180);
                    int newselection = Mathf.FloorToInt(Mathf.Min(stickdir * LStickFuncDegreesDivider, LStickNumFuncs - 1));
                    if (!LStickNULL[newselection])
                    { LStickSelection = newselection; }
                }
                if (LStickSelection != LStickSelectionLastFrame)
                {
                    //new function selected, send deselected to old one
                    if (LStickSelectionLastFrame != -1 && Dial_Functions_L[LStickSelectionLastFrame] != null)
                    {
                        Dial_Functions_L[LStickSelectionLastFrame].SendCustomEvent("DFUNC_Deselected");
                    }
                    //get udonbehaviour for newly selected function and then send selected
                    if (LStickSelection > -1)
                    {
                        if (Dial_Functions_L[LStickSelection] != null)
                        {
                            Dial_Functions_L[LStickSelection].SendCustomEvent("DFUNC_Selected");
                        }
                    }
                    if (PlaySelectSoundLeft && SwitchFunctionSound) { SwitchFunctionSound.Play(); }
                    if (LStickDisplayHighlighter)
                    {
                        if (LStickSelection < 0)
                        { LStickDisplayHighlighter.localRotation = Quaternion.Euler(0, 180, 0); }
                        else
                        {
                            LStickDisplayHighlighter.localRotation = Quaternion.Euler(0, 0, -LStickFuncDegrees * LStickSelection);
                        }
                    }
                    LStickSelectionLastFrame = LStickSelection;
                }
            }

            //RStick Selection wheel
            if (DoDialRight && !_DisableRightDial)
            {
                if (InVR && RStickPos.magnitude > DialSensitivity)
                {
                    float stickdir = Vector2.SignedAngle(RStickCheckAngle, RStickPos);

                    stickdir = -(stickdir - 180);
                    int newselection = Mathf.FloorToInt(Mathf.Min(stickdir * RStickFuncDegreesDivider, RStickNumFuncs - 1));
                    if (!RStickNULL[newselection])
                    { RStickSelection = newselection; }
                }
                if (RStickSelection != RStickSelectionLastFrame)
                {
                    //new function selected, send deselected to old one
                    if (RStickSelectionLastFrame != -1 && Dial_Functions_R[RStickSelectionLastFrame])
                    {
                        Dial_Functions_R[RStickSelectionLastFrame].SendCustomEvent("DFUNC_Deselected");
                    }
                    //get udonbehaviour for newly selected function and then send selected
                    if (RStickSelection > -1)
                    {
                        if (Dial_Functions_R[RStickSelection])
                        {
                            Dial_Functions_R[RStickSelection].SendCustomEvent("DFUNC_Selected");
                        }
                    }
                    if (PlaySelectSoundRight && SwitchFunctionSound) { SwitchFunctionSound.Play(); }
                    if (RStickDisplayHighlighter)
                    {
                        if (RStickSelection < 0)
                        { RStickDisplayHighlighter.localRotation = Quaternion.Euler(0, 180, 0); }
                        else
                        {
                            RStickDisplayHighlighter.localRotation = Quaternion.Euler(0, 0, -RStickFuncDegrees * RStickSelection);
                        }
                    }
                    RStickSelectionLastFrame = RStickSelection;
                }
            }
        }
        private float LastJumpInput = 0f;
        public override void InputJump(bool value, VRC.Udon.Common.UdonInputEventArgs args)
        {
            if (InVehicle && InVR && args.boolValue)
            {
                ExitVehicleCheck();
            }
        }
        public void ExitVehicleCheck()
        {
            if (!DoubleTapToExit)
            { ExitStation(); }
            else
            {
                if (Time.time - LastJumpInput < .3f)
                { ExitStation(); return; }
                LastJumpInput = Time.time;
            }
        }
        private void OnEnable()
        {
            SendEventToExtensions("SFEXT_L_OnEnable");
            ConstantForce cf = GetComponent<ConstantForce>();
            if (cf)
            {
                cf.relativeForce = Vector3.zero;
                cf.relativeTorque = Vector3.zero;
            }
        }
        private void OnDisable()
        {
            SendEventToExtensions("SFEXT_L_OnDisable");
        }
        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            if (player.isLocal)
            {
                IsOwner = true;
                TakeOwnerShipOfExtensions();
                SendEventToExtensions("SFEXT_O_TakeOwnership");
            }
            else
            {
                if (IsOwner)
                {
                    IsOwner = false;
                    SendEventToExtensions("SFEXT_O_LoseOwnership");
                }
            }
            SendEventToExtensions("SFEXT_L_OwnershipTransfer");
        }
        public void PilotEnterVehicleLocal()//called from PilotSeat
        {
            Using = true;
            Piloting = true;
            InVehicle = true; SendCustomEventDelayedFrames(nameof(InVehicleControls), 1);
            Occupied = true;
            if (LStickNumFuncs == 1)
            {
                Dial_Functions_L[0].SendCustomEvent("DFUNC_Selected");
            }
            if (RStickNumFuncs == 1)
            {
                Dial_Functions_R[0].SendCustomEvent("DFUNC_Selected");
            }
            if (LStickDisplayHighlighter)
            { LStickDisplayHighlighter.localRotation = Quaternion.Euler(0, 180, 0); }
            if (RStickDisplayHighlighter)
            { RStickDisplayHighlighter.localRotation = Quaternion.Euler(0, 180, 0); }

            if (!InEditor && localPlayer.IsUserInVR()) { InVR = true; }
            if (InVehicleOnly) { InVehicleOnly.SetActive(true); }
            for (int i = 0; i < EnableInVehicle.Length; i++)
            { if (EnableInVehicle[i]) EnableInVehicle[i].SetActive(true); }
            if (!_DisallowOwnerShipTransfer)
            {
                Networking.SetOwner(localPlayer, gameObject);
                TakeOwnerShipOfExtensions();
            }
            SendEventToExtensions("SFEXT_O_PilotEnter");
        }
        public void PilotEnterVehicleGlobal(VRCPlayerApi player)
        {
            if (player != null)
            {
                Occupied = true;
                UsersName = player.displayName;
                UsersID = player.playerId;
                PilotEnterTime = Time.time;
                SendEventToExtensions("SFEXT_G_PilotEnter");
            }
        }
        public void PilotExitVehicle(VRCPlayerApi player)
        {
            PilotExitTime = Time.time;
            LStickSelection = -1;
            RStickSelection = -1;
            LStickSelectionLastFrame = -1;
            RStickSelectionLastFrame = -1;
            SendEventToExtensions("SFEXT_G_PilotExit");
            if (player.isLocal)
            {
                Using = false;
                Piloting = false;
                InVehicle = false;
                if (InVehicleOnly) { InVehicleOnly.SetActive(false); }
                for (int i = 0; i < EnableInVehicle.Length; i++)
                { if (EnableInVehicle[i]) EnableInVehicle[i].SetActive(false); }
                SendEventToExtensions("SFEXT_O_PilotExit");
            }
            Occupied = false;
            UsersName = string.Empty;
            UsersID = -1;
        }
        public void PassengerEnterVehicleLocal()
        {
            Passenger = true;
            InVehicle = true; SendCustomEventDelayedFrames(nameof(InVehicleControls), 1);
            if (LStickDisplayHighlighter)
            { LStickDisplayHighlighter.localRotation = Quaternion.Euler(0, 180, 0); }
            if (RStickDisplayHighlighter)
            { RStickDisplayHighlighter.localRotation = Quaternion.Euler(0, 180, 0); }
            if (!InEditor && localPlayer.IsUserInVR()) { InVR = true; }//move me to start when they fix the bug
            if (InVehicleOnly) { InVehicleOnly.SetActive(true); }
            for (int i = 0; i < EnableInVehicle.Length; i++)
            { if (EnableInVehicle[i]) EnableInVehicle[i].SetActive(true); }
            SendEventToExtensions("SFEXT_P_PassengerEnter");
        }
        public void PassengerExitVehicleLocal()
        {
            Passenger = false;
            InVehicle = false;
            if (InVehicleOnly) { InVehicleOnly.SetActive(false); }
            for (int i = 0; i < EnableInVehicle.Length; i++)
            { if (EnableInVehicle[i]) EnableInVehicle[i].SetActive(false); }
            SendEventToExtensions("SFEXT_P_PassengerExit");
        }
        public void PassengerEnterVehicleGlobal()
        {
            SendEventToExtensions("SFEXT_G_PassengerEnter");
        }
        public void PassengerExitVehicleGlobal()
        {
            SendEventToExtensions("SFEXT_G_PassengerExit");
        }
        public override void OnPlayerJoined(VRCPlayerApi player)
        {
            if (IsOwner)
            { SendEventToExtensions("SFEXT_O_OnPlayerJoined"); }
        }
        public override void OnPickup()
        {
            Holding = true;
            Using = true;
            if (EnableWhenHolding.Length > 0)
            {
                for (int i = 0; i < EnableWhenHolding.Length; i++)
                { EnableWhenHolding[i].SetActive(true); }
            }
            if (!_DisallowOwnerShipTransfer) { TakeOwnerShipOfExtensions(); }
            if (LStickNumFuncs == 1)
            { Dial_Functions_L[0].SendCustomEvent("DFUNC_Selected"); }
            if (RStickNumFuncs == 1)
            { Dial_Functions_R[0].SendCustomEvent("DFUNC_Selected"); }
            SendEventToExtensions("SFEXT_O_OnPickup");
            SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(SendEvent_Pickup));
        }
        public override void OnDrop()
        {
            Holding = false;
            Using = false;
            if (EnableWhenHolding.Length > 0)
            {
                for (int i = 0; i < EnableWhenHolding.Length; i++)
                { EnableWhenHolding[i].SetActive(false); }
            }
            SendEventToExtensions("SFEXT_O_OnDrop");
            SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(SendEvent_Drop));
        }
        public void SendEvent_Pickup()
        {
            SendEventToExtensions("SFEXT_G_OnPickup");
        }
        public void SendEvent_Drop()
        {
            SendEventToExtensions("SFEXT_G_OnDrop");
        }
        public override void OnPickupUseDown()
        {
            SendEventToExtensions("SFEXT_O_OnPickupUseDown");
        }
        public override void OnPickupUseUp()
        {
            SendEventToExtensions("SFEXT_O_OnPickupUseUp");
        }
        [System.NonSerialized] VRCPlayerApi LastPlayerCollisionEnter;
        public override void OnPlayerCollisionEnter(VRCPlayerApi player)
        {
            LastPlayerCollisionEnter = player;
            SendEventToExtensions("SFEXT_L_OnPlayerCollisionEnter");
        }
        [System.NonSerialized] VRCPlayerApi LastPlayerCollisionExit;
        public override void OnPlayerCollisionExit(VRCPlayerApi player)
        {
            LastPlayerCollisionEnter = player;
            SendEventToExtensions("SFEXT_L_OnPlayerCollisionExit");
        }
        public void SetCoM()
        {
            //WARNING: Setting this will reset ITR in SaccAirVehicle etc.
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb)
            {
                GetComponent<Rigidbody>().centerOfMass = transform.InverseTransformDirection(CenterOfMass.position - transform.position);//correct position if scaled}
            }
            SendEventToExtensions("SFEXT_L_CoMSet");
        }
        [System.NonSerialized] public Collision LastCollisionEnter;
        private void OnCollisionEnter(Collision Col)
        {
            LastCollisionEnter = Col;
            SendEventToExtensions("SFEXT_L_OnCollisionEnter");
        }
        [System.NonSerialized] public Collider LastTriggerEnter;
        private void OnTriggerEnter(Collider Trig)
        {
            LastTriggerEnter = Trig;
            SendEventToExtensions("SFEXT_L_OnTriggerEnter");
        }
        [System.NonSerialized] public Collision LastCollisionExit;
        private void OnCollisionExit(Collision Col)
        {
            LastCollisionExit = Col;
            SendEventToExtensions("SFEXT_L_OnCollisionExit");
        }
        [System.NonSerialized] public Collider LastTriggerExit;
        private void OnTriggerExit(Collider Trig)
        {
            LastTriggerExit = Trig;
            SendEventToExtensions("SFEXT_L_OnTriggerExit");
        }
        public void TellDFUNCsLR()
        {
            foreach (UdonSharpBehaviour EXT in Dial_Functions_L)
            {
                if (EXT)
                { EXT.SendCustomEvent("DFUNC_LeftDial"); }
            }
            foreach (UdonSharpBehaviour EXT in Dial_Functions_R)
            {
                if (EXT)
                { EXT.SendCustomEvent("DFUNC_RightDial"); }
            }
        }
        public void TakeOwnerShipOfExtensions()
        {
            if (!InEditor)
            {
                foreach (UdonSharpBehaviour EXT in ExtensionUdonBehaviours)
                { if (EXT) { if (!localPlayer.IsOwner(EXT.gameObject)) { Networking.SetOwner(localPlayer, EXT.gameObject); } } }
                foreach (UdonSharpBehaviour EXT in Dial_Functions_L)
                { if (EXT) { if (!localPlayer.IsOwner(EXT.gameObject)) { Networking.SetOwner(localPlayer, EXT.gameObject); } } }
                foreach (UdonSharpBehaviour EXT in Dial_Functions_R)
                { if (EXT) { if (!localPlayer.IsOwner(EXT.gameObject)) { Networking.SetOwner(localPlayer, EXT.gameObject); } } }
            }
        }
        [RecursiveMethod]
        public void SendEventToExtensions(string eventname)
        {
            if (!Initialized) { return; }
            foreach (UdonSharpBehaviour EXT in ExtensionUdonBehaviours)
            {
                if (EXT)
                { EXT.SendCustomEvent(eventname); }
            }
            foreach (UdonSharpBehaviour EXT in Dial_Functions_L)
            {
                if (EXT)
                { EXT.SendCustomEvent(eventname); }
            }
            foreach (UdonSharpBehaviour EXT in Dial_Functions_R)
            {
                if (EXT)
                { EXT.SendCustomEvent(eventname); }
            }
        }
        public void ExitStation()
        {
            if (MySeat > -1 && MySeat < VehicleStations.Length)
            { VehicleStations[MySeat].ExitStation(localPlayer); }
        }
        [System.NonSerializedAttribute] public Vector3 Spawnposition;
        [System.NonSerializedAttribute] public Quaternion Spawnrotation;
        public void EntityRespawn()//can be used by simple items to respawn
        {
            if (!Occupied && !_dead && (!EntityPickup || !EntityPickup.IsHeld))
            {
                Networking.SetOwner(localPlayer, gameObject);
                IsOwner = true;
                VRC.SDK3.Components.VRCObjectSync ObjectSync = (VRC.SDK3.Components.VRCObjectSync)gameObject.GetComponent(typeof(VRC.SDK3.Components.VRCObjectSync));
                if (ObjectSync)
                {
                    ObjectSync.Respawn();
                }
                else
                {
                    transform.localPosition = Spawnposition;
                    transform.localRotation = Spawnrotation;
                }
                Rigidbody rb = GetComponent<Rigidbody>();
                if (rb)
                {
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;//editor needs this
                }
            }
        }
        // ToDo: Use static to better performance on U#1.0
        // public static UdonSharpBehaviour GetExtention(SaccEntity entity, string udonTypeName)
        public UdonSharpBehaviour GetExtention(string udonTypeName)
        {
            SaccEntity entity = this;

            foreach (var extention in entity.ExtensionUdonBehaviours)
            {
                if (extention && extention.GetUdonTypeName() == udonTypeName) return extention;
            }
            foreach (var extention in entity.Dial_Functions_L)
            {
                if (extention && extention.GetUdonTypeName() == udonTypeName) return extention;
            }
            foreach (var extention in entity.Dial_Functions_R)
            {
                if (extention && extention.GetUdonTypeName() == udonTypeName) return extention;
            }
            return null;
        }

        // ToDo: Use static to better performance on U#1.0
        // public static UdonSharpBehaviour[] GetExtentions(SaccEntity entity, string udonTypeName)
        public UdonSharpBehaviour[] GetExtentions(string udonTypeName)
        {
            SaccEntity entity = this;

            var result = new UdonSharpBehaviour[entity.ExtensionUdonBehaviours.Length + entity.Dial_Functions_L.Length + entity.Dial_Functions_R.Length];
            var count = 0;
            foreach (var extention in entity.ExtensionUdonBehaviours)
            {
                if (extention && extention.GetUdonTypeName() == udonTypeName)
                {
                    result[count++] = extention;
                }
            }
            foreach (var extention in entity.Dial_Functions_L)
            {
                if (extention && extention.GetUdonTypeName() == udonTypeName)
                {
                    result[count++] = extention;
                }
            }
            foreach (var extention in entity.Dial_Functions_R)
            {
                if (extention && extention.GetUdonTypeName() == udonTypeName)
                {
                    result[count++] = extention;
                }
            }

            var finalResult = new UdonSharpBehaviour[count];
            System.Array.Copy(result, finalResult, count);

            return finalResult;
        }
        public void ToggleStickSelectionLeft(UdonSharpBehaviour dfunc)
        {
            var index = System.Array.IndexOf(Dial_Functions_L, dfunc);
            if (LStickSelection == index)
            {
                LStickSelection = -1;
                dfunc.SendCustomEvent("DFUNC_Deselected");
            }
            else
            {
                LStickSelection = index;
                dfunc.SendCustomEvent("DFUNC_Selected");
            }
        }

        public void ToggleStickSelectionRight(UdonSharpBehaviour dfunc)
        {
            var index = System.Array.IndexOf(Dial_Functions_R, dfunc);
            if (RStickSelection == index)
            {
                RStickSelection = -1;
                dfunc.SendCustomEvent("DFUNC_Deselected");
            }
            else
            {
                RStickSelection = index;
                dfunc.SendCustomEvent("DFUNC_Selected");
            }
        }
        public void SetDeadFor(float deadtime)
        {
            dead = true;
            SendCustomEventDelayedSeconds(nameof(UnsetSetDead), deadtime);
        }
        public void UnsetSetDead() { dead = false; }
        public void SendDamageEvent(int dmg, bool More)
        {
            if (More)//More than default damage
            {
                switch (dmg)
                {
                    case 2:
                        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(BulletDamage2x));
                        break;
                    case 3:
                        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(BulletDamage4x));
                        break;
                    case 4:
                        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(BulletDamage8x));
                        break;
                    case 5:
                        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(BulletDamage16x));
                        break;
                    case 6:
                        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(BulletDamage32x));
                        break;
                    case 7:
                        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(BulletDamage64x));
                        break;
                    case 8:
                        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(BulletDamage128x));
                        break;
                    case 9:
                        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(BulletDamage256x));
                        break;
                    case 10:
                        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(BulletDamage512x));
                        break;
                    case 11:
                        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(BulletDamage1024x));
                        break;
                    case 12:
                        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(BulletDamage2048x));
                        break;
                    case 13:
                        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(BulletDamage4096x));
                        break;
                    case 14:
                        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(BulletDamage8192x));
                        break;
                    default:
                        if (dmg != 1) { Debug.LogWarning("Invalid bullet damage, using default"); }
                        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(BulletDamageDefault));
                        break;
                }
            }
            else
            {
                switch (dmg)
                {
                    case 2:
                        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(BulletDamageHalf));
                        break;
                    case 3:
                        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(BulletDamageThird));
                        break;
                    case 4:
                        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(BulletDamageQuarter));
                        break;
                    case 5:
                        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(BulletDamageFifth));
                        break;
                    case 6:
                        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(BulletDamageSixth));
                        break;
                    case 7:
                        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(BulletDamageSeventh));
                        break;
                    case 8:
                        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(BulletDamageEighth));
                        break;
                    case 9:
                        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(BulletDamageNinth));
                        break;
                    default:
                        if (dmg != 1) { Debug.LogWarning("Invalid bullet damage, using default"); }
                        SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(BulletDamageDefault));
                        break;
                }
            }
        }
        [System.NonSerializedAttribute] public float LastHitBulletDamageMulti = 1;
        public void BulletDamageNinth()
        {
            LastHitBulletDamageMulti = .11111111111111f;
            SendEventToExtensions("SFEXT_G_BulletHit");
        }
        public void BulletDamageEighth()
        {
            LastHitBulletDamageMulti = .125f;
            SendEventToExtensions("SFEXT_G_BulletHit");
        }
        public void BulletDamageSeventh()
        {
            LastHitBulletDamageMulti = .14285714285714f;
            SendEventToExtensions("SFEXT_G_BulletHit");
        }
        public void BulletDamageSixth()
        {
            LastHitBulletDamageMulti = .16666666666666f;
            SendEventToExtensions("SFEXT_G_BulletHit");
        }
        public void BulletDamageFifth()
        {
            LastHitBulletDamageMulti = .2f;
            SendEventToExtensions("SFEXT_G_BulletHit");
        }
        public void BulletDamageQuarter()
        {
            LastHitBulletDamageMulti = .25f;
            SendEventToExtensions("SFEXT_G_BulletHit");
        }
        public void BulletDamageThird()
        {
            LastHitBulletDamageMulti = .33333333333333f;
            SendEventToExtensions("SFEXT_G_BulletHit");
        }
        public void BulletDamageHalf()
        {
            LastHitBulletDamageMulti = .5f;
            SendEventToExtensions("SFEXT_G_BulletHit");
        }
        public void BulletDamageDefault()
        {
            LastHitBulletDamageMulti = 1;
            SendEventToExtensions("SFEXT_G_BulletHit");
        }
        public void BulletDamage2x()
        {
            LastHitBulletDamageMulti = 2;
            SendEventToExtensions("SFEXT_G_BulletHit");
        }
        public void BulletDamage4x()
        {
            LastHitBulletDamageMulti = 4;
            SendEventToExtensions("SFEXT_G_BulletHit");
        }
        public void BulletDamage8x()
        {
            LastHitBulletDamageMulti = 8;
            SendEventToExtensions("SFEXT_G_BulletHit");
        }
        public void BulletDamage16x()
        {
            LastHitBulletDamageMulti = 16;
            SendEventToExtensions("SFEXT_G_BulletHit");
        }
        public void BulletDamage32x()
        {
            LastHitBulletDamageMulti = 32;
            SendEventToExtensions("SFEXT_G_BulletHit");
        }
        public void BulletDamage64x()
        {
            LastHitBulletDamageMulti = 64;
            SendEventToExtensions("SFEXT_G_BulletHit");
        }
        public void BulletDamage128x()
        {
            LastHitBulletDamageMulti = 128;
            SendEventToExtensions("SFEXT_G_BulletHit");
        }
        public void BulletDamage256x()
        {
            LastHitBulletDamageMulti = 256;
            SendEventToExtensions("SFEXT_G_BulletHit");
        }
        public void BulletDamage512x()
        {
            LastHitBulletDamageMulti = 512;
            SendEventToExtensions("SFEXT_G_BulletHit");
        }
        public void BulletDamage1024x()
        {
            LastHitBulletDamageMulti = 1024;
            SendEventToExtensions("SFEXT_G_BulletHit");
        }
        public void BulletDamage2048x()
        {
            LastHitBulletDamageMulti = 2048;
            SendEventToExtensions("SFEXT_G_BulletHit");
        }
        public void BulletDamage4096x()
        {
            LastHitBulletDamageMulti = 4096;
            SendEventToExtensions("SFEXT_G_BulletHit");
        }
        public void BulletDamage8192x()
        {
            LastHitBulletDamageMulti = 8192;
            SendEventToExtensions("SFEXT_G_BulletHit");
        }
    }
}