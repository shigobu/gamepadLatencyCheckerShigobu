using System;
using System.Runtime.InteropServices;

namespace Controller
{
    /// <summary>
    /// Xinput方式によるコントローラーの操作を提供します。
    /// </summary>
    public class XInput
    {
        #region DllImport宣言

        [DllImport("xinput1_3.dll", CallingConvention = CallingConvention.Winapi)]
        extern static uint XInputGetCapabilities(uint num, uint flag, out XInputCapabilities pxic);

        [DllImport("xinput1_3.dll", CallingConvention = CallingConvention.Winapi)]
        extern static uint XInputGetDSoundAudioDeviceGuids(uint num, out Guid render, out Guid capture);

        [DllImport("xinput1_3.dll", CallingConvention = CallingConvention.Winapi)]
        extern static uint XInputGetKeystroke(uint num, uint fetch, out XInputState stroke);

        [DllImport("xinput1_3.dll", CallingConvention = CallingConvention.Winapi)]
        extern static uint XInputSetState(uint num, ref XInputVibrationState vib);

        [DllImport("xinput1_3.dll", CallingConvention = CallingConvention.Winapi)]
        extern static uint XInputGetState(uint num, out XInputState state);

        [DllImport("xinput1_3.dll", CallingConvention = CallingConvention.Winapi)]
        extern static void XInputEnable([MarshalAs(UnmanagedType.Bool)]bool enable);

        #endregion

        /// <summary>
        /// コントローラーの性能を取得します。
        /// </summary>
        public XInputCapabilities Capabilities { get; protected set; }

        /// <summary>
        /// ヘッドセットが接続されている場合、レンダリングデバイスのGUIDを取得します。
        /// 接続されていない場合、Guid.Emptyが返されると思われます。
        /// </summary>
        public Guid DSoundRenderDeviceGuid { get; protected set; }

        /// <summary>
        /// ヘッドセットが接続されている場合、キャプチャデバイスのGUIDを取得します。
        /// 接続されていない場合、Guid.Emptyが返されると思われます。
        /// </summary>
        public Guid DSoundCaptureDeviceGuid { get; protected set; }

        /// <summary>
        /// コントローラーの番号を取得します。
        /// </summary>
        public uint Number { get; protected set; }

        uint packetnum = 0;

        /// <summary>
        /// 指定されたコントローラー番号を使用して初期化します。
        /// </summary>
        /// <param name="number">コントローラー番号(0~3)</param>
        public XInput(uint number)
        {
            Number = number;
            if (Number >= 4) throw new ArgumentOutOfRangeException("コントローラー番号に4以上は指定できません。");
            if (!IsConnected(Number)) throw new NotSupportedException("指定された番号のコントローラーは存在しません。");

            var c = new XInputCapabilities();
            XInputGetCapabilities(Number, 0, out c);
            Capabilities = c;

            var cd = new Guid();
            var rd = new Guid();
            XInputGetDSoundAudioDeviceGuids(Number, out rd, out cd);
            DSoundCaptureDeviceGuid = cd;
            DSoundRenderDeviceGuid = rd;

        }

        /// <summary>
        /// コントローラーの現在の状態を取得します。
        /// </summary>
        /// <returns>現在の状態</returns>
        public XInputState GetState()
        {
            if (!IsConnected(Number)) throw new NotSupportedException("接続が切れた可能性があります");
            var i = new XInputState();
            XInputGetState(Number, out i);
            packetnum = i.PacketNumber;
            return i;
        }

        /// <summary>
        /// コントローラーの現在の状態を取得して、
        /// 前回GetStateかTryGetStateを呼び出した時と同じ状態だった場合は、
        /// falseを返します。変化があった場合は、trueを返します。
        /// </summary>
        /// <param name="state">現在の状態</param>
        /// <returns>変化があった場合true</returns>
        public bool TryGetState(out XInputGamepadState state)
        {
            var i = GetState();
            state = i.Gamepad;
            if (packetnum == i.PacketNumber)
            {
                return false;
            }
            else
            {
                packetnum = i.PacketNumber;
                return true;
            }
        }

        /// <summary>
        /// 指定されたコントローラー番号は接続されているかチェックします。
        /// </summary>
        /// <param name="num">コントローラー番号</param>
        /// <returns>接続されている場合はtrue</returns>
        public static bool IsConnected(uint num)
        {
            var s = new XInputState();
            if (num >= 4) throw new ArgumentOutOfRangeException("コントローラー番号に4以上は指定できません。");
            //0x48FはERROR_DEVICE_NOT_CONNECTED
            if (XInputGetState(num, out s) == 0x48F)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        /// <summary>
        /// 振動機能の状態を送信します。
        /// </summary>
        /// <param name="state">設定する状態</param>
        public void SetVibration(XInputVibrationState state)
        {
            XInputSetState(Number, ref state);
        }

    }

