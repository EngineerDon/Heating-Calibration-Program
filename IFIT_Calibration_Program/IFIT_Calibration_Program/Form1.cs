using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Media;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Xml.Linq;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ToolTip;


namespace IFIT_Calibration_Program
{
    public partial class Form1 : Form
    {
        private StringBuilder buffer1;
        private StringBuilder buffer2;
        private bool isRunning = false;
        private bool isLogging = false;
        private bool isPort1Connected = false; // 시리얼 포트 1 연결 상태
        private bool isPort2Connected = false; // 시리얼 포트 2 연결 상태
        private bool arduinoResponse = false; // 시리얼 포트 2 연결 상태
        private bool ifitResponse = false; // 시리얼 포트 2 연결 상태

        private double targetTemperature = 50.0; // 목표 온도
        private double currentTemperature = 0.0; //현재 온도
        private double avgTemperature = 0.0;
        private int timer_minutes = 0;
        private int timer_seconds = 0;
        private int targetTime = 20; // 목표 보정시간
        private int x_pos = 0;
        private double tempCal = 0;
        private double sumTempCal = 0;

        private List<double> rcvDataArray = new List<double>();
        private List<double> subMean = new List<double>();
        private List<double> subStd = new List<double>();
        private List<double> subSubMean = new List<double>();
        private List<double> slopList = new List<double>();

        //private List<float> rcvDataArray = new List<float>();
        //private List<float> subMean = new List<float>();
        //private List<float> subStd = new List<float>();
        //private List<float> slopList = new List<float>();
        //private List<float> subSubMean = new List<float>();

        private double rcvData;
        private double mean;
        private double std;
        private double slop;

        private int subMeanCount = 0;
        private int slopCount = 0;

        private const int MeasCount = 5;
        private const double MinTestTemp = 40;
        private const double TestSlop = 10;

        private bool autoTestMode = true;
        private double testTempVal = 42;
        private bool logMode = true;


