using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Sandbox;
using Sandbox.Game;
using VRage.Input;
using VRage.Utils;
using VRage.Plugins;
using HarmonyLib;
using WootingAnalogSDKNET;

namespace WootingPlugin
{
    public class WootingPlugin : IPlugin
    {
        public static WootingPlugin Instance { get; private set; }

        public void Init(object gameInstance = null)
        {
            Instance = this;

            MyLogExtensions.Info(MySandboxGame.Log, "WootingPlugin: Patching methods");
            try
            {
                var harmony = new Harmony("WootingPlugin");
                harmony.PatchAll(Assembly.GetExecutingAssembly());
            }
            catch (Exception e)
            {
                MyLogExtensions.Error(MySandboxGame.Log, $"WootingPlugin: Caught {e}:{e.Message} while patching methods.\nDisabling plugin");
                return;
            }

            try
            {
                var (numDev, result) = WootingAnalogSDK.Initialise();

                if (result == WootingAnalogResult.Ok)
                {
                    MyLogExtensions.Info(MySandboxGame.Log, $"WootingPlugin initialized successfully, {numDev} device(s)");
                }
                else
                {
                    MyLogExtensions.Error(MySandboxGame.Log, $"Failed to initialize WootingPlugin: {result}");
                    return;
                }

                if (numDev < 1)
                {
                    DeviceFailed();
                }
                else
                {
                    Patch.ReadAnalog = true;
                }
            }
            catch (SEHException)
            {
                MyLogExtensions.Error(MySandboxGame.Log, "WootingPlugin: WootingAnalogSDK failed to initialize due to an internal error");
                return;
            }

            WootingAnalogSDK.SetKeycodeMode(KeycodeType.VirtualKey);
        }

        public void DeviceFailed()
        {
            MyLogExtensions.Error(MySandboxGame.Log, "WootingPlugin: No keyboard detected");
            WootingAnalogSDK.DeviceEvent += DeviceEvent;
            Patch.ReadAnalog = false;
        }

        public void DeviceEvent(DeviceEventType type, DeviceInfo info)
        {
            if (type == DeviceEventType.Connected)
            {
                WootingAnalogSDK.DeviceEvent -= DeviceEvent;
                Patch.ReadAnalog = true;
                MyLogExtensions.Info(MySandboxGame.Log, "WootingPlugin: Keyboard detected");
            }
        }

        public void Update() { }

        public void Dispose()
        {
            WootingAnalogSDK.UnInitialise();
        }
    }

    /// <summary>
    /// Changed to patch MyVrageInput.GetGameControlAnalogState instead of MyControl.GetAnalogState (thanksKeen)
    /// </summary>
    [HarmonyPatch(typeof(MyVRageInput), "GetGameControlAnalogState")]
    internal static class Patch
    {
        public static bool ReadAnalog { get; set; }
        private static FieldInfo hasFocusField = typeof(MySandboxGame).GetField("hasFocus", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly MyStringId[] m_analogControls = new MyStringId[]
        {
            MyControlsSpace.FORWARD,
            MyControlsSpace.BACKWARD,
            MyControlsSpace.STRAFE_LEFT,
            MyControlsSpace.STRAFE_RIGHT,
            MyControlsSpace.ROLL_LEFT,
            MyControlsSpace.ROLL_RIGHT,
            MyControlsSpace.JUMP,
            MyControlsSpace.CROUCH,
            MyControlsSpace.ROTATION_DOWN,
            MyControlsSpace.ROTATION_LEFT,
            MyControlsSpace.ROTATION_RIGHT,
            MyControlsSpace.ROTATION_UP,
        };
        
        // Some default sensitivity multpliers (looking at you, roll) make less sense once you have analogue control over that axis
        private static readonly Dictionary<MyStringId, float> m_sensitivityMultipliers= new Dictionary<MyStringId, float>()
        {
            { MyControlsSpace.ROLL_LEFT, 3f },
            { MyControlsSpace.ROLL_RIGHT, 3f },
            { MyControlsSpace.ROTATION_DOWN, 2f },
            { MyControlsSpace.ROTATION_UP, 2f },
            { MyControlsSpace.ROTATION_LEFT, 2f },
            { MyControlsSpace.ROTATION_RIGHT, 2f },
        };
        static float Postfix(float returnValue, MyStringId controlId)
        {
            if (!ReadAnalog) return returnValue;
            if (!m_analogControls.Contains(controlId)) return returnValue;
            if (MySandboxGame.Static.PauseInput) return returnValue;
            
            // Check if the game has focus
            if (hasFocusField != null && MySandboxGame.Static != null)
            {
                bool hasFocus = (bool)hasFocusField.GetValue(MySandboxGame.Static);
                if (!hasFocus) return returnValue;
            }

 
            MyControl control = MyInput.Static.GetGameControl(controlId);
            if (control == null) return returnValue;

            float key1Val = GetAnalogValue(control.GetKeyboardControl());
            float key2Val = GetAnalogValue(control.GetSecondKeyboardControl());
            float analogVal = key1Val > key2Val ? key1Val : key2Val;

            if (controlId == MyControlsSpace.ROLL_LEFT || controlId == MyControlsSpace.ROLL_RIGHT) analogVal *= 3f; // Stupid!!
            if (controlId == MyControlsSpace.ROTATION_DOWN || controlId == MyControlsSpace.ROTATION_UP || controlId == MyControlsSpace.ROTATION_LEFT || controlId == MyControlsSpace.ROTATION_RIGHT) analogVal *= 2f; // AlsoStupid!!

            
            // Use whichever is greater: the Wooting analog value or the original game value.
            // This way analog always works, and non-Wooting inputs (gamepad, mouse) still function.
            return (analogVal != 0f) ? analogVal : returnValue;
        }

        private static float GetAnalogValue(MyKeys key)
        {
            if (key != MyKeys.None)
            {
                var (value, result) = WootingAnalogSDK.ReadAnalog((ushort)key);

                if (result != WootingAnalogResult.Ok)
                {
                    MyLogExtensions.Error(MySandboxGame.Log, $"WootingPlugin: Failed to read key {key} from WootingAnalogSDK: {result}");
                    if (result == WootingAnalogResult.NoDevices)
                    {
                        WootingPlugin.Instance.DeviceFailed();
                    }
                }
                else
                {
                    return value;
                }
            }
            return 0f;
        }
    }
}
