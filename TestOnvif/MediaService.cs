﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.ComponentModel;

namespace TestOnvif
{
    interface IMediaForm
    {
        void UpdateControls();
    }

    interface IMediaFormManager
    {
        void AddForm(IMediaForm form);
        void RemoveForm(IMediaForm form);
        void UpdateControls();
    }

    class MediaService : IDisposable, IMediaFormManager
    {
        private static MediaService service = null;
        private MediaService()  { MediaFormManager = this; }

        public static MediaService Instance
        {
            get
            {
                if (service == null)
                {
                    service = new MediaService();
                }
                return service;
            }
        }

        /// <summary>
        /// Колекция доступных ONVIF устройств
        /// </summary>
        MediaDevice[] mediaDeviceCollection;

        /// <summary>
        /// камера с которой работаем
        /// </summary>
         MediaDevice mediaDevice;

         VideoForm videoForm;
         MainForm mainForm;

         IMediaFormManager MediaFormManager = null;
         List<IMediaForm> MediaForms = new List<IMediaForm>();
 

        public MediaDevice[] MediaDeviceCollection
        {
            get { return mediaDeviceCollection; }
            set { mediaDeviceCollection = value; }
        }

        public MediaDevice MediaDevice
        {
            get { return mediaDevice; }
            private set { mediaDevice = value; }
        }


        public MainForm CreateMainForm()
        {
            mainForm =new MainForm();
            MediaFormManager.AddForm(mainForm);

            return mainForm;
        }

        private bool isConnected = false;
        private bool isStreaming = false;

        public bool IsConnected
        {
            get { return isConnected; }
            set { isConnected = value; }
        }

        public bool IsStreaming
        {
            get { return isStreaming; }
            set { isStreaming = value; }
        }

        public void FindDevices()
        {
            mediaDeviceCollection = ONVIFClient.GetAvailableMediaDevices();

            mainForm.BindMediaDeviceCollection(mediaDeviceCollection);

            MediaFormManager.UpdateControls();
        }

        public void Connect(MediaDevice device, string login, string password)
        {
            if (isConnected == false)
            {
                device.ONVIFClient.Connect(login, password);

                deviceio.Profile[] profiles = device.ONVIFClient.MediaProfiles;

                mainForm.BindMediaProfileCollection(profiles);

                mediaDevice = device;

                isConnected = true;

            }

            MediaFormManager.UpdateControls();
        }

        public void Disconnect() 
        {
            if (isConnected == true)
            {
                if (isStreaming == true)
                {
                    //...
                    Stop();
                    isConnected = false;
                }
                else
                {
                    //...
                    isConnected = false;
                }
                //...
            }

            MediaFormManager.UpdateControls();
        }


        public void Start(deviceio.Profile profile)
        {
            if (isConnected == true && isStreaming == false)
            {
                mediaDevice.ONVIFClient.CurrentMediaProfile = profile;
                isStreaming = mediaDevice.StartMedia();

                string uri = mediaDevice.ONVIFClient.GetCurrentMediaProfileRtspStreamUri().AbsoluteUri;
                string filename = mediaDevice.AVProcessor.FFmpegMedia.OutputFilename;

                int width = mediaDevice.AVProcessor.InVideoParams.Width;
                int height = mediaDevice.AVProcessor.InVideoParams.Height;

                videoForm = new VideoForm(uri, filename, width, height);
                if (videoForm != null)
                {
                    mediaDevice.AVProcessor.ShowVideo += videoForm.ShowVideo;
                    mediaDevice.AVProcessor.PlayAudio += videoForm.PlayAudio;

                    MediaFormManager.AddForm(videoForm);

                    videoForm.Show();
                }
                MediaFormManager.UpdateControls();

            }
        }

        public void Stop()
        {
            if (isConnected == true && isStreaming == true)
            {
                mediaDevice.StopMedia();
                isStreaming = false;

                if (videoForm != null)
                {
                    mediaDevice.AVProcessor.ShowVideo -= videoForm.ShowVideo;
                    mediaDevice.AVProcessor.PlayAudio -= videoForm.PlayAudio;

                    videoForm.Close();
                    MediaFormManager.RemoveForm(videoForm);
                };

                MediaFormManager.UpdateControls();
                
            }
        }

        public void AddForm(IMediaForm form)
        {
            MediaForms.Add(form);
        }

        public void RemoveForm(IMediaForm form)
        {
            MediaForms.Remove(form);
        }

        public void UpdateControls()
        {
            foreach (IMediaForm form in MediaForms)
            {
                form.UpdateControls();
            }
        }