        //private double rcvData;
        //private double mean;
        //private double std;
        //private int subMeanCount = 0;
        private int successCount = 0;
        //private int slopCount = 0;
        //private double slop;
        //private int meancount = 5;
        //private double minTestTemp = 40;
        //private double testSlop = 15;
        public Form1()
        {
            InitializeComponent();
            //FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            this.FormClosing += MainForm_FormClosing;
            LoadTargetTemperature(); // 이전 목표 온도 설정값 로드
            LoadTargetTime();
            InitializeTextBox();
            button5.BackColor = Color.Yellow;
            button10.BackColor = Color.Yellow;

            DrawTextWithBackground(pictureBox1, "ARDUINO", Color.Red);
            DrawTextWithBackground(pictureBox2, "IFIT", Color.Red);
            
            // DrawItem 이벤트 핸들러 추가
            comboBox1.DrawItem += (sender, e) =>
            {
                // 아이템 인덱스가 -1인 경우 아무것도 그리지 않음
                if (e.Index < 0) return;

                // 배경색 및 포커스 렌더링
                e.DrawBackground();
                e.DrawFocusRectangle();

                // 아이템 텍스트를 가져와서 그리기
                string itemText = comboBox1.Items[e.Index].ToString();
                e.Graphics.DrawString(itemText, e.Font, Brushes.Black, e.Bounds);
            };

            comboBox5.DrawItem += (sender, e) =>
            {
                // 아이템 인덱스가 -1인 경우 아무것도 그리지 않음
                if (e.Index < 0) return;

                // 배경색 및 포커스 렌더링
                e.DrawBackground();
                e.DrawFocusRectangle();

                // 아이템 텍스트를 가져와서 그리기
                string itemText = comboBox5.Items[e.Index].ToString();
                e.Graphics.DrawString(itemText, e.Font, Brushes.Black, e.Bounds);
            };

            //In order to visualize chart without datasets
            chart1.Series[0].Points.Add(); 
            chart1.Series[0].Points[0].IsEmpty = true;

            // 시리얼 포트 객체 초기화
            serialPort1 = new SerialPort();
            serialPort2 = new SerialPort();

            // 시리얼 포트 이벤트 핸들러 설정
            serialPort1.DataReceived += SerialPort1_DataReceived;
            serialPort2.DataReceived += SerialPort2_DataReceived;

            // 데이터 수신 버퍼 초기화
            buffer1 = new StringBuilder();
            buffer2 = new StringBuilder();
        }
        // TextBox 초기화
        private void InitializeTextBox()
        {
            textBox1.Font = new Font("Arial", 31);           // 글자 크기 설정
            textBox1.TextAlign = HorizontalAlignment.Center; // 텍스트 가운데 정렬
            textBox1.ReadOnly = true;                        // 읽기 전용 설정
            textBox1.BorderStyle = BorderStyle.FixedSingle;  // 테두리 추가
            textBox1.Text = targetTemperature.ToString("F1"); // 초기 값 설정

            textBox2.Font = new Font("Arial", 31);           // 글자 크기 설정
            textBox2.TextAlign = HorizontalAlignment.Center; // 텍스트 가운데 정렬
            textBox2.ReadOnly = true;                        // 읽기 전용 설정
            textBox2.BorderStyle = BorderStyle.FixedSingle;  // 테두리 추가
            textBox2.Text = targetTime.ToString("F0"); // 초기 값 설정

            textBox3.Font = new Font("Arial", 29);           // 글자 크기 설정
            textBox3.TextAlign = HorizontalAlignment.Center; // 텍스트 가운데 정렬
            textBox3.ReadOnly = true;                        // 읽기 전용 설정
            textBox3.BorderStyle = BorderStyle.FixedSingle;  // 테두리 추가
            textBox3.Text = currentTemperature.ToString("F1");

            textBox4.Text = "00 : 00";
            textBox4.Font = new Font("Arial", 29);           // 글자 크기 설정
            textBox4.TextAlign = HorizontalAlignment.Center; // 텍스트 가운데 정렬
            textBox4.ReadOnly = true;                        // 읽기 전용 설정
            textBox4.BorderStyle = BorderStyle.FixedSingle;  // 테두리 추가

            textBox5.Text = "0.0";
            textBox5.Font = new Font("Arial", 29);           // 글자 크기 설정
            textBox5.TextAlign = HorizontalAlignment.Center; // 텍스트 가운데 정렬
            textBox5.ReadOnly = true;                        // 읽기 전용 설정
            textBox5.BorderStyle = BorderStyle.FixedSingle;  // 테두리 추가
        }
        // TextBox에 현재 온도 업데이트
        private void UpdateTemperatureDisplay()
        {
            textBox1.Text = targetTemperature.ToString("F1");
        }
        private void UpdateTargetTimeDisplay()
        {
            textBox2.Text = targetTime.ToString("F0");
        }
        // 폼 종료 시 설정 저장
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Properties.Settings.Default.TargetTemperature = targetTemperature;
            Properties.Settings.Default.TargetTime = targetTime;
            
            Properties.Settings.Default.Save();
        }

        // 폼 로드 시 저장된 설정값 로드
        private void LoadTargetTemperature()
        {
            targetTemperature = Properties.Settings.Default.TargetTemperature;
            UpdateTemperatureDisplay();
        }
        private void LoadTargetTime()
        {
            targetTime = Properties.Settings.Default.TargetTime;
            UpdateTargetTimeDisplay();
        }
        private void ExtractDoubleValue(string input)
        {
            // "SOP,T,"로 시작하는지 확인
            if (input.StartsWith("connection"))//SOP,T
            {
                arduinoResponse = true;
            }
            else
            {
                // 정규식 패턴: SOP,T, 이후의 숫자 값 추출
                string pattern = @"^SOP,T,(-?\d+(\.\d+)?),";
                Match match = Regex.Match(input, pattern);

                if (match.Success)
                {
                    // 매칭된 그룹 중 숫자 값 반환
                    currentTemperature = double.Parse(match.Groups[1].Value);
                }
                else
                {
                    throw new FormatException("입력 문자열에서 숫자 값을 추출할 수 없습니다.");
                }

            }
        }
        // 시리얼 포트1에서 데이터 수신 시 호출되는 이벤트 핸들러
        private void SerialPort1_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string data = serialPort1.ReadExisting();
                buffer1.Append(data);  // 데이터를 버퍼에 추가

