using Microsoft.Kinect;
using OpenCvSharp.CPlusPlus;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace KinectSkeletonRecorder
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        Microsoft.Kinect.KinectSensor m_sensor;
        public MainWindow()
        {
            InitializeComponent();
            foreach (var p in KinectSensor.KinectSensors)
            {
                this.ListBox_KinectList.Items.Add("ConnectID : " + p.DeviceConnectionId + "\nUniqueID : " + p.UniqueKinectId);
            }
            if (KinectSensor.KinectSensors.Count == 0)
            {
                this.ListBox_KinectList.Items.Add("No Kinect detected. Please connect Kinect.");
            }
            else
            {
                this.ListBox_KinectList.SelectedIndex = 0;
            }
            this.Closing += MainWindow_Closing;
            this.Closed += MainWindow_Closed;
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            KinectStop();
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            KinectStop();
        }

        private void KinectStop()
        {
            if (m_sensor != null)
            {
                this.m_sensor.Stop();
                this.m_sensor.AllFramesReady -= this.Sensor_AllFramesReady;
                System.Threading.Thread.Sleep(100);
                m_sensor.ColorStream.Disable();
                m_sensor.DepthStream.Disable();
                m_sensor.SkeletonStream.Disable();

                this.m_sensor = null;
            }
        }

        private void Button_KinectStart_Click(object sender, RoutedEventArgs e)
        {
            if (this.ListBox_KinectList.SelectedIndex >= 0)
            {
                this.m_sensor = KinectSensor.KinectSensors[this.ListBox_KinectList.SelectedIndex];
                this.updateStatus();
                this.startKinect();
            }

        }

        private void startKinect()
        {
            try
            {
                InitializeKinectBuffer();
                m_sensor.ColorStream.Enable();
                m_sensor.DepthStream.Enable();
                m_sensor.SkeletonStream.Enable();
                m_sensor.AllFramesReady += Sensor_AllFramesReady;
                m_sensor.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }


        byte[] m_colorImageBuffer;
        short[] m_depthImageBufferShort;
        byte[] m_depthImageBuffer;
        Skeleton[] m_skeletonBuffer;

        WriteableBitmap m_wbColor;
        WriteableBitmap m_wbDepth;

        Int32Rect m_rect;

        public bool isREC { get; private set; }

        private void InitializeKinectBuffer()
        {
            m_colorImageBuffer = new byte[m_sensor.ColorStream.FramePixelDataLength];
            m_depthImageBufferShort = new short[m_sensor.DepthStream.FramePixelDataLength];
            m_depthImageBuffer = new byte[m_sensor.ColorStream.FramePixelDataLength];
            m_skeletonBuffer = new Skeleton[m_sensor.SkeletonStream.FrameSkeletonArrayLength];
            m_wbColor = new WriteableBitmap(m_sensor.ColorStream.FrameWidth, m_sensor.ColorStream.FrameHeight, 96, 96, PixelFormats.Bgr32, null);
            m_wbDepth = new WriteableBitmap(m_sensor.DepthStream.FrameWidth, m_sensor.DepthStream.FrameHeight, 96, 96, PixelFormats.Rgb24, null);
            m_rect = new Int32Rect(0, 0, m_sensor.ColorStream.FrameWidth, m_sensor.ColorStream.FrameHeight);
            this.Image_Color.Source = m_wbColor;
            this.Image_depth.Source = m_wbDepth;
        }

        private void Sensor_AllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            try
            {
                using (var colorFrame = e.OpenColorImageFrame())
                {
                    colorFrame.CopyPixelDataTo(m_colorImageBuffer);
                    m_wbColor.WritePixels(m_rect, m_colorImageBuffer, colorFrame.Width * colorFrame.BytesPerPixel, 0);
                }
                using (var depthFrame = e.OpenDepthImageFrame())
                {
                    depthFrame.CopyPixelDataTo(m_depthImageBufferShort);
                    for (int i = 0; i < m_depthImageBufferShort.Length; ++i)
                    {
                        m_depthImageBuffer[i * 3] = (byte)((float)m_depthImageBufferShort[i] / ushort.MaxValue * byte.MaxValue);
                        m_depthImageBuffer[i * 3 + 1] = (byte)((float)m_depthImageBufferShort[i] / ushort.MaxValue * byte.MaxValue);
                        m_depthImageBuffer[i * 3 + 2] = (byte)((float)m_depthImageBufferShort[i] / ushort.MaxValue * byte.MaxValue);
                    }
                    m_wbDepth.WritePixels(m_rect, m_depthImageBuffer, depthFrame.Width * 3, 0);
                }
                using (var skeletonFrame = e.OpenSkeletonFrame())
                {
                    if (isREC)
                    {
                        this.m_sw.Write("Time(ms),");
                        this.m_sw.Write(-(this.m_Initializetime - DateTime.Now).TotalMilliseconds);
                    }
                    int num = 0;
                    skeletonFrame.CopySkeletonDataTo(m_skeletonBuffer);
                    foreach (var p in this.m_skeletonBuffer)
                    {
                        if (p.TrackingState != SkeletonTrackingState.NotTracked)
                        {
                            ++num;
                            if (isREC)
                            {
                                this.m_sw.Write(",ID,");
                                this.m_sw.Write(p.TrackingId.ToString());
                                this.m_sw.Write(",");

                                foreach (Joint joint in p.Joints)
                                {
                                    this.m_sw.Write(joint.JointType.ToString());
                                    this.m_sw.Write(",state,");
                                    this.m_sw.Write(joint.TrackingState.ToString());
                                    this.m_sw.Write(",x,");
                                    this.m_sw.Write(joint.Position.X.ToString());
                                    this.m_sw.Write(",y,");
                                    this.m_sw.Write(joint.Position.Y.ToString());
                                    this.m_sw.Write(",z,");
                                    this.m_sw.Write(joint.Position.Z.ToString());
                                    this.m_sw.Write(",");
                                }
                            }
                        }
                    }
                    if (isREC)
                    {
                        m_sw.Write("\n");
                    }
                    this.TextBlock_SkeletonInfo.Text = "CaptureHumanNum : " + num.ToString();
                }
            }
            catch
            {

            }
        }

        private void updateStatus()
        {
            this.Status_Name.Text = this.m_sensor.ToString();
            this.Status_KinectStatus.Text = this.m_sensor.Status.ToString();
            this.Status_RecStatus.Text = isREC.ToString();
            this.TextBox_AutoIndex.Text = autoIdNumber.ToString();
        }

        int autoIdNumber = 0;
        private void CheckBox_AutoIndexNumber_Unchecked(object sender, RoutedEventArgs e)
        {
            this.TextBox_AutoIndex.Text = "";
        }

        private void CheckBox_AutoIndexNumber_Checked(object sender, RoutedEventArgs e)
        {
            this.autoIdNumber = 0;
            this.TextBox_AutoIndex.Text = autoIdNumber.ToString();
            
        }

        enum RecState
        {
            REC,
            NOTREC,
        }
        RecState m_recState;
        StreamWriter m_sw;
        DateTime m_Initializetime;
        private void Button_REC_START_Click(object sender, RoutedEventArgs e)
        {
            if (!isREC)
            {
                string pathString = this.TextBox_FolderPath.Text + @"\" + this.TextBox_FileName.Text + (CheckBox_AutoIndexNumber.IsChecked == true ? this.autoIdNumber.ToString() : "") + ".csv";
                FileInfo info = new FileInfo(pathString);
                m_Initializetime = DateTime.Now;
                m_sw = new StreamWriter(pathString);
                isREC = true;

                this.updateStatus();
            }
            else
            {
                MessageBox.Show("already started");
            }
        }

        private void Button_REC_FINISH_Click(object sender, RoutedEventArgs e)
        {
            if (isREC)
            {
                m_sw.Close();
                isREC = false;
                this.autoIdNumber++;
                this.updateStatus();
            }
            else
            {
                MessageBox.Show("already stopped");
            }
        }

        private void Button_SelectFolderPath_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog fbd = new System.Windows.Forms.FolderBrowserDialog();
            if(fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                this.TextBox_FolderPath.Text = fbd.SelectedPath;
            }
        }
    }
}
