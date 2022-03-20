using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Econsim
{

    public class WorldTime : MonoBehaviour
    {

        public WorldTime Instance;

        private DateTime minGameTime;

        private bool run;

        private const float timerMax = .2f;
        private int tick;
        private float timer;


        public event EventHandler<WorldTickEventArgs> WorldTick;
        public class WorldTickEventArgs : EventArgs
        {

        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(this);
            }
            minGameTime = new DateTime(); // wip
            tick = 0;
            run = false;
            EconSim.WorldMap.Instance.WorldStart += WorldStartListener;
            EconSim.WorldMap.Instance.WorldResume += WorldResumeListener;
            EconSim.WorldMap.Instance.WorldPause += WorldPauseListener;
        }

        // Start is called before the first frame update
        void Start()
        {
            tick = 0;
        }

        void FixedUpdate()
        {
            if (run)
            {
                timer += Time.deltaTime;
                if (timer >= timerMax)
                {
                    timer -= timerMax;
                    tick++;
                    WorldTick?.Invoke(this, new WorldTickEventArgs());
                }
            }
        }

        public void WorldStartListener(object sender, EventArgs args)
        {
            run = true;
        }

        public void WorldResumeListener(object sender, EventArgs args) {
            run = true;
        }

        public void WorldPauseListener(object sender, EventArgs args) {
            run = false;
        }
    }

}