        public bool ExecuteCommand(string command)
        {
            if (IsConnected == true)
            {
                switch (command)
                {
                    case "GetDeviceInformation":
                        {
                            MessageBox.Show(mediaDevice.ONVIFClient.GetDeviceInformation());
                            break;
                        }
                    case "GetSystemDateAndTime":
                        {
                            string nowTimeString = DateTime.Now.ToString("HH:mm:ss.fff");

                            string mediaTimeString = string.Empty;

                            DateTime? media = mediaDevice.ONVIFClient.GetSystemDateAndTime();

                            if (media != null)
                                mediaTimeString = ((DateTime)media).ToString("HH:mm:ss.fff");

                            string message = string.Format("now={0}; media={1}", nowTimeString, mediaTimeString);
                            Logger.Write(message, EnumLoggerType.Output);

                            break;
                        }
                    case "SetDateTime":
                        {
                            if (mediaDevice != null)
                            {
                                BackgroundWorker worker = new BackgroundWorker();
                                worker.DoWork += (obj, arg) =>
                                {
                                    mediaDevice.ONVIFClient.SetSystemDateAndTime(DateTime.Now);
                                };
                                worker.RunWorkerCompleted += (obj, arg) =>
                                {
                                    mainForm.Enabled = true;
                                    mainForm.Cursor = Cursors.Default;
                                };
                                worker.RunWorkerAsync();
                                mainForm.Cursor = Cursors.WaitCursor;
                                mainForm.Enabled = false;
                            }
                            break;
                        }
                    case "SetDateTimefromNtp":
                        {
                            if (mediaDevice != null)
                            {
                                BackgroundWorker worker = new BackgroundWorker();
                                worker.DoWork += (obj, arg) =>
                                {
                                    mediaDevice.ONVIFClient.SetSystemDateAndTimeNTP("192.168.10.251", "UTC-4");
                                };
                                worker.RunWorkerCompleted += (obj, arg) =>
                                {
                                    mainForm.Enabled = true;
                                    mainForm.Cursor = Cursors.Default;
                                };

                                worker.RunWorkerAsync();
                                Logger.Write(String.Format("SetSystemDateAndTimeNTP"), EnumLoggerType.DebugLog);
                                mainForm.Cursor = Cursors.WaitCursor;
                                mainForm.Enabled = false;

                            }
                            break;
                        }
                    case "MediaClientGetProfiles":
                        {
                            MediaClientProfilesForm mcpf = new MediaClientProfilesForm(mediaDevice.ONVIFClient.MediaProfiles);
                            mcpf.ShowDialog();
                            break;
                        }
                    case "GetHostname":
                        {
                            MessageBox.Show(mediaDevice.ONVIFClient.GetHostname());
                            break;
                        }
                    case "Reboot":
                        {
                            if (MessageBox.Show("Reboot will take 2 minutes\nYou are sure that want to reboot device now?", "",
                                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                            {
                                string message = mediaDevice.ONVIFClient.Reboot();
                                using (RebootForm rf = new RebootForm(message))
                                {
                                    //
                                    rf.ShowDialog();
                                }
                            }
                            break;
                        }
                    default:
                        //...
                        break;
                }
            }

            switch (command)
            {
                case "WsDicovery":
                    {
                        BackgroundWorker worker = new BackgroundWorker();

                        worker.DoWork += (s, arg) =>
                        {
                            mediaDeviceCollection = ONVIFClient.GetAvailableMediaDevices();
                        };

                        worker.RunWorkerCompleted += (s, arg) =>
                        {
                            if (mediaDeviceCollection != null)
                            {
                                DiscoverForm df = new DiscoverForm();
                                df.ShowDialog();
                            }

                            mainForm.Cursor = Cursors.Default;
                            // WsDicoveryButton.Enabled = true;

                        };

                        worker.RunWorkerAsync();
                        //WsDicoveryButton.Enabled = false;
                        mainForm.Cursor = Cursors.WaitCursor;
                        break;
                    }
                case "ShowWebForm":
                    {
                        WebForm form = new WebForm(new Uri(@"http://192.168.10.203/admin/index.html"));
                        form.Show();
                        break;
                    }
                default:
                    //...
                    break;
            }

            return true;
        }

        public void Dispose()
        {
            if (videoForm != null)
            {
                videoForm.Dispose();
                videoForm = null;
            }

            if (mainForm != null)
            {
                mainForm.Dispose();
                mainForm = null;
            }
        }

    }

}
