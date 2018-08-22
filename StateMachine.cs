using System;
using System.Collections.Generic;

namespace PiMachine
{
    public class StateMachine<TState, TTrigger>
    {
        public TState CurrentState { get; private set; }

        private IList<Permit> Permits = new List<Permit>();
        private IDictionary<TState, Action> OnExit = new Dictionary<TState, Action>();
        private IDictionary<TState, Action> OnEnter = new Dictionary<TState, Action>();

        public delegate void StateDelegate(TState state);
        public event StateDelegate EnteredState;
        public event StateDelegate ExitedState;

        public StateMachine() : this(default(TState))
        {
        }

        public StateMachine(TState initialState)
        {
            this.CurrentState = initialState;

            if (OnEnter.TryGetValue(initialState, out var onEnter))
                onEnter();
        }

        public Configurer Configure(TState state) => new Configurer(state, this);

        public void PermitFromAny(TTrigger trigger, TState state)
            => PermitFromAny(trigger, state, () => true);

        public void PermitFromAny(TTrigger trigger, TState state, Action execute)
            => PermitFromAny(trigger, state, () => { execute(); return true; });

        public void PermitFromAny(TTrigger trigger, TState state, Func<bool> condition)
        {
            Permits.Add(new Permit(trigger, state, condition));
        }

        public void Fire(TTrigger trigger)
        {
            foreach (var permit in Permits)
            {
                if (permit.Original.Equals(CurrentState) && permit.Trigger.Equals(trigger))
                {
                    //Check if we are allowed to change state
                    bool? cont = permit.Execute?.Invoke();

                    //If not, break out
                    if (cont == false)
                        break;

                    //Fire the configuration's exit method
                    if (OnExit.TryGetValue(CurrentState, out var onExit))
                        onExit();

                    //Fire the exit event
                    ExitedState?.Invoke(CurrentState);

                    this.CurrentState = permit.NextState;

                    //Fire the configuration's enter method
                    if (OnEnter.TryGetValue(CurrentState, out var onEnter))
                        onEnter();

                    //Fire the enter event
                    EnteredState?.Invoke(CurrentState);

                    break;
                }
            }
        }

        private struct Permit
        {
            public TState Original;
            public TTrigger Trigger;
            public TState NextState;
            public Func<bool> Execute;
            public bool FromAny;

            public Permit(TState original, TTrigger trigger, TState nextState, Func<bool> execute)
            {
                this.Original = original;
                this.Trigger = trigger;
                this.NextState = nextState;
                this.Execute = execute;
                this.FromAny = false;
            }

            public Permit(TTrigger trigger, TState nextState, Func<bool> execute)
            {
                this.Original = default(TState);
                this.Trigger = trigger;
                this.NextState = nextState;
                this.Execute = execute;
                this.FromAny = true;
            }
        }

        public class Configurer
        {
            private readonly TState State;
            private readonly StateMachine<TState, TTrigger> Machine;

            internal Configurer(TState state, StateMachine<TState, TTrigger> machine)
            {
                this.State = state;
                this.Machine = machine;
            }

            public Configurer OnExit(Action action)
            {
                Machine.OnExit[State] = action;

                return this;
            }

            public Configurer OnEnter(Action action)
            {
                Machine.OnEnter[State] = action;

                return this;
            }

            public Configurer Permit(TTrigger trigger, TState changeTo, Action execute)
                => Permit(trigger, changeTo, () => { execute(); return true; });

            public Configurer Permit(TTrigger trigger, TState changeTo)
                => Permit(trigger, changeTo, () => true);

            public Configurer Permit(TTrigger trigger, TState changeTo, Func<bool> condition)
            {
                Machine.Permits.Add(new Permit(State, trigger, changeTo, condition));

                return this;
            }
        }
    }
}
