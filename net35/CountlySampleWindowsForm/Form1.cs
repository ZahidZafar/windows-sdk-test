using CountlySDK;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading.Tasks;
using CountlySDK.Entities;

namespace CountlySampleWindowsForm
{
    public partial class Form1 : Form
    {        
        const String serverURL = "http://try.count.ly";//put your server URL here
        const String appKey = "APP_key";//put your server APP key here       

        public Form1()
        {
            InitializeComponent();
            Countly.IsLoggingEnabled = true;
        }

        private async void btnBeginSession_Click(object sender, EventArgs e)
        {
            Debug.WriteLine("Before init");

            Countly.IsLoggingEnabled = true;

            CountlyConfig countlyConfig = new CountlyConfig();
            countlyConfig.serverUrl = serverURL;
            countlyConfig.appKey = appKey;
            countlyConfig.appVersion = "123";

            await Countly.Instance.Init(countlyConfig);

            Countly.UserDetails.Custom.Add("aaa", "666");

            await Countly.Instance.SessionBegin();

            Debug.WriteLine("After init");
        }

        private async void btnEndSession_Click(object sender, EventArgs e)
        {
            await Countly.Instance.SessionEnd();
        }

        private void btnEventSimple_Click(object sender, EventArgs e)
        {
            Countly.RecordEvent("Some event");
        }

        private void btnCrash_Click(object sender, EventArgs e)
        {
            try
            {
                throw new Exception("This is some bad exception 3");
            }
            catch (Exception ex)
            {
                Dictionary<string, string> customInfo = new Dictionary<string, string>();
                customInfo.Add("customData", "importantStuff");
                Countly.RecordException(ex.Message, ex.StackTrace, customInfo);
            }
        }
    }
}
