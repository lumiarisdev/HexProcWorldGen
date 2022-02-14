using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EconSim;

public class GameHandler : MonoBehaviour
{

    public static GameHandler Instance;

    private StateMachine gsm;

    void Awake() { 
        if(Instance == null) {
            Instance = this;
        } else {
            Destroy(this);
        }
        gsm = new StateMachine();
    }

    // Start is called before the first frame update
    void Start() {
        gsm.MoveNext(Command.StartApp);
    }

    // Update is called once per frame
    void Update()
    {
        switch(gsm.CurrentState) {
            case GameState.Inactive:
            break;
            case GameState.MainMenu:
            break;
            case GameState.Loading:
                // HUDHandler.Instance.HUDGenerate -= WorldMap.Instance.HUDGenerateListener;
                // WorldMap.Instance.Gen.GenerateComplete += GenerateCompleteListener;
            break;
            case GameState.Simulation:
                // WorldMap.Instance.Gen.GenerateComplete -= GenerateCompleteListener;
            break;
            case GameState.Paused:
            break;
            case GameState.Terminated:
            break;
            default:
            break;
        }
    }

    void HUDGenerateListener(object sender, HUDHandler.HUDGenerateEventArgs args) {
        gsm.MoveNext(Command.GenWorld);
    }

    void GenerateCompleteListener(object sender, System.EventArgs args) {
        gsm.MoveNext(Command.StartSim);
    }
    
}

enum GameState {
    Inactive,
    MainMenu,
    Loading,
    Simulation,
    Paused,
    Terminated
}

enum Command {
    StartApp,
    GenWorld,
    StartSim,
    Pause,
    Resume,
    Exit
}

class StateMachine {
    class StateTransition {
        readonly GameState CurrentState;
        readonly Command Command;

        public StateTransition(GameState gState, Command c) {
            CurrentState = gState;
            Command = c;
        }

        public override int GetHashCode()
        {
            return 17 + 31 * CurrentState.GetHashCode() + 31 * Command.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            StateTransition other = obj as StateTransition;
            return other != null && this.CurrentState == other.CurrentState && this.Command == other.Command;
        }

    }

    Dictionary<StateTransition, GameState> transitions;
    public GameState CurrentState { get; private set; }

    public StateMachine() {
        CurrentState = GameState.Inactive;
        transitions = new Dictionary<StateTransition, GameState>{
            // new entries like so
            // { new StateTransition(GameState.MainMenu, Command.GenWorld), GameState.Loading }
            { new StateTransition(GameState.Inactive, Command.StartApp), GameState.MainMenu },
            { new StateTransition(GameState.MainMenu, Command.GenWorld), GameState.Loading },
            { new StateTransition(GameState.Loading, Command.StartSim), GameState.Simulation },
            { new StateTransition(GameState.Simulation, Command.Pause), GameState.Paused },
            { new StateTransition(GameState.Paused, Command.Resume), GameState.Simulation },
            { new StateTransition(GameState.Paused, Command.Exit), GameState.MainMenu },
            { new StateTransition(GameState.MainMenu, Command.Exit), GameState.Terminated }
        };
    }

    public GameState GetNext(Command c) {
        StateTransition transition = new StateTransition(CurrentState, c);
        GameState nextState;
        if(!transitions.TryGetValue(transition, out nextState)) {
            throw new System.Exception("Invalid transition: " + CurrentState + " -> " + c);
        }
        return nextState;
    }

    public GameState MoveNext(Command c) {
        CurrentState = GetNext(c);
        return CurrentState;
    }

}