    /// <summary>
    /// Xinput対応コントローラーの性能を記述します。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct XInputCapabilities
    {

        /// <summary>
        /// タイプ。現状XINPUT_DEVTYPE_GAMEPAD(0x01)のみ。
        /// </summary>
        public byte Type;

        /// <summary>
        /// サブタイプ。詳しい種類が指定されます。
        /// </summary>
        [MarshalAs(UnmanagedType.U1)]
        public XInputSubTypeKind SubType;

        /// <summary>
        /// コントローラーの機能。現状XINPUT_CAPS_VOICE_SUPPORTED(0x04)のみ。
        /// </summary>
        public ushort Flags;

        /// <summary>
        /// コントローラーの状態。
        /// </summary>
        [MarshalAs(UnmanagedType.Struct)]
        public XInputGamepadState Gamepad;

        /// <summary>
        /// コントローラーの振動の状態。
        /// </summary>
        [MarshalAs(UnmanagedType.Struct)]
        public XInputVibrationState Vibration;
    }

    /// <summary>
    /// コントローラーの状態を表します。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct XInputState
    {
        /// <summary>
        /// 状態パケット番号。この番号が連続して一致している場合、コントローラーに変化はない。
        /// </summary>
        public uint PacketNumber;

        /// <summary>
        /// コントローラーの状態。
        /// </summary>
        [MarshalAs(UnmanagedType.Struct)]
        public XInputGamepadState Gamepad;
    }

    /// <summary>
    /// Xinput対応コントローラーの操作状態を記述します。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct XInputGamepadState
    {
        /// <summary>
        /// 各ボタン。それぞれビットマスク指定。
        /// </summary>
        [MarshalAs(UnmanagedType.U2)]
        public XInputButtonKind Buttons;

        /// <summary>
        /// 左トリガー。0で無押下、255で完全押下。
        /// </summary>
        public byte LeftTrigger;

        /// <summary>
        /// 右トリガー。0で無押下、255で完全押下。
        /// </summary>
        public byte RightTrigger;

        /// <summary>
        /// 左スティックX軸。-32768で最も左、0で中央、32767で最も右。
        /// </summary>
        public short ThumbLeftX;

        /// <summary>
        /// 左スティックY軸。-32768で最も下、、0で中央、32767で最も上。
        /// </summary>
        public short ThumbLeftY;

        /// <summary>
        /// 右スティックX軸。-32768で最も左、0で中央、32767で最も右。
        /// </summary>
        public short ThumbRightX;

        /// <summary>
        /// 左スティックY軸。-32768で最も下、、0で中央、32767で最も上。
        /// </summary>
        public short ThumbRightY;
    }

    /// <summary>
    /// Xinput対応コントローラーの振動機能の状態を指定します。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct XInputVibrationState
    {
        /// <summary>
        /// 左モーターの速度。0は全く振動せず、65535では
        /// 完全に振動する。大きく動く方。
        /// </summary>
        public ushort LeftMotorSpeed;

        /// <summary>
        /// 右モーターの速度。0は全く振動せず、65535では
        /// 完全に振動する。小さく動く方。
        /// </summary>
        public ushort RightMotorSpeed;
    }

    /// <summary>
    /// Xinput対応コントローラーの種類を表します。
    /// </summary>
    public enum XInputSubTypeKind : byte
    {
        /// <summary>
        /// 通常のゲームパッド
        /// </summary>
        Gamepad = 0x01,

        /// <summary>
        /// ステアリング型
        /// </summary>
        Wheel = 0x02,

        /// <summary>
        /// アーケードスティック
        /// </summary>
        ArcadeStick = 0x03,

        /// <summary>
        /// フライトスティック
        /// </summary>
        FlightStick = 0x04,

        /// <summary>
        /// ダンスパッド(おそらくDDR用)
        /// </summary>
        DancePad = 0x05,

        /// <summary>
        /// ギター(おそらくギタフリ用)
        /// </summary>
        Guitar = 0x06,

        /// <summary>
        /// ドラムキット(おそらくドラマニ用)
        /// </summary>
        DrumKit = 0x07
    }

    /// <summary>
    /// Xinput対応コントローラーのボタンの種類を表します。
    /// </summary>
    [Flags]
    public enum XInputButtonKind : ushort
    {
        /// <summary>
        /// デジタルパッド上
        /// </summary>
        DigitalPadUp = 0x0001,

        /// <summary>
        /// デジタルパッド下
        /// </summary>
        DigitalPadDown = 0x0002,

        /// <summary>
        /// デジタルパッド左
        /// </summary>
        DigitalPadLeft = 0x0004,

        /// <summary>
        /// デジタルパッド右
        /// </summary>
        DigitalPadRight = 0x0008,

        /// <summary>
        /// スタート
        /// </summary>
        Start = 0x0010,

        /// <summary>
        /// バック
        /// </summary>
        Back = 0x0020,

        /// <summary>
        /// 左スティックボタン
        /// </summary>
        LeftThumb = 0x0040,

        /// <summary>
        /// 右スティックボタン
        /// </summary>
        RightThumb = 0x0080,

        /// <summary>
        /// 左ショルダー(左トリガーの上)
        /// </summary>
        LeftShoulder = 0x0100,

        /// <summary>
        /// 左ショルダー(左トリガーの上)
        /// </summary>
        RightShoulder = 0x0200,

        /// <summary>
        /// Aボタン
        /// </summary>
        A = 0x1000,

        /// <summary>
        /// Bボタン
        /// </summary>
        B = 0x2000,

        /// <summary>
        /// Xボタン
        /// </summary>
        X = 0x4000,

        /// <summary>
        /// Yボタン
        /// </summary>
        Y = 0x8000,

    }
}