using System;
using RailgunNet;
using RailgunNet.System.Types;
using Xunit;

namespace Tests
{
    public class RailClockTest
    {
        private Tick m_TickRemote = Tick.START;

        [Theory]
        [InlineData(0, 5)]
        [InlineData(1, 10)]
        [InlineData(2, 20)]
        [InlineData(2, 256)]
        [InlineData(10, 100)]
        private void EstimateCatchesUpToDesiredDelay(uint uiDelayMin, uint uiDelayMax)
        {
            // Init
            RailClock clock = new RailClock(
                1,
                RailClock.EDesiredDelay.Minimal,
                uiDelayMin,
                uiDelayMax);
            Assert.Equal(uiDelayMin, clock.DelayDesired);
            clock.UpdateLatest(m_TickRemote);
            clock.UpdateLatest(m_TickRemote += clock.DelayDesired + 1);
            clock.Update();
            Assert.Equal(m_TickRemote, clock.LatestRemote);
            Assert.Equal(m_TickRemote - clock.DelayDesired, clock.EstimatedRemote);

            clock.UpdateLatest(m_TickRemote += uiDelayMax - uiDelayMin);
            clock.Update();
            Assert.Equal(m_TickRemote, clock.LatestRemote);
            int delta = m_TickRemote - clock.EstimatedRemote;
            Assert.True(delta >= 2 && delta <= uiDelayMax);

            int newDelta = m_TickRemote - clock.EstimatedRemote;
            int iNumberOfStepsToDesiredResult = 0;
            while (m_TickRemote != clock.EstimatedRemote + clock.DelayDesired)
            {
                Assert.True(m_TickRemote >= clock.EstimatedRemote);
                clock.UpdateLatest(++m_TickRemote);
                clock.Update();
                delta = newDelta;
                newDelta = m_TickRemote - clock.EstimatedRemote;
                int stepSize = delta - newDelta;
                Assert.True(stepSize > 0);
                ++iNumberOfStepsToDesiredResult;
            }

            int expectedNumberOfSteps = (int) Math.Log2(uiDelayMax - uiDelayMin);
            Assert.Equal(expectedNumberOfSteps, iNumberOfStepsToDesiredResult);
            Assert.Equal(m_TickRemote, clock.LatestRemote);
            Assert.Equal(m_TickRemote - clock.DelayDesired, clock.EstimatedRemote);
        }

        [Fact]
        private void EstimateIsUpdated()
        {
            RailClock clock = new RailClock(1, RailClock.EDesiredDelay.Minimal, 0, 2);

            // Initialize the clock
            clock.UpdateLatest(m_TickRemote);
            Assert.Equal(m_TickRemote, clock.LatestRemote);
            Assert.Equal(m_TickRemote, clock.EstimatedRemote);
            clock.UpdateLatest(++m_TickRemote);
            Assert.Equal(m_TickRemote, clock.LatestRemote);
            Assert.Equal(
                m_TickRemote - 1,
                clock.EstimatedRemote); // Estimate only changed during `Update()`

            clock.Update();
            Assert.Equal(m_TickRemote, clock.LatestRemote);
            Assert.Equal(m_TickRemote, clock.EstimatedRemote); // Estimate was updated

            clock.Update();
            Assert.Equal(m_TickRemote, clock.LatestRemote);
            Assert.Equal(m_TickRemote + 1, clock.EstimatedRemote);

            clock.Update();
            Assert.Equal(m_TickRemote, clock.LatestRemote);
            Assert.Equal(m_TickRemote + 2, clock.EstimatedRemote);

            clock.Update();
            Assert.Equal(m_TickRemote, clock.LatestRemote);
            Assert.Equal(m_TickRemote + 3, clock.EstimatedRemote);

            clock.UpdateLatest(++m_TickRemote);
            Assert.Equal(m_TickRemote, clock.LatestRemote);
            Assert.Equal(m_TickRemote + 2, clock.EstimatedRemote); // Estimate is still unchanged!

            clock.Update();
            Assert.Equal(m_TickRemote, clock.LatestRemote);
            Assert.Equal(m_TickRemote, clock.EstimatedRemote);
        }
    }
}
