﻿using System;
using System.Threading;

namespace Tests.Example.Tests
{
    public class UpdateThread
    {
        private readonly Action action;
        private readonly object m_StopRequestLock = new object();
        private readonly uint tickRate;

        private bool m_bStopRequest;
        private Thread m_Thread;

        public UpdateThread(Action action, uint uiTickRate = 0)
        {
            this.action = action;
            tickRate = uiTickRate;
            Start();
        }

        ~UpdateThread()
        {
            Stop();
        }

        public void Start()
        {
            startMainLoop();
        }

        public void Stop()
        {
            stopMainLoop();
        }

        private void startMainLoop()
        {
            m_Thread = new Thread(() => run());
            lock (m_StopRequestLock)
            {
                m_bStopRequest = false;
            }

            m_Thread.Start();
        }

        private void run()
        {
            FrameLimiter frameLimiter = new FrameLimiter(
                tickRate > 0 ? TimeSpan.FromMilliseconds(1000 / (double) tickRate) : TimeSpan.Zero);
            bool bRunning = true;
            while (bRunning)
            {
                if (bRunning)
                {
                    action();
                }

                frameLimiter.Throttle();
                lock (m_StopRequestLock)
                {
                    bRunning = !m_bStopRequest;
                }
            }
        }

        private void stopMainLoop()
        {
            if (m_Thread == null)
            {
                return;
            }

            lock (m_StopRequestLock)
            {
                m_bStopRequest = true;
            }

            m_Thread.Join();
            m_Thread = null;
        }
    }
}
