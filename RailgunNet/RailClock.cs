/*
 *  RailgunNet - A Client/Server Network State-Synchronization Layer for Games
 *  Copyright (c) 2016-2018 - Alexander Shoulson - http://ashoulson.com
 *
 *  This software is provided 'as-is', without any express or implied
 *  warranty. In no event will the authors be held liable for any damages
 *  arising from the use of this software.
 *  Permission is granted to anyone to use this software for any purpose,
 *  including commercial applications, and to alter it and redistribute it
 *  freely, subject to the following restrictions:
 *  
 *  1. The origin of this software must not be misrepresented; you must not
 *     claim that you wrote the original software. If you use this software
 *     in a product, an acknowledgment in the product documentation would be
 *     appreciated but is not required.
 *  2. Altered source versions must be plainly marked as such, and must not be
 *     misrepresented as being the original software.
 *  3. This notice may not be removed or altered from any source distribution.
 */

using RailgunNet.System.Types;

namespace RailgunNet
{
    /// <summary>
    ///     Used for keeping track of the remote peer's clock.
    /// </summary>
    public class RailClock
    {
        private const int DELAY_MIN = 3;
        private const int DELAY_MAX = 9;
        private readonly int delayDesired;
        private readonly int delayMax;
        private readonly int delayMin;

        private readonly int remoteRate;

        private bool shouldUpdateEstimate;

        public RailClock(int remoteSendRate, int delayMin = DELAY_MIN, int delayMax = DELAY_MAX)
        {
            remoteRate = remoteSendRate;
            EstimatedRemote = Tick.INVALID;
            LatestRemote = Tick.INVALID;

            this.delayMin = delayMin;
            this.delayMax = delayMax;
            delayDesired = (delayMax - delayMin) / 2 + delayMin;

            shouldUpdateEstimate = false;
            ShouldTick = false;
        }

        private bool ShouldTick { get; set; }
        public Tick EstimatedRemote { get; private set; }
        public Tick LatestRemote { get; private set; }

        public void UpdateLatest(Tick latestTick)
        {
            if (LatestRemote.IsValid == false) LatestRemote = latestTick;
            if (EstimatedRemote.IsValid == false)
            {
                EstimatedRemote = Tick.Subtract(LatestRemote, delayDesired);
            }

            if (latestTick > LatestRemote)
            {
                LatestRemote = latestTick;
                shouldUpdateEstimate = true;
                ShouldTick = true;
            }
        }

        // See http://www.gamedev.net/topic/652186-de-jitter-buffer-on-both-the-client-and-server/
        public void Update()
        {
            if (ShouldTick == false) return; // 0;

            EstimatedRemote = EstimatedRemote + 1;
            if (shouldUpdateEstimate == false) return; // 1;

            int delta = LatestRemote - EstimatedRemote;

            if (ShouldSnapTick(delta))
            {
                // Reset
                EstimatedRemote = LatestRemote - delayDesired;
                return; // 0;
            }

            if (delta > delayMax)
            {
                // Jump 1
                EstimatedRemote = EstimatedRemote + 1;
                return; // 2;
            }

            if (delta < delayMin)
            {
                // Stall 1
                EstimatedRemote = EstimatedRemote - 1;
                return; // 0;
            }

            shouldUpdateEstimate = false;
        }

        private bool ShouldSnapTick(float delta)
        {
            if (delta < delayMin - remoteRate) return true;
            if (delta > delayMax + remoteRate) return true;
            return false;
        }
    }
}