                // '\n'이 포함되어 있으면 메시지를 완전히 수신한 것으로 간주하고 처리
                if (buffer1.ToString().Contains("\n"))
                {
                    string completeMessage = buffer1.ToString();
                    Invoke((Action)(() => listBox1.Items.Add($"Port1 Received: {completeMessage}")));

                    // 자동 스크롤 기능 추가
                    listBox1.TopIndex = listBox1.Items.Count - 1;
                    try
                    {
                        ExtractDoubleValue(completeMessage);
                    }
                    catch (Exception ex)
                    {
                        //Console.WriteLine($"에러: {ex.Message}");
                        if (isRunning)
                        {
                            listBox1.Items.Add($"에러: {ex.Message}");
                        }
                    }
                    // 메시지를 처리한 후 버퍼 초기화
                    buffer1.Clear();
                }
            }
            catch (Exception ex)
            {
                Invoke((Action)(() => MessageBox.Show($"Error receiving data from Port1: {ex.Message}")));
            }
        }

        // 시리얼 포트2에서 데이터 수신 시 호출되는 이벤트 핸들러
        private void SerialPort2_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            this.Invoke(new EventHandler(MySerialReceived2));
        }
        private void MySerialReceived2(object s, EventArgs e)
        {
            string input = serialPort2.ReadLine();
            listBox2.Items.Add("Port2 Received : " + input);
            if(input == "SOP,A,\r")
            {
                ifitResponse = true;
            }

            listBox2.TopIndex = listBox2.Items.Count - 1;
        }
        private void DrawTextWithBackground(PictureBox pictureBox, string text, Color backgroundColor)
        {
            // Create a new Bitmap with the same size as the PictureBox
            Bitmap bitmap = new Bitmap(pictureBox.Width, pictureBox.Height);

            // Create a Graphics object from the Bitmap
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                // Set the background color
                g.Clear(backgroundColor);

                // Enable better text rendering
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

                // Set the font and brush
                Font font = new Font("Arial", 25);  // Font size 20
                Brush brush = Brushes.Black;        // Black color for the text

                // Calculate the size of the text
                SizeF textSize = g.MeasureString(text, font);

                // Calculate the position to center the text
                float x = (pictureBox.Width - textSize.Width) / 2;
                float y = (pictureBox.Height - textSize.Height) / 2;

                // Draw the string at the calculated position
                g.DrawString(text, font, brush, new PointF(x + 60, y));
            }

            // Set the image of the PictureBox to the Bitmap with the background and centered text
            pictureBox.Image = bitmap;
        }
        //private void DrawTextWithBackground(PictureBox pictureBox, string text, Color backgroundColor)
        //{
        //    // Create a new Bitmap with the same size as the PictureBox
        //    Bitmap bitmap = new Bitmap(pictureBox.Width, pictureBox.Height);

        //    // Create a Graphics object from the Bitmap
        //    using (Graphics g = Graphics.FromImage(bitmap))
        //    {
        //        // Set the background color
        //        g.Clear(backgroundColor);

        //        // Set the font and brush
        //        Font font = new Font("Arial", 20);  // Font size 24
        //        Brush brush = Brushes.Black;        // Black color for the text

        //        // Calculate the size of the text to center it
        //        SizeF textSize = g.MeasureString(text, font);
        //        PointF point = new PointF(
        //            (pictureBox.Width - textSize.Width) / 2, // Center horizontally
        //            (pictureBox.Height - textSize.Height) / 2 // Center vertically
        //        );

        //        // Draw the string at the calculated position
        //        g.DrawString(text, font, brush, point);
        //    }

        //    // Set the image of the PictureBox to the Bitmap with the background and centered text
        //    pictureBox.Image = bitmap;
        //}
        private void comboBox1_DropDown(object sender, EventArgs e)
        {
            this.comboBox1.Items.Clear();
            string[] serial_list = SerialPort.GetPortNames();

            foreach (string name in serial_list)
            {
                this.comboBox1.Items.Add(name);
            }
        }
        
        // 포트가 실제로 사용 가능한지 확인하는 함수
        private bool IsPortAvailable(string portName)
        {
            string[] availablePorts = SerialPort.GetPortNames();
            return availablePorts.Contains(portName);
        }
        private async void button7_Click(object sender, EventArgs e)//통신 연결하기 버튼
        {
            // 포트 이름을 사용자가 선택하도록 입력받기
            string portName = comboBox1.SelectedItem?.ToString();
            
            if (!IsPortAvailable(portName))
            {
                MessageBox.Show($"포트 {portName}는 존재하지 않거나 사용할 수 없습니다.", "포트 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                // 포트 설정 (포트 번호, 보드레이트 등)
                serialPort1.PortName = portName;
                serialPort1.BaudRate = 115200;
                serialPort1.Open();

                serialPort1.WriteLine("SOP,A,");//Arduino Start
                await Task.Delay(200);
                if(arduinoResponse == true)
                {
                    // 연결 성공 메시지 출력
                    listBox1.Items.Add($"Port1 connected to {portName}.");
                    comboBox1.Enabled = false;  //COM포트설정 콤보박스 비활성화
                    DrawTextWithBackground(pictureBox1, "ARDUINO", Color.Blue);
                    isPort1Connected = true; // Port1 연결 상태 업데이트
                }
                else
                {
                    listBox1.Items.Add($"Port1 not connected to {portName}.");
                    serialPort1.Close();
                }

            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show($"포트 {portName}에 접근할 수 없습니다. 다른 프로그램에서 사용 중일 수 있습니다.", "접근 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Port1 연결 중 오류 발생: {ex.Message}", "연결 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private async void button8_Click(object sender, EventArgs e)
        {
            try
            {
                serialPort1.WriteLine("SOP,Z,");//Arduino disconnection
                await Task.Delay(200);
                serialPort1.Close();
                listBox1.Items.Add("Port1 disconnected.");
                comboBox1.Enabled = true;
                DrawTextWithBackground(pictureBox1, "ARDUINO", Color.Red);
                isPort1Connected = false; // Port1 연결 해제 상태 업데이트
                arduinoResponse = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Port1 해제 중 오류 발생: {ex.Message}", "해제 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void comboBox5_DropDown(object sender, EventArgs e)
        {
            this.comboBox5.Items.Clear();
            string[] serial_list = SerialPort.GetPortNames();

            foreach (string name in serial_list)
            {
                this.comboBox5.Items.Add(name);
            }
        }

        private async void button6_Click(object sender, EventArgs e)
        {
            // 포트 이름을 사용자가 선택하도록 입력받기
            string portName = comboBox5.SelectedItem?.ToString();

            if (!IsPortAvailable(portName))
            {
                MessageBox.Show($"포트 {portName}는 존재하지 않거나 사용할 수 없습니다.", "포트 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                // 포트 설정 (포트 번호, 보드레이트 등)
                serialPort2.PortName = portName;
                serialPort2.BaudRate = 9600;
                serialPort2.Open();
                serialPort2.Write("SOP,A,\r");//IFIT Response
                listBox2.Items.Add("Sent: SOP,A,\\r");
                await Task.Delay(200);
                if (ifitResponse)
                {
                    listBox2.Items.Add($"Port2 connected to {portName}.");
                    comboBox5.Enabled = false;  //COM포트설정 콤보박스 비활성화
                    DrawTextWithBackground(pictureBox2, "IFIT", Color.Blue);
                    isPort2Connected = true; // Port2 연결 상태 업데이트
                }
                else
                {
                    listBox2.Items.Add($"Port2 not connected to {portName}.");
                    serialPort2.Close();
                }

            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show($"포트 {portName}에 접근할 수 없습니다. 다른 프로그램에서 사용 중일 수 있습니다.", "접근 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Port2 연결 중 오류 발생: {ex.Message}", "연결 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button9_Click(object sender, EventArgs e)
        {
            try
            {
                serialPort2.Close();
                listBox2.Items.Add("Port2 disconnected.");
                comboBox5.Enabled = true;
                DrawTextWithBackground(pictureBox2, "IFIT", Color.Red);
                isPort2Connected = false; // Port1 연결 해제 상태 업데이트
                ifitResponse = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Port2 해제 중 오류 발생: {ex.Message}", "해제 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void button5_Click(object sender, EventArgs e)
        {
            // 두 포트가 모두 연결되어 있는지 확인
            if (!isPort1Connected || !isPort2Connected)
            {
                MessageBox.Show("두 포트 모두 연결된 상태에서만 검사를 시작할 수 있습니다.", "포트 연결 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return; // 두 포트가 모두 연결되지 않으면 타이머 시작/정지하지 않음
            }

            // 버튼 클릭 시 타이머 시작/정지
            if (isRunning)
            {
                timer1.Stop(); // 타이머 정지

                button5.Text = "검사 시작"; // 버튼 텍스트 변경
                button5.BackColor = Color.Yellow;
                
                if (serialPort1.IsOpen)
                {
                    serialPort1.WriteLine("SOP,G,");//(Arduino)Send Stop
                    listBox1.Items.Add("Sent: Send Stop");
                }

                if (serialPort2.IsOpen)
                {
                    serialPort2.Write("SOP,S,\r");//(Ifit)Heat off 
                    listBox2.Items.Add("Sent: Heat Off");
                }
            }
            else
            {
                ClearData();
                timer_minutes = 0;
                timer_seconds = 0;
                timer1.Start(); // 타이머 시작
                button5.Text = "정지"; // 버튼 텍스트 변경
                button5.BackColor = Color.Blue;
                if (serialPort1.IsOpen)
                {
                    serialPort1.WriteLine("SOP,F,");//(Arduino)Send start
                    listBox1.Items.Add("Sent: Send Start"); 
                }

                if(serialPort2.IsOpen)
                {
                    serialPort2.Write("SOP,R,\r");//(Ifit)Heat on 
                    listBox2.Items.Add("Sent: Heat On");
                }
            }
            isRunning = !isRunning; // 상태 반전
        }

        private void button1_Click(object sender, EventArgs e)
        {
            targetTemperature += 1.0;
            UpdateTemperatureDisplay();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            targetTemperature -= 0.1;
            UpdateTemperatureDisplay();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            targetTime += 1;
            UpdateTargetTimeDisplay();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            targetTime -= 1;
            UpdateTargetTimeDisplay();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            TemperatureProcess(currentTemperature);

            textBox4.Text = timer_minutes.ToString("D2") + " : " + timer_seconds.ToString("D2");
            //Time Update
            timer_seconds++;
            if (timer_seconds > 60)
            {
                timer_seconds = 0;
                timer_minutes++;
                if (timer_minutes == targetTime) { 
                    timer1.Stop(); // 타이머 정지

                    button5.Text = "검사 시작"; // 버튼 텍스트 변경
                    button5.BackColor = Color.Yellow;
                    isRunning = false;

                    if (serialPort1.IsOpen)
                    {
                        serialPort1.WriteLine("SOP,G,");//(Arduino)Send Stop
                        listBox1.Items.Add("Sent: Send Stop");
                    }

                    if (serialPort2.IsOpen)
                    {
                        serialPort2.Write("SOP,S,\r");//(Ifit)Heat off 
                        listBox2.Items.Add("Sent: Heat Off");
                    }
                    ShowErrorMessage("검사오류","검사시간 초과되었습니다");
                }
            }

            //Current Temperature Update
            textBox3.Text = currentTemperature.ToString("F1");

            //Chart Update
            chart1.Series[0].Points.AddXY(x_pos, currentTemperature);
            x_pos++;

        }


        // Clear method
        private void ClearData()
        {
            // 리스트 초기화
            rcvDataArray.Clear();
            subMean.Clear();
            subStd.Clear();
            subSubMean.Clear();
            slopList.Clear();

            // 변수 초기화
            rcvData = 0;
            mean = 0;
            std = 0;
            subMeanCount = 0;
            successCount = 0;
            slopCount = 0;
            slop = 0;
            tempCal = 0;
            sumTempCal = 0;

            // 차트의 모든 시리즈 지우기
            chart1.Series[0].Points.Clear();
            x_pos = 0;  // x_pos를 초기화하여 다시 0부터 시작하도록 설정
        }

        public void TemperatureProcess(double sd)
        {
            rcvData = sd;
            rcvDataArray.Add(rcvData);
            double[] dataArray = rcvDataArray.ToArray();

            // 최근 2분(120개) 온도 평균 계산
            if (rcvDataArray.Count >= 120)
            {
                var lastTwoMinutesData = rcvDataArray.Skip(Math.Max(0, rcvDataArray.Count - 120)).ToList();
                double averageLastTwoMinutes = Math.Round(lastTwoMinutesData.Average(), 2);
                avgTemperature = averageLastTwoMinutes;
                textBox5.Text = averageLastTwoMinutes.ToString("F1");
            }
            else
            {
                textBox5.Text = "0.0";
            }
            if (dataArray.Length >= 30)
            {
                mean = (float)Math.Round(dataArray.Skip(Math.Max(0, dataArray.Length - 30)).Average(), 2);
                std = Math.Round(StandardDeviation(dataArray.Skip(Math.Max(0, dataArray.Length - 30)).ToList()), 2);

                subMean.Add(mean);
                subStd.Add(std);

                if (subMeanCount > MeasCount)
                {
                    double avgMean = subMean.Skip(Math.Max(0, subMean.Count - MeasCount)).Average();

                    subSubMean.Add(avgMean);

                    if (subSubMean.Count > 1)
                    {
                        double lastMean = subSubMean[subSubMean.Count - 1];
                        double prevMean = subSubMean[subSubMean.Count - 2];
                        slop = (lastMean - prevMean) / MeasCount;
                        slopList.Add((double)Math.Round(slop * 1000, 2));
                        double[] slopArray = slopList.ToArray();
                        // 수신된 온도 데이터, 평균값, 표준편차, 기울기 출력
                        listBox1.Items.Add($"Slope:{slop},slopeCnt:{slopCount}");

                        if (slopCount > 6 && 
                            Math.Abs(slopArray.Skip(Math.Max(0, slopArray.Length - 6)).Take(6).Average()) < TestSlop &&
                            mean > MinTestTemp)
                        {
                            System.Media.SystemSounds.Asterisk.Play();

                            if (Math.Abs(targetTemperature - mean) <= 1 && std <= 0.7)
                                TestSuccess();
                            else
                                SendCalCode();

                            slopCount = 0;
                        }
                        slopCount++;
                    }
                    subMeanCount = 0;
                }
                subMeanCount++;
            }
            else
            {
                subMean.Add((double)Math.Round(dataArray.Average(), 2));
                subStd.Add((double)Math.Round(StandardDeviation(dataArray), 2));
            }
        }
        private void TestSuccess()
        {
            successCount++;

            if (successCount >= 3)
            {
                if (isLogging)
                {
                    SaveCalibrationResult(targetTemperature, avgTemperature, std, slop, sumTempCal);
                }

                successCount = 0;
                StopSendingData();
                StopTimer();
                PlayAlarmSound();

                serialPort2.Write("SOP,O,\r");//IFIT End calibration

                ////////////////////
                button5.Text = "검사 시작"; // 버튼 텍스트 변경
                button5.BackColor = Color.Yellow;

                serialPort2.Close();
                listBox2.Items.Add("Port2 disconnected.");
                comboBox5.Enabled = true;
                DrawTextWithBackground(pictureBox2, "IFIT", Color.Red);
                isPort2Connected = false; // Port1 연결 해제 상태 업데이트
                ifitResponse = false;

                isRunning = false;
                string displayTime = timer_minutes + " 분 " + timer_seconds + " 초 ";
                ShowSuccessMessage("검사완료", "검사가 완료되었습니다.", displayTime, avgTemperature, sumTempCal);

            }
        }

        private void SendCalCode()
        {
            // 보정 값 계산
            tempCal = Math.Round(Math.Abs(targetTemperature - mean) / 0.7); // * 2

            if (targetTemperature > mean)
            {
                serialPort2.Write($"SOP,+,{tempCal},\r");
                sumTempCal += tempCal;
            }
            else
            {
                serialPort2.Write($"SOP,-,{tempCal},\r");
                sumTempCal -= tempCal;
            }
        }
        // Stop sending data
        private void StopSendingData()
        {
            // Implementation of sending stop data
            serialPort1.WriteLine("SOP,G,");//(Arduino)Send Stop
            listBox1.Items.Add("Sent: Send Stop");
        }

        // Stop the timer
        private void StopTimer()
        {
            timer1.Stop();
        }

        // Play alarm sound
        private void PlayAlarmSound()
        {
            SystemSounds.Beep.Play();
        }

        // Show error message
        private void ShowErrorMessage(string title, string message)
        {
            MessageBox.Show($"{message}", title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        // Show success message
        private void ShowSuccessMessage(string title, string message, string displayTime, double mean, double tempCal)
        {
            DialogResult result = MessageBox.Show($"검사결과\n   - 평균온도\t\t : {mean} 도\n   - 경과 시간(분)\t\t : {displayTime}\n   - 보정값\t\t : {tempCal}\n{message}",
                            title,
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);

            // 확인 버튼 클릭 시 사용자 지정 소리 재생
            if (result == DialogResult.OK)
            {
                PlayAlarmSound();
            }
        }

        private double StandardDeviation(IEnumerable<double> values)
        {
            double avg = values.Average();
            return Math.Sqrt(values.Average(v => Math.Pow(v - avg, 2)));
        }

        // 온도 보정 결과 저장 메서드
        private void SaveCalibrationResult(double targetTemperature, double mean, double std, double slope, double calResult)
        {
            // 시스템 날짜 기반 파일 이름 생성
            string fileName = DateTime.Now.ToString("yyyyMMdd") + "_CalibrationResults.txt";
            string filePath = Path.Combine(Environment.CurrentDirectory, fileName);

            // 저장할 내용 준비
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string result = $"시간: {timestamp}, 목표 온도: {targetTemperature:F2}, 평균 온도: {mean:F2}, 표준편차: {std:F2}, 기울기: {slope:F2}, 온도보정 값 : {calResult:F2}";

            try
            {
                // 텍스트 파일에 결과 쓰기
                File.AppendAllText(filePath, result + Environment.NewLine);
                listBox1.Items.Add($"결과가 {fileName}에 저장되었습니다!");
            }
            catch (Exception ex)
            {
                listBox1.Items.Add($"파일 저장 실패: {ex.Message}");
            }
        }


        private void timer2_Tick(object sender, EventArgs e)
        {
            if (isPort1Connected)
            {
                if (serialPort1 != null && serialPort1.IsOpen)
                {
                    //listBox1.Items.Add($"{DateTime.Now}: 연결 상태 양호");
                    //do nothing
                }
                else
                {
                    listBox1.Items.Add($"{DateTime.Now}: 시리얼 포트 연결 끊김!");
                    comboBox1.Enabled = true;
                    DrawTextWithBackground(pictureBox1, "ARDUINO", Color.Red);
                    isPort1Connected = false; // Port1 연결 해제 상태 업데이트
                    arduinoResponse = false;

                    timer1.Stop(); // 타이머 정지
                    button5.Text = "검사 시작"; // 버튼 텍스트 변경
                    button5.BackColor = Color.Yellow;
                    isRunning = false;

                    if (serialPort2.IsOpen)
                    {
                        serialPort2.Write("SOP,S,\r");//(Ifit)Heat off 
                        listBox2.Items.Add("Sent: Heat Off");
                    }

                }
            }
            else//port1 disconnected
            {
                //do nothing
            }

            if (isPort2Connected)
            {
                if (serialPort2 != null && serialPort2.IsOpen)
                {
                    //listBox2.Items.Add($"{DateTime.Now}: 연결 상태 양호");
                    //do nothing
                }
                else
                {
                    listBox2.Items.Add($"{DateTime.Now}: 시리얼 포트 연결 끊김!");
                    comboBox5.Enabled = true;
                    DrawTextWithBackground(pictureBox2, "IFIT", Color.Red);
                    isPort2Connected = false; // Port1 연결 해제 상태 업데이트
                    ifitResponse = false;

                    timer1.Stop(); // 타이머 정지
                    button5.Text = "검사 시작"; // 버튼 텍스트 변경
                    button5.BackColor = Color.Yellow;
                    isRunning = false;

                    if (serialPort1.IsOpen)
                    {
                        serialPort1.WriteLine("SOP,G,");//(Arduino)Send Stop
                        listBox1.Items.Add("Sent: Send Stop");
                    }
                }
            }
            else//port1 disconnected
            {
                //do nothing
            }
        }

        private void button10_Click(object sender, EventArgs e)
        {
            if(isLogging)
            {
                button10.Text = "로깅 시작"; // 버튼 텍스트 변경
                button10.BackColor = Color.Yellow;
            }
            else
            {
                button10.Text = "로깅 중지"; // 버튼 텍스트 변경
                button10.BackColor = Color.Blue;
            }
            isLogging = !isLogging;
        }

        private void comboBox1_MouseClick(object sender, MouseEventArgs e)
        {
            comboBox1.DroppedDown = true; // 클릭 시 드롭다운 열기
        }

        private void comboBox5_MouseClick(object sender, MouseEventArgs e)
        {
            comboBox5.DroppedDown = true; // 클릭 시 드롭다운 열기
        }
    }
}
