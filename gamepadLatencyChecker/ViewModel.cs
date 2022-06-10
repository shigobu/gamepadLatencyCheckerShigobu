using Controller;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;

namespace gamepadLatencyChecker
{
    public class ViewModel
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public ViewModel()
        {
            SetSerialPortNames();

            BindingOperations.EnableCollectionSynchronization(ResultList, _listBoxLock);
            StartButtonCommand = new DelegateCommand(Button_Click);
        }

        // ロックオブジェクト
        private readonly object _listBoxLock = new object();

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

        private static readonly PropertyChangedEventArgs TryTimesPropertyChangedEventArgs = new PropertyChangedEventArgs(nameof(TryTimes));

        private int tryTimes = 10;
        /// <summary>
        /// 試行回数
        /// </summary>
        public int TryTimes
        {
            get { return this.tryTimes; }
            set
            {
                if (this.tryTimes == value) { return; }
                this.tryTimes = value;
                this.PropertyChanged?.Invoke(this, TryTimesPropertyChangedEventArgs);
            }
        }

        private static readonly PropertyChangedEventArgs ResultListPropertyChangedEventArgs = new PropertyChangedEventArgs(nameof(ResultList));

        private ObservableCollection<string> _resultList = new ObservableCollection<string>();
        /// <summary>
        /// 結果リストボックスに表示する文字列
        /// </summary>
        public ObservableCollection<string> ResultList
        {
            get { return this._resultList; }
            set
            {
                if (this._resultList == value) { return; }
                this._resultList = value;
                this.PropertyChanged?.Invoke(this, ResultListPropertyChangedEventArgs);
            }
        }

        private static readonly PropertyChangedEventArgs StartButtonCommandPropertyChangedEventArgs = new PropertyChangedEventArgs(nameof(StartButtonCommand));

        private ICommand _startButtonCommand;

        public ICommand StartButtonCommand
        {
            get { return this._startButtonCommand; }
            set
            {
                if (this._startButtonCommand == value) { return; }
                this._startButtonCommand = value;
                this.PropertyChanged?.Invoke(this, StartButtonCommandPropertyChangedEventArgs);
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
        private async void Button_Click()
        {
            ResultList.Clear();

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
                ResultList.Add("コントローラが接続されていません。");
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedSerialPortName))
            {
                ResultList.Add("COMポートが選択されていません。");
                return;
            }

            await Task.Run(() =>
            {
                SerialPort port = null;
                try
                {
                    port = new SerialPort(SelectedSerialPortName, 115200);
                    port.Open();

                    Stopwatch stopwatch = new Stopwatch();
                    List<double> latencys = new List<double>(TryTimes);
                    for (int i = 0; i < TryTimes; i++)
                    {
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

                            if (stopwatch.ElapsedMilliseconds > 1000)
                            {
                                //1秒でタイムアウト
                                lock (_listBoxLock)
                                {
                                    ResultList.Add("ボタン入力がありませんでした。");
                                }
                                stopwatch.Stop();
                                return;
                            }
                        }

                        port.Write(new byte[1] { 0 }, 0, 1);

                        //結果出力
                        double elapsed = stopwatch.Elapsed.TotalMilliseconds;
                        latencys.Add(elapsed);
                        lock (_listBoxLock)
                        {
                            ResultList.Add($"{i + 1}回目　{elapsed:F2} ミリ秒");
                        }
                        Thread.Sleep(100);
                    }

                    lock (_listBoxLock)
                    {
                        ResultList.Add("");
                        ResultList.Add($"平均:{latencys.Average():F2} 最小:{latencys.Min():F2} 最大:{latencys.Max():F2}");
                    }
                }
                finally
                {
                    if (port != null)
                    {
                        port.Write(new byte[1] { 0 }, 0, 1);
                        port.Dispose();
                    }
                }
            });
        }

    }
}

#region コマンドクラス

/// <summary>
/// プリズムのコードを参考に、デリゲートコマンドを作成。
/// </summary>
class DelegateCommand : ICommand
{
    public event EventHandler CanExecuteChanged;

    public DelegateCommand(Action executeMethod)
    {
        ExecuteMethod = executeMethod;
    }

    public bool CanExecute(object parameter)
    {
        return true;
    }

    public void Execute(object parameter)
    {
        ExecuteMethod();
    }

    private Action ExecuteMethod { get; set; }
}

#endregion

