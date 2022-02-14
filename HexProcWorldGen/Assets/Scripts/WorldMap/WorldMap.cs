using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EconSim {

    public class WorldMap : MonoBehaviour {

        public static WorldMap Instance;

        public WorldGenerator Gen;
        public WorldArgs generatorArgs;
        //public bool DebugMode;

        public WorldMapData worldMapData;
        
        public bool runSim;
        WorldStateMachine wsm;

        private void Awake() {

            if(Instance == null) {
                Instance = this;
            } else {
                Destroy(this);
            }

            Gen = new WorldGenerator(generatorArgs);

            // initialize state machine

            wsm = new WorldStateMachine();

            // define transitions

            wsm.transitions.Add(new WorldStateMachine.WorldStateTransition(
                WorldState.WorldNone, WorldCommand.Generate
            ), new Func<WorldState>(() => {

                // start world generation coroutine and listen to completion event
                Gen.GenerateComplete += GenerateCompleteListener;
                StartCoroutine(Gen.GenerateWorld());

                // return next state
                return WorldState.WorldGenerating;
            }));

            wsm.transitions.Add(new WorldStateMachine.WorldStateTransition(
                WorldState.WorldNone, WorldCommand.Load
            ), new Func<WorldState>(() => {

                // map loading logic

                return WorldState.WorldLoaded;
            }));

            wsm.transitions.Add(new WorldStateMachine.WorldStateTransition(
                WorldState.WorldGenerated, WorldCommand.Start
            ), new Func<WorldState>(() => {

                // start world from freshly generated world

                // bootstrap a freshly generated world
                
                // start the time after everything else is set to go,
                // effectively dropping you into the simulation with the time running
                WorldStart?.Invoke(this, EventArgs.Empty);

                return WorldState.WorldActive;
            }));

            wsm.transitions.Add(new WorldStateMachine.WorldStateTransition(
                WorldState.WorldLoaded, WorldCommand.Start
            ), new Func<WorldState>(() => {

                // start world from loaded world
                WorldStart?.Invoke(this, EventArgs.Empty);

                return WorldState.WorldActive;
            }));

            wsm.transitions.Add(new WorldStateMachine.WorldStateTransition(
                WorldState.WorldActive, WorldCommand.Pause
            ), new Func<WorldState>(() => {

                // pause an active world simulation

                // invoke pause event
                WorldPause?.Invoke(this, EventArgs.Empty);

                return WorldState.WorldPaused;
            }));

            wsm.transitions.Add(new WorldStateMachine.WorldStateTransition(
                WorldState.WorldPaused, WorldCommand.Resume
            ), new Func<WorldState>(() => {

                // resume a paused world

                return WorldState.WorldActive;
            }));

            wsm.transitions.Add(new WorldStateMachine.WorldStateTransition(
                WorldState.WorldPaused, WorldCommand.Unload
            ), new Func<WorldState>(() => {

                // unload and possibly save the currently loaded world

                return WorldState.WorldNone;
            }));

            wsm.transitions.Add(new WorldStateMachine.WorldStateTransition(
                WorldState.WorldGenerating, WorldCommand.GenerateComplete
            ), new Func<WorldState>(() => {

                // any necessary logic after generation  completes
                worldMapData = Gen.WorldData;
                Gen.GenerateComplete -= GenerateCompleteListener;
                WorldLoaded?.Invoke(this, EventArgs.Empty);

                return WorldState.WorldGenerated;
            }));

            // listen for hud button to generate world
            HUDHandler.Instance.HUDGenerate += HUDGenerateListener;

        }

        public event EventHandler WorldLoaded;
        public event EventHandler WorldStart;
        public event EventHandler WorldResume;
        public event EventHandler WorldPause;

        void WorldLoadedListener(object sender, EventArgs args) {
            wsm.MoveNext(WorldCommand.Start);
        }

        // listener for the WorldGenerator.GenerateComplete event
        void GenerateCompleteListener(object sender, System.EventArgs args) {
            wsm.MoveNext(WorldCommand.GenerateComplete);
        }

        public void HUDGenerateListener(object sender, HUDHandler.HUDGenerateEventArgs args) {
            Gen.args.WorldSeed = args.seed > 0 ? args.seed : UnityEngine.Random.Range(0, int.MaxValue);
            Gen.args.RandomizeSeed = args != null ? args.randomSeed : true;
            wsm.MoveNext(WorldCommand.Generate);
        }

        // Start is called before the first frame update
        void Start() {
        }

        // with the correct Time.fixedDeltaTime value, this can be used as the primary simulation tick independent of visuals
        private void FixedUpdate() {
            
            // use state machine to guide current logic
            switch(wsm.CurrentState) {
                case WorldState.WorldGenerating:
                break;
                case WorldState.WorldGenerated:
                break;
                case WorldState.WorldLoaded:
                break;
                case WorldState.WorldActive:

                break;
                case WorldState.WorldPaused:
                break;
                case WorldState.WorldNone:
                break;
                default:
                break;
            }

        }

        // Update is called once per frame
        void Update() {
            
        }

        enum WorldState {
            WorldNone,
            WorldGenerating,
            WorldGenerated,
            WorldLoaded,
            WorldActive,
            WorldPaused,
        }
        
        enum WorldCommand {
            Generate,
            GenerateComplete, // triggered by event
            Load,
            Pause,
            Resume,
            Unload,
            Start,
            Terminate,
        }

        class WorldStateMachine {
            public class WorldStateTransition {
                readonly WorldState CurrentState;
                readonly WorldCommand Command;

                public WorldStateTransition(WorldState wState, WorldCommand c) {
                    CurrentState = wState;
                    Command = c;
                }

                public override int GetHashCode()
                {
                    return 17 + 31 * CurrentState.GetHashCode() + 31 * Command.GetHashCode();
                }

                public override bool Equals(object obj) {
                    WorldStateTransition other = obj as WorldStateTransition;
                    return other != null && this.CurrentState == other.CurrentState && this.Command == other.Command;
                }
                
            }

            public Dictionary<WorldStateTransition, Func<WorldState>> transitions;
            public WorldState CurrentState { get; private set; }

            public WorldStateMachine() {
                CurrentState = WorldState.WorldNone;
                transitions = new Dictionary<WorldStateTransition, Func<WorldState>>();
            }

            public Func<WorldState> GetNext(WorldCommand c) {
                WorldStateTransition transition = new WorldStateTransition(CurrentState, c);
                Func<WorldState> a;
                if(!transitions.TryGetValue(transition, out a)) {
                    throw new System.Exception("Invalid transition: " + CurrentState + " -> " + c);
                }
                return a;
            }

            public WorldState MoveNext(WorldCommand c) {
                var a = GetNext(c);
                CurrentState = a();
                return CurrentState;
            }

        }

    }

}
