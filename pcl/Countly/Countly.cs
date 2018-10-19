﻿/*
Copyright (c) 2012, 2013, 2014 Countly

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
*/

using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CountlySDK.Entities;
using CountlySDK.Helpers;
using CountlySDK.Server.Responses;
using System.IO;
using PCLStorage;
using CountlySDK.CountlyCommon;

namespace CountlySDK
{
    /// <summary>
    /// This class is the public API for the Countly Windows Phone SDK.
    /// </summary>
    public class Countly : CountlyBase
    {
        //==============SINGLETON============
        //fourth version from:
        //http://csharpindepth.com/Articles/General/Singleton.aspx
        private static readonly Countly instance = new Countly();
        // Explicit static constructor to tell C# compiler
        // not to mark type as beforefieldinit    
        static Countly() { }
        internal Countly() { }
        public static Countly Instance { get { return instance; } }
        //-------------SINGLETON-----------------

        // Raised when the async session is established
        public static event EventHandler SessionStarted;

        // Update session timer
        private TimerHelper Timer;

        /// <summary>
        /// Saves collection to the storage
        /// </summary>
        /// <returns>True if success, otherwise - False</returns>
        private async Task<bool> SaveCollection<T>(List<T> collection, string path)
        {
            List<T> collection_;

            lock (sync)
            {
                collection_ = collection.ToList();
            }

            bool success = await Storage.Instance.SaveToFile<List<T>>(path, collection_);

            if (success)
            {
                if (collection_.Count != collection.Count)
                {
                    // collection was changed during saving, save it again
                    return await SaveCollection<T>(collection, path);
                }
                else
                {
                    return true;
                }
            }
            else
            {
                return false;
            }
        }

        protected override bool SaveEvents()
        {
            lock (sync)
            {
                return SaveCollection<CountlyEvent>(Events, eventsFilename).Result;
            }
        }

        protected override bool SaveSessions()
        {
            lock (sync)
            {
                return SaveCollection<SessionEvent>(Sessions, sessionsFilename).Result;
            }
        }

        protected override bool SaveExceptions()
        {
            lock (sync)
            {
                return SaveCollection<ExceptionEvent>(Exceptions, exceptionsFilename).Result;
            }
        }

        internal override bool SaveUnhandledException(ExceptionEvent exceptionEvent)
        {
            lock (sync)
            {
                //for now we treat unhandled exceptions just like regular exceptions
                Exceptions.Add(exceptionEvent);
                return SaveExceptions();
            }
        }

        protected override bool SaveUserDetails()
        {
            lock (sync)
            {
                return Storage.Instance.SaveToFile<CountlyUserDetails>(userDetailsFilename, UserDetails).Result;
            }
        }

        /// <summary>
        /// Starts Countly tracking session.
        /// Call from your App.xaml.cs Application_Launching and Application_Activated events.
        /// Must be called before other SDK methods can be used.
        /// </summary>
        /// <param name="serverUrl">URL of the Countly server to submit data to; use "https://cloud.count.ly" for Countly Cloud</param>
        /// <param name="appKey">app key for the application being tracked; find in the Countly Dashboard under Management > Applications</param>
        /// <param name="appVersion">Application version</param>
        public static async Task StartSession(string serverUrl, string appKey, string appVersion, IFileSystem fileSystem)
        {
            await Countly.Instance.StartSessionInternal(serverUrl, appKey, appVersion, fileSystem);
        }

        public async Task StartSessionInternal(string serverUrl, string appKey, string appVersion, IFileSystem fileSystem)
        {
            if (ServerUrl != null)
            {
                // session already active
                return;
            }

            if (!IsServerURLCorrect(serverUrl))
            {
                throw new ArgumentException("invalid server url");
            }

            if (!IsAppKeyCorrect(appKey))
            {
                throw new ArgumentException("invalid application key");
            }

            ServerUrl = serverUrl;
            AppKey = appKey;
            AppVersion = appVersion;

            Storage.Instance.fileSystem = fileSystem;

            lock (sync)
            {
                Events = Storage.Instance.LoadFromFile<List<CountlyEvent>>(eventsFilename).Result ?? new List<CountlyEvent>();
                Sessions = Storage.Instance.LoadFromFile<List<SessionEvent>>(sessionsFilename).Result ?? new List<SessionEvent>();
                Exceptions = Storage.Instance.LoadFromFile<List<ExceptionEvent>>(exceptionsFilename).Result ?? new List<ExceptionEvent>();                
            }

            startTime = DateTime.Now;

            SessionTimerStart();

            Metrics metrics = new Metrics(DeviceData.OS, null, null, null, null, appVersion, DeviceData.Locale);
            await AddSessionEvent(new BeginSession(AppKey, await DeviceData.GetDeviceId(), sdkVersion, metrics));

            if (null != SessionStarted)
            {
                SessionStarted(null, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Sends session duration. Called automatically each <updateInterval> seconds
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void UpdateSession(object sender, object e)
        {
            UpdateSessionInternal();
        }        

        protected override void SessionTimerStart()
        {
            Timer = new TimerHelper(UpdateSession, null, updateInterval * 1000, updateInterval * 1000);
        }

        protected override void SessionTimerStop()
        {
            if (Timer != null)
            {
                Timer.Dispose();
                Timer = null;
            }
        }
    }
}