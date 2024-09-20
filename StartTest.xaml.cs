using EDNA_App.Helpers.Camera;
using EDNA_App.Helpers.Impl;
using EDNA_App.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace EDNA_App.View
{
    /// <summary>
    /// Interaction logic for StartTest.xaml
    /// </summary>
    public partial class StartTest : Page
    {
        DispatcherTimer _timer;
        TimeSpan _time;
        private double testTime = 360;
        private double clockBackTime = 10;
        List<string> fileData = new List<string>();
        private bool isTestFinished = false;
        private bool isTestPaused = false;
        List<(int, int, string, byte[])> frames = new List<(int, int, string, byte[])>();
        private int frameCount = 25;
        private int frameId = 1024;
        private bool isTestStarted = false;
        private KinectModelService kinectModelService;
        private K4HelperService k4HelperService;

        public StartTest()
        {
            InitializeComponent();
            Global.IsBackButtonClicked = false;
            PlayTest.IsEnabled = true;
            if (Global.ElapsedTime <= 0)
            {
                isTestStarted = false;
                StartTestPanel.Visibility = Visibility.Visible;
                PauseTestPanel.Visibility = Visibility.Collapsed;
                TimerText.FontSize = 10;
                TimerText.FontWeight = FontWeights.Bold;
            }
            else
            {
                StartPatientTest();
            }
        }
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (PauseTestPanel.Visibility == Visibility.Visible)
                {
                    Global.IsBackButtonClicked = true;
                    this.TerminateTest_Click(sender, e);
                }
                else
                {
                    Uri uri = new Uri("/View/TestInstruction.xaml", UriKind.Relative);
                    this.NavigationService.Navigate(uri);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private async void StartExam_Click(object sender, RoutedEventArgs e)
        {
            var cDrive = DriveInfo.GetDrives().FirstOrDefault(x => x.Name == @"C:\");
            if (cDrive.IsReady && cDrive.DriveType == DriveType.Fixed)
            {
                double driveSize = Math.Round(cDrive.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0, 2);
                if(driveSize < 35)
                {
                    MessageBox.Show("System has low disk space. The minimum available storage should be 35 GB.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            TestStarted.Visibility = Visibility.Visible;
            PlayTest.IsEnabled = false;
            await StartPatientTest();
        }
        public bool DevicesConnected()
        {
            Global.IsTekscanSensor = false;
            Global.IsAngelEyeCamera = false;
            Global.IsAzureDkCamera = false;

            //Determine which sensors are connected 
            TakescanApiService takeScanApiService = new TakescanApiService();
            if (!string.IsNullOrEmpty(Global.TekscanSensorSerialNumber))
            {
                takeScanApiService.ReleaseSensor(Global.TekscanSensorSerialNumber);
            }

            string sensorSerialNumber = takeScanApiService.GetSensorIntializationAndSensorId();
            if (!string.IsNullOrEmpty(sensorSerialNumber))
            {
                Global.IsTekscanSensor = true;
            }
            return Global.IsTekscanSensor || Global.IsAngelEyeCamera || Global.IsAngelEyeCamera;
        }

        public async Task StartPatientTest()
        {
            try
            {
                isTestPaused = false;
                if (Global.IsAzureDkCamera && Global.IsTekscanSensor)  //need a mat to run the test 
                {
                    k4HelperService = new K4HelperService();
                    Global.K4HelperService = k4HelperService;
                    k4HelperService.SaveCapture();
                }
                if (Global.IsTekscanSensor)
                {
                    TakescanApiService takeScanApiService = new TakescanApiService();
                    if (!string.IsNullOrEmpty(Global.TekscanSensorSerialNumber))
                    {
                        takeScanApiService.ReleaseSensor(Global.TekscanSensorSerialNumber);
                    }
                    string sensorSerialNumber = takeScanApiService.GetSensorIntializationAndSensorId();
                    if (!string.IsNullOrEmpty(sensorSerialNumber))
                    {
                        logoutStack.Visibility = Visibility.Collapsed;
                        backtopreviousstack.Visibility = Visibility.Collapsed;
                        Global.TekscanSensorSerialNumber = sensorSerialNumber;

                        _time = TimeSpan.FromSeconds(Global.ElapsedTime);

                        _timer = new DispatcherTimer(new TimeSpan(0, 0, 1), DispatcherPriority.Normal, delegate
                        {
                            TimerText.Text = _time.ToString(@"mm\:ss");
                            TimerText.FontSize = 50;
                            TimerText.FontWeight = FontWeights.Normal;
                            TimerText.HorizontalAlignment= HorizontalAlignment.Center;
                            TimerText.VerticalAlignment = VerticalAlignment.Center;
                            StartTestPanel.Visibility = Visibility.Collapsed;
                            PauseTestPanel.Visibility = Visibility.Visible;
                            PlayTest.IsEnabled = true;
                            RestartTest.Visibility = Visibility.Collapsed;

                            frameId = frameId + frameCount;
                            ////var newFrames = takeScanApiService.GetFrames(frameId, frameCount, sensorSerialNumber);
                            ////frames.AddRange(newFrames);
                            Task task1 = Task.Run(() => GetFramesFromSensorAndStoreInList(takeScanApiService, frameId));
                            if (_time >= TimeSpan.FromSeconds(testTime))
                            {
                                _timer.Stop();
                                isTestFinished = true;
                                PauseTestPanel.Visibility = Visibility.Collapsed;
                                StartTestPanel.Visibility = Visibility.Visible;

                                takeScanApiService.ReleaseSensor(sensorSerialNumber);
                                Test_Completed(null, new RoutedEventArgs());
                            }
                            _time = _time.Add(TimeSpan.FromSeconds(1));
                            ProgressBar.EndAngle = _time.TotalSeconds * 1;
                        }, Application.Current.Dispatcher);

                        if (!_timer.IsEnabled)
                        {
                            _timer.Start();
                        }
                    }
                    else
                    {
                        recordingRunningMessage.Visibility = Visibility.Collapsed;
                        MessageBox.Show("Please connect a sensor to your computer.", "No Sensor Found!", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    recordingRunningMessage.Visibility = Visibility.Collapsed;
                    MessageBox.Show("Please connect a sensor to your computer.", "No Sensor Found!", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GetFramesFromSensorAndStoreInList(TakescanApiService takeScanApiService, int frameId)
        {
            var newFrames = takeScanApiService.GetFrames(frameId, frameCount, Global.TekscanSensorSerialNumber);
            frames.AddRange(newFrames);
        }

        public void StoreTestDataToFile()
        {
            WebRequest request = WebRequest.Create(Global.SensorDataStream);
            request.BeginGetResponse(ar =>
            {
                var req = (WebRequest)ar.AsyncState;
                using (var response = req.EndGetResponse(ar))
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    while (!isTestPaused && !isTestFinished)
                    {
                        fileData.Add(reader.ReadLine());
                    }
                }
            }, request);
        }

        private void PauseTest_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TestStarted.Visibility = Visibility.Collapsed;
                isTestPaused = true;
                _timer.Stop();
                if (!Global.IsTekscanSensor && fileData.Any())
                {
                    fileData = fileData.Where(x => x.Contains("data: {")).ToList();
                    fileData = _time.TotalSeconds > 10 ? fileData.GetRange(0, fileData.Count - 300) : new List<string>();
                }
                else if (Global.IsTekscanSensor)
                {
                    if (_time.TotalSeconds > 10 && frames.Count > 0)
                    {
                        var x = frames.Count;
                        if (x > 300)
                        {
                            frames = frames.GetRange(0, frames.Count - 300);
                        }
                        else if (x > 150)
                        {
                            frames = frames.GetRange(0, frames.Count - 150);
                        }
                        else
                        {
                            frames = frames.GetRange(0, frames.Count);
                        }
                        //frames = frames.Count > 300 ? frames.GetRange(0, frames.Count - 300) : frames.GetRange(0, frames.Count - 150);
                    }
                    else
                    {
                        frames = new List<(int, int, string, byte[])>();
                    }
                }
                else
                {
                    MessageBox.Show("Please connect a sensor to your computer. If already connected please reconnect the sensor or try to restart your computer.", "No Sensor Found!", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                if (Global.IsAzureDkCamera)
                {
                    k4HelperService.PauseCamera();
                }
                RestartTest.Visibility = Visibility.Visible;
                PauseTest.Visibility = Visibility.Collapsed;

            }
            catch (Exception ex)
            {
                MessageBox.Show("Something went wrong. Please go back and restart test.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void TerminateTest_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!isTestPaused)
                {
                    this.PauseTest_Click(sender, e);
                }
                terminateTestPopup.Visibility = Visibility.Visible;
                startMat.Visibility = Visibility.Collapsed;
                backtopreviousstack.Visibility = Visibility.Collapsed;
                logoutStack.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RestartTest_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_timer.IsEnabled)
                {
                    isTestPaused = false;
                    TestStarted.Visibility = Visibility.Visible;
                    PauseTest.Visibility = Visibility.Visible;
                    RestartTest.Visibility = Visibility.Collapsed;
                    ResetCameraCapture();

                    Global.ElapsedTime = _time.TotalSeconds > 10 ? _time.TotalSeconds - clockBackTime : 0;

                    _time = TimeSpan.FromSeconds(Global.ElapsedTime);
                    _timer.Start();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void CreatePatientResultCsvFile()
        {
            if (frames.Count > 0)
            {
                string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), $"{Global.StudyId}-{Global.PatientSelectedDOB.Value.ToString("yyyyMMdd")}-{Global.ConsentTime.Value.ToString("HHmm")}.csv");
                var directory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                string newFilePath = filePath;
                if (File.Exists(filePath))
                {     
                    int count = 1;
                    while (File.Exists(newFilePath))
                    {
                        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
                        string extension = Path.GetExtension(filePath);
                        newFilePath = Path.Combine(directory, $"{fileNameWithoutExtension}({count}){extension}");
                        count++;
                    }
                }
                using (StreamWriter csv = File.AppendText(newFilePath))
                {
                    var frameList = frames.ToList();
                    foreach (var frame in frameList)
                    {
                        csv.Write($"Frame: {frame.Item1}, ID: {frame.Item2}, \"{frame.Item3}\" ");
                        try
                        {
                            if (frame.Item4 != null)
                                foreach (var data in frame.Item4)
                                {
                                    csv.Write($",{data}");
                                }
                        }
                        catch (Exception ex)
                        {
                            string sz = ex.Message;
                            //may need to log this error 
                        }
                        csv.WriteLine();
                    }
                }
            }
            else
            {
                MessageBox.Show("Please connect a sensor to your computer.", "No Sensor Found!", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Test_Completed(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Global.IsTekscanSensor)
                {
                    CreatePatientResultCsvFile();
                }
                if (Global.IsAzureDkCamera)
                {
                    k4HelperService.StopCamera();
                }
                Uri uri = new Uri("/View/ResultDisplayView.xaml", UriKind.Relative);
                this.NavigationService.Navigate(uri);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void logout_checked(object sender, RoutedEventArgs e)
        {
            try
            {
                LogoutButton.IsChecked = false;
                //home.IsChecked = true;
                Global.K4HelperService.StopCamera();
                Uri uri = new Uri("/View/LogoutPopup.xaml", UriKind.Relative);
                this.NavigationService.Navigate(uri);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Page_Unloaded(object sender, EventArgs e)
        {
            Global.ElapsedTime = _time.TotalSeconds > 10 ? _time.TotalSeconds - clockBackTime : 0;
            if (Global.IsAzureDkCamera)
            {
                Global.K4HelperService.StopCamera();
            }
        }
        private void TerminateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Global.IsAzureDkCamera)
                {
                    Global.K4HelperService.StopCamera();
                    Global.K4HelperService.DeleteCapture();
                }

                if (Global.IsBackButtonClicked)
                {
                    Uri uri = new Uri("/View/TestInstruction.xaml", UriKind.Relative);
                    this.NavigationService.Navigate(uri);
                }
                else
                {
                    Global.ElapsedTime = 0;
                    terminateTestPopup.Visibility = Visibility.Collapsed;
                    startMat.Visibility = Visibility.Visible;
                    backtopreviousstack.Visibility = Visibility.Visible;
                    PauseTestPanel.Visibility = Visibility.Collapsed;
                    StartTestPanel.Visibility = Visibility.Visible;
                    isTestPaused = false;
                    PauseTest.Visibility = Visibility.Visible;
                    logoutStack.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                RestartTest.Visibility = Visibility.Collapsed;
                PauseTest.Visibility = Visibility.Visible;

                terminateTestPopup.Visibility = Visibility.Collapsed;
                startMat.Visibility = Visibility.Visible;
                PauseTest.Visibility = Visibility.Visible;
                backtopreviousstack.Visibility = Visibility.Collapsed;
                logoutStack.Visibility = Visibility.Collapsed;
                Global.K4HelperService.RestartTest();
                isTestPaused = false;
                _timer.Start();

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RestartButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Global.ElapsedTime = 0;
                terminateTestPopup.Visibility = Visibility.Collapsed;
                startMat.Visibility = Visibility.Visible;
                backtopreviousstack.Visibility = Visibility.Collapsed;
                PauseTestPanel.Visibility = Visibility.Collapsed;
                StartTestPanel.Visibility = Visibility.Visible;
                isTestPaused = false;
                isTestStarted = false;
                isTestFinished = false;
                PlayTest.IsEnabled = true;
                PauseTest.Visibility = Visibility.Visible;
                logoutStack.Visibility = Visibility.Visible;
                //Global.ElapsedTime = _time.TotalSeconds > 10 ? _time.TotalSeconds - clockBackTime : 0;
                //_time = TimeSpan.FromSeconds(Global.ElapsedTime);
                if (Global.IsAzureDkCamera)
                {
                    Global.K4HelperService.StopCamera();
                    Global.K4HelperService.DeleteCapture();
                    Global.K4HelperService.RestartTest();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            double newWindowHeight = e.NewSize.Height;
            double newWindowWidth = e.NewSize.Width;
            var heightResolution = newWindowHeight / 800;
            var widthResolution = newWindowWidth / 1536;
            var averageResolution = (heightResolution + widthResolution) / 2;

            HeaderImage.Height = 100 * averageResolution;
            HeaderImage.Margin = new Thickness(20 * averageResolution, 2* averageResolution, 0, 0);

            step2label.FontSize = 10 * averageResolution;
            instructionslabel.FontSize = 15 * averageResolution;
            bubbleimage.Height = 26 * averageResolution;

            //MenuList.Margin = new Thickness(2 * averageResolution, 140 * averageResolution, 0, 0);
            //home.FontSize = 18 * averageResolution;
            LogoutButton.FontSize = 15 * averageResolution;
            LogoutButton.Margin = new Thickness(20 * averageResolution, 15 * averageResolution, 0, 0);

            //thingsToRemember.FontSize = 24 * averageResolution;
            TestStarted.Width = 440 * averageResolution;
            TestStarted.Height = 36 * averageResolution;
            TestStartedLabel.FontSize = 20 * averageResolution;

            OuterMat.Height = 330 * averageResolution;
            OuterMat.Width = 330 * averageResolution;

            borderhid.Height = 300 * averageResolution;
            borderhid.Width = 600 * averageResolution;
            exclamation.Height = 40 * averageResolution;
            terminatefont.FontSize = 18 * averageResolution;

            ContinueButton.Height = 40 * averageResolution;
            ContinueButton.Width = 165 * averageResolution;
            ContinueButton.FontSize = 16 * averageResolution;

            TerminateButton.Height = 40 * averageResolution;
            TerminateButton.Width = 165 * averageResolution;
            TerminateButton.FontSize = 16 * averageResolution;

            RestartButton.Height = 40 * averageResolution;
            RestartButton.Width = 165 * averageResolution;
            RestartButton.FontSize = 16 * averageResolution;

            backtopreviousstack.Margin = new Thickness(20 * averageResolution, -20 * averageResolution, 0, 0);
            backtoprevious.FontSize = 16 * averageResolution;
            backarrow.Height = 8 * averageResolution;

            footerlabel.FontSize = 14 * averageResolution;

            footerlogo.Height = 50 * averageResolution;
            footerlogo.Margin = new Thickness(0, 5 * averageResolution, 0, 0);
            research.FontSize = 14 * heightResolution;
        }

        private void ResetCameraCapture()
        {
            if (Global.IsAzureDkCamera)
            {
                Global.K4HelperService.StopCamera();
                Global.K4HelperService.DeleteCapture();
                Global.K4HelperService.RestartTest();
                Global.K4HelperService.SaveCapture();
            }
        }

    }
}
