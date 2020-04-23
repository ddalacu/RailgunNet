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

namespace Railgun
{
    /// <summary>
    /// Used for keeping track of the remote peer's clock.
    /// </summary>
    public class RailClock
    {
        public bool ShouldTick { get; private set; }
        public Tick EstimatedRemote { get; private set; }
        public Tick LatestRemote { get; private set; }

        private const int DELAY_MIN = 3;
        private const int DELAY_MAX = 9;

        private readonly int remoteRate;
        private readonly int delayDesired;
        private readonly int delayMin;
        private readonly int delayMax;

        private bool shouldUpdateEstimate;

        public RailClock(
          int remoteSendRate,
          int delayMin = RailClock.DELAY_MIN,
          int delayMax = RailClock.DELAY_MAX)
        {
            this.remoteRate = remoteSendRate;
            this.EstimatedRemote = Tick.INVALID;
            this.LatestRemote = Tick.INVALID;

            this.delayMin = delayMin;
            this.delayMax = delayMax;
            this.delayDesired = ((delayMax - delayMin) / 2) + delayMin;

            this.shouldUpdateEstimate = false;
            this.ShouldTick = false;
        }

        public void UpdateLatest(Tick latestTick)
        {
            if (this.LatestRemote.IsValid == false)
                this.LatestRemote = latestTick;
            if (this.EstimatedRemote.IsValid == false)
                this.EstimatedRemote = Tick.Subtract(this.LatestRemote, this.delayDesired);

            if (latestTick > this.LatestRemote)
            {
                this.LatestRemote = latestTick;
                this.shouldUpdateEstimate = true;
                this.ShouldTick = true;
            }
        }

        // See http://www.gamedev.net/topic/652186-de-jitter-buffer-on-both-the-client-and-server/
        public void Update()
        {
            if (this.ShouldTick == false)
                return; // 0;

            this.EstimatedRemote = this.EstimatedRemote + 1;
            if (this.shouldUpdateEstimate == false)
                return; // 1;

            int delta = this.LatestRemote - this.EstimatedRemote;

            if (this.ShouldSnapTick(delta))
            {
                // Reset
                this.EstimatedRemote = this.LatestRemote - this.delayDesired;
                return; // 0;
            }
            else if (delta > this.delayMax)
            {
                // Jump 1
                this.EstimatedRemote = this.EstimatedRemote + 1;
                return; // 2;
            }
            else if (delta < this.delayMin)
            {
                // Stall 1
                this.EstimatedRemote = this.EstimatedRemote - 1;
                return; // 0;
            }

            this.shouldUpdateEstimate = false;
            return; // 1;
        }

        private bool ShouldSnapTick(float delta)
        {
            if (delta < (this.delayMin - this.remoteRate))
                return true;
            if (delta > (this.delayMax + this.remoteRate))
                return true;
            return false;
        }
    }
}
