using Controller;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Ports;
using System.Threading.Tasks;
using System.Windows;

namespace gamepadLatencyChecker
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public MainWindow()
        {
            InitializeComponent();

            this.DataContext = this;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private static readonly PropertyChangedEventArgs SerialPortNamesPropertyChangedEventArgs = new PropertyChangedEventArgs(nameof(SerialPortNames));

        private ObservableCollection<string> _seriakPortNames;
        /// <summary>
        /// COMポート名一覧
        /// </summary>
        public ObservableCollection<string> SerialPortNames
        {
            get { return this._seriakPortNames; }
            set
            {
                if (this._seriakPortNames == value) { return; }
                this._seriakPortNames = value;
                this.PropertyChanged?.Invoke(this, SerialPortNamesPropertyChangedEventArgs);
            }
        }

        private static readonly PropertyChangedEventArgs SelectedSerialPortNamePropertyChangedEventArgs = new PropertyChangedEventArgs(nameof(SelectedSerialPortName));

        private string _selectedSerialPortName;
        /// <summary>
        /// 選択されているCOMポート名
        /// </summary>
        public string SelectedSerialPortName
        {
            get { return this._selectedSerialPortName; }
            set
            {
                if (this._selectedSerialPortName == value) { return; }
                this._selectedSerialPortName = value;
                this.PropertyChanged?.Invoke(this, SelectedSerialPortNamePropertyChangedEventArgs);
            }
        }


        /// <summary>
        /// シリアルポート名一覧コンボボックスに値を設定します。
        /// </summary>
        private void SetSerialPortNames()
        {
            string[] names = SerialPort.GetPortNames();
            SerialPortNames = new ObservableCollection<string>(names);
        }

        /// <summary>
        /// 実行ボタン押下イベント
        /// </summary>
        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SelectedSerialPortName))
            {
                return;
            }

            //使えるコントローラの作成
            List<XInput> xInputs = new List<XInput>();
            for (uint i = 0; i < 4; i++)
            {
                if (XInput.IsConnected(i))
                {
                    xInputs.Add(new XInput(i));
                }
            }

            if (xInputs.Count == 0)
            {
                return;
            }

            await Task.Run(() => 
            {
                using (SerialPort port = new SerialPort(SelectedSerialPortName, 115200))
                {
                    Stopwatch stopwatch = new Stopwatch();

                    stopwatch.Restart();
                    port.Write(new byte[1] { 1 }, 0, 1);

                    //コントローラのボタン入力があるまでループ
                    while (true)
                    {
                        foreach (var input in xInputs)
                        {
                            XInputState contState = input.GetState();
                            XInputGamepadState Xpad = contState.Gamepad;
                            XInputButtonKind buttonFlags = Xpad.Buttons;
                            if (buttonFlags != 0)
                            {
                                //ボタン入力があったら、Stopwatchを止める。
                                stopwatch.Stop();
                                break;
                            }
                        }

                        if (!stopwatch.IsRunning)
                        {
                            //Stopwatchが止まっていたら、ボタン入力があったので、終了。
                            break;
                        }
                    }

                }
            });

        }
    }
}